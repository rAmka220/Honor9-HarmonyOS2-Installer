#!/usr/bin/env python3
"""Extract one named partition from a Huawei UPDATE.APP container."""

import argparse
import os
import struct
from pathlib import Path


MAGIC = b"\x55\xAA\x5A\xA5"


def records(source):
    while True:
        magic = source.read(4)
        if not magic:
            return
        if magic != MAGIC:
            continue

        header_size = struct.unpack("<I", source.read(4))[0]
        source.seek(16, os.SEEK_CUR)
        file_size = struct.unpack("<I", source.read(4))[0]
        source.seek(32, os.SEEK_CUR)
        name = source.read(16).split(b"\0", 1)[0].decode("ascii", "replace")
        source.seek(header_size - 76, os.SEEK_CUR)
        yield name, file_size
        source.seek(file_size, os.SEEK_CUR)
        padding = (-source.tell()) % 4
        if padding:
            source.seek(padding, os.SEEK_CUR)


def extract(update_app: Path, partition: str, output: Path) -> None:
    wanted = partition.upper()
    with update_app.open("rb") as source:
        for index, (name, size) in enumerate(records(source), 1):
            print(f"{index:3d} {name:20s} {size:12d}", flush=True)
            if name.upper() != wanted:
                continue
            output.parent.mkdir(parents=True, exist_ok=True)
            remaining = size
            with output.open("wb") as target:
                while remaining:
                    chunk = source.read(min(8 * 1024 * 1024, remaining))
                    if not chunk:
                        raise EOFError(f"Unexpected end of UPDATE.APP while extracting {name}")
                    target.write(chunk)
                    remaining -= len(chunk)
            print(f"Extracted {name}: {size} bytes -> {output}", flush=True)
            return
    raise SystemExit(f"Partition {partition!r} was not found")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("update_app", type=Path)
    parser.add_argument("partition")
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    extract(args.update_app, args.partition, args.output)


if __name__ == "__main__":
    main()
