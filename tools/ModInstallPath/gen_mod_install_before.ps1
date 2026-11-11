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
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Base = "snes-usa/theme-b1",
    [string]$Out = ""
)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Out) { $Out = Join-Path $here "..\..\hakchi_gui.Tests\Data\mod_install_before.json" }
Add-Type -Path (Join-Path $here "IlCallees.cs")
$asm = [System.Reflection.Assembly]::LoadFrom((Resolve-Path $Exe))
# initialiseur de module (Costura) : resolution des assemblages embarques pour ResolveMethod
[System.Runtime.CompilerServices.RuntimeHelpers]::RunModuleConstructor($asm.ManifestModule.ModuleHandle)

$roots = [ordered]@{}
foreach ($r in [ModInstallPath.IlCallees]::Roots) {
    $callees = $null; $strings = $null
    [ModInstallPath.IlCallees]::Fingerprint($asm, $r[0], $r[1], [ref]$callees, [ref]$strings)
    $roots[$r[0] + "::" + $r[1]] = [ordered]@{ callees = @($callees); strings = @($strings) }
}
$doc = [ordered]@{ _base = $Base; _exe = (Split-Path -Leaf $Exe); roots = $roots }
$json = $doc | ConvertTo-Json -Depth 6   # (le fichier commite a ete reindente par json.dumps(indent=1) : meme contenu)
[System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($Out), $json + "`n", (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("ecrit " + [System.IO.Path]::GetFullPath($Out) + " : " + $roots.Count + " racines")
