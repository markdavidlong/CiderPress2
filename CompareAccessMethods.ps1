#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Demonstrates different ways to use GeneralChunkAccess with the same file.

.DESCRIPTION
    This script shows how the same disk image can be accessed as both blocks and sectors,
    and how the properties differ between the two approaches.
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dllPath = Join-Path $scriptDir "DiskArc\bin\Debug\net6.0"

# Load libraries
Add-Type -Path (Join-Path $dllPath "CommonUtil.dll")
Add-Type -Path (Join-Path $dllPath "DiskArc.dll")

$fileStream = [System.IO.File]::OpenRead($FilePath)
$fileLength = $fileStream.Length

Write-Host "Comparing Block vs Sector Access for: $FilePath" -ForegroundColor Green
Write-Host "File size: $fileLength bytes" -ForegroundColor Cyan
Write-Host ""

try {
    # Method 1: Auto-detect and use appropriate primary access
    Write-Host "=== AUTO-DETECTED PRIMARY ACCESS ===" -ForegroundColor Yellow
    $totalSectors = $fileLength / 256
    $extension = [System.IO.Path]::GetExtension($FilePath).ToLower()
    $primaryAccess = $null
    
    if ($totalSectors -eq 455 -or $extension -eq ".d13") {
        # 13-sector disk - can only be accessed as sectors
        Write-Host "Primary: 13-sector disk (sectors only)"
        $primaryAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, 35, 13, [DiskArc.Defs+SectorOrder]::DOS_Sector)
    } elseif ($extension -eq ".po") {
        # ProDOS - block access is most appropriate
        $blockCount = [uint32]($fileLength / 512)
        Write-Host "Primary: ProDOS block image"
        $primaryAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, $blockCount)
    } elseif ($totalSectors -eq 560 -or $totalSectors -eq 640) {
        # 16-sector DOS disk - can be accessed both ways, but sectors are native
        $tracks = if ($totalSectors -eq 560) { 35 } else { 40 }
        Write-Host "Primary: $tracks-track DOS disk (sector-based)"
        $primaryAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, $tracks, 16, [DiskArc.Defs+SectorOrder]::DOS_Sector)
    } else {
        # Default to block access
        $blockCount = [uint32]($fileLength / 512)
        Write-Host "Primary: Block-based access"
        $primaryAccess = New-Object DiskArc.GeneralChunkAccess($fileStream, 0, $blockCount)
    }
    
    Write-Host "HasBlocks: $($primaryAccess.HasBlocks)"
    Write-Host "HasSectors: $($primaryAccess.HasSectors)"
    Write-Host "FormattedLength: $($primaryAccess.FormattedLength) bytes"
    Write-Host "FileOrder: $($primaryAccess.FileOrder)"
    Write-Host "NumTracks: $($primaryAccess.NumTracks)"
    Write-Host "NumSectorsPerTrack: $($primaryAccess.NumSectorsPerTrack)"
    Write-Host ""
    
    # Method 2: Alternative access (if possible)
    if ($primaryAccess.HasBlocks -and $primaryAccess.HasSectors -and $primaryAccess.NumSectorsPerTrack -eq 16) {
        Write-Host "=== ALTERNATIVE ACCESS (BLOCKS) ===" -ForegroundColor Yellow
        
        # Create new stream for alternative access
        $fileStream2 = [System.IO.File]::OpenRead($FilePath)
        $blockCount = [uint32]($fileLength / 512)
        $blockAccess = New-Object DiskArc.GeneralChunkAccess($fileStream2, 0, $blockCount)
        
        Write-Host "HasBlocks: $($blockAccess.HasBlocks)"
        Write-Host "HasSectors: $($blockAccess.HasSectors)"
        Write-Host "FormattedLength: $($blockAccess.FormattedLength) bytes"
        Write-Host "FileOrder: $($blockAccess.FileOrder)"
        Write-Host "Total blocks: $blockCount"
        Write-Host ""
        
        # Compare reading the same data both ways
        Write-Host "=== COMPARING SECTOR vs BLOCK ACCESS ===" -ForegroundColor Magenta
        
        # Read first block (512 bytes) via block access
        $blockBuffer = New-Object byte[] 512
        $blockAccess.ReadBlock(0, $blockBuffer, 0)
        
        # Read first two sectors (256 bytes each = 512 bytes total) via sector access
        $sectorBuffer1 = New-Object byte[] 256
        $sectorBuffer2 = New-Object byte[] 256
        $primaryAccess.ReadSector(0, 0, $sectorBuffer1, 0)
        $primaryAccess.ReadSector(0, 1, $sectorBuffer2, 0)
        
        # Combine the two sectors
        $combinedSectorData = $sectorBuffer1 + $sectorBuffer2
        
        # Compare the data
        $blockHex = [System.BitConverter]::ToString($blockBuffer[0..31]).Replace('-', ' ')
        $sectorHex = [System.BitConverter]::ToString($combinedSectorData[0..31]).Replace('-', ' ')
        
        Write-Host "Block 0 first 32 bytes:     $blockHex"
        Write-Host "T0S0+T0S1 first 32 bytes:   $sectorHex"
        Write-Host "Data matches: $(($blockHex -eq $sectorHex))" -ForegroundColor $(if ($blockHex -eq $sectorHex) { "Green" } else { "Red" })
        
        $fileStream2.Close()
    } elseif ($primaryAccess.HasBlocks -and -not $primaryAccess.HasSectors) {
        Write-Host "=== BLOCK-ONLY ACCESS ===" -ForegroundColor Yellow
        Write-Host "This disk format only supports block access (no sectors)"
    } elseif ($primaryAccess.HasSectors -and -not $primaryAccess.HasBlocks) {
        Write-Host "=== SECTOR-ONLY ACCESS ===" -ForegroundColor Yellow
        Write-Host "This disk format only supports sector access (no blocks)"
        Write-Host "Sectors per track: $($primaryAccess.NumSectorsPerTrack)"
    } else {
        Write-Host "=== SINGLE ACCESS MODE ===" -ForegroundColor Yellow
        Write-Host "No alternative access method available for this format"
    }
    
} finally {
    $fileStream.Close()
}

Write-Host ""
Write-Host "Demo complete!" -ForegroundColor Green