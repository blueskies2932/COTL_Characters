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

## Reset or Change API Key

Use the Reset button in the in-game AI Provider Setup panel when it is visible.

Manual reset:

1. Close the game.
2. Open your modded profile folder.
3. Open `BepInEx/config/COTL_AL_NPCs/`.
4. Delete `AiProvider.json`, `AiProviderKey.txt`, and `LAST_PROVIDER_SETUP_TEST.txt` if they exist.
5. Update or delete the matching Windows user environment variable: `OPENAI_API_KEY`, `OPENROUTER_API_KEY`, `ANTHROPIC_API_KEY`, `GEMINI_API_KEY`, or `AI_PROVIDER_API_KEY`.
6. Relaunch the game.

