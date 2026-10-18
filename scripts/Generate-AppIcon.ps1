[CmdletBinding()]
param([string]$OutputDirectory,[string]$SourceImage)
if(-not $OutputDirectory){ $OutputDirectory = Join-Path $PSScriptRoot '../src/ExamHelper.App/Assets' }
$ErrorActionPreference = 'Stop'
if([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA'){ throw 'Use powershell.exe -STA -File scripts/Generate-AppIcon.ps1' }
Add-Type -AssemblyName PresentationCore,WindowsBase
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
if(-not $SourceImage){ $SourceImage = Join-Path $PSScriptRoot '../src/ExamHelper.App/Assets/app-icon-source.jpg' }
$taskSource = [Windows.Media.Imaging.BitmapImage]::new()
$taskSource.BeginInit(); $taskSource.CacheOption = [Windows.Media.Imaging.BitmapCacheOption]::OnLoad
$taskSource.UriSource = [Uri]::new([IO.Path]::GetFullPath($SourceImage)); $taskSource.EndInit(); $taskSource.Freeze()
$taskImages = @()
foreach($taskSize in @(16,24,32,48,64,128,256)){
 $taskVisual = [Windows.Media.DrawingVisual]::new()
 $taskContext = $taskVisual.RenderOpen()
 [Windows.Media.RenderOptions]::SetBitmapScalingMode($taskVisual,[Windows.Media.BitmapScalingMode]::HighQuality)
 $taskScale = [Math]::Min($taskSize / [double]$taskSource.PixelWidth,$taskSize / [double]$taskSource.PixelHeight)
 $taskWidth = $taskSource.PixelWidth * $taskScale; $taskHeight = $taskSource.PixelHeight * $taskScale
 $taskBounds = [Windows.Rect]::new(0,0,$taskSize,$taskSize)
 $taskRadius = $taskSize * 0.18
 $taskContext.PushClip([Windows.Media.RectangleGeometry]::new($taskBounds,$taskRadius,$taskRadius))
 $taskContext.DrawRectangle([Windows.Media.Brushes]::White,$null,$taskBounds)
 $taskContext.DrawImage($taskSource,[Windows.Rect]::new(($taskSize-$taskWidth)/2,($taskSize-$taskHeight)/2,$taskWidth,$taskHeight))
 $taskContext.Pop()
 $taskContext.Close()
 $taskBitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($taskSize,$taskSize,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $taskBitmap.Render($taskVisual)
 $taskPixels = [byte[]]::new($taskSize*$taskSize*4)
 $taskBitmap.CopyPixels($taskPixels,$taskSize*4,0)
 foreach($taskCorner in @(0,($taskSize-1),(($taskSize-1)*$taskSize),($taskSize*$taskSize-1))){
  for($taskChannel=0;$taskChannel -lt 4;$taskChannel++){ $taskPixels[$taskCorner*4+$taskChannel]=0 }
 }
 $taskBitmap = [Windows.Media.Imaging.BitmapSource]::Create($taskSize,$taskSize,96,96,[Windows.Media.PixelFormats]::Pbgra32,$null,$taskPixels,$taskSize*4)

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
