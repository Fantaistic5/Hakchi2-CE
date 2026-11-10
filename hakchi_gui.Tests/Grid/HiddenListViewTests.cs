using System.Collections.Generic;
using System.Windows.Forms;
using Xunit;

namespace hakchi_gui.Tests.Grid
{
    /// <summary>
    /// Le point dur de la grille : listViewGames reste la source de verite mais devient invisible.
    /// Ses evenements (ItemSelectionChanged, ItemCheck, ItemChecked) doivent encore se declencher.
    /// </summary>
    public class HiddenListViewTests
    {
        private static ListView MakeList(Form f, bool visible)
        {
            var lv = new ListView { View = View.Details, CheckBoxes = true, HideSelection = false, Visible = visible };
            lv.Columns.Add("Nom", 200);
            for (int i = 0; i < 5; i++) lv.Items.Add(new ListViewItem("Jeu " + i));
            f.Controls.Add(lv);
            return lv;
        }

        [StaFact]
        public void InvisibleListViewWithoutHandleRaisesNothing()
        {
            using (var f = new Form())
            {
                var lv = MakeList(f, false);
                f.CreateControl();
                lv.CreateControl(); // ne cree rien : le controle n'est pas visible
                Assert.False(lv.IsHandleCreated);
                int sel = 0;
                lv.ItemSelectionChanged += (s, e) => sel++;
                lv.Items[1].Selected = true;
                Assert.Equal(0, sel);
            }
        }

        [StaFact]
        public void InvisibleListViewWithForcedHandleRaisesSelectionAndCheckEvents()
        {
            using (var f = new Form())
            {
                var lv = MakeList(f, false);
                var h = f.Handle; // la fenetre n'est jamais affichee
                h = lv.Handle;    // handle force : CreateControl ne le cree pas pour un controle invisible
                Assert.True(lv.IsHandleCreated);
                Assert.False(lv.Visible);

                var log = new List<string>();
                lv.ItemSelectionChanged += (s, e) => log.Add("sel " + e.ItemIndex + " " + e.IsSelected);
                lv.ItemCheck += (s, e) => log.Add("check " + e.Index + " " + e.NewValue);
                lv.ItemChecked += (s, e) => log.Add("checked " + e.Item.Index + " " + e.Item.Checked);

                lv.Items[1].Selected = true;
                lv.Items[3].Selected = true;
                lv.Items[1].Selected = false;
                lv.Items[2].Checked = true;

                Assert.Equal(new[] { "sel 1 True", "sel 3 True", "sel 1 False", "check 2 Checked", "checked 2 True" }, log);
                Assert.Single(lv.SelectedItems);
                Assert.Equal(3, lv.SelectedItems[0].Index);
                Assert.Single(lv.CheckedItems);
            }
        }
    }
}
