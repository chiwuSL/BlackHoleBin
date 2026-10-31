$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' |
    Sort-Object Name |
    Select-Object -ExpandProperty FullName
$manifestFile = Join-Path $projectRoot 'app.manifest'
$distDirectory = Join-Path $projectRoot 'dist'
$outputFile = Join-Path $distDirectory 'BlackHoleBin.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

if (-not (Test-Path -LiteralPath $compiler)) {
    throw '找不到 Windows .NET Framework C# 编译器。请安装 .NET Framework 4.x Developer Pack。'
}

New-Item -ItemType Directory -Force -Path $distDirectory | Out-Null
& (Join-Path $projectRoot 'native\build-native.ps1')

& $compiler `
    /nologo `
    /target:winexe `
    /optimize+ `
    /platform:x64 `
    /win32manifest:$manifestFile `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /out:$outputFile `
    $sourceFiles

if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码：$LASTEXITCODE"
}

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $outputFile
Write-Host "构建完成：$outputFile"
Write-Host "SHA256：$($hash.Hash)"
