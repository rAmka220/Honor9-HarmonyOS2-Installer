# Security and private device data

Do not publish device backup folders, OEMINFO, NVME, modemnvm, persist, secure
storage, eMMC boot partitions, IMEI, serial numbers, bootloader unlock codes,
GitHub tokens, or other credentials.

Before attaching an installer log, review it and remove local usernames,
computer paths, and device serial numbers.

For a security issue in the installer, prefer GitHub's private vulnerability
reporting page when available. For ordinary installation failures, use the bug
report form with a sanitized log.

Never bypass the installer model, USERLOCK, FBLOCK, battery, integrity, or
backup checks as a workaround.
