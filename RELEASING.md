# Releasing COTL Characters

This folder is a GitHub-ready staging copy for `COTL_Characters`.

## First GitHub Release

1. Create a public GitHub repository named `COTL_Characters` under `blueskies2932`.
2. Upload or commit the contents of this `github_repo` folder.
3. Create a GitHub Release with tag `v0.1.0`.
4. Use the contents of `release-assets/v0.1.0/GITHUB_RELEASE_NOTES_0.1.0.md` as the release notes.
5. Attach these files to the release:
   - `release-assets/v0.1.0/COTL_Characters-0.1.0-direct-install.zip`
   - `release-assets/v0.1.0/COTL_Characters-0.1.0-direct-install.zip.sha256.txt`

The release zip is the user-facing installer. Players should not need to clone the repo.

## Regenerate Release Assets

From the local COTL workspace root:

```powershell
.\COTL_AL_NPCs\Character_only_product_build\tools\Package_GitHub_Release.ps1
```

After regenerating, copy the new zip, checksum, and release notes into `release-assets/v<version>/`.

## Stable IDs

- GitHub repo owner: `blueskies2932`
- Public author name: `Deamon_Blue`
- BepInEx plugin GUID: `io.github.blueskies2932.COTL_Characters`
- Plugin display name: `COTL Characters`

Changing the BepInEx GUID after release can affect user config identity, so keep it stable.
