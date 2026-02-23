# Requires -Version 5.1
Add-Type -AssemblyName System.Drawing

$size = 256
$bitmap = New-Object System.Drawing.Bitmap $size, $size
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(10, 118, 225))

$gradientBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    [System.Drawing.Rectangle]::new(0, 0, $size, $size),
    [System.Drawing.Color]::FromArgb(40, 200, 255),
    [System.Drawing.Color]::FromArgb(4, 80, 170),
    [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
)
$graphics.FillEllipse($gradientBrush, 20, 20, $size - 40, $size - 40)

$points = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new($size * 0.45, $size * 0.42),
    [System.Drawing.PointF]::new($size * 0.55, $size * 0.42),
    [System.Drawing.PointF]::new($size * 0.72, $size * 0.78),
    [System.Drawing.PointF]::new($size * 0.62, $size * 0.78),
    [System.Drawing.PointF]::new($size * 0.5, $size * 0.33),
    [System.Drawing.PointF]::new($size * 0.38, $size * 0.78),
    [System.Drawing.PointF]::new($size * 0.28, $size * 0.78)
)
$whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255))
$graphics.FillPolygon($whiteBrush, $points)

$crossRect = [System.Drawing.RectangleF]::new($size * 0.33, $size * 0.55, $size * 0.34, $size * 0.08)
$graphics.FillRectangle($whiteBrush, $crossRect)

$iconPath = Join-Path (Get-Location) 'Aiden.TrayMonitor\Assets\aiden.ico'
if (Test-Path $iconPath) {
    Remove-Item -Force $iconPath
}
$bitmap.Save($iconPath, [System.Drawing.Imaging.ImageFormat]::Icon)
$graphics.Dispose()
$bitmap.Dispose()
