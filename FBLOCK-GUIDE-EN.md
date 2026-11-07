# Honor 9: disabling FBLOCK with PotatoNV

[Русская версия](FBLOCK-GUIDE-RU.md)

This installer requires two independent states:

- `USER LockState: UNLOCKED` — the regular bootloader/user lock;
- `FB LockState: UNLOCKED` — Huawei's additional flash protection is disabled.

Unlocking USERLOCK with a code is not enough. FBLOCK restricts writes to secure
partitions and blocks some OEM commands. The HarmonyOS installer modifies
OEMINFO and flashes protected partitions, so it must not run while
`FB LockState: LOCKED`.

## Warning

This procedure requires opening the phone and shorting a hardware testpoint.
Incorrect disassembly, shorting the wrong contacts, or selecting the wrong
bootloader may damage the device. Back up your data first. Work only with the
phone powered off, never touch battery contacts with a metal tool, and stop if
you are not confident about any step.

The installer author performed this procedure on an Honor 9 using PotatoNV by
shorting the testpoint and enabling **Disable FBLOCK**. The result is still not
guaranteed on another device or board revision.

## Requirements

1. An Honor 9 with an `STF` model code and Kirin 960 SoC.
2. A Windows computer and a reliable USB cable.
3. Huawei HiSuite or Huawei USB drivers.
4. Huawei Testpoint Driver.
5. PotatoNV from its official repository:
   <https://github.com/kitsuned/PotatoNV>
6. Proper tools for removing the rear cover and conductive tweezers.

Use the current links from the official PotatoNV documentation. Do not use
modified builds downloaded from random archives.

## 1. Prepare the phone

1. Charge the phone to at least 50%.
2. Back up personal data. Unlocking USERLOCK later may erase all internal data.
3. Power the phone off completely and disconnect USB.
4. Carefully heat and remove the rear cover.
5. Do not damage the fingerprint sensor cable or other flex cables.
6. Find a testpoint diagram specifically for the Honor 9 STF motherboard. Do
   not use a diagram for the Honor 9 Lite, Huawei P10, or another model.

## 2. Enter testpoint mode

1. Open Windows Device Manager.
2. On the powered-off phone, short the correct testpoint to the metal shield,
   or connect the two marked points, exactly as shown for your board revision.
3. Keep the contacts shorted and connect USB to the computer.
4. Remove the tweezers after a few seconds.
5. Device Manager should show `HUAWEI USB COM 1.0` or `USB SER`.
6. If it does not appear, disconnect USB and check the driver, cable, and
   testpoint location. Never probe random contacts by trial and error.

## 3. Disable FBLOCK

1. Start PotatoNV.
2. Select the **Kirin 960** bootloader. PotatoNV's official compatibility table
   lists Honor 9 (`STF`) under Kirin 960.
3. Enable the **Disable FBLOCK** checkbox.
4. Follow the PotatoNV interface and click **Start**.
5. Do not disconnect USB until the operation has completed.
6. Store the unlock code shown by PotatoNV in a safe place. Never publish it or
   use a code from another phone.

According to the PotatoNV documentation, its service bootloader is loaded into
RAM. **Disable FBLOCK** disables a separate security check that regular USERLOCK
unlocking does not remove.

## 4. Unlock USERLOCK

If USERLOCK is still locked, enter Fastboot and run this command with the code
generated for your own phone:

```text
fastboot oem unlock YOUR_PERSONAL_CODE
```

Confirm the operation on the phone if prompted. This command may erase all user
data. If USERLOCK is already unlocked, do not unlock it again.

## 5. Verify before installing HarmonyOS

1. Reboot the phone into its regular Fastboot/Rescue Mode.
2. Both states must be shown as unlocked:

```text
FB LockState: UNLOCKED
USER LockState: UNLOCKED
```

3. Connect the phone to the installer folder and run `check-phone.cmd`.
4. Continue only after this message:

```text
CHECK PASSED. No partitions were changed.
```

If the installer reports `FBLOCK is locked` or cannot determine the FBLOCK
state, do not edit the check or run flashing commands manually.

## After installing HarmonyOS

- never relock USERLOCK;
- do not try to restore FBLOCK to LOCKED on top of the modified system;
- do not use a three-button `dload` package from another model;
- keep the unlock code and the `backups` folder private and in separate safe
  locations.

PotatoNV source and documentation: <https://github.com/kitsuned/PotatoNV>
