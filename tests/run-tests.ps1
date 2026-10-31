param(
    [switch]$IncludeRecycleIntegration
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$sourceDirectory = Join-Path $projectRoot 'src'
$testDirectory = Join-Path $projectRoot 'tests'
$distDirectory = Join-Path $projectRoot 'dist'
$manifestFile = Join-Path $projectRoot 'app.manifest'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

if (-not (Test-Path -LiteralPath $compiler)) {
    throw '找不到 .NET Framework C# 编译器。'
}

$sourceFiles = Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.cs' |
    Sort-Object Name |
    Select-Object -ExpandProperty FullName

New-Item -ItemType Directory -Force -Path $distDirectory | Out-Null

function Invoke-Test {
    param(
        [string]$MainClass,
        [string]$TestFile,
        [string[]]$Arguments
    )

    $testName = [System.IO.Path]::GetFileNameWithoutExtension($TestFile)
    $testExecutable = Join-Path $distDirectory ($testName + '.exe')

    & $compiler `
        /nologo `
        /target:exe `
        /platform:x64 `
        "/win32manifest:$manifestFile" `
        /optimize+ `
        /main:$MainClass `
        /reference:System.dll `
        /reference:System.Core.dll `
        /reference:System.Drawing.dll `
        /reference:System.Windows.Forms.dll `
        /out:$testExecutable `
        $sourceFiles `
        (Join-Path $testDirectory $TestFile)

    if ($LASTEXITCODE -ne 0) {
        throw "$testName 编译失败。"
    }

    & $testExecutable $Arguments
    $exitCode = $LASTEXITCODE
    Remove-Item -LiteralPath $testExecutable -Force

    if ($exitCode -ne 0) {
        throw "$testName 失败，退出码：$exitCode"
    }
}

Invoke-Test -MainClass 'BlackHoleBin.SmokeTest' -TestFile 'SmokeTest.cs'
Invoke-Test -MainClass 'BlackHoleBin.RecoveryRegressionTest' -TestFile 'RecoveryRegressionTest.cs'
Invoke-Test -MainClass 'BlackHoleBin.SecurityRegressionTest' -TestFile 'SecurityRegressionTest.cs'
Invoke-Test `
    -MainClass 'BlackHoleBin.VisualSnapshotTest' `
    -TestFile 'VisualSnapshotTest.cs' `
    -Arguments @(
        (Join-Path $projectRoot 'ui-preview-v0.4.png'),
        (Join-Path $projectRoot 'settings-preview-v0.4.png'),
        (Join-Path $projectRoot 'settings-safety-preview-v0.4.png'),
        (Join-Path $projectRoot 'settings-english-preview-v0.4.png'),
        (Join-Path $projectRoot 'settings-recovery-preview-v0.4.png'),
        (Join-Path $projectRoot 'settings-appearance-preview-v0.4.png')
    )

if ($IncludeRecycleIntegration) {
    Invoke-Test -MainClass 'BlackHoleBin.CombinedFlowTest' -TestFile 'CombinedFlowTest.cs'
}

Write-Host '全部测试通过。'
