[CmdletBinding()]
param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.4.0',[string]$IsccPath,[string]$PublishDirectory,[string]$OutputDirectory)
. (Join-Path $PSScriptRoot 'Common.ps1')
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $IsccPath) {
    $taskCompiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($taskCompiler) { $IsccPath = $taskCompiler.Source }
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw '未找到 Inno Setup 6 编译器。请从 https://jrsoftware.org/isdl.php 安装，并传入 -IsccPath 指向 ISCC.exe。'
}
if (-not $PublishDirectory) { $PublishDirectory = Join-Path $taskRoot 'artifacts/publish/win-x64' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskRoot 'artifacts/installers' }
$taskPublish = [IO.Path]::GetFullPath($PublishDirectory)
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $taskPublish 'ExamHelper.App.exe'))) { throw '发布目录中没有应用 EXE，请先运行 Publish.ps1。' }
foreach ($taskForbidden in @('ExamHelper.InteractionLab.dll','settings.json','history.db','TempAudio','Images')) {
    if (Test-Path -LiteralPath (Join-Path $taskPublish $taskForbidden)) { throw "发布目录含不应打包的文件或目录：$taskForbidden" }
}
$taskInfo = Get-Content -LiteralPath (Join-Path $taskPublish 'build-info.json') -Raw | ConvertFrom-Json
if ($taskInfo.Version -ne $Version) { throw '发布版本与安装器版本不一致，请重新发布。' }
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
& $IsccPath "/DAppVersion=$Version" "/DPublishDir=$taskPublish" "/DInstallerOutput=$taskOutput" (Join-Path $taskRoot 'packaging/ExamHelper.iss')
Assert-ExamHelperExit '安装器编译'
$taskInstaller = Join-Path $taskOutput "ExamHelper-Setup-$Version-x64.exe"
if (-not (Test-Path -LiteralPath $taskInstaller)) { throw '编译器未生成预期安装器。' }
$taskHash = (Get-FileHash -LiteralPath $taskInstaller -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($taskInstaller + '.sha256'),"$taskHash  $([IO.Path]::GetFileName($taskInstaller))" + [Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output "安装包：$taskInstaller"
Write-Output "SHA-256：$taskHash"
