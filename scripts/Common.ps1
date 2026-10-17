Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Resolve-ExamHelperDotnet {
    param([string]$DotnetPath)
    if ($DotnetPath) {
        if (-not (Test-Path -LiteralPath $DotnetPath -PathType Leaf)) { throw '指定的 .NET SDK 路径不存在。请安装 .NET 10 x64 SDK 或传入 -DotnetPath。' }
        return (Resolve-Path -LiteralPath $DotnetPath).Path
    }
    $taskCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($taskCommand) { return $taskCommand.Source }
    $taskLocal = Join-Path (Split-Path -Parent $PSScriptRoot) '.tools/dotnet/dotnet.exe'
    if (Test-Path -LiteralPath $taskLocal) { return $taskLocal }
    throw '未找到 .NET 10 SDK。请从 https://dotnet.microsoft.com/download/dotnet/10.0 安装或传入 -DotnetPath。'
}
function Assert-ExamHelperExit {
    param([string]$Action)
    if ($LASTEXITCODE -ne 0) { throw "$Action 失败，退出码 $LASTEXITCODE。" }
}
