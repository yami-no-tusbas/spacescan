[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BaselineExe)
$ErrorActionPreference = 'Stop'
$framework = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force -Path "$PSScriptRoot\.validation" | Out-Null
& "$framework\csc.exe" /nologo /target:exe /platform:x64 /codepage:65001 "/lib:$framework\WPF" /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll "/out:$PSScriptRoot\.validation\PerfChecks.exe" "$PSScriptRoot\tests\PerfChecks.cs"
if ($LASTEXITCODE -ne 0) { throw 'Could not compile benchmark' }
foreach ($exe in @($BaselineExe, "$PSScriptRoot\SpaceScan.exe")) {
    & "$PSScriptRoot\.validation\PerfChecks.exe" $exe
    if ($LASTEXITCODE -ne 0) { throw "Benchmark failed for $exe" }
}
