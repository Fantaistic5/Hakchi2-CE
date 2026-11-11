param([int]$Runs = 3, [string]$Exe = 'P:\HakchiSNESUSA\app\hakchi_new.exe', [int]$MaxMs = 25000, [int]$StableMs = 2500,
      # Interface B1 : listViewGames est cachee derriere la grille de jaquettes. -HiddenLists compte aussi les ListView cachees (C).
      [switch]$HiddenLists,
      # Interface B1 : « grille complete » = derniere modification des pixels de la grille (controle Name = "grid"),
      # capturee par PrintWindow toutes les ~40 ms, apres StableMs ms sans changement. Un lancement de calibration
      # (non compte) trouve la grille par UI Automation (AutomationId = Name WinForms).
      [switch]$Grid,
      [string]$SaveLastFrame = '',
      # diagnostic : enregistre chaque etat different de la grille (<dossier>\run<N>_<ms>.png)
      [string]$SaveChanges = '')

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class W32 {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
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
    // Items de tous les SysListView32 sous la fenetre (handle:nombre:v|h)
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
    // Fenetre fille de meme classe et de meme position relative (calibration)
    public static IntPtr FindChild(IntPtr top, string cls, int dx, int dy, int w, int hgt) {
        IntPtr found = IntPtr.Zero; RECT tr; GetWindowRect(top, out tr);
        EnumChildWindows(top, (h, l) => {
            RECT r; GetWindowRect(h, out r);
            if (r.L - tr.L == dx && r.T - tr.T == dy && r.R - r.L == w && r.B - r.T == hgt && Cls(h) == cls && IsWindowVisible(h)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc, byte[] bmi, uint usage, out IntPtr bits, IntPtr sec, uint off);
    static long Area(IntPtr h) { RECT r; GetWindowRect(h, out r); return (long)(r.R - r.L) * (r.B - r.T); }
    static IntPtr Largest(IntPtr parent, bool direct) {
        IntPtr best = IntPtr.Zero; long ba = 0;
        EnumChildWindows(parent, (h, l) => {
            if (IsWindowVisible(h) && (!direct || GetParent(h) == parent)) { long a = Area(h); if (a > ba) { ba = a; best = h; } }
            return true;
        }, IntPtr.Zero);
        return best;
    }
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    public static IntPtr GridCandidate(IntPtr top) {
        IntPtr cur = Largest(top, false);
        while (cur != IntPtr.Zero) {
            IntPtr k = Largest(cur, true);
            if (k == IntPtr.Zero || Area(k) * 100 < Area(cur) * 85) break;
            cur = k;
        }
        return cur;
    }
    public static byte[] LastPixels; public static int LastW, LastH;
    // Capture de la zone client (PW_CLIENTONLY | PW_RENDERFULLCONTENT) en DIB 32 bits et empreinte FNV-1a
    public static ulong Capture(IntPtr h) {
        RECT r; GetClientRect(h, out r); int w = r.R - r.L, ht = r.B - r.T;
        if (w <= 0 || ht <= 0) return 0;
        var bmi = new byte[40];
        BitConverter.GetBytes(40).CopyTo(bmi, 0); BitConverter.GetBytes(w).CopyTo(bmi, 4); BitConverter.GetBytes(-ht).CopyTo(bmi, 8);
        BitConverter.GetBytes((short)1).CopyTo(bmi, 12); BitConverter.GetBytes((short)32).CopyTo(bmi, 14);
        IntPtr sdc = GetDC(IntPtr.Zero), mdc = CreateCompatibleDC(sdc), bits;
        IntPtr dib = CreateDIBSection(sdc, bmi, 0, out bits, IntPtr.Zero, 0);
        IntPtr old = SelectObject(mdc, dib);
        PrintWindow(h, mdc, 3);
        int n = w * ht * 4; var buf = new byte[n]; Marshal.Copy(bits, buf, 0, n);
        SelectObject(mdc, old); DeleteObject(dib); DeleteDC(mdc); ReleaseDC(IntPtr.Zero, sdc);
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < n; i += 4) { hash ^= (ulong)(buf[i] | buf[i + 1] << 8 | buf[i + 2] << 16); hash *= 1099511628211UL; }
        LastPixels = buf; LastW = w; LastH = ht;
        return hash;
    }
}
'@

$appDir = Split-Path $Exe
$libCount = (Get-ChildItem -Directory (Join-Path $appDir 'games') | Where-Object { Get-ChildItem $_.FullName -Filter *.desktop -ErrorAction SilentlyContinue }).Count
Write-Output "Bibliotheque (dossiers de jeux avec .desktop) : $libCount"

$existing = Get-Process -Name (Split-Path $Exe -LeafBase) -ErrorAction SilentlyContinue
if ($existing) { Write-Output "ABANDON : $((Split-Path $Exe -LeafBase)) tourne deja (PID $($existing.Id -join ','))"; exit 3 }

function Save-Frame([string]$path) {
    Add-Type -AssemblyName System.Drawing
    $w = [W32]::LastW; $h = [W32]::LastH
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppRgb)
    $d = $bmp.LockBits((New-Object System.Drawing.Rectangle 0, 0, $w, $h), 'WriteOnly', ([System.Drawing.Imaging.PixelFormat]::Format32bppRgb))
    [Runtime.InteropServices.Marshal]::Copy([W32]::LastPixels, 0, $d.Scan0, $w * $h * 4); $bmp.UnlockBits($d)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

function Start-Hakchi {
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = $Exe; $psi.WorkingDirectory = $appDir; $psi.UseShellExecute = $false
    return [Diagnostics.Process]::Start($psi)
}

$gridCls = $null; $gridGeom = $null
if ($Grid) {
    # Calibration : lancement non compte. Les noms WinForms ne sont pas exposes en UI Automation (AutomationId = HWND) :
    # la grille est la plus grande fenetre fille visible, en descendant tant qu'une fille en couvre >= 85 %.
    $p = Start-Hakchi; $sw = [Diagnostics.Stopwatch]::StartNew(); $seen = $null
    try {
        while ($sw.ElapsedMilliseconds -lt $MaxMs -and -not $gridCls) {
            Start-Sleep -Milliseconds 200
            foreach ($h in [W32]::TopWindows([uint32]$p.Id)) {
                if ([W32]::Title($h) -like 'hakchi CE*' -and [W32]::IsWindowVisible($h)) {
                    if ($null -eq $seen) { $seen = $sw.ElapsedMilliseconds }
                    if ($sw.ElapsedMilliseconds - $seen -lt 4000) { continue }   # disposition finale
                    $gh = [W32]::GridCandidate($h)
                    if ($gh -ne [IntPtr]::Zero) {
                        $tr = New-Object W32+RECT; $gr = New-Object W32+RECT
                        [void][W32]::GetWindowRect($h, [ref]$tr); [void][W32]::GetWindowRect($gh, [ref]$gr)
                        $gridCls = [W32]::Cls($gh)
                        $gridGeom = @(($gr.L - $tr.L), ($gr.T - $tr.T), ($gr.R - $gr.L), ($gr.B - $gr.T))
                        if ($SaveLastFrame) { [void][W32]::Capture($gh); Save-Frame ($SaveLastFrame -f 0) }
                    }
                }
            }
        }
    } finally { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }   # par PID
    Start-Sleep -Milliseconds 800
    if (-not $gridCls) { Write-Output "ABANDON : grille introuvable"; exit 4 }
    Write-Output ("calibration : grille classe={0} position relative={1}" -f $gridCls, ($gridGeom -join ','))
}

for ($run = 1; $run -le $Runs; $run++) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Hakchi
    $procId = $p.Id
    $tB = $null; $tC = $null; $lastChange = 0; $lastCount = -1; $detail = ''; $mainTitle = ''
    $gh = [IntPtr]::Zero; $tGridFirst = $null; $tGridLast = $null; $lastHash = 0; $frames = 0; $changes = 0; $changeTimes = @()
    try {
        while ($sw.ElapsedMilliseconds -lt $MaxMs) {
            Start-Sleep -Milliseconds 40
            if ($p.HasExited) { break }
            $now = $sw.ElapsedMilliseconds
            foreach ($h in [W32]::TopWindows([uint32]$procId)) {
                $t = [W32]::Title($h)
                if ($t -like 'hakchi CE*' -and [W32]::IsWindowVisible($h)) {
                    if ($null -eq $tB) { $tB = $now; $mainTitle = $t }
                    $lv = [W32]::ListViews($h)
                    $total = 0
                    foreach ($part in ($lv -split ',')) { if ($part) { $f = $part -split ':'; if ($f[2] -eq 'v' -or $HiddenLists) { $total += [int]$f[1] } } }
                    if ($total -gt 0 -and $null -eq $tC) { $tC = $now }
                    if ($total -ne $lastCount) { $lastCount = $total; $lastChange = $now; $detail = $lv }
                    if ($Grid) {
                        if ($gh -eq [IntPtr]::Zero) { $gh = [W32]::FindChild($h, $gridCls, $gridGeom[0], $gridGeom[1], $gridGeom[2], $gridGeom[3]) }
                        if ($gh -ne [IntPtr]::Zero) {
                            $hash = [W32]::Capture($gh); $frames++
                            if ($hash -ne $lastHash) { $lastHash = $hash; $changes++; $changeTimes += $now; if ($SaveChanges) { Save-Frame (Join-Path $SaveChanges ("run{0}_{1}.png" -f $run, $now)) } if ($null -eq $tGridFirst) { $tGridFirst = $now }; $tGridLast = $now }
                        }
                    }
                }
            }
            $stableList = ($null -ne $tC -and ($now - $lastChange) -ge $StableMs)
            $stableGrid = (-not $Grid) -or ($null -ne $tGridLast -and ($now - $tGridLast) -ge $StableMs)
            if ($stableList -and $stableGrid) { break }
        }
        if ($Grid -and $SaveLastFrame -and $gh -ne [IntPtr]::Zero) { [void][W32]::Capture($gh); Save-Frame ($SaveLastFrame -f $run) }
    } finally {
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue   # fermeture par PID, jamais par nom d'image
    }
    Start-Sleep -Milliseconds 800
    $tFull = if ($Grid) { $tGridLast } else { $lastChange }
    $ok = ($lastCount -ge $libCount)
    Write-Output ("run {0}: B={1} ms  C={2} ms  Full={3} ms  items={4}  >=lib:{5}  gridFirst={6} gridFrames={7} gridChanges={8} [{11}]  lv=[{9}]  title='{10}'" -f $run, $tB, $tC, $tFull, $lastCount, $ok, $tGridFirst, $frames, $changes, $detail, $mainTitle, ($changeTimes -join ','))
}
