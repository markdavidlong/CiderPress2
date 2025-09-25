#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Tests all disk image files in TestData directory with our DiskArc scripts.

.DESCRIPTION
    Systematically goes through all disk image files in the TestData directory
    and tests them with our SimpleChunkExample.ps1 script to see which formats
    work and which ones don't.
#>

param(
    [switch]$Verbose
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$testScript = Join-Path $scriptDir "SimpleChunkExample.ps1"

# Define file extensions to test
$diskExtensions = @("*.po", "*.do", "*.dsk", "*.d13", "*.2mg", "*.hdv", "*.img", "*.iso", "*.raw", "*.nib", "*.woz", "*.dc42", "*.ddd", "*.app")

Write-Host "=== TESTING ALL DISK IMAGES IN TESTDATA ===" -ForegroundColor Green
Write-Host ""

$totalTested = 0
$successCount = 0
$failedFiles = @()
$successFiles = @()

# Get all disk image files
$allFiles = @()
foreach ($ext in $diskExtensions) {
    $files = Get-ChildItem -Path "TestData" -Recurse -Include $ext -File | Where-Object { $_.Name -notlike "*.gz" -and $_.Name -notlike "*.metadata" }
    $allFiles += $files
}

$allFiles = $allFiles | Sort-Object FullName

Write-Host "Found $($allFiles.Count) disk image files to test" -ForegroundColor Cyan
Write-Host ""

foreach ($file in $allFiles) {
    $totalTested++
    $relativePath = $file.FullName.Replace($PWD.Path + [System.IO.Path]::DirectorySeparatorChar, "")
    
    Write-Host "[$totalTested/$($allFiles.Count)] Testing: $relativePath" -ForegroundColor Yellow
    
    try {
        # Capture output and check for errors
        $output = & pwsh $testScript -FilePath $file.FullName 2>&1
        $exitCode = $LASTEXITCODE
        
        if ($exitCode -eq 0 -and ($output -join "`n") -notlike "*Write-Error*" -and ($output -join "`n") -notlike "*Exception*") {
            $successCount++
            $successFiles += $relativePath
            
            # Extract key info from output
            $detectedLine = ($output | Where-Object { $_ -like "Detected:*" }) -join ""
            $formattedLine = ($output | Where-Object { $_ -like "*FormattedLength:*" }) -join ""
            $hasBlocksLine = ($output | Where-Object { $_ -like "*HasBlocks:*" }) -join ""
            $hasSectorsLine = ($output | Where-Object { $_ -like "*HasSectors:*" }) -join ""
            
            Write-Host "  ✅ SUCCESS" -ForegroundColor Green
            if ($detectedLine) { Write-Host "     $detectedLine" -ForegroundColor White }
            if ($formattedLine) { Write-Host "     $formattedLine" -ForegroundColor White }
            if ($hasBlocksLine -and $hasSectorsLine) { 
                Write-Host "     $hasBlocksLine, $hasSectorsLine" -ForegroundColor White 
            }
            
        } else {
            $failedFiles += @{
                Path = $relativePath
                Error = ($output | Where-Object { $_ -like "*Error*" -or $_ -like "*Exception*" }) -join "; "
            }
            Write-Host "  ❌ FAILED" -ForegroundColor Red
            if ($Verbose) {
                $errorLines = $output | Where-Object { $_ -like "*Error*" -or $_ -like "*Exception*" }
                foreach ($errorLine in $errorLines) {
                    Write-Host "     $errorLine" -ForegroundColor Red
                }
            }
        }
        
    } catch {
        $failedFiles += @{
            Path = $relativePath
            Error = $_.Exception.Message
        }
        Write-Host "  ❌ FAILED (Exception)" -ForegroundColor Red
        if ($Verbose) {
            Write-Host "     $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host ""
}

# Summary
Write-Host "=== SUMMARY ===" -ForegroundColor Magenta
Write-Host "Total files tested: $totalTested" -ForegroundColor White
Write-Host "Successful: $successCount" -ForegroundColor Green
Write-Host "Failed: $($failedFiles.Count)" -ForegroundColor Red
Write-Host "Success rate: $([math]::Round(($successCount / $totalTested) * 100, 1))%" -ForegroundColor Cyan
Write-Host ""

if ($successFiles.Count -gt 0) {
    Write-Host "=== SUCCESSFUL FILES ===" -ForegroundColor Green
    foreach ($file in $successFiles) {
        Write-Host "  ✅ $file" -ForegroundColor Green
    }
    Write-Host ""
}

if ($failedFiles.Count -gt 0) {
    Write-Host "=== FAILED FILES ===" -ForegroundColor Red
    foreach ($failed in $failedFiles) {
        Write-Host "  ❌ $($failed.Path)" -ForegroundColor Red
        if ($failed.Error -and $failed.Error.Trim() -ne "") {
            Write-Host "     Reason: $($failed.Error)" -ForegroundColor Yellow
        }
    }
    Write-Host ""
}

# Categorize by format
Write-Host "=== SUCCESS BY FORMAT ===" -ForegroundColor Magenta
$successByExt = @{}
foreach ($file in $successFiles) {
    $ext = [System.IO.Path]::GetExtension($file).ToLower()
    if (-not $successByExt.ContainsKey($ext)) {
        $successByExt[$ext] = 0
    }
    $successByExt[$ext]++
}

foreach ($ext in ($successByExt.Keys | Sort-Object)) {
    Write-Host "  $ext : $($successByExt[$ext]) files" -ForegroundColor White
}

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Green