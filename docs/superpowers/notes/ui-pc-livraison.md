# Interface PC B1 : livraison (2026-11-11)

## Ce qui est livre

- Toutes les fenetres de hakchi CE redessinees dans le theme B1 (Tasks 1 a 10) : palette, Jost, controles dessines, fenetre principale « A · Jaquettes » (barre du haut, systemes, puces, grille de jaquettes, fiche du jeu, pied de page), fenetres secondaires, de tache, de flash, de mods, gestionnaire de dossiers, Chercher les jaquettes, textes FR / EN.
- Branche `snes-usa/ui-pc`, non poussee. Dernier commit de code : `19a7a573` (correctifs de performance de la Task 11).
- Aucune fonction retiree. Le moteur (flash, FEL, synchronisation, mods, installation) et les ecrans de flash et d'installation gardent leur comportement.
- Tests : `N:\dotnet\dotnet.exe test hakchi_gui.Tests` (apres le build msbuild Debug) : 504 reussis.

## Ou

| Quoi | Emplacement |
|---|---|
| Build Release | `hakchi_gui\bin\Release\net48\hakchi.exe` (3.9.3+19a7a573, x86 comme le projet d'origine) |
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

L'offset change a chaque build : 4978139 pour ce Release. Ne jamais toucher a `P:\HakchiSNESUSA\app\hakchi.exe` ni a `R:`.

## Mesures (detail dans baseline-perf.md)

Series alternees avant / apres, 10 fois, mediane. Reference « avant » : `hakchi_mtx.exe`, interface d'origine.

| | avant | apres (Release) | objectif |
|---|---|---|---|
| B fenetre principale visible | 1090 ms | 1119 ms (+2,7 %) | <= +5 % : tenu |
| C premiers jeux | 1497 ms | 1414,5 ms (-5,5 %) | pas plus lent : tenu |
| Grille de jaquettes complete apres C | - | 499 ms | < 1 s : tenu |

En Debug, la meme comparaison donne B -1,3 % et C -5,5 %.

Avant correction, les objectifs etaient rates : B +8,2 % et C +7,0 %. Deux correctifs ont ete faits (commit `19a7a573`) :
- l'habillage ne cree plus le handle des listes deroulantes cachees ;
- la grille se peint des que la liste est chargee.

## Points connus, reportes

- La jauge de stockage compte la memoire interne, comme l'original, meme quand les jeux sont sur USB.
- En droite-a-gauche force, le pied de page et les puces de filtre ne sont pas en miroir. L'ar-SA d'origine n'active pas le RTL : les utilisateurs ne le voient pas.
- CoverBatchForm n'est pas testee langue par langue (place des textes).
- SettingsForm est une fenetre vide de l'original.
- Le genre de F-ZERO est vide dans les donnees d'origine.
- Galerie : barres de titre claires (DrawToBitmap), sombres dans l'application. Le 144 dpi est emule (mise a l'echelle de la police), l'application reste non DPI-aware comme l'original.
- 13 fichiers `patches\*.ips` du build manquent dans la copie de test. Ils n'ont pas ete copies, car seuls l'exe, sa config et les satellites de langue sont redeployes. Tous les autres fichiers du build Release y sont deja.
