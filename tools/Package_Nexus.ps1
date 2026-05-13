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
        throw "Required Nexus package file is missing: $path"
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
            throw "Forbidden development entry included in Nexus package: $($entry.FullName)"
        }
        if ($forbiddenDependencyFiles -contains $entry.Name) {
            throw "Do not bundle external dependency/game/framework assembly in Nexus package: $($entry.FullName)"
        }
        if ($entry.Name -match "\.(env|user|suo|tmp|cache)$") {
            throw "Forbidden temp/secret-like file included in Nexus package: $($entry.FullName)"
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
                throw "Potential secret-like text found in Nexus package file: $($file.FullName)"
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
            "plugins/COTL_AL_NPCs/COTL_AL_NPCs.dll",
            "plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.dll",
            "plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.deps.json",
            "plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.runtimeconfig.json")) {
            if ($entryNames -notcontains $required) {
                throw "Nexus zip root is missing required file: $required"
            }
        }
        if ($entryNames | Where-Object { $_ -like "nexus/*" -or $_ -like "dist/*" -or $_ -like "COTL_Characters-*/*" }) {
            throw "Nexus zip contains an extra parent output folder."
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
$nexusRoot = Join-Path $distRoot "nexus"

if (-not $SkipBuild) {
    & (Join-Path $ProductRoot "tools\Assemble_Product_Build.ps1") -ManifestPath $manifestFullPath
    if ($LASTEXITCODE -ne 0) {
        throw "Character-only product assembly failed."
    }
}

Reset-DirectoryInsideProduct $nexusRoot
Copy-RequiredTree $packageRoot $nexusRoot
if (Test-Path -LiteralPath (Join-Path $nexusRoot "BepInEx")) {
    New-Item -ItemType Directory -Force -Path (Join-Path $nexusRoot "plugins") | Out-Null
    Copy-Item -Path (Join-Path $nexusRoot "BepInEx\plugins\*") -Destination (Join-Path $nexusRoot "plugins") -Recurse -Force
    Remove-Item -LiteralPath (Join-Path $nexusRoot "BepInEx") -Recurse -Force
}

if (Test-Path -LiteralPath (Join-Path $nexusRoot "manifest.json")) {
    Remove-Item -LiteralPath (Join-Path $nexusRoot "manifest.json") -Force
}

$sidecarExe = Join-Path $nexusRoot "plugins\COTL_AL_NPCs\sidecar\CotlAiNpcSidecar.exe"
if (Test-Path -LiteralPath $sidecarExe) {
    Remove-Item -LiteralPath $sidecarExe -Force
}

foreach ($scriptPath in @(
    (Join-Path $nexusRoot "Configure_AI_Provider.cmd"),
    (Join-Path $nexusRoot "tools\Configure_AI_Provider.ps1"))) {
    if (Test-Path -LiteralPath $scriptPath) {
        Remove-Item -LiteralPath $scriptPath -Force
    }
}

$emptyToolsDir = Join-Path $nexusRoot "tools"
if (Test-Path -LiteralPath $emptyToolsDir) {
    $remainingTools = @(Get-ChildItem -LiteralPath $emptyToolsDir -Force -ErrorAction SilentlyContinue)
    if ($remainingTools.Count -eq 0) {
        Remove-Item -LiteralPath $emptyToolsDir -Force
    }
}

Copy-Item -LiteralPath ([System.IO.Path]::GetFullPath($IconSource)) -Destination (Join-Path $nexusRoot "COTL_Characters.png") -Force

$readme = @"
# COTL Characters

COTL Characters adds AI-powered Character Mode conversations to Cult of the Lamb followers.

This Nexus package is for manual installation into a modded BepInEx profile. It does not include Cult of the Lamb, BepInEx, COTL_API, game assemblies, provider accounts, or API keys.

This Nexus build intentionally does not include installer scripts. It is a manual-install archive to keep the upload simple and transparent.

## Requirements

- Windows.
- Cult of the Lamb.
- BepInExPack CultOfTheLamb 5.4.2101, installed separately.
- COTL_API 0.3.3, installed separately.
- .NET 10 runtime for the included sidecar process.
- An AI provider key or local AI endpoint, unless you only use local providers that do not require a key.

## Quick Install

1. Install Cult of the Lamb.
2. Install BepInEx and COTL_API separately.
3. Download and extract this archive.
4. Open the plugins folder.
5. Copy the COTL_AL_NPCs folder.
6. Paste it into your modded profile's BepInEx/plugins folder.
7. Start the game from that same profile.

## Manual Install

1. Extract this archive.
2. Open the plugins folder.
3. Copy the COTL_AL_NPCs folder.
4. Paste it into your modded profile's BepInEx/plugins folder.
5. Launch Cult of the Lamb from that same profile.

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

## AI-Assisted Creation Disclosure

This mod and Nexus package were partially created with the assistance of Generative AI for code changes, refactoring, documentation, and release packaging.

AI model vendor used: OpenAI.

## Credits

- Deamon_Blue
"@
Write-Utf8NoBom (Join-Path $nexusRoot "README.md") ($readme + [Environment]::NewLine)

$install = @"
# COTL Characters Install Guide

This is the manual install path:

1. Install BepInEx and COTL_API separately in your Cult of the Lamb modded profile.
2. Extract this archive.
3. Open the plugins folder.
4. Copy the COTL_AL_NPCs folder.
5. Paste it into your modded profile's BepInEx/plugins folder.
6. Launch the game from that same profile.

## Picking the right folder

Choose the profile folder that contains BepInEx. Common examples:

- Thunderstore Mod Manager profile folder
- r2modman profile folder
- A manual Cult of the Lamb folder that already has BepInEx installed

You are copying:

BepInEx/plugins/COTL_AL_NPCs/

into the selected profile.

## After install

Use the in-game AI Provider Setup prompt. No provider keys, BepInEx files, COTL_API files, game files, or installer scripts are included in this release.
"@
Write-Utf8NoBom (Join-Path $nexusRoot "INSTALL.md") ($install + [Environment]::NewLine)

$changelog = @"
# Changelog

## $($mod.version_number)

- Fixed world-state sanitation context so a full in-game sanitation gauge is described as clean instead of hazardous/dirty.
"@
Write-Utf8NoBom (Join-Path $nexusRoot "CHANGELOG.md") ($changelog + [Environment]::NewLine)

$pageDescription = @"
# COTL Characters

AI-powered Character Mode conversations for Cult of the Lamb followers.

## Version $($mod.version_number)

- Fixed world-state sanitation context so a full in-game sanitation gauge is described as clean instead of hazardous/dirty.

## Requirements

- Cult of the Lamb
- BepInExPack CultOfTheLamb 5.4.2101
- COTL_API 0.3.3
- .NET 10 runtime
- An AI provider key or local AI endpoint

BepInEx, COTL_API, game files, and API keys are not included in this upload.

## Install

1. Install BepInEx and COTL_API separately.
2. Download the main file from Nexus.
3. Extract the archive.
4. Copy plugins/COTL_AL_NPCs into your profile's BepInEx/plugins folder.
5. Launch the game from that same profile.

Manual install: copy plugins/COTL_AL_NPCs into your profile's BepInEx/plugins folder.

## Features

- Character Mode follower conversations.
- Per-character awareness settings for traits, cult details, current events, world state, lore, and long-term chat memory.
- Short, Medium, and Long reply length controls.
- Player-written lore for individual characters.
- Tournament Ledger support.
- Invocations.
- AI provider setup for OpenAI, compatible endpoints, Anthropic, Gemini, LM Studio, and Ollama-compatible endpoints.
- Optional internet access for eligible replies.

## Keyboard Controls

- F7 while paused: Invocations menu.
- F8 while paused: Tournament Ledger.
- F8 while unpaused: current tournament match overlay.
- F9 while paused: Internet Access panel.

## AI-Assisted Creation Disclosure

This mod and Nexus package were partially created with the assistance of Generative AI for code changes, refactoring, documentation, and release packaging.

AI model vendor used: OpenAI.
"@
Write-Utf8NoBom (Join-Path $distRoot "NEXUS_DESCRIPTION_$($mod.version_number).md") ($pageDescription + [Environment]::NewLine)

Assert-FileExists (Join-Path $nexusRoot "README.md")
Assert-FileExists (Join-Path $nexusRoot "INSTALL.md")
Assert-FileExists (Join-Path $nexusRoot "CHANGELOG.md")
Assert-FileExists (Join-Path $nexusRoot "plugins\COTL_AL_NPCs\COTL_AL_NPCs.dll")
Assert-FileExists (Join-Path $nexusRoot "plugins\COTL_AL_NPCs\sidecar\CotlAiNpcSidecar.dll")
Assert-FileExists (Join-Path $nexusRoot "plugins\COTL_AL_NPCs\sidecar\CotlAiNpcSidecar.deps.json")
Assert-FileExists (Join-Path $nexusRoot "plugins\COTL_AL_NPCs\sidecar\CotlAiNpcSidecar.runtimeconfig.json")

foreach ($file in @("README.md", "INSTALL.md", "CHANGELOG.md", "README_SETUP.txt")) {
    Assert-StrictUtf8TextFile (Join-Path $nexusRoot $file)
}

Assert-NoForbiddenEntries $nexusRoot
Assert-NoSecretText $nexusRoot

$zipName = "$($mod.name)-$($mod.version_number)-nexus.zip"
$zipPath = Join-Path $distRoot $zipName
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$items = Get-ChildItem -LiteralPath $nexusRoot -Force
Compress-Archive -Path $items.FullName -DestinationPath $zipPath -CompressionLevel Optimal
Assert-ZipRoot $zipPath

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
$hashText = "$($hash.Hash.ToLowerInvariant())  $zipName"
$hashPath = Join-Path $distRoot "$zipName.sha256.txt"
Write-Utf8NoBom $hashPath ($hashText + [Environment]::NewLine)

Write-Host "Nexus package folder: $nexusRoot"
Write-Host "Nexus package zip: $zipPath"
Write-Host "SHA256: $hashPath"
Write-Host "Nexus page description: $(Join-Path $distRoot "NEXUS_DESCRIPTION_$($mod.version_number).md")"
