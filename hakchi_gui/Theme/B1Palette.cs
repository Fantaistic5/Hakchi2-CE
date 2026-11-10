using System.Collections.Generic;
using System.Drawing;

namespace com.clusterrr.hakchi_gui.Theme
{
    /// <summary>
    /// Palette B1 de l'interface PC : exactement les 16 couleurs de la maquette, aucune autre.
    /// Tout le theme (B1Skin, B1ToolStripRenderer, controles dessines) ne peint qu'avec ces valeurs.
    /// </summary>
    public static class B1Palette
    {
        // fonds
        public static readonly Color Slate = Color.FromArgb(0x1C, 0x1B, 0x22);      // fond
        public static readonly Color RowActive = Color.FromArgb(0x26, 0x25, 0x2E);  // ligne active, champs de saisie
        public static readonly Color Rule = Color.FromArgb(0x2C, 0x2A, 0x34);       // filet

        // accents
        public static readonly Color Primary = Color.FromArgb(0x4A, 0x3C, 0x8C);    // bouton primaire
        public static readonly Color Accent = Color.FromArgb(0x6B, 0x5B, 0xB5);     // accent, barre
        public static readonly Color Lavender = Color.FromArgb(0xA7, 0x9F, 0xD6);   // lavande
        public static readonly Color Dashed = Color.FromArgb(0x5C, 0x58, 0x70);     // pointille, bordures

        // textes
        public static readonly Color Text = Color.FromArgb(0xE4, 0xE2, 0xEA);
        public static readonly Color TextOnPrimary = Color.FromArgb(0xEC, 0xE9, 0xF8);
        public static readonly Color TextSecondary = Color.FromArgb(0xB6, 0xB2, 0xC4);
        public static readonly Color TextDim = Color.FromArgb(0x8E, 0x8A, 0x9C);
        public static readonly Color TextFaint = Color.FromArgb(0x6F, 0x6B, 0x7E);

        // hachures « sans jaquette »
        public static readonly Color HatchA = Color.FromArgb(0x24, 0x23, 0x2B);
        public static readonly Color HatchB = Color.FromArgb(0x2A, 0x29, 0x32);

        // etat
        public static readonly Color Online = Color.FromArgb(0x7B, 0xC4, 0x7F);     // console en ligne (pastille seulement)
        public static readonly Color Error = Color.FromArgb(0xD9, 0x77, 0x6A);

        public static readonly IReadOnlyList<Color> All = new List<Color>
        {
            Slate, RowActive, Rule,
            Primary, Accent, Lavender, Dashed,
            Text, TextOnPrimary, TextSecondary, TextDim, TextFaint,
            HatchA, HatchB,
            Online, Error,
        }.AsReadOnly();
    }
}
