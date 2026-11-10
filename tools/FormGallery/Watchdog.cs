using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FormGallery
{
    /// <summary>
    /// La galerie ne montre jamais de fenetre : toute fenetre visible sur le thread UI est un dialogue parasite
    /// (MessageBox, ErrorForm...) ouvert par un constructeur. On le ferme et on le note, pour ne jamais se bloquer.
    /// </summary>
    internal static class Watchdog
    {
        private delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        private const uint WM_CLOSE = 0x0010;
        private const uint WM_COMMAND = 0x0111;

        private static uint uiThread;
        private static Timer timer;
        private static readonly List<string> dismissed = new List<string>();
        private static readonly object gate = new object();

        private static readonly HashSet<IntPtr> owned = new HashSet<IntPtr>();
        private static readonly Dictionary<IntPtr, DateTime> firstSeen = new Dictionary<IntPtr, DateTime>();

        /// <summary>Fenetre de la galerie elle-meme (affichee hors ecran) : a ne pas fermer.</summary>
        public static void Own(IntPtr hWnd) { lock (gate) owned.Add(hWnd); }

        public static void Start()
        {
            uiThread = GetCurrentThreadId();
            timer = new Timer(_ => Tick(), null, 1500, 1500);
        }

        /// <summary>Retourne (et vide) les dialogues fermes depuis le dernier appel.</summary>
        public static List<string> TakeDismissed()
        {
            lock (gate)
            {
                var copy = new List<string>(dismissed);
                dismissed.Clear();
                return copy;
            }
        }

        private static void Tick()
        {
            EnumThreadWindows(uiThread, (h, l) =>
            {
                if (!IsWindowVisible(h)) return true;
                lock (gate)
                {
                    if (owned.Contains(h)) return true;
                    // Une fenetre de chargement peut apparaitre puis se fermer seule : on lui laisse 4 s.
                    DateTime first;
                    if (!firstSeen.TryGetValue(h, out first)) { firstSeen[h] = DateTime.UtcNow; return true; }
                    if ((DateTime.UtcNow - first).TotalSeconds < 4) return true;
                    firstSeen.Remove(h);
                }
                var cls = new StringBuilder(128);
                var txt = new StringBuilder(256);
                GetClassName(h, cls, cls.Capacity);
                GetWindowText(h, txt, txt.Capacity);
                lock (gate) dismissed.Add(cls + " \"" + txt + "\"");
                PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                if (cls.ToString() == "#32770") PostMessage(h, WM_COMMAND, (IntPtr)2 /*IDCANCEL*/, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);
        }
    }
}
