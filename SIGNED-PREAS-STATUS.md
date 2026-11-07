# Signed PREAS status

## Current release

Release `v1.1.0` intentionally does **not** publish a signed PREAS installation
option. The EXE keeps that option disabled when no verified asset is present.

The tested installer path uses the modified minimal `/china` PREAS with AVB
algorithm `NONE`. The bootloader must remain unlocked.

## What was verified

The complete official STF-AL00/AL10 HarmonyOS `2.0.0.140 C00E140R1P3` DLOAD
package was downloaded and its `UPDATE.APP` was enumerated. It contains 42
records, including system, vendor, product, userdata, and recovery images, but
it contains no `PREAS` record. Therefore a signed Chinese PREAS cannot be
extracted from that package.

The available signed backup is an official European `/oversea` PREAS. Its AVB
metadata is valid:

- algorithm: `SHA256_RSA2048`;
- Huawei public-key SHA-1:
  `005f38b26728b8e065544833eac99a68e0b6bf51`;
- partition name: `preas`.

It is not published as an installable option because the C00 system requires
`/preas/china`; the `/oversea` image has already produced
`/preas/china not exist` on this conversion.

## What is required

A safe signed option requires an unmodified PREAS dump from a stock Chinese
STF-AL00/AL10 device, followed by all of these checks:

1. EROFS contains `/china`;
2. AVB algorithm is `SHA256_RSA2048`;
3. key SHA-1 equals `005f38b26728b8e065544833eac99a68e0b6bf51`;
4. partition descriptor is `preas`;
5. practical boot testing with the 2.0.0.150 package.

Even after those checks, a signed PREAS alone does not make bootloader relock
safe.
