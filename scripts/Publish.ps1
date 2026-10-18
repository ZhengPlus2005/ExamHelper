[CmdletBinding()]
param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.4.0',[string]$DotnetPath,[string]$OutputDirectory)
. (Join-Path $PSScriptRoot 'Common.ps1')
$taskDotnet = Resolve-ExamHelperDotnet $DotnetPath
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskRoot 'artifacts/publish/win-x64' }
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
Push-Location $taskRoot
try {
    & $taskDotnet restore ExamHelper.slnx --locked-mode
    Assert-ExamHelperExit '锁定依赖恢复'
    & $taskDotnet publish src/ExamHelper.App -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=false -p:PublishTrimmed=false "-p:Version=$Version" -o $taskOutput
    Assert-ExamHelperExit '自包含发布'
    Copy-Item -LiteralPath (Join-Path $taskRoot 'THIRD-PARTY-NOTICES.md') -Destination $taskOutput
    $taskLicenses = Join-Path $taskOutput 'licenses'
    [IO.Directory]::CreateDirectory($taskLicenses) | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $taskRoot 'packaging/licenses') -File | Copy-Item -Destination $taskLicenses -Force
    $taskSdk = (& $taskDotnet --version | Out-String).Trim()
    $taskSha = (& git rev-parse HEAD | Out-String).Trim()
    Assert-ExamHelperExit '读取 Git 提交'
    $taskDirty = -not [string]::IsNullOrWhiteSpace((& git status --porcelain | Out-String))
    $taskLocks = @(Get-ChildItem src -Recurse -Filter 'packages.lock.json' -File | ForEach-Object {
        @{ Path = $_.FullName.Substring($taskRoot.Length + 1); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $taskInfo = @{ Version = $Version; GitCommit = $taskSha; Dirty = $taskDirty; SDK = $taskSdk; RuntimeIdentifier = 'win-x64'; BuiltAtUtc = [DateTimeOffset]::UtcNow.ToString('O'); LockFiles = $taskLocks }
    [IO.File]::WriteAllText((Join-Path $taskOutput 'build-info.json'),($taskInfo | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    if (-not (Test-Path -LiteralPath (Join-Path $taskOutput 'ExamHelper.App.exe'))) { throw '发布未生成应用 EXE。' }
    Write-Output "发布目录：$taskOutput"
}
finally { Pop-Location }
