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
- Script : `tools/perf/measure-startup.ps1` (`-ConsoleUnplugged -Runs 3`, filtrer la sortie avec `Select-String "^run"`). Les deux scripts de mesure lancent hakchi : ils refusent de tourner sans `-ConsoleUnplugged` (console debranchee).

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

## Refonte PC (projet 2) : mesures apres refonte (2026-11-11, Task 11, corrigees au fix round 1)

### Ce que mesure chaque chiffre

Tous les temps partent du lancement du processus (`Process.Start`).

| Mesure | Definition | Outil |
|---|---|---|
| B | la fenetre principale « hakchi CE... » est visible | les deux scripts |
| C (compteur) | `LVM_GETITEMCOUNT` > 0 sur listViewGames (cachee dans l'interface B1) | `measure-startup.ps1 -HiddenLists` |
| C_visible | des jeux sont **a l'ecran** dans la zone de la liste | `measure-visible.ps1` |
| Grille complete | toutes les cases visibles affichent leur jaquette | `measure-visible.ps1` |

Precisions sur C_visible :
- interface d'origine : la ListView blanche contient du texte, au moins 150 px sombres ;
- interface B1 : la grille montre ses cases et leurs titres, au moins 5 % de pixels RowActive `#26252E`.

Precisions sur la grille complete :
- interface B1 : les pixels satures (jaquettes) atteignent 97 % de leur nombre final, soit 21 cases a 96 dpi ;
- interface d'origine : la liste n'a pas de jaquettes et s'affiche d'un coup, donc grille complete = C_visible.

C (compteur) ne dit pas ce que voit l'utilisateur. Dans l'interface B1, la liste comptee est cachee ; les jeux n'apparaissent qu'une fois la grille peinte. Les verdicts « premiers jeux » et « grille » se jugent donc sur C_visible et sur la grille complete.

`measure-visible.ps1` capture la fenetre depuis le processus de mesure, par `GetWindowDC` + `BitBlt` sur la surface DWM de la fenetre, toutes les ~30 ms. Il n'envoie aucun message a hakchi et n'attend pas son thread UI.

Verification sur une fenetre de test dont le thread UI dort 3 s : la capture repond en moins de 1 ms et montre le contenu reel. En revanche, `measure-startup.ps1` (C et `-Grid`) attend le thread UI de hakchi (`LVM_GETITEMCOUNT` dans la meme boucle) : ses instants sont des bornes hautes. La premiere version de ce rapport en a deduit a tort que les titres n'apparaissaient que ~400 ms apres C.

### Configuration et conditions

- « Avant » : `P:\HakchiSNESUSA\app\hakchi_mtx.exe`, interface d'origine, 3.9.3-98aef422. **C'est un build Debug** : `AssemblyConfiguration("Debug")`, `DebuggableAttribute` 0x107, optimisations JIT coupees.
- « Apres » : `hakchi_new.exe` construit en **Debug** depuis `19a7a573`, meme configuration. Seule cette paire sert aux verdicts.
- Le Release livre n'a pas de reference Release « avant ». Ses chiffres ne servent pas de comparaison.
- Meme dossier, meme bibliotheque : 198 dossiers, 219 elements, console non connectee.
- 10 lancements alternes avant / apres, mediane et ecart interquartile (IQR).
- La machine est chargee par d'autres processus (python, Adobe, par moments qemu et Alchemia), d'ou des valeurs absolues instables d'une serie a l'autre. Seule l'alternance compare a conditions egales.

### Resultats (Debug contre Debug, 10 alternances, 2026-11-11)

| Mesure (depuis le lancement) | avant : mediane (IQR) | apres : mediane (IQR) | ecart | objectif | verdict |
|---|---|---|---|---|---|
| B fenetre visible | 972 ms (43) | 939,5 ms (58) | -3,3 % | <= +5 % | **tenu** |
| C_visible premiers jeux a l'ecran | 1552 ms (39) | 1510,5 ms (99) | -2,7 % | pas plus lent | **tenu** (ecart dans le bruit) |
| Grille complete | 1552 ms (39), liste sans jaquettes | 1593 ms (148) | +2,6 % (+41 ms) | < 1 s sur ~257 jaquettes | voir ci-dessous |

Valeurs brutes, en ms.

Avant :
- B : 884, 911, 930, 945, 969, 975, 976, 977, 982, 999
- C_visible : 1449, 1526, 1532, 1536, 1538, 1566, 1572, 1572, 1581, 1649

Apres :
- B : 870, 902, 930, 931, 932, 947, 975, 993, 994, 1015
- C_visible : 1465, 1466, 1468, 1475, 1494, 1527, 1558, 1572, 1589, 1622
- grille complete : 1526, 1527, 1529, 1556, 1588, 1598, 1681, 1685, 1697, 1742
- grille complete - C_visible : 60 a 153 ms, mediane 62,5 ms

Verdict sur la grille (« premier rendu complet en moins de 1 s ») :
- **Depuis le lancement : non tenu.** Mediane 1,59 s. L'objectif est hors d'atteinte depuis le lancement : la fenetre principale elle-meme n'apparait qu'a ~0,94 s, et la liste d'origine (texte seul) s'affichait a 1,55 s.
- **Depuis l'affichage des premiers jeux : tenu.** Les jaquettes visibles sont toutes dessinees 60 a 150 ms apres les titres.
- **Par rapport a l'avant : dans le bruit.** La grille complete arrive 41 ms apres la liste d'origine, et cet ecart est inferieur a l'IQR de la serie apres (148 ms).

### Historique de la Task 11

1. **Premiere mesure, avec C (compteur), avant les correctifs** (code 89d169e7, Debug contre Debug, 10 alternances) : B +8,2 % (1210 contre 1309 ms), C +7,0 %. Les objectifs etaient rates.
2. **Correctifs `19a7a573`.**
   - `ComboPaint.HookStock` reposait `ItemHeight` ; ce setter creait le handle de 4 listes deroulantes cachees et de leurs parents, ~150 ms dans le premier habillage de MainForm. La valeur est maintenant posee a la creation du handle. Test : `SkinningHiddenComboBoxDefersHandleAndKeepsItemHeight`, qui verifie aussi la valeur native `CB_GETITEMHEIGHT`.
   - La grille se peint juste apres sa relecture (`PostedRebuild` + `Update()`), sans attendre que le thread UI soit libre.
3. **Mesures avec C (compteur) apres les correctifs** (alternance, mediane) :
   - Debug : B -1,3 %, C -5,5 %.
   - Release « apres » contre Debug « avant » : B +2,7 %, C -5,5 %. Ces deux chiffres ne comparent pas la meme configuration et l'ecart est dans le bruit (IQR ~200 ms) : ils sont **retires des verdicts**.
4. **Fix round 1.** Nouvelle mesure de ce que voit l'utilisateur (`measure-visible.ps1`) : tableau ci-dessus. Aucun nouveau correctif n'a ete necessaire : C_visible n'est pas plus lent, et la grille suit les titres de 60 a 150 ms.
