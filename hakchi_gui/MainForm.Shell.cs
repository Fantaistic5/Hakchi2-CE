using com.clusterrr.hakchi_gui.Covers;
using com.clusterrr.hakchi_gui.Properties;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui
{
    /// <summary>
    /// Coque « A · Jaquettes » de la fenetre principale (mise en page et cablage seulement) :
    /// TopBar en haut (vrai MenuStrip : Jeux, Mods, Pochettes, Outils), colonne des systemes a gauche (22 %),
    /// puces de filtre au-dessus de la grille de jaquettes au centre, fiche du jeu a droite (27 %), pied de page
    /// (stockage, « Chercher N jaquettes », Synchroniser).
    /// <para>
    /// Rien n'est supprime : les controles du Designer sont deplaces dans la coque (listViewGames reste la source,
    /// cachee, handle cree ; les champs editables et les boutons d'illustration vont dans la fiche) ; ceux qui n'ont plus
    /// de place (barre d'etat, boutons Ajouter / Exporter, ancien tableau) restent dans la fenetre, caches, et leurs
    /// fonctions sont atteignables par les menus. Les ToolStripMenuItem existants sont deplaces, pas recrees.
    /// </para>
    /// </summary>
    partial class MainForm
    {
        internal TopBar topBar;
        internal SystemNavList nav;
        internal CoverGridView grid;
        internal GameSheetPanel sheet;
        internal StorageBar storageBar;
        internal B1Chip chipOnConsole, chipLibrary, chipAz;
        internal B1Button buttonFindCovers;
        internal Panel shellLeft, shellLeftHeader, shellLeftGap, shellCenter, shellChips, shellChipAzHost;
        internal Panel shellFooter, shellFooterRule, shellFooterPad, shellFooterRow, shellFooterGap1, shellFooterGap2;
        internal Panel sheetEditPanel;
        internal ShellCaption shellCollectionCaption;
        internal ContextMenuStrip coverChooserMenu;
        internal ToolStripMenuItem changeCoverToolStripMenuItem, googleCoverToolStripMenuItem, defaultCoverToolStripMenuItem, spineCoverToolStripMenuItem;
        private bool shellBuilt, shellLayingOut;
        private string lastBadgeKey;

        /// <summary>Taille minimale de la fenetre (logique, 96 dpi).</summary>
        private bool fittingCombos;
        internal const int DescriptionMinHeight = 120;      // px a 96 dpi
        internal static readonly Size ShellMinimumSize = new Size(1024, 640);

        private float ShellScale { get { return B1Dpi.Scale(this); } }

        private void BuildShell()
        {
            SuspendLayout();
            BackColor = B1Palette.Slate;

            // --- ancienne disposition : gardee, cachee (ses controles encore utiles en sortent ci-dessous) ---
            tableLayoutPanelMain.Visible = false;

            // --- source de verite : listViewGames, cachee dans la fenetre ---
            listViewGames.Dock = DockStyle.None;
            listViewGames.Visible = false;
            Controls.Add(listViewGames);
            listViewGames.ContextMenuStrip = contextMenuStrip;      // la grille montre le menu de sa source

            // --- centre : puces + grille ---
            grid = new CoverGridView { Name = "grid", Dock = DockStyle.Fill };
            chipOnConsole = new B1Chip { Name = "chipOnConsole", Text = Resources.ShellChipOnConsole, AutoSize = false };
            chipLibrary = new B1Chip { Name = "chipLibrary", Text = Resources.ShellChipLibrary, AutoSize = false, Checked = true };
            chipAz = new B1Chip { Name = "chipAz", Text = Resources.ShellChipAz, AutoSize = false, AutoCheck = false, Dock = DockStyle.Fill };
            shellChipAzHost = new Panel { Name = "shellChipAzHost", BackColor = B1Palette.Slate };   // autre parent : hors du groupe radio
            shellChipAzHost.Controls.Add(chipAz);
            shellChips = new Panel { Name = "shellChips", Dock = DockStyle.Top, BackColor = B1Palette.Slate };
            shellChips.Controls.Add(chipOnConsole);
            shellChips.Controls.Add(chipLibrary);
            shellChips.Controls.Add(shellChipAzHost);
            shellCenter = new Panel { Name = "shellCenter", Dock = DockStyle.Fill, BackColor = B1Palette.Slate };
            shellCenter.Controls.Add(grid);
            shellCenter.Controls.Add(shellChips);

            chipOnConsole.CheckedChanged += (s, e) => { if (chipOnConsole.Checked) grid.Filter.Scope = GameScope.OnConsole; };
            chipLibrary.CheckedChanged += (s, e) => { if (chipLibrary.Checked) grid.Filter.Scope = GameScope.Library; };
            chipAz.Click += (s, e) => { chipAz.Checked = !chipAz.Checked; grid.Filter.SortAz = chipAz.Checked; };
            grid.Filter.Scope = GameScope.Library;      // decision du controleur : « Dans la bibliotheque », pour la session

            // --- gauche : collection (console) + structure, puis systemes ---
            nav = new SystemNavList { Name = "nav", Dock = DockStyle.Fill };
            nav.FilterChanged += (s, f) => { grid.Filter.System = f.System; grid.Filter.Collection = f.Collection; };
            gamesConsoleComboBox.Dock = DockStyle.Top;
            structureButton.Dock = DockStyle.Top;
            structureButton.AutoSize = false;
            shellLeftGap = new Panel { Name = "shellLeftGap", Dock = DockStyle.Top, BackColor = B1Palette.Slate };
            shellLeftHeader = new Panel { Name = "shellLeftHeader", Dock = DockStyle.Top, BackColor = B1Palette.Slate };
            shellLeftHeader.Controls.Add(structureButton);      // Dock Top : le dernier ajoute est en haut
            shellLeftHeader.Controls.Add(shellLeftGap);
            shellLeftHeader.Controls.Add(gamesConsoleComboBox);
            // titre de la liste deroulante : le titre d'origine de son cadre (« Collection de jeux actuelle »), style « SYSTEMES »
            shellCollectionCaption = new ShellCaption { Name = "shellCollectionCaption", Dock = DockStyle.Top, Text = groupBoxCurrentGamesCollection.Text };
            shellLeftHeader.Controls.Add(shellCollectionCaption);
            shellLeftHeader.Paint += (s, e) =>
            {
                int r = B1Draw.Px(1, ShellScale);
                using (var b = new SolidBrush(B1Palette.Rule)) e.Graphics.FillRectangle(b, shellLeftHeader.ClientSize.Width - r, 0, r, shellLeftHeader.ClientSize.Height);
            };
            shellLeft = new Panel { Name = "shellLeft", Dock = DockStyle.Left, BackColor = B1Palette.Slate };
            shellLeft.Controls.Add(nav);
            shellLeft.Controls.Add(shellLeftHeader);

            // --- droite : fiche + champs editables existants (meme objets, memes gestionnaires) ---
            sheet = new GameSheetPanel { Name = "sheet", Dock = DockStyle.Right };
            sheet.ChangeCoverRequested += (s, e) => ShowCoverChooser();
            sheetEditPanel = new Panel { Name = "sheetEditPanel", Dock = DockStyle.Fill, AutoScroll = true, BackColor = B1Palette.Slate, Visible = false };

            groupBoxButtons.Dock = DockStyle.Top;
            groupBoxGameInfo.Dock = DockStyle.Top;
            groupBoxGameInfo.AutoSize = true;
            groupBoxGameInfo.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanelGameInfo.Dock = DockStyle.Top;
            tableLayoutPanelGameInfo.AutoSize = true;
            tableLayoutPanelGameInfo.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            groupBoxArtNintendo.Dock = DockStyle.Top;
            groupBoxArtSega.Dock = DockStyle.Top;
            panel2.Layout += (s, e) => LayoutArtPanel();
            panel3.Layout += (s, e) => LayoutSegaArtPanel();
            foreach (Control c in tableLayoutPanelGameInfo.Controls)
                if (c is ComboBox) c.SizeChanged += (s, e) => FitGameInfoCombos();
            sheetEditPanel.Controls.Add(groupBoxArtSega);       // Dock Top : le dernier ajoute est en haut
            sheetEditPanel.Controls.Add(groupBoxArtNintendo);
            sheetEditPanel.Controls.Add(groupBoxGameInfo);
            sheetEditPanel.Controls.Add(groupBoxButtons);
            sheet.Controls.Add(sheetEditPanel);
            DarkTitleBar.ThemeScrollBars(sheetEditPanel);     // barres de defilement sombres (pose a la creation du handle)
            foreach (var c in new Control[] { textBoxName, textBoxPublisher, maskedTextBoxReleaseDate })
                c.TextChanged += (s, e) => RefreshSheetHeader();
            maxPlayersComboBox.SelectedIndexChanged += (s, e) => RefreshSheetHeader();
            comboBoxGenre.SelectedValueChanged += (s, e) => RefreshSheetHeader();

            // --- bas : stockage, « Chercher N jaquettes », Synchroniser (le bouton de synchro d'origine) ---
            storageBar = new StorageBar { Name = "storageBar", Dock = DockStyle.Fill };
            buttonFindCovers = new B1Button { Name = "buttonFindCovers", Kind = B1ButtonKind.Secondary, Dock = DockStyle.Right, Text = string.Format(Resources.ShellFindCovers, 0) };
            buttonFindCovers.Click += findMissingCoversToolStripMenuItem_Click;
            buttonStart.Text = Resources.ShellSync;
            buttonStart.Dock = DockStyle.Right;
            B1Skin.SetKind(buttonStart, B1ButtonKind.Primary);
            shellFooterGap1 = new Panel { Name = "shellFooterGap1", Dock = DockStyle.Right, BackColor = B1Palette.Slate };
            shellFooterGap2 = new Panel { Name = "shellFooterGap2", Dock = DockStyle.Right, BackColor = B1Palette.Slate };
            shellFooterRow = new Panel { Name = "shellFooterRow", Dock = DockStyle.Fill, BackColor = B1Palette.Slate };
            shellFooterRow.Controls.Add(storageBar);
            shellFooterRow.Controls.Add(shellFooterGap1);
            shellFooterRow.Controls.Add(buttonFindCovers);
            shellFooterRow.Controls.Add(shellFooterGap2);
            shellFooterRow.Controls.Add(buttonStart);
            shellFooterPad = new Panel { Name = "shellFooterPad", Dock = DockStyle.Fill, BackColor = B1Palette.Slate };
            shellFooterPad.Controls.Add(shellFooterRow);
            shellFooterRule = new Panel { Name = "shellFooterRule", Dock = DockStyle.Top, BackColor = B1Palette.Rule };
            shellFooter = new Panel { Name = "shellFooter", Dock = DockStyle.Bottom, BackColor = B1Palette.Slate };
            shellFooter.Controls.Add(shellFooterPad);
            shellFooter.Controls.Add(shellFooterRule);

            // --- haut ---
            topBar = new TopBar { Name = "topBar", Dock = DockStyle.Top };
            BuildShellMenus();
            UpdateConsoleBadge();

            // ordre d'ajout : le dernier ajoute est range en premier (Top/Bottom pleine largeur, puis cotes, puis centre)
            Controls.Add(shellCenter);
            Controls.Add(sheet);
            Controls.Add(shellLeft);
            Controls.Add(shellFooter);
            Controls.Add(topBar);

            grid.Bind(listViewGames);   // force le handle de la liste cachee : ses evenements partent normalement

            MinimumSize = ShellMinimumSize;
            if (Width < 1280 || Height < 800) Size = new Size(1280, 800);
            shellBuilt = true;
            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>
        /// Range les menus d'origine dans le vrai MenuStrip de la TopBar puis retire l'ancien MenuStrip
        /// (sinon Form.ProcessCmdKey trouverait les raccourcis deux fois). Les items sont DEPLACES : memes objets,
        /// memes Click / DropDownOpening / ShortcutKeys.
        /// <list type="bullet">
        /// <item>Jeux = Fichier (+ Vue en sous-menu, avant Quitter)</item>
        /// <item>Mods = Modules</item>
        /// <item>Pochettes = Outils &gt; Pochettes, + changer / Google / par defaut / etiquette du jeu choisi (Ctrl+J)</item>
        /// <item>Outils = Outils (+ Kernel, Parametres, Bluetooth, Aide en sous-menus)</item>
        /// </list>
        /// </summary>
        private void BuildShellMenus()
        {
            // Jeux
            int exitAt = fileToolStripMenuItem.DropDownItems.IndexOf(exitToolStripMenuItem);
            if (exitAt > 0 && fileToolStripMenuItem.DropDownItems[exitAt - 1] is ToolStripSeparator) exitAt--;
            if (exitAt < 0) exitAt = fileToolStripMenuItem.DropDownItems.Count;
            fileToolStripMenuItem.DropDownItems.Insert(exitAt, viewToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Insert(exitAt, new ToolStripSeparator { Name = "shellGamesViewSeparator" });
            topBar.AddMenu(fileToolStripMenuItem, Resources.ShellMenuGames);

            // Mods
            topBar.AddMenu(modulesToolStripMenuItem, Resources.ShellMenuMods);

            // Pochettes
            changeCoverToolStripMenuItem = new ToolStripMenuItem(Resources.ShellCoverChange) { Name = "changeCoverToolStripMenuItem", ShortcutKeys = Keys.Control | Keys.J };
            googleCoverToolStripMenuItem = new ToolStripMenuItem(Resources.ShellCoverGoogle) { Name = "googleCoverToolStripMenuItem" };
            defaultCoverToolStripMenuItem = new ToolStripMenuItem(Resources.ShellCoverDefault) { Name = "defaultCoverToolStripMenuItem" };
            spineCoverToolStripMenuItem = new ToolStripMenuItem(Resources.ShellCoverSpine) { Name = "spineCoverToolStripMenuItem" };
            changeCoverToolStripMenuItem.Click += ChangeCoverFromMenu;
            googleCoverToolStripMenuItem.Click += buttonGoogle_Click;
            defaultCoverToolStripMenuItem.Click += buttonDefaultCover_Click;
            spineCoverToolStripMenuItem.Click += buttonSpine_Click;
            coversToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator { Name = "shellCoversSeparator" });
            coversToolStripMenuItem.DropDownItems.Add(changeCoverToolStripMenuItem);
            coversToolStripMenuItem.DropDownItems.Add(googleCoverToolStripMenuItem);
            coversToolStripMenuItem.DropDownItems.Add(defaultCoverToolStripMenuItem);
            coversToolStripMenuItem.DropDownItems.Add(spineCoverToolStripMenuItem);
            coversToolStripMenuItem.DropDownOpening += (s, e) => UpdateCoverItems();
            topBar.AddMenu(coversToolStripMenuItem, Resources.ShellMenuCovers);   // quitte Outils

            // Outils
            var more = new ToolStripItem[]
            {
                new ToolStripSeparator { Name = "shellToolsSeparator1" },
                kernelToolStripMenuItem, settingsToolStripMenuItem, bluetoothToolStripMenuItem,
                new ToolStripSeparator { Name = "shellToolsSeparator2" },
                helpToolStripMenuItem,
            };
            foreach (var it in more) toolsToolStripMenuItem.DropDownItems.Add(it);
            topBar.AddMenu(toolsToolStripMenuItem, null);

            // l'ancien MenuStrip est vide : on le retire (raccourcis une seule fois, Alt/F10 sur la TopBar)
            foreach (var left in menuStrip.Items.Cast<ToolStripItem>().ToList()) toolsToolStripMenuItem.DropDownItems.Add(left);
            Controls.Remove(menuStrip);
            menuStrip.Dispose();
            MainMenuStrip = topBar.Menu;
            topBar.Menu.MenuActivate += MenuStrip_MenuActivate;

            // le meme choix de jaquette que le menu Pochettes, ouvert par le lien « changer » de la fiche
            coverChooserMenu = new ContextMenuStrip { Name = "coverChooserMenu" };
            var chooseFile = new ToolStripMenuItem(Resources.ShellCoverChange) { Name = "coverChooserChange" };
            var chooseGoogle = new ToolStripMenuItem(Resources.ShellCoverGoogle) { Name = "coverChooserGoogle" };
            var chooseDefault = new ToolStripMenuItem(Resources.ShellCoverDefault) { Name = "coverChooserDefault" };
            var chooseSpine = new ToolStripMenuItem(Resources.ShellCoverSpine) { Name = "coverChooserSpine" };
            chooseFile.Click += ChangeCoverFromMenu;
            chooseGoogle.Click += buttonGoogle_Click;
            chooseDefault.Click += buttonDefaultCover_Click;
            chooseSpine.Click += buttonSpine_Click;
            coverChooserMenu.Items.AddRange(new ToolStripItem[] { chooseFile, chooseGoogle, chooseDefault, chooseSpine });
            components.Add(coverChooserMenu);
        }

        private void UpdateCoverItems()
        {
            bool one = GetSelectedGame() != null;
            changeCoverToolStripMenuItem.Enabled = googleCoverToolStripMenuItem.Enabled =
                defaultCoverToolStripMenuItem.Enabled = spineCoverToolStripMenuItem.Enabled = one;
        }

        /// <summary>« Changer la jaquette… » : le meme chemin que le bouton « Parcourir » (choix d'un fichier, toutes les faces).</summary>
        private void ChangeCoverFromMenu(object sender, EventArgs e)
        {
            if (GetSelectedGame() == null) return;
            pictureBoxArt_MouseClick(buttonBrowseImage, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        }

        private void ShowCoverChooser()
        {
            if (GetSelectedGame() == null || coverChooserMenu == null) return;
            var r = sheet.ChangeLinkBounds;
            coverChooserMenu.Show(sheet, r.IsEmpty ? sheet.PointToClient(Cursor.Position) : new Point(r.Left, r.Bottom));
        }

        // --- mise en page -----------------------------------------------------------------------------------------

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (shellBuilt && !shellLayingOut)
            {
                shellLayingOut = true;
                try { ApplyShellSizes(); }
                finally { shellLayingOut = false; }
            }
            base.OnLayout(levent);
        }

        private static void SetWidth(Control c, int w) { if (c.Width != w) c.Width = w; }
        private static void SetHeight(Control c, int h) { if (c.Height != h) c.Height = h; }

        /// <summary>Tailles de la coque : colonnes en pourcentage de la largeur, hauteurs en cotes de 96 dpi mises a l'echelle.</summary>
        internal void ApplyShellSizes()
        {
            float s = ShellScale;
            int W = ClientSize.Width;
            SetHeight(topBar, B1Draw.Px(36, s));
            SetHeight(shellFooter, B1Draw.Px(52, s));
            SetHeight(shellFooterRule, B1Draw.Px(1, s));
            shellFooterPad.Padding = new Padding(B1Draw.Px(18, s), B1Draw.Px(11, s), B1Draw.Px(18, s), B1Draw.Px(12, s));
            SetWidth(shellFooterGap1, B1Draw.Px(18, s));
            SetWidth(shellFooterGap2, B1Draw.Px(10, s));
            SetWidth(buttonFindCovers, Math.Max(B1Draw.Px(150, s), buttonFindCovers.GetPreferredSize(Size.Empty).Width));
            SetWidth(buttonStart, Math.Max(B1Draw.Px(130, s), TextRenderer.MeasureText(buttonStart.Text, buttonStart.Font).Width + B1Draw.Px(32, s)));

            int lw = (int)Math.Round(W * 0.22), rw = (int)Math.Round(W * 0.27);
            SetWidth(shellLeft, lw);
            SetWidth(sheet, rw);

            // colonne gauche : liste deroulante des collections puis bouton Structure
            int pad = B1Draw.Px(14, s);
            shellLeftHeader.Padding = new Padding(B1Draw.Px(16, s), pad, B1Draw.Px(14, s) + B1Draw.Px(1, s), B1Draw.Px(8, s));
            SetHeight(shellLeftGap, B1Draw.Px(6, s));
            SetHeight(structureButton, Math.Max(B1Draw.Px(26, s), structureButton.Font.Height + B1Draw.Px(10, s)));
            SetHeight(shellCollectionCaption, shellCollectionCaption.GetPreferredSize(Size.Empty).Height);
            SetHeight(shellLeftHeader, shellLeftHeader.Padding.Vertical + shellCollectionCaption.Height + gamesConsoleComboBox.Height + shellLeftGap.Height + structureButton.Height);

            // fiche : la description (ligne 22, AutoSize ; ShowSelected remet panel1 a 1x1) garde une hauteur utilisable
            var dmin = new Size(0, B1Draw.Px(DescriptionMinHeight, s));
            if (panel1.MinimumSize != dmin) panel1.MinimumSize = dmin;

            // puces : de gauche a droite, a la ligne si la largeur manque (jamais de chevauchement)
            int cw = Math.Max(1, W - lw - rw);
            int px = B1Draw.Px(14, s), py = B1Draw.Px(10, s), gap = B1Draw.Px(8, s);
            int x = px, y = py, lineH = 0;
            foreach (var c in new Control[] { chipOnConsole, chipLibrary, shellChipAzHost })
            {
                var ps = (c == shellChipAzHost ? chipAz : c).GetPreferredSize(Size.Empty);
                int w = Math.Min(ps.Width, Math.Max(1, cw - 2 * px));
                if (x > px && x + w > cw - px) { x = px; y += lineH + gap; lineH = 0; }
                var b = new Rectangle(x, y, w, ps.Height);
                if (c.Bounds != b) c.Bounds = b;
                x += w + gap;
                lineH = Math.Max(lineH, ps.Height);
            }
            SetHeight(shellChips, y + lineH + B1Draw.Px(4, s));
        }

        /// <summary>Illustration Nintendo : la miniature reste en bas a droite, la jaquette prend le reste de la largeur.</summary>
        private void LayoutArtPanel()
        {
            int m = pictureBoxArt.Left;
            int tx = Math.Max(m, panel2.ClientSize.Width - pictureBoxThumbnail.Width - m);
            if (pictureBoxThumbnail.Left != tx) pictureBoxThumbnail.Left = tx;
            int aw = Math.Max(10, tx - m - pictureBoxArt.Left);
            if (pictureBoxArt.Width != aw) pictureBoxArt.Width = aw;
            // hauteurs : tout tient dans le panneau (le concepteur debordait de 2 px une fois la fiche plus etroite)
            int ah = Math.Max(10, panel2.ClientSize.Height - 2 * m);
            if (pictureBoxArt.Height != ah) pictureBoxArt.Height = ah;
            int ty = Math.Max(m, panel2.ClientSize.Height - pictureBoxThumbnail.Height - m);
            if (pictureBoxThumbnail.Top != ty) pictureBoxThumbnail.Top = ty;
        }

        /// <summary>Illustrations Mega Drive : tranche puis face, cote a cote sans chevauchement, a la hauteur du panneau.</summary>
        private void LayoutSegaArtPanel()
        {
            int m = pictureBoxM2Spine.Left;
            int h = Math.Max(10, panel3.ClientSize.Height - 2 * m);
            if (pictureBoxM2Spine.Height != h) pictureBoxM2Spine.Height = h;
            if (pictureBoxM2Front.Height != h) pictureBoxM2Front.Height = h;
            int fx = pictureBoxM2Spine.Right + 1;
            if (pictureBoxM2Front.Left < fx) pictureBoxM2Front.Left = fx;
        }

        /// <summary>
        /// Les listes deroulantes de la fiche (hauteur imposee par Windows apres le theme) depassaient leur ligne AutoSize, calculee
        /// sur la taille du concepteur : la marge du bas absorbe l'ecart pour que la ligne suivante ne soit pas recouverte.
        /// </summary>
        private void FitGameInfoCombos()
        {
            if (!shellBuilt || fittingCombos) return;
            fittingCombos = true;
            try
            {
                var t = tableLayoutPanelGameInfo;
                t.PerformLayout();
                var rows = t.GetRowHeights();
                bool changed = false;
                foreach (Control c in t.Controls)
                {
                    if (!(c is ComboBox)) continue;
                    int row = t.GetRow(c);
                    if (row < 0 || row >= rows.Length || t.GetRowSpan(c) != 1) continue;
                    int over = c.Height + c.Margin.Vertical - rows[row];
                    if (over > 0)
                    {
                        c.Margin = new Padding(c.Margin.Left, c.Margin.Top, c.Margin.Right, c.Margin.Bottom + over);
                        changed = true;
                    }
                }
                if (changed) t.PerformLayout();
            }
            finally { fittingCombos = false; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);      // B1Form : theme
            FitGameInfoCombos();
        }

        // --- etat ------------------------------------------------------------------------------------------------

        /// <summary>Fiche du jeu (selection unique) ; appele a la fin de ShowSelected.</summary>
        private void ShowSheet()
        {
            if (!shellBuilt) return;
            var app = GetSelectedGame();
            sheet.Show(app);
            sheetEditPanel.Visible = app != null;
        }

        /// <summary>Rafraichit l'en-tete de la fiche quand l'utilisateur edite un champ (nom, editeur, date, joueurs, genre).</summary>
        private void RefreshSheetHeader()
        {
            if (!shellBuilt || showingSelected) return;
            var app = GetSelectedGame();
            if (app != null && app == sheet.Game) sheet.Show(app);
        }

        /// <summary>Apres un changement de jaquette : la grille et la fiche relisent l'image.</summary>
        internal void CoverChanged(NesApplication app)
        {
            if (app != null) CoversChanged(new[] { app });
        }

        internal void CoversChanged(IEnumerable<NesApplication> apps)
        {
            if (!shellBuilt || apps == null) return;
            foreach (var a in apps) if (a != null) grid.Cache.Forget(a);
            grid.Invalidate();
            sheet.ReloadCover();
            UpdateShellCounts();
        }

        private List<NesApplication> SelectedApps()
        {
            return listViewGames.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag as NesApplication).Where(a => a != null).ToList();
        }

        /// <summary>Systemes et nombre de jeux (colonne gauche), jeux sans jaquette (collection + bouton du bas).</summary>
        /// <summary>Pied de page : « N jeu(x) selectionne(s) · Console » devant la jauge (ancienne barre d'etat, memes chaines).</summary>
        internal void ShowSelectedCount(int count)
        {
            if (storageBar == null) return;
            storageBar.Caption = count + " " + Resources.GamesSelected + " · " + Resources.StorageBarCaption;
        }

        internal void UpdateShellCounts()
        {
            if (!shellBuilt) return;
            var apps = listViewGames.Items.Cast<ListViewItem>().Select(i => i.Tag as NesApplication).Where(a => a != null).ToList();
            nav.SetSystems(apps.GroupBy(a => GameFilter.SystemOf(a) ?? string.Empty)
                .Where(g => g.Key.Length > 0)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.CurrentCulture)
                .Select(g => (g.Key, g.Count())).ToList());
            int missing = CoverTargets.DefaultSelection(apps).Count();
            nav.SetCollectionCount(GameCollection.NoCover, missing);
            var text = string.Format(Resources.ShellFindCovers, missing);
            if (buttonFindCovers.Text != text)
            {
                buttonFindCovers.Text = text;
                PerformLayout();
            }
        }

        /// <summary>Pastille de la barre du haut : modele et version de hakchi de la console, ou « Aucune console ».</summary>
        internal void UpdateConsoleBadge()
        {
            if (topBar == null) return;
            bool online = hakchi.Connected;
            string model = string.Empty;
            if (online)
                model = hakchi.DetectedConsoleType != null ? GetConsoleTypeName(hakchi.DetectedConsoleType) : GetConsoleTypeName(ConfigIni.Instance.ConsoleType);
            string version = ShortHakchiVersion(online ? hakchi.RawScriptVersion : hakchi.RawLocalScriptVersion);
            string key = online + "|" + model + "|" + version;
            if (key == lastBadgeKey) return;
            lastBadgeKey = key;
            topBar.Badge.SetState(online, model, version);
        }

        /// <summary>« v1.0.4-126 » -> « 1.0.4 ».</summary>
        internal static string ShortHakchiVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var v = raw.Trim();
            if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);
            int dash = v.IndexOf('-');
            return dash > 0 ? v.Substring(0, dash) : v;
        }
    }

    /// <summary>Petit titre de section de la coque : capitales espacees, TextFaint (comme « SYSTEMES » de la SystemNavList).</summary>
    internal sealed class ShellCaption : Control
    {
        private readonly ShellFonts fonts = new ShellFonts();

        public ShellCaption()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            BackColor = B1Palette.Slate;
        }

        private float S { get { return B1Dpi.Scale(this); } }
        private Font Caps { get { return fonts.Get(ShellFonts.Kind.Caps, 7.5f, S); } }

        /// <summary>Texte affiche (capitales de la culture d'interface).</summary>
        public string DisplayText { get { return (Text ?? string.Empty).ToUpper(System.Globalization.CultureInfo.CurrentUICulture); } }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); AccessibleName = Text; Invalidate(); }

        public override Size GetPreferredSize(Size proposedSize)
        {
            return new Size(Width, Caps.Height + B1Draw.Px(8, S));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(B1Palette.Slate);
            var f = Caps;
            string t = DisplayText;
            int tw = B1Draw.TrackedWidth(g, t, f, 0.14f);
            int x = RightToLeft == RightToLeft.Yes ? Math.Max(0, ClientSize.Width - tw) : 0;
            var clip = g.Clip;
            g.SetClip(ClientRectangle);
            B1Draw.DrawTracked(g, t, f, B1Palette.TextFaint, x, 0, 0.14f);
            g.Clip = clip;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) fonts.Dispose();
            base.Dispose(disposing);
        }
    }
}
