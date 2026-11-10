param([int]$Runs = 3, [string]$Exe = 'P:\HakchiSNESUSA\app\hakchi_new.exe', [int]$MaxMs = 25000, [int]$StableMs = 2500)

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class W32 {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    public static List<IntPtr> TopWindows(uint pid) {
        var r = new List<IntPtr>();
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) r.Add(h); return true; }, IntPtr.Zero);
        return r;
    }
    public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
    public static string Cls(IntPtr h) { var sb = new StringBuilder(128); GetClassName(h, sb, 128); return sb.ToString(); }
    // Somme des items de tous les SysListView32 visibles ou non sous la fenetre, + detail
    public static string ListViews(IntPtr top) {
        var parts = new List<string>();
        EnumChildWindows(top, (h, l) => {
            if (Cls(h).Contains("SysListView32")) {
                int n = (int)SendMessage(h, 0x1004, IntPtr.Zero, IntPtr.Zero);
                parts.Add(h.ToInt64().ToString() + ":" + n + ":" + (IsWindowVisible(h) ? "v" : "h"));
            }
            return true;
        }, IntPtr.Zero);
        return string.Join(",", parts);
    }
}
'@

$appDir = Split-Path $Exe
$libCount = (Get-ChildItem -Directory (Join-Path $appDir 'games') | Where-Object { Get-ChildItem $_.FullName -Filter *.desktop -ErrorAction SilentlyContinue }).Count
Write-Output "Bibliotheque (dossiers de jeux avec .desktop) : $libCount"

$existing = Get-Process -Name (Split-Path $Exe -LeafBase) -ErrorAction SilentlyContinue
if ($existing) { Write-Output "ABANDON : $((Split-Path $Exe -LeafBase)) tourne deja (PID $($existing.Id -join ','))"; exit 3 }

$results = @()
for ($run = 1; $run -le $Runs; $run++) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = $Exe
    $psi.WorkingDirectory = $appDir
    $psi.UseShellExecute = $false
    $p = [Diagnostics.Process]::Start($psi)
    $procId = $p.Id
    $tB = $null; $tC = $null; $maxItems = 0; $tFull = $null; $lastChange = 0; $lastCount = -1; $detail = ''; $mainTitle = ''
    while ($sw.ElapsedMilliseconds -lt $MaxMs) {
        Start-Sleep -Milliseconds 40
        if ($p.HasExited) { break }
        $now = $sw.ElapsedMilliseconds
        $wins = [W32]::TopWindows([uint32]$procId)
        foreach ($h in $wins) {
            $t = [W32]::Title($h)
            if ($t -like 'hakchi CE*' -and [W32]::IsWindowVisible($h)) {
                if ($null -eq $tB) { $tB = $now; $mainTitle = $t }
                $lv = [W32]::ListViews($h)
                $total = 0
                foreach ($part in ($lv -split ',')) { if ($part) { $f = $part -split ':'; if ($f[2] -eq 'v') { $total += [int]$f[1] } } }
                if ($total -gt 0 -and $null -eq $tC) { $tC = $now }
                if ($total -ne $lastCount) { $lastCount = $total; $lastChange = $now; $detail = $lv }
                if ($total -gt $maxItems) { $maxItems = $total }
            }
        }
        if ($null -ne $tC -and ($now - $lastChange) -ge $StableMs) { break }
    }
    # "grille complete" = instant ou le compteur atteint sa valeur finale (stable StableMs ms) et >= bibliotheque
    $tFull = $lastChange
    $ok = ($lastCount -ge $libCount)
    Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue   # fermeture par PID, jamais par nom d'image
    Start-Sleep -Milliseconds 800
    $results += [pscustomobject]@{ Run = $run; B_ms = $tB; C_ms = $tC; Full_ms = $tFull; Items = $lastCount; AtLeastLib = $ok; ListViews = $detail; Title = $mainTitle }
    Write-Output ("run {0}: B={1} ms  C={2} ms  Full={3} ms  items={4}  >=lib:{5}  lv=[{6}]  title='{7}'" -f $run, $tB, $tC, $tFull, $lastCount, $ok, $detail, $mainTitle)
}
