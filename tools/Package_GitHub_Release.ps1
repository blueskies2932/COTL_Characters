param(
    [string]$ManifestPath = (Join-Path $PSScriptRoot "..\product_build_manifest.json"),
    [string]$IconSource = (Join-Path $PSScriptRoot "..\COTL_Characters.png"),
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Reset-DirectoryInsideProduct([string]$path) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($ProductRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside Character_only_product_build: $fullPath"
    }
    if ($fullPath -eq $ProductRoot) {
        throw "Refusing to clean Character_only_product_build root."
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $fullPath | Out-Null
}

function Write-Utf8NoBom([string]$path, [string]$content) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
}

function Assert-FileExists([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required GitHub release file is missing: $path"
    }
}

function Assert-StrictUtf8TextFile([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw "Text file must be UTF-8 without BOM: $path"
    }
    if ($bytes | Where-Object { $_ -eq 0 }) {
        throw "Text file contains NUL bytes and may not be UTF-8 text: $path"
    }
    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $null = $strictUtf8.GetString($bytes)
}

function Assert-NoForbiddenEntries([string]$root) {
    $forbiddenNames = @("bin", "obj", ".git", ".vs", ".idea", "__pycache__")
    $forbiddenDependencyFiles = @(
        "COTL_API.dll",
        "BepInEx.dll",
        "0Harmony.dll",
        "Assembly-CSharp.dll",
        "UnityEngine.dll",
        "UnityEngine.CoreModule.dll",
        "Newtonsoft.Json.dll",
        "Sirenix.Serialization.dll"
    )
    foreach ($entry in Get-ChildItem -LiteralPath $root -Recurse -Force) {
        if ($forbiddenNames -contains $entry.Name) {
            throw "Forbidden development entry included in GitHub release package: $($entry.FullName)"
        }
        if ($forbiddenDependencyFiles -contains $entry.Name) {
            throw "Do not bundle external dependency/game/framework assembly in GitHub release package: $($entry.FullName)"
        }
        if ($entry.Name -match "\.(env|user|suo|tmp|cache)$") {
            throw "Forbidden temp/secret-like file included in GitHub release package: $($entry.FullName)"
        }
    }
}

function Assert-NoSecretText([string]$root) {
    $patterns = @(
        "sk-[A-Za-z0-9_-]{20,}",
        "OPENAI_API_KEY\s*=",
        "ANTHROPIC_API_KEY\s*=",
        "GEMINI_API_KEY\s*=",
        "apiKey\s*:\s*`"[^`"]+`""
    )
    $textFiles = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $_.Extension -in ".json", ".md", ".txt", ".cmd", ".ps1"
    }
    foreach ($file in $textFiles) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($pattern in $patterns) {
            if ($text -match $pattern) {
                throw "Potential secret-like text found in release file: $($file.FullName)"
            }
        }
    }
}

function Assert-ZipRoot([string]$zipPath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entries = @($zip.Entries | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Name) })
        $entryNames = @($entries | ForEach-Object { $_.FullName.Replace("\", "/") })
        foreach ($required in @(
            "README.md",
            "INSTALL.md",
            "CHANGELOG.md",
            "Install_COTL_Characters.cmd",
            "Install_COTL_Characters.ps1",
            "plugins/COTL_AL_NPCs/COTL_AL_NPCs.dll",
            "plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.exe")) {
            if ($entryNames -notcontains $required) {
                throw "Zip root is missing required file: $required"
            }
        }
        if ($entryNames | Where-Object { $_ -like "github/*" -or $_ -like "dist/*" -or $_ -like "COTL_Characters-*/*" }) {
            throw "Zip contains an extra parent output folder."
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Copy-RequiredTree([string]$source, [string]$destination) {
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required package source is missing: $source"
    }
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -Path (Join-Path $source "*") -Destination $destination -Recurse -Force
}

$manifestFullPath = [System.IO.Path]::GetFullPath($ManifestPath)
$ProductRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $manifestFullPath))
$manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
$mod = $manifest.mod
$packageRoot = Join-Path $ProductRoot $manifest.package_name
$distRoot = Join-Path $ProductRoot "dist"
$releaseRoot = Join-Path $distRoot "github"

if (-not $SkipBuild) {
    & (Join-Path $ProductRoot "tools\Assemble_Product_Build.ps1") -ManifestPath $manifestFullPath
    if ($LASTEXITCODE -ne 0) {
        throw "Character-only product assembly failed."
    }
}

Reset-DirectoryInsideProduct $releaseRoot
Copy-RequiredTree $packageRoot $releaseRoot
if (Test-Path -LiteralPath (Join-Path $releaseRoot "BepInEx")) {
    New-Item -ItemType Directory -Force -Path (Join-Path $releaseRoot "plugins") | Out-Null
    Copy-Item -Path (Join-Path $releaseRoot "BepInEx\plugins\*") -Destination (Join-Path $releaseRoot "plugins") -Recurse -Force
    Remove-Item -LiteralPath (Join-Path $releaseRoot "BepInEx") -Recurse -Force
}

if (Test-Path -LiteralPath (Join-Path $releaseRoot "manifest.json")) {
    Remove-Item -LiteralPath (Join-Path $releaseRoot "manifest.json") -Force
}

Copy-Item -LiteralPath ([System.IO.Path]::GetFullPath($IconSource)) -Destination (Join-Path $releaseRoot "COTL_Characters.png") -Force

$readme = @"
# COTL Characters

COTL Characters adds AI-powered Character Mode conversations to Cult of the Lamb followers. It is packaged for direct GitHub download: download the release zip, unzip it, then double-click the installer.

## Quick Install

1. Install Cult of the Lamb.
2. Install a modded profile with BepInEx and COTL_API separately.
3. Download COTL_Characters-$($mod.version_number)-direct-install.zip from GitHub Releases.
4. Right-click the zip and choose Extract All.
5. Open the extracted folder.
6. Double-click Install_COTL_Characters.cmd.
7. Choose your Thunderstore or r2modman profile folder when asked.
8. Start the game from that same modded profile.

No API keys are included. BepInEx, COTL_API, and game files are not included. The first launch will guide you through AI provider setup in game.

## What It Adds

- In-game Character Mode conversations with followers.
- Per-character awareness settings for traits, cult details, current events, world state, lore, and long-term chat memory.
- Short, Medium, and Long reply length controls.
- Player-written lore for individual characters.
- Tournament Ledger support.
- Character-safe invocations for cult faith and vanilla role cleanup.
- Provider setup for OpenAI, OpenAI-compatible endpoints, Anthropic, Gemini, LM Studio, and Ollama-compatible local endpoints.
- Optional internet access for eligible character replies.

## Keyboard Controls

- F7 while paused: open or close the Invocations menu.
- F8 while paused: open or close the full Tournament Ledger.
- F8 while unpaused: show or hide the current tournament match overlay.
- F9 while paused: open or close the Internet Access panel.

## Requirements

- Windows.
- Cult of the Lamb.
- BepInExPack CultOfTheLamb 5.4.2101, installed separately.
- COTL_API 0.3.3, installed separately.
- .NET 10 runtime for the included sidecar process.
- An AI provider key or local AI endpoint, unless you only use local providers that do not require a key.

## AI Provider Setup

After installation, launch the game from the modded profile. The AI Provider Setup prompt appears until setup is usable.

Use Find, Test & Save Setup. This tests provider and model settings through the same sidecar path used by character conversations.

Manual configuration is also possible at:

BepInEx/config/COTL_AL_NPCs/AiProvider.json

This hotfix no longer stores pasted provider keys in the mod manager profile. If you used an older release and have BepInEx/config/COTL_AL_NPCs/AiProviderKey.txt, delete that file before sharing or syncing a profile. Rotate the provider key if that profile was already shared.

## Reset or Change API Key

Use the Reset button in the in-game AI Provider Setup panel when it is visible.

Manual reset:

1. Close the game.
2. Open your modded profile folder.
3. Open BepInEx/config/COTL_AL_NPCs/.
4. Delete AiProvider.json, AiProviderKey.txt, and LAST_PROVIDER_SETUP_TEST.txt if they exist.
5. Update or delete the matching Windows user environment variable: OPENAI_API_KEY, OPENROUTER_API_KEY, ANTHROPIC_API_KEY, GEMINI_API_KEY, or AI_PROVIDER_API_KEY.
6. Relaunch the game. The AI Provider Setup panel should appear again.

Thunderstore/r2modman profiles are usually under Thunderstore Mod Manager/DataFolder/CultOfTheLamb/profiles/<ProfileName>/ or r2modmanPlus-local/CultOfTheLamb/profiles/<ProfileName>/.

## Troubleshooting

- If the mod does not load, make sure you installed into the same profile you use to launch the game.
- If the installer cannot find a profile automatically, choose the folder that contains the BepInEx folder.
- If AI replies do not work, finish the in-game AI Provider Setup prompt.
- If sidecar errors appear, install the .NET 10 runtime and confirm BepInEx/plugins/COTL_AL_NPCs/sidecar exists.
- If COTL_API is missing, install it separately into the same modded profile.

## AI-Assisted Creation Disclosure

This mod and GitHub release package were partially created with the assistance of Generative AI for code changes, refactoring, documentation, and release packaging.

AI model vendor used: OpenAI.

## Credits

- Deamon_Blue
"@
Write-Utf8NoBom (Join-Path $releaseRoot "README.md") ($readme + [Environment]::NewLine)

$install = @"
# COTL Characters Install Guide

This is the friendly path:

1. Install BepInEx and COTL_API separately in your Cult of the Lamb modded profile.
2. Extract the release zip.
3. Double-click Install_COTL_Characters.cmd.
4. Pick your modded Cult of the Lamb profile folder.
5. Launch the game from that same profile.

## Picking the right folder

Choose the profile folder that contains BepInEx. Common examples:

- Thunderstore Mod Manager profile folder
- r2modman profile folder
- A manual Cult of the Lamb folder that already has BepInEx installed

The installer copies:

BepInEx/plugins/COTL_AL_NPCs/

into the selected profile.

## Manual install

If the installer does not work:

1. Open this extracted folder.
2. Open the plugins folder.
3. Copy the COTL_AL_NPCs folder.
4. Paste it into your modded profile's BepInEx/plugins folder.
5. Launch Cult of the Lamb from the modded profile.

## After install

Use the in-game AI Provider Setup prompt. No provider keys, BepInEx files, COTL_API files, or game files are included in this release.
"@
Write-Utf8NoBom (Join-Path $releaseRoot "INSTALL.md") ($install + [Environment]::NewLine)

$changelog = @"
# Changelog

## $($mod.version_number)

- Security hotfix: AI provider setup no longer stores pasted API keys in the mod manager profile.
- Removed generated and packaged provider setup command/script files from the release.
- Existing users should delete BepInEx/config/COTL_AL_NPCs/AiProviderKey.txt before sharing or syncing a profile.
- Fixed the AI Cult About editor text readability while typing.
- Removed an extra overlay blocker layer from the About editor.
- Kept the About editor above the underlying game UI while open.
"@
Write-Utf8NoBom (Join-Path $releaseRoot "CHANGELOG.md") ($changelog + [Environment]::NewLine)

$cmd = @"
@echo off
setlocal
title COTL Characters Installer
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install_COTL_Characters.ps1"
echo.
pause
"@
Write-Utf8NoBom (Join-Path $releaseRoot "Install_COTL_Characters.cmd") ($cmd -replace "`n", "`r`n")

$ps1 = @'
$ErrorActionPreference = "Stop"

function Write-Step([string]$message) {
    Write-Host ""
    Write-Host $message -ForegroundColor Cyan
}

function Find-CandidateProfiles {
    $roots = New-Object System.Collections.Generic.List[string]
    $appData = [Environment]::GetFolderPath("ApplicationData")
    foreach ($path in @(
        (Join-Path $appData "Thunderstore Mod Manager\DataFolder\COTL\profiles"),
        (Join-Path $appData "Thunderstore Mod Manager\DataFolder\CultOfTheLamb\profiles"),
        (Join-Path $appData "r2modmanPlus-local\CultOfTheLamb\profiles"),
        (Join-Path $appData "r2modmanPlus-local\COTL\profiles"))) {
        if (Test-Path -LiteralPath $path) {
            $roots.Add($path)
        }
    }

    $profiles = New-Object System.Collections.Generic.List[string]
    foreach ($root in $roots) {
        Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            if (Test-Path -LiteralPath (Join-Path $_.FullName "BepInEx")) {
                $profiles.Add($_.FullName)
            }
        }
    }
    return @($profiles | Sort-Object -Unique)
}

function Select-FolderWithDialog {
    try {
        Add-Type -AssemblyName System.Windows.Forms
        $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
        $dialog.Description = "Select your modded Cult of the Lamb profile folder. Pick the folder that contains BepInEx."
        $dialog.ShowNewFolderButton = $false
        $result = $dialog.ShowDialog()
        if ($result -eq [System.Windows.Forms.DialogResult]::OK) {
            return $dialog.SelectedPath
        }
    }
    catch {
        Write-Host "Folder picker was not available: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    return ""
}

function Normalize-ProfileRoot([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        return ""
    }
    $full = [System.IO.Path]::GetFullPath($path.Trim('" '))
    if ((Split-Path -Leaf $full) -ieq "BepInEx") {
        return Split-Path -Parent $full
    }
    return $full
}

function Copy-DirectoryContents([string]$source, [string]$destination) {
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Missing source folder: $source"
    }
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -Path (Join-Path $source "*") -Destination $destination -Recurse -Force
}

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$payloadPlugins = Join-Path $packageRoot "plugins"
$payloadPlugin = Join-Path $payloadPlugins "COTL_AL_NPCs\COTL_AL_NPCs.dll"

Write-Host "COTL Characters Installer" -ForegroundColor Green
Write-Host "This installer copies the mod into a BepInEx Cult of the Lamb profile."

if (-not (Test-Path -LiteralPath $payloadPlugin)) {
    throw "This installer cannot find the packaged mod files. Extract the release zip first, then run Install_COTL_Characters.cmd from the extracted folder."
}

$profiles = Find-CandidateProfiles
$selected = ""
if ($profiles.Count -gt 0) {
    Write-Step "Detected mod profiles:"
    for ($i = 0; $i -lt $profiles.Count; $i++) {
        Write-Host ("[{0}] {1}" -f ($i + 1), $profiles[$i])
    }
    Write-Host "[B] Browse for a folder"
    Write-Host ""
    $choice = Read-Host "Choose a profile number, or B to browse"
    if ($choice -match "^\d+$") {
        $index = [int]$choice - 1
        if ($index -ge 0 -and $index -lt $profiles.Count) {
            $selected = $profiles[$index]
        }
    }
}

if ([string]::IsNullOrWhiteSpace($selected)) {
    Write-Step "Select the profile folder"
    $selected = Select-FolderWithDialog
}

if ([string]::IsNullOrWhiteSpace($selected)) {
    Write-Host ""
    $selected = Read-Host "Paste the full path to your modded profile folder"
}

$targetRoot = Normalize-ProfileRoot $selected
if ([string]::IsNullOrWhiteSpace($targetRoot)) {
    throw "No install folder was selected."
}

Write-Step "Installing to:"
Write-Host $targetRoot

$targetPlugins = Join-Path $targetRoot "BepInEx\plugins"
New-Item -ItemType Directory -Force -Path $targetPlugins | Out-Null
Copy-DirectoryContents $payloadPlugins $targetPlugins

$installedDll = Join-Path $targetRoot "BepInEx\plugins\COTL_AL_NPCs\COTL_AL_NPCs.dll"
$installedSidecar = Join-Path $targetRoot "BepInEx\plugins\COTL_AL_NPCs\sidecar\CotlAiNpcSidecar.exe"
if (-not (Test-Path -LiteralPath $installedDll)) {
    throw "Install did not create the plugin DLL at $installedDll"
}
if (-not (Test-Path -LiteralPath $installedSidecar)) {
    throw "Install did not create the sidecar runtime at $installedSidecar"
}

Write-Step "Install complete."
Write-Host "Launch Cult of the Lamb from this same modded profile."
Write-Host "If the AI Provider Setup prompt appears in game, finish setup there."

$cotlApi = Get-ChildItem -LiteralPath (Join-Path $targetRoot "BepInEx\plugins") -Recurse -Filter "COTL_API.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $cotlApi) {
    Write-Host ""
    Write-Host "Note: I did not find COTL_API.dll in this profile. Install COTL_API 0.3.3 if the mod does not load." -ForegroundColor Yellow
}

Write-Host ""
'@
Write-Utf8NoBom (Join-Path $releaseRoot "Install_COTL_Characters.ps1") $ps1

Assert-FileExists (Join-Path $releaseRoot "README.md")
Assert-FileExists (Join-Path $releaseRoot "INSTALL.md")
Assert-FileExists (Join-Path $releaseRoot "CHANGELOG.md")
Assert-FileExists (Join-Path $releaseRoot "Install_COTL_Characters.cmd")
Assert-FileExists (Join-Path $releaseRoot "Install_COTL_Characters.ps1")
Assert-FileExists (Join-Path $releaseRoot "plugins\COTL_AL_NPCs\COTL_AL_NPCs.dll")
Assert-FileExists (Join-Path $releaseRoot "plugins\COTL_AL_NPCs\sidecar\CotlAiNpcSidecar.exe")

foreach ($file in @("README.md", "INSTALL.md", "CHANGELOG.md", "README_SETUP.txt", "Install_COTL_Characters.cmd", "Install_COTL_Characters.ps1")) {
    Assert-StrictUtf8TextFile (Join-Path $releaseRoot $file)
}

Assert-NoForbiddenEntries $releaseRoot
Assert-NoSecretText $releaseRoot

$zipName = "$($mod.name)-$($mod.version_number)-direct-install.zip"
$zipPath = Join-Path $distRoot $zipName
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$items = Get-ChildItem -LiteralPath $releaseRoot -Force
Compress-Archive -Path $items.FullName -DestinationPath $zipPath -CompressionLevel Optimal
Assert-ZipRoot $zipPath

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
$hashText = "$($hash.Hash.ToLowerInvariant())  $zipName"
$hashPath = Join-Path $distRoot "$zipName.sha256.txt"
Write-Utf8NoBom $hashPath ($hashText + [Environment]::NewLine)

$releaseNotes = @"
# COTL Characters $($mod.version_number)

Hotfix release for Cult of the Lamb.

## Fixed

- AI provider setup no longer stores pasted API keys in the mod manager profile.
- Generated and packaged provider setup command/script files were removed from the release.
- Fixed the AI Cult About editor so the input text is readable while typing.
- Removed the extra full-screen blocker layer from the About overlay that could appear as a dark layer over the editor.
- Kept the About editor on top of the game UI while it is open.

## Action Recommended

If you used an older release and have BepInEx/config/COTL_AL_NPCs/AiProviderKey.txt, delete that file before sharing or syncing a profile. Rotate the provider key if that profile was already shared.

## Download

Download and extract:

- $zipName

Optional checksum:

- $zipName.sha256.txt

## Install

1. Install BepInExPack CultOfTheLamb 5.4.2101 and COTL_API 0.3.3 separately in your modded Cult of the Lamb profile.
2. Download $zipName from this release.
3. Right-click the zip and choose Extract All.
4. Open the extracted folder.
5. Double-click Install_COTL_Characters.cmd.
6. Choose your Thunderstore or r2modman profile folder.
7. Launch Cult of the Lamb from that same profile.

## Notes

- No AI provider keys are included.
- Pasted provider keys are saved to the user's Windows environment variable instead of a profile-local key file.
- The in-game AI Provider Setup prompt guides provider/model setup.
- .NET 10 runtime is required for the included sidecar process.
- This release includes an AI-assisted creation disclosure in README.md and assembly metadata.

## SHA256

$hashText
"@
Write-Utf8NoBom (Join-Path $distRoot "GITHUB_RELEASE_NOTES_$($mod.version_number).md") ($releaseNotes + [Environment]::NewLine)

Write-Host "GitHub release folder: $releaseRoot"
Write-Host "GitHub release zip: $zipPath"
Write-Host "SHA256: $hashPath"
