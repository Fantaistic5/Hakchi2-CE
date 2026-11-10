using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace com.clusterrr.hakchi_gui.Theme
{
    /// <summary>
    /// Polices de l'interface PC : Jost (embarquee, licence OFL) avec repli sur Segoe UI.
    ///
    /// Trois instances statiques (theme/retroarch/font.py) : « Jost » (Regular, petites etiquettes),
    /// « Jost Medium » (texte) et « Jost SemiBold » (titres). Chaque graisse est sa propre famille car GDI/GDI+
    /// ne sait pas choisir 500 ou 600 parmi les styles Regular/Bold d'une famille.
    ///
    /// Chargement : PrivateFontCollection.AddMemoryFont pour GDI+ (Graphics.DrawString, controles dessines)
    /// ET AddFontMemResourceEx pour GDI (TextRenderer, Label, Button, menus : tout ce qui n'est pas
    /// UseCompatibleTextRendering). Sans ce second appel, un Label demande « Jost Medium » mais GDI ne le
    /// voit pas et substitue silencieusement une autre police.
    /// Pieges : (1) la memoire passee a AddMemoryFont doit rester valide pour toute la vie du processus
    /// (GDI+ ne la copie pas), donc elle est allouee une fois et jamais liberee ; (2) la PrivateFontCollection
    /// ne doit pas etre eliminee tant que des Font en dependent.
    /// Si quoi que ce soit echoue (ressource absente, police refusee), JostLoaded vaut false et tout retombe
    /// sur Segoe UI, sans exception.
    /// </summary>
    public static class B1Fonts
    {
        public const string FamilyRegular = "Jost";
        public const string FamilyMedium = "Jost Medium";
        public const string FamilySemiBold = "Jost SemiBold";
        public const string Fallback = "Segoe UI";

        /// <summary>Espacement des titres de section en capitales, en em.</summary>
        public const float CapsTracking = 0.14f;

        private const string ResourcePrefix = "com.clusterrr.hakchi_gui.Theme.Fonts.";
        private static readonly string[] FontFiles = { "Jost-Regular.ttf", "Jost-Medium.ttf", "Jost-SemiBold.ttf" };

        /// <summary>
        /// Source des octets d'une police (nom de fichier -> contenu). null = ressources embarquees.
        /// Point d'injection pour les tests (chargeur qui leve une exception) ; appeler Reset() apres changement.
        /// </summary>
        public static Func<string, byte[]> FontSource;

        private static readonly object gate = new object();
        private static PrivateFontCollection defaultCollection;                  // chargement depuis les ressources embarquees, garde a vie
        private static Dictionary<string, FontFamily> defaultFamilies;
        private static volatile bool tried;
        private static volatile bool loaded;
        private static PrivateFontCollection collection;                        // jamais Dispose : des Font en dependent
        private static readonly List<IntPtr> pinned = new List<IntPtr>();       // memoire des polices : jamais liberee
        private static readonly Dictionary<string, FontFamily> families = new Dictionary<string, FontFamily>();

        public static bool JostLoaded
        {
            get { EnsureLoaded(); return loaded; }
        }

        /// <summary>Oublie l'etat de chargement : le prochain acces recharge (depuis FontSource). Pour les tests.</summary>
        public static void Reset()
        {
            lock (gate)
            {
                // Reset est un auxiliaire de test. On ne libere ni la collection ni la memoire (des Font en dependent,
                // et GDI+ ne copie pas la memoire) ; pour ne pas re-epingler a chaque Reset, le chargement depuis les
                // ressources embarquees reussi une fois est conserve et reutilise (defaultCollection).
                tried = false;
                loaded = false;
                collection = null;
                families.Clear();
                B1Skin.ForgetFonts();
            }
        }

        // Les Font retournees appartiennent a l'appelant (Dispose a lui).
        public static Font Body(float pt = 9f) { return Make(FamilyMedium, pt, false, FontStyle.Regular); }
        public static Font Title(float pt = 13f) { return Make(FamilySemiBold, pt, true, FontStyle.Regular); }
        public static Font Small(float pt = 8f) { return Make(FamilyRegular, pt, false, FontStyle.Regular); }
        public static Font Caps(float pt = 7.5f) { return Make(FamilyRegular, pt, false, FontStyle.Regular); }

        /// <summary>
        /// Police B1 equivalente a une police existante : meme taille, memes soulignement / barre ;
        /// gras ou « Jost SemiBold » -> titre, sinon texte. La police par defaut de WinForms (Microsoft Sans Serif 8,25) passe a 9 pt.
        /// Idempotent : Restyle(Restyle(f)) a les memes proprietes que Restyle(f).
        /// </summary>
        public static Font Restyle(Font src)
        {
            // Polices fonctionnelles (code, symboles) : jamais converties, seule la couleur change.
            if (!IsUiFamily(src.FontFamily.Name)) return (Font)src.Clone();
            float pt = src.SizeInPoints;
            if (src.Name == "Microsoft Sans Serif" && Math.Abs(pt - 8.25f) < 0.01f) pt = 9f;
            bool title = src.Bold || src.FontFamily.Name == FamilySemiBold;
            bool small = !title && src.FontFamily.Name == FamilyRegular;
            var keep = src.Style & (FontStyle.Underline | FontStyle.Strikeout | FontStyle.Italic);
            return Make(title ? FamilySemiBold : small ? FamilyRegular : FamilyMedium, pt, title, keep);
        }

        private static readonly HashSet<string> UiFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft Sans Serif", "Segoe UI", "Segoe UI Semibold", "Tahoma", "Arial", "Verdana", "MS Shell Dlg", "MS Shell Dlg 2",
            FamilyRegular, FamilyMedium, FamilySemiBold,
        };

        /// <summary>Familles d'interface que le theme remplace par Jost. Toute autre (Consolas, Courier New, Marlett, Wingdings...) est conservee.</summary>
        public static bool IsUiFamily(string name) { return name != null && UiFamilies.Contains(name); }

        private static Font Make(string jostFamily, float pt, bool bold, FontStyle extra)
        {
            EnsureLoaded();
            FontFamily fam;
            lock (gate) { families.TryGetValue(jostFamily, out fam); }
            if (loaded && fam != null)
            {
                var style = extra;
                if (!fam.IsStyleAvailable(style)) style &= ~FontStyle.Italic;           // pas d'italique dans Jost : on le retire plutot que de lever
                if (fam.IsStyleAvailable(style)) return new Font(fam, pt, style, GraphicsUnit.Point);
            }
            return new Font(Fallback, pt, (bold ? FontStyle.Bold : FontStyle.Regular) | extra, GraphicsUnit.Point);
        }

        private static void EnsureLoaded()
        {
            if (tried) return;
            lock (gate)
            {
                if (tried) return;
                tried = true;
                loaded = false;
                try
                {
                    if (FontSource == null && defaultFamilies != null)
                    {
                        families.Clear();
                        foreach (var kv in defaultFamilies) families[kv.Key] = kv.Value;
                        collection = defaultCollection;
                        loaded = true;
                        return;
                    }
                    var col = new PrivateFontCollection();
                    var source = FontSource ?? ReadResource;
                    foreach (var file in FontFiles)
                    {
                        var data = source(file);
                        if (data == null || data.Length < 64) throw new InvalidDataException("police absente : " + file);
                        var mem = Marshal.AllocCoTaskMem(data.Length);          // epinglee a vie : AddMemoryFont ne copie pas
                        Marshal.Copy(data, 0, mem, data.Length);
                        pinned.Add(mem);
                        col.AddMemoryFont(mem, data.Length);                    // GDI+ ; leve si le contenu n'est pas une police
                        NativeTheme.AddFontToGdi(mem, data.Length);             // GDI (TextRenderer) ; echec non fatal
                    }
                    var found = new Dictionary<string, FontFamily>();
                    foreach (var f in col.Families) found[f.Name] = f;
                    if (!found.ContainsKey(FamilyRegular) || !found.ContainsKey(FamilyMedium) || !found.ContainsKey(FamilySemiBold))
                        throw new InvalidDataException("familles Jost incompletes");
                    collection = col;
                    families.Clear();
                    foreach (var kv in found) families[kv.Key] = kv.Value;
                    loaded = true;
                    if (FontSource == null) { defaultCollection = col; defaultFamilies = new Dictionary<string, FontFamily>(found); }
                }
                catch (Exception)
                {
                    loaded = false;
                    collection = null;
                    families.Clear();
                }
            }
        }

        private static byte[] ReadResource(string file)
        {
            using (var s = typeof(B1Fonts).Assembly.GetManifestResourceStream(ResourcePrefix + file))
            {
                if (s == null) return null;
                var buf = new byte[s.Length];
                int off = 0;
                while (off < buf.Length)
                {
                    int n = s.Read(buf, off, buf.Length - off);
                    if (n <= 0) break;
                    off += n;
                }
                return buf;
            }
        }
    }
}
