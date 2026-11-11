# Mesure de ce que l'utilisateur voit au demarrage, sans passer par le thread UI de hakchi.
# Capture de la fenetre principale par GetWindowDC + BitBlt (surface DWM de la fenetre : ne bloque pas, meme si le thread UI
# est occupe, et ne depend pas des fenetres qui la recouvrent), toutes les ~15 ms, depuis ce processus. Aucun message envoye.
#
#  B          fenetre principale visible (titre « hakchi CE... »)
#  C_visible  premiers jeux affiches :
#             - interface d'origine : la ListView (fond blanc) contient du texte (>= 150 px sombres) ;
#             - interface B1 : la grille montre des cases (>= 5 % de pixels RowActive #26252E) ;
#  Full       grille complete :
#             - interface B1 : pixels « jaquette » (saturation >= 60) >= 97 % de leur nombre final, cases presentes ;
#             - interface d'origine : pas de jaquettes, la liste est complete des qu'elle s'affiche (Full = C_visible).
#
# Usage : measure-visible.ps1 -Exes a.exe,b.exe -Rounds 10 [-SaveDir <dossier>]  (lancements alternes, fermeture par PID)
param([string[]]$Exes = @('P:\HakchiSNESUSA\app\hakchi_mtx.exe', 'P:\HakchiSNESUSA\app\hakchi_new.exe'),
      [int]$Rounds = 10, [int]$MaxMs = 20000, [int]$AfterMs = 3500, [string]$SaveDir = '')

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class Vis {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int InternalGetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc, byte[] bmi, uint usage, out IntPtr bits, IntPtr sec, uint off);

    static string Title(IntPtr h) { var sb = new StringBuilder(256); InternalGetWindowText(h, sb, 256); return sb.ToString(); }
    static string Cls(IntPtr h) { var sb = new StringBuilder(128); GetClassName(h, sb, 128); return sb.ToString(); }
    static long Area(IntPtr h) { RECT r; GetWindowRect(h, out r); return (long)(r.R - r.L) * (r.B - r.T); }

    static IntPtr MainWindow(uint pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid && IsWindowVisible(h) && Title(h).StartsWith("hakchi CE")) { found = h; return false; } return true; }, IntPtr.Zero);
        return found;
    }
    static IntPtr Largest(IntPtr parent, bool direct, string cls) {
        IntPtr best = IntPtr.Zero; long ba = 0;
        EnumChildWindows(parent, (h, l) => {
            if (IsWindowVisible(h) && (!direct || GetParent(h) == parent) && (cls == null || Cls(h).Contains(cls))) { long a = Area(h); if (a > ba) { ba = a; best = h; } }
            return true; }, IntPtr.Zero);
        return best;
    }
    // Interface B1 : la grille = plus grande fenetre fille visible, en descendant tant qu'une fille en couvre >= 85 %.
    static IntPtr Grid(IntPtr top) {
        IntPtr cur = Largest(top, false, null);
        while (cur != IntPtr.Zero) { IntPtr k = Largest(cur, true, null); if (k == IntPtr.Zero || Area(k) * 100 < Area(cur) * 85) break; cur = k; }
        return cur;
    }

    static byte[] Grab(IntPtr top, out int w, out int ht) {
        RECT r; GetWindowRect(top, out r); w = r.R - r.L; ht = r.B - r.T;
        if (w <= 0 || ht <= 0) return null;
        var bmi = new byte[40];
        BitConverter.GetBytes(40).CopyTo(bmi, 0); BitConverter.GetBytes(w).CopyTo(bmi, 4); BitConverter.GetBytes(-ht).CopyTo(bmi, 8);
        BitConverter.GetBytes((short)1).CopyTo(bmi, 12); BitConverter.GetBytes((short)32).CopyTo(bmi, 14);
        IntPtr sdc = GetDC(IntPtr.Zero), mdc = CreateCompatibleDC(sdc), bits;
        IntPtr dib = CreateDIBSection(sdc, bmi, 0, out bits, IntPtr.Zero, 0);
        IntPtr old = SelectObject(mdc, dib);
        IntPtr wdc = GetWindowDC(top); BitBlt(mdc, 0, 0, w, ht, wdc, 0, 0, 0x00CC0020); ReleaseDC(top, wdc);
        var buf = new byte[w * ht * 4]; Marshal.Copy(bits, buf, 0, buf.Length);
        SelectObject(mdc, old); DeleteObject(dib); DeleteDC(mdc); ReleaseDC(IntPtr.Zero, sdc);
        return buf;
    }

    public class Frame { public long T; public double RowActive, White; public int Dark, Colorful; public byte[] Px; public int W, H, RX, RY, RW, RH; }

    // Mesures dans la zone de la liste / grille (coordonnees relatives a la fenetre)
    static Frame Measure(byte[] px, int w, int h, RECT region, RECT win, long t) {
        var f = new Frame { T = t, W = w, H = h };
        int x0 = Math.Max(0, region.L - win.L), y0 = Math.Max(0, region.T - win.T);
        int x1 = Math.Min(w, region.R - win.L), y1 = Math.Min(h, region.B - win.T);
        f.RX = x0; f.RY = y0; f.RW = x1 - x0; f.RH = y1 - y0;
        long n = 0, ra = 0, wh = 0; int dark = 0, col = 0;
        for (int y = y0; y < y1; y += 2) for (int x = x0; x < x1; x += 2) {
            int i = (y * w + x) * 4; int b = px[i], g = px[i + 1], r = px[i + 2]; n++;
            if (Math.Abs(r - 0x26) <= 3 && Math.Abs(g - 0x25) <= 3 && Math.Abs(b - 0x2E) <= 3) ra++;
            int lum = (r * 299 + g * 587 + b * 114) / 1000;
            if (lum >= 235) wh++;
            if (lum < 110) dark++;
            int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            if (mx - mn >= 60) col++;
        }
        f.RowActive = n == 0 ? 0 : (double)ra / n; f.White = n == 0 ? 0 : (double)wh / n; f.Dark = dark; f.Colorful = col;
        return f;
    }

    public static string Run(string exe, bool b1, int maxMs, int afterMs, string saveDir, string tag) {
        var sw = Stopwatch.StartNew();
        var psi = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false };
        var p = Process.Start(psi);
        var frames = new List<Frame>();
        long tB = -1, tCv = -1; string err = "";
        try {
            while (sw.ElapsedMilliseconds < maxMs) {
                Thread.Sleep(15);
                if (p.HasExited) { err = "exited"; break; }
                long now = sw.ElapsedMilliseconds;
                IntPtr top = MainWindow((uint)p.Id);
                if (top == IntPtr.Zero) continue;
                if (tB < 0) tB = now;
                IntPtr reg = b1 ? Grid(top) : Largest(top, false, "SysListView32");
                if (reg == IntPtr.Zero) continue;
                RECT rr, wr; GetWindowRect(reg, out rr); GetWindowRect(top, out wr);
                int w, h; var px = Grab(top, out w, out h); if (px == null) continue;
                var f = Measure(px, w, h, rr, wr, now);
                if (saveDir != null) f.Px = px;
                frames.Add(f);
                bool visible = b1 ? f.RowActive >= 0.05 : (f.White >= 0.5 && f.Dark >= 150);
                if (visible && tCv < 0) tCv = now;
                if (tCv >= 0 && now - tCv >= afterMs) break;
            }
        } finally { try { p.Kill(); } catch (Exception) { } }      // ce processus, par son PID
        try { p.WaitForExit(3000); } catch (Exception) { }
        long tFull = -1;
        if (tCv >= 0) {
            if (!b1) tFull = tCv;
            else {
                int fin = frames[frames.Count - 1].Colorful;
                foreach (var f in frames) if (f.T >= tCv && f.RowActive >= 0.02 && f.Colorful >= fin * 0.97) { tFull = f.T; break; }
            }
        }
        if (saveDir != null) Save(frames, saveDir, tag, tCv, tFull);
        int finalCol = frames.Count > 0 ? frames[frames.Count - 1].Colorful : 0;
        return string.Format("B={0} Cvis={1} Full={2} frames={3} finalColorful={4} {5}", tB, tCv, tFull, frames.Count, finalCol, err);
    }

    static void Save(List<Frame> frames, string dir, string tag, long tCv, long tFull) {
        var sb = new StringBuilder("t;rowActive;white;dark;colorful\n");
        foreach (var f in frames) sb.AppendFormat("{0};{1:0.000};{2:0.000};{3};{4}\n", f.T, f.RowActive, f.White, f.Dark, f.Colorful);
        File.WriteAllText(Path.Combine(dir, tag + "_timeline.csv"), sb.ToString());
        foreach (var f in frames) if (f.T == tCv || f.T == tFull) SaveBmp(f, Path.Combine(dir, tag + "_" + f.T + ".bmp"));
        if (frames.Count > 0) { var l = frames[frames.Count - 1]; SaveBmp(l, Path.Combine(dir, tag + "_last.bmp")); }
    }
    static void SaveBmp(Frame f, string path) {
        if (f.Px == null) return;
        int w = f.RW, h = f.RH; int stride = w * 4;
        using (var fs = File.Create(path)) using (var bw = new BinaryWriter(fs)) {
            bw.Write((ushort)0x4D42); bw.Write(54 + stride * h); bw.Write(0); bw.Write(54);
            bw.Write(40); bw.Write(w); bw.Write(-h); bw.Write((short)1); bw.Write((short)32); bw.Write(0); bw.Write(stride * h); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
            for (int y = 0; y < h; y++) bw.Write(f.Px, ((f.RY + y) * f.W + f.RX) * 4, stride);
        }
    }
}
'@

foreach ($e in $Exes) {
    $name = [IO.Path]::GetFileNameWithoutExtension($e)
    if (Get-Process -Name $name -ErrorAction SilentlyContinue) { Write-Output "ABANDON : $name tourne deja"; exit 3 }
}
# interface : B1 si l'assemblage contient l'espace de noms du theme (sans charger l'exe)
$isB1 = @{}
foreach ($e in $Exes) { $isB1[$e] = [bool](Select-String -Path $e -Pattern 'B1Skin' -SimpleMatch -Encoding Unicode -Quiet) -or [bool](Select-String -Path $e -Pattern 'B1Skin' -SimpleMatch -Quiet) }
for ($i = 1; $i -le $Rounds; $i++) {
    foreach ($e in $Exes) {
        $tag = '{0}_{1}' -f [IO.Path]::GetFileNameWithoutExtension($e), $i
        $r = [Vis]::Run($e, $isB1[$e], $MaxMs, $AfterMs, $(if ($SaveDir) { $SaveDir } else { $null }), $tag)
        Write-Output ("{0} {1} b1={2} {3}" -f $i, [IO.Path]::GetFileName($e), $isB1[$e], $r)
        Start-Sleep -Milliseconds 800
    }
}
