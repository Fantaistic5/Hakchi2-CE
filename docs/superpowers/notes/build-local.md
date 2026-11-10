# Build local reproductible (Windows 10, VS 2022 Community, sans SDK .NET installe)

Date : 2026-11-10. Resultat : `hakchi_gui\bin\Debug\net48\hakchi.exe` (version `3.9.3-98aef422`), avec `basehmods.tar` et `driver\`.
Le build ne depend ni du SDK .NET systeme ni de NSIS pour produire `hakchi.exe` (voir pieges 1 a 5).

## Prerequis (tous hors de J:, qui est presque plein)

| Outil | Emplacement | Origine |
|---|---|---|
| SDK .NET 8.0.427 portable | `N:\dotnet` | `dotnet-install.ps1 -Channel 8.0 -InstallDir N:\dotnet` |
| nuget.exe 7.9 | `N:\tools\nuget.exe` | https://dist.nuget.org/win-x86-commandline/latest/nuget.exe |
| Reference assemblies .NET 4.8 et 4.8.1 | `N:\tools\fxroot\.NETFramework\v4.8` et `v4.8.1` | paquets NuGet `Microsoft.NETFramework.ReferenceAssemblies.net48` / `.net481` |
| MSBuild 17.14 + csc | VS 2022 Community (`VsDevCmd.bat`) | installe |

Preparation des reference assemblies (une fois) :

```
nuget install Microsoft.NETFramework.ReferenceAssemblies.net48  -OutputDirectory N:\tools\refasm
nuget install Microsoft.NETFramework.ReferenceAssemblies.net481 -OutputDirectory N:\tools\refasm
copier N:\tools\refasm\Microsoft.NETFramework.ReferenceAssemblies.net48.*\build\.NETFramework\v4.8     vers N:\tools\fxroot\.NETFramework\v4.8
copier N:\tools\refasm\Microsoft.NETFramework.ReferenceAssemblies.net481.*\build\.NETFramework\v4.8.1  vers N:\tools\fxroot\.NETFramework\v4.8.1
```

## Etapes

1. `git submodule update --init --recursive` : les 9 sous-modules (Libraries/CueSharp, FelLib, LibUsbDotNet, ProgressODoom, SSH.NET, SpineGen, tiny7z, driver/source, libwdi) ne sont plus vides. Aucun changement de pointeur (`git status` propre).
2. Environnement de build (fichier .cmd a executer avant `nuget`/`msbuild`) :

```
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=x86 -host_arch=x64
set PATH=N:\tools;<repo>\tools;%PATH%
set MSBuildSDKsPath=N:\dotnet\sdk\8.0.427\Sdks
set DOTNET_ROOT=N:\dotnet
set DOTNET_HOST_PATH=N:\dotnet\dotnet.exe
set MSBuildEnableWorkloadResolver=false
set TargetFrameworkRootPath=N:\tools\fxroot\
cd /d <repo>
```

3. `nuget restore hakchi_gui.sln`
4. Commande identique a la CI (`.github/workflows/dotnet-desktop.yml`, etape « Build solution ») :

```
msbuild Installer/Installer.vcxproj /p:SolutionDir=<repo>\ /p:SolutionPath=<repo>\hakchi_gui.sln "/p:Platform=Any CPU"
```

   Duree : environ 10 s en incremental, code retour 0, 0 erreur, 74 avertissements. `hakchi_gui\ApiKeys\TheGamesDB.txt` est cree vide par le pre-build (ignore par git ; la vraie cle n'est jamais utilisee en local, `New Text Document.txt` reste hors de l'index).
5. Verification comme la CI : `hakchi.exe --versionFormat {0} --versionFile <fichier>` ecrit `3.9.3-98aef422`.

## Pieges rencontres

1. **VS 2022 Community sans le workload .NET SDK** : `Could not resolve SDK "Microsoft.NET.Sdk"`. Corrige par `MSBuildSDKsPath` vers le SDK portable `N:\dotnet`, plus `MSBuildEnableWorkloadResolver=false` (sinon `WorkloadAutoImportPropsLocator` introuvable).
2. **Pas de targeting pack .NET 4.8** (seul le 4.8.1 est installe) : `MSB3644`. Corrige par `TargetFrameworkRootPath=N:\tools\fxroot\` (v4.8 et v4.8.1 ; nsi-helper cible 4.8.1). `FrameworkPathOverride` ne convient pas : il casse la resolution des facades `netstandard` (CS0012).
3. **`"busybox"` introuvable dans le post-build de hakchi_gui** (`tar ... | "busybox" tar -xvf`) : `<repo>\tools` (qui contient `busybox.exe`) doit etre dans le `PATH`. Sans cela `bin` est incomplet (pas de `basehmods.tar` ni `driver\`).
4. **NSIS 3.10 non installe, et telechargement impossible** : sourceforge repond 403 depuis cette machine (toutes les URL testees). L'etape finale de Installer.vcxproj (`makensis.exe NSI\Installer.nsi`) echoue donc (« 'makensis.exe' is not recognized »), mais ce n'est pas fatal : la commande NMake se termine par `del ...` dont le code retour 0 masque l'echec, msbuild rend 0. Consequence : **`hakchi2-ce-<version>-installer.exe` n'est pas produit** ; tout le reste (hakchi.exe, basehmods.tar, driver\, langues) l'est. Si l'installateur devient necessaire : NSIS 3.10 en zip portable sous `N:\nsis`, ajoute au `PATH` pour le build seulement.
5. `nsi-helper`, `Zipper`, `CueSharp` sont au format de projet ancien : ils ont besoin des memes reference assemblies (piege 2).
6. **Instance unique (mutex nomme `hakchi2`)** : si un autre hakchi.exe tourne (ici `R:\hakchi2-ce-3.8.0-release\hakchi.exe`, PID 44488, lance par l'utilisateur), un second lancement se termine aussitot (code 0, sans fenetre) et **met au premier plan la fenetre de l'instance existante** (`ShowWindow`/`SetForegroundWindow`). Pour mesurer, un exemplaire de l'exe est patche : un seul octet change dans le nom de mutex (`hakchi2` -> `hakchi3`, UTF-16 dans le tas `#US`, offset 5177126+12) -> `P:\HakchiSNESUSA\app\hakchi_mtx.exe` (+ `hakchi_mtx.exe.config` copie de `hakchi.exe.config`), hors depot. `hakchi.exe` reste le binaire de build, intact. Le meme patch convient pour tout test futur en parallele de l'instance de l'utilisateur.

## Copie de travail (`P:\HakchiSNESUSA\app`)

- `robocopy hakchi_gui\bin\Debug\net48 P:\HakchiSNESUSA\app /E`
- depuis `R:\hakchi2-ce-3.8.0-release\` (lecture seule) : `games` (195 dossiers, 1440 fichiers, 9,6 Go), `games_originals` (816 fichiers), `art` (7), `config` (config.json + motd.md), `folder_images` (778), `user_mods` (58). Comptes de fichiers identiques source/copie. Rien n'a ete ecrit sur R:.
- Mode portable (config et dossiers a cote de l'exe).

## Premier lancement hors ligne

- Detection de console : hakchi ne detecte que les peripheriques USB (Clovershell/FEL, VID 1F3A) ; aucun n'est present sur cette machine (seulement un telephone Samsung), donc aucune connexion a la console. Aucune commande envoyee a 169.254.13.37 (un unique ping ICMP pour verifier l'etat reseau : il a repondu).
- Fenetres observees au demarrage (3 lancements) : « Chargement des mods... », fenetre principale « hakchi CE v3.9.3-98aef422 - Super NES (Europe) », « Chargement des jeux... », « Telechargement du dernier hakchi » (telechargement automatique non interactif), « Mise a jour de la liste... », et « Mise a jour du cache local de jeux originaux... » lors d'un lancement precedent. Aucune boite proposant mise a jour, migration ou flash n'est apparue ; rien n'a ete accepte ni clique. Chaque lancement a ete ferme par `Stop-Process -Id <PID>`.
- La liste contient 216 elements d'apres `LVM_GETITEMCOUNT` (195 dossiers de jeux sur disque ; ecart non verifie visuellement : pas d'acces GUI).
