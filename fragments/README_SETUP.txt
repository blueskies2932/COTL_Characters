COTL AI NPCs - Setup
====================

This is the character-only consumer product draft for the Cult of the Lamb AI
NPC mod. It includes Character Mode conversations, Tournament Ledger support,
the BepInEx plugin, the sidecar AI runtime, and plain text feature guides.
Development notes, private diagnostics, API keys, private development tools,
and automatic work-order follower modes are not included in this product build.

Requirements
------------

- Cult of the Lamb on Windows.
- Thunderstore Mod Manager or an equivalent BepInEx 5 profile.
- BepInExPack CultOfTheLamb 5.4.2101.
- COTL_API 0.3.3.
- .NET 10 Desktop/Runtime available on the PC running the mod. The sidecar is a
  small .NET 10 process launched by the plugin.
- An AI provider. OpenAI, OpenAI-compatible endpoints, Anthropic Claude, and
  Google Gemini can be configured through the product provider settings.

Install
-------

1. Install the required dependencies in the same Cult of the Lamb profile.
2. Copy the folder:

   BepInEx/plugins/COTL_AL_NPCs

   into the target profile's:

   BepInEx/plugins/

3. Start the game once.
4. Configure your AI provider.

   Easiest option:

   - Use the in-game AI Provider Setup prompt. It stays visible until setup is
     actually usable.
   - Choose a preset.
   - Paste the key from that provider's account dashboard once, if that provider
     needs one.
   - Click Find, Test & Save Setup. The mod fetches model names when possible,
     tests likely chat/text models through the same sidecar/provider adapter
     path NPC conversations use, and saves the first one that works.
   - Existing configs that have not passed this validation will reopen setup.
   - Relaunch the game if the sidecar was already running.

   To reset provider setup later, double-click:

   BepInEx/config/COTL_AL_NPCs/Reset_AI_Provider.cmd

   The generated Setup_AI_Provider.cmd is an advanced file setup helper. The
   in-game setup is preferred because it can test the exact provider/model path.

   Manual option:

   The sidecar looks for:

   BepInEx/config/COTL_AL_NPCs/AiProvider.json

   The first launch creates a default AiProvider.json. You can edit it directly
   or copy one of the files from config_templates/ and rename it to AiProvider.json.

   For paid providers, set the provider's environment variable or provide an
   apiKeyFile in AiProvider.json. For local OpenAI-compatible servers, set
   requiresApiKey to false and configure baseUrl/model. Manual configs still
   need to pass in-game model validation before setup is considered complete.

   Do not share files containing API keys.

5. Launch the game through the modded profile.

Included Runtime Files
----------------------

- BepInEx/plugins/COTL_AL_NPCs/COTL_AL_NPCs.dll
- BepInEx/plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.exe
- BepInEx/plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.dll
- BepInEx/plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.deps.json
- BepInEx/plugins/COTL_AL_NPCs/sidecar/CotlAiNpcSidecar.runtimeconfig.json
- manifest.json
- Configure_AI_Provider.cmd
- tools/Configure_AI_Provider.ps1
- config_templates/*.json
- guides/*.txt

Important Notes
---------------

- The sidecar is expected to live in the sidecar folder next to the plugin DLL.
- The mod does not include an AI provider key.
- If AI replies fail, first check that the AI provider is configured and that the
  sidecar files are present in BepInEx/plugins/COTL_AL_NPCs/sidecar.
