# Generates app.ico with multiple resolutions (PNG-compressed ICO entries).
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$outPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "app.ico"
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = New-Object System.Collections.ArrayList

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $r = [int]($s * 0.22)
    $d = $r * 2
    $max = $s - 1
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($max - $d, 0, $d, $d, 270, 90)
    $path.AddArc($max - $d, $max - $d, $d, $d, 0, 90)
    $path.AddArc(0, $max - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $p1 = New-Object System.Drawing.Point 0, 0
    $p2 = New-Object System.Drawing.Point 0, $s
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $p1, $p2,
        [System.Drawing.Color]::FromArgb(255, 159, 122, 255),
        [System.Drawing.Color]::FromArgb(255, 108, 62, 240))
    $g.FillPath($brush, $path)

    # Toggle track
    $tw = [int]($s * 0.62)
    $th = [int]($s * 0.30)
    if ($th -lt 4) { $th = 4 }
    $tx = [int](($s - $tw) / 2)
    $ty = [int](($s - $th) / 2)
    $tp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tp.AddArc($tx, $ty, $th, $th, 180, 90)
    $tp.AddArc($tx + $tw - $th, $ty, $th, $th, 270, 90)
    $tp.AddArc($tx + $tw - $th, $ty + $th - $th, $th, $th, 0, 90)
    $tp.AddArc($tx, $ty + $th - $th, $th, $th, 90, 90)
    $tp.CloseFigure()
    $g.FillPath([System.Drawing.Brushes]::White, $tp)

    # Knob
    $kd = [int]($th * 0.72)
    $kx = $tx + $tw - $kd - [int]($th * 0.14)
    $ky = $ty + [int](($th - $kd) / 2)
    $knob = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 120, 72, 250))
    $g.FillEllipse($knob, $kx, $ky, $kd, $kd)

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$pngs.Add($ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $data = $pngs[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)
    $bw.Write([Byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($data in $pngs) { $bw.Write($data) }
$bw.Close()
$fs.Close()

Write-Host "Wrote $outPath ($([math]::Round((Get-Item $outPath).Length / 1KB, 1)) KB)"
