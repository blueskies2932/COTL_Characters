# Character-Only Product Scope

This folder is for a consumer build that keeps the working character/tournament
experience and leaves out unfinished automatic work-order follower mode.

## Included

- Character Mode conversations.
- Per-character awareness settings.
- Per-character Lore.
- Conversation transcript copy.
- Cult About context.
- Current Events context.
- Tournament Ledger UI, current-match display, entrant status, and champion archive.
- Tournament context for character replies.
- Invocations, with simple success/failure receipts.
- Internet Access settings for eligible character replies.
- AI provider setup docs, templates, and setup tool.
- Troubleshooting and setup guides.

## Keep While Stripping Work-Order Code

- The Invocation system is product-facing and remains in the Character-only build.
- Strip only invocations that depend on removed automatic work-order systems.
- Keep character-safe invocations such as cult faith and vanilla role cleanup.

## Excluded

- Automatic work-order follower mode.
- Action menu for follower work.
- Farm/work-plan execution.
- Drink-reservation actions.
- Follower-search actions.
- Action catalog validation for executable game actions.
- Any private or development-only owner/debug mode.

## Current Folder State

The Character-only plugin and sidecar sources have been stripped, built, and
assembled into the local package output.
