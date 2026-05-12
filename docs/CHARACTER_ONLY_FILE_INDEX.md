# Character-Only File Index

This tracks the character-only product scaffold copied from `product_build`.

## Root Files

- `README.md` - Orientation for this character-only lane and its current source-stripping status.
- `CHARACTER_ONLY_SCOPE.md` - Included and excluded product behavior for this lane.
- `CHARACTER_ONLY_FILE_INDEX.md` - This searchable file map.
- `PROMOTION_WORKFLOW.md` - Promotion rule reference copied from the main product lane.
- `product_manifest.json` - Character-only promoted feature manifest.

## Fragments

- `fragments/README_SETUP.txt` - Consumer setup guide edited for Character Mode and Tournament support.
- `fragments/Configure_AI_Provider.cmd` - Provider setup launcher.
- `fragments/tools/Configure_AI_Provider.ps1` - Provider setup tool.
- `fragments/config_templates/*.json` - Provider config templates.
- `fragments/guides/FEATURES_OVERVIEW.txt` - Character-only feature overview.
- `fragments/guides/CHARACTER_MODE.txt` - Character Mode guide.
- `fragments/guides/TOURNAMENT_LEDGER.txt` - Tournament Ledger guide.
- `fragments/guides/INVOCATIONS.txt` - Invocation guide trimmed to character-only supported invocations.
- `fragments/guides/INTERNET_ACCESS.txt` - Internet access guide.
- `fragments/guides/AI_PROVIDER_SETUP.txt` - AI provider setup guide.
- `fragments/guides/TROUBLESHOOTING.txt` - Consumer troubleshooting guide for install, provider, and in-game UI issues.

## Source Modules

- `source/plugin/Invocations/` - Product-facing Invocation system. Keep this module while stripping automatic work-order code; only remove invocation entries that depend on removed work-order systems.
- `source/plugin/UI/` - Character conversation, awareness, lore, internet, about, invocation, and tournament overlays.
- `source/plugin/Tournament/` - Tournament ledger models, status, prompt context, bracket rules, and champion archive.
- `source/plugin/Context/` - AI-facing character context, follower facts, current events, social memory, world state, cult/about text, and trait voice profiles.
- `source/plugin/Core/` - Plugin bootstrap, config, save-scoped state, Character/Vanilla mode persistence, and native role cleanup for the role-clearing invocation.
- `source/plugin/Bridge/` - Character-only sidecar request/response bridge and live-state export.
- `source/plugin/Interactions/` - Vanilla follower interaction hooks that open/close the Character conversation UI.
- `source/plugin/Indoctrination/` - Vanilla/Character mode selector for indoctrination and reindoctrination.
- `source/plugin/SpecialEvents/` - Ritual, sermon, lifecycle, and follower-selection hooks for recent current events.
- `source/plugin/Diagnostics/` - Lightweight diagnostics and live report stream.
- `source/sidecar/` - Character-only AI provider sidecar. Staged action/menu decision files are intentionally not present.
- `tools/Assemble_Product_Build.ps1` - Builds plugin and sidecar, then assembles `package/`.
- `package/` - Generated Character-only product package output. Do not edit manually.
