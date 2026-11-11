using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Hmod.Controls;
using com.clusterrr.hakchi_gui.ModHub;
using com.clusterrr.hakchi_gui.ModHub.Repository;

namespace FormGallery
{
    /// <summary>
    /// ModHubForm et SelectForm peuplees pour la galerie, sans reseau ni console : depot construit en memoire (aucun Load, aucun
    /// telechargement), liste de mods ecrite ici (aucun Hmod.GetMods, aucune lecture de user_mods). Les readmes d'exemple n'ont
    /// aucune image ni ressource distante : le navigateur n'a rien a charger. Rien n'est installe ni desinstalle.
    /// </summary>
    internal static class ModHubSample
    {
        public const string WelcomeReadme = "# KMFD's Mod Hub\n\nModules pour la Mini SNES, verifies par la communaute.";

        public static string ModReadme(string name, string creator, string version, string category, string system, string body)
        {
            return "---\nName: " + name + "\nCreator: " + creator + "\nVersion: " + version + "\nCategory: " + category +
                   (system != null ? "\nEmulated System: " + system : "") + "\n---\n" + body;
        }

        public const string MusicBody =
            "Joue une musique de fond dans le menu de la console.\n\n" +
            "## Installation\n\n" +
            "1. Copier les fichiers `.wav` dans `/usr/share/music`.\n" +
            "2. Redemarrer la console.\n\n" +
            "> Les fichiers doivent etre en 44,1 kHz, 16 bits.\n\n" +
            "Plus d'informations sur [le forum](https://example.invalid/forum).\n";

        /// <summary>Depot d'exemple : une page d'accueil, des emulateurs groupes par systeme et des mods d'interface.</summary>
        public static Repository Repo()
        {
            var repo = new Repository("https://example.invalid/repo/");
            typeof(Repository).GetProperty("Readme").SetValue(repo, WelcomeReadme, null);
            repo.Items.Add(new Repository.Item("retroarch.hmod", ModReadme("RetroArch", "Cluster", "1.9.0", "Emulators", "Multi-system",
                "RetroArch et ses coeurs pour lancer d'autres consoles.\n\n- NES, SNES, Mega Drive\n- PC Engine, Game Boy\n"), true));
            repo.Items.Add(new Repository.Item("snes9x.hmod", ModReadme("Snes9x 2010", "Snes9x team", "1.53", "Emulators", "Super Nintendo", "Coeur Snes9x pour RetroArch."), true));
            repo.Items.Add(new Repository.Item("nestopia.hmod", ModReadme("Nestopia UE", "Nestopia team", "1.50", "Emulators", "Nintendo", "Coeur Nestopia pour RetroArch."), true));
            repo.Items.Add(new Repository.Item("advanced_music_hack.hmod", ModReadme("Hakchi Advanced Music Hack", "KMFDManic", "2.1", "UI", null, MusicBody), true));
            repo.Items.Add(new Repository.Item("custom_backgrounds.hmod", ModReadme("Custom Backgrounds", "Cluster", "1.0", "UI", null, "Fonds d'ecran personnalises."), true));
            repo.Items.Add(new Repository.Item("snes_custom_filters.hmod", ModReadme("SNES Filters Hack", "DanTheMan827", "1.2", "UI", null, "Filtres d'image supplementaires."), true));
            return repo;
        }

        /// <summary>ModHubForm construite sans son constructeur applicatif (Host.DesignerOnlyTypes) : liste des mods installes vide, depot d'exemple.</summary>
        public static void PopulateHub(Control form)
        {
            var hub = (ModHubForm)form;
            hub.Text = "KMFD's Mod Hub";
            typeof(ModHubForm).GetField("installedMods", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hub, new List<com.clusterrr.hakchi_gui.Hmod.Hmod>());
            hub.LoadData(Repo());
            var tabs = hub.Controls.Find("tabControl1", true).OfType<TabControl>().First();
            // premiere categorie de mods (apres l'onglet d'accueil) : liste groupee, carte du mod et readme
            if (tabs.TabPages.Count > 1) tabs.SelectedIndex = 1;
        }

        /// <summary>SelectForm construite sans son constructeur applicatif : mods ecrits ici (un coche, un deja installe), readme affiche.</summary>
        public static void PopulateSelect(Control form)
        {
            form.Text = "Selectionner les mods a installer";
            var list = form.Controls.Find("listViewHmods", true).OfType<ListView>().First();
            var system = new ListViewGroup("System", HorizontalAlignment.Center);
            var ui = new ListViewGroup("UI", HorizontalAlignment.Center);
            list.Groups.Add(system);
            list.Groups.Add(ui);
            var rows = new[]
            {
                new { Name = "Clovercon Hack", Group = system, Checked = false, Installed = true },
                new { Name = "Gamepath Cleanup", Group = system, Checked = false, Installed = false },
                new { Name = "NTFS-3G", Group = system, Checked = true, Installed = false },
                new { Name = "Custom Backgrounds", Group = ui, Checked = false, Installed = false },
                new { Name = "Hakchi Advanced Music Hack", Group = ui, Checked = true, Installed = false },
                new { Name = "SNES Filters Hack", Group = ui, Checked = false, Installed = true },
            };
            foreach (var r in rows)
            {
                var item = new ListViewItem(r.Name) { Group = r.Group, Checked = r.Checked };
                // meme rendu que SelectForm.populateList pour un mod deja installe
                if (r.Installed) item.ForeColor = SystemColors.GrayText;
                list.Items.Add(item);
            }
            var readme = form.Controls.Find("readmeControl1", true).OfType<ReadmeControl>().First();
            readme.setReadme("Hakchi Advanced Music Hack", ModReadme("Hakchi Advanced Music Hack", "KMFDManic", "2.1", "UI", null, MusicBody), true);
        }
    }
}
