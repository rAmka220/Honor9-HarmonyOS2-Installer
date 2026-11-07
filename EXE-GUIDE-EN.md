# Installation with Honor9-HarmonyOS2-Downloader.exe

[Русская версия](EXE-GUIDE-RU.md)

This is the recommended installation method. One application downloads and
verifies the GitHub files, checks the phone, and performs the flashing process.
No separate CMD or PowerShell window is required.

## 1. Before starting

Prepare:

- an Honor 9 `STF-AL00/AL10/TL10/L09/L19` running EMUI 9.x;
- `USER LockState: UNLOCKED` and `FB LockState: UNLOCKED`;
- Windows 10/11 x64 with at least 10 GiB free;
- at least 50% battery charge;
- a complete backup of personal data.

If FBLOCK is still enabled, follow
[`FBLOCK-GUIDE-EN.md`](FBLOCK-GUIDE-EN.md) first.

Internal storage will be erased completely. The microSD card is not formatted.

## 2. Downloading the EXE

1. Open the latest [Release](../../releases/latest).
2. Download
   [`Honor9-HarmonyOS2-Downloader.exe`](../../releases/latest/download/Honor9-HarmonyOS2-Downloader.exe).
3. Verify its SHA-256 against the Release `SHA256SUMS.txt`.
4. Run the EXE. If SmartScreen warns, verify the file and checksum before
   selecting **More info → Run anyway**.

The program is portable and uses the .NET Framework included with Windows.

## 3. Preparing files

1. Select a short path on an internal drive, such as `C:\Honor9Harmony`.
2. Wait for the automatic Release check or click **Проверить GitHub**.
3. Select PREAS:
   - **unsigned** — modified minimal `/china`, tested on STF-AL10;
   - **Huawei-signed** — official Chinese PREAS from HarmonyOS 2.0.0.140,
     experimental with the 2.0.0.150 system.
4. Click **1. Скачать и подготовить**.
5. Wait for **КОМПЛЕКТ ГОТОВ**.

Downloads can resume after interruption. The application verifies every
Release asset, safely extracts the base ZIP, and verifies the assembled
`payload.7z`. Only the separate signed PREAS is added when selected.

## 4. Connecting the phone

1. Power the phone off.
2. Close HiSuite and other ADB/Fastboot software.
3. Hold Volume Down and connect USB.
4. Wait for Fastboot/Rescue Mode.
5. Confirm that USERLOCK and FBLOCK both show `UNLOCKED`.

## 5. Phone check

1. Click **2. Проверить телефон**.
2. Continue only after **CHECK PASSED**.

The check validates the STF model, exactly one connected phone, USERLOCK,
FBLOCK, and the installed system version. It does not flash or erase anything.

## 6. Flashing

1. Click **3. Прошить телефон**.
2. Verify the selected PREAS option.
3. Enter exactly `STF-HARMONY-ERASE`.
4. Click **Начать прошивку**.
5. Do not disconnect USB, close the EXE, or touch TWRP controls.
6. Watch the integrated log and wait for **DONE**.

The process normally takes 30–60 minutes.

## 7. After installation

- first boot can take 10–15 minutes;
- never relock the bootloader with either PREAS option;
- do not install OTA updates over this mixed installation;
- preserve the `backups` and `logs` folders;
- never share `backups`, because it contains device-unique data.

A signed PREAS proves only that Huawei signed that image. It does not make
relocking safe and remains experimental with the 2.0.0.150 system.

Use this package at your own risk.
