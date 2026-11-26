$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$directory = Join-Path $repository 'src\StickyNote\Assets'
New-Item -ItemType Directory -Force $directory | Out-Null
$previews = Join-Path $repository 'TestResults'
New-Item -ItemType Directory -Force $previews | Out-Null

# PATH: original vector artwork, matching Assets/App.svg.
function New-IconDrawing([int]$Size) {
    $bitmap = New-Object Drawing.Bitmap($Size, $Size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.ScaleTransform(($Size / 256.0), ($Size / 256.0))
    $ink = New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#183B4E'))
    $paper = New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#FFD75E'))
    $fold = New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#FFF1B8'))
    $outline = New-Object Drawing.Drawing2D.GraphicsPath
    $page = New-Object Drawing.Drawing2D.GraphicsPath
    $corner = New-Object Drawing.Drawing2D.GraphicsPath
    $pen = New-Object Drawing.Pen($ink, 12)
    try {
        $outline.AddArc(22, 16, 68, 68, 180, 90)
        $outline.AddArc(166, 16, 68, 68, 270, 90)
        $outline.AddArc(166, 172, 68, 68, 0, 90)
        $outline.AddArc(22, 172, 68, 68, 90, 90)
        $outline.CloseFigure()
        $graphics.FillPath($ink, $outline)
        $page.AddLine(58, 30, 164, 30); $page.AddLine(164, 30, 218, 84)
        $page.AddLine(218, 84, 218, 202)
        $page.AddArc(178, 182, 40, 40, 0, 90)
        $page.AddLine(198, 222, 58, 222)
        $page.AddArc(38, 182, 40, 40, 90, 90)
        $page.AddLine(38, 202, 38, 50)
        $page.AddArc(38, 30, 40, 40, 180, 90)
        $page.CloseFigure(); $graphics.FillPath($paper, $page)
        $corner.AddLine(164, 30, 164, 64)
        $corner.AddArc(164, 44, 40, 40, 180, -90)
        $corner.AddLine(184, 84, 218, 84)
        $corner.CloseFigure(); $graphics.FillPath($fold, $corner)
        $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawLine($pen, 70, 118, 180, 118)
        $graphics.DrawLine($pen, 70, 151, 170, 151)
        $graphics.DrawLine($pen, 70, 184, 137, 184)
        return $bitmap
    }
    catch { $bitmap.Dispose(); throw }
    finally {
        $graphics.Dispose(); $ink.Dispose(); $paper.Dispose(); $fold.Dispose()
        $outline.Dispose(); $page.Dispose(); $corner.Dispose(); $pen.Dispose()
    }
}

# ICO: PNG frames supported from Windows Vista onward, x86/x64.
$frames = @()
foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
    $bitmap = New-IconDrawing $size
    $memory = New-Object IO.MemoryStream
    try {
        $bitmap.Save($memory, [Drawing.Imaging.ImageFormat]::Png)
        $frames += [pscustomobject]@{ Size = $size; Bytes = $memory.ToArray() }
        if ($size -eq 256) { $bitmap.Save((Join-Path $previews 'App-icon.png'), [Drawing.Imaging.ImageFormat]::Png) }
    }
    finally { $bitmap.Dispose(); $memory.Dispose() }
}
$path = Join-Path $directory 'App.ico'
$temporary = $path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
$stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew)
$writer = New-Object IO.BinaryWriter($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = [byte]($frame.Size % 256)
        $writer.Write($dimension); $writer.Write($dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    $writer.Flush(); $stream.Flush($true)
}
finally { $writer.Dispose() }
if (Test-Path -LiteralPath $path) { [IO.File]::Replace($temporary, $path, "$path.bak") }
else { [IO.File]::Move($temporary, $path) }
