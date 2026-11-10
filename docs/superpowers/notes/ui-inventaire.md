# Inventaire de l'interface PC (avant refonte)

Genere le 2026-11-10 depuis `hakchi.exe` (branche `snes-usa/ui-pc`, build Debug) par `tools/FormGallery` : `FormGallery.exe --dump-baselines hakchi_gui.Tests/Data` et `--out`. Donnees machine-lisibles : `hakchi_gui.Tests/Data/controls_before.json` et `menu_items_before.json`.

- 43 fenetres (`Form`) et 6 `UserControl` concrets dans `hakchi.exe`. Le plan en annonce 42 + 6 : l'ecart d'une fenetre vient de `LicenseInfo`, qui n'est citee dans aucune des Tasks 5 a 9 (rattachee a la Task 6 par defaut).
- Rendues par la galerie : 49 sur 49. Une fenetre est rendue hors ecran, **sans** ses gestionnaires Load / Shown / Closing : c'est son etat vide de conception (aucune connexion console, aucun reseau).
- Construction : arguments factices simples ; les fenetres qui exigent un jeu recoivent un vrai jeu copie de la copie de test (`CLV-Z-CKTTS`, `CLV-U-YABAN`). `WaitingFelForm` / `WaitingShellForm` lancent `pnputil` a la construction : la galerie fournit une doublure qui repond « pilote installe » (l'installeur de pilote n'est jamais lance).

## Fenetres (43)

| Fenetre | Role | Rendue | Famille | Controles nommes |
|---|---|---|---|---|
| `AboutBox` | A propos de hakchi | oui | Task 6 - Fenetres simples | 13 |
| `CoverBatchForm` | Recherche groupee de jaquettes | oui | Task 8 - Bibliotheque | 14 |
| `DumperForm` | Extraction du noyau / kernel de la console (build Dumper) | oui | Task 6 - Fenetres simples | 0 |
| `ErrorForm` | Affichage d'une erreur critique avec trace | oui | Task 7 - Taches, flash, attente (apparence seule) | 5 |
| `ExportGamesDialog` | Export des jeux vers USB / dossier | oui | Task 6 - Fenetres simples | 7 |
| `FoldersManagerForm` | Gestionnaire de dossiers de l'ecran d'accueil | oui | Task 8 - Bibliotheque | 27 |
| `GameGenieCodeAddModForm` | Ajout / modification d'un code Game Genie | oui | Task 8 - Bibliotheque | 6 |
| `GameGenieCodeForm` | Liste des codes Game Genie d'un jeu | oui | Task 8 - Bibliotheque | 5 |
| `GameImporterForm` | Import de jeux (resume, choix) | oui | Task 8 - Bibliotheque | 5 |
| `ImageGooglerForm` | Fenetre hote de la recherche d'image | oui | Task 6 - Fenetres simples | 2 |
| `ImagesForm` | Choix des jaquettes / images (dossiers) | oui | Task 6 - Fenetres simples | 4 |
| `LicenseInfo` | Licences tierces | oui | Task 6 - Fenetres simples (absente de la liste du plan : rattachee ici par defaut) | 2 |
| `MainForm` | Fenetre principale (bibliotheque, fiche de jeu, synchro) | oui | Task 5 - Fenetre principale | 67 |
| `MessageForm` | Boite de message personnalisee | oui | Task 7 - Taches, flash, attente (apparence seule) | 9 |
| `ModHubForm` | ModHub : depot de mods | oui | Task 9 - ModHub et mods | 1 |
| `Motd` | Message du jour | oui | Task 6 - Fenetres simples | 2 |
| `NewCustomGameForm` | Creation d'une appli / jeu utilisateur | oui | Task 6 - Fenetres simples | 7 |
| `RepoManagementForm` | Gestion des depots du ModHub | oui | Task 9 - ModHub et mods | 15 |
| `SDFormat` | Formatage de carte SD | oui | Task 7 - Taches, flash, attente (apparence seule) | 9 |
| `SaveStateManager` | Gestionnaire de sauvegardes (save states) | oui | Task 8 - Bibliotheque | 8 |
| `ScraperForm` | Scraper d'informations (TheGamesDB) | oui | Task 8 - Bibliotheque | 46 |
| `ScreenshotForm` | Capture d'ecran de la console | oui | Task 8 - Bibliotheque | 1 |
| `SearchForm` | Recherche dans la bibliotheque | oui | Task 6 - Fenetres simples | 1 |
| `SelectCoreDialog` | Choix du coeur RetroArch d'un jeu | oui | Task 8 - Bibliotheque | 12 |
| `SelectCoverDialog` | Choix d'une jaquette parmi des resultats | oui | Task 8 - Bibliotheque | 8 |
| `SelectFileForm` | Choix d'un fichier dans une liste | oui | Task 6 - Fenetres simples | 3 |
| `SelectFirmwareDialog` | Choix du firmware | oui | Task 6 - Fenetres simples | 3 |
| `SelectForm` | Selection des mods a installer | oui | Task 9 - ModHub et mods | 6 |
| `SelectIconForm` | Choix d'une icone de dossier | oui | Task 6 - Fenetres simples | 3 |
| `SelectNesButtonsForm` | Mappage des boutons NES | oui | Task 7 - Taches, flash, attente (apparence seule) | 10 |
| `SelectSnesButtonsForm` | Mappage des boutons SNES | oui | Task 7 - Taches, flash, attente (apparence seule) | 14 |
| `SelectSystemDialog` | Choix des systemes (dossiers d'illustrations) | oui | Task 6 - Fenetres simples | 6 |
| `SettingsForm` | Parametres (chemins) | oui | Task 6 - Fenetres simples | 1 |
| `SnesPresetEditor` | Editeur de preset SNES (filtres / options) | oui | Task 7 - Taches, flash, attente (apparence seule) | 5 |
| `SpineForm` | Editeur de tranche (spine) | oui | Task 6 - Fenetres simples | 6 |
| `StringInputForm` | Saisie d'un texte | oui | Task 6 - Fenetres simples | 4 |
| `TaskerForm` | Fenetre de progression des taches | oui | Task 7 - Taches, flash, attente (apparence seule) | 3 |
| `TaskerTransferForm` | Fenetre de progression des transferts | oui | Task 7 - Taches, flash, attente (apparence seule) | 8 |
| `TechInfo` | Informations techniques / journal | oui | Task 6 - Fenetres simples | 5 |
| `TextInfo` | Affichage de texte (readme, informations) | oui | Task 6 - Fenetres simples | 2 |
| `WaitingFelForm` | Attente de la console en mode FEL (flash) | oui | Task 7 - Taches, flash, attente (apparence seule) | 9 |
| `WaitingShellCycleForm` | Attente du cycle de la console (shell) | oui | Task 7 - Taches, flash, attente (apparence seule) | 3 |
| `WaitingShellForm` | Attente de la console (shell) | oui | Task 7 - Taches, flash, attente (apparence seule) | 6 |

## UserControl (6)

| Fenetre | Role | Rendue | Famille | Controles nommes |
|---|---|---|---|---|
| `BrowserReadmeControl` | Readme de mod en HTML (WebBrowser) | oui | Task 9 - ModHub et mods | 1 |
| `ImageGoogler` | Recherche d'image Google (controle) | oui | Task 6 - Fenetres simples | 1 |
| `ModHubTabControl` | Onglet du ModHub (liste de mods) | oui | Task 9 - ModHub et mods | 9 |
| `ModInfoControl` | Fiche d'un mod du ModHub | oui | Task 9 - ModHub et mods | 0 |
| `ReadmeControl` | Readme de mod (conteneur) | oui | Task 9 - ModHub et mods | 2 |
| `TextReadmeControl` | Readme de mod en texte | oui | Task 9 - ModHub et mods | 1 |

## Familles (Tasks 5 a 9)

- **Task 5 - Fenetre principale** : `MainForm`
- **Task 6 - Fenetres simples** : `AboutBox`, `DumperForm`, `ExportGamesDialog`, `ImageGoogler`, `ImageGooglerForm`, `ImagesForm`, `Motd`, `NewCustomGameForm`, `SearchForm`, `SelectFileForm`, `SelectFirmwareDialog`, `SelectIconForm`, `SelectSystemDialog`, `SettingsForm`, `SpineForm`, `StringInputForm`, `TechInfo`, `TextInfo`
- **Task 6 - Fenetres simples (absente de la liste du plan : rattachee ici par defaut)** : `LicenseInfo`
- **Task 7 - Taches, flash, attente (apparence seule)** : `ErrorForm`, `MessageForm`, `SDFormat`, `SelectNesButtonsForm`, `SelectSnesButtonsForm`, `SnesPresetEditor`, `TaskerForm`, `TaskerTransferForm`, `WaitingFelForm`, `WaitingShellCycleForm`, `WaitingShellForm`
- **Task 8 - Bibliotheque** : `CoverBatchForm`, `FoldersManagerForm`, `GameGenieCodeAddModForm`, `GameGenieCodeForm`, `GameImporterForm`, `SaveStateManager`, `ScraperForm`, `ScreenshotForm`, `SelectCoreDialog`, `SelectCoverDialog`
- **Task 9 - ModHub et mods** : `BrowserReadmeControl`, `ModHubForm`, `ModHubTabControl`, `ModInfoControl`, `ReadmeControl`, `RepoManagementForm`, `SelectForm`, `TextReadmeControl`

## ToolStripMenuItem de MainForm (148)

Texte FR (culture fr-FR), proprietaire (chemin de menus) et handler(s) `Click`. Le texte en anglais et les raccourcis sont dans `menu_items_before.json`.

| Champ | Texte FR | Proprietaire | Click |
|---|---|---|---|
| `aboutToolStripMenuItem` | À propos... | `menuStrip > helpToolStripMenuItem` | `aboutToolStripMenuItem_Click` |
| `addCustomAppToolStripMenuItem` | Ajouter &app utilisateur | `menuStrip > fileToolStripMenuItem` | `addCustomAppToolStripMenuItem_Click` |
| `addMoreGamesToolStripMenuItem` | Ajouter des &jeux supplémentaires | `menuStrip > fileToolStripMenuItem` | - |
| `addPrefixToolStripMenuItem` | Ajouter un préfixe | `contextMenuStrip` | `addPrefixToolStripMenuItem_Click` |
| `addPresetToolStripMenuItem` | Nouvelle liste | `menuStrip > fileToolStripMenuItem > presetsToolStripMenuItem` | `AddPreset` |
| `advancedToolStripMenuItem` | Options avancées | `menuStrip > kernelToolStripMenuItem` | - |
| `alwaysCopyOriginalGamesToolStripMenuItem` | Toujours copier les jeux originaux | `menuStrip > settingsToolStripMenuItem` | `alwaysCopyOriginalGamesToolStripMenuItem_Click` |
| `archiveSelectedGamesToolStripMenuItem` | Archiver les jeux sélectionnés | `contextMenuStrip` | `archiveSelectedGamesToolStripMenuItem_Click` |
| `asIsToolStripMenuItem` | Tel quel... | `menuStrip > fileToolStripMenuItem > addMoreGamesToolStripMenuItem` | `asIsToolStripMenuItem_Click` |
| `autoToolStripMenuItem` | Auto | `menuStrip > settingsToolStripMenuItem > segaUiThemeToolStripMenuItem` | `changeM2Theme` |
| `autodetectToolStripMenuItem` | Autodétecter... | `menuStrip > fileToolStripMenuItem > addMoreGamesToolStripMenuItem` | `buttonAddGames_Click` |
| `automaticOriginalToolStripMenuItem` | Jeux d’origine à la racine -> Automatiquement dans sous-dossier | `foldersContextMenuStrip` | `pagesModefoldersToolStripMenuItem_Click` |
| `automaticToolStripMenuItem` | Automatique | `foldersContextMenuStrip` | `pagesModefoldersToolStripMenuItem_Click` |
| `backFolderButtonPositionToolStripMenuItem` | Position du bouton retour | `foldersContextMenuStrip` | - |
| `bootImageToolStripMenuItem` | Écran d'accueil | `menuStrip > toolsToolStripMenuItem` | - |
| `canoeToolStripMenuItem` | Canoe | `menuStrip > settingsToolStripMenuItem > globalCommandLineArgumentsexpertsOnluToolStripMenuItem` | `globalCommandLineArgumentsexpertsOnluToolStripMenuItem_Click` |
| `centerBoxArtThumbnailToolStripMenuItem` | Centrer verticalement les miniatures | `menuStrip > settingsToolStripMenuItem` | `centerBoxArtThumbnailToolStripMenuItem_Click` |
| `changeBootImageToolStripMenuItem` | Modifier l'écran d'accueil | `menuStrip > toolsToolStripMenuItem > bootImageToolStripMenuItem` | `changeBootImageToolStripMenuItem_Click` |
| `cloverconHackToolStripMenuItem` | Hacks relatifs aux manettes | `menuStrip > settingsToolStripMenuItem` | - |
| `compressBoxArtToolStripMenuItem` | Compresser les images au moment de l'ajout | `menuStrip > settingsToolStripMenuItem` | `compressBoxArtToolStripMenuItem_Click` |
| `compressGamesToolStripMenuItem` | Compresser les jeux au moment de l’ajout | `menuStrip > settingsToolStripMenuItem` | `compressGamesToolStripMenuItem_Click` |
| `compressSelectedGamesToolStripMenuItem` | Compresser les jeux sélectionnés | `contextMenuStrip` | `compressSelectedGamesToolStripMenuItem_Click` |
| `convertSNESROMSToSFROMToolStripMenuItem` | Convertir ROMs SNES en SFROM | `menuStrip > settingsToolStripMenuItem` | `convertSNESROMSToSFROMToolStripMenuItem_Click` |
| `coreToolStripMenuItem` | Coeur | `menuStrip > viewToolStripMenuItem > sortByToolStripMenuItem` | `sortByToolStripMenuItem_Click` |
| `coversToolStripMenuItem` | Pochettes | `menuStrip > toolsToolStripMenuItem` | - |
| `customToolStripMenuItem` | Custom - Utiliser le gestionnaire de dossiers (Recliquer pour ouvrir) | `foldersContextMenuStrip` | `pagesModefoldersToolStripMenuItem_Click` |
| `decompressSelectedGamesToolStripMenuItem` | Décompresser les jeux sélectionnés | `contextMenuStrip` | `decompressSelectedGamesToolStripMenuItem_Click` |
| `deletePresetToolStripMenuItem` | Supprimer liste | `menuStrip > fileToolStripMenuItem > presetsToolStripMenuItem` | - |
| `deleteSelectedGamesBoxArtToolStripMenuItem` | Effacer les images pour les jeux sélectionnés | `contextMenuStrip` | `deleteSelectedGamesBoxArtToolStripMenuItem_Click` |
| `deleteSelectedGamesToolStripMenuItem` | Supprimer les jeux sélectionnés | `contextMenuStrip` | `deleteSelectedGamesToolStripMenuItem_Click` |
| `devForceSshToolStripMenuItem` | Forcer les transferts par SSH | `menuStrip > settingsToolStripMenuItem > developerToolsToolStripMenuItem` | `devForceSshToolStripMenuItem_Click` |
| `developerToolsToolStripMenuItem` | Outils du développeur | `menuStrip > settingsToolStripMenuItem` | - |
| `disableBootImageToolStripMenuItem` | Désactiver l'écran d'accueil | `menuStrip > toolsToolStripMenuItem > bootImageToolStripMenuItem` | `disableBootImageToolStripMenuItem_Click` |
| `disableHakchi2PopupsToolStripMenuItem` | Désactiver les pop-ups de hakchi2 | `menuStrip > settingsToolStripMenuItem` | `disableHakchi2PopupsToolStripMenuItem_Click` |
| `disablePagefoldersToolStripMenuItem` | Désactiver page/dossiers | `foldersContextMenuStrip` | `pagesModefoldersToolStripMenuItem_Click` |
| `donateToolStripMenuItem` | Faire un don | `menuStrip > helpToolStripMenuItem` | `openWebsiteLink` |
| `downloadBoxArtForSelectedGamesToolStripMenuItem` | Télécharger les images pour les jeux sélectionnés | `contextMenuStrip` | `downloadBoxArtForSelectedGamesToolStripMenuItem_Click` |
| `downloadLatestHakchiToolStripMenuItem` | Télécharger la dernière version de Hakchi | `menuStrip > settingsToolStripMenuItem > developerToolsToolStripMenuItem` | `DownloadLatestHakchiToolStripMenuItem_Click` |
| `dumpNANDBToolStripMenuItem` | Sauvegarde du logiciel système décrypté | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `dumpNANDBToolStripMenuItem_Click` |
| `dumpNANDCPartitionToolStripMenuItem` | Sauvegarde de la partition données utilisateur | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `dumpNANDCPartitionToolStripMenuItem_Click` |
| `dumpOriginalKernellegacyToolStripMenuItem` | Sauvegarde du kernel d'origine (ancien) | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `dumpOriginalKernellegacyToolStripMenuItem_Click` |
| `dumpTheWholeNANDToolStripMenuItem` | Extraction complète de la mémoire flash NAND | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `dumpTheWholeNANDToolStripMenuItem_Click` |
| `editROMHeaderToolStripMenuItem` | Éditer l'entête SFROM | `contextMenuStrip > sFROMToolToolStripMenuItem1` | `editROMHeaderToolStripMenuItem_Click` |
| `enableAutofireToolStripMenuItem` | Utiliser "Select+A/B" pour activer l’autofire sur A/B  | `menuStrip > settingsToolStripMenuItem > cloverconHackToolStripMenuItem` | `enableAutofireToolStripMenuItem_Click` |
| `enableInformationScrapeOnImportToolStripMenuItem` | Activer la recherche auto en ligne d'info sur importation | `menuStrip > settingsToolStripMenuItem` | `enableInformationScrapeOnImportToolStripMenuItem_Click` |
| `enableSFROMToolToolStripMenuItem` | Activer | `menuStrip > settingsToolStripMenuItem > sFROMToolToolStripMenuItem` | `enableSFROMToolToolStripMenuItem_Click` |
| `epilepsyProtectionToolStripMenuItem` | Désactiver la protection contre l’épilepsie | `menuStrip > settingsToolStripMenuItem` | `ToolStripMenuItemArmet_Click` |
| `europeToolStripMenuItem` | Europe | `menuStrip > settingsToolStripMenuItem > segaUiThemeToolStripMenuItem` | `changeM2Theme` |
| `exitToolStripMenuItem` | &Quitter | `menuStrip > fileToolStripMenuItem` | `exitToolStripMenuItem_Click` |
| `explorerToolStripMenuItem` | Montrer dans l'explorateur Windows | `contextMenuStrip` | `explorerToolStripMenuItem_Click` |
| `exportGamesToolStripMenuItem` | E&xporter les jeux... | `menuStrip > fileToolStripMenuItem` | `buttonExport_Click` |
| `fAQToolStripMenuItem` | Questions fréquentes | `menuStrip > helpToolStripMenuItem` | `openWebsiteLink` |
| `factoryResetToolStripMenuItem` | Restoration aux paramètres d'usine  | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `factoryResetToolStripMenuItem_Click` |
| `fileToolStripMenuItem` | &Fichier | `menuStrip` | - |
| `findMissingCoversToolStripMenuItem` | Chercher les jaquettes manquantes... | `menuStrip > toolsToolStripMenuItem > coversToolStripMenuItem` | `findMissingCoversToolStripMenuItem_Click` |
| `flashCustomKernelToolStripMenuItem` | Installer / Réparer | `menuStrip > kernelToolStripMenuItem` | `flashCustomKernelToolStripMenuItem_Click` |
| `flashNANDBPartitionToolStripMenuItem` | Flasher logiciel système décrypté | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `flashNANDBPartitionToolStripMenuItem_Click` |
| `flashNANDCPartitionToolStripMenuItem` | Flasher la partition données utilisateur | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `flashNANDCPartitionToolStripMenuItem_Click` |
| `flashUbootToolStripMenuItem` | Installer uboot | `menuStrip > kernelToolStripMenuItem` | - |
| `folderImagesSetToolStripMenuItem` | Jeu d'illustrations des dossiers | `foldersContextMenuStrip` | - |
| `foldersOriginalToolStripMenuItem` | Jeux d’origine à la racine -> Dossiers, scinder les jeux uniformément | `foldersContextMenuStrip` | `pagesModefoldersToolStripMenuItem_Click` |
| `foldersSplitByFirstLetterOriginalToolStripMenuItem` | Jeux d’origine à la racine -> Dossiers, scinder à partir de la première lettre | `foldersContextMenuStrip` | `pagesModefoldersToolStripMenuItem_Click` |
| `foldersSplitByFirstLetterToolStripMenuItem` | Dossiers, scinder en fonction de la première lettre | `foldersContextMenuStrip` | `pagesModefoldersToolStripMenuItem_Click` |
| `foldersToolStripMenuItem` | Dossiers, scinder les jeux uniformément | `foldersContextMenuStrip` | `pagesModefoldersToolStripMenuItem_Click` |
| `forceClovershellMembootsToolStripMenuItem` | Forcer memboots Clovershell | `menuStrip > settingsToolStripMenuItem > developerToolsToolStripMenuItem` | `forceClovershellMembootsToolStripMenuItem_Click` |
| `forceNetworkMembootsToolStripMenuItem` | Forcer memboots mode réseau | `menuStrip > settingsToolStripMenuItem > developerToolsToolStripMenuItem` | `forceNetworkMembootsToolStripMenuItem_Click` |
| `formatNANDCToolStripMenuItem` | Formater la partition données utilisateur | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `formatNANDCToolStripMenuItem_Click` |
| `formatSDCardToolStripMenuItem` | Formater carte SD | `menuStrip > toolsToolStripMenuItem` | `formatSDCardToolStripMenuItem_Click` |
| `generateModulesReportToolStripMenuItem` | Générer rapport de modules | `menuStrip > modulesToolStripMenuItem` | `generateModulesReportToolStripMenuItem_Click` |
| `gitHubPageWithActualReleasesToolStripMenuItem` | Page GitHub avec les nouvelles publications | `menuStrip > helpToolStripMenuItem` | `openWebsiteLink` |
| `globalCommandLineArgumentsexpertsOnluToolStripMenuItem` | Arguments de ligne de commande globaux (utilisateurs avancés !) | `menuStrip > settingsToolStripMenuItem` | - |
| `helpToolStripMenuItem` | &Aide | `menuStrip` | - |
| `importGamesFromMiniToolStripMenuItem` | Importer les jeux à partir de la mini | `menuStrip > toolsToolStripMenuItem` | `importGamesFromMiniToolStripMenuItem_Click` |
| `installModulesToolStripMenuItem` | Installer des modules supplémentaires | `menuStrip > modulesToolStripMenuItem` | `installModulesToolStripMenuItem_Click` |
| `japanToolStripMenuItem` | Japon | `menuStrip > settingsToolStripMenuItem > segaUiThemeToolStripMenuItem` | `changeM2Theme` |
| `joinOurDiscordServerToolStripMenuItem` | Joindre notre serveur Discord | `menuStrip > helpToolStripMenuItem` | `openWebsiteLink` |
| `kachikachiToolStripMenuItem` | Kachikachi | `menuStrip > settingsToolStripMenuItem > globalCommandLineArgumentsexpertsOnluToolStripMenuItem` | `globalCommandLineArgumentsexpertsOnluToolStripMenuItem_Click` |
| `kernelToolStripMenuItem` | &Kernel | `menuStrip` | - |
| `languageToolStripMenuItem` | Langue | `menuStrip > settingsToolStripMenuItem` | - |
| `leftmostToolStripMenuItem` | À gauche | `foldersContextMenuStrip > backFolderButtonPositionToolStripMenuItem` | `leftmostToolStripMenuItem_Click` |
| `manageModRepositoriesToolStripMenuItem` | Gérer les sources de mod | `menuStrip > modulesToolStripMenuItem` | `manageModRepositoriesToolStripMenuItem_Click` |
| `maximumGamesPerFolderToolStripMenuItem` | Nombre de jeux maximum par page/dossier | `foldersContextMenuStrip` | - |
| `membootCustomKernelToolStripMenuItem` | Démarrer le kernel custom à partir de la RAM | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `membootCustomKernelToolStripMenuItem_Click` |
| `membootOriginalKernelToolStripMenuItem` | Démarrer le kernel d’origine à partir de la RAM | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `membootOriginalKernelToolStripMenuItem_Click` |
| `membootRecoveryKernelToolStripMenuItem` | Démarrer le kernel de récupération à partir de la RAM | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `membootRecoveryKernelToolStripMenuItem_Click` |
| `messageOfTheDayToolStripMenuItem` | Message important | `menuStrip > helpToolStripMenuItem` | `messageOfTheDayToolStripMenuItem_Click` |
| `modulesToolStripMenuItem` | &Modules | `menuStrip` | - |
| `nameToolStripMenuItem` | Nom | `menuStrip > viewToolStripMenuItem > sortByToolStripMenuItem` | `sortByToolStripMenuItem_Click` |
| `normalModeToolStripMenuItem` | Mode normal | `menuStrip > kernelToolStripMenuItem > flashUbootToolStripMenuItem` | `flashUbootToolStripMenuItem_Click` |
| `openFTPInExplorerToolStripMenuItem` | Ouvrir le client FTP | `menuStrip > toolsToolStripMenuItem` | `openFTPInExplorerToolStripMenuItem_Click` |
| `openTelnetToolStripMenuItem` | Ouvrir le client Telnet | `menuStrip > toolsToolStripMenuItem` | `openTelnetToolStripMenuItem_Click` |
| `originalGamesToolStripMenuItem` | Jeux d'origine | `menuStrip > viewToolStripMenuItem` | - |
| `pagesOriginalToolStripMenuItem` | Jeux d’origine à la racine -> Pages, scinder les jeux uniformément | `foldersContextMenuStrip` | `pagesModefoldersToolStripMenuItem_Click` |
| `pagesToolStripMenuItem` | Pages, scinder les jeux uniformément | `foldersContextMenuStrip` | `pagesModefoldersToolStripMenuItem_Click` |
| `positionAtTheBottomToolStripMenuItem` | En bas | `menuStrip > viewToolStripMenuItem > originalGamesToolStripMenuItem` | `originalGamesPositionToolStripMenuItem_Click` |
| `positionAtTheTopToolStripMenuItem` | En haut | `menuStrip > viewToolStripMenuItem > originalGamesToolStripMenuItem` | `originalGamesPositionToolStripMenuItem_Click` |
| `positionHiddenToolStripMenuItem` | Cachés (désélectionnés) | `menuStrip > viewToolStripMenuItem > originalGamesToolStripMenuItem` | `originalGamesPositionToolStripMenuItem_Click` |
| `positionSortedToolStripMenuItem` | Triés | `menuStrip > viewToolStripMenuItem > originalGamesToolStripMenuItem` | `originalGamesPositionToolStripMenuItem_Click` |
| `prepareArtDirectoryToolStripMenuItem` | Préparer dossiers pour images | `menuStrip > toolsToolStripMenuItem` | `prepareArtDirectoryToolStripMenuItem_Click` |
| `presetsToolStripMenuItem` | Listes prédéfinies | `menuStrip > fileToolStripMenuItem` | - |
| `rRockinTheClassicsToolStripMenuItem` | /r/RockinTheClassics | `menuStrip > helpToolStripMenuItem` | `openWebsiteLink` |
| `rebootToolStripMenuItem` | Redémarrer | `menuStrip > toolsToolStripMenuItem` | `rebootToolStripMenuItem_Click` |
| `regionToolStripMenuItem` | Region | `menuStrip > viewToolStripMenuItem > sortByToolStripMenuItem` | `sortByToolStripMenuItem_Click` |
| `reloadGamesToolStripMenuItem` | &Recharger | `menuStrip > fileToolStripMenuItem` | `reloadGamesToolStripMenuItem_Click` |
| `removePrefixToolStripMenuItem` | Retirer un préfixe | `contextMenuStrip` | `removePrefixToolStripMenuItem_Click` |
| `repairGamesToolStripMenuItem` | Réparer les jeux | `contextMenuStrip` | `repairGamesToolStripMenuItem_Click` |
| `resetDefaultBootImageToolStripMenuItem` | Remettre par défaut | `menuStrip > toolsToolStripMenuItem > bootImageToolStripMenuItem` | `resetDefaultBootImageToolStripMenuItem_Click` |
| `resetOriginalGamesToolStripMenuItem` | Restaurer les jeux d'&origine | `menuStrip > fileToolStripMenuItem` | `resetOriginalGamesToolStripMenuItem_Click` |
| `resetROMHeaderToolStripMenuItem` | Réinitialiser l'entête SFROM pour les jeux sélectionnés | `contextMenuStrip > sFROMToolToolStripMenuItem1` | `resetROMHeaderToolStripMenuItem_Click` |
| `resetToolStripMenuItem` | Réinitialiser | `menuStrip > kernelToolStripMenuItem` | `resetToolStripMenuItem_Click` |
| `resetUsingCombinationOfButtonsToolStripMenuItem` | Utiliser une combinaison de boutons pour retourner au menu | `menuStrip > settingsToolStripMenuItem > cloverconHackToolStripMenuItem` | `cloverconHackToolStripMenuItem_Click` |
| `retroarchToolStripMenuItem` | Retroarch | `menuStrip > settingsToolStripMenuItem > globalCommandLineArgumentsexpertsOnluToolStripMenuItem` | `globalCommandLineArgumentsexpertsOnluToolStripMenuItem_Click` |
| `rightmostToolStripMenuItem` | À droite | `foldersContextMenuStrip > backFolderButtonPositionToolStripMenuItem` | `rightmostToolStripMenuItem_Click` |
| `sDModeToolStripMenuItem` | Mode SD | `menuStrip > kernelToolStripMenuItem > flashUbootToolStripMenuItem` | `flashUbootToolStripMenuItem_Click` |
| `sFROMToolToolStripMenuItem` | SFROM Tool | `menuStrip > settingsToolStripMenuItem` | - |
| `sFROMToolToolStripMenuItem1` | Outil SFROM Tool | `contextMenuStrip` | - |
| `saveDmesgOutputToolStripMenuItem` | Sauvegarder le résultat de 'dmesg' | `menuStrip > toolsToolStripMenuItem` | `saveDmesgOutputToolStripMenuItem_Click` |
| `saveSettingsToNESMiniNowToolStripMenuItem` | Sauvegarder les paramètres vers la mini maintenant | `menuStrip > settingsToolStripMenuItem` | `saveSettingsToNESMiniNowToolStripMenuItem_Click` |
| `saveStateManagerToolStripMenuItem` | Gestionnaire des sauvegardes d’état | `menuStrip > toolsToolStripMenuItem` | `saveStateManagerToolStripMenuItem_Click` |
| `scanForNewBoxArtForSelectedGamesToolStripMenuItem` | Rechercher de nouvelles images pour les jeux sélectionnés | `contextMenuStrip` | `scanForNewBoxArtForSelectedGamesToolStripMenuItem_Click` |
| `scrapeSelectedGamesToolStripMenuItem` | Rechercher les bases de données en ligne | `contextMenuStrip` | `scrapeSelectedGamesToolStripMenuItem_Click` |
| `searchToolStripMenuItem` | Rechercher | `menuStrip > fileToolStripMenuItem` | `searchToolStripMenuItem_Click` |
| `segaUiThemeToolStripMenuItem` | Thème IU SEGA | `menuStrip > settingsToolStripMenuItem` | - |
| `selectButtonCombinationToolStripMenuItem` | Définir la combinaison du bouton Reset | `menuStrip > settingsToolStripMenuItem > cloverconHackToolStripMenuItem` | `selectButtonCombinationToolStripMenuItem_Click` |
| `selectEmulationCoreToolStripMenuItem` | Sélectionner le coeur d'émulation... | `contextMenuStrip` | `selectEmulationCoreToolStripMenuItem_Click` |
| `separateGamesForMultibootToolStripMenuItem` | Séparer les collections de jeux pour multiboot | `menuStrip > settingsToolStripMenuItem` | `separateGamesForMultibootToolStripMenuItem_Click` |
| `separateGamesStorageToolStripMenuItem` | Séparer les dossiers de jeux | `menuStrip > settingsToolStripMenuItem` | `separateGamesStorageToolStripMenuItem_Click` |
| `setRegionToolStripMenuItem` | Set Region | `contextMenuStrip` | - |
| `settingsToolStripMenuItem` | &Paramètres | `menuStrip` | - |
| `showGamesWithoutBoxArtToolStripMenuItem` | Regrouper les jeux sans image | `menuStrip > viewToolStripMenuItem` | `showGamesWithoutBoxArtToolStripMenuItem_Click` |
| `sortByToolStripMenuItem` | Trier par | `menuStrip > viewToolStripMenuItem` | - |
| `switchRunningFirmwareToolStripMenuItem` | Changer de firmware | `menuStrip > toolsToolStripMenuItem` | `switchRunningFirmwareToolStripMenuItem_Click` |
| `syncStructureForAllGamesCollectionsToolStripMenuItem` | Synchroniser les réglages pour toutes les collections | `foldersContextMenuStrip` | `syncStructureForAllGamesCollectionsToolStripMenuItem_Click` |
| `synchronizeToolStripMenuItem` | Synchroniser | `menuStrip > fileToolStripMenuItem` | `buttonStart_Click` |
| `systemToolStripMenuItem` | Console | `menuStrip > viewToolStripMenuItem > sortByToolStripMenuItem` | `sortByToolStripMenuItem_Click` |
| `takeScreenshotToolStripMenuItem` | Faire une capture d’écran | `menuStrip > toolsToolStripMenuItem` | `takeScreenshotToolStripMenuItem_Click` |
| `technicalInformationToolStripMenuItem` | Information technique... | `menuStrip > helpToolStripMenuItem` | `technicalInformationToolStripMenuItem_Click` |
| `toolFlashTheWholeNANDStripMenuItem` | Flasher la NAND tout entière (Experts seulement!) | `menuStrip > kernelToolStripMenuItem > advancedToolStripMenuItem` | `toolFlashTheWholeNANDStripMenuItem_Click` |
| `toolsToolStripMenuItem` | Ou&tils | `menuStrip` | - |
| `uninstallModulesToolStripMenuItem` | Désinstaller les modules supplémentaires | `menuStrip > modulesToolStripMenuItem` | `uninstallModulesToolStripMenuItem_Click` |
| `uninstallToolStripMenuItem` | Désinstaller les modules supplémentaires | `menuStrip > kernelToolStripMenuItem` | `uninstallToolStripMenuItem_Click` |
| `unitedStatesToolStripMenuItem` | États-Unis | `menuStrip > settingsToolStripMenuItem > segaUiThemeToolStripMenuItem` | `changeM2Theme` |
| `upABStartOnSecondControllerToolStripMenuItem` | "Haut+A+B = Start" sur la manette 2 | `menuStrip > settingsToolStripMenuItem > cloverconHackToolStripMenuItem` | `upABStartOnSecondControllerToolStripMenuItem_Click` |
| `uploadTotmpforTestingToolStripMenuItem` | Envoyer vers /tmp (pour tests) | `menuStrip > settingsToolStripMenuItem > developerToolsToolStripMenuItem` | `uploadTotmpforTestingToolStripMenuItem_Click` |
| `useLinkedSyncToolStripMenuItem` | Utiliser la synchro liée | `menuStrip > settingsToolStripMenuItem` | `useLinkedSyncToolStripMenuItem_Click` |
| `usePCMPatchWhenAvailableToolStripMenuItem` | Utiliser patch PCM quand dispo | `menuStrip > settingsToolStripMenuItem > sFROMToolToolStripMenuItem` | `usePCMPatchWhenAvailableToolStripMenuItem_Click` |
| `useXYOnClassicControllerAsAutofireABToolStripMenuItem` | Utiliser X/Y sur la Manette Classique comme autofire A/B | `menuStrip > settingsToolStripMenuItem > cloverconHackToolStripMenuItem` | `useXYOnClassicControllerAsAutofireABToolStripMenuItem_Click` |
| `viewToolStripMenuItem` | &Vue | `menuStrip` | - |
