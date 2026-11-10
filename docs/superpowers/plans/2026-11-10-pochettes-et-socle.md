# Pochettes + socle de build — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Compiler hakchi depuis ce dépôt, le faire tourner sur une copie de la bibliothèque de l'utilisateur, et ajouter une recherche de jaquettes groupée (libretro-thumbnails → TheGamesDB → Google) qui trouve la jaquette exacte par CRC / nom de ROM et retombe sur le titre.

**Architecture:** Un nouveau module `Covers/` dans la bibliothèque `Scrapers` (netstandard2.0, testable hors WinForms) définit `ICoverSource`, les index DAT libretro, la source libretro-thumbnails, un adaptateur TheGamesDB et un orchestrateur `CoverSearchService`. `hakchi_gui` fournit l'adaptateur Google (moteur `ImageGoogler` existant), l'enregistrement de la source dans `metadata.json` et une fenêtre de recherche groupée fonctionnelle (elle sera redessinée au projet 3).

**Tech Stack:** C# ; `Scrapers` netstandard2.0 + Newtonsoft.Json 13.0.3 ; `hakchi_gui` net48 WinForms ; tests xUnit sur net8.0 ; build complet par MSBuild VS 2022 (comme la CI).

**Spec:** `docs/superpowers/specs/2026-11-10-hakchi-snes-usa-refonte-design.md` (§3, §4, §7, §8, §8 bis)

## Global Constraints

- Ordre des sources : **libretro-thumbnails → TheGamesDB → Google**. ScreenScraper n'est pas implémenté, mais doit pouvoir s'ajouter comme un `ICoverSource` de plus sans toucher au reste.
- Préférence de région : celle du jeu, puis `Europe`, puis `USA`.
- **Ne jamais écraser une jaquette dont `Metadata.CustomCoverArt == true`** sans que l'utilisateur ait coché ce jeu explicitement.
- TheGamesDB : clé lue dans `hakchi_gui/ApiKeys/TheGamesDB.txt` (déjà présente, ignorée par git). Pas de clé = source désactivée, pas d'erreur. **Ne jamais afficher, journaliser ni committer la clé.**
- Le nouveau hakchi tourne dans **son propre dossier** sur une **copie** de `R:\hakchi2-ce-3.8.0-release`. `R:` n'est jamais modifié.
- **Aucune écriture sur la console** dans ce plan (pas de synchro, pas de flash, pas de mise à jour hakchi). La console reste en `v1.0.4-126`.
- Nouveau champ `metadata.json` : `"CoverSource"` (string, optionnel). Le 3.8.0 doit toujours pouvoir lire le fichier : champ additionnel uniquement, aucun champ renommé.
- Nouveaux textes d'interface : FR + EN via resx. Les autres langues retombent sur l'anglais.
- Gros fichiers et sorties de build hors de `J:` quand c'est possible (J: est souvent presque plein) : dossier de travail `P:\HakchiSNESUSA\`.
- Ne jamais tuer un processus par nom d'image. Toujours par PID.
- Commits sur la branche `snes-usa/pochettes`. Remotes : `origin` = fork de l'utilisateur `https://github.com/Fantaistic5/Hakchi2-CE`, `upstream` = `TeamShinkansen`. **Push uniquement vers `origin`, et seulement si l'utilisateur le demande. Jamais vers `upstream`.**

## Review Focus

1. **Jeu d'arcade au nom de ROM cryptique** (`kof98.zip`, système `FBA - Arcade Games` ou `CP System II`) → trouvé via l'index DAT FBNeo, jamais via une recherche floue hasardeuse. → test `ArcadeRomNameResolvesViaFbneoDat` (Task 3).
2. **Correspondance par titre seulement** (pas de CRC connu, par exemple les jeux CD PBP/CHD/ISO) → proposée, mais **décochée par défaut** dans la fenêtre groupée. → test `TitleOnlyMatchIsNotAutoAccepted` (Task 5).
3. **Réseau coupé, 404 ou 429** au milieu d'une recherche groupée → le jeu est marqué « non trouvé », les autres continuent, aucune exception jusqu'à l'interface. → test `FailingSourceDoesNotStopBatch` (Task 5).
4. **Caractères spéciaux dans les titres** (`'`, `&`, `:`, `/`) → nom de fichier libretro correctement assaini et URL encodée. → test `SanitizesLibretroForbiddenChars` (Task 2).
5. **Jaquette perso existante** (`CustomCoverArt=true`, 178 jeux sur 195) → exclue de la sélection par défaut, jamais remplacée sans case cochée. → test `CustomCoversExcludedByDefault` (Task 6).

---

### Task 0: Socle — build local reproductible + copie de la bibliothèque + mesures de référence

**Files:**
- Create: `docs/superpowers/notes/build-local.md` (commandes exactes qui ont marché, pièges rencontrés)
- Create: `docs/superpowers/notes/baseline-perf.md`

**Interfaces:**
- Produces: un `hakchi.exe` lançable dans `P:\HakchiSNESUSA\app\`, avec la bibliothèque copiée ; le SDK .NET 8 dans `N:\dotnet` pour les tests.

- [ ] **Step 1: Branche** — `git switch -c snes-usa/pochettes`. Ajouter `.superpowers/` et `New Text Document.txt` à `.git/info/exclude` (le second contient la clé TheGamesDB en clair : ne jamais l'indexer).
- [ ] **Step 2: Sous-modules** — `git submodule update --init --recursive`. Attendu : les 9 dossiers (`Libraries/*`, `driver/source`, `libwdi`) ne sont plus vides.
- [ ] **Step 3: SDK de test** — installer le SDK .NET 8 en portable : `powershell -c "& ([scriptblock]::Create((iwr https://dot.net/v1/dotnet-install.ps1))) -Channel 8.0 -InstallDir N:\dotnet"`. Attendu : `N:\dotnet\dotnet.exe --list-sdks` liste un 8.0.x. (Aucun SDK n'est installé aujourd'hui, seulement le runtime 8.0.10.)
- [ ] **Step 4: Build complet comme la CI** — dans un « Developer PowerShell for VS 2022 » : `nuget restore hakchi_gui.sln` puis `msbuild Installer/Installer.vcxproj /p:SolutionDir=<repo>\ /p:SolutionPath=<repo>\hakchi_gui.sln "/p:Platform=Any CPU"` (voir `.github/workflows/dotnet-desktop.yml`, qui nécessite aussi NSIS 3.10). Attendu : `hakchi_gui\bin\Debug\net48\hakchi.exe` existe, avec `basehmods.tar` et `driver\` à côté. Noter chaque étape réellement nécessaire dans `build-local.md`.
- [ ] **Step 5: Copie de travail** — copier `hakchi_gui\bin\Debug\net48\*` vers `P:\HakchiSNESUSA\app\`. Copier depuis `R:\hakchi2-ce-3.8.0-release\` les dossiers `games`, `games_originals`, `art`, `config`, `folder_images`, `user_mods` vers `P:\HakchiSNESUSA\app\`. Ne rien écrire sur `R:`.
- [ ] **Step 6: Premier lancement hors ligne** — console **débranchée**, lancer `P:\HakchiSNESUSA\app\hakchi.exe`. Attendu : la liste montre les 195 jeux de la bibliothèque et les jaquettes existantes. Si l'application propose une mise à jour, une migration ou un flash : **refuser** et noter le message dans `build-local.md`.
- [ ] **Step 7: Mesures de référence** — mesurer 3 fois (médiane) : le temps jusqu'à la fenêtre principale prête, et le temps de chargement de la liste de jeux. Consigner les résultats dans `baseline-perf.md`, avec la machine et la date.
- [ ] **Step 8: Commit** — `git add docs/superpowers/notes && git commit -m "docs: build local reproductible et mesures de reference"`

### Task 1: Projet de tests `Scrapers.Tests`

**Files:**
- Create: `Scrapers.Tests/Scrapers.Tests.csproj` (xUnit, `net8.0`, `ProjectReference ..\Scrapers\Scrapers.csproj`)
- Create: `Scrapers.Tests/Fixtures/` (petits extraits de DAT copiés à la main, pas de DAT complets)
- Modify: `hakchi_gui.sln` (ajouter le projet)

- [ ] **Step 1:** créer le projet avec un test trivial `Smoke_ReferencesScrapers` qui instancie `TeamShinkansen.Scrapers.Enums.ArtType.Front`.
- [ ] **Step 2:** `N:\dotnet\dotnet.exe test Scrapers.Tests` → attendu : `Passed: 1`.
- [ ] **Step 3:** vérifier que le build MSBuild de la Task 0 (Step 4) passe toujours, avec le nouveau projet dans la solution.
- [ ] **Step 4: Commit** `test: projet Scrapers.Tests`.

### Task 2: Contrats `Covers` + noms libretro

**Files:**
- Create: `Scrapers/Covers/GameQuery.cs`, `CoverCandidate.cs`, `MatchKind.cs`, `ICoverSource.cs`, `LibretroNames.cs`
- Test: `Scrapers.Tests/LibretroNamesTests.cs`

**Interfaces (Produces):**
```csharp
public enum MatchKind { Crc = 0, RomName = 1, Title = 2 }          // ordre = confiance décroissante
public sealed class GameQuery {
    public string System;        // valeur brute de AppMetadata.System (ex. "Nintendo - Super Nintendo Entertainment System", "FBA - Arcade Games", "CP System II", "")
    public string Core;          // AppMetadata.Core (ex. "km_fbneo"), sert quand System est vide
    public uint Crc32;           // AppMetadata.OriginalCrc32 (0 = inconnu)
    public string RomFileName;   // AppMetadata.OriginalFilename
    public string Title;         // NesApplication.Name
    public string PreferredRegion; // "Europe" par défaut
}
public sealed class CoverCandidate {
    public string Url; public string SourceName; public string Region; public MatchKind Match; public string MatchedName;
}
public interface ICoverSource {
    string Name { get; }
    bool IsAvailable { get; }    // ex. TheGamesDB sans clé => false
    Task<IReadOnlyList<CoverCandidate>> FindAsync(GameQuery q, CancellationToken ct);
}
public static class LibretroNames {
    public static string Sanitize(string gameName);                 // & * / : ` < > ? \ | " -> _
    public static string ThumbnailSystem(string system, string core); // voir table ci-dessous ; null = non pris en charge
    public static string BoxartUrl(string thumbnailSystem, string gameName); // https://thumbnails.libretro.com/{esc(system)}/Named_Boxarts/{esc(Sanitize(name))}.png
    public static string NormalizeTitle(string title);               // minuscules, sans balises (...) [...], sans ponctuation, espaces réduits
}
```
Table `ThumbnailSystem` : identité pour les noms déjà au format libretro (Nintendo/Sega/Sony/NEC/3DO, tels que dans la bibliothèque). `FBA - Arcade Games`, `FB Alpha - Arcade Games`, `CP System I`, `CP System II`, `CP System III`, et `""` avec un core `km_fbalpha*`/`km_fbneo` → `FBNeo - Arcade Games`.

- [ ] **Step 1: tests** —
  - `SanitizesLibretroForbiddenChars` : `Sanitize("Tom & Jerry: Frantic/Antics?") == "Tom _ Jerry_ Frantic_Antics_"`.
  - `BoxartUrlEscapesSpaces` : `BoxartUrl("Nintendo - Super Nintendo Entertainment System","Super Metroid (Japan, USA) (En,Ja)")` commence par `https://thumbnails.libretro.com/Nintendo%20-%20Super%20Nintendo%20Entertainment%20System/Named_Boxarts/Super%20Metroid%20` et finit par `.png`.
  - `ArcadeSystemsMapToFbneo` : `ThumbnailSystem("CP System II","km_fbalpha2012_cps2") == "FBNeo - Arcade Games"` et `ThumbnailSystem("","km_fbalpha2012") == "FBNeo - Arcade Games"`.
  - `NormalizeTitleStripsTags` : `NormalizeTitle("The King of Fighters '98 (Europe) [b]") == "the king of fighters 98"`.
- [ ] **Step 2:** `dotnet test` → FAIL (types absents).
- [ ] **Step 3:** implémenter les fichiers ci-dessus.
- [ ] **Step 4:** `dotnet test` → PASS.
- [ ] **Step 5: Commit** `feat(covers): contrats et noms libretro`.

### Task 3: Index DAT libretro (CRC → nom, ROM arcade → nom)

**Files:**
- Create: `Scrapers/Covers/LibretroDatIndex.cs`
- Test: `Scrapers.Tests/LibretroDatIndexTests.cs` + `Fixtures/snes-sample.dat`, `Fixtures/fbneo-sample.dat`

**Interfaces (Produces):**
```csharp
public sealed class LibretroDatIndex {
    public static LibretroDatIndex Parse(TextReader clrmameproDat);
    public string FindByCrc(uint crc32);          // null si absent
    public string FindByRomSet(string romFileName); // "kof98.zip" ou "kof98" -> nom du jeu ; null si absent
    public IReadOnlyCollection<string> Names { get; }
}
```
Format : DAT clrmamepro tel que publié dans `libretro/libretro-database`. **Vérifier les chemins exacts sur GitHub avant de coder** : les DAT No-Intro par système sont dans `metadat/no-intro/<Système>.dat`, et l'arcade se trouve sous un DAT FBNeo (`dat/FBNeo - Arcade Games.dat` ou `metadat/fbneo-split/...`). Les fixtures sont des extraits de 2 ou 3 entrées copiés de ces vrais fichiers.

- [ ] **Step 1: tests** —
  - `FindsGameByCrc` : avec l'extrait SNES contenant `Super Metroid`, `FindByCrc(<crc de l'extrait>)` renvoie le `name` exact de l'entrée.
  - `ArcadeRomNameResolvesViaFbneoDat` : `FindByRomSet("kof98.zip")` et `FindByRomSet("KOF98")` renvoient le titre complet de l'entrée kof98 de l'extrait.
  - `UnknownCrcReturnsNull`.
  - `ParsesQuotedNamesWithParentheses` (entrée dont le nom contient `"` échappé ou des parenthèses).
- [ ] **Step 2:** FAIL. **Step 3:** implémenter un parseur ligne à ligne (blocs `game (` … `)` ; `name`, `description`, `rom ( name … crc … )`). **Step 4:** PASS.
- [ ] **Step 5: Commit** `feat(covers): index DAT libretro`.

### Task 4: Source `LibretroThumbnailsSource` + cache disque

**Files:**
- Create: `Scrapers/Covers/HttpCache.cs`, `Scrapers/Covers/LibretroThumbnailsSource.cs`
- Test: `Scrapers.Tests/LibretroThumbnailsSourceTests.cs` (HTTP simulé par un `HttpMessageHandler` de test, aucun réseau)

**Interfaces:**
- Consumes: `LibretroNames`, `LibretroDatIndex`, `ICoverSource` (Tasks 2-3).
- Produces:
```csharp
public sealed class HttpCache {
    public HttpCache(string cacheDir, HttpMessageHandler handler = null, TimeSpan? maxAge = null); // maxAge par défaut 30 jours
    public Task<string> GetTextAsync(string url, CancellationToken ct);   // null si 404
    public Task<bool> ExistsAsync(string url, CancellationToken ct);      // HEAD, résultat mis en cache
}
public sealed class LibretroThumbnailsSource : ICoverSource {
    public LibretroThumbnailsSource(HttpCache http, Func<string, string> datUrlForSystem);
    // Name => "libretro-thumbnails", IsAvailable => true
}
```
Résolution, dans l'ordre : (1) `Crc32 != 0` → DAT système → nom exact → `MatchKind.Crc` ; (2) système arcade → `FindByRomSet(RomFileName)` → `MatchKind.RomName` ; (3) sinon recherche par titre dans `Names` du DAT (égalité de `NormalizeTitle`, puis préférence de région `PreferredRegion` → `Europe` → `USA`) → `MatchKind.Title`. Un candidat n'est renvoyé que si `ExistsAsync(BoxartUrl)` est vrai. `Region` = premier tag de région du nom libretro. Cache : `P:\HakchiSNESUSA\app\cache\covers\` (au runtime : `Program.BaseDirectoryExternal\cache\covers`), au plus 4 requêtes simultanées par hôte.

- [ ] **Step 1: tests** — `CrcMatchReturnsExactBoxart` (DAT fixture + HEAD 200 → 1 candidat `MatchKind.Crc`, URL attendue) ; `ArcadeUsesRomSetName` ; `TitleFallbackPrefersEurope` (deux noms Europe/USA dans le DAT, HEAD 200 sur les deux → le premier candidat a `Region == "Europe"`) ; `MissingBoxartYieldsNoCandidate` (HEAD 404) ; `DatIsCachedOnDisk` (deuxième appel : 0 requête réseau).
- [ ] **Step 2:** FAIL. **Step 3:** implémenter. **Step 4:** PASS.
- [ ] **Step 5: Commit** `feat(covers): source libretro-thumbnails avec cache`.

### Task 5: `CoverSearchService` + adaptateur TheGamesDB

**Files:**
- Create: `Scrapers/Covers/CoverSearchService.cs`, `Scrapers/Covers/TheGamesDbCoverSource.cs`
- Test: `Scrapers.Tests/CoverSearchServiceTests.cs`

**Interfaces:**
- Consumes: `ICoverSource`, `GameQuery`, `CoverCandidate`, `MatchKind`.
- Produces:
```csharp
public sealed class CoverProposal {
    public GameQuery Query; public CoverCandidate Best; public IReadOnlyList<CoverCandidate> Alternatives;
    public bool AutoAccept;   // true seulement si Best.Match est Crc ou RomName
    public string Error;      // non null si toutes les sources ont échoué
}
public sealed class CoverSearchService {
    public CoverSearchService(IEnumerable<ICoverSource> orderedSources);
    public Task<CoverProposal> SearchAsync(GameQuery q, CancellationToken ct);
    public Task<IReadOnlyList<CoverProposal>> SearchManyAsync(IEnumerable<GameQuery> qs, IProgress<int> progress, CancellationToken ct);
}
public sealed class TheGamesDbCoverSource : ICoverSource {   // enveloppe TeamShinkansen.Scrapers.TheGamesDB.Scraper
    public TheGamesDbCoverSource(string apiKey);             // IsAvailable = !string.IsNullOrWhiteSpace(apiKey)
}
```
Règle : on parcourt les sources dans l'ordre, en ignorant celles dont `IsAvailable == false`. On s'arrête à la première source qui donne un candidat `Crc`/`RomName`. Sinon, on garde le meilleur candidat `Title` toutes sources confondues, en préférant la première source. Une exception d'une source est attrapée et consignée dans `Error` si plus rien n'est trouvé. TheGamesDB renvoie des candidats `Title` (recherche par nom + plateforme), avec les images `ArtType.Front`.

- [ ] **Step 1: tests (sources factices)** — `StopsAtFirstExactMatch` (source 1 → Crc ; source 2 jamais appelée) ; `TitleOnlyMatchIsNotAutoAccepted` ; `UnavailableSourceIsSkipped` ; `FailingSourceDoesNotStopBatch` (une source lève `HttpRequestException` pour le jeu A → `Error` non nul pour A, B trouvé normalement) ; `TheGamesDbWithoutKeyIsUnavailable`.
- [ ] **Step 2:** FAIL. **Step 3:** implémenter. **Step 4:** PASS.
- [ ] **Step 5: Commit** `feat(covers): orchestrateur et source TheGamesDB`.

### Task 6: Intégration hakchi — Google, source mémorisée, sélection des jeux

**Files:**
- Create: `hakchi_gui/Covers/GoogleCoverSource.cs` (enveloppe `ImageGoogler.GetImageUrls(query)` avec la même requête que `ImageGooglerForm.GetImageUrls` ; `MatchKind.Title`)
- Create: `hakchi_gui/Covers/CoverTargets.cs`
- Modify: `hakchi_gui/Apps/NesApplication.cs:88-95` (ajouter `public string CoverSource = null;` dans `AppMetadata`) et ajouter `public void ApplyCover(Image img, string source)` (appelle `SetImage`, puis renseigne `CoverSource` et `SaveMetadata()`)
- Test: tests manuels décrits ci-dessous (projet WinForms net48, sans harnais de test) + `Scrapers.Tests` pour toute logique déplaçable

**Interfaces (Produces):**
```csharp
static class CoverTargets {
    public static GameQuery ToQuery(NesApplication app);                       // mapping AppMetadata -> GameQuery
    public static bool NeedsCover(NesApplication app);                         // !CustomCoverArt || image == DefaultCover du système
    public static IEnumerable<NesApplication> DefaultSelection(IEnumerable<NesApplication> apps); // = NeedsCover
}
```
- [ ] **Step 1:** implémenter `CoverSource`, `ApplyCover`, `GoogleCoverSource`, `CoverTargets`.
- [ ] **Step 2: vérif compat 3.8.0** — sur la copie `P:\HakchiSNESUSA`, appliquer une jaquette à 1 jeu, puis ouvrir ce même `metadata.json` avec `R:\hakchi2-ce-3.8.0-release\hakchi.exe` **pointé sur une 2ᵉ copie jetable** (jamais le vrai `R:`) : le jeu se charge sans erreur.
- [ ] **Step 3: vérif `CustomCoversExcludedByDefault`** — sur la copie de la bibliothèque, `DefaultSelection` renvoie exactement les 17 jeux avec `CustomCoverArt=false` (nombre mesuré le 2026-11-10 ; s'il diffère, recompter et noter l'écart).
- [ ] **Step 4: Commit** `feat(covers): source Google, CoverSource dans metadata, selection`.

### Task 7: Fenêtre « Chercher les jaquettes » (fonctionnelle, redessinée au projet 3)

**Files:**
- Create: `hakchi_gui/CoverBatchForm.cs` + `.Designer.cs` + `.resx` + `.fr-FR.resx`
- Modify: `hakchi_gui/MainForm.cs` (entrée de menu « Pochettes › Chercher les jaquettes manquantes… » / « Covers › Find missing covers… »), `MainForm.resx` + `MainForm.fr-FR.resx`

Comportement : la fenêtre liste les jeux de `DefaultSelection`, avec une case « Inclure aussi les jeux qui ont déjà une jaquette » qui affiche les autres, décochés. Le bouton **Chercher** appelle `SearchManyAsync`, avec une barre de progression et un bouton Annuler. Pour chaque jeu, la fenêtre affiche la vignette proposée, la source et la région (« libretro-thumbnails · Europe »), et le type de correspondance. Elle est cochée si `AutoAccept`, décochée sinon. Un clic ouvre les `Alternatives`. Le bouton **Appliquer** télécharge et applique uniquement les jeux cochés, via `ApplyCover`. Un résumé final indique « N appliquées · M à vérifier · K non trouvées ». Les sources sont construites dans l'ordre `LibretroThumbnailsSource`, `TheGamesDbCoverSource(Resources.TheGamesDBKey)`, `GoogleCoverSource`.

- [ ] **Step 1:** implémenter la fenêtre et l'entrée de menu (FR + EN).
- [ ] **Step 2: essai réel sur la copie** — lancer la recherche sur les 17 jeux. Consigner dans `docs/superpowers/notes/covers-run-1.md` les nombres trouvés par source et par type de correspondance, les non-trouvés, et 5 exemples vérifiés à l'œil (dont au moins 1 arcade et 1 PS1).
- [ ] **Step 3: annulation et hors ligne** — couper le réseau pendant une recherche : la fenêtre reste utilisable, les jeux restants passent en « non trouvé », aucune boîte d'exception.
- [ ] **Step 4: perf** — comparer le temps de démarrage avec `baseline-perf.md` : pas plus de +5 %. Sinon, retarder la création des sources jusqu'à l'ouverture de la fenêtre.
- [ ] **Step 5: Commit** `feat(covers): recherche groupee des jaquettes`.

---

## Hors de ce plan (plans suivants)

- Projet 2 : thème TV B1, RetroArch, musique (plan dédié, après validation de celui-ci).
- Projet 3 : refonte complète de l'interface PC (la `CoverBatchForm` y sera redessinée).
- ScreenScraper : nouvel `ICoverSource` quand l'utilisateur aura les identifiants.
