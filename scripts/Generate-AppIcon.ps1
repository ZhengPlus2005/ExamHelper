[CmdletBinding()]
param([string]$OutputDirectory)
if(-not $OutputDirectory){ $OutputDirectory = Join-Path $PSScriptRoot '../src/ExamHelper.App/Assets' }
$ErrorActionPreference = 'Stop'
if([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA'){ throw 'Use powershell.exe -STA -File scripts/Generate-AppIcon.ps1' }
Add-Type -AssemblyName PresentationCore,WindowsBase
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
[xml]$taskSvg = [IO.File]::ReadAllText((Join-Path $taskOutput 'app-icon.svg'))
$taskGeometry = [Windows.Media.Geometry]::Parse('F0 ' + $taskSvg.svg.path.d)
$taskInk = [Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromRgb(43,43,43))
$taskImages = @()
foreach($taskSize in @(16,24,32,48,64,128,256)){
 $taskVisual = [Windows.Media.DrawingVisual]::new()
 $taskContext = $taskVisual.RenderOpen()
 $taskContext.PushTransform([Windows.Media.ScaleTransform]::new($taskSize/256.0,$taskSize/256.0))
 $taskContext.DrawRoundedRectangle([Windows.Media.Brushes]::White,$null,[Windows.Rect]::new(8,8,240,240),48,48)
 $taskContext.DrawGeometry($taskInk,$null,$taskGeometry)
 $taskContext.Pop(); $taskContext.Close()
 $taskBitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($taskSize,$taskSize,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $taskBitmap.Render($taskVisual)
 $taskEncoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
 $taskEncoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($taskBitmap))
 $taskStream = [IO.MemoryStream]::new()
 try { $taskEncoder.Save($taskStream); $taskImages += @{ Size=$taskSize; Bytes=$taskStream.ToArray() } } finally { $taskStream.Dispose() }
}
[IO.File]::WriteAllBytes((Join-Path $taskOutput 'app-icon.png'),$taskImages[-1].Bytes)
$taskFile = [IO.File]::Create((Join-Path $taskOutput 'app-icon.ico'))
$taskWriter = [IO.BinaryWriter]::new($taskFile)
try {
 $taskWriter.Write([uint16]0);$taskWriter.Write([uint16]1);$taskWriter.Write([uint16]$taskImages.Count)
 $taskOffset = 6 + 16 * $taskImages.Count
 foreach($taskItem in $taskImages){
  $taskDimension = if($taskItem.Size -eq 256){0}else{$taskItem.Size}
  $taskWriter.Write([byte]$taskDimension);$taskWriter.Write([byte]$taskDimension);$taskWriter.Write([byte]0);$taskWriter.Write([byte]0)
  $taskWriter.Write([uint16]1);$taskWriter.Write([uint16]32);$taskWriter.Write([uint32]$taskItem.Bytes.Length);$taskWriter.Write([uint32]$taskOffset)
  $taskOffset += $taskItem.Bytes.Length
 }
 foreach($taskItem in $taskImages){$taskWriter.Write([byte[]]$taskItem.Bytes)}
} finally { $taskWriter.Dispose();$taskFile.Dispose() }
Write-Output ('Generated 7 icon sizes at '+$taskOutput)
