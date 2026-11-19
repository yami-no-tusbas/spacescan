[CmdletBinding()]
param([switch]$Shell, [switch]$Elevated, [switch]$Engine)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$evidence = Join-Path $projectRoot '.validation'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$framework = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
function Compile-Check([string]$name) {
    & "$framework\csc.exe" /nologo /target:exe /platform:x64 /codepage:65001 "/lib:$framework\WPF" /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll "/out:$evidence\$name.exe" "$projectRoot\tests\$name.cs"
    if ($LASTEXITCODE -ne 0) { throw "Could not compile $name" }
}
Compile-Check ForkChecks
& "$evidence\ForkChecks.exe" $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Fork checks failed' }
if ($Engine -and (-not $Shell -or -not $Elevated)) { throw '-Engine requires -Shell -Elevated' }
if ($Shell) {
    $fixture = Join-Path $evidence 'fixture'
    New-Item -ItemType Directory -Force -Path "$fixture\FolderA", "$fixture\FolderB" | Out-Null
    [IO.File]::WriteAllText("$fixture\FolderA\sample.txt", 'Shell context menu test')
    [IO.File]::WriteAllText("$fixture\FolderA\second.txt", 'Second test')
    [IO.File]::WriteAllText("$fixture\FolderB\copy.txt", 'Shell context menu test')
    Compile-Check ShellChecks
    if ($Elevated) {
        $arguments = '"' + $projectRoot + '"' + $(if ($Engine) { ' --with-engine' } else { '' })
        $process = Start-Process -FilePath "$evidence\ShellChecks.exe" -ArgumentList $arguments -Verb RunAs -WindowStyle Hidden -PassThru
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw 'Elevated checks failed; see .validation\shell-admin.log and engine-admin.log' }
    } else {
        & "$evidence\ShellChecks.exe" $projectRoot
        if ($LASTEXITCODE -ne 0) { throw 'Shell checks failed' }
    }
}
