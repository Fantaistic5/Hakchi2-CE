using System;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme
{
    /// <summary>
    /// Fenetre de base du theme B1 : habille ses controles (B1Skin) et passe la barre de titre en sombre.
    /// OnHandleCreated couvre les fenetres creees sans passer par Load ; OnLoad rejoue l'habillage apres les
    /// gestionnaires Load, pour les controles qu'ils ajoutent (idempotent).
    /// </summary>
    public class B1Form : Form
    {
        private bool applying;

        private void Skin()
        {
            if (applying) return;
            applying = true;
            try
            {
                B1Skin.Apply(this);
                DarkTitleBar.Enable(this);
            }
            finally { applying = false; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Skin();
        }

        protected override void OnLoad(EventArgs e)
        {
            Skin();
            base.OnLoad(e);
            Skin();
        }
    }
}
