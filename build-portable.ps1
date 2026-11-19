[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    & $env:ComSpec /d /c "`"$projectRoot\build.cmd`""
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    $exe = Join-Path $projectRoot 'SpaceScan.exe'
    $bytes = [IO.File]::ReadAllBytes($exe)
    $pe = [BitConverter]::ToInt32($bytes, 0x3c)
    if ([BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664) { throw 'Expected Windows AMD64 executable' }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
    $name = "SpaceScan-personal-$version-win-x64"
    $output = Join-Path $projectRoot "dist\$name"
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $files = @('SpaceScan.exe', 'LICENSE', 'NOTICE.txt')
    foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination (Join-Path $output $file) -Force }
    Copy-Item -LiteralPath "$projectRoot\docs\FORK-README.md" -Destination "$output\README.md" -Force
    Copy-Item -LiteralPath "$projectRoot\docs\WINDOWS-SHELL.md" -Destination "$output\WINDOWS-SHELL.md" -Force
    Copy-Item -LiteralPath "$projectRoot\docs\VALIDATION.md" -Destination "$output\VALIDATION.md" -Force
    $revision = 'unknown'
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $revision = (& git rev-parse HEAD) -join ''
        if ((& git status --porcelain | Measure-Object).Count -gt 0) { $revision += ' (working tree has changes)' }
    }
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    @("SpaceScan personal $version", 'Architecture: Windows x64 / AMD64', "Source: $revision", "Built UTC: $([DateTime]::UtcNow.ToString('o'))", "SpaceScan.exe SHA256: $hash", 'Original source: https://github.com/limburatorul/spacescan', 'MIT; see LICENSE and NOTICE.txt') | Set-Content -Encoding utf8 -LiteralPath "$output\BUILD-INFO.txt"
    $packageFiles = @($files | ForEach-Object { Join-Path $output $_ }) + @("$output\README.md", "$output\WINDOWS-SHELL.md", "$output\VALIDATION.md", "$output\BUILD-INFO.txt")
    $archive = Join-Path $projectRoot "dist\$name.zip"
    Compress-Archive -LiteralPath $packageFiles -DestinationPath $archive -Force
    (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash + '  ' + [IO.Path]::GetFileName($archive) | Set-Content -Encoding ascii -LiteralPath ($archive + '.sha256')
    Write-Output "Portable: $archive"
} finally { Pop-Location }
