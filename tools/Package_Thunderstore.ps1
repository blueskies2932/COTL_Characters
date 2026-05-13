param(
    [string]$ManifestPath = (Join-Path $PSScriptRoot "..\product_build_manifest.json"),
    [string]$IconSource = (Join-Path $PSScriptRoot "..\COTL_Characters.png"),
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Resolve-ProductPath([string]$path) {
    return [System.IO.Path]::GetFullPath((Join-Path $ProductRoot $path))
}

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

function Copy-RequiredTree([string]$source, [string]$destination) {
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required package source is missing: $source"
    }
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -Path (Join-Path $source "*") -Destination $destination -Recurse -Force
}

function Write-Utf8NoBom([string]$path, [string]$content) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
}

function Resize-IconPng([string]$source, [string]$destination) {
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Thunderstore icon source is missing: $source"
    }

    Add-Type -AssemblyName System.Drawing
    $sourceImage = [System.Drawing.Image]::FromFile($source)
    try {
        $bitmap = New-Object System.Drawing.Bitmap 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.DrawImage($sourceImage, 0, 0, 256, 256)
            }
            finally {
                $graphics.Dispose()
            }
            $bitmap.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $bitmap.Dispose()
        }
    }
    finally {
        $sourceImage.Dispose()
    }
}

function Assert-IconPng256([string]$path) {
    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($path)
    try {
        if ($image.Width -ne 256 -or $image.Height -ne 256) {
            throw "icon.png must be 256x256. Actual size: $($image.Width)x$($image.Height)"
        }
    }
    finally {
        $image.Dispose()
    }
}

function Assert-FileExists([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required Thunderstore package file is missing: $path"
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
    try {
        $null = $strictUtf8.GetString($bytes)
    }
    catch {
        throw "Text file is not valid strict UTF-8: $path"
    }
}

function Assert-NoForbiddenEntries([string]$root) {
    $forbiddenNames = @("bin", "obj", ".git", ".vs", ".idea", "__pycache__")
    foreach ($entry in Get-ChildItem -LiteralPath $root -Recurse -Force) {
        if ($forbiddenNames -contains $entry.Name) {
            throw "Forbidden development entry included in Thunderstore package: $($entry.FullName)"
        }
        if ($entry.Name -match "\.(env|user|suo|tmp|cache)$") {
            throw "Forbidden temp/secret-like file included in Thunderstore package: $($entry.FullName)"
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
                throw "Potential secret-like text found in package file: $($file.FullName)"
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
        foreach ($required in @("manifest.json", "README.md", "CHANGELOG.md", "icon.png")) {
            if ($entryNames -notcontains $required) {
                throw "Zip root is missing required file: $required"
            }
        }
        if (-not ($entryNames | Where-Object { $_ -eq "BepInEx/plugins/COTL_AL_NPCs/COTL_AL_NPCs.dll" })) {
            throw "Zip is missing BepInEx/plugins/COTL_AL_NPCs/COTL_AL_NPCs.dll"
        }
        foreach ($forbidden in @("tools/", "guides/", "config_templates/", "Configure_AI_Provider.cmd")) {
            if ($entryNames | Where-Object { $_ -like "$forbidden*" }) {
                throw "Zip contains Thunderstore-unsafe or obsolete path: $forbidden"
            }
        }
        if ($entryNames | Where-Object { $_ -like "thunderstore/*" -or $_ -like "dist/*" }) {
            throw "Zip contains an extra parent output folder."
        }
    }
    finally {
        $zip.Dispose()
    }
}

$manifestFullPath = [System.IO.Path]::GetFullPath($ManifestPath)
$ProductRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $manifestFullPath))
$manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
$packageRoot = Join-Path $ProductRoot $manifest.package_name
$distRoot = Join-Path $ProductRoot "dist"
$thunderstoreRoot = Join-Path $distRoot "thunderstore"

if (-not $SkipBuild) {
    & (Join-Path $ProductRoot "tools\Assemble_Product_Build.ps1") -ManifestPath $manifestFullPath
    if ($LASTEXITCODE -ne 0) {
        throw "Character-only product assembly failed."
    }
}

Reset-DirectoryInsideProduct $thunderstoreRoot

Copy-RequiredTree $packageRoot $thunderstoreRoot

foreach ($removePath in @(
    (Join-Path $thunderstoreRoot "README_SETUP.txt"),
    (Join-Path $thunderstoreRoot "PACKAGE_CONTENTS.txt"),
    (Join-Path $thunderstoreRoot "guides"),
    (Join-Path $thunderstoreRoot "config_templates"),
    (Join-Path $thunderstoreRoot "Configure_AI_Provider.cmd"),
    (Join-Path $thunderstoreRoot "tools"))) {
    if (Test-Path -LiteralPath $removePath) {
        Remove-Item -LiteralPath $removePath -Recurse -Force
    }
}

$mod = $manifest.mod
$dependencies = @(
    "BepInEx-BepInExPack_CultOfTheLamb-5.4.2101",
    "xhayper-COTL_API-0.3.3"
)

$thunderstoreManifest = [ordered]@{
    name = $mod.name
    version_number = $mod.version_number
    website_url = $mod.website_url
    description = $mod.description
    dependencies = $dependencies
}
Write-Utf8NoBom (Join-Path $thunderstoreRoot "manifest.json") (($thunderstoreManifest | ConvertTo-Json -Depth 6) + [Environment]::NewLine)

$readme = @"
# COTL Characters

Give your Cult of the Lamb followers configurable AI-powered character conversations, memory, lore, and cult-aware reactions.

## Features

- Talk directly with Character Mode followers from the in-game conversation UI.
- Let followers respond with awareness of traits, cult details, current events, world state, custom lore, and optional long-term chat memory.
- Choose Short, Medium, or Long reply lengths per character.
- Add player-written lore to individual characters.
- Track tournament matchups, entrant status, and champion history.
- Use included character-safe invocations for cult faith and vanilla role cleanup.
- Configure OpenAI, OpenAI-compatible, Anthropic, Gemini, LM Studio, or Ollama-compatible providers.
- Optionally enable internet access for eligible character replies.

## Installation

Install through Thunderstore Mod Manager or r2modman. The package places the plugin under BepInEx/plugins/COTL_AL_NPCs/ so the mod manager installs it into the profile's BepInEx plugins folder.

## Controls

- F7 while paused: open or close the Invocations menu.
- F8 while paused: open or close the full Tournament Ledger.
- F8 while unpaused: show or hide the current tournament match overlay.
- F9 while paused: open or close the Internet Access panel.

## Configuration

Users provide their own AI provider settings. No API keys are included with the mod, and you should never share files containing your own provider key.

The preferred setup path is the in-game AI Provider Setup prompt. Choose a provider preset, enter your provider details, then use Find, Test & Save Setup. The setup prompt remains visible until a provider and model have passed validation.

Manual configuration uses:

BepInEx/config/COTL_AL_NPCs/AiProvider.json

This hotfix no longer stores pasted provider keys in the mod manager profile. If you used an older release and have BepInEx/config/COTL_AL_NPCs/AiProviderKey.txt, delete that file before sharing or syncing a profile. Rotate the provider key if that profile was already shared.

To reset or change provider setup manually: close the game, open BepInEx/config/COTL_AL_NPCs/ in the modded profile, delete AiProvider.json, AiProviderKey.txt, and LAST_PROVIDER_SETUP_TEST.txt if present, then update or delete the matching Windows user environment variable such as OPENAI_API_KEY, OPENROUTER_API_KEY, ANTHROPIC_API_KEY, GEMINI_API_KEY, or AI_PROVIDER_API_KEY.

## AI Provider Setup

Supported provider styles include:

- OpenAI Responses or Chat Completions
- OpenAI-compatible API endpoints
- Anthropic Claude
- Google Gemini
- LM Studio
- Ollama-compatible local endpoints

The sidecar runtime is included in plugins/COTL_AL_NPCs/sidecar/ and is used for provider requests.

## Compatibility

- Cult of the Lamb on Windows
- BepInExPack CultOfTheLamb 5.4.2101
- COTL_API 0.3.3
- .NET 10 runtime for the included sidecar process

## Troubleshooting

- Make sure BepInEx is installed through the mod manager.
- Make sure the mod DLL is enabled in the profile.
- Make sure COTL_API is installed in the same profile.
- If the AI Provider Setup prompt appears, finish provider setup before expecting character replies.
- Check BepInEx logs for plugin errors.
- Check that the sidecar files exist under BepInEx/plugins/COTL_AL_NPCs/sidecar/ after installation.
- Check that AI provider settings are valid.
- Never paste someone else's API key.

## AI-Assisted Creation Disclosure

This mod and Thunderstore package were partially created with the assistance of Generative AI for code changes, refactoring, documentation, and release packaging.

AI model vendor used: OpenAI.

## Credits

- Deamon_Blue
"@
Write-Utf8NoBom (Join-Path $thunderstoreRoot "README.md") ($readme + [Environment]::NewLine)

$changelog = @"
# Changelog

## $($mod.version_number)

- Security hotfix: AI provider setup no longer stores pasted API keys in the mod manager profile.
- Removed generated and packaged provider setup command/script files from the release.
- Existing users should delete BepInEx/config/COTL_AL_NPCs/AiProviderKey.txt before sharing or syncing a profile.
- Initial character-only Thunderstore release candidate.
- Added Character Mode follower conversations.
- Added per-character awareness and reply length controls.
- Added Tournament Ledger support.
- Added Invocations.
- Added user-configurable AI provider setup.
- Added sidecar runtime packaging.
"@
Write-Utf8NoBom (Join-Path $thunderstoreRoot "CHANGELOG.md") ($changelog + [Environment]::NewLine)

Resize-IconPng ([System.IO.Path]::GetFullPath($IconSource)) (Join-Path $thunderstoreRoot "icon.png")

Assert-FileExists (Join-Path $thunderstoreRoot "manifest.json")
Assert-FileExists (Join-Path $thunderstoreRoot "README.md")
Assert-FileExists (Join-Path $thunderstoreRoot "CHANGELOG.md")
Assert-FileExists (Join-Path $thunderstoreRoot "icon.png")
Assert-FileExists (Join-Path $thunderstoreRoot "BepInEx\plugins\COTL_AL_NPCs\COTL_AL_NPCs.dll")
Assert-FileExists (Join-Path $thunderstoreRoot "BepInEx\plugins\COTL_AL_NPCs\sidecar\CotlAiNpcSidecar.exe")
Assert-FileExists (Join-Path $thunderstoreRoot "BepInEx\plugins\COTL_AL_NPCs\sidecar\CotlAiNpcSidecar.dll")
Assert-StrictUtf8TextFile (Join-Path $thunderstoreRoot "README.md")
Assert-StrictUtf8TextFile (Join-Path $thunderstoreRoot "CHANGELOG.md")
Assert-IconPng256 (Join-Path $thunderstoreRoot "icon.png")
Assert-NoForbiddenEntries $thunderstoreRoot
Assert-NoSecretText $thunderstoreRoot

$parsedManifest = Get-Content -LiteralPath (Join-Path $thunderstoreRoot "manifest.json") -Raw | ConvertFrom-Json
if ($parsedManifest.name -notmatch "^[A-Za-z0-9_]+$") {
    throw "Thunderstore manifest name must contain only letters, numbers, and underscores."
}
if ($parsedManifest.version_number -notmatch "^\d+\.\d+\.\d+$") {
    throw "Thunderstore manifest version_number must use semantic versioning."
}
if (@($parsedManifest.dependencies) -notcontains "BepInEx-BepInExPack_CultOfTheLamb-5.4.2101") {
    throw "Thunderstore manifest is missing required BepInEx dependency."
}

$zipName = "$($parsedManifest.name)-$($parsedManifest.version_number).zip"
$zipPath = Join-Path $distRoot $zipName
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$items = Get-ChildItem -LiteralPath $thunderstoreRoot -Force
Compress-Archive -Path $items.FullName -DestinationPath $zipPath -CompressionLevel Optimal
Assert-ZipRoot $zipPath

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
$hashText = "$($hash.Hash.ToLowerInvariant())  $zipName"
$hashPath = Join-Path $distRoot "$zipName.sha256.txt"
Write-Utf8NoBom $hashPath ($hashText + [Environment]::NewLine)

Write-Host "Thunderstore package folder: $thunderstoreRoot"
Write-Host "Thunderstore zip: $zipPath"
Write-Host "SHA256: $hashPath"
