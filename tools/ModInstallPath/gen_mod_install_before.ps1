# Genere hakchi_gui.Tests/Data/mod_install_before.json (Task 9) : empreinte IL du chemin d'installation / desinstallation des mods
# dans un hakchi.exe compile depuis snes-usa/theme-b1 (meme algorithme que le test : IlCallees.cs).
#
# Construire la reference (une fois, hors de J:) :
#   git worktree add --detach P:\HakchiSNESUSA\wt_theme_b1 snes-usa/theme-b1
#   copier les sous-modules Libraries\* (memes commits) dans le worktree
#   msbuild hakchi_gui\hakchi_gui.csproj /restore "/p:SolutionDir=P:\HakchiSNESUSA\wt_theme_b1\\" /p:Configuration=Debug /p:Platform=AnyCPU
# Puis, avec Windows PowerShell 5.1 en 32 bits (hakchi.exe est x86) :
#   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\ModInstallPath\gen_mod_install_before.ps1 `
#       -Exe P:\HakchiSNESUSA\wt_theme_b1\hakchi_gui\bin\Debug\net48\hakchi.exe -Base snes-usa/theme-b1
# Puis retirer le worktree : git worktree remove --force P:/HakchiSNESUSA/wt_theme_b1 ; git worktree prune
#
# Garde-fou : refuse si hakchi.exe ne vient pas d'un worktree dont le HEAD est exactement le commit de -Base, sans modification
# des fichiers suivis. Le SHA du commit est ecrit dans le JSON (_base_sha) et verifie par le test.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Base = "snes-usa/theme-b1",
    [string]$Out = ""
)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Out) { $Out = Join-Path $here "..\..\hakchi_gui.Tests\Data\mod_install_before.json" }
$exeFull = (Resolve-Path $Exe).Path

# worktree qui contient l'exe : premier dossier parent qui porte un .git (fichier pour un worktree, dossier pour la copie principale)
$root = Split-Path -Parent $exeFull
while ($root -and -not (Test-Path (Join-Path $root ".git"))) { $root = Split-Path -Parent $root }
if (-not $root) { throw "refus : $exeFull n'est dans aucun worktree git" }
$head = (& git -C $root rev-parse HEAD).Trim()
$baseSha = (& git -C $root rev-parse "$Base^{commit}").Trim()
if ($LASTEXITCODE -ne 0 -or -not $baseSha) { throw "refus : $Base introuvable" }
if ($head -ne $baseSha) { throw "refus : HEAD du worktree $root = $head, pas $Base ($baseSha)" }
& git -C $root diff --quiet --ignore-submodules HEAD -- .
if ($LASTEXITCODE -ne 0) { throw "refus : le worktree $root a des modifications de fichiers suivis" }

Add-Type -Path (Join-Path $here "IlCallees.cs")
$asm = [System.Reflection.Assembly]::LoadFrom($exeFull)
# initialiseur de module (Costura) : resolution des assemblages embarques pour ResolveMethod
[System.Runtime.CompilerServices.RuntimeHelpers]::RunModuleConstructor($asm.ManifestModule.ModuleHandle)

$roots = [ordered]@{}
foreach ($r in [ModInstallPath.IlCallees]::Roots) {
    $callees = $null; $strings = $null
    [ModInstallPath.IlCallees]::Fingerprint($asm, $r[0], $r[1], [ref]$callees, [ref]$strings)
    $roots[$r[0] + "::" + $r[1]] = [ordered]@{ callees = @($callees); strings = @($strings) }
}
$doc = [ordered]@{ _base = $Base; _base_sha = $baseSha; _exe = (Split-Path -Leaf $exeFull); roots = $roots }
$json = $doc | ConvertTo-Json -Depth 6   # (le fichier commite est reindente par json.dumps(indent=1) : meme contenu)
[System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($Out), $json + "`n", (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("ecrit " + [System.IO.Path]::GetFullPath($Out) + " : " + $roots.Count + " racines, base " + $baseSha)
