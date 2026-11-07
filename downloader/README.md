# Downloader EXE

Windows GUI bootstrapper for the Honor 9 HarmonyOS 2 installer.

Build on Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The generated `Honor9-HarmonyOS2-Downloader.exe` uses the .NET Framework
included with Windows 10/11. It retrieves the latest GitHub Release, supports
resuming partial downloads, verifies every SHA-256 digest, safely extracts the
base package, assembles `payload.7z`, and launches the existing phone check or
installer with the selected PREAS mode.

`create-icon.py` generates the original multi-resolution application icon.
