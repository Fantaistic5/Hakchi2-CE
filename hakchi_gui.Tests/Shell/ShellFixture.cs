using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;

namespace hakchi_gui.Tests.Shell
{
    internal static class ShellFixture
    {
        /// <summary>Fenetre jamais affichee, controle ajoute, handle cree.</summary>
        public static Form Host<T>(T control, Size client) where T : Control
        {
            var f = new Form { ClientSize = client, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ShowInTaskbar = false, BackColor = B1Palette.Slate };
            f.Controls.Add(control);
            var h = f.Handle;
            h = control.Handle;
            f.PerformLayout();
            return f;
        }

        public static Bitmap Draw(Control c)
        {
            var bmp = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppArgb);
            c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
            return bmp;
        }

        public static void Click(Control c, Point p, MouseButtons b = MouseButtons.Left)
        {
            Invoke(c, "OnMouseMove", new MouseEventArgs(MouseButtons.None, 0, p.X, p.Y, 0));
            Invoke(c, "OnMouseDown", new MouseEventArgs(b, 1, p.X, p.Y, 0));
            Invoke(c, "OnMouseUp", new MouseEventArgs(b, 1, p.X, p.Y, 0));
        }

        public static Point Center(Rectangle r) { return new Point(r.X + r.Width / 2, r.Y + r.Height / 2); }

        private static void Invoke(Control c, string method, object arg)
        {
            var m = c.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            m.Invoke(c, new[] { arg });
        }

        public static void Key(Control c, Keys k) { Invoke(c, "OnKeyDown", new KeyEventArgs(k)); }
    }
}
