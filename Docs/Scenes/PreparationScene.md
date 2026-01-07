# PreparationScene (Preparation domain)

## Purpose
- Pre-battle preparation scene before entering `BattleScene`.
- Displays the current tournament path image for upcoming battles.

## Domain & Ownership
- Scene path: `Assets/Scenes/PreparationScene.unity` (Preparation domain entry point).
- Runtime scripts under:
  - `Assets/Scripts/Preparation/` (preparation controllers and views).
- Shared UI lives in `Assets/Scripts/UI/` when needed.

## Root Hierarchy (high-level)
> Note: this reflects the current scene structure. Update when adding/removing key roots.

- `PreparationScene` (scene root)
  - `Main Camera`
  - `TournamentPathPreview` (`TournamentPathPreviewRenderer`)
    - `SpriteRenderer` displays the tournament path image.

**Extension Points:**
- New preparation controllers attach as new roots or under a future `_System` root if added.
- Additional UI can reuse `Assets/Scripts/UI/` utilities.

## Wiring Notes (Unity)
- `TournamentPathPreview` (`TournamentPathPreviewRenderer`)
  - `_tournament` -> `Assets/Data/Tournaments/Tournament-Default.asset` (or another `TournamentDefinition`).
  - `_spriteRenderer` -> `SpriteRenderer` on the same GameObject.
