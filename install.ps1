param([switch]$CheckOnly)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Tools = Join-Path $Root "tools"
$Fastboot = Join-Path $Tools "fastboot.exe"
$Adb = Join-Path $Tools "adb.exe"
$SevenZip = Join-Path $Tools "7z.exe"
$PayloadArchive = Join-Path $Root "payload.7z"
$PayloadManifest = Join-Path $Root "payload-manifest.sha256"
$ArchiveHashFile = Join-Path $Root "payload.7z.sha256"
$PreparePayload = Join-Path $Root "prepare-payload.ps1"
$PayloadDir = $null
$LogDir = Join-Path $Root "logs"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$LogPath = Join-Path $LogDir ("install-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

function Log([string]$Message) {
    $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $Message
    Write-Host $line
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
}

function Run-Capture([string]$Program, [string[]]$Arguments, [switch]$AllowFailure) {
    $text = (& $Program @Arguments 2>&1 | ForEach-Object { $_.ToString() }) -join "`n"
    $code = $LASTEXITCODE
    Add-Content -LiteralPath $LogPath -Value ("> {0} {1}`n{2}" -f [IO.Path]::GetFileName($Program), ($Arguments -join " "), $text) -Encoding UTF8
    if ($code -ne 0 -and -not $AllowFailure) { throw "$([IO.Path]::GetFileName($Program)) failed ($code): $text" }
    return $text
}

function Wait-Fastboot([int]$Seconds = 120) {
    $until = (Get-Date).AddSeconds($Seconds)
    do {
        $rows = Run-Capture $Fastboot @("devices") -AllowFailure
        if (($rows -split "`n" | Where-Object { $_ -match "^\S+\s+fastboot" }).Count -eq 1) { return }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $until)
    throw "Fastboot device did not appear"
}

function Wait-Recovery([int]$Seconds = 150) {
    $until = (Get-Date).AddSeconds($Seconds)
    do {
        $state = Run-Capture $Adb @("get-state") -AllowFailure
        if ($state.Trim() -eq "device") {
            $twrp = Run-Capture $Adb @("shell", "getprop", "ro.twrp.version") -AllowFailure
            if ($twrp.Trim()) { return }
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $until)
    throw "TWRP ADB did not appear"
}

function Flash([string]$Partition, [string]$File) {
    $path = Join-Path $PayloadDir $File
    Log "Flashing $Partition"
    [void](Run-Capture $Fastboot @("flash", $Partition, $path))
}

function Verify-Payload([string]$Directory) {
    Log "Verifying extracted payload"
    foreach ($line in Get-Content -LiteralPath $PayloadManifest) {
        if (-not $line.Trim()) { continue }
        if ($line -notmatch "^([0-9a-fA-F]{64})\s+\*(.+)$") { throw "Invalid manifest line: $line" }
        $path = Join-Path $Directory $Matches[2]
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing payload file: $($Matches[2])" }
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($actual -ne $Matches[1]) { throw "Hash mismatch: $($Matches[2])" }
    }
}

try {
    foreach ($required in @($Fastboot, $Adb)) {
        if (-not (Test-Path -LiteralPath $required)) { throw "Missing tool: $required" }
    }

    Wait-Fastboot 20
    $deviceRows = Run-Capture $Fastboot @("devices")
    $devices = @($deviceRows -split "`n" | Where-Object { $_ -match "^\S+\s+fastboot" })
    if ($devices.Count -ne 1) { throw "Connect exactly one phone in Fastboot mode" }
    $serial = ($devices[0] -split "\s+")[0]

    $modelText = Run-Capture $Fastboot @("oem", "get-product-model")
    $modelMatch = [regex]::Match($modelText, "STF-(AL00|AL10|TL10|L09|L19)", "IgnoreCase")
    if (-not $modelMatch.Success) { throw "Unsupported device. Only Honor 9 STF-AL00/AL10/TL10/L09/L19 is accepted" }
    $model = $modelMatch.Value.ToUpperInvariant()

    $bootInfo = Run-Capture $Fastboot @("oem", "get-bootinfo")
    if ($bootInfo -notmatch "(?i)unlocked") { throw "Bootloader is not unlocked" }
    $fblockInfo = Run-Capture $Fastboot @("oem", "backdoor", "info") -AllowFailure
    if ($fblockInfo -match "(?im)FB\s*LockState\s*:\s*LOCKED") {
        throw "FBLOCK is locked. Disable FBLOCK before installation"
    }
    if ($fblockInfo -notmatch "(?im)FB\s*LockState\s*:\s*UNLOCKED") {
        throw "Unable to verify FBLOCK state. Installation requires FB LockState: UNLOCKED"
    }
    $build = Run-Capture $Fastboot @("oem", "get-build-number") -AllowFailure
    if ($build -notmatch "(9\.(0\.1|1\.0)|102\.0\.0)") {
        throw "EMUI 9.x is required before this installer. Reported build: $build"
    }

    Log "Device accepted: $model, serial $serial, USERLOCK and FBLOCK unlocked"
    if ($CheckOnly) {
        Log "CHECK PASSED. No partitions were changed."
        exit 0
    }

    if (-not (Test-Path -LiteralPath $PayloadArchive) -and (Test-Path -LiteralPath $PreparePayload)) {
        Log "Assembling payload from GitHub Release parts"
        & $PreparePayload
    }
    foreach ($required in @($SevenZip, $PayloadArchive, $PayloadManifest, $ArchiveHashFile)) {
        if (-not (Test-Path -LiteralPath $required)) { throw "Missing package file: $required" }
    }
    $expectedArchiveHash = ((Get-Content -LiteralPath $ArchiveHashFile -Raw).Trim() -split "\s+")[0]
    Log "Verifying payload archive"
    $actualArchiveHash = (Get-FileHash -LiteralPath $PayloadArchive -Algorithm SHA256).Hash
    if ($actualArchiveHash -ne $expectedArchiveHash) { throw "payload.7z is damaged" }

    Write-Host ""
    Write-Host "THIS WILL ERASE INTERNAL STORAGE. microSD will not be touched." -ForegroundColor Yellow
    Write-Host "The bootloader must stay unlocked after installation." -ForegroundColor Yellow
    $confirm = Read-Host "Type STF-HARMONY-ERASE to continue"
    if ($confirm -cne "STF-HARMONY-ERASE") { throw "Cancelled by user" }

    $PayloadDir = Join-Path $env:TEMP ("Honor9-HarmonyOS-2.0.0.150-{0}" -f $PID)
    New-Item -ItemType Directory -Force -Path $PayloadDir | Out-Null
    Log "Extracting payload to $PayloadDir"
    [void](Run-Capture $SevenZip @("x", "-y", "-o$PayloadDir", $PayloadArchive))
    Verify-Payload $PayloadDir

    Log "Starting temporary TWRP"
    [void](Run-Capture $Fastboot @("flash", "recovery_ramdisk", (Join-Path $Tools "TWRP-3.2.1.0_Mod_9.img")))
    [void](Run-Capture $Fastboot @("reboot", "recovery"))
    Wait-Recovery

    $batteryText = Run-Capture $Adb @("shell", "cat /sys/class/power_supply/Battery/capacity 2>/dev/null || cat /sys/class/power_supply/battery/capacity")
    $battery = [int]([regex]::Match($batteryText, "\d+").Value)
    if ($battery -lt 50) { throw "Battery must be at least 50%; detected $battery%" }
    Log "TWRP connected; battery $battery%"

    $BackupDir = Join-Path $Root ("backups\{0}-{1}" -f $serial, (Get-Date -Format "yyyyMMdd-HHmmss"))
    New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
    $backupParts = @(
        "oeminfo", "nvme", "secure_storage", "modem_secure", "modemnvm_factory",
        "modemnvm_backup", "modemnvm_system", "modemnvm_img", "modemnvm_update",
        "persist", "fastboot", "kernel", "recovery_ramdisk", "version"
    )
    Log "Backing up unique partitions to $BackupDir"
    foreach ($part in $backupParts) {
        $remote = "/dev/block/bootdevice/by-name/$part"
        [void](Run-Capture $Adb @("pull", $remote, (Join-Path $BackupDir "$part.img")))
    }
    foreach ($boot in @("mmcblk0boot0", "mmcblk0boot1")) {
        [void](Run-Capture $Adb @("pull", "/dev/block/$boot", (Join-Path $BackupDir "$boot.img")))
    }
    Get-ChildItem -LiteralPath $BackupDir -Filter "*.img" | ForEach-Object {
        "{0} *{1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
    } | Set-Content -LiteralPath (Join-Path $BackupDir "SHA256SUMS.txt") -Encoding ASCII

    $patchedOeminfo = Join-Path $BackupDir "oeminfo-harmony-patched.img"
    & (Join-Path $Tools "patch-oeminfo.ps1") -InputPath (Join-Path $BackupDir "oeminfo.img") -OutputPath $patchedOeminfo -OriginalModel $model -AuditPath (Join-Path $BackupDir "oeminfo-patch.json")

    Log "Returning to Fastboot"
    [void](Run-Capture $Adb @("reboot", "bootloader"))
    Wait-Fastboot

    $firmware = @(
        @("dts", "DTS.img"), @("vector", "VECTOR.img"), @("fw_lpm3", "FW_LPM3.img"),
        @("teeos", "TEEOS.img"), @("trustfirmware", "TRUSTFIRMWARE.img"),
        @("sensorhub", "SENSORHUB.img"), @("fw_hifi", "FW_HIFI.img"),
        @("vbmeta", "VBMETA.img"), @("kernel", "KERNEL.img"),
        @("eng_system", "ENG_SYSTEM.img"), @("eng_vendor", "ENG_VENDOR.img"),
        @("system", "SYSTEM.img"), @("vendor", "VENDOR.img"), @("product", "PRODUCT.img"),
        @("cust", "CUST.img"), @("odm", "ODM.img"), @("patch", "PATCH.img"),
        @("prets", "PRETS.img"), @("pretvs", "PRETVS.img"),
        @("isp_firmware", "ISP_FIRMWARE.img"), @("modem_fw", "MODEM_FW.img"),
        @("hisee_img", "HISEE_IMG.img"), @("hisee_fs", "HISEE_FS.img"),
        @("modemnvm_update", "MODEMNVM_UPDATE.img"),
        @("version", "VERSION.img"), @("preload", "PRELOAD.img"),
        @("preas", "PREAS-fixed-sparse.img")
    )
    foreach ($entry in $firmware) { Flash $entry[0] $entry[1] }
    Log "Flashing device-specific patched OEMINFO"
    [void](Run-Capture $Fastboot @("flash", "oeminfo", $patchedOeminfo))

    Log "Starting TWRP for clean userdata setup"
    [void](Run-Capture $Fastboot @("flash", "recovery_ramdisk", (Join-Path $Tools "TWRP-3.2.1.0_Mod_9.img")))
    [void](Run-Capture $Fastboot @("reboot", "recovery"))
    Wait-Recovery

    $formatData = "umount /sdcard 2>/dev/null; umount /data 2>/dev/null; mkfs.f2fs -f /dev/block/bootdevice/by-name/userdata || exit 20; mkdir -p /data; mount -t f2fs /dev/block/bootdevice/by-name/userdata /data || exit 21; mkdir -p /data/media/0"
    [void](Run-Capture $Adb @("shell", $formatData))
    Log "Copying initial HarmonyOS data"
    [void](Run-Capture $Adb @("push", (Join-Path $PayloadDir "hwinit.tar.gz"), "/data/hwinit.tar.gz"))
    $installData = "tar -xzpf /data/hwinit.tar.gz -C / || exit 30; rm -f /data/hwinit.tar.gz; chown -R 0:0 /data/hw_init; find /data/hw_init -type d -exec chmod 0755 {} \;; find /data/hw_init -type f -exec chmod 0644 {} \;; chcon u:object_r:hw_cust_file:s0 /data/hw_init /data/hw_init/dataversion.txt /data/hw_init/reserved_dir.txt || exit 31; chcon -R u:object_r:cust_file:s0 /data/hw_init/product /data/hw_init/preload /data/hw_init/version || exit 32; sync; find /data/hw_init -type f | wc -l"
    $fileCountText = Run-Capture $Adb @("shell", $installData)
    $fileCount = [int]([regex]::Matches($fileCountText, "(?m)^\d+$")[-1].Value)
    if ($fileCount -lt 3000) { throw "Incomplete hw_init extraction: $fileCount files" }
    Log "Installed $fileCount hw_init files"

    $formatCache = "umount /cache 2>/dev/null; SIZE=`$(blockdev --getsize64 /dev/block/bootdevice/by-name/cache); make_ext4fs -l `$SIZE -L cache -a cache /dev/block/bootdevice/by-name/cache || exit 40; sync"
    [void](Run-Capture $Adb @("shell", $formatCache))

    Log "Restoring stock recovery"
    [void](Run-Capture $Adb @("reboot", "bootloader"))
    Wait-Fastboot
    foreach ($entry in @(
        @("recovery_ramdisk", "RECOVERY_RAMDISK.img"),
        @("recovery_vendor", "RECOVERY_VENDOR.img"),
        @("recovery_vbmeta", "RECOVERY_VBMETA.img"),
        @("erecovery_kernel", "ERECOVERY_KERNEL.img"),
        @("erecovery_ramdisk", "ERECOVERY_RAMDIS.img"),
        @("erecovery_vendor", "ERECOVERY_VENDOR.img"),
        @("erecovery_vbmeta", "ERECOVERY_VBMETA.img")
    )) { Flash $entry[0] $entry[1] }

    Log "Installation complete. Rebooting; first boot can take 10-15 minutes."
    [void](Run-Capture $Fastboot @("reboot"))
    Write-Host ""
    Write-Host "DONE. Keep the bootloader unlocked. Backup: $BackupDir" -ForegroundColor Green
}
catch {
    Log "ERROR: $($_.Exception.Message)"
    Write-Host "Installation stopped. Do not relock the bootloader." -ForegroundColor Red
    Write-Host "Log: $LogPath" -ForegroundColor Red
    exit 1
}
finally {
    if ($PayloadDir -and (Test-Path -LiteralPath $PayloadDir)) {
        Remove-Item -LiteralPath $PayloadDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
