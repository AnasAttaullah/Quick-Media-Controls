<#
.SYNOPSIS
    Automated Release & Packaging Script for Quick Media Controls.

.DESCRIPTION
    Builds, synchronizes versions, and generates release artifacts for:
    1. GitHub Standalone Installer (.exe via Inno Setup)
    2. Microsoft Store Package (.msixupload via MSBuild/WAP)

.PARAMETER Version
    The release version number in semver format (e.g. 2.0.1).
    If omitted, the script prompts or uses the currently configured version.

.PARAMETER Target
    The distribution target: 'GitHub', 'Store', or 'All'.
    If omitted, an interactive selection menu is shown.

.PARAMETER SkipGit
    Skip git add, commit, and tag prompts.

.PARAMETER SkipVersionSync
    Skip updating version numbers in project files before building.

.EXAMPLE
    .\build-release.ps1 -Version 2.0.1 -Target All
    .\build-release.ps1 -Target GitHub
    .\build-release.ps1 -Target Store -SkipGit
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Version,

    [Parameter(Position = 1)]
    [ValidateSet("GitHub", "Store", "All", "")]
    [string]$Target = "",

    [switch]$SkipGit,
    [switch]$SkipVersionSync
)

$ErrorActionPreference = "Stop"
$ScriptRoot = $PSScriptRoot
Set-Location $ScriptRoot

# --- UI Formatting Functions ---

function Write-Header {
    param([string]$Title)
    Write-Host ""
    Write-Host "==================================================================" -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor White
    Write-Host "==================================================================" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Status, [string]$Message)
    Write-Host " [$Status] " -ForegroundColor Yellow -NoNewline
    Write-Host $Message -ForegroundColor White
}

function Write-Success {
    param([string]$Message)
    Write-Host " [SUCCESS] " -ForegroundColor Green -NoNewline
    Write-Host $Message -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host " [INFO] " -ForegroundColor Cyan -NoNewline
    Write-Host $Message -ForegroundColor Gray
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host " [ERROR] " -ForegroundColor Red -NoNewline
    Write-Host $Message -ForegroundColor Red
}

function Get-FriendlySize {
    param([long]$Bytes)
    if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N2} MB" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N2} KB" -f ($Bytes / 1KB) }
    return "$Bytes B"
}

# --- Tool Discovery Functions ---

function Find-Dotnet {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw ".NET SDK (dotnet CLI) was not found in PATH. Please install .NET 8 SDK."
    }
    return $dotnet.Source
}

function Find-ISCC {
    $candidatePaths = @(
        "D:\Softwares\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe",
        "C:\Inno Setup 6\ISCC.exe"
    )

    foreach ($path in $candidatePaths) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    # Probe registry
    $regKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1"
    )
    foreach ($key in $regKeys) {
        if (Test-Path $key) {
            $installLocation = (Get-ItemProperty $key -Name "InstallLocation" -ErrorAction SilentlyContinue).InstallLocation
            if ($installLocation -and (Test-Path (Join-Path $installLocation "ISCC.exe"))) {
                return (Join-Path $installLocation "ISCC.exe")
            }
        }
    }

    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    throw "Inno Setup Compiler (ISCC.exe) was not found. Please install Inno Setup 6 or add it to PATH."
}

function Find-MSBuild {
    # 1. Try vswhere
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    # 2. Known standard paths
    $candidatePaths = @(
        "D:\Softwares\Visual Studio\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($path in $candidatePaths) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    throw "MSBuild.exe was not found. Please install Visual Studio with desktop development tools."
}

# --- Project Paths ---
$CsprojPath        = Join-Path $ScriptRoot "Quick Media Controls\Quick Media Controls.csproj"
$SetupIssPath      = Join-Path $ScriptRoot "setup.iss"
$AppxManifestPath  = Join-Path $ScriptRoot "QuickMediaControls.Store\Package.appxmanifest"
$UpdateXmlPath     = Join-Path $ScriptRoot "update.xml"
$StoreWapprojPath  = Join-Path $ScriptRoot "QuickMediaControls.Store\QuickMediaControls.Store.wapproj"
$PackagingOutputDir = Join-Path $ScriptRoot "Packaging Output"
$StoreOutputDir    = Join-Path $PackagingOutputDir "Store Output"

# --- Read Current Version ---
function Get-CurrentProjectVersion {
    if (Test-Path $CsprojPath) {
        $content = Get-Content $CsprojPath -Raw
        if ($content -match '<Version>(.*?)</Version>') {
            return $matches[1].Trim()
        }
    }
    return "1.0.0"
}

$CurrentVersion = Get-CurrentProjectVersion

Write-Header "Quick Media Controls - Release Automation"
Write-Info "Repository: $ScriptRoot"
Write-Info "Detected Current Version: $CurrentVersion"

# --- Determine Target Version ---
if ([string]::IsNullOrWhiteSpace($Version)) {
    $inputVersion = Read-Host "`nEnter release version (default: $CurrentVersion)"
    if ([string]::IsNullOrWhiteSpace($inputVersion)) {
        $Version = $CurrentVersion
    } else {
        $Version = $inputVersion.Trim()
    }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-ErrorMsg "Invalid version format '$Version'. Please use SemVer format (e.g. 2.0.1)."
    exit 1
}

# --- Determine Release Target ---
if ([string]::IsNullOrWhiteSpace($Target)) {
    Write-Host "`nSelect release target:" -ForegroundColor Yellow
    Write-Host "  [1] GitHub (Standalone Installer .exe)" -ForegroundColor White
    Write-Host "  [2] Microsoft Store (MSIX Upload Package .msixupload)" -ForegroundColor White
    Write-Host "  [3] All (Both GitHub & Store)" -ForegroundColor White
    $choice = Read-Host "Choose option [1-3] (default: 3)"
    switch ($choice) {
        "1" { $Target = "GitHub" }
        "2" { $Target = "Store" }
        default { $Target = "All" }
    }
}

Write-Info "Target: $Target | Release Version: v$Version"

# --- Stage 1: Version Synchronization ---
if (-not $SkipVersionSync) {
    Write-Header "Stage 1: Synchronizing Version Numbers ($Version)"

    # 1. Update .csproj
    if (Test-Path $CsprojPath) {
        Write-Step "RUNNING" "Updating Quick Media Controls.csproj version to $Version..."
        $content = Get-Content $CsprojPath -Raw
        $updated = $content -replace '<Version>.*?</Version>', "<Version>$Version</Version>"
        Set-Content -Path $CsprojPath -Value $updated -NoNewline
        Write-Success "Updated Quick Media Controls.csproj"
    }

    # 2. Update setup.iss
    if (Test-Path $SetupIssPath) {
        Write-Step "RUNNING" "Updating setup.iss version to $Version..."
        $content = Get-Content $SetupIssPath -Raw
        $updated = $content -replace '#define MyAppVersion ".*?"', "#define MyAppVersion `"$Version`""
        Set-Content -Path $SetupIssPath -Value $updated -NoNewline
        Write-Success "Updated setup.iss"
    }

    # 3. Update Package.appxmanifest (Store requires Quad version X.Y.Z.0)
    if (Test-Path $AppxManifestPath) {
        Write-Step "RUNNING" "Updating Package.appxmanifest version to $Version.0..."
        $content = Get-Content $AppxManifestPath -Raw
        $updated = [regex]::Replace($content, '(\<Identity[^>]*Version=")[^"]*(")', "`${1}$Version.0`${2}")
        Set-Content -Path $AppxManifestPath -Value $updated -NoNewline
        Write-Success "Updated Package.appxmanifest"
    }

    # 4. Update update.xml
    if (Test-Path $UpdateXmlPath) {
        Write-Step "RUNNING" "Updating update.xml manifest..."
        $content = Get-Content $UpdateXmlPath -Raw
        $updated = $content -replace '<version>.*?</version>', "<version>$Version</version>"
        $updated = $updated -replace '<url>.*?</url>', "<url>https://github.com/AnasAttaullah/Quick-Media-Controls/releases/download/v$Version/QuickMediaControls-Setup-v$Version.exe</url>"
        $updated = $updated -replace '<changelog>.*?</changelog>', "<changelog>https://github.com/AnasAttaullah/Quick-Media-Controls/releases/tag/v$Version</changelog>"
        Set-Content -Path $UpdateXmlPath -Value $updated -NoNewline
        Write-Success "Updated update.xml"
    }
} else {
    Write-Info "Skipping version synchronization as requested (-SkipVersionSync)."
}

# Ensure Output Directories exist
if (-not (Test-Path $PackagingOutputDir)) { New-Item -ItemType Directory -Path $PackagingOutputDir | Out-Null }
if (-not (Test-Path $StoreOutputDir)) { New-Item -ItemType Directory -Path $StoreOutputDir | Out-Null }

$GeneratedArtifacts = @()

# --- Stage 2: Build GitHub Installer Target ---
if ($Target -eq "GitHub" -or $Target -eq "All") {
    Write-Header "Stage 2: Building GitHub Standalone Installer"

    $dotnetPath = Find-Dotnet
    $isccPath   = Find-ISCC

    Write-Info "Using dotnet CLI: $dotnetPath"
    Write-Info "Using Inno Setup: $isccPath"

    # 1. Publish .NET application
    Write-Step "RUNNING" "Publishing Quick Media Controls for win-x64 Release..."
    & $dotnetPath publish "$CsprojPath" -c Release -r win-x64 --self-contained false --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-ErrorMsg "dotnet publish failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
    Write-Success "dotnet publish completed successfully."

    # 2. Compile Inno Setup Script
    Write-Step "RUNNING" "Compiling installer with Inno Setup (ISCC)..."
    & "$isccPath" "$SetupIssPath"
    if ($LASTEXITCODE -ne 0) {
        Write-ErrorMsg "Inno Setup compilation failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }

    $installerFile = Join-Path $PackagingOutputDir "QuickMediaControls-Setup-v$Version.exe"
    if (Test-Path $installerFile) {
        $item = Get-Item $installerFile
        $size = Get-FriendlySize $item.Length
        Write-Success "Generated Installer: $($item.Name) ($size)"
        $GeneratedArtifacts += [PSCustomObject]@{
            Channel = "GitHub"
            Type    = "Setup Installer (.exe)"
            Path    = $installerFile
            Size    = $size
        }
    } else {
        Write-ErrorMsg "Installer not found at expected path: $installerFile"
        exit 1
    }
}

# --- Stage 3: Build Microsoft Store Package Target ---
if ($Target -eq "Store" -or $Target -eq "All") {
    Write-Header "Stage 3: Building Microsoft Store MSIX Package"

    $msbuildPath = Find-MSBuild
    Write-Info "Using MSBuild: $msbuildPath"

    # Clean old Store output artifacts before build to prevent version conflicts
    if (Test-Path $StoreOutputDir) {
        Write-Step "RUNNING" "Cleaning previous Store build outputs..."
        Get-ChildItem -Path $StoreOutputDir -Filter "*.msixupload" | Remove-Item -Force -ErrorAction SilentlyContinue
        Get-ChildItem -Path $StoreOutputDir -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Write-Success "Cleaned previous Store outputs."
    }

    Write-Step "RUNNING" "Building QuickMediaControls.Store multi-architecture package (x86, x64, ARM64)..."
    $storeArgs = @(
        $StoreWapprojPath,
        "/p:Configuration=Release",
        "/p:AppxBundlePlatforms=x86|x64|arm64",
        "/p:AppxBundle=Always",
        "/p:UapAppxPackageBuildMode=StoreUpload",
        "/p:AppxPackageSigningEnabled=false",
        "/nologo",
        "/verbosity:minimal"
    )

    & $msbuildPath @storeArgs
    if ($LASTEXITCODE -ne 0) {
        Write-ErrorMsg "MSBuild packaging failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }

    $uploadPackage = Get-ChildItem -Path $StoreOutputDir -Filter "*.msixupload" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($uploadPackage) {
        $size = Get-FriendlySize $uploadPackage.Length
        Write-Success "Generated Store Package: $($uploadPackage.Name) ($size)"
        $GeneratedArtifacts += [PSCustomObject]@{
            Channel = "Microsoft Store"
            Type    = "Upload Package (.msixupload)"
            Path    = $uploadPackage.FullName
            Size    = $size
        }
    } else {
        Write-ErrorMsg "Store package (.msixupload) not found in: $StoreOutputDir"
        exit 1
    }
}

# --- Stage 4: Git Commit & Tagging ---
if (-not $SkipGit) {
    $gitCmd = Get-Command git -ErrorAction SilentlyContinue
    if ($gitCmd) {
        Write-Host ""
        $confirmGit = Read-Host "Create git commit and tag 'v$Version'? [Y/n]"
        if ($confirmGit -ne "n" -and $confirmGit -ne "N") {
            Write-Step "RUNNING" "Staging and committing version changes..."
            git add "$CsprojPath" "$SetupIssPath" "$AppxManifestPath" "$UpdateXmlPath" "$StoreWapprojPath"
            git commit -m "Release v$Version"
            git tag -a "v$Version" -m "Release v$Version"
            Write-Success "Git commit and tag 'v$Version' created."
            Write-Info "To push to GitHub, run: git push origin main --tags"
        }
    }
}

# --- Release Summary & Post-Release Instructions ---
Write-Header "Release Artifacts Summary"
foreach ($art in $GeneratedArtifacts) {
    Write-Host "  Channel:  " -ForegroundColor Yellow -NoNewline
    Write-Host "$($art.Channel)" -ForegroundColor White
    Write-Host "  Type:     " -ForegroundColor Gray -NoNewline
    Write-Host "$($art.Type) ($($art.Size))" -ForegroundColor Gray
    Write-Host "  Location: " -ForegroundColor Cyan -NoNewline
    Write-Host "$($art.Path)" -ForegroundColor Green
    Write-Host ""
}

Write-Header "Next Steps to Publish"

if ($Target -eq "GitHub" -or $Target -eq "All") {
    Write-Host "--- GitHub Release ---" -ForegroundColor Yellow
    Write-Host "1. Push your branch & tag:" -ForegroundColor White
    Write-Host "   git push origin main --tags" -ForegroundColor Cyan
    Write-Host "2. Create the GitHub release:" -ForegroundColor White
    Write-Host "   https://github.com/AnasAttaullah/Quick-Media-Controls/releases/new" -ForegroundColor Cyan
    Write-Host "   - Select tag: v$Version" -ForegroundColor Gray
    Write-Host "   - Upload file: $($PackagingOutputDir)\QuickMediaControls-Setup-v$Version.exe" -ForegroundColor Gray
    Write-Host "3. Publish the release and verify update.xml is on main branch." -ForegroundColor Gray
    Write-Host ""
}

if ($Target -eq "Store" -or $Target -eq "All") {
    Write-Host "--- Microsoft Store Submission ---" -ForegroundColor Yellow
    Write-Host "1. Go to Microsoft Partner Center:" -ForegroundColor White
    Write-Host "   https://partner.microsoft.com/dashboard/apps-and-games/overview" -ForegroundColor Cyan
    Write-Host "2. Open 'Quick Media Controls' -> 'Start update' / 'Submissions'." -ForegroundColor White
    Write-Host "3. In the 'Packages' section, upload the .msixupload package from:" -ForegroundColor White
    Write-Host "   $StoreOutputDir" -ForegroundColor Cyan
    Write-Host "4. Complete release notes and submit for certification." -ForegroundColor Gray
    Write-Host ""
}

Write-Success "Release workflow completed for v$Version!"
