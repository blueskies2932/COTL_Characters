# COTL Characters

COTL Characters adds AI-powered Character Mode conversations to Cult of the Lamb followers.

This repo is prepared for GitHub distribution. Most players should install from the latest GitHub Release rather than building from source.

## One-Click Install

1. Install Cult of the Lamb.
2. Install a modded profile with BepInEx and COTL_API separately.
3. Open this repo's Releases page.
4. Download `COTL_Characters-0.1.5-direct-install.zip`.
5. Right-click the zip and choose Extract All.
6. Open the extracted folder.
7. Double-click `Install_COTL_Characters.cmd`.
8. Choose your Thunderstore or r2modman profile folder when asked.
9. Start the game from that same modded profile.

No API keys are included. BepInEx, COTL_API, and game files are not included. The first launch guides you through AI provider setup in game.

## Requirements

- Windows.
- Cult of the Lamb.
- BepInExPack CultOfTheLamb 5.4.2101, installed separately.
- COTL_API 0.3.3, installed separately.
- .NET 10 runtime for the included sidecar process.
- An AI provider key or local AI endpoint, unless you only use local providers that do not require a key.

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

```text
BepInEx/config/COTL_AL_NPCs/AiProvider.json
```

This hotfix no longer stores pasted provider keys in the mod manager profile. If you used an older release and have `BepInEx/config/COTL_AL_NPCs/AiProviderKey.txt`, delete that file before sharing or syncing a profile. Rotate the provider key if that profile was already shared.

## Reset or Change API Key

Use the Reset button in the in-game AI Provider Setup panel when it is visible.

Manual reset:

1. Close the game.
2. Open your modded profile folder.
3. Open `BepInEx/config/COTL_AL_NPCs/`.
4. Delete `AiProvider.json`, `AiProviderKey.txt`, and `LAST_PROVIDER_SETUP_TEST.txt` if they exist.
5. Update or delete the matching Windows user environment variable: `OPENAI_API_KEY`, `OPENROUTER_API_KEY`, `ANTHROPIC_API_KEY`, `GEMINI_API_KEY`, or `AI_PROVIDER_API_KEY`.
6. Relaunch the game. The AI Provider Setup panel should appear again.

Thunderstore/r2modman profiles are usually under `Thunderstore Mod Manager/DataFolder/CultOfTheLamb/profiles/<ProfileName>/` or `r2modmanPlus-local/CultOfTheLamb/profiles/<ProfileName>/`.

## Build From Source

This source tree is the character-only product build. The release package is assembled from this folder and does not depend on files outside it.

From the workspace root used during development:

```powershell
.\COTL_AL_NPCs\Character_only_product_build\tools\Package_GitHub_Release.ps1
```

The script builds the plugin and sidecar, creates the direct-install package, and writes the release zip plus checksum under:

```text
COTL_AL_NPCs/Character_only_product_build/dist/
```

The project currently builds against local Cult of the Lamb and Thunderstore profile assemblies. If you clone this on another PC, update the assembly reference paths in `source/plugin/COTL_AL_NPCs.Product.csproj` or install the same local profile layout.

## AI-Assisted Creation Disclosure

This mod and GitHub release package were partially created with the assistance of Generative AI for code changes, refactoring, documentation, and release packaging.

AI model vendor used: OpenAI.

## License

This project is released under the MIT License. Cult of the Lamb, BepInEx, COTL_API, and other dependencies remain owned by their respective authors.

## Credits

- Deamon_Blue


