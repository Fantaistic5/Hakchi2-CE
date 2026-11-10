using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Vignettes de jaquettes pour la grille. Le decodage se fait en tache de fond (2 ouvriers au plus : une jaquette pleine
    /// taille en memoire par ouvrier) ; le resultat est range et signale sur le thread UI qui a cree le cache.
    /// <para>
    /// Taille quantifiee : la grille demande une seule taille par echelle DPI (<see cref="BucketSize"/>, la plus grande case) et
    /// met a l'echelle au dessin ; redimensionner la fenetre ne redecode donc rien. Si la vignette d'une autre taille existe
    /// (changement de DPI), <see cref="GetAny"/> la rend en attendant la nouvelle.
    /// </para>
    /// <para>
    /// Jaquette absente (l'accesseur de hakchi rend null) : entree « absente » definitive. Echec de lecture (fichier verrouille
    /// pendant sa reecriture, PNG illisible) : entree « absente » elle aussi, mais retentee au premier Get apres
    /// <see cref="RetryAfter"/>. Jamais d'exception. Eviction LRU au-dela de <c>capacity</c> entrees.
    /// </para>
    /// </summary>
    public sealed class ThumbnailCache : IDisposable
    {
        private struct Key : IEquatable<Key>
        {
            public readonly NesApplication App;
            public readonly int W, H;
            public Key(NesApplication app, Size size) { App = app; W = size.Width; H = size.Height; }
            public bool Equals(Key o) { return ReferenceEquals(App, o.App) && W == o.W && H == o.H; }
            public override bool Equals(object obj) { return obj is Key && Equals((Key)obj); }
            public override int GetHashCode() { return (RuntimeHelpers.GetHashCode(App) * 397) ^ (W * 31 + H); }
        }

        private sealed class Entry
        {
            public Image Image;      // null = absente
            public bool Transient;   // echec de lecture : a retenter
            public DateTime At;      // derniere tentative
            public LinkedListNode<Key> Node;
        }

        private sealed class Request
        {
            public Key Key;
            public SynchronizationContext Context;
            public readonly List<Action<NesApplication>> Callbacks = new List<Action<NesApplication>>();
            public bool Stale;
        }

        private sealed class Result { public Image Image; public bool Transient; }

        private readonly object gate = new object();
        private readonly int capacity;
        private readonly int maxWorkers;
        private readonly int ownerThread;
        private readonly Dictionary<Key, Entry> map = new Dictionary<Key, Entry>();
        private readonly LinkedList<Key> lru = new LinkedList<Key>(); // tete = plus recent
        private readonly Dictionary<Key, Request> pending = new Dictionary<Key, Request>();
        private readonly LinkedList<Request> queue = new LinkedList<Request>();
        private int workers;
        private bool disposed;

        /// <summary>
        /// Lecteur de remplacement (tests). Null par defaut : le fichier de jaquette que hakchi a calcule (champ iconPath) est
        /// lu en flux puis libere aussitot la vignette faite ; s'il n'existe pas, l'accesseur <c>NesApplication.Image</c>
        /// decide (jaquette d'origine en cache, jaquette par defaut du systeme...).
        /// </summary>
        public Func<NesApplication, Image> Loader;

        /// <summary>Delai avant de retenter une lecture qui a echoue (2 s).</summary>
        public TimeSpan RetryAfter = TimeSpan.FromSeconds(2);

        public ThumbnailCache(int capacity = 512, int maxConcurrency = 2)
        {
            this.capacity = Math.Max(1, capacity);
            maxWorkers = Math.Max(1, maxConcurrency);
            ownerThread = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>Taille unique des vignettes pour une echelle : la plus grande case (160 px a 96 dpi), en 3:4.</summary>
        public static Size BucketSize(float dpiScale)
        {
            int w = B1Dpi.Px(CoverGridLayout.MaxCell, dpiScale > 0 ? dpiScale : 1f);
            return new Size(w, (int)Math.Round(w * 4 / 3.0, MidpointRounding.AwayFromZero));
        }

        public int MaxConcurrency { get { return maxWorkers; } }
        public int Count { get { lock (gate) return map.Count; } }
        public int PendingCount { get { lock (gate) return pending.Count; } }

        public bool Contains(NesApplication app, Size size)
        {
            lock (gate) return app != null && map.ContainsKey(new Key(app, size));
        }

        /// <summary>Vrai si la jaquette a ete lue et qu'elle est absente ou illisible.</summary>
        public bool IsMissing(NesApplication app, Size size)
        {
            if (app == null) return true;
            lock (gate)
            {
                Entry e;
                return map.TryGetValue(new Key(app, size), out e) && e.Image == null;
            }
        }

        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("ThumbnailCache.Get doit etre appele depuis le thread UI qui a cree le cache.");
        }

        /// <summary>
        /// Vignette en cache a la taille demandee, ou null : soit elle est absente (voir <see cref="IsMissing"/>), soit elle se
        /// decode en tache de fond et <paramref name="onReady"/> sera rappele sur le thread UI. Thread UI seulement.
        /// L'image renvoyee appartient au cache : a utiliser tout de suite (peinture), sans la garder ni la liberer.
        /// </summary>
        public Image Get(NesApplication app, Size size, Action<NesApplication> onReady)
        {
            CheckThread();
            if (app == null || size.Width <= 0 || size.Height <= 0) return null;
            var key = new Key(app, size);
            bool start = false;
            lock (gate)
            {
                if (disposed) return null;
                Entry e;
                Request r;
                if (map.TryGetValue(key, out e))
                {
                    lru.Remove(e.Node);
                    lru.AddFirst(e.Node);
                    bool retry = e.Image == null && e.Transient && DateTime.UtcNow - e.At >= RetryAfter && !pending.ContainsKey(key);
                    if (!retry) return e.Image;
                    e.At = DateTime.UtcNow; // l'entree reste « absente » jusqu'au resultat de la nouvelle lecture
                }
                else if (pending.TryGetValue(key, out r))
                {
                    if (onReady != null && !r.Callbacks.Contains(onReady)) r.Callbacks.Add(onReady);
                    var node = queue.Find(r); // redemandee : repasse en tete de file
                    if (node != null) { queue.Remove(node); queue.AddFirst(node); }
                    return null;
                }
                r = new Request { Key = key, Context = SynchronizationContext.Current };
                if (onReady != null) r.Callbacks.Add(onReady);
                pending[key] = r;
                queue.AddFirst(r);
                if (workers < maxWorkers) { workers++; start = true; }
            }
            if (start) ThreadPool.QueueUserWorkItem(_ => Work());
            return null;
        }

        /// <summary>Une vignette quelconque du jeu deja en cache (autre taille), pour patienter pendant un nouveau decodage.</summary>
        public Image GetAny(NesApplication app)
        {
            CheckThread();
            if (app == null) return null;
            lock (gate)
            {
                foreach (var kv in map)
                    if (ReferenceEquals(kv.Key.App, app) && kv.Value.Image != null) return kv.Value.Image;
            }
            return null;
        }

        /// <summary>Oublie les vignettes d'un jeu (jaquette changee).</summary>
        public void Forget(NesApplication app)
        {
            if (app == null) return;
            lock (gate)
            {
                var drop = new List<Key>();
                foreach (var k in map.Keys) if (ReferenceEquals(k.App, app)) drop.Add(k);
                foreach (var k in drop) Remove(k);
                foreach (var r in pending.Values) if (ReferenceEquals(r.Key.App, app)) r.Stale = true;
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                foreach (var e in map.Values) if (e.Image != null) e.Image.Dispose();
                map.Clear();
                lru.Clear();
                foreach (var r in pending.Values) r.Stale = true;
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                Clear();
                disposed = true;
                queue.Clear();
            }
        }

        private void Remove(Key k)
        {
            Entry e;
            if (!map.TryGetValue(k, out e)) return;
            map.Remove(k);
            lru.Remove(e.Node);
            if (e.Image != null) e.Image.Dispose();
        }

        private void Work()
        {
            while (true)
            {
                Request r;
                lock (gate)
                {
                    if (disposed || queue.Count == 0) { workers--; return; }
                    r = queue.First.Value;
                    queue.RemoveFirst();
                }
                var res = Decode(r.Key);
                var ctx = r.Context;
                if (ctx != null)
                {
                    try { ctx.Post(_ => Complete(r, res), null); }
                    catch (Exception) { Complete(r, res); } // contexte mort (fenetre fermee)
                }
                else Complete(r, res);
            }
        }

        private static readonly FieldInfo iconPathField = typeof(NesMenuElementBase).GetField("iconPath", BindingFlags.Instance | BindingFlags.NonPublic);

        private Result Decode(Key k)
        {
            var custom = Loader;
            try
            {
                if (custom == null)
                {
                    // chemin calcule par hakchi ; lu en flux, sans la copie pleine taille de Shared.LoadBitmapCopy
                    var path = iconPathField != null ? iconPathField.GetValue(k.App) as string : null;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
                        using (var img = Image.FromStream(fs, false, false))
                            return new Result { Image = Scale(img, k.W, k.H) };
                    }
                }
                var src = custom != null ? custom(k.App) : k.App.Image;
                if (src == null) return new Result(); // pas de jaquette : definitif
                bool shared = IsShared(k.App, src);
                try
                {
                    if (shared) { lock (src) return new Result { Image = Scale(src, k.W, k.H) }; }
                    return new Result { Image = Scale(src, k.W, k.H) };
                }
                finally { if (!shared) src.Dispose(); }
            }
            catch (Exception)
            {
                return new Result { Transient = true }; // verrouille, en cours d'ecriture, illisible : a retenter
            }
        }

        /// <summary>Jaquette par defaut d'un systeme : instance partagee par hakchi, a ne jamais liberer.</summary>
        private static bool IsShared(NesApplication app, Image img)
        {
            try
            {
                foreach (var a in AppTypeCollection.Apps) if (ReferenceEquals(a.DefaultCover, img)) return true;
                if (ReferenceEquals(AppTypeCollection.UnknownApp.DefaultCover, img)) return true;
                if (app.Metadata != null && ReferenceEquals(app.Metadata.AppInfo.DefaultCover, img)) return true;
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>
        /// Met la jaquette a la taille de la vignette. Proportions proches du 3:4 (+-20 %) : elle remplit la case (leger
        /// recadrage centre). Sinon (boite paysage, jaquette tres etiree) : elle tient entiere, centree sur RowActive.
        /// </summary>
        internal static Bitmap Scale(Image src, int w, int h)
        {
            var dst = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            try
            {
                using (var g = Graphics.FromImage(dst))
                using (var attr = new ImageAttributes())
                {
                    attr.SetWrapMode(WrapMode.TileFlipXY); // pas de liseré clair sur les bords
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.Clear(B1Palette.RowActive);
                    double sa = src.Width / (double)src.Height, da = w / (double)h;
                    Rectangle srcRect, dstRect;
                    if (Math.Abs(sa / da - 1) <= 0.2)
                    {
                        if (sa > da) { int sw = (int)Math.Round(src.Height * da); srcRect = new Rectangle((src.Width - sw) / 2, 0, sw, src.Height); }
                        else { int sh = (int)Math.Round(src.Width / da); srcRect = new Rectangle(0, (src.Height - sh) / 2, src.Width, sh); }
                        dstRect = new Rectangle(0, 0, w, h);
                    }
                    else
                    {
                        srcRect = new Rectangle(0, 0, src.Width, src.Height);
                        if (sa > da) { int dh = Math.Max(1, (int)Math.Round(w / sa)); dstRect = new Rectangle(0, (h - dh) / 2, w, dh); }
                        else { int dw = Math.Max(1, (int)Math.Round(h * sa)); dstRect = new Rectangle((w - dw) / 2, 0, dw, h); }
                    }
                    g.DrawImage(src, dstRect, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, GraphicsUnit.Pixel, attr);
                }
                return dst;
            }
            catch
            {
                dst.Dispose();
                throw;
            }
        }

        private void Complete(Request r, Result res)
        {
            List<Action<NesApplication>> callbacks;
            lock (gate)
            {
                Request current;
                if (pending.TryGetValue(r.Key, out current) && ReferenceEquals(current, r)) pending.Remove(r.Key);
                if (disposed || r.Stale)
                {
                    if (res.Image != null) res.Image.Dispose();
                    if (disposed) return;
                }
                else
                {
                    Remove(r.Key);
                    var e = new Entry { Image = res.Image, Transient = res.Transient, At = DateTime.UtcNow, Node = new LinkedListNode<Key>(r.Key) };
                    map[r.Key] = e;
                    lru.AddFirst(e.Node);
                    while (map.Count > capacity) Remove(lru.Last.Value);
                }
                callbacks = new List<Action<NesApplication>>(r.Callbacks);
            }
            foreach (var cb in callbacks)
            {
                try { cb(r.Key.App); } catch (Exception ex) { System.Diagnostics.Trace.WriteLine("ThumbnailCache callback: " + ex.Message); }
            }
        }
    }
}
