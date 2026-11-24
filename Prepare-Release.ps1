[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    & .\Build.bat
    if ($LASTEXITCODE -ne 0) { throw "Build.bat failed (exit $LASTEXITCODE)." }

    & .\tests\Run.bat test-results
    if ($LASTEXITCODE -ne 0) { throw "Regression tests failed (exit $LASTEXITCODE)." }

    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $PSScriptRoot 'OzelCrosshair.exe')).ProductVersion
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid EXE product version: $version" }

    $dist = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'dist'))
    New-Item -ItemType Directory -Path $dist -Force | Out-Null
    $stage = [IO.Path]::GetFullPath((Join-Path $dist ('.package-' + [Guid]::NewGuid().ToString('N'))))
    if (!(($stage).StartsWith($dist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) {
        throw 'Packaging folder must stay inside dist.'
    }
    $files = @('OzelCrosshair.exe', 'README.md', 'README.tr.md', 'LICENSE', 'CHANGELOG.md')
    try {
        New-Item -ItemType Directory -Path $stage | Out-Null
        foreach ($file in $files) {
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $stage
        }
        $zip = Join-Path $dist "Starlit-Crosshair-$version-windows.zip"
        Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($zip)
        try {
            $actual = @($archive.Entries | ForEach-Object { $_.FullName })
            if (Compare-Object ($files | Sort-Object) ($actual | Sort-Object)) {
                throw 'Portable ZIP contents do not match the required file list.'
            }
        }
        finally { $archive.Dispose() }

        $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        $sums = Join-Path $dist 'SHA256SUMS.txt'
        [IO.File]::WriteAllText($sums, "$hash  $([IO.Path]::GetFileName($zip))`n", [Text.Encoding]::ASCII)
        Write-Output "Created $zip"
        Write-Output "SHA256 $hash"
    }
    finally {
        if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    }
}
finally { Pop-Location }
