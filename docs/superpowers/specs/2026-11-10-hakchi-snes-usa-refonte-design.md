# Hakchi2-CE « SNES USA » : pochettes, thème TV, interface PC

Date : 2026-11-10 · Statut : conception relue (relecture « builder » faite), en attente de validation utilisateur

## 1. Intention

L'utilisateur possède une **SNES Mini EUR** hackée avec hakchi CE (noyau madmonkey, hakchi `v1.0.4-126`, RetroArch 1.8.4 Xtreme + Ozone, CPU 1,34 GHz). Il pilote la console avec **hakchi2 CE 3.8.0** (avril 2020) en version portable, sur `R:\hakchi2-ce-3.8.0-release` (bibliothèque locale de 195 jeux, `config\config.json`). Ses ~257 jeux sont sur une clé USB NTFS de 14 Go (`Q:` sur le PC), branchée en OTG sur la console : 52 SNES d'origine, 85 arcade, 20 GBA/GB/GBC/SMS, 14 NES, 11 Mega Drive, 10 N64, 7 PSP, 5 PS1 et 1 3DO.

Il veut :

- un hakchi **qui trouve les pochettes**, ce qui ne marche plus depuis longtemps ;
- un ensemble **« fonctionnel et classe » aux couleurs de la SNES USA**, sur la TV comme sur le PC, **sans effet « IA »** (pas de dégradés décoratifs, de halos, de glassmorphism ni de néons) ;
- un code **plus propre et plus rapide** là où on intervient.

Le dépôt `J:\AI\Claude_code_local\Hakchi2-CE` est un clone de `TeamShinkansen/Hakchi2-CE` (HEAD 2025-11-20). Il contient 51 commits non publiés après la dernière version v3.9.3 (2022), dont « Fix Google image search ».

## 2. Déjà fait (hors de ce document)

- Sauvegardes complètes dans `P:\Backups\SNESMini_2026-11-10\` : la clé `Q:` entière (`cle_Q`, 8,87 Go) et le `/var/lib/hakchi` de la console (`console_var_lib_hakchi.tar`).
- Correctifs PS1/3DO appliqués sur la console : `pcsx_rearmed_async_cd = async`, `4do_cpu_overclock = 1.0x`, et des overrides par core `config/PCSX-ReARMed/PCSX-ReARMed.cfg` (`audio_latency` 96) et `config/4DO/4DO.cfg` (128). Les originaux sont dans `/etc/libretro/backup_claude_20261110/`. **Ces correctifs n'ont pas encore été testés sur TV** : l'utilisateur les testera plus tard. Si ça craque encore, la piste suivante est la clé NTFS (`ntfs-3g`, coûteux en CPU) → FAT32, mais seulement avec son accord.
- Entrées SOTN en double ou cassées repérées (« PSX1-audio » = piste audio seule, « PSX1 » = sans .cue). On ne les supprime pas sans son accord.

## 3. Découpage et ordre

Le travail se fait en trois projets, dans cet ordre, parce que l'interface PC (projet 3) réutilise le moteur de pochettes (projet 1) :

1. **Pochettes** (moteur, dans le code hakchi)
2. **Thème TV « B1 »** (hmod pour la console : menu Nintendo + menu RetroArch + musique originale)
3. **Nouvelle interface PC « A »** (refonte complète de toutes les fenêtres de hakchi)

L'optimisation n'est pas un projet à part : elle s'applique à chaque fois qu'on touche du code (§7).

Livrable final : un hakchi compilé à partir de ce dépôt, portable, qui **remplace le 3.8.0 en reprenant la bibliothèque existante** (jeux, réglages, jaquettes déjà présentes), plus un hmod de thème.

## 4. Projet 1 : pochettes

**But :** chacun des ~257 jeux a une vraie jaquette, sans recherche jeu par jeu.

**Ordre de recherche (décidé avec l'utilisateur : « la jaquette exacte, sinon une belle image ») :**

> **Décision utilisateur (2026-11-10) :** on démarre **sans ScreenScraper**, avec libretro-thumbnails, TheGamesDB et Google. ScreenScraper reste prévu comme source branchable plus tard, quand l'utilisateur aura un compte et des identifiants développeur. L'architecture doit permettre de l'ajouter sans retoucher le reste. Conséquence acceptée : la couverture arcade sera moins bonne au départ. Pour l'arcade, libretro-thumbnails (`MAME`, `FBNeo - Arcade Games`) est interrogé via la correspondance nom de ROM → titre fournie par ces dépôts.
>
> TheGamesDB n'est utilisable localement qu'avec une clé API. Le build doit lire une clé fournie par l'utilisateur (réglages ou fichier `ApiKeys/TheGamesDB.txt`) et désactiver proprement la source quand il n'y en a pas.

1. *(plus tard)* **ScreenScraper.fr**, par empreinte du fichier (CRC/MD5/SHA1 + taille), en privilégiant la région du jeu, puis EUR, puis USA. C'est la seule source qui couvre bien l'arcade (noms de ROM comme `kof98`, `fatfursp`).
2. **libretro-thumbnails** (dépôts GitHub `libretro-thumbnails/<Système>`, dossier `Named_Boxarts`), par nom No-Intro/Redump. Gratuit, sans compte.
3. **TheGamesDB** (déjà dans `Scrapers/`). Attention : la clé API n'est injectée qu'en CI (`hakchi_gui/ApiKeys/TheGamesDB.txt` depuis un secret GitHub). En compilation locale, la source est donc désactivée tant qu'il n'y a pas de clé.
4. **Google Images** (`ImageGooglerForm`), avec la correction de 2025.

**Recherche groupée :** hakchi repère les jeux sans jaquette ou avec l'image générique, cherche pour tous, et propose la meilleure trouvaille par jeu. L'utilisateur valide tout d'un coup ou corrige au cas par cas. La source retenue (par exemple « ScreenScraper · EUR ») est mémorisée et affichée dans la fiche du jeu.

**Contraintes :**
- Respecter les quotas et les règles de chaque service (ScreenScraper limite les requêtes et le nombre de threads par compte).
- **Jeux multi-fichiers** (bin/cue, PBP, iso, zip arcade) : définir quel fichier sert à l'empreinte.
- Les sources s'intègrent dans l'interface `IScraper` existante (`Scrapers/Interfaces`).
- Ne jamais écraser une jaquette que l'utilisateur a mise lui-même sans le lui demander.

## 5. Projet 2 : thème TV « B1 »

Maquette validée : `.superpowers/brainstorm/9314-1794315727/content/theme-tv-b2.html`, variante **B1 · Interrupteur**.

**Langage visuel (décidé) :**
- Fond **ardoise uni `#1C1B22`**, aucun dégradé. Seul décor : la double rainure de la console US, réduite à deux filets `#2C2A34` en haut.
- Exactement quatre couleurs : ardoise `#1C1B22`, filet `#2C2A34`, violet manette `#4A3C8C` (et `#6B5BB5`/`#5747A0` pour les stries), lavande `#A79FD6`. Texte `#E4E2EA` / secondaire `#8E8A9C`.
- **Typo géométrique façon Futura** (esprit des notices Nintendo des années 90).
- Jeu sélectionné : souligné par un **petit interrupteur violet strié** (POWER/RESET de la console US). Pas de halo ni de lueur.
- Boutons à l'écran comme sur la manette US : **A/B violet bombé, X/Y lavande creusé**.
- Composition alignée à gauche, comme une fiche de jeu.

**Musique (décidé) :** un **morceau original** dans l'esprit de *Belmont's Revenge* / *Belmont's Curse* (gothique énergique, mode mineur, clavecin et orgue façon 16 bits, basse qui avance). Boucle d'environ une minute, sans couture, mixée pour ne pas fatiguer. **Aucune mélodie, aucun motif ni aucun sample de Konami** : la référence donne l'ambiance, pas la musique.

**Contraintes :**
- Le menu est le programme `clover-ui` de Nintendo (`/usr/share/ui/snes-eur/...`). On remplace uniquement des ressources (images, police, sons), pas la disposition ni les animations. **Le rendu est une adaptation fidèle de la maquette, pas une copie au pixel près.** Si une partie de la maquette est impossible (par exemple l'interrupteur sous la jaquette), le builder choisit l'équivalent le plus proche et le signale.
- Livré comme **hmod** installable et désinstallable depuis hakchi, sans toucher aux fichiers d'origine en dehors du mécanisme de surcouche de hakchi.
- Console **EUR** : l'interface utilise `snes-eur`. Les chaînes de 9 langues sont déjà montées en tmpfs par hakchi : ne pas casser ce mécanisme.

### 5 bis. RetroArch aussi en B1 (décision utilisateur)

Plus de 200 jeux tournent dans RetroArch 1.8.4 Xtreme (menu Ozone). Son menu est habillé dans le même langage B1 : ardoise, violet manette, lavande, typo géométrique, sans dégradé ni halo.
- **Contrainte :** la marge de personnalisation des couleurs d'Ozone en 1.8.4 est limitée. Le builder vérifie ce que la version installée permet (couleurs, police, icônes, fond) avec Ozone, XMB ou RGUI (presets de thème), choisit le pilote de menu qui rend B1 le plus fidèlement, et **signale ce choix et les écarts** avant de l'appliquer.
- Les réglages sont appliqués via un hmod ou des fichiers de config versionnés, avec une sauvegarde de l'existant (`/etc/libretro/retroarch.cfg`, `config_save_on_exit` étant à `true`). Ne pas casser les overrides PS1/3DO déjà en place (§2).

## 6. Projet 3 : interface PC « A »

Maquette validée : `.superpowers/brainstorm/9314-1794315727/content/hakchi-pc.html`, option **A · Bibliothèque en jaquettes**.

- Même langage visuel que la TV (§5) : thème sombre ardoise, violet et lavande, typo géométrique.
- **Barre du haut** : Jeux · Mods · Pochettes · Outils, avec à droite l'état de la console (pastille, modèle, version hakchi).
- **Colonne gauche** : systèmes avec leur nombre de jeux, puis les collections « Sans jaquette » et « Favoris ».
- **Centre** : grille de jaquettes. Les jeux sans jaquette sont **hachurés** et lisibles d'un coup d'œil. Filtres « Sur la console / Dans la bibliothèque / A–Z ».
- **Colonne droite** : fiche du jeu (grande jaquette, titre, système, année, joueurs, éditeur, genre, core, **source de la pochette** avec « changer »).
- **Barre du bas** : espace occupé sur la console, « Chercher N jaquettes » (projet 1), **Synchroniser**.
- Toutes les fonctions actuelles de hakchi restent accessibles (flash, mods, sauvegardes, Game Genie, réglages, dumper, etc.). On peut les réorganiser, mais pas les supprimer.
- **Décision utilisateur : refonte complète (option B).** **Toutes les fenêtres** sont entièrement redessinées dans le langage B1, pas seulement la fenêtre principale : ModHub, réglages, Game Genie, gestionnaire de dossiers, sauvegardes, dumper, import, recherche d'images, dialogues de flash/attente, à propos, etc. L'interface est presque entièrement réécrite. **Le moteur, lui, ne l'est pas** (§7). Les écrans liés au flash ou à l'installation gardent exactement le même comportement et les mêmes confirmations : seule leur apparence change.

**Décisions techniques (choix de Claude, pas validés par l'utilisateur) :**
- On reste sur **.NET Framework 4.8 / WinForms** : les bibliothèques USB et FEL (LibUsbDotNet, libwdi, FelLib) et tout l'existant en dépendent. L'interface est refaite par contrôles dessinés sur mesure, pas par une migration de framework.
- Le moteur (flash, synchro, mods) n'est pas réécrit. Seule l'interface est remplacée.

## 7. Optimisation (règle décidée par Claude, présentée à l'utilisateur)

- **On améliore le code qu'on touche** (pochettes, interface, synchronisation), avec des **mesures avant et après** : démarrage de l'application, synchronisation complète, chargement de la grille de ~257 jaquettes.
- **On ne touche pas, sauf bug précis**, au chemin qui peut rendre la console inutilisable : flash du noyau, mode FEL, installation et désinstallation de hakchi sur la console.
- On corrige au passage les bugs ouverts du dépôt GitHub qui concernent l'usage de l'utilisateur.

## 8. Contraintes générales

- **Machine Windows 10** ; VS 2019/2022 installés ; **les 9 sous-modules git sont vides** (`git submodule update --init --recursive` à faire). Le build attend un dossier `GeneratedData` (basehmods, pilote) produit par le projet `Installer`/`NSI` (voir `.github/workflows/dotnet-desktop.yml`).
- Disque **J: souvent presque plein** : sorties lourdes vers `P:` (620 Go libres) ou `N:`.
- **La console est l'unique exemplaire de l'utilisateur** : toute action qui écrit sur la console passe par une sauvegarde préalable et reste réversible. Aucune opération de flash pour tester.
- **Ne jamais tuer de processus par nom d'image** (par exemple `python.exe`), toujours par PID.
- Les ROMs et BIOS de l'utilisateur ne quittent pas sa machine : ni commit, ni envoi à un service, sauf l'empreinte demandée par ScreenScraper.
- Dépôt de l'utilisateur : le fork `https://github.com/Fantaistic5/Hakchi2-CE` (remote `origin`). `TeamShinkansen` est le remote `upstream`. Les commits se font sur des branches de travail. On ne pousse vers `origin` que sur demande, et jamais vers `upstream`.

## 8 bis. Précisions après relecture

- **Bibliothèque vs clé (vérifié) :** les 257 entrées de la clé se répartissent en 50 dossiers (`chmenu`) et 207 jeux. **Les 207 jeux sont tous connus de la bibliothèque hakchi** (195 dans `games`, le reste dans `games_originals`). La grille PC s'appuie donc sur la bibliothèque hakchi, et la synchronisation ne risque pas d'effacer des jeux « inconnus ».
- **Coexistence avec le 3.8.0 (choix de Claude) :** le nouveau hakchi vit dans **son propre dossier** et travaille sur une **copie** de la bibliothèque de `R:\hakchi2-ce-3.8.0-release`. Le 3.8.0 n'est pas modifié et reste utilisable pour revenir en arrière. Le nouveau champ « source de la pochette » est stocké de façon à ne pas gêner la lecture des métadonnées par le 3.8.0.
- **Mise à jour de hakchi sur la console (choix de Claude) :** le nouveau build **ne met jamais à jour la console automatiquement**. Si les basehmods récents sont nécessaires, la mise à jour devient une étape explicite, proposée à l'utilisateur, précédée d'une sauvegarde, avec un retour arrière documenté. Tant qu'elle n'a pas été acceptée, la synchronisation doit marcher avec la console en `v1.0.4-126`, ou expliquer clairement pourquoi elle ne le peut pas.
- **Langues (choix de Claude) :** on conserve le mécanisme resx existant. Les nouveaux textes sont fournis en **français et en anglais**, et les 7 autres langues retombent sur l'anglais pour ces nouveaux textes.
- **Musique (choix de Claude) :** Claude la compose et la produit (rendu tracker ou MIDI vers échantillons 16 bits). **L'utilisateur l'écoute et la valide** avant qu'elle n'entre dans le hmod.

## 9. Laissé au builder

- Architecture interne des nouvelles sources de pochettes et format du cache.
- Choix de la police exacte (libre de droits, style Futura) et sa conversion au format de police de `clover-ui`.
- Outils de composition de la musique et format final attendu par la console.
- Technique de dessin des contrôles WinForms sur mesure.
- Découpage en étapes et tests.
