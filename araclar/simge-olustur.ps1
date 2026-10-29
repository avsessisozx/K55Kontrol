# K55 Kontrol uygulama simgesini (assets\K55Kontrol.ico) üretir.
# Küçük boyutlar 32 bit BMP, 256 piksel PNG olarak kaydedilir (Windows simge biçimi).
param([string]$Cikti = (Join-Path $PSScriptRoot '..\assets\K55Kontrol.ico'))

Add-Type -AssemblyName System.Drawing

function Ciz([int]$n) {
    $bmp = New-Object System.Drawing.Bitmap $n, $n, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $n / 256.0

    # Zemin: yuvarlatılmış kare, çapraz renk geçişi
    $yol = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 56 * $k; $m = 8 * $k; $w = $n - 2 * $m
    $yol.AddArc($m, $m, $r, $r, 180, 90); $yol.AddArc($m + $w - $r, $m, $r, $r, 270, 90)
    $yol.AddArc($m + $w - $r, $m + $w - $r, $r, $r, 0, 90); $yol.AddArc($m, $m + $w - $r, $r, $r, 90, 90)
    $yol.CloseFigure()
    $f = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point $n, $n), ([System.Drawing.Color]::FromArgb(255, 255, 77, 109)), ([System.Drawing.Color]::FromArgb(255, 34, 195, 255))
    $karisim = New-Object System.Drawing.Drawing2D.ColorBlend 3
    $karisim.Colors = @([System.Drawing.Color]::FromArgb(255, 255, 77, 109), [System.Drawing.Color]::FromArgb(255, 122, 92, 255), [System.Drawing.Color]::FromArgb(255, 34, 195, 255))
    $karisim.Positions = @(0.0, 0.5, 1.0)
    $f.InterpolationColors = $karisim
    $g.FillPath($f, $yol)

    # Klavye: beyaz gövde ve koyu tuşlar
    $beyaz = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $koyu = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 40, 34, 70))
    $gx = 40 * $k; $gy = 78 * $k; $gw = 176 * $k; $gh = 100 * $k
    $g.FillRectangle($beyaz, $gx, $gy, $gw, $gh)
    $tus = 20 * $k; $ara = 7 * $k
    for ($satir = 0; $satir -lt 3; $satir++) {
        for ($sutun = 0; $sutun -lt 6; $sutun++) {
            $g.FillRectangle($koyu, $gx + 12 * $k + $sutun * ($tus + $ara), $gy + 10 * $k + $satir * ($tus + $ara) * 0.9, $tus, $tus * 0.8)
        }
    }
    $g.FillRectangle($koyu, $gx + 40 * $k, $gy + 78 * $k, 96 * $k, 12 * $k)
    $g.Dispose()
    return $bmp
}

function BmpGirdisi([System.Drawing.Bitmap]$bmp) {
    $n = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $maskeSatir = [int]([math]::Ceiling($n / 32.0) * 4)
    $bw.Write([int]40); $bw.Write([int]$n); $bw.Write([int]($n * 2)); $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]0); $bw.Write([int]($n * $n * 4 + $maskeSatir * $n)); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($y = $n - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $n; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
        }
    }
    $bw.Write((New-Object byte[] ($maskeSatir * $n)))
    $bw.Flush()
    return , $ms.ToArray()
}

$boyutlar = 16, 24, 32, 48, 256
$girdiler = @()
foreach ($n in $boyutlar) {
    $bmp = Ciz $n
    if ($n -eq 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $girdiler += , $ms.ToArray()
    } else {
        $girdiler += , (BmpGirdisi $bmp)
    }
    $bmp.Dispose()
}

New-Item -ItemType Directory -Force (Split-Path $Cikti) | Out-Null
$fs = [System.IO.File]::Create($Cikti)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$boyutlar.Count)
$konum = 6 + 16 * $boyutlar.Count
for ($i = 0; $i -lt $boyutlar.Count; $i++) {
    $n = $boyutlar[$i]
    $bw.Write([byte]($n % 256)); $bw.Write([byte]($n % 256)); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]$girdiler[$i].Length); $bw.Write([int]$konum)
    $konum += $girdiler[$i].Length
}
foreach ($d in $girdiler) { $bw.Write($d) }
$bw.Close()
Write-Output "Simge yazildi: $Cikti"
