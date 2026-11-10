using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Theme.Controls;
using Xunit;

namespace hakchi_gui.Tests.Grid
{
    public class ThumbnailCacheTests
    {
        private static readonly Size Cell = new Size(120, 160);

        /// <summary>Un controle cree sur ce thread installe le contexte de synchronisation WinForms (rappels sur le thread UI).</summary>
        private static Control UiContext()
        {
            var c = new Control();
            var h = c.Handle;
            // xUnit pose son propre contexte sur le thread de test ; l'application, elle, tourne sous celui de WinForms
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Assert.IsType<WindowsFormsSynchronizationContext>(SynchronizationContext.Current);
            return c;
        }

        [StaFact]
        public void ValidCoverIsDecodedScaledAndCached()
        {
            HakchiEnv.Init();
            using (UiContext())
            using (var cache = new ThumbnailCache())
            {
                var app = TestApp.WithCover("Bleu", Color.FromArgb(10, 20, 200));
                int ready = 0;
                Assert.Null(cache.Get(app, Cell, a => ready++));
                Assert.True(GridFixture.PumpUntil(() => ready == 1));
                var img = cache.Get(app, Cell, a => ready++);
                Assert.NotNull(img);
                Assert.Equal(Cell, img.Size);
                Assert.False(cache.IsMissing(app, Cell));
                Assert.Equal(Color.FromArgb(10, 20, 200).ToArgb(), ((Bitmap)img).GetPixel(60, 80).ToArgb());
                Assert.Equal(1, ready); // pas de second decodage
            }
        }

        [StaFact]
        public void CorruptOrAbsentCoverIsMissingWithoutException()
        {
            HakchiEnv.Init();
            using (UiContext())
            using (var cache = new ThumbnailCache())
            {
                var bad = TestApp.Corrupt("Casse");
                var none = new TestApp("Rien", null, true);
                int ready = 0;
                Assert.Null(cache.Get(bad, Cell, a => ready++));
                Assert.Null(cache.Get(none, Cell, a => ready++));
                Assert.True(GridFixture.PumpUntil(() => ready == 2));
                Assert.Null(cache.Get(bad, Cell, a => ready++));
                Assert.True(cache.IsMissing(bad, Cell));
                Assert.True(cache.IsMissing(none, Cell));
                Assert.Equal(0, cache.PendingCount);
            }
        }

        [StaFact]
        public void LargeCoverDecodedOffUiThread()
        {
            HakchiEnv.Init();
            using (UiContext())
            using (var cache = new ThumbnailCache())
            {
                var app = TestApp.WithCover("Geant", Color.DarkRed, 4000, 5000);
                app.LoadDelay = TimeSpan.FromMilliseconds(300); // en plus du decodage d'un PNG de 20 Mpx
                int ui = Thread.CurrentThread.ManagedThreadId;
                int readyThread = 0;
                var sw = Stopwatch.StartNew();
                var first = cache.Get(app, Cell, a => readyThread = Thread.CurrentThread.ManagedThreadId);
                long getMs = sw.ElapsedMilliseconds;
                Assert.Null(first);
                Assert.True(getMs < 100, "Get a bloque " + getMs + " ms");
                Assert.True(GridFixture.PumpUntil(() => readyThread != 0, 15000));
                Assert.NotEqual(0, app.LoaderThread);
                Assert.NotEqual(ui, app.LoaderThread); // decode hors du thread UI
                Assert.Equal(ui, readyThread);         // rappel sur le thread UI
                Assert.NotNull(cache.Get(app, Cell, null));
            }
        }

        [StaFact]
        public void CacheEvictsLru()
        {
            HakchiEnv.Init();
            using (UiContext())
            using (var cache = new ThumbnailCache(3))
            {
                var apps = new List<TestApp>();
                for (int i = 0; i < 4; i++) apps.Add(TestApp.WithCover("J" + i, Color.FromArgb(40 * i, 50, 60)));
                int ready = 0;
                for (int i = 0; i < 3; i++) // un par un : l'ordre d'arrivee fixe l'ordre LRU
                {
                    cache.Get(apps[i], Cell, a => ready++);
                    Assert.True(GridFixture.PumpUntil(() => ready == i + 1));
                }
                Assert.Equal(3, cache.Count);
                var kept = cache.Get(apps[0], Cell, null); // J0 redevient le plus recent : J1 est le moins recent
                Assert.NotNull(kept);
                cache.Get(apps[3], Cell, a => ready++);
                Assert.True(GridFixture.PumpUntil(() => ready == 4));
                Assert.Equal(3, cache.Count);
                Assert.True(cache.Contains(apps[0], Cell));
                Assert.False(cache.Contains(apps[1], Cell));
                Assert.True(cache.Contains(apps[2], Cell));
                Assert.True(cache.Contains(apps[3], Cell));
            }
        }

        [StaFact]
        public void ForgetDropsEntriesOfOneGame()
        {
            HakchiEnv.Init();
            using (UiContext())
            using (var cache = new ThumbnailCache())
            {
                var a = TestApp.WithCover("A", Color.Green);
                var b = TestApp.WithCover("B", Color.Blue);
                int ready = 0;
                cache.Get(a, Cell, x => ready++); cache.Get(b, Cell, x => ready++);
                Assert.True(GridFixture.PumpUntil(() => ready == 2));
                cache.Forget(a);
                Assert.False(cache.Contains(a, Cell));
                Assert.True(cache.Contains(b, Cell));
            }
        }
    }
}
