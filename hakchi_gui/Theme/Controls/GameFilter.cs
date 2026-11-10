using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Covers;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Collections de la barre laterale.</summary>
    public enum GameCollection
    {
        All,
        /// <summary>Jeux sans jaquette personnalisee (meme regle que <see cref="CoverTargets.NeedsCover"/>).</summary>
        NoCover,
        /// <summary>
        /// Reserve : hakchi n'a aucune notion de favoris (ni groupe ni dossier « Favoris »). <see cref="GameFilter.FavoritesAvailable"/>
        /// vaut false et l'interface masque cette collection ; si elle est choisie quand meme, elle se comporte comme <see cref="All"/>.
        /// </summary>
        Favorites,
    }

    /// <summary>Puces « Sur la console » / « Dans la bibliotheque ».</summary>
    public enum GameScope
    {
        /// <summary>Jeux coches (ceux que Synchroniser envoie).</summary>
        OnConsole,
        /// <summary>Toute la bibliotheque.</summary>
        Library,
    }

    /// <summary>
    /// Filtre d'affichage de la grille. Il ne touche jamais a la ListView source : il dit seulement quels items montrer
    /// et dans quel ordre. Synchroniser, Exporter et le gestionnaire de dossiers lisent toujours tous les items coches.
    /// </summary>
    public sealed class GameFilter
    {
        /// <summary>hakchi n'a pas de favoris : la collection <see cref="GameCollection.Favorites"/> est masquee.</summary>
        public static bool FavoritesAvailable { get { return false; } }

        private string system;
        private GameCollection collection = GameCollection.All;
        private GameScope scope = GameScope.Library;
        private bool sortAz;

        /// <summary>Declenche a chaque changement effectif d'un critere.</summary>
        public event EventHandler Changed;

        /// <summary>Systeme affiche (valeur de <see cref="SystemOf"/>), null = tous.</summary>
        public string System { get { return system; } set { if (system != value) { system = value; OnChanged(); } } }
        public GameCollection Collection { get { return collection; } set { if (collection != value) { collection = value; OnChanged(); } } }
        public GameScope Scope { get { return scope; } set { if (scope != value) { scope = value; OnChanged(); } } }
        /// <summary>Tri A-Z sur le texte des items ; sinon l'ordre de la ListView source (groupe puis position).</summary>
        public bool SortAz { get { return sortAz; } set { if (sortAz != value) { sortAz = value; OnChanged(); } } }

        private void OnChanged() { var h = Changed; if (h != null) h(this, EventArgs.Empty); }

        /// <summary>Nom du systeme d'un jeu, tel que la barre laterale le regroupe (nom de l'AppInfo de hakchi), "" si inconnu.</summary>
        public static string SystemOf(NesApplication app)
        {
            if (app == null || app.Metadata == null) return string.Empty;
            if (string.IsNullOrEmpty(app.Metadata.System)) return string.Empty;
            var info = app.Metadata.AppInfo;
            return info != null && !string.IsNullOrEmpty(info.Name) ? info.Name : app.Metadata.System;
        }

        public bool Matches(ListViewItem item)
        {
            if (item == null) return false;
            var app = item.Tag as NesApplication;
            if (scope == GameScope.OnConsole && !item.Checked) return false;
            if (system != null && !string.Equals(SystemOf(app), system, StringComparison.Ordinal)) return false;
            if (collection == GameCollection.NoCover && !CoverTargets.NeedsCover(app)) return false;
            return true;
        }

        /// <summary>Items de la source a afficher, dans l'ordre d'affichage.</summary>
        public List<ListViewItem> Apply(ListView source)
        {
            var list = new List<ListViewItem>();
            if (source == null) return list;
            var groupIndex = new Dictionary<ListViewGroup, int>();
            for (int i = 0; i < source.Groups.Count; i++) groupIndex[source.Groups[i]] = i;
            var keys = new List<KeyValuePair<long, ListViewItem>>();
            int n = source.Items.Count;
            for (int i = 0; i < n; i++)
            {
                var it = source.Items[i];
                if (!Matches(it)) continue;
                int g;
                // ordre d'une ListView groupee : groupe par groupe ; les items sans groupe vont a la fin (groupe par defaut)
                if (!source.ShowGroups) g = 0;
                else if (it.Group == null || !groupIndex.TryGetValue(it.Group, out g)) g = source.Groups.Count;
                keys.Add(new KeyValuePair<long, ListViewItem>(((long)g << 32) | (uint)i, it));
            }
            if (sortAz)
            {
                var cmp = CultureInfo.CurrentCulture.CompareInfo;
                keys.Sort((a, b) =>
                {
                    int c = cmp.Compare(a.Value.Text ?? "", b.Value.Text ?? "", CompareOptions.IgnoreCase);
                    return c != 0 ? c : a.Key.CompareTo(b.Key);
                });
            }
            else keys.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (var k in keys) list.Add(k.Value);
            return list;
        }
    }
}
