# Honor 9 — HarmonyOS 2.0.0.150

[Русская версия](README-RU.md)

Dedicated EXE guide: [`EXE-GUIDE-EN.md`](EXE-GUIDE-EN.md).

Automated offline installer for Windows. No internet connection is required
during installation.

## 1. Supported phones

The installer accepts only Honor 9 devices:

- `STF-AL10` — tested on a real phone;
- `STF-AL00`, `STF-TL10`, `STF-L09`, and `STF-L19` — experimental support;
  practical testing is still required.

All variants are converted to the Chinese `STF-AL10 / C00` profile. On
L09/L19, mobile network bands, SIM operation, NFC, and factory calibration may
differ. The installer preserves device-specific NV partitions, but risk cannot
be eliminated without testing each hardware variant.

**Do not use this package on the Huawei P10, Honor 9 Lite, Honor 9X, or any
device whose model code does not begin with STF.**

## 2. Requirements

1. A Windows 10 or Windows 11 x64 computer.
2. A reliable USB cable connected directly to the computer when possible.
3. At least 50% battery charge; 70% is recommended.
4. **USERLOCK already unlocked and FBLOCK disabled.** Fastboot must show
   `USER LockState: UNLOCKED` and `FB LockState: UNLOCKED`. A normal unlock
   code alone is not sufficient. See [`FBLOCK-GUIDE-EN.md`](FBLOCK-GUIDE-EN.md).
5. EMUI 9.x already installed. Upgrade officially to EMUI 9 before continuing
   from EMUI 8.
6. At least 8 GiB of free space on drive `C:`.
7. A backup of photos, documents, chats, 2FA codes, and other personal data.

The phone's internal storage will be completely erased. The microSD card is
not formatted, although removing it provides additional protection.

## 3. Preparing Windows

### Driver

If Windows has not previously detected a Huawei/Honor phone in Fastboot:

1. Install Huawei HiSuite or a standalone Huawei USB/Fastboot driver.
2. Close HiSuite completely after installation, including its tray icon.
3. Do not run other ADB/Fastboot or flashing programs at the same time.

### EXE installer (recommended)

1. Download `Honor9-HarmonyOS2-Downloader.exe` from the latest GitHub Release.
2. Run it and select a folder on an internal drive, such as
   `C:\Honor9Harmony`.
3. Select a PREAS variant and click **“1. Download and prepare”**.
4. The program supports resuming and verifies every GitHub Release file by
   SHA-256 automatically.

Phone checking and flashing are performed in the same window; no CMD files
need to be started manually.

### Installer folder for manual setup

1. Copy the **entire** installer folder to an internal computer drive.
2. Use a short path containing no non-ASCII characters, for example:

   `C:\Honor9Harmony`

3. Do not run files directly from a ZIP/7z/RAR archive.
4. Do not rename or remove:
   - `payload.7z`;
   - `payload.7z.sha256`;
   - `payload-manifest.sha256`;
   - the `tools` folder.

Exact sizes and SHA-256 values are published with every Release. The EXE checks
each GitHub asset digest, the assembled `payload.7z`, and the selected PREAS.

## 4. Entering Fastboot

1. Power the phone off completely.
2. Connect the USB cable to the computer.
3. Hold the **Volume Down** button.
4. While holding it, connect the cable to the phone.
5. Wait for Fastboot/Rescue Mode, then release the button.

Both states must be unlocked: `USER LockState: UNLOCKED` and
`FB LockState: UNLOCKED`. If FBLOCK was disabled through PotatoNV, it does not
need to be disabled again. Do not continue if either state is `LOCKED`.

## 5. Mandatory pre-installation check

1. Leave the phone on the Fastboot screen.
2. Click **“2. Check phone”** in the EXE. For manual setup, double-click
   `check-phone.cmd`.
3. Do not close the window before it reports a result.

Successful result:

`CHECK PASSED. No partitions were changed.`

This check does not flash or erase anything. It verifies:

- exactly one connected phone;
- an allowed STF model;
- unlocked USERLOCK;
- disabled FBLOCK;
- a compatible EMUI/HarmonyOS version.

Fix every reported error before installation. Never bypass a failed check by
editing the script or running Fastboot commands manually.

## 6. Starting installation

1. Close HiSuite, old ADB/Fastboot windows, and other flashing software.
2. Leave the phone in Fastboot.
3. Click **“3. Flash phone”** in the EXE. For manual setup, double-click
   `install.cmd`.
4. The installer checks the phone and package integrity again.
5. Select a PREAS variant:
   - Huawei-signed Chinese PREAS from HarmonyOS 2.0.0.140 — experimental with
     2.0.0.150;
   - modified unsigned `/china` PREAS — tested on STF-AL10.
6. Read the data-erasure warning.
7. Enter this exact confirmation phrase:

   `STF-HARMONY-ERASE`

8. Press Enter and do not disconnect the cable.

Do not press phone buttons, select anything in TWRP, or close the window. The
screen may switch between Fastboot, TWRP, and the Honor logo several times.
This is expected.

## 7. Automated installation stages

Installation usually takes 30–60 minutes:

1. **Archive verification** — approximately 3–10 minutes.
2. **Extraction to a temporary folder** — approximately 5–15 minutes.
3. Temporary TWRP startup.
4. Backup of unique partitions to the computer's `backups` folder.
5. Creation of a device-specific OEMINFO from the connected phone's own
   OEMINFO. Another phone's OEMINFO is never used.
6. HarmonyOS flashing through Fastboot.
7. Formatting internal storage as F2FS.
8. Deployment of initial `/data/hw_init` files.
9. Restoration of stock recovery and eRecovery.
10. Reboot into HarmonyOS.

The large `SYSTEM.img` may remain at `Sending` or `Writing` for several
minutes. Do not interfere unless the window reports `ERROR` or `FAILED`.

## 8. What the installer changes

The installer flashes portable HarmonyOS system partitions, including system,
vendor, product, cust, odm, kernel, vbmeta, modem firmware, recovery, and
PREAS.

The phone's own OEMINFO is backed up first. Only model, region, and version
records are modified; all other records remain from the connected device.

The installer intentionally does **not** flash:

- XLOADER;
- FASTBOOT;
- GPT/partition tables;
- NVME;
- modemnvm_factory or modemnvm_backup;
- persist or secure_storage;
- another phone's IMEI, serial number, or calibration data;
- the microSD card.

The bootloader remains unlocked.

### PREAS selection

| Variant | AVB | Status |
|---|---|---|
| Signed Chinese PREAS from official HarmonyOS 2.0.0.140 | `SHA256_RSA2048`, Huawei key | Experimental with the 2.0.0.150 system |
| Modified minimal PREAS containing `/china` | `NONE` | Tested on STF-AL10 |

The EXE downloads the separate signed image only when the first option is
selected. Its signature only verifies PREAS with a Huawei key and **does not
make bootloader relocking safe**.

## 9. First boot

After the `DONE` message:

1. The phone reboots automatically.
2. The first boot may take 10–15 minutes.
3. Do not power it off for at least 20 minutes while a logo or boot animation
   is still visible.
4. Complete setup, connect Wi-Fi, and test the mobile network, sound, cameras,
   Bluetooth, and NFC.

After booting:

- never relock the bootloader;
- do not install OTA updates over this mixed installation;
- do not use a `dload` package from another model;
- store the `backups` folder safely.

The unsigned option uses AVB algorithm `NONE`; the signed option comes from
release 2.0.0.140. Do not relock the bootloader with either option.

## 10. Backups

A separate folder is created for every phone:

`backups\SERIAL_NUMBER-DATE-TIME`

It contains OEMINFO, NVME, modemnvm, persist, secure_storage, the original
recovery, and checksums.

**Never send this folder to other people or attach it to a public post.** It
contains unique phone data. For diagnostics, share only the relevant file from
`logs` after reviewing it for private information.

Returning to factory firmware requires a complete official package for the
phone's original model and the saved unique partitions. Relocking or a factory
reset alone is not sufficient.

## 11. Common errors

### `Fastboot device did not appear`

- reconnect the cable;
- try a direct USB 2.0 port without a hub;
- reinstall the Huawei Fastboot Driver;
- close HiSuite and other ADB processes;
- enter Fastboot again using Volume Down and USB.

### `Connect exactly one phone`

Disconnect other Android devices and emulators. Exactly one phone must be
connected.

### `Unsupported device`

The reported model is not an allowed STF model. Do not bypass the check.

### `Bootloader is not unlocked`

USERLOCK must be unlocked with the code issued for that exact phone. Never use
another phone's unlock code.

### `FBLOCK is locked` or `Unable to verify FBLOCK state`

Protected partitions and the device-specific OEMINFO require
`FB LockState: UNLOCKED`. A normal USERLOCK unlock is not sufficient. Follow
[`FBLOCK-GUIDE-EN.md`](FBLOCK-GUIDE-EN.md) and do not bypass the check.

### `EMUI 9.x is required`

Install official EMUI 9 for the original phone model first. Do not flash
HarmonyOS directly over EMUI 8.

### `Battery must be at least 50%`

Return to the system, charge the phone, and start again.

### `payload.7z is damaged` or `Hash mismatch`

The package is damaged or incomplete. Stop and obtain a new complete copy.

### The window appears stuck at `Sending`/`Writing`

SYSTEM may take several minutes. Do not disconnect the cable. If no output
appears for 20 minutes, photograph the window and preserve the log.

### `ERROR`/`FAILED` appears

1. Do not relock the bootloader.
2. Do not run random commands or flash OEMINFO downloaded from the internet.
3. Do not delete `backups` or `logs`.
4. Leave the phone in Fastboot if Fastboot is still available.
5. Provide the exact error and corresponding `logs\install-*.log` to a
   specialist.

### The phone has not booted after 20 minutes

1. Hold Power until it switches off.
2. Enter Fastboot using Volume Down and USB.
3. Do not relock the bootloader.
4. Preserve the installer folder and backups for recovery.

## 12. Limitations

- This is an unofficial installer and not a Huawei/Honor product.
- An unlocked bootloader reduces physical device security.
- Banking and payment applications may reject the modified device.
- Full Google Play Services are not included.
- The fingerprint sensor and other hardware functions depend on the condition
  and calibration of the individual board.
- Public distribution requires permission to distribute firmware and
  compliance with the licenses of TWRP, Android Platform Tools, and 7-Zip.

Use this package at your own risk.
