# Changelog

## 0.1.6

- Improved conversation UI scaling on smaller laptop screens.
- Made Character Awareness controls wrap based on the conversation window width instead of using fixed rows.
- Added a clickable Close button and Escape-key close support for keyboard/mouse conversation users.
- Added Escape-key close support to the other blocking overlay menus.
- Fixed the Cult About editor so longer text remains reachable in the scroll view.

## 0.1.5

- Fixed AI Provider Setup model validation so the pasted setup key is the key used during tests, instead of allowing an existing Windows environment variable to override it.

## 0.1.4

- Fixed world-state sanitation context so a full in-game sanitation gauge is described as clean instead of hazardous/dirty.

## 0.1.3

- Security hotfix: AI provider setup no longer stores pasted API keys in the mod manager profile.
- Removed generated and packaged provider setup command/script files from the release.
- Existing users should delete `BepInEx/config/COTL_AL_NPCs/AiProviderKey.txt` before sharing or syncing a profile.
- Fixed the AI Cult About editor text readability while typing.
- Removed an extra overlay blocker layer from the About editor.
- Kept the About editor above the underlying game UI while open.

## 0.1.0

- Initial direct GitHub release.
- Added Character Mode follower conversations.
- Added per-character awareness and reply length controls.
- Added Tournament Ledger support.
- Added Invocations.
- Added user-configurable AI provider setup.
- Added double-click Windows installer.
- Added sidecar runtime packaging.


