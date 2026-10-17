[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release',[string]$DotnetPath)
. (Join-Path $PSScriptRoot 'Common.ps1')
$taskDotnet = Resolve-ExamHelperDotnet $DotnetPath
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskResults = Join-Path $taskRoot ('artifacts/test-results/' + [Guid]::NewGuid().ToString('N'))
Push-Location $taskRoot
try {
    & $taskDotnet restore ExamHelper.slnx --locked-mode
    Assert-ExamHelperExit '锁定依赖恢复'
    & $taskDotnet build ExamHelper.slnx -c $Configuration --no-restore
    Assert-ExamHelperExit '构建'
    & $taskDotnet test ExamHelper.slnx -c $Configuration --no-build --no-restore --logger trx --results-directory $taskResults
    Assert-ExamHelperExit '测试'
    $taskFiles = @(Get-ChildItem -LiteralPath $taskResults -Filter '*.trx' -File)
    $taskTotal = 0
    foreach ($taskFile in $taskFiles) {
        [xml]$taskXml = Get-Content -LiteralPath $taskFile.FullName -Raw
        $taskTotal += [int]$taskXml.TestRun.ResultSummary.Counters.total
    }
    if ($taskTotal -le 0) { throw '没有发现任何测试，不能报告通过。' }
    Write-Output "测试总数：$taskTotal；结果：$taskResults"
}
finally { Pop-Location }
