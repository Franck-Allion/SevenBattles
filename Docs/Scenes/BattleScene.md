# BattleScene (Battle domain)

## Purpose
- Main battle scene for resolving tactical combats between squads.
- Hosts core battle controllers (turn order, placement, movement) and the battle HUD.

## Domain & Ownership
- Scene path: `Assets/Scenes/BattleScene.unity` (Battle domain entry point).
- Runtime scripts under:
  - `Assets/Scripts/Battle/` (battle logic and controllers).
  - `Assets/Scripts/UI/` (battle HUD and common UI).
- Core services should live in `Core` domain assemblies and be referenced via interfaces.

## Root Hierarchy (high-level)
> Note: this reflects the actual current scene structure as of 2025-11-30.  
> When changing roots or adding important controllers, update this document.

- `BattleScene` (scene root)
  - `_System`
    - `WorldBattleBootstrap` (`WorldBattleBootstrap`) - Entry point for battle initialization.
    - `BattleSessionService` (`BattleSessionService`) - Holds current battle session configuration (player/enemy squads, difficulty).
    - `BattleVisualFeedbackService` (`BattleVisualFeedbackService`) - Manages battle visual effects (damage numbers, healing, buffs).
    - `SimpleTurnOrderController` (`SimpleTurnOrderController`) - Manages turn order and unit activation.
    - `WorldSquadPlacementController` (`WorldSquadPlacementController`) - Handles pre-battle squad placement.
    - `WorldEnemySquadStartController` (`WorldEnemySquadStartController`) - Spawns enemy units at start.
    - `Save-Load System` (Container)
      - `SaveGameService` (`SaveGameServiceComponent`) - Core save/load service.
      - `BattleBoardGameStateSaveProvider` (`BattleBoardGameStateSaveProvider`) - Saves board state.
      - `BattleSessionSaveProvider` (`BattleSessionSaveProvider`) - Saves battle session configuration.
      - `PlayerGameStateProvider` (`PlayerSquadGameStateSaveProvider`) - Saves player squad state (DEPRECATED).
      - `CompositeGameStateProvider` (`CompositeGameStateSaveProvider`) - Aggregates save providers.
  - `World`
    - World/board representation (`WorldPerspectiveBoard`, tiles, highlights, etc.)
    - Camera rigs and controllers
  - `BattleHUD`
    - `TurnOrderHUD` (`TurnOrderHUD`) - Shows active unit stats, health, action points, and End Turn button
    - `SpellsContainer` (`BattleSpellsHUD`) - Displays active unit spells (icons + AP cost) using a HorizontalLayoutGroup
    - `SelectedSpellDescription` (`TextMeshProUGUI`) - Shows the currently selected spell description (localized)
    - `BattlePauseHUD` (`BattlePauseHUD`) - Pause menu overlay
  - `SquadPlacementHUD`
    - Pre-battle placement UI (`SquadPlacementHUD`)
  - `ConfirmationHUD`
    - Confirmation dialogs (`ConfirmationMessageBoxHUD`)
  - `SaveLoadHUD`
    - Save/load UI (`SaveLoadHUD`)

**Extension Points:**
- New battle controllers/services → attach to `_System`
- New HUD elements → create new root Canvas or add to existing HUD roots
- Board/world elements → add under `World`

## Key Controllers & Services

- `SimpleTurnOrderController` (`Assets/Scripts/Battle/Turn/SimpleTurnOrderController.cs`)
  - Responsibilities:
    - Discover battle units, maintain initiative order, and advance turns.
    - Provide `TurnIndex`, active unit metadata, and `TryGetActiveUnitStats`.
    - Control movement using BFS and legal tile caching.
    - Raise `ActiveUnitChanged`, `ActiveUnitActionPointsChanged`, `ActiveUnitStatsChanged` events.
  - **Actual location:** child of `_System` root GameObject.
  - Extension point:
    - Any system needing the active unit or turn index should depend on `IBattleTurnController` / `ITurnOrderController`, not concrete components.

- `WorldSquadStartController` (`Assets/Scripts/Battle/Start/WorldSquadStartController.cs`)
  - Responsibilities:
    - Bridge from pre-battle squad data into runtime battle units.
    - Spawn or configure units on the board at battle start.
  - **Actual location:** child of `_System` root GameObject.

- `WorldSquadPlacementController` (`Assets/Scripts/Battle/Start/WorldSquadPlacementController.cs`)
  - Responsibilities:
    - Handle pre-battle placement flow on the battle board.
    - Coordinate with `SquadPlacementHUD` for placement interactions.
  - **Actual location:** child of `_System` root GameObject.

- `BattleSessionService` (`Assets/Scripts/Battle/BattleSessionService.cs`)
  - Responsibilities:
    - Hold the current battle session configuration (player squad, enemy squad, battle type, difficulty).
    - Provide `IBattleSessionService` interface for controllers to query battle configuration.
    - Initialize session from SceneFlow, load system, or legacy ScriptableObject references.
  - **Actual location:** child of `_System` root GameObject.
  - Extension point:
    - Any system needing battle configuration should depend on `IBattleSessionService`, not ScriptableObject references.
    - Session must be initialized before any controllers spawn units (`WorldBattleBootstrap.Awake` ensures this).
  - **Debugging workflow**:
    - When pressing Play directly in BattleScene (no previous scene), `WorldBattleBootstrap` auto-generates a session from inspector-assigned ScriptableObject references.
    - This allows rapid iteration without setting up a full scene flow.
    - Legacy `_playerSquad` / `_enemySquad` fields in controllers serve as "debug parameters" for this workflow.

- `BattleVisualFeedbackService` (`Assets/Scripts/Battle/BattleVisualFeedbackService.cs`)
  - Responsibilities:
    - Manage battle visual effects (damage numbers, healing effects, buff indicators).
    - Spawn DamageNumbersPro prefabs at unit positions during combat events.
    - Provide extension points for future visual feedback (healing, buffs, status effects).
  - **Actual location:** child of `_System` root GameObject.
  - **Inspector configuration**:
    - `_damageNumberPrefab` → Assign a DamageNumbersPro prefab from `Assets/DamageNumbersPro/Demo/Prefabs/2D/` (e.g., "Red Glow", "Blood Text").
    - `_damageNumberYOffset` → Vertical offset in world units to display numbers above units (default: 2.0).
  - Extension point:
    - Controllers needing visual feedback should reference this service and call `ShowDamageNumber()`, `ShowHealNumber()`, or `ShowBuffText()`.

- Battle HUD controllers (`Assets/Scripts/UI/`)
  - `TurnOrderHUD` (under `BattleHUD` root)
    - Shows active unit portrait, stats, health, and action points.
    - End Turn button drives `IBattleTurnController` / `ITurnOrderController`.
  - `BattleSpellsHUD` (under `BattleHUD/SpellsContainer`)
    - Displays the active unit's spells (icons + AP cost overlay) driven by `ITurnOrderController`.
  - `BattlePauseHUD` (under `BattleHUD` root)
    - Pause menu overlay for battle.
  - `SquadPlacementHUD` (under `SquadPlacementHUD` root)
    - Shows placement UI (portraits, confirm/cancel placement, etc.).
  - `ConfirmationMessageBoxHUD` (under `ConfirmationHUD` root)
    - Generic confirmation dialog system.
  - `SaveLoadHUD` (under `SaveLoadHUD` root)
    - Save and load game UI.

## Extension Points

When adding new functionality, prefer extending these areas instead of inventing new roots:

- New battle HUD elements
  - Add to existing HUD root GameObjects (`BattleHUD`, `SquadPlacementHUD`, etc.) or create a new root Canvas if the HUD is functionally distinct.
  - Implement a dedicated HUD MonoBehaviour in the `UI` domain.
  - Drive data via interfaces such as `IBattleTurnController`, `ITurnOrderController`, or other Core/Battle interfaces.
  - Avoid direct references to board/unit MonoBehaviours from UI; go through services or controllers.

- New battle controllers / services
  - Attach to `_System` root GameObject.
  - Expose interfaces from the `Core` or `Battle` domains and inject them into UI via serialized fields or service locators/scopes.
  - Respect domain rules: `Core` does not depend on `Battle` or `UI`.

- Save/load related extensions
  - For new battle state that must be persisted, extend `SaveGameData` and an appropriate `IGameStateSaveProvider` (see `Docs/SaveSystem.md`).
  - Battle session configuration is captured via `BattleSessionSaveProvider` into `BattleSessionSaveData`.
  - Do not add file IO in this scene; keep it in the `Core` save system.

## Wiring Notes (Unity)

> Use this section together with the Unity Wiring checklist in `AGENTS.md` section 5.

- `TurnOrderHUD` (under `BattleHUD` root)
  - `_controllerBehaviour` → reference to a component implementing `IBattleTurnController` (e.g., `SimpleTurnOrderController` under `_System`).
  - Other serialized fields (portrait image, end turn button, action point bar, stats panel, health bar) should be assigned to child objects under `BattleHUD`.

- `SquadPlacementHUD` (under `SquadPlacementHUD` root)
  - Should be wired to the placement controller (`WorldSquadPlacementController` under `_System`) rather than talking directly to board/unit components.

- `BattleSpellsHUD` (under `BattleHUD/SpellsContainer`)
  - `_controllerBehaviour` → reference to a component implementing `ITurnOrderController` (e.g., `SimpleTurnOrderController` under `_System`).
  - `_spellsContainer` → reference to the `SpellsContainer` RectTransform (same object).
  - `_selectedSpellDescriptionText` → reference to `BattleHUD/Canvas/SelectedSpellDescription` (`TextMeshProUGUI`).
  - Slot binding options:
    - Fixed slots (recommended): author inactive children `Spell0`, `Spell1`, ... under `SpellsContainer` and (optionally) add `BattleSpellSlotView` to each slot to wire `Icon`, `ApCost`, `SelectionFrame` and `Button`.
    - Template fallback: assign `_slotTemplate` to a UI prefab representing a single spell slot (recommended to add `BattleSpellSlotView` on the prefab and wire its fields).
  - Audio (optional):
    - Assign `_spellClickClip` (and optionally `_audio`) to play a click SFX when selecting/deselecting spells.

- `SaveLoadHUD` (under `SaveLoadHUD` root)
  - Wired to save/load services for persisting game state.

- `ConfirmationMessageBoxHUD` (under `ConfirmationHUD` root)
  - Generic confirmation dialog, can be invoked by any system needing user confirmation.

- Modal overlays (pause, banners, confirmations)
  - Must use `CanvasGroup` with `alpha` and `blocksRaycasts` as per UI modal invariants in `AGENTS.md`.
  - Lock/unlock interaction via `IBattleTurnController.SetInteractionLocked` instead of custom flags.

When implementing a feature:
- Link to this document from your PR `Unity Wiring` section.
- Only change roots or key controllers when necessary, and update this document accordingly.
