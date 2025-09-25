#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Simple example showing how to use DiskArc's GeneralChunkAccess from PowerShell.

.DESCRIPTION
    This is a minimal example that demonstrates the basic usage pattern for loading
    the DiskArc library and examining file properties through GeneralChunkAccess.

.PARAMETER FilePath
    Path to the disk image or binary file to examine.

.EXAMPLE
    .\SimpleChunkExample.ps1 -FilePath "disk.dsk"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath
)

# Assume DLLs are in the Debug build directory relative to script location
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dllPath = Join-Path $scriptDir "DiskArc\bin\Debug\net6.0"

$diskArcDll = Join-Path $dllPath "DiskArc.dll"
$commonUtilDll = Join-Path $dllPath "CommonUtil.dll"

# Verify files exist
if (-not (Test-Path $FilePath)) {
    Write-Error "File not found: $FilePath"
    exit 1
}

if (-not (Test-Path $diskArcDll) -or -not (Test-Path $commonUtilDll)) {
    Write-Error "DLL files not found in: $dllPath"
    Write-Host "Make sure you've built the DiskArc project first."
    exit 1
}

try {
    # Load the DiskArc library
    Add-Type -Path $commonUtilDll
    Add-Type -Path $diskArcDll
    
    # Open the file
    $fileStream = [System.IO.File]::OpenRead($FilePath)
    $fileLength = $fileStream.Length
    
    Write-Host "Examining file: $FilePath ($fileLength bytes)"
    
    # Detect disk image type based on file size and extension
    $totalSectors = $fileLength / 256
    $extension = [System.IO.Path]::GetExtension($FilePath).ToLower()
    $chunkAccess = $null
    
    # Check for common sector-based disk formats first (they take precedence)
    if ($totalSectors -eq 455 -or $extension -eq ".d13") {
        # 13-sector disk (35 tracks × 13 sectors = 455 sectors = 116,480 bytes)
        Write-Host "Detected: 13-sector disk image (DOS 3.2/3.1 format)"
        $chunkAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, 35, 13, [DiskArc.Defs+SectorOrder]::DOS_Sector)
    } elseif ($extension -eq ".po" -and ($totalSectors -eq 280 -or $totalSectors -eq 320)) {
        # ProDOS-ordered block image (280 blocks = 35 tracks, 320 blocks = 40 tracks)
        $blockCount = [uint32]($fileLength / 512)
        Write-Host "Detected: ProDOS block image ($blockCount blocks)"
        $chunkAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, $blockCount)
    } elseif ($totalSectors -eq 560 -and $extension -ne ".po") {
        # 16-sector disk, 35 tracks (35 × 16 = 560 sectors = 143,360 bytes)  
        Write-Host "Detected: 16-sector disk image, 35 tracks (DOS 3.3 format)"
        $chunkAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, 35, 16, [DiskArc.Defs+SectorOrder]::DOS_Sector)
    } elseif ($totalSectors -eq 640 -and $extension -ne ".po") {
        # 16-sector disk, 40 tracks (40 × 16 = 640 sectors = 163,840 bytes)
        Write-Host "Detected: 16-sector disk image, 40 tracks (DOS 3.3 format)"
        $chunkAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, 40, 16, [DiskArc.Defs+SectorOrder]::DOS_Sector)
    } elseif ($extension -eq ".do" -or $extension -eq ".dsk") {
        # DOS-ordered disk, try to determine track count (limit to reasonable sizes)
        if ($totalSectors -eq 560) {
            $tracks = 35
        } elseif ($totalSectors -eq 640) {
            $tracks = 40
        } elseif ($totalSectors -le 1280) { # Max ~80 tracks
            $tracks = [uint32]($totalSectors / 16)
        } else {
            # Too large for sector access, use block access
            $blockCount = [uint32]($fileLength / 512)
            Write-Host "Detected: Large DOS disk, using block access ($blockCount blocks)"
            $chunkAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, $blockCount)
        }
        if ($chunkAccess -eq $null) {
            Write-Host "Detected: DOS-ordered disk image ($tracks tracks)"
            $chunkAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, $tracks, 16, [DiskArc.Defs+SectorOrder]::DOS_Sector)
        }
    } elseif ($extension -eq ".po" -or $extension -eq ".2mg" -or $extension -eq ".hdv" -or ($fileLength % 512) -eq 0) {
        # ProDOS/2MG/HDV or any file divisible by 512 bytes - use block access
        $blockCount = [uint32]($fileLength / 512)
        if ($extension -eq ".2mg") {
            Write-Host "Detected: 2MG disk image ($blockCount blocks)"
        } elseif ($extension -eq ".hdv") {
            Write-Host "Detected: Hard disk image ($blockCount blocks)"
        } elseif ($extension -eq ".po") {
            Write-Host "Detected: ProDOS block image ($blockCount blocks)"
        } else {
            Write-Host "Detected: Block-based image ($blockCount blocks)"
        }
        $chunkAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, $blockCount)
    } else {
        # Fallback: try sector access with reasonable defaults
        $tracks = [uint32]($totalSectors / 16)
        Write-Host "Unknown format, trying sector access ($tracks tracks, 16 sectors/track)"
        $chunkAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, $tracks, 16, [DiskArc.Defs+SectorOrder]::Physical)
    }
    
    # Print key properties
    Write-Host ""
    Write-Host "IChunkAccess Properties:"
    Write-Host "  FormattedLength:    $($chunkAccess.FormattedLength) bytes"
    Write-Host "  HasBlocks:          $($chunkAccess.HasBlocks)"
    Write-Host "  HasSectors:         $($chunkAccess.HasSectors)"
    Write-Host "  IsReadOnly:         $($chunkAccess.IsReadOnly)"
    Write-Host "  FileOrder:          $($chunkAccess.FileOrder)"
    Write-Host "  NumTracks:          $($chunkAccess.NumTracks)"
    Write-Host "  NumSectorsPerTrack: $($chunkAccess.NumSectorsPerTrack)"
    
    # Read first block/sector to demonstrate access
    if ($chunkAccess.HasBlocks -and $chunkAccess.FormattedLength -ge 512) {
        $buffer = New-Object byte[] 512
        $chunkAccess.ReadBlock(0, $buffer, 0)
        
        Write-Host ""
        Write-Host "First 16 bytes of block 0:"
        $hexString = [System.BitConverter]::ToString($buffer[0..15]).Replace('-', ' ')
        Write-Host "  $hexString"
        
    } elseif ($chunkAccess.HasSectors -and $chunkAccess.NumTracks -gt 0) {
        $buffer = New-Object byte[] 256
        $chunkAccess.ReadSector(0, 0, $buffer, 0)
        
        Write-Host ""
        Write-Host "First 16 bytes of track 0, sector 0:"
        $hexString = [System.BitConverter]::ToString($buffer[0..15]).Replace('-', ' ')
        Write-Host "  $hexString"
    }
    
    Write-Host "Read operations performed: $($chunkAccess.ReadCount)"
    
} catch {
    Write-Error "Error: $($_.Exception.Message)"
} finally {
    if ($fileStream) {
        $fileStream.Close()
    }
}