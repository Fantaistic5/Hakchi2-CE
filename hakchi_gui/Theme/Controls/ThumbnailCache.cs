using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Threading;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Vignettes de jaquettes pour la grille. Le decodage (lecture du PNG par l'accesseur existant <c>NesApplication.Image</c>,
    /// puis mise a la taille de la case) se fait en tache de fond ; le resultat est range et signale sur le thread qui a fait
    /// la demande (thread UI). Une jaquette absente ou illisible donne une entree « absente », jamais d'exception.
    /// Eviction LRU au-dela de <c>capacity</c> entrees. Les demandes les plus recentes passent en premier (pile) : ce sont
    /// les cases a l'ecran apres un defilement.
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
            public Image Image; // null = absente
            public LinkedListNode<Key> Node;
        }

        private sealed class Request
        {
            public Key Key;
            public SynchronizationContext Context;
            public readonly List<Action<NesApplication>> Callbacks = new List<Action<NesApplication>>();
            public bool Stale;
        }

        private readonly object gate = new object();
        private readonly int capacity;
        private readonly Dictionary<Key, Entry> map = new Dictionary<Key, Entry>();
        private readonly LinkedList<Key> lru = new LinkedList<Key>(); // tete = plus recent
        private readonly Dictionary<Key, Request> pending = new Dictionary<Key, Request>();
        private readonly LinkedList<Request> queue = new LinkedList<Request>();
        private readonly int maxWorkers = Math.Max(2, Math.Min(8, Environment.ProcessorCount));
        private int workers;
        private bool disposed;

        /// <summary>Lit l'image pleine taille d'un jeu (par defaut l'accesseur de hakchi, qui renvoie null si le fichier manque ou est illisible).</summary>
        public Func<NesApplication, Image> Loader = app => app.Image;

        public ThumbnailCache(int capacity = 512)
        {
            this.capacity = Math.Max(1, capacity);
        }

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

        /// <summary>
        /// Vignette en cache (a la taille exacte demandee), ou null : soit elle est absente (voir <see cref="IsMissing"/>),
        /// soit elle se decode en tache de fond et <paramref name="onReady"/> sera rappele sur le thread appelant.
        /// L'image renvoyee appartient au cache : a utiliser tout de suite (peinture), sans la garder ni la liberer.
        /// </summary>
        public Image Get(NesApplication app, Size size, Action<NesApplication> onReady)
        {
            if (app == null || size.Width <= 0 || size.Height <= 0) return null;
            var key = new Key(app, size);
            bool start = false;
            lock (gate)
            {
                if (disposed) return null;
                Entry e;
                if (map.TryGetValue(key, out e))
                {
                    lru.Remove(e.Node);
                    lru.AddFirst(e.Node);
                    return e.Image;
                }
                Request r;
                if (pending.TryGetValue(key, out r))
                {
                    if (onReady != null && !r.Callbacks.Contains(onReady)) r.Callbacks.Add(onReady);
                    // redemandee : repasse en tete de file
                    var node = queue.Find(r);
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
                Image thumb = Decode(r.Key);
                var ctx = r.Context;
                if (ctx != null)
                {
                    try { ctx.Post(_ => Complete(r, thumb), null); }
                    catch (Exception) { Complete(r, thumb); } // contexte mort (fenetre fermee)
                }
                else Complete(r, thumb);
            }
        }

        private Image Decode(Key k)
        {
            Image src = null;
            try { src = Loader(k.App); }
            catch (Exception) { src = null; }
            if (src == null) return null;
            bool shared = IsShared(k.App, src);
            try
            {
                if (shared) { lock (src) return Scale(src, k.W, k.H); }
                return Scale(src, k.W, k.H);
            }
            catch (Exception) { return null; }
            finally { if (!shared) src.Dispose(); }
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
        /// Met la jaquette a la taille de la case. Proportions proches du 3:4 (+-20 %) : elle remplit la case (leger recadrage
        /// centre). Sinon (affiche paysage, jaquette tres etiree) : elle tient entiere, centree sur RowActive.
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

        private void Complete(Request r, Image thumb)
        {
            List<Action<NesApplication>> callbacks;
            lock (gate)
            {
                Request current;
                if (pending.TryGetValue(r.Key, out current) && ReferenceEquals(current, r)) pending.Remove(r.Key);
                if (disposed || r.Stale)
                {
                    if (thumb != null) thumb.Dispose();
                    if (disposed) return;
                }
                else
                {
                    Remove(r.Key);
                    var e = new Entry { Image = thumb, Node = new LinkedListNode<Key>(r.Key) };
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
