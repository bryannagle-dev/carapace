# VXM Format Specification

## Overview

- Extension: `.vxm`
- Endianness: little-endian
- Compression: gzip
- Focus: simple, fast, versionable

## Uncompressed Layout

```
[Header]
[Palette]
[Voxel Data]
[Metadata JSON length + blob]
```

## Header

| Offset | Size | Type    | Name            |
| ------ | ---- | ------- | --------------- |
| 0      | 4    | char[4] | Magic = "VXM1"  |
| 4      | 2    | uint16  | Version         |
| 6      | 1    | uint8   | Endian (0 = LE) |
| 7      | 1    | uint8   | Reserved        |
| 8      | 2    | uint16  | SizeX           |
| 10     | 2    | uint16  | SizeY           |
| 12     | 2    | uint16  | SizeZ           |
| 14     | 2    | int16   | OriginX         |
| 16     | 2    | int16   | OriginY         |
| 18     | 2    | int16   | OriginZ         |

Header size is 20 bytes.

## Palette

- 256 entries
- RGBA8 per entry
- Size is 1024 bytes

If a future version omits this section, use a default palette.

## Voxel Data

Linear index order:

```
index = x + y * SizeX + z * SizeX * SizeY
```

RLE stream of runs:

- `uint8 material_id` where `0` is empty
- `uint16 run_length`

Example:

```
[0, 512]  -> 512 empty voxels
[3, 64]   -> 64 voxels of material 3
```

## Metadata Block

At the end of the file:

```
uint32 metadata_length
byte[metadata_length] UTF-8 JSON
```

Suggested metadata fields:

```json
{
  "seed": 12345,
  "generator": "Ossuary 0.1",
  "height_vox": 72,
  "parts": ["Torso", "Neck", "Head", "Arms", "Legs"],
  "style": "chunky"
}
```

### Tag Metadata (ViewerTags)

The tag viewer stores semantic voxel tags in the metadata JSON under a `tags` array.
Each tag includes:

- `name`: string tag identifier
- `direction`: string direction (`up`, `down`, `left`, `right`)
- `voxels`: list of voxel coordinates

Example:

```json
{
  "tags": [
    {
      "name": "pivot",
      "direction": "up",
      "voxels": [
        { "x": 10, "y": 12, "z": 8 },
        { "x": 11, "y": 12, "z": 8 }
      ]
    }
  ]
}
```
