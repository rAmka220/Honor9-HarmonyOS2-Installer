param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$OriginalModel = "unknown",
    [string]$AuditPath
)

$ErrorActionPreference = "Stop"
$bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $InputPath))
if ($bytes.Length -ne 67108864) { throw "Unexpected OEMINFO size: $($bytes.Length)" }

function Read-U32([byte[]]$Buffer, [int]$Offset) {
    return [BitConverter]::ToUInt32($Buffer, $Offset)
}

function Write-U32([byte[]]$Buffer, [int]$Offset, [uint32]$Value) {
    $encoded = [BitConverter]::GetBytes($Value)
    [Array]::Copy($encoded, 0, $Buffer, $Offset, 4)
}

function Test-Magic([byte[]]$Buffer, [int]$Offset) {
    $magic = [Text.Encoding]::ASCII.GetString($Buffer, $Offset, 8)
    return $magic -eq "OEM_INFO"
}

$changes = [Collections.Generic.List[object]]::new()

function Set-OemRecord {
    param(
        [int]$Item,
        [string]$Value,
        [ValidateSet("Variable", "Existing", "Fixed")][string]$LengthMode = "Variable",
        [int]$FixedLength = 0,
        [byte]$Padding = 0xFF
    )

    $valueBytes = [Text.Encoding]::ASCII.GetBytes($Value)
    $patched = 0
    foreach ($region in @(@{ Name = "primary"; Base = 0 }, @{ Name = "backup"; Base = 0x2000000 })) {
        $offset = [int]($region.Base + (($Item - 1) * 0x1000))
        if (-not (Test-Magic $bytes $offset)) { continue }
        if ((Read-U32 $bytes ($offset + 12)) -ne $Item) { throw "OEMINFO item mismatch at 0x$($offset.ToString('X'))" }
        if ((Read-U32 $bytes ($offset + 16)) -ne 1) { continue }

        $oldLength = [int](Read-U32 $bytes ($offset + 20))
        $newLength = switch ($LengthMode) {
            "Variable" { $valueBytes.Length }
            "Existing" { $oldLength }
            "Fixed" { $FixedLength }
        }
        if ($newLength -lt $valueBytes.Length -or $newLength -gt 0xE00) {
            throw "Invalid length for OEMINFO item $Item"
        }

        Write-U32 $bytes ($offset + 20) ([uint32]$newLength)
        for ($i = 0; $i -lt $newLength; $i++) { $bytes[$offset + 0x200 + $i] = $Padding }
        [Array]::Copy($valueBytes, 0, $bytes, $offset + 0x200, $valueBytes.Length)
        $changes.Add([pscustomobject]@{
            item = $Item
            copy = $region.Name
            offset = ('0x{0:x}' -f $offset)
            oldLength = $oldLength
            newLength = $newLength
            value = $Value
        })
        $patched++
    }
    if ($patched -eq 0) { throw "OEMINFO item $Item was not found" }
}

# Rebrand only active configuration records. Device-unique records are untouched.
Set-OemRecord -Item 18  -Value "all/cn" -LengthMode Variable
Set-OemRecord -Item 91  -Value "STF-AL10" -LengthMode Existing -Padding 0
Set-OemRecord -Item 97  -Value "STF-AL10" -LengthMode Variable
Set-OemRecord -Item 78  -Value "STF-AL10 102.0.0.150(C00E150R1P3)" -LengthMode Fixed -FixedLength 128 -Padding 0
Set-OemRecord -Item 180 -Value "STF-AL10-CUST 102.0.0.150(C00)" -LengthMode Variable
Set-OemRecord -Item 182 -Value "STF-AL10-PRELOAD 102.0.0.3(C00R1)" -LengthMode Variable
Set-OemRecord -Item 186 -Value "STF-LGRP1-CHN 102.0.0.150" -LengthMode Variable

[IO.File]::WriteAllBytes($OutputPath, $bytes)
$hash = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($AuditPath) {
    [pscustomobject]@{
        sourceModel = $OriginalModel
        input = [IO.Path]::GetFileName($InputPath)
        output = [IO.Path]::GetFileName($OutputPath)
        outputSha256 = $hash
        changedRecords = $changes
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $AuditPath -Encoding UTF8
}
Write-Host "Patched OEMINFO SHA-256: $hash"
