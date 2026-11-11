# Interface PC B1 : livraison (2026-11-11)

## Ce qui est livre

- Toutes les fenetres de hakchi CE redessinees dans le theme B1 (Tasks 1 a 10) : palette, Jost, controles dessines, fenetre principale « A · Jaquettes » (barre du haut, systemes, puces, grille de jaquettes, fiche du jeu, pied de page), fenetres secondaires, de tache, de flash, de mods, gestionnaire de dossiers, Chercher les jaquettes, textes FR / EN.
- Branche `snes-usa/ui-pc`, non poussee. Dernier commit de code : `19a7a573` (correctifs de performance de la Task 11).
- Aucune fonction retiree. Le moteur (flash, FEL, synchronisation, mods, installation) et les ecrans de flash et d'installation gardent leur comportement.
- Tests : `N:\dotnet\dotnet.exe test hakchi_gui.Tests` (apres le build msbuild Debug) : 504 reussis.

## Ou

| Quoi | Emplacement |
|---|---|
| Build Release | `hakchi_gui\bin\Release\net48\hakchi.exe` (3.9.3+e158744f, meme code que 19a7a573 ; x86 `Requires32Bit`, reglage du csproj d'origine inchange) |
| Copie de test | `P:\HakchiSNESUSA\app\hakchi_new.exe` (Release, mutex patche) + `hakchi_new.exe.config` + `languages\*\hakchi.resources.dll` |
| Galerie apres, 96 dpi | `P:\HakchiSNESUSA\ui_after\` (50 fenetres et controles, fr-FR) |
| Galerie apres, 144 dpi | `P:\HakchiSNESUSA\ui_after_144\` (144 dpi emule) |
| Galerie avant | `P:\HakchiSNESUSA\ui_before\` (Task 0) |
| Planche avant / apres | `P:\HakchiSNESUSA\ui_board\index.html` |
| Mesures | `docs/superpowers/notes/baseline-perf.md` |

## Lancer la copie de test

1. Ouvrir `P:\HakchiSNESUSA\app\hakchi_new.exe`. Mode portable : la configuration, les jeux et les jaquettes sont ceux de `P:\HakchiSNESUSA\app`, pas ceux de `R:`.
2. Le mutex patche (`hakchi3`) permet de la lancer a cote du hakchi 3.8.0 de l'utilisateur (`R:`), sans l'activer.
3. Au demarrage, hakchi telecharge seul le dernier hakchi.hmod et la liste des jeux, comme l'original.
4. Depuis la copie de test, ne jamais utiliser Synchroniser ou Exporter, ni rien qui ecrive sur la console.

Redeployer apres un nouveau build :
- copier `hakchi.exe` en `hakchi_new.exe` ;
- dans la copie, chercher la chaine UTF-16LE « hakchi2 » precedee de l'octet de longueur 15 (une seule occurrence) ;
- remplacer le « 2 » par « 3 » (offset de la chaine + 12) ;
- copier aussi `hakchi.exe.config` en `hakchi_new.exe.config`, et les satellites `languages\*\hakchi.resources.dll`.

L'offset change a chaque build : 4978411 pour ce Release. Ne jamais toucher a `P:\HakchiSNESUSA\app\hakchi.exe` ni a `R:`.

## Mesures (detail dans baseline-perf.md)

- Tous les temps sont comptes depuis le lancement.
- 10 lancements alternes avant / apres, mediane (IQR).
- Les deux exe sont des builds **Debug** :
  - avant : `hakchi_mtx.exe`, interface d'origine ;
  - apres : `19a7a573`.
- Mesure par capture de la fenetre (`tools/perf/measure-visible.ps1`), sans passer par le thread UI de hakchi.

| Mesure | avant | apres | objectif | verdict |
|---|---|---|---|---|
| B fenetre principale visible | 972 ms (43) | 939,5 ms (58) | <= +5 % | tenu (-3,3 %) |
| Premiers jeux a l'ecran (C_visible) | 1552 ms (39) | 1510,5 ms (99) | pas plus lent | tenu (-2,7 %, dans le bruit) |
| Grille complete (jaquettes visibles dessinees) | 1552 ms (liste sans jaquettes) | 1593 ms (148) | < 1 s | non tenu depuis le lancement ; tenu depuis l'affichage des jeux (+60 a 150 ms) ; +41 ms contre l'avant, dans le bruit |

Le seuil de 1 s ne peut pas etre tenu depuis le lancement, ni par la refonte ni par l'interface d'origine : la fenetre seule apparait a ~0,94 s.

Le Release livre n'a pas de reference Release « avant » ; il n'entre pas dans ces verdicts.

Deux correctifs (`19a7a573`) ont ramene B et C dans les objectifs. Avant eux, mesure par compteur, B etait a +8,2 % et C a +7,0 %. Les correctifs :
- l'habillage ne cree plus le handle des listes deroulantes cachees ;
- la grille se peint des sa relecture.

## Points connus, reportes

- La jauge de stockage compte la memoire interne, comme l'original, meme quand les jeux sont sur USB.
- En droite-a-gauche force, le pied de page et les puces de filtre ne sont pas en miroir. L'ar-SA d'origine n'active pas le RTL : les utilisateurs ne le voient pas.
- CoverBatchForm n'est pas testee langue par langue (place des textes).
- SettingsForm est une fenetre vide de l'original.
- Le genre de F-ZERO est vide dans les donnees d'origine.
- Galerie : barres de titre claires (DrawToBitmap), sombres dans l'application. Le 144 dpi est emule (mise a l'echelle de la police), l'application reste non DPI-aware comme l'original.
- `patches\` : verifie le 2026-11-11, `hakchi_gui\bin\Release\net48\patches` et `P:\HakchiSNESUSA\app\patches` sont identiques. Plus generalement, aucun fichier du build Release ne manque dans la copie de test (verification par chemins litteraux). Le « 13 fichiers manquants » de la premiere version venait d'un faux positif : les crochets des noms etaient lus comme des jokers par `Test-Path`.
