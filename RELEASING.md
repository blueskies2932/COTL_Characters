# Releasing COTL Characters

This repository is the source of truth for public COTL Characters releases.

## Everyday Fix Workflow

Use this flow for bug fixes after a version has already shipped:

1. Make the source change in `source/`.
2. Add one bullet under `## Unreleased` in `CHANGELOG.md`.
3. Build the changed project:

```powershell
dotnet build source\plugin\COTL_AL_NPCs.Product.csproj /p:DefineConstants=PRODUCT_BUILD /p:OutputPath=..\..\build\plugin\net472\
dotnet build source\sidecar\CotlAiNpcSidecar.csproj /p:DefineConstants=PRODUCT_BUILD /p:OutputPath=..\..\build\sidecar\net10.0\
```

4. If the game is closed, copy the fixed plugin DLL into the local test install:

```powershell
Copy-Item -LiteralPath .\build\plugin\net472\COTL_AL_NPCs.dll -Destination "C:\Program Files (x86)\Steam\steamapps\common\Cult of the Lamb\BepInEx\plugins\COTL_AL_NPCs\COTL_AL_NPCs.dll" -Force
```

5. Launch the game, reproduce the old behavior, and check the live diagnostics/logs.
6. Commit the fix once it passes local testing.

Do not edit generated release zips by hand. Rebuild packages from the scripts below.

## Before A Public Update

1. Pick the next version number.
2. Move the `## Unreleased` bullets in `CHANGELOG.md` under the new version heading.
3. Update plugin/product version metadata.
4. Run all package scripts from this repo root:

```powershell
.\tools\Package_GitHub_Release.ps1
.\tools\Package_Nexus.ps1
.\tools\Package_Thunderstore.ps1
```

5. Verify generated files in `dist\` and `release-assets\v<version>\`.
6. Upload the matching artifacts to GitHub, Nexus, and Thunderstore.
7. Commit the source, docs, and release assets together.

## Release Artifacts

- GitHub direct install: `dist\COTL_Characters-<version>-direct-install.zip`
- Nexus upload: `dist\COTL_Characters-<version>-nexus.zip`
- Thunderstore upload: `dist\COTL_Characters-<version>.zip`
- Long-term copies: `release-assets\v<version>\`

## Stable IDs

- GitHub repo owner: `blueskies2932`
- Public author name: `Deamon_Blue`
- BepInEx plugin GUID: `io.github.blueskies2932.COTL_Characters`
- Plugin display name: `COTL Characters`

Changing the BepInEx GUID after release can affect user config identity, so keep it stable.
