# Contributing

Contributions are welcome for documentation, device testing, downloader UI,
and safer validation.

## Bug reports

1. Use the repository bug-report form.
2. State the exact STF model and original firmware.
3. Include USERLOCK/FBLOCK state and the failed stage.
4. Attach only a reviewed `logs/install-*.log` when needed.
5. Remove serial numbers, local usernames and other private information.

Never attach `backups`, OEMINFO, NVME, modemnvm, persist, secure storage,
eMMC boot images, IMEI, serial numbers, or unlock codes.

## Pull requests

- keep Windows PowerShell 5.1 compatibility for installer scripts;
- keep the C# downloader compatible with the Windows .NET Framework compiler;
- preserve SHA-256 verification and device gating;
- do not weaken USERLOCK, FBLOCK, model, battery, or backup checks;
- update Russian and English documentation together;
- run `downloader/build.ps1` and parse all modified PowerShell files before
  submitting.

Support for AL00/TL10/L09/L19 must remain marked experimental until verified on
real hardware.
