#!/usr/bin/env python3
"""Convert an exact raw partition image to Android sparse format."""

import argparse
import struct
from pathlib import Path


SPARSE_MAGIC = 0xED26FF3A
CHUNK_RAW = 0xCAC1
CHUNK_FILL = 0xCAC2


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--block-size", type=int, default=4096)
    args = parser.parse_args()

    block_size = args.block_size
    size = args.source.stat().st_size
    if size % block_size:
        raise SystemExit("Image size must be a multiple of the block size")

    chunks = []
    with args.source.open("rb") as source:
        block_count = size // block_size
        current_type = None
        current_start = 0
        current_count = 0
        current_fill = 0

        for block_index in range(block_count):
            block = source.read(block_size)
            if block == bytes(block_size):
                chunk_type, fill = CHUNK_FILL, 0
            elif block == block[:4] * (block_size // 4):
                chunk_type, fill = CHUNK_FILL, struct.unpack("<I", block[:4])[0]
            else:
                chunk_type, fill = CHUNK_RAW, 0

            can_merge = (
                chunk_type == current_type
                and (chunk_type != CHUNK_FILL or fill == current_fill)
                and current_count < 0xFFFFFFFF
            )
            if not can_merge and current_type is not None:
                chunks.append((current_type, current_start, current_count, current_fill))
                current_start = block_index
                current_count = 0
            if current_type is None or not can_merge:
                current_type = chunk_type
                current_fill = fill
            current_count += 1

        if current_type is not None:
            chunks.append((current_type, current_start, current_count, current_fill))

    with args.source.open("rb") as source, args.output.open("wb") as output:
        output.write(struct.pack(
            "<IHHHHIIII", SPARSE_MAGIC, 1, 0, 28, 12, block_size,
            size // block_size, len(chunks), 0,
        ))
        for chunk_type, first_block, count, fill in chunks:
            payload_size = count * block_size if chunk_type == CHUNK_RAW else 4
            output.write(struct.pack("<HHII", chunk_type, 0, count, 12 + payload_size))
            if chunk_type == CHUNK_RAW:
                source.seek(first_block * block_size)
                remaining = payload_size
                while remaining:
                    part = source.read(min(8 * 1024 * 1024, remaining))
                    if not part:
                        raise EOFError("Unexpected end of raw image")
                    output.write(part)
                    remaining -= len(part)
            else:
                output.write(struct.pack("<I", fill))

    print(f"raw={size} sparse={args.output.stat().st_size} chunks={len(chunks)}")


if __name__ == "__main__":
    main()
