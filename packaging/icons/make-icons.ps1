# Renders RelicWriter/Assets/Icon.svg into every icon the app ships with, using headless Chrome (or Edge) for the rendering.
#   RelicWriter/Assets/Icon.ico               Windows icon (16 to 256 px) for the window and the exe
#   RelicWriter/Assets/Icon.png               256 px, for Avalonia code that wants a bitmap
#   packaging/flatpak/icons/hicolor/...       the Linux sizes (16 to 512 px, plus the scalable SVG)
# Sizes up to 32 px are rendered without the fine detail (elements with class "detail"), which turns to mush that small.
# Run from anywhere: powershell -File packaging/icons/make-icons.ps1
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$svg = Join-Path $root 'RelicWriter\Assets\Icon.svg'
$appId = 'io.github.hendy643.RelicWriter'
$browser = @("$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw 'Chrome or Edge is needed to render the SVG.' }

$work = Join-Path ([IO.Path]::GetTempPath()) 'relicwriter-icons'
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item $work -ItemType Directory | Out-Null
$markup = Get-Content $svg -Raw

function Render([int]$size, [string]$path) {
    $simple = $size -le 32
    $svgText = if ($simple) { $markup -replace '</defs>', '<style>.detail{display:none}</style></defs>' } else { $markup }
    $page = Join-Path $work "icon-$size.html"
    $svgFile = Join-Path $work "icon-$size.svg"
    Set-Content $svgFile $svgText -Encoding UTF8
    Set-Content $page "<html><body style='margin:0;background:transparent'><img src='file:///$($svgFile.Replace('\','/'))' width='$size' height='$size' style='display:block'></body></html>" -Encoding UTF8
    New-Item (Split-Path $path) -ItemType Directory -Force | Out-Null
    $browserArgs = @('--headless', '--disable-gpu', '--hide-scrollbars', '--default-background-color=00000000', '--force-device-scale-factor=1', "--screenshot=$path", "--window-size=$size,$size", "file:///$($page.Replace([string][char]92,'/'))")
    Start-Process $browser -ArgumentList $browserArgs -Wait -NoNewWindow
    if (-not (Test-Path $path)) { throw "Rendering $size px failed." }
}

# Linux: the sizes AppStream and the icon themes accept, and the scalable original.
$hicolor = Join-Path $root "packaging\flatpak\icons\hicolor"
Remove-Item $hicolor -Recurse -Force -ErrorAction SilentlyContinue
foreach ($size in 16, 32, 48, 64, 128, 256, 512) { Render $size (Join-Path $hicolor "${size}x${size}\apps\$appId.png") }
New-Item (Join-Path $hicolor 'scalable\apps') -ItemType Directory -Force | Out-Null
Copy-Item $svg (Join-Path $hicolor "scalable\apps\$appId.svg") -Force

# Avalonia: a bitmap, and the Windows icon with PNG-compressed images of every size.
Copy-Item (Join-Path $hicolor "256x256\apps\$appId.png") (Join-Path $root 'RelicWriter\Assets\Icon.png') -Force
$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($size in $sizes) {
    $png = Join-Path $work "ico-$size.png"
    Render $size $png
    , @($size, [IO.File]::ReadAllBytes($png))
}
$ms = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $ms
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $size = $image[0]; $bytes = $image[1]
    $w.Write([byte]($size % 256)); $w.Write([byte]($size % 256))   # 256 is stored as 0
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$bytes.Length); $w.Write([uint32]$offset)
    $offset += $bytes.Length
}
foreach ($image in $images) { $w.Write([byte[]]$image[1]) }
[IO.File]::WriteAllBytes((Join-Path $root 'RelicWriter\Assets\Icon.ico'), $ms.ToArray())
Remove-Item $work -Recurse -Force
Write-Host 'Icons written.'
