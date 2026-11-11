# Mesures de reference au demarrage (avant toute modification)

- Date : 2026-11-10
- Machine : Intel Core i5-14600K, 64 Go RAM, Windows 10 Pro 19045
- Binaire : build local Debug de la branche `snes-usa/pochettes` (hakchi CE v3.9.3-98aef422), exemplaire `hakchi_mtx.exe` (identique a `hakchi.exe` sauf le nom du mutex, voir build-local.md piege 6)
- Dossier : `P:\HakchiSNESUSA\app` (disque dur WD10EZEX 1 To, HDD), bibliotheque copiee : 195 jeux, console non connectee
- Methode : demarrage du processus (`Start-Process`), interrogation toutes les 50-100 ms, fermeture par PID. Pas d'acces GUI : la liste est lue sans rien cliquer, par message `LVM_GETITEMCOUNT` sur les ListView de la fenetre principale.

## Resultats (3 lancements, mediane)

| Mesure | Run 1 | Run 2 | Run 3 | Mediane |
|---|---|---|---|---|
| A. `MainWindowHandle` non nul et titre non vide (definition de la consigne ; c'est en fait la fenetre « Chargement des mods... ») | 768 ms | 450 ms | 455 ms | **455 ms** |
| B. Fenetre principale visible (titre « hakchi CE v3.9.3... ») | 785 ms | 767 ms | 768 ms | **768 ms** |
| C. Liste de jeux : premiers elements presents (ListView > 0) | 1429 ms | 1236 ms | 1220 ms | **1236 ms** |
| D. Nombre d'elements final de la liste | 216 | 216 | 216 | 216 |

Le compteur ne bouge plus apres le premier instant non nul : le chargement de la liste est donc termine environ 1,2 a 1,4 s apres le lancement. A vient d'une premiere serie de 3 lancements, B/C/D d'une seconde serie de 3 lancements (scripts de mesure hors depot).

## Limites

- Le rendu graphique complet (peinture des jaquettes) n'est pas mesure : pas d'acces GUI, seulement le compteur d'elements du ListView.
- Cache de fichiers chaud apres le premier lancement ; le tout premier lancement du jour peut etre plus long.
- Apres B/C, hakchi lance seul « Telechargement du dernier hakchi » puis « Mise a jour de la liste... » (reseau, non attendu ici).
- 216 elements pour 195 jeux : le compteur inclut selon toute vraisemblance d'autres ListView ou dossiers ; a recouper lors de tests visuels.

## Refonte PC (projet 2) : mesures de reference avant refonte (2026-11-10)

- Binaire : `P:\HakchiSNESUSA\app\hakchi_new.exe` (copie de test, patchee pour le mutex), version `3.9.3-1fa4523a`, interface d'origine (avant la Task 1).
- Meme methode que ci-dessus : demarrage du processus, interrogation toutes les 40 ms, `LVM_GETITEMCOUNT` sur les `SysListView32` visibles de la fenetre « hakchi CE », fermeture par PID (`Stop-Process -Id`). Console non connectee, rien de clique. Aucun hakchi n'etait lance au depart.
- Bibliotheque : 195 dossiers de jeux. Le compteur de la liste vaut 216 (195 jeux + groupes / dossiers), comme dans la serie precedente.
- Script : `tools/perf/measure-startup.ps1` (`-Runs 3`, filtrer la sortie avec `Select-String "^run"`).

| Mesure | Run 1 | Run 2 | Run 3 | Mediane |
|---|---|---|---|---|
| B. Fenetre principale visible (titre « hakchi CE v3.9.3... ») | 883 ms | 995 ms | 1005 ms | **995 ms** |
| C. Liste de jeux : premiers elements presents (ListView > 0) | 1192 ms | 1549 ms | 1328 ms | **1328 ms** |
| « Grille complete » (provisoire) : compteur stable (2,5 s) et >= 195 | 1192 ms | 1549 ms | 1328 ms | **1328 ms** |
| D. Nombre d'elements final | 216 | 216 | 216 | 216 |

Notes :
- Le compteur passe de 0 a 216 en un seul pas (`LoadGamesTask.UpdateListView`) : « grille complete » et C coincident tant que la grille n'est pas la vue cible. A redefinir en Task 3 / 11 comme « jaquettes toutes dessinees » une fois la grille de jaquettes en place.
- B est plus haut que la serie du matin (768 ms) : variation de machine et de cache, pas de changement de code. Les budgets de la refonte se jugent contre **cette** serie : B <= 995 x 1,05 ~ 1045 ms, C <= 1328 ms.
- La galerie (`tools/FormGallery`) ne mesure pas la performance : elle ne rend que l'etat vide de conception des fenetres.

## Refonte PC (projet 2) : mesures apres refonte (2026-11-11, Task 11)

- Binaires : `P:\HakchiSNESUSA\app\hakchi_new.exe` (copie de test patchee pour le mutex), branche `snes-usa/ui-pc`, commit `19a7a573`, en Debug (meme configuration que la serie « avant ») puis en Release (livraison). Bibliotheque : 198 dossiers de jeux, compteur 219.
- Machine chargee pendant la journee (VM qemu, python, Alchemia : 50 a 60 % de CPU hors hakchi) : les valeurs absolues bougent de +-30 %. Pour comparer a conditions egales, chaque serie alterne l'exe d'avant et celui d'apres, 10 fois. Reference « avant » : `hakchi_mtx.exe` (interface d'origine, 3.9.3-98aef422, meme dossier, meme bibliotheque).
- Script : `tools/perf/measure-startup.ps1 -Runs 1 -Exe <exe> -HiddenLists`, alterne. `-HiddenLists` : listViewGames est maintenant cachee derriere la grille, son compteur est lu quand meme (meme message `LVM_GETITEMCOUNT`, meme liste).
- « Grille complete » : `-Grid`. Un lancement de calibration repere la fenetre de la grille, puis chaque lancement capture la grille (PrintWindow) toutes les ~40 ms. La mesure est la derniere modification des pixels avant 2,5 s de stabilite. Les captures intermediaires (`-SaveChanges`) montrent la suite : grille vide, cases avec titres, jaquettes. Limite : comme pour C, l'echantillonnage attend que le thread UI reponde ; les instants sont des bornes hautes.

### Premiere mesure, avant correction (Debug, 10 alternances, mediane)

| | avant (hakchi_mtx) | apres (19a7a573^) | ecart |
|---|---|---|---|
| B fenetre visible | 1210 ms | 1309 ms | **+8,2 %** (objectif +5 % : rate) |
| C premiers jeux | 1591 ms | 1702 ms | **+7,0 %** (objectif : pas plus lent : rate) |

Cause trouvee par traces temporaires (retirees) : le premier habillage de MainForm prenait 190 a 280 ms, dont ~150 ms pour 4 listes deroulantes. `ComboPaint.HookStock` reposait `ItemHeight`, et ce setter cree le handle de la liste et de tous ses parents, meme caches (fiche de jeu). La valeur est maintenant posee a la creation du handle (meme hauteur, test `SkinningHiddenComboBoxDefersHandleAndKeepsItemHeight`). Second correctif : la grille se peignait seulement quand le thread UI etait libre, soit apres la partie synchrone de `UpdateFiles` (250 a 500 ms apres le remplissage). Elle se peint maintenant des la relecture (`CoverGridView.PostedRebuild`).

### Mesure finale (10 alternances, mediane)

| | avant (hakchi_mtx) | apres | ecart | objectif |
|---|---|---|---|---|
| B fenetre visible, Debug | 987,5 ms | 974,5 ms | -1,3 % | <= +5 % : tenu |
| C premiers jeux, Debug | 1327 ms | 1254,5 ms | -5,5 % | pas plus lent : tenu |
| B fenetre visible, Release | 1090 ms | 1119 ms | +2,7 % | <= +5 % : tenu |
| C premiers jeux, Release | 1497 ms | 1414,5 ms | -5,5 % | pas plus lent : tenu |

Valeurs brutes (Release, apres) : B 905, 914, 940, 996, 1116, 1122, 1129, 1162, 1295, 1321 ; C 1169, 1178, 1187, 1293, 1404, 1425, 1460, 1551, 1585, 1648.

### Grille complete (Release, 3 lancements)

| Mesure | Run 1 | Run 2 | Run 3 | Mediane |
|---|---|---|---|---|
| B | 989 ms | 905 ms | 1011 ms | 989 ms |
| C (compteur > 0) | 1237 ms | 1138 ms | 1382 ms | 1237 ms |
| Grille complete (jaquettes visibles toutes dessinees) | 1735 ms | 1637 ms | 1930 ms | 1735 ms |
| Grille complete - C | 498 ms | 499 ms | 548 ms | **499 ms** (objectif < 1 s : tenu) |

- 21 jeux visibles (645 x 634 px, 96 dpi), 219 elements. Etat final verifie a l'image : toutes les cases visibles ont leur jaquette.
- La grille montre les titres environ 400 ms apres C, puis les jaquettes ~110 ms plus tard. Ces instants sont des bornes hautes : le thread UI execute alors `UpdateFiles` (lecture de hakchi.hmod) et ne repond pas a l'echantillonnage.
- En Debug, apres le premier correctif seulement, la grille complete arrivait 620 ms apres C. Dans 2 lancements sur 5, un dernier changement venait plus tard : la mise en evidence d'un jeu (texte du titre plus clair), sans lien avec les jaquettes.
- Avant la refonte, la « grille » etait la ListView : texte visible des C, pas de jaquettes.
