$ErrorActionPreference = "Stop"
$ScriptPath = $PSScriptRoot
$ProjectDir = Join-Path $ScriptPath "DropGoLine"
$ReleaseDir = Join-Path $ScriptPath "Releases"

# Clean and Create Releases Directory
Write-Host "Cleaning Releases directory..." -ForegroundColor Cyan
if (Test-Path $ReleaseDir) {
    Remove-Item $ReleaseDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ReleaseDir | Out-Null

function Build-Variant {
    param (
        [string]$RuntimeIdentifier,
        [boolean]$SelfContained,
        [string]$OutputName,
        [boolean]$IsZip
    )

    $TypeStr = if ($SelfContained) { "Self-Contained (No Runtime)" } else { "Framework-Dependent (With Runtime)" }
    Write-Host "Building $OutputName [$RuntimeIdentifier - $TypeStr]..." -ForegroundColor Green

    # Temporary output directory for this build
    $TempDir = Join-Path $ReleaseDir "Temp_$OutputName"
    
    # Build arguments
    # Note: .NET 8/9/10 changes -p:PublishSingleFile behavior slightly, ensuring it's set.
    $BuildArgs = @(
        "publish",
        "$ProjectDir",
        "-c", "Release",
        "-r", $RuntimeIdentifier,
        "--self-contained", $SelfContained.ToString().ToLower(),
        "-o", "$TempDir"
    )

    if ($SelfContained) {
        $BuildArgs += "-p:PublishSingleFile=true"
        # Optional: -p:IncludeNativeLibrariesForSelfExtract=true to extract to temp if needed, 
        # but pure single file is usually preferred if possible.
    }

    # Execute dotnet publish
    dotnet @BuildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $OutputName"
    }

    # Packaging
    $DestPath = Join-Path $ReleaseDir $OutputName
    
    if ($IsZip) {
        # Create ZIP
        Write-Host "Packaging to ZIP: $DestPath" -ForegroundColor Gray
        Compress-Archive -Path "$TempDir\*" -DestinationPath $DestPath
    } else {
        # Copy Single File (EXE)
        $ExePath = Join-Path $TempDir "DropGoLine.exe"
        if (Test-Path $ExePath) {
            Write-Host "Copying EXE to: $DestPath" -ForegroundColor Gray
            Move-Item -Path $ExePath -Destination $DestPath
        } else {
            Write-Error "Executable not found at $ExePath"
        }
    }

    # Cleanup Temp Dir
    Remove-Item $TempDir -Recurse -Force
}

# --- 1. Windows 10 x32 (x86) ---
# No Runtime (EXE)
Build-Variant -RuntimeIdentifier "win-x86" -SelfContained $true -OutputName "DropGoLine_Win10_x32_NoRuntime.exe" -IsZip $false
# With Runtime (ZIP)
Build-Variant -RuntimeIdentifier "win-x86" -SelfContained $false -OutputName "DropGoLine_Win10_x32_WithRuntime.zip" -IsZip $true

# --- 2. Windows 10 x64 ---
# No Runtime (EXE)
Build-Variant -RuntimeIdentifier "win-x64" -SelfContained $true -OutputName "DropGoLine_Win10_x64_NoRuntime.exe" -IsZip $false
# With Runtime (ZIP)
Build-Variant -RuntimeIdentifier "win-x64" -SelfContained $false -OutputName "DropGoLine_Win10_x64_WithRuntime.zip" -IsZip $true

# --- 3. Windows 11 x64 (Copy of Win10 x64) ---
# Since Win11 is x64 and compatible with Win10 builds, we verify by simple copy.
Write-Host "Generating Windows 11 copies..." -ForegroundColor Cyan

# Copy EXE
$SrcExe = Join-Path $ReleaseDir "DropGoLine_Win10_x64_NoRuntime.exe"
$DstExe = Join-Path $ReleaseDir "DropGoLine_Win11_x64_NoRuntime.exe"
if (Test-Path $SrcExe) {
    Copy-Item -Path $SrcExe -Destination $DstExe
}

# Copy ZIP
$SrcZip = Join-Path $ReleaseDir "DropGoLine_Win10_x64_WithRuntime.zip"
$DstZip = Join-Path $ReleaseDir "DropGoLine_Win11_x64_WithRuntime.zip"
if (Test-Path $SrcZip) {
    Copy-Item -Path $SrcZip -Destination $DstZip
}

Write-Host "All builds completed successfully! Files are in: $ReleaseDir" -ForegroundColor Green
Get-ChildItem $ReleaseDir | Select-Object Name, Length, LastWriteTime
