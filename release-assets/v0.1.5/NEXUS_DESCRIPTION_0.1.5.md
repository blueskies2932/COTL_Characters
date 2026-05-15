# COTL Characters

AI-powered Character Mode conversations for Cult of the Lamb followers.

## Version 0.1.5

- Fixed AI Provider Setup model validation so the pasted setup key is the key used during tests, instead of allowing an existing Windows environment variable to override it.

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
