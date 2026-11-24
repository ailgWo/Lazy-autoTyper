$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $scriptDir
$source = Join-Path $root 'src\LazyTyper.cs'
$assemblyInfo = Join-Path $root 'src\AssemblyInfo.cs'
$outDir = Join-Path $root 'dist'
$outFile = Join-Path $outDir 'LazyTyper.exe'

if (-not (Test-Path -LiteralPath $source)) {
    throw "Source file not found: $source"
}
if (-not (Test-Path -LiteralPath $assemblyInfo)) {
    throw "AssemblyInfo file not found: $assemblyInfo"
}

$candidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $null
foreach ($candidate in $candidates) {
    if (Test-Path -LiteralPath $candidate) {
        $csc = $candidate
        break
    }
}
if (-not $csc) {
    throw 'Cannot find the .NET Framework C# compiler (csc.exe).'
}

New-Item -ItemType Directory -Path $outDir -Force | Out-Null

& $csc /nologo /target:winexe /platform:anycpu /optimize+ `
    "/out:$outFile" `
    $source $assemblyInfo `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built $outFile"
