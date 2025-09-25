# Using DiskArc Library with PowerShell

This directory contains PowerShell scripts that demonstrate how to use the CiderPress2 DiskArc library to examine disk images and binary files through the `GeneralChunkAccess` class.

## Prerequisites

1. **Build the DiskArc library first:**
   ```bash
   dotnet build DiskArc/DiskArc.csproj
   ```
   This creates `DiskArc.dll` and `CommonUtil.dll` in `DiskArc/bin/Debug/net6.0/`

2. **PowerShell Core (pwsh)** - Works on Windows, Linux, and macOS
   - On Linux/macOS: Install via package manager or [PowerShell releases](https://github.com/PowerShell/PowerShell/releases)
   - On Windows: Usually pre-installed, or use PowerShell Core for best compatibility

## Scripts

### SimpleChunkExample.ps1

A minimal example showing basic usage with **intelligent format detection**:

```powershell
# Examine different disk image formats
./SimpleChunkExample.ps1 -FilePath "TestData/dos32/3.2.1-master.d13"    # 13-sector
./SimpleChunkExample.ps1 -FilePath "TestData/dos33/dos-forty.do"        # 16-sector DOS
./SimpleChunkExample.ps1 -FilePath "TestData/prodos/blank-140k.po"      # ProDOS 140K
./SimpleChunkExample.ps1 -FilePath "TestData/prodos/blank-800k.po"      # ProDOS 800K
./SimpleChunkExample.ps1 -FilePath "TestData/pascal/Apple Pascal1.po"   # Pascal
```

**What it does:**
- Loads the DiskArc library
- **Automatically detects** disk image format based on file size and extension
- **Prioritizes sector-based access** for track/sector disk formats (13-sector, 16-sector)
- **Uses block access** for ProDOS/Pascal images and other block-based formats
- Creates appropriate `GeneralChunkAccess` object
- Displays all `IChunkAccess` properties
- Reads and displays the first block or sector of data

**Format Detection Logic:**
- `.d13` files or 455 sectors (116,480 bytes) → 13-sector disk (sectors only)
- `.do`/`.dsk` files with 560/640 sectors → 16-sector DOS disk (sector-based)
- `.po` files → ProDOS block image (block-based)
- Other files divisible by 512 bytes → Block-based access
- Fallback → Sector-based access

### ExamineChunkAccess.ps1

A comprehensive script with advanced options:

```powershell
# Basic usage
./ExamineChunkAccess.ps1 -FilePath "disk.dsk"

# Specify custom DLL location
./ExamineChunkAccess.ps1 -FilePath "disk.po" -DllPath "./DiskArc/bin/Release/net6.0"

# Force block-based access
./ExamineChunkAccess.ps1 -FilePath "harddisk.hdv" -AsBlocks

# Sector-based access for a DOS 3.3 disk
./ExamineChunkAccess.ps1 -FilePath "dos33.dsk" -NumTracks 35 -SectorsPerTrack 16 -SectorOrder "DOS_Sector"

# 13-sector disk
./ExamineChunkAccess.ps1 -FilePath "old.d13" -NumTracks 35 -SectorsPerTrack 13
```

**Features:**
- Auto-detects common disk formats
- Supports both block-based (512-byte) and sector-based (256-byte) access
- Configurable sector ordering for 5.25" disks
- Displays comprehensive property information
- Demonstrates reading data and testing block/sector validity

## IChunkAccess Properties Explained

The scripts display all properties from the `IChunkAccess` interface:

| Property | Description |
|----------|-------------|
| `IsReadOnly` | True if the underlying stream doesn't support writing |
| `IsModified` | Flag indicating if any write operations have been performed |
| `ReadCount` | Number of block/sector read operations performed |
| `WriteCount` | Number of block/sector write operations performed |
| `FormattedLength` | Total size of formatted storage in bytes |
| `NumTracks` | Number of tracks (for sector-based access, 0 for block-based) |
| `NumSectorsPerTrack` | Sectors per track (13, 16, or 32 for DOS, 0 for block-based) |
| `HasSectors` | True if disk can be addressed as 256-byte track/sector |
| `HasBlocks` | True if disk can be addressed as 512-byte blocks |
| `FileOrder` | Sector ordering: Physical, DOS_Sector, ProDOS_Block, CPM_KBlock |
| `NibbleCodec` | Nibble encoder/decoder (null for block/sector images) |

## Common Disk Image Types

| Format | Typical Extension | File Size | Access Mode | Notes |
|--------|-------------------|-----------|-------------|-------|
| DOS 3.2/3.1 | `.d13` | 116,480 bytes | Sector-based only | 35 tracks × 13 sectors × 256 bytes |
| DOS 3.3 (35-track) | `.dsk`, `.do` | 143,360 bytes | Sector or Block | 35 tracks × 16 sectors × 256 bytes |
| DOS 3.3 (40-track) | `.dsk`, `.do` | 163,840 bytes | Sector or Block | 40 tracks × 16 sectors × 256 bytes |
| ProDOS (140K) | `.po` | 143,360 bytes | Block-based | 280 blocks × 512 bytes |
| ProDOS (800K) | `.po` | 819,200 bytes | Block-based | 1600 blocks × 512 bytes |
| Pascal | `.po` | 143,360 bytes | Block-based | 280 blocks × 512 bytes |
| CP/M | `.do`, `.po` | 143,360 bytes | Depends on extension | Format varies |

## Auto-Detection Results

The scripts now automatically detect and use the most appropriate access method:

```
# 13-sector disk (sectors only)
Detected: 13-sector disk image (DOS 3.2/3.1 format)
HasBlocks: False, HasSectors: True

# 16-sector DOS disk (native sector access, but blocks available)  
Detected: 16-sector disk image, 35 tracks (DOS 3.3 format)
HasBlocks: True, HasSectors: True

# ProDOS disk (blocks only)
Detected: ProDOS block image (280 blocks)
HasBlocks: True, HasSectors: False
```

## Example Output

### 13-Sector Disk (Sector-Only Access)
```
Examining file: TestData/dos32/3.2.1-master.d13 (116480 bytes)
Detected: 13-sector disk image (DOS 3.2/3.1 format)

IChunkAccess Properties:
  FormattedLength:    116480 bytes
  HasBlocks:          False
  HasSectors:         True
  IsReadOnly:         True
  FileOrder:          DOS_Sector
  NumTracks:          35
  NumSectorsPerTrack: 13

First 16 bytes of track 0, sector 0:
  F0 4A 99 FF FF 03 3C AD FF FF FF 26 B3 FF FF 4D
Read operations performed: 1
```

### ProDOS Disk (Block-Only Access)
```
Examining file: TestData/prodos/blank-800k.po (819200 bytes)
Detected: ProDOS block image (1600 blocks)

IChunkAccess Properties:
  FormattedLength:    819200 bytes
  HasBlocks:          True
  HasSectors:         False
  IsReadOnly:         True
  FileOrder:          ProDOS_Block
  NumTracks:          0
  NumSectorsPerTrack: 0

First 16 bytes of block 0:
  01 38 B0 03 4C 1C 09 78 86 43 C9 03 08 8A 29 70
Read operations performed: 1
```

### DOS 3.3 Disk (Dual Access)
```
Examining file: TestData/dos33/dos-forty.do (163840 bytes)
Detected: 16-sector disk image, 40 tracks (DOS 3.3 format)

IChunkAccess Properties:
  FormattedLength:    163840 bytes
  HasBlocks:          True
  HasSectors:         True
  IsReadOnly:         True
  FileOrder:          DOS_Sector
  NumTracks:          40
  NumSectorsPerTrack: 16

First 16 bytes of block 0:
  01 A5 27 C9 09 D0 18 A5 2B 4A 4A 4A 4A 09 C0 85
Read operations performed: 1
```

## Error Handling

The scripts include error handling for common issues:
- Missing DLL files (build the project first)
- File not found
- Invalid disk format parameters
- I/O errors when reading data

## Extending the Scripts

You can modify these scripts to:
- Write data to disk images (if opened read-write)
- Iterate through all blocks/sectors
- Analyze specific track/sector patterns
- Integrate with other DiskArc library features

## Apache License Compatibility

The DiskArc library is licensed under Apache License 2.0, which is compatible with GPL3 projects. When incorporating this code into a GPL3 project:

1. Keep all Apache license attributions
2. The combined work must be distributed under GPL3
3. Follow all GPL3 requirements for source distribution

See the main repository's LICENSE and NOTICE files for complete licensing information.