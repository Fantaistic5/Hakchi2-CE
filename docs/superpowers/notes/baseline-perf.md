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
