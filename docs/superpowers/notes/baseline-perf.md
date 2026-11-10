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
