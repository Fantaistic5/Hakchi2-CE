# Essai reel des sources de jaquettes (covers-run-1)

- Date : 2026-11-10 15:13
- Genere par `Scrapers.Tests/LiveCoverRunTests.cs` (`HAKCHI_LIVE=1`, categorie `Live`), sans aucune ecriture dans les dossiers de jeux.
- Jeux : 37 (17 sans CustomCoverArt + 20 echantillon parmi ceux qui en ont une).
- Sources essayees : libretro-thumbnails, TheGamesDB (cle presente). **Google non essayee** : `GoogleCoverSource` vit dans hakchi_gui (net48, depend de ImageGoogler), hors de ce test.

## Resultats de l'orchestrateur (libretro puis TheGamesDB)

- Les 17 jeux sans jaquette : 17 jeux : 14 trouves (libretro-thumbnails 12, thegamesdb 2) ; type de correspondance : Title 3, Crc 11 ; cochees d'office : 11 ; non trouves : 3
- Echantillon de jeux deja illustres : 20 jeux : 20 trouves (libretro-thumbnails 18, thegamesdb 2) ; type de correspondance : Crc 4, Title 9, RomName 7 ; cochees d'office : 11 ; non trouves : 0

## Par source (chaque source interrogee seule, sur les 37 jeux)

- libretro-thumbnails : 30/37 jeux avec au moins un candidat ; meilleur candidat par type : Title 8, Crc 15, RomName 7
- TheGamesDB : 30/37 jeux avec au moins un candidat ; meilleur candidat par type : Title 30

Par systeme (libretro / TGDB, jeux avec candidat sur total) :

| Systeme | Jeux | libretro | TGDB |
|---|---|---|---|
| () | 1 | 0 | 0 |
| CP System II | 1 | 1 | 1 |
| FB Alpha - Arcade Games | 3 | 3 | 3 |
| FBA - Arcade Games | 16 | 15 | 12 |
| NEC - PC Engine CD - TurboGrafx-CD | 1 | 1 | 1 |
| Nintendo - Game Boy | 1 | 1 | 1 |
| Nintendo - Game Boy Advance | 1 | 1 | 1 |
| Nintendo - Nintendo 64 | 2 | 2 | 2 |
| Nintendo - Nintendo Entertainment System | 2 | 1 | 2 |
| Nintendo - Super Nintendo Entertainment System | 5 | 3 | 5 |
| Sega - Mega Drive - Genesis | 3 | 1 | 1 |
| Sony - PlayStation | 1 | 1 | 1 |

## Non trouves par l'orchestrateur

- CLV-G-EEHZM « Castlevania - Symphony of the Night (PSX1-audio) » (Sega - Mega Drive - Genesis, rom `Castlevania - Symphony of the Night (Track 2).bin`)
- CLV-G-OVDCN « Castlevania - Symphony of the Night (PSX1) » (Sega - Mega Drive - Genesis, rom `Castlevania - Symphony of the Night (Track 1).bin`)
- CLV-I-UUKKY « readme » ((), rom `readme.html`)

## Exemples a verifier a l'oeil (URL des jaquettes proposees)

### kof2003 (FBA - Arcade Games)

- Meilleur : libretro-thumbnails · sans region · Crc · nom trouve « The King of Fighters 2003 (NGM-2710, Export) »
- URL : https://thumbnails.libretro.com/FBNeo%20-%20Arcade%20Games/Named_Boxarts/The%20King%20of%20Fighters%202003%20%28NGM-2710%2C%20Export%29.png
- Cochee d'office : oui ; alternatives : 0

### Heart of Darkness (Sony - PlayStation)

- Meilleur : libretro-thumbnails · Europe · Title · nom trouve « Heart of Darkness (Europe) (Disc 1) »
- URL : https://thumbnails.libretro.com/Sony%20-%20PlayStation/Named_Boxarts/Heart%20of%20Darkness%20%28Europe%29%20%28Disc%201%29.png
- Cochee d'office : non ; alternatives : 17

### ChronoTrigger - Crimson Echoes (Nintendo - Super Nintendo Entertainment System)

- Meilleur : thegamesdb · sans region · Title · nom trouve « Chrono Trigger: Crimson Echoes »
- URL : https://cdn.thegamesdb.net/images/original/boxart/front/121042-1.jpg
- Cochee d'office : non ; alternatives : 0

### Alex Kidd in the Enchanted Castle (Sega - Mega Drive - Genesis)

- Meilleur : libretro-thumbnails · Europe · Title · nom trouve « Alex Kidd in the Enchanted Castle (Europe) »
- URL : https://thumbnails.libretro.com/Sega%20-%20Mega%20Drive%20-%20Genesis/Named_Boxarts/Alex%20Kidd%20in%20the%20Enchanted%20Castle%20%28Europe%29.png
- Cochee d'office : non ; alternatives : 3

### Yoshi's Story (Nintendo - Nintendo 64)

- Meilleur : libretro-thumbnails · USA · Crc · nom trouve « Yoshi's Story (USA) (En,Ja) »
- URL : https://thumbnails.libretro.com/Nintendo%20-%20Nintendo%2064/Named_Boxarts/Yoshi%27s%20Story%20%28USA%29%20%28En%2CJa%29.png
- Cochee d'office : oui ; alternatives : 0

### Akumajou Dracula X - Chi no Rondo (NEC - PC Engine CD - TurboGrafx-CD)

- Meilleur : libretro-thumbnails · Japan · Title · nom trouve « Akumajou Dracula X - Chi no Rondo (Japan) »
- URL : https://thumbnails.libretro.com/NEC%20-%20PC%20Engine%20CD%20-%20TurboGrafx-CD/Named_Boxarts/Akumajou%20Dracula%20X%20-%20Chi%20no%20Rondo%20%28Japan%29.png
- Cochee d'office : non ; alternatives : 2

### wh2j (FBA - Arcade Games)

- Meilleur : libretro-thumbnails · sans region · Crc · nom trouve « World Heroes 2 Jet (ADM-007 ~ ADH-007) »
- URL : https://thumbnails.libretro.com/FBNeo%20-%20Arcade%20Games/Named_Boxarts/World%20Heroes%202%20Jet%20%28ADM-007%20~%20ADH-007%29.png
- Cochee d'office : oui ; alternatives : 0

## Verification des valeurs `Platform` de TheGamesDB

| Systeme hakchi | Plateformes acceptees (carte) | Requete | Filtre |
|---|---|---|---|
| NEC - PC Engine CD - TurboGrafx-CD | TurboGrafx CD ; PC Engine CD | Akumajou Dracula X - Chi no Rondo -> 2 resultats, plateformes vues : TurboGrafx CD | 2 gardes |
| Sega - Mega Drive - Genesis | Sega Genesis ; Sega Mega Drive | Castlevania - Symphony of the Night (PSX1-audio) -> 0 resultats, plateformes vues :  | 0 gardes |
| () | (aucune plateforme TGDB mappee) | | |
| Nintendo - Super Nintendo Entertainment System | Super Nintendo (SNES) ; Super Famicom | ChronoTrigger - Crimson Echoes -> 1 resultats, plateformes vues : Super Nintendo (SNES) | 1 gardes |
| FBA - Arcade Games | Arcade | kof2003 -> 20 resultats, plateformes vues : Neo Geo ; Sony Playstation ; Sega Genesis ; PC ; Amiga ; Arcade ; Nintendo Entertainment System (NES) ; Amstrad CPC ; Commodore 64 ; Sinclair ZX Spectrum ; Atari ST | 3 gardes |
| Nintendo - Nintendo 64 | Nintendo 64 | Yoshi's Story -> 6 resultats, plateformes vues : Nintendo 64 ; Nintendo Wii ; Super Nintendo (SNES) ; Nintendo Wii U | 3 gardes |
| Nintendo - Game Boy Advance | Nintendo Game Boy Advance | Naruto - Ninja Council 2 -> 5 resultats, plateformes vues : Nintendo Game Boy Advance ; Nintendo DS | 2 gardes |
| Nintendo - Game Boy | Nintendo Game Boy | Wario Land II -> 14 resultats, plateformes vues : Nintendo Game Boy Color ; Nintendo Game Boy ; Nintendo 3DS ; Nintendo Entertainment System (NES) ; Super Nintendo (SNES) ; Nintendo Game Boy Advance ; Nintendo Wii U | 1 gardes |
| Nintendo - Nintendo Entertainment System | Nintendo Entertainment System (NES) ; Famicom | robocop2 -> 20 resultats, plateformes vues : Nintendo Entertainment System (NES) ; Commodore 64 ; Microsoft Xbox ; Arcade ; Amiga ; Nintendo Game Boy ; Nintendo Game Boy Color ; Sinclair ZX Spectrum ; TRS-80 Color Computer ; Sony Playstation 2 ; Nintendo GameCube ; PC ; Amstrad CPC ; MSX ; Atari ST ; Nintendo Game Boy Advance | 2 gardes |
| Sony - PlayStation | Sony Playstation | Heart of Darkness -> 5 resultats, plateformes vues : Sony Playstation ; PC | 4 gardes |
| FB Alpha - Arcade Games | Arcade | TETRIS -> 20 resultats, plateformes vues : Nintendo Entertainment System (NES) ; Commodore 64 ; Arcade ; Amiga ; Nintendo Game Boy ; Sony Playstation 3 ; Sega Genesis ; Philips CD-i ; Sony Playstation Portable ; WonderSwan Color ; Amstrad CPC ; Intellivision ; Sinclair ZX Spectrum ; PC ; TRS-80 Color Computer ; Nintendo 3DS ; Atari ST | 2 gardes |
| CP System II | Arcade | Marvel vs Capcom - clash of super heroes -> 6 resultats, plateformes vues : Sony Playstation ; Sega Dreamcast ; Arcade | 1 gardes |

Aucune divergence : chaque systeme mappe qui a renvoye des resultats en a garde au moins un apres filtrage.
