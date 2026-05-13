# COTL Characters 0.1.3

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

- COTL_Characters-0.1.3-direct-install.zip

Optional checksum:

- COTL_Characters-0.1.3-direct-install.zip.sha256.txt

## Install

1. Install BepInExPack CultOfTheLamb 5.4.2101 and COTL_API 0.3.3 separately in your modded Cult of the Lamb profile.
2. Download COTL_Characters-0.1.3-direct-install.zip from this release.
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

855b88d544cc0c3458cc52d693c3bbff17290b5b1de589307078cab4d9843ae9  COTL_Characters-0.1.3-direct-install.zip
