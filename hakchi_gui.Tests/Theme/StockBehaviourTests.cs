using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using Xunit;

namespace hakchi_gui.Tests.Theme
{
    /// <summary>Apres B1Skin.Apply, les controles standard (handles crees, fenetre affichee) gardent leur comportement.</summary>
    [Collection("B1Static")]
    public class StockBehaviourTests
    {
        private static Form Host(Control c, Size size)
        {
            var f = new Form
            {
                FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ShowInTaskbar = false,
                ClientSize = new Size(500, 400),
            };
            c.Bounds = new Rectangle(8, 8, size.Width, size.Height);
            f.Controls.Add(c);
            B1Skin.Apply(f);
            f.Show();
            var h = c.Handle;
            Application.DoEvents();
            return f;
        }

        private static void Paint(Control c)
        {
            using (var bmp = new Bitmap(c.Width, c.Height, PixelFormat.Format24bppRgb)) c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
        }

        [StaFact]
        public void CheckedDetailsListViewKeepsItsBehaviour()
        {
            var lv = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true };
            lv.Columns.Add("Jeu", 150); lv.Columns.Add("Taille", 80);
            for (int i = 0; i < 5; i++) lv.Items.Add(new ListViewItem(new[] { "Jeu " + i, i + " Ko" }));
            var log = new List<string>();
            lv.ItemCheck += (s, e) => log.Add("check" + e.Index + ":" + e.NewValue);
            lv.ItemChecked += (s, e) => log.Add("checked" + e.Item.Index + ":" + e.Item.Checked);
            int selEvents = 0, selectedNow = 0;
            lv.ItemSelectionChanged += (s, e) => { selEvents++; if (e.IsSelected) selectedNow++; };
            using (var f = Host(lv, new Size(300, 160)))
            {
                log.Clear(); selEvents = 0; selectedNow = 0;          // evenements de creation du handle
                lv.Items[0].Checked = true;
                Assert.Equal(new[] { "check0:Checked", "checked0:True" }, log);
                Assert.Single(lv.CheckedItems);

                lv.Items[2].Selected = true;
                lv.Items[2].Focused = true;
                Assert.Single(lv.SelectedItems);
                Assert.Same(lv.Items[2], lv.SelectedItems[0]);
                Assert.Same(lv.Items[2], lv.FocusedItem);
                Assert.True(selEvents >= 1 && selectedNow == 1);

                Paint(lv);                                          // peinture (NM_CUSTOMDRAW) : aucun effet de bord
                Assert.Single(lv.SelectedItems);
                Assert.Same(lv.Items[2], lv.FocusedItem);
                Assert.Single(lv.CheckedItems);

                lv.Items[2].Checked = true;
                lv.Items[0].Checked = false;
                Assert.Equal(new[] { "check0:Checked", "checked0:True", "check2:Checked", "checked2:True", "check0:Unchecked", "checked0:False" }, log);
                Assert.Same(lv.Items[2], lv.CheckedItems[0]);
                lv.Items[2].Selected = false;
                Assert.Empty(lv.SelectedItems);
            }
        }

        [StaTheory]
        [InlineData(ComboBoxStyle.DropDownList)]
        [InlineData(ComboBoxStyle.DropDown)]
        public void ComboBoxKeepsItsBehaviour(ComboBoxStyle style)
        {
            var c = new ComboBox { DropDownStyle = style };
            c.Items.AddRange(new object[] { "Un", "Deux", "Trois" });
            int changed = 0, textChanged = 0;
            c.SelectedIndexChanged += (s, e) => changed++;
            c.TextChanged += (s, e) => textChanged++;
            using (var f = Host(c, new Size(160, 24)))
            {
                Assert.Equal(DrawMode.OwnerDrawFixed, c.DrawMode);
                changed = 0; textChanged = 0;
                c.SelectedIndex = 1;
                Assert.Equal(1, changed);
                Assert.Equal("Deux", c.Text);
                Assert.Equal("Deux", c.SelectedItem);
                Paint(c);
                Assert.Equal(1, c.SelectedIndex);
                Assert.Equal(1, changed);
                if (style == ComboBoxStyle.DropDown)
                {
                    c.Text = "Saisie libre";                       // equivalent de la frappe : le champ d edition garde sa valeur
                    Assert.Equal("Saisie libre", c.Text);
                    Assert.True(textChanged >= 1);
                    c.SelectedIndex = 2;
                    Assert.Equal("Trois", c.Text);
                }
                else
                {
                    c.SelectedIndex = 0;
                    Assert.Equal("Un", c.Text);
                    Assert.Equal(2, changed);
                }
            }
        }

        /// <summary>
        /// Une liste deroulante cachee (fiche de MainForm au demarrage) ne recoit pas de handle a l'habillage :
        /// le setter ItemHeight en creait un, avec ceux de ses parents (~150 ms au demarrage de MainForm).
        /// La hauteur native conservee est posee quand le handle est cree.
        /// </summary>
        [StaFact]
        public void SkinningHiddenComboBoxDefersHandleAndKeepsItemHeight()
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(8, 8, 160, 24) };
            combo.Items.AddRange(new object[] { "Un", "Deux", "Trois" });
            var panel = new Panel { Visible = false, Bounds = new Rectangle(0, 0, 300, 100) };
            panel.Controls.Add(combo);
            using (var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ShowInTaskbar = false })
            {
                f.Controls.Add(panel);
                var formHandle = f.Handle;                       // comme B1Form : habillage a la creation du handle de la fenetre
                B1Skin.Apply(f);
                int expected = combo.ItemHeight;                 // hauteur retenue par l'habillage (police B1)
                Assert.False(combo.IsHandleCreated);
                Assert.False(panel.IsHandleCreated);
                Assert.Equal(DrawMode.OwnerDrawFixed, combo.DrawMode);

                f.Show();
                panel.Visible = true;
                Application.DoEvents();
                Assert.True(combo.IsHandleCreated);
                Assert.Equal(expected, combo.ItemHeight);
                Paint(combo);
                combo.SelectedIndex = 1;
                Assert.Equal("Deux", combo.Text);
            }
        }

        [StaFact]
        public void NumericUpDownKeepsItsBehaviour()
        {
            var n = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 42 };
            int changed = 0;
            n.ValueChanged += (s, e) => changed++;
            using (var f = Host(n, new Size(100, 24)))
            {
                Paint(n);
                Assert.Equal(42, n.Value);
                n.Value = 7;
                Assert.Equal(1, changed);
                Assert.Equal("7", n.Text);
                n.Text = "12";                                      // saisie puis validation, comme en quittant le champ
                typeof(UpDownBase).GetMethod("ValidateEditText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(n, null);
                Assert.Equal(12, n.Value);
                Assert.Equal(2, changed);
                n.UpButton();
                Assert.Equal(13, n.Value);
                n.DownButton();
                n.DownButton();
                Assert.Equal(11, n.Value);
            }
        }

        [StaFact]
        public void ButtonKeepsItsBehaviour()
        {
            var b = new Button { Text = "OK", DialogResult = DialogResult.Retry };
            int clicks = 0;
            b.Click += (s, e) => clicks++;
            using (var f = Host(b, new Size(100, 28)))
            {
                Paint(b);
                b.PerformClick();
                Assert.Equal(1, clicks);
                Assert.Equal(DialogResult.Retry, b.DialogResult);
                Assert.Equal("OK", b.Text);
                Assert.True(b.Enabled);
            }
        }

        [StaFact]
        public void ProgressTimerFollowsTheHandle()
        {
            var p = new ProgressBar { Style = ProgressBarStyle.Marquee };
            using (var f = Host(p, new Size(200, 16)))
            {
                var w = ProgressTimer(p);
                Assert.True(w, "le minuteur tourne avec le handle");
                p.Style = ProgressBarStyle.Continuous;
                Paint(p);
                // recreation du handle : le minuteur repart
                var m = typeof(Control).GetMethod("RecreateHandle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                m.Invoke(p, null);
                Application.DoEvents();
                Assert.True(ProgressTimer(p));
            }
            Assert.False(ProgressTimer(p), "arrete et libere apres la destruction");
        }

        // lit l etat du minuteur du ProgressWindow attache (champ interne)
        private static bool ProgressTimer(ProgressBar p)
        {
            var tableField = typeof(Subclasser).GetField("table", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var table = tableField.GetValue(null);
            var tryGet = table.GetType().GetMethod("TryGetValue");
            var args = new object[] { p, null };
            if (!(bool)tryGet.Invoke(table, args)) return false;
            var dict = (Dictionary<string, NativeWindow>)args[1];
            NativeWindow w;
            if (!dict.TryGetValue("progress", out w)) return false;
            return ((ProgressWindow)w).TimerRunning;
        }
    }
}
