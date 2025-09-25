#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Examines a file using CiderPress2's DiskArc library and GeneralChunkAccess class.

.DESCRIPTION
    This PowerShell script takes a path to a file, opens it as a stream, and uses the 
    GeneralChunkAccess class to print out all the IChunkAccess properties. It demonstrates
    how to use the DiskArc library from PowerShell to analyze disk images or binary files.

.PARAMETER FilePath
    The path to the file to examine.

.PARAMETER DllPath
    Optional path to the DiskArc.dll and CommonUtil.dll files. If not specified, assumes
    they are in the Debug build directory relative to this script.

.PARAMETER AsBlocks
    If specified, treats the file as block-based (512-byte blocks). Otherwise tries to
    detect the format or defaults to sector-based access.

.PARAMETER StartOffset
    Offset within the file where the disk data starts (default: 0).

.PARAMETER NumTracks
    For sector-based access, number of tracks (default: auto-detect).

.PARAMETER SectorsPerTrack
    For sector-based access, sectors per track - 13, 16, or 32 (default: 16).

.PARAMETER SectorOrder
    Sector ordering for 16-sector disks: Physical, DOS_Sector, ProDOS_Block, CPM_KBlock (default: DOS_Sector).

.EXAMPLE
    .\ExamineChunkAccess.ps1 -FilePath "C:\disk.dsk"
    
.EXAMPLE
    .\ExamineChunkAccess.ps1 -FilePath "mydisk.po" -AsBlocks -DllPath ".\bin\Release\net6.0"

.EXAMPLE
    .\ExamineChunkAccess.ps1 -FilePath "dos33.dsk" -NumTracks 35 -SectorsPerTrack 16 -SectorOrder "DOS_Sector"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath,
    
    [string]$DllPath,
    
    [switch]$AsBlocks,
    
    [long]$StartOffset = 0,
    
    [uint32]$NumTracks = 0,
    
    [uint32]$SectorsPerTrack = 16,
    
    [string]$SectorOrder = "DOS_Sector"
)

# Determine DLL path
if (-not $DllPath) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $DllPath = Join-Path $scriptDir "DiskArc\bin\Debug\net6.0"
}

# Verify file exists
if (-not (Test-Path $FilePath)) {
    Write-Error "File not found: $FilePath"
    exit 1
}

# Verify DLL files exist
$diskArcDll = Join-Path $DllPath "DiskArc.dll"
$commonUtilDll = Join-Path $DllPath "CommonUtil.dll"

if (-not (Test-Path $diskArcDll)) {
    Write-Error "DiskArc.dll not found at: $diskArcDll"
    exit 1
}

if (-not (Test-Path $commonUtilDll)) {
    Write-Error "CommonUtil.dll not found at: $commonUtilDll"
    exit 1
}

try {
    # Load the assemblies
    Write-Host "Loading DiskArc library..." -ForegroundColor Green
    Add-Type -Path $commonUtilDll
    Add-Type -Path $diskArcDll
    
    # Open the file as a stream
    Write-Host "Opening file: $FilePath" -ForegroundColor Green
    $fileStream = [System.IO.File]::OpenRead($FilePath)
    $fileLength = $fileStream.Length
    
    Write-Host "File size: $fileLength bytes ($([math]::Round($fileLength/1024, 2)) KB)" -ForegroundColor Cyan
    
    # Create GeneralChunkAccess object with improved detection
    $chunkAccess = $null
    $totalSectors = $fileLength / 256
    $extension = [System.IO.Path]::GetExtension($FilePath).ToLower()
    
    # Auto-detect disk image type, prioritizing sector-based formats
    if (-not $AsBlocks -and $NumTracks -eq 0) {
        if ($totalSectors -eq 455 -or $extension -eq ".d13") {
            # 13-sector disk (35 tracks × 13 sectors = 455 sectors = 116,480 bytes)
            $NumTracks = 35
            $SectorsPerTrack = 13
            $SectorOrder = "DOS_Sector"
            Write-Host "Auto-detected: 13-sector disk image (DOS 3.2/3.1 format)" -ForegroundColor Cyan
        } elseif ($extension -eq ".po") {
            # ProDOS-ordered block image
            $AsBlocks = $true
            Write-Host "Auto-detected: ProDOS block image format" -ForegroundColor Cyan
        } elseif ($totalSectors -eq 560 -and $extension -ne ".po") {
            # 16-sector disk, 35 tracks
            $NumTracks = 35
            $SectorsPerTrack = 16
            Write-Host "Auto-detected: 16-sector disk image, 35 tracks (DOS 3.3 format)" -ForegroundColor Cyan
        } elseif ($totalSectors -eq 640 -and $extension -ne ".po") {
            # 16-sector disk, 40 tracks  
            $NumTracks = 40
            $SectorsPerTrack = 16
            Write-Host "Auto-detected: 16-sector disk image, 40 tracks (DOS 3.3 format)" -ForegroundColor Cyan
        } elseif ($extension -eq ".do" -or $extension -eq ".dsk") {
            # DOS-ordered disk, try to determine track count (limit to reasonable sizes)
            if ($totalSectors -eq 560) {
                $NumTracks = 35
            } elseif ($totalSectors -eq 640) {
                $NumTracks = 40
            } elseif ($totalSectors -le 1280) { # Max ~80 tracks for reasonable sector access
                $NumTracks = [uint32]($totalSectors / 16)
            } else {
                # Too large for sector access, use block access
                $AsBlocks = $true
                Write-Host "Auto-detected: Large DOS disk, using block access" -ForegroundColor Cyan
            }
            if (-not $AsBlocks) {
                $SectorsPerTrack = 16
                Write-Host "Auto-detected: DOS-ordered disk image ($NumTracks tracks)" -ForegroundColor Cyan
            }
        } elseif ($extension -eq ".2mg") {
            # 2MG disk image format
            $AsBlocks = $true
            Write-Host "Auto-detected: 2MG disk image format" -ForegroundColor Cyan
        } elseif ($extension -eq ".hdv") {
            # Hard disk image format
            $AsBlocks = $true
            Write-Host "Auto-detected: Hard disk image format" -ForegroundColor Cyan
        } elseif (($fileLength % 512) -eq 0) {
            # Default to block access if file size is a multiple of 512 bytes
            $AsBlocks = $true
            Write-Host "Auto-detected: Block-based image format" -ForegroundColor Cyan
        } else {
            # Fallback: try sector access only for reasonably sized files
            if ($totalSectors -le 1280) { # Max ~80 tracks
                $NumTracks = [uint32]($totalSectors / 16)
                $SectorsPerTrack = 16
                $SectorOrder = "Physical"
                Write-Host "Unknown format, defaulting to sector access ($NumTracks tracks)" -ForegroundColor Yellow
            } else {
                # Large file, use block access
                $AsBlocks = $true
                Write-Host "Large file, defaulting to block access" -ForegroundColor Yellow
            }
        }
    }
    
    if ($AsBlocks -or ($NumTracks -eq 0)) {
        # Block-based access (512-byte blocks)
        $blockCount = [uint32][math]::Floor($fileLength / 512)
        Write-Host "Creating block-based access with $blockCount blocks" -ForegroundColor Yellow
        $chunkAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, $StartOffset, $blockCount)
    } else {
        # Sector-based access (256-byte sectors)
        # Convert SectorOrder string to enum
        $sectorOrderEnum = [DiskArc.Defs+SectorOrder]::$SectorOrder
        
        Write-Host "Creating sector-based access: $NumTracks tracks, $SectorsPerTrack sectors/track, order: $SectorOrder" -ForegroundColor Yellow
        $chunkAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, $StartOffset, $NumTracks, $SectorsPerTrack, $sectorOrderEnum)
    }
    
    # Print all IChunkAccess properties
    Write-Host "`n=== IChunkAccess Properties ===" -ForegroundColor Magenta
    Write-Host "IsReadOnly:           $($chunkAccess.IsReadOnly)" -ForegroundColor White
    Write-Host "IsModified:           $($chunkAccess.IsModified)" -ForegroundColor White
    Write-Host "ReadCount:            $($chunkAccess.ReadCount)" -ForegroundColor White
    Write-Host "WriteCount:           $($chunkAccess.WriteCount)" -ForegroundColor White
    Write-Host "FormattedLength:      $($chunkAccess.FormattedLength) bytes ($([math]::Round($chunkAccess.FormattedLength/1024, 2)) KB)" -ForegroundColor White
    Write-Host "NumTracks:            $($chunkAccess.NumTracks)" -ForegroundColor White
    Write-Host "NumSectorsPerTrack:   $($chunkAccess.NumSectorsPerTrack)" -ForegroundColor White
    Write-Host "HasSectors:           $($chunkAccess.HasSectors)" -ForegroundColor White
    Write-Host "HasBlocks:            $($chunkAccess.HasBlocks)" -ForegroundColor White
    Write-Host "FileOrder:            $($chunkAccess.FileOrder)" -ForegroundColor White
    Write-Host "NibbleCodec:          $($chunkAccess.NibbleCodec)" -ForegroundColor White
    
    # Additional calculated information
    Write-Host "`n=== Calculated Information ===" -ForegroundColor Magenta
    if ($chunkAccess.HasBlocks) {
        $numBlocks = $chunkAccess.FormattedLength / 512
        Write-Host "Total Blocks:         $numBlocks" -ForegroundColor White
    }
    
    if ($chunkAccess.HasSectors) {
        $totalSectors = $chunkAccess.NumTracks * $chunkAccess.NumSectorsPerTrack
        Write-Host "Total Sectors:        $totalSectors" -ForegroundColor White
        Write-Host "Sector Size:          256 bytes" -ForegroundColor White
    }
    
    # Demonstrate reading some data (first block/sector)
    Write-Host "`n=== Sample Data Read ===" -ForegroundColor Magenta
    try {
        if ($chunkAccess.HasBlocks) {
            $buffer = New-Object byte[] 512
            $chunkAccess.ReadBlock(0, $buffer, 0)
            Write-Host "Successfully read block 0" -ForegroundColor Green
            Write-Host "First 32 bytes (hex): $([System.BitConverter]::ToString($buffer[0..31]).Replace('-', ' '))" -ForegroundColor Cyan
        } elseif ($chunkAccess.HasSectors) {
            $buffer = New-Object byte[] 256
            $chunkAccess.ReadSector(0, 0, $buffer, 0)
            Write-Host "Successfully read track 0, sector 0" -ForegroundColor Green
            Write-Host "First 32 bytes (hex): $([System.BitConverter]::ToString($buffer[0..31]).Replace('-', ' '))" -ForegroundColor Cyan
        }
        
        Write-Host "ReadCount after test: $($chunkAccess.ReadCount)" -ForegroundColor Yellow
        
    } catch {
        Write-Host "Error reading data: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test sector/block validity (useful for nibble images)
    Write-Host "`n=== Testing Access ===" -ForegroundColor Magenta
    try {
        if ($chunkAccess.HasBlocks) {
            $writable = $false
            $readable = $chunkAccess.TestBlock(0, [ref]$writable)
            Write-Host "Block 0 - Readable: $readable, Writable: $writable" -ForegroundColor White
        }
        
        if ($chunkAccess.HasSectors) {
            $writable = $false
            $readable = $chunkAccess.TestSector(0, 0, [ref]$writable)
            Write-Host "Track 0, Sector 0 - Readable: $readable, Writable: $writable" -ForegroundColor White
        }
    } catch {
        Write-Host "Error testing access: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Show ToString() representation
    Write-Host "`n=== Object String Representation ===" -ForegroundColor Magenta
    Write-Host $chunkAccess.ToString() -ForegroundColor White
    
} catch {
    Write-Error "Error: $($_.Exception.Message)"
    Write-Error "Stack trace: $($_.Exception.StackTrace)"
} finally {
    # Clean up
    if ($fileStream) {
        $fileStream.Close()
        $fileStream.Dispose()
        Write-Host "`nFile stream closed." -ForegroundColor Green
    }
}

Write-Host "`nDone!" -ForegroundColor Green