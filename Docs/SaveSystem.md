# SevenBattles Save/Load System

This document describes how the SevenBattles save system works, how the JSON file is structured, and how to extend it safely when adding new game‑state features.

It reflects the current **Save + Load** implementation.

---

## 1. High‑Level Architecture

### 1.1 Responsibilities

- **Core domain**
  - `SaveGameService`: orchestrates building a `SaveGameData` snapshot and writing it as JSON.
  - `ISaveGameService`: UI‑facing interface for saving/reading slot metadata.
  - `IGameStateSaveProvider`: abstraction that populates `SaveGameData` from the current game state.
  - `SaveGameServiceComponent`: MonoBehaviour wrapper that wires a provider into `SaveGameService` using `Application.persistentDataPath`.
  - `CompositeGameStateSaveProvider`: aggregates multiple `IGameStateSaveProvider` instances so different domains can contribute to a single `SaveGameData`.

- **Battle domain**
  - `BattleBoardGameStateSaveProvider`: captures unit placements (player + enemy) and per‑unit stats.
  - `BattleTurnGameStateSaveProvider`: captures phase (placement/battle), turn index, active unit identity, AP, and whether the active unit has already moved this turn.
  - `BattleEnchantmentGameStateSaveProvider`: captures active battlefield enchantments (spell id, quad index, caster identity).

- **Players domain**
  - `PlayerSquadGameStateSaveProvider`: captures the player squad composition (which wizards are in the squad).

- **UI domain**
  - `SaveLoadHUD`: Save/Load overlay that drives slot selection and calls into `ISaveGameService` for persistence.
  - `BattlePauseHUD`: pause menu which opens the Save/Load HUD when the user presses Save.

The **single source of truth** for the on‑disk format is the `SaveGameData` DTO in `SevenBattles.Core.Save`.

---

## 2. Runtime Flow

### 2.1 Entry Points

- The UI opens the Save overlay (e.g., from `BattlePauseHUD` calling `SaveLoadHUD.ShowSave()`).
- `SaveLoadHUD`:
  - Resolves an `ISaveGameService` instance, typically `SaveGameServiceComponent` on a scene object.
  - When the player clicks a slot:
    - Calls `ISaveGameService.SaveSlotAsync(slotIndex)`.

### 2.2 `SaveGameService` Save Pipeline

For each `SaveSlotAsync(int slotIndex)` call:

1. **Directory/Path resolution**
   - `SaveGameServiceComponent` is constructed in `Awake` with:
     - `baseDirectory = Application.persistentDataPath` (or an override).
   - `SaveGameService.GetSaveDirectory()` → `<baseDirectory>/Saves`.
   - Slot file names: `save_slot_01.json` … `save_slot_08.json`.

2. **Run number computation**
   - If a slot file exists, it is deserialized to `SaveGameData` and:
     - `nextRunNumber = existing.RunNumber + 1` (or `2` if missing).
   - Otherwise, `nextRunNumber = 1`.

3. **Snapshot capture**
   - `BuildSaveGameData(_gameStateProvider, timestamp, nextRunNumber)`:
     - Creates a new `SaveGameData { Timestamp, RunNumber }`.
     - Calls `IGameStateSaveProvider.PopulateGameState(SaveGameData data)` on the configured provider.
     - Fills in safe defaults if the provider leaves fields `null`:
       - `PlayerSquad`: always non‑null with an empty `WizardIds` array.
       - `UnitPlacements`: always non‑null, possibly empty.
       - `BattleTurn`: always non‑null, defaults to `Phase = "unknown"`, zeroed indices and AP.

4. **JSON serialization**
   - Uses `JsonUtility.ToJson(SaveGameData, prettyPrint: true)` to obtain the JSON string.

5. **Atomic write**
   - Writes to `<slotPath>.tmp`.
   - If the final file exists:
     - Attempts `File.Replace(temp, path, backup, ignoreMetadataErrors: true)`.
     - Logs the backup path, then deletes the backup if possible.
     - On failure, falls back to `File.Copy(temp, path, overwrite: true)` then deletes temp.
   - If the file does not exist:
     - `File.Move(temp, path)`.
   - Disk IO happens on a background `Task` so the UI thread does not block.

---

## 3. Game‑State Providers

### 3.1 `IGameStateSaveProvider`

```csharp
public interface IGameStateSaveProvider {
    void PopulateGameState(SaveGameData data);
}
```

A provider must:

- Read the runtime state from authoritative owners (controllers/services/ScriptableObjects).
- Populate the appropriate parts of the `SaveGameData` DTO.
- Never perform file IO (that is `SaveGameService`’s responsibility).
- Be robust to the game being in different phases (placement, battle, etc.).

### 3.2 `CompositeGameStateSaveProvider`

File: `Assets/Scripts/Core/Save/CompositeGameStateSaveProvider.cs`

- Serialized field:

  ```csharp
  [SerializeField]
  private MonoBehaviour[] _providers; // each must implement IGameStateSaveProvider
  ```

- `Awake`/`OnValidate` cache a typed array of `IGameStateSaveProvider`.
- `PopulateGameState` loops providers and calls `PopulateGameState` on each, catching and logging individual errors so one provider failing does not break the entire save.

Typical configuration:

- Element 0: `PlayerSquadGameStateSaveProvider`
- Element 1: `BattleBoardGameStateSaveProvider`
- Element 2: `BattleTurnGameStateSaveProvider`

### 3.3 Player Squad Provider

File: `Assets/Scripts/Core/Save/PlayerSquadGameStateSaveProvider.cs`

- Reads from `PlayerSquad` (via `PlayerContext` or direct reference).
- Fills `SaveGameData.PlayerSquad` with:

  ```csharp
  public sealed class PlayerSquadSaveData {
      public string[] WizardIds; // UnitDefinition.Id per slot
  }
  ```

- If the squad is null or empty, writes an empty `WizardIds` array (no crash).

### 3.4 Battle Board Provider (Placements + Stats)

File: `Assets/Scripts/Battle/Save/BattleBoardGameStateSaveProvider.cs`

- Finds all `UnitBattleMetadata` instances:

  ```csharp
  var metas = Object.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
  ```

- For each meta:
  - **Identity**
    - `meta.Definition?.Id` → `UnitPlacementSaveData.UnitId`.
    - `meta.IsPlayerControlled` → `"player"` / `"enemy"` in `Team`.
  - **Position**
    - If `meta.HasTile`:
      - `Tile.x`, `Tile.y` → `X`, `Y`.
    - Else:
      - `X = -1`, `Y = -1` (invalid tile, but save still succeeds).
  - **Stats / Life**
    - Reads `UnitStats` on the same GameObject (if present).
    - Populates `UnitStatsSaveData`:

      ```csharp
      public sealed class UnitStatsSaveData {
          public int Life;
          public int MaxLife;
          public int Attack;
          public int Shoot;
          public int Spell;
          public int Speed;
          public int Luck;
          public int Defense;
          public int Protection;
          public int Initiative;
          public int Morale;
      }
      ```

    - `Dead` is derived as `stats.Life <= 0`.
    - If stats are missing, `Stats` is left `null` and `Dead == false`.
  - **Facing**
    - Reads `meta.Facing` (a `Vector2` updated whenever we call `UnitVisualUtil.SetDirectionIfCharacter4D`).
    - Quantizes it to a 4‑direction string:

      ```csharp
      "up", "down", "left", "right"
      ```

    - Stored in `UnitPlacementSaveData.Facing`.

Result: `SaveGameData.UnitPlacements` contains one entry per unit with:

- Identity (`UnitId`, `Team`)
- Position (`X`, `Y`)
- Facing (`Facing`)
- Death flag (`Dead`)
- Full stats (`Stats`)

### 3.5 Battle Turn Provider (Phase + Turn + Active Unit)

File: `Assets/Scripts/Battle/Save/BattleTurnGameStateSaveProvider.cs`

- Attempts to find a `SimpleTurnOrderController`:

  ```csharp
  var turnController = Object.FindFirstObjectByType<SimpleTurnOrderController>();
  ```

- If found and there is an active battle turn (`TurnIndex > 0`, `HasActiveUnit == true`):
  - Sets:

    ```csharp
    BattleTurn.Phase = "battle";
    BattleTurn.TurnIndex = turnController.TurnIndex;
    BattleTurn.ActiveUnitCurrentActionPoints = turnController.ActiveUnitCurrentActionPoints;
    BattleTurn.ActiveUnitMaxActionPoints = turnController.ActiveUnitMaxActionPoints;
    ```

  - Uses `SimpleTurnOrderController.ActiveUnitMetadata` to get the current unit's metadata:
    - `Definition?.Id` → `ActiveUnitId` (not necessarily unique across all units).
    - `GetInstanceID()` → `ActiveUnitInstanceId` (unique within the scene and within the snapshot).
    - `IsPlayerControlled` → `ActiveUnitTeam` (`"player"` / `"enemy"`).

- Else (no active battle turn):
  - Looks for `WorldSquadPlacementController`:
    - If found and `IsLocked == false`:
      - `Phase = "placement"`.
    - Otherwise:
      - `Phase = "unknown"`.
  - Clears the rest of the fields:

    ```csharp
    TurnIndex = 0;
    ActiveUnitId = null;
    ActiveUnitTeam = null;
    ActiveUnitCurrentActionPoints = 0;
    ActiveUnitMaxActionPoints = 0;
    ```

The DTO:

```csharp
public sealed class BattleTurnSaveData {
    public string Phase;                 // "placement", "battle", "unknown"
    public int TurnIndex;                // 0 if not in battle
    public string ActiveUnitId;          // definition id (may repeat)
    public string ActiveUnitInstanceId;  // stable battle-local id from UnitBattleMetadata.SaveInstanceId
    public string ActiveUnitTeam;        // "player" / "enemy"
    public int ActiveUnitCurrentActionPoints;
    public int ActiveUnitMaxActionPoints;
}
```

### 3.6 Battle Session Provider (Original Configuration)

File: `Assets/Scripts/Battle/Save/BattleSessionSaveProvider.cs`

- Captures the original battle configuration from `IBattleSessionService`:

  ```csharp
  var sessionService = Object.FindFirstObjectByType<BattleSessionService>();
  ```

- Populates `BattleSessionSaveData`:
  - `PlayerSquadIds`: Array of unit definition IDs from `CurrentSession.PlayerSquad`
  - `EnemySquadIds`: Array of unit definition IDs from `CurrentSession.EnemySquad`
  - `BattleType`: Battle type identifier (e.g., "campaign", "arena")
  - `Difficulty`: Difficulty level
  - `CampaignMissionId`: Optional campaign mission identifier

The DTO:

```csharp
public sealed class BattleSessionSaveData {
    public string[] PlayerSquadIds;
    public string[] EnemySquadIds;
    public string BattleType;
    public int Difficulty;
    public string CampaignMissionId;
}
```

**Purpose**: This captures the original battle configuration, not just the current unit placements. This allows complete battle reconstruction when loading a save, including the ability to restart the battle with the same squads.

### 3.7 Battle Enchantment Provider (Active Enchantments)

File: `Assets/Scripts/Battle/Save/BattleEnchantmentGameStateSaveProvider.cs`

- Captures active enchantments from `BattleEnchantmentController`:
  - `SpellId`
  - `QuadIndex`
  - `CasterInstanceId`
  - `CasterUnitId`
  - `CasterTeam`

DTO:

```csharp
public sealed class BattleEnchantmentSaveData {
    public string SpellId;
    public int QuadIndex;
    public string CasterInstanceId;
    public string CasterUnitId;
    public string CasterTeam;
}
```

---

## 4. JSON File Format

### 4.1 Top‑Level Shape

The JSON produced by `SaveGameService` has the following top‑level structure:

```jsonc
{
  "Timestamp": "2025-11-29 21:30:12",
  "RunNumber": 3,
  "PlayerSquad": {
    "WizardIds": ["WizardA", "WizardB", "WizardC"]  // DEPRECATED - use BattleSession
  },
  "UnitPlacements": [
    {
      "UnitId": "WizardA",
      "Team": "player",
      "X": 2,
      "Y": 1,
      "Facing": "up",
      "Dead": false,
      "Stats": {
        "Life": 23,
        "MaxLife": 30,
        "Attack": 5,
        "Shoot": 3,
        "Spell": 2,
        "Speed": 4,
        "Luck": 1,
        "Defense": 2,
        "Protection": 1,
        "Initiative": 10,
        "Morale": 0
      }
    },
    {
      "UnitId": "WizardEnemy1",
      "Team": "enemy",
      "X": 5,
      "Y": 6,
      "Facing": "down",
      "Dead": false,
      "Stats": { /* same fields */ }
    }
    // ...
  ],
  "BattleTurn": {
    "Phase": "battle",
    "TurnIndex": 2,
    "ActiveUnitId": "WizardA",
    "ActiveUnitTeam": "player",
    "ActiveUnitCurrentActionPoints": 1,
    "ActiveUnitMaxActionPoints": 2
  },
  "BattleEnchantments": [
    {
      "SpellId": "spell.enchant.attack",
      "QuadIndex": 0,
      "CasterInstanceId": "unit_42",
      "CasterUnitId": "WizardA",
      "CasterTeam": "player"
    }
  ],
  "BattleSession": {
    "PlayerSquadIds": ["WizardA", "WizardB", "WizardC"],
    "EnemySquadIds": ["WizardEnemy1", "WizardEnemy2"],
    "BattleType": "campaign",
    "Difficulty": 1,
    "CampaignMissionId": "mission_01"
  }
}
```

Notes:

- Fields may be omitted if future versions of `SaveGameData` add new properties, but older JSON won’t have them; any load implementation must treat missing fields as defaults.
- `UnitPlacements` may be an empty array if no units exist (e.g., before battle).
- `BattleTurn.Phase`:
  - `"placement"`: still in placement flow.
  - `"battle"`: battle is active, and the rest of the fields are meaningful.
  - `"unknown"`: e.g., in transitional or end states.

---

## 5. Extending the Save Model Safely

Whenever you add, remove, or change game state that should be persisted (see AGENTS.md §12), follow these steps:

1. **Decide ownership and DTO shape**
   - Identify the runtime owner (controller/service/model).
   - Decide on a simple DTO representation in `SaveGameData`:
     - Prefer small, typed sub‑objects (e.g., `UnitStatsSaveData`, `CampaignStateSaveData`) over dumping raw components.

2. **Extend `SaveGameData`**
   - Add a new `[Serializable]` DTO type in `SaveGameService.cs` (or a separate Core file if it’s large).
   - Add a new field to `SaveGameData` to hold it (or extend existing DTOs).
   - In `BuildSaveGameData`, add a null‑check to ensure the field is set to a safe default when the provider does not fill it.

3. **Add/extend an `IGameStateSaveProvider`**
   - If the state belongs to:
     - Battle → extend or create a provider under `Assets/Scripts/Battle/Save`.
     - Core/global → extend or create a provider under `Assets/Scripts/Core/Save`.
   - Implement `PopulateGameState` to:
     - Read runtime state from the authoritative component(s).
     - Populate the new DTO field inside `SaveGameData`.
     - Avoid file IO or long‑running work.

4. **Wire into `CompositeGameStateSaveProvider`**
   - Add your provider component to the `_providers` list on the composite object in the scene.
   - Ensure `SaveGameServiceComponent` references that composite.

5. **Add tests**
   - Under `Assets/Scripts/Tests/`:
     - Add unit tests that exercise the provider in isolation:
       - Construct minimal GameObjects and components.
       - Call `PopulateGameState`.
       - Assert that the new DTO field is populated correctly.
     - Add or extend Core tests to confirm that:
       - `SaveGameService` can serialize/deserialize the new field.
       - Missing or corrupt JSON for the new field does not crash the game and falls back to defaults.

6. **Keep Load forward‑compatible**
   - Although Load is not implemented yet, design DTOs so:
     - New fields are optional (older saves don’t have them).
     - Removing fields is safe by treating them as optional on reading.
   - When implementing Load later, always assume some fields may be missing and use safe defaults.

---

## 6. Quick Checklist for New Features

When you add a new persistent game‑state feature, answer these questions (see AGENTS.md §12):

1. **Is this state transient or persistent?**
   - If persistent, which DTO in `SaveGameData` should hold it?
2. **Where is the single source of truth?**
   - Which type/field owns this state at runtime?
3. **How will this state be serialized?**
   - What JSON‑friendly structure (ids, enums, ints, bools) will you use?
4. **What is the default when missing or corrupt?**
   - How does the game behave if this field is absent in older saves?
5. **Which provider should be extended or created?**
   - Core vs Battle, and how to wire it into `CompositeGameStateSaveProvider`.
6. **What tests validate it?**
   - At least one test that:
     - Verifies the field is present in `SaveGameData`.
     - Ensures bad JSON does not crash and falls back to a safe state.

Keeping these invariants in mind will ensure the save format remains robust, debuggable, and easy to extend as SevenBattles grows.







