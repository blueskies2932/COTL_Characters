param(
    [string]$ManifestPath = (Join-Path $PSScriptRoot "..\product_build_manifest.json"),
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Resolve-RepoPath([string]$path) {
    return [System.IO.Path]::GetFullPath((Join-Path (Join-Path $PSScriptRoot "..") $path))
}

function Copy-RequiredFile([string]$source, [string]$destination) {
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required product file missing: $source"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

function Reset-ProductPackage([string]$packageRoot, [string]$productBuildRoot) {
    $fullPackageRoot = [System.IO.Path]::GetFullPath($packageRoot)
    $fullProductBuildRoot = [System.IO.Path]::GetFullPath($productBuildRoot)
    if (-not $fullPackageRoot.StartsWith($fullProductBuildRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean package outside product_build: $fullPackageRoot"
    }
    if ($fullPackageRoot -eq $fullProductBuildRoot) {
        throw "Refusing to clean product_build root."
    }
    if (Test-Path -LiteralPath $fullPackageRoot) {
        Remove-Item -LiteralPath $fullPackageRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $fullPackageRoot | Out-Null
}

$manifestFullPath = [System.IO.Path]::GetFullPath($ManifestPath)
$productBuildRoot = Split-Path -Parent $manifestFullPath
$manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
$packageRoot = Join-Path $productBuildRoot $manifest.package_name
$fragmentsRoot = Join-Path $productBuildRoot "fragments"
$buildRoot = Join-Path $productBuildRoot "build"
$pluginOutputDir = Join-Path $buildRoot "plugin\net472"
$sidecarOutputDir = Join-Path $buildRoot "sidecar\net10.0"

if (-not $SkipBuild) {
    $pluginProject = Resolve-RepoPath $manifest.build.plugin_project
    New-Item -ItemType Directory -Force -Path $pluginOutputDir | Out-Null
    dotnet build $pluginProject "/p:DefineConstants=$($manifest.build.product_define)" "/p:OutputPath=$pluginOutputDir\"
    if ($LASTEXITCODE -ne 0) { throw "Product plugin build failed." }

    $sidecarProject = Join-Path $productBuildRoot $manifest.build.sidecar_project
    New-Item -ItemType Directory -Force -Path $sidecarOutputDir | Out-Null
    dotnet build $sidecarProject "/p:DefineConstants=$($manifest.build.product_define)" "/p:OutputPath=$sidecarOutputDir\"
    if ($LASTEXITCODE -ne 0) { throw "Product sidecar build failed." }
}

Reset-ProductPackage $packageRoot $productBuildRoot
New-Item -ItemType Directory -Force -Path (Join-Path $packageRoot "BepInEx\plugins\COTL_AL_NPCs\sidecar") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $packageRoot "guides") | Out-Null

$manifestJson = $manifest.mod | ConvertTo-Json -Depth 8
Set-Content -LiteralPath (Join-Path $packageRoot "manifest.json") -Value $manifestJson -Encoding UTF8

$pluginOutput = Resolve-RepoPath $manifest.build.plugin_output
Copy-RequiredFile $pluginOutput (Join-Path $packageRoot "BepInEx\plugins\COTL_AL_NPCs\COTL_AL_NPCs.dll")

$sidecarOutput = Join-Path $productBuildRoot $manifest.build.sidecar_output
foreach ($fileName in @("CotlAiNpcSidecar.exe", "CotlAiNpcSidecar.dll", "CotlAiNpcSidecar.deps.json", "CotlAiNpcSidecar.runtimeconfig.json")) {
    Copy-RequiredFile (Join-Path $sidecarOutput $fileName) (Join-Path $packageRoot "BepInEx\plugins\COTL_AL_NPCs\sidecar\$fileName")
}

foreach ($guide in $manifest.always_include_guides) {
    Copy-RequiredFile (Join-Path $fragmentsRoot "guides\$guide") (Join-Path $packageRoot "guides\$guide")
}

foreach ($feature in $manifest.promoted_features) {
    if ($feature.guide) {
        Copy-RequiredFile (Join-Path $fragmentsRoot "guides\$($feature.guide)") (Join-Path $packageRoot "guides\$($feature.guide)")
    }
    foreach ($template in @($feature.config_templates) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
        Copy-RequiredFile (Join-Path $fragmentsRoot "config_templates\$template") (Join-Path $packageRoot "config_templates\$template")
    }
    foreach ($tool in @($feature.tools) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
        Copy-RequiredFile (Join-Path $fragmentsRoot $tool) (Join-Path $packageRoot $tool)
    }
}

$readmeSource = Join-Path $fragmentsRoot "README_SETUP.txt"
if (Test-Path -LiteralPath $readmeSource) {
    Copy-RequiredFile $readmeSource (Join-Path $packageRoot "README_SETUP.txt")
}

$packageContents = @()
$packageContents += "Package Contents"
$packageContents += "================"
$packageContents += ""
$packageContents += "Runtime:"
$packageContents += ""
$packageContents += "- BepInEx/plugins/COTL_AL_NPCs/COTL_AL_NPCs.dll"
$packageContents += "- BepInEx/plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.exe"
$packageContents += "- BepInEx/plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.dll"
$packageContents += "- BepInEx/plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.deps.json"
$packageContents += "- BepInEx/plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.runtimeconfig.json"
$packageContents += ""
$packageContents += "Metadata:"
$packageContents += ""
$packageContents += "- manifest.json"
if (Test-Path -LiteralPath (Join-Path $packageRoot "Configure_AI_Provider.cmd")) {
    $packageContents += "- Configure_AI_Provider.cmd"
}
if (Test-Path -LiteralPath (Join-Path $packageRoot "tools\Configure_AI_Provider.ps1")) {
    $packageContents += "- tools/Configure_AI_Provider.ps1"
}
$packageContents += ""
$packageContents += "Config Templates:"
$packageContents += ""
foreach ($template in Get-ChildItem -File (Join-Path $packageRoot "config_templates") -ErrorAction SilentlyContinue | Sort-Object Name) {
    $packageContents += "- config_templates/$($template.Name)"
}
$packageContents += ""
$packageContents += "Guides:"
$packageContents += ""
$packageContents += "- README_SETUP.txt"
foreach ($guideFile in Get-ChildItem -File (Join-Path $packageRoot "guides") | Sort-Object Name) {
    $packageContents += "- guides/$($guideFile.Name)"
}
$packageContents += ""
$packageContents += "Not Included:"
$packageContents += ""
$packageContents += "- AI provider API keys."
$packageContents += "- Local save data."
$packageContents += "- Local config files."
$packageContents += "- Development diagnostics and trace reports."
$packageContents += "- Private development tools."
Set-Content -LiteralPath (Join-Path $packageRoot "PACKAGE_CONTENTS.txt") -Value ($packageContents -join [Environment]::NewLine) -Encoding UTF8

$scanText = ""
foreach ($file in Get-ChildItem -Recurse -File $packageRoot | Where-Object { $_.Extension -in ".txt", ".json", ".cmd", ".ps1" }) {
    $scanText += "`n$($file.FullName)`n"
    $scanText += Get-Content -LiteralPath $file.FullName -Raw
}
foreach ($term in $manifest.excluded_product_terms) {
    if ($scanText.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Excluded product term found after assembly: $term"
    }
}

Write-Host "Product package assembled: $packageRoot"
