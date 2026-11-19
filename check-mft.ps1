[CmdletBinding()]
param([switch]$Elevated)
$ErrorActionPreference = 'Stop'
$evidence = Join-Path $PSScriptRoot '.validation'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 "/out:$evidence\MftChecks.exe" "$PSScriptRoot\tests\MftChecks.cs"
if ($LASTEXITCODE -ne 0) { throw 'Could not compile MFT checks' }
if ($Elevated) {
    $process = Start-Process -FilePath "$evidence\MftChecks.exe" -ArgumentList ('"' + $PSScriptRoot + '"') -Verb RunAs -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw 'MFT checks failed; see .validation\mft-controlled.log' }
} else {
    & "$evidence\MftChecks.exe" $PSScriptRoot
    if ($LASTEXITCODE -ne 0) { throw 'MFT checks failed; requires administrator and NTFS TEMP' }
}
Get-Content -LiteralPath "$evidence\mft-controlled.log"
