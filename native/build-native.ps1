$ErrorActionPreference='Stop'
$root=Split-Path -Parent $MyInvocation.MyCommand.Path
& 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\fxc.exe' /nologo /T ps_5_0 /E PS /Fo (Join-Path $root 'shader-test.cso') (Join-Path $root 'singularity.hlsl')
if($LASTEXITCODE -ne 0){throw 'HLSL compilation failed'}
$shader=Get-Content -LiteralPath (Join-Path $root 'singularity.hlsl') -Raw
# Build-generated header; source of truth is the HLSL file.
[IO.File]::WriteAllText((Join-Path $root 'shader.h'), 'static const char shaderSource[] = R"HLSL(' + $shader + ')HLSL";', [Text.Encoding]::ASCII)
Push-Location $root
try {
    & cmd.exe /d /s /c 'call "C:\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul && rc /nologo /fo version.res version.rc && cl /nologo /LD /O2 /MT /EHsc /guard:cf renderer.cpp /link /DYNAMICBASE /NXCOMPAT /guard:cf /OUT:..\dist\BlackHoleGpu.dll version.res d3d11.lib d3dcompiler.lib'
    if($LASTEXITCODE -ne 0){throw 'GPU build failed'}
} finally {Pop-Location}
