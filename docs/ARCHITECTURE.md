# Architecture

## Components

| Component | Purpose |
|---|---|
| `Honor9-HarmonyOS2-Downloader.exe` | GitHub download, resume, integrity checks, extraction, UI and process log |
| `install.ps1` | Device validation, backups, OEMINFO patching, flashing and data setup |
| `prepare-payload.ps1` | Manual fallback for joining Release payload parts |
| `tools/patch-oeminfo.ps1` | Patches only model, region and version records in the phone's own OEMINFO |
| `payload-manifest.sha256` | Verifies every extracted firmware file |

## Installation flow

```text
GitHub Release
    │
    ├── base ZIP ───────────────┐
    └── payload.7z.partNN ──────┤
                                ▼
                    EXE verifies SHA-256
                                │
                    extracts + joins payload
                                │
                                ▼
Phone in Fastboot ──► model / USERLOCK / FBLOCK / build checks
                                │
                                ▼
Temporary TWRP ─────► unique partition backups + OEMINFO audit
                                │
                                ▼
Fastboot ───────────► HarmonyOS partitions + selected PREAS
                                │
                                ▼
Temporary TWRP ─────► F2FS userdata + /data/hw_init + cache
                                │
                                ▼
Fastboot ───────────► stock recovery/eRecovery ──► first boot
```

Honor 9 may not switch modes programmatically. Wait functions emit
`MANUAL ACTION REQUIRED`, sound an EXE alert, and continue waiting for five
minutes so the user can enter Fastboot or cable-free TWRP manually.

## Integrity layers

1. GitHub Release asset digest or `SHA256SUMS.txt`;
2. SHA-256 of the assembled `payload.7z`;
3. `payload-manifest.sha256` for every extracted image;
4. SHA-256 of locally created backups;
5. signed PREAS, if ever enabled, additionally requires the expected Huawei
   public-key hash and a `/china` filesystem check.

## Safety boundaries

The installer intentionally does not flash XLOADER, FASTBOOT, GPT, NVME,
factory modem backups, persist, secure storage, foreign OEMINFO, IMEI, serial
numbers, or calibration data. The bootloader is never relocked.
