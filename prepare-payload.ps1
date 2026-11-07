param([switch]$KeepParts)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Output = Join-Path $Root "payload.7z"
$Temporary = Join-Path $Root "payload.7z.tmp"
$PartsManifest = Join-Path $Root "payload.parts.sha256"
$OutputManifest = Join-Path $Root "payload.7z.sha256"

function Read-HashManifest([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing file: $Path" }
    $items = foreach ($line in Get-Content -LiteralPath $Path) {
        if (-not $line.Trim()) { continue }
        if ($line -notmatch "^([0-9a-fA-F]{64})\s+\*(.+)$") {
            throw "Invalid manifest line: $line"
        }
        [pscustomobject]@{ Hash = $Matches[1].ToLowerInvariant(); Name = $Matches[2] }
    }
    return @($items)
}

function Test-Hash([string]$Path, [string]$Expected) {
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Expected) {
        throw "SHA-256 mismatch: $([IO.Path]::GetFileName($Path))"
    }
}

try {
    $outputEntry = @(Read-HashManifest $OutputManifest)
    if ($outputEntry.Count -ne 1 -or $outputEntry[0].Name -ne "payload.7z") {
        throw "Invalid payload.7z.sha256"
    }

    if (Test-Path -LiteralPath $Output) {
        Write-Host "Checking existing payload.7z..."
        Test-Hash $Output $outputEntry[0].Hash
        Write-Host "PAYLOAD READY: $Output" -ForegroundColor Green
        return
    }

    $parts = @(Read-HashManifest $PartsManifest | Sort-Object Name)
    if ($parts.Count -lt 2) { throw "At least two payload parts are required" }
    foreach ($part in $parts) {
        if ($part.Name -notmatch "^payload\.7z\.part\d{2}$") {
            throw "Unexpected part name: $($part.Name)"
        }
        $path = Join-Path $Root $part.Name
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing release asset: $($part.Name)" }
        Write-Host "Checking $($part.Name)..."
        Test-Hash $path $part.Hash
    }

    Remove-Item -LiteralPath $Temporary -Force -ErrorAction SilentlyContinue
    $target = [IO.File]::Open($Temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        foreach ($part in $parts) {
            Write-Host "Adding $($part.Name)..."
            $source = [IO.File]::OpenRead((Join-Path $Root $part.Name))
            try { $source.CopyTo($target, 16777216) } finally { $source.Dispose() }
        }
    } finally {
        $target.Dispose()
    }

    Write-Host "Checking assembled payload.7z..."
    Test-Hash $Temporary $outputEntry[0].Hash
    Move-Item -LiteralPath $Temporary -Destination $Output

    if (-not $KeepParts) {
        foreach ($part in $parts) {
            Remove-Item -LiteralPath (Join-Path $Root $part.Name) -Force
        }
    }
    Write-Host "PAYLOAD READY: $Output" -ForegroundColor Green
}
catch {
    Remove-Item -LiteralPath $Temporary -Force -ErrorAction SilentlyContinue
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    throw
}
