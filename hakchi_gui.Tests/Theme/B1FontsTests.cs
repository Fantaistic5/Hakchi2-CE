using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;
using Xunit;

namespace hakchi_gui.Tests.Theme
{
    [Collection("B1Static")]
    public class B1FontsTests
    {
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern int GetTextFace(IntPtr hdc, int c, StringBuilder name);

        [StaFact]
        public void FontsFallbackWhenJostMissing()
        {
            try
            {
                B1Fonts.FontSource = name => { throw new InvalidOperationException("police bloquee (test)"); };
                B1Fonts.Reset();
                Assert.False(B1Fonts.JostLoaded);
                using (var body = B1Fonts.Body())
                using (var title = B1Fonts.Title())
                using (var small = B1Fonts.Small())
                using (var caps = B1Fonts.Caps())
                {
                    Assert.Equal("Segoe UI", body.FontFamily.Name);
                    Assert.Equal(9f, body.SizeInPoints, 1);
                    Assert.Equal("Segoe UI", title.FontFamily.Name);
                    Assert.Equal("Segoe UI", small.FontFamily.Name);
                    Assert.Equal("Segoe UI", caps.FontFamily.Name);
                }
            }
            finally { B1Fonts.FontSource = null; B1Fonts.Reset(); }
        }

        [StaFact]
        public void FontsFallbackWhenFontDataIsGarbage()
        {
            try
            {
                B1Fonts.FontSource = name => new byte[] { 1, 2, 3, 4 };
                B1Fonts.Reset();
                Assert.False(B1Fonts.JostLoaded);
                Assert.Equal("Segoe UI", B1Fonts.Body().FontFamily.Name);
            }
            finally { B1Fonts.FontSource = null; B1Fonts.Reset(); }
        }

        [StaFact]
        public void JostLoadsFromEmbeddedResources()
        {
            B1Fonts.FontSource = null;
            B1Fonts.Reset();
            Assert.True(B1Fonts.JostLoaded);
            using (var body = B1Fonts.Body()) { Assert.Equal("Jost Medium", body.FontFamily.Name); Assert.Equal(9f, body.SizeInPoints, 1); }
            using (var title = B1Fonts.Title()) { Assert.Equal("Jost SemiBold", title.FontFamily.Name); Assert.Equal(13f, title.SizeInPoints, 1); }
            using (var small = B1Fonts.Small()) { Assert.Equal("Jost", small.FontFamily.Name); Assert.Equal(8f, small.SizeInPoints, 1); }
            using (var caps = B1Fonts.Caps()) { Assert.Equal("Jost", caps.FontFamily.Name); Assert.Equal(7.5f, caps.SizeInPoints, 1); }
        }

        // TextRenderer / Label passent par GDI : la police privee doit aussi etre visible de GDI (AddFontMemResourceEx).
        [StaFact]
        public void GdiSeesJostFaces()
        {
            B1Fonts.FontSource = null;
            B1Fonts.Reset();
            Assert.True(B1Fonts.JostLoaded);
            foreach (var pair in new[] { Tuple.Create(B1Fonts.Body(), "Jost Medium"), Tuple.Create(B1Fonts.Title(), "Jost SemiBold"), Tuple.Create(B1Fonts.Small(), "Jost") })
            {
                var hdc = GetDC(IntPtr.Zero);
                var hfont = pair.Item1.ToHfont();
                var old = SelectObject(hdc, hfont);
                var sb = new StringBuilder(64);
                GetTextFace(hdc, 64, sb);
                SelectObject(hdc, old);
                DeleteObject(hfont);
                ReleaseDC(IntPtr.Zero, hdc);
                Assert.Equal(pair.Item2, sb.ToString());
            }
        }
    }
}
