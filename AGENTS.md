# AGENTS.md — Engineering Governance Rules for AI Contributions

## 🎯 Purpose

This document defines mandatory rules for all **AI agents** and automation systems contributing to the **SevenBattles** Unity 6 LTS project.  
Its goal is to ensure every generated code or documentation artifact aligns with the studio’s engineering standards: **reuse first**, **tested code**, and **traceable commits**.

---

## 1. 🔁 REUSE SCAN — Always check before coding

Before implementing anything new, the agent **must scan the repository** for reusable or extensible code.

### Required Output

| File | API/Class | What to Reuse | Gaps / Missing |
|------|------------|---------------|----------------|

**Rules**
- Always prefer extending or integrating existing code before creating new files.  
- If no suitable code exists, explain **why** (e.g., violates SRP, incompatible architecture, different lifetime scope).  
- Skipping this step invalidates the proposal.
- For gameplay and UI features, the scan must explicitly look for:
  - Existing **controllers / services** in the correct domain.  
  - Existing **prefabs / ScriptableObjects** that already expose extension points.

---

## 2. 🧩 PLAN — Integration before invention

After the reuse scan, the agent must propose a **short implementation plan**:

- Describe how existing code will be reused or extended.  
- Justify any new code creation.  
- Respect SceneFlow, LifetimeContentService, asmdefs, SOLID.
- Specify which scenes, prefabs, and ScriptableObjects are modified.  
- Provide a high-level wiring description (details in section 5).

---

## 3. 📜 CONTRACTS — Declare dependencies clearly

Every implementation must list:

- **Calls**: existing APIs and systems it depends on.  
- **Exposes**: new public types, methods, interfaces, or events.

---

## 4. 🧪 TESTS — Mandatory for all non-trivial logic

- Create or update matching test classes under `/Tests/`.  
- Use NUnit (`[Test]`, `[SetUp]`).  
- Mock with NSubstitute.  
- Reuse test helpers.  
- If untestable via unit tests, propose integration tests.

---

## 5. 🔌 HOW-TO WIRE IN UNITY — Detailed Setup Steps

Step-by-step Unity integration guidelines (scenes, prefabs, SOs, Addressables, localization).

**Include:**

### A. Scenes & Domain Ownership  
### B. Prefab & Component Modifications  
### C. ScriptableObject Setup  
### D. Addressables & LifetimeContentService Scopes  
### E. Localization Setup  
### F. Input & Events Wiring  

If not applicable, explicitly write:  
**“Not applicable — pure code utility.”**

---

## 6. 🧪 AGENTS.md SUGGESTIONS — Improve Governance

Each implementation must propose optional improvements to AGENTS.md based on the feature (unless none).

Example improvements:
- Suggest a new invariant.  
- Suggest new reusable utilities.  
- Suggest documentation additions.

If none:  
**“AGENTS.md Suggestions: None.”**

---

## 7. 🧾 COMMIT MESSAGE — Follow Conventional Commits

`<type>(<scope>): <short description>`

Types: feat, fix, test, refactor, chore, docs.

---

## 8. 🧱 CODE IMPLEMENTATION

Only after the above sections are complete:

- Runtime C# scripts.  
- Unit tests.  
- Optional YAML snippets for ScriptableObjects.

---

## 9. ⚙️ UNITY CODE QUALITY RULES

- Respect SOLID.  
- Unity naming conventions:
  - PascalCase → types/methods  
  - _camelCase → private fields  
  - ALL_CAPS → constants  
- All comments in English.  
- Optimize memory & performance.  
- Use correct asmdefs.  
- Never hardcode UI strings (must use Localization).  
- Avoid complex inspector UnityEvent wiring — prefer C# events.

---

## 10. 🧭 GAME DOMAINS — Functional Architecture

To maintain modularity and enforce clean code organization, all systems in **SevenBattles** must belong to a defined **domain**.  
Each domain represents a functional area of the game, with its own folder, assembly definition, and test suite.

| Domain | Purpose |
|--------|---------|
| Core | Foundational services, SceneFlow, LifetimeContentService |
| Menu | Main menu, settings |
| Preparation | Pre-battle shop, recruitment |
| Battle | Turn order, movement, skills, AI |
| UI | Common reusable UI |
| AI | Decision systems |
| Tests | All test code |
---

### Folder & Assembly Structure

Assets/
├── Scripts/
│ ├── Core/
│ ├── Menu/
│ ├── Preparation/
│ ├── Battle/
│ ├── UI/
│ ├── AI/
│ └── Tests/
└── Art/
│ ├── Sprites/
│ ├── Fonts/
│ ├── SFX/
│ ├── Music/
│ ├── VFX/
└── Localization/
    ├── UI/
    │   ├── Common/
    │   └── Menu/
    ├── Battle/
    ├── Preparation/
    ├── Core/
    └── Shared/

**Rules**
- Each domain has its **own `.asmdef`** and optional `Tests.asmdef`.  
- `Core` must not depend on any other domain.  
- Other domains may depend on `Core`, but never on each other directly (only via interfaces).  
- Place shared logic (helpers, services) in `Core/`.  
- Tests should mirror the runtime structure (`Battle → BattleTests`, etc.).  

---

## 11. 🌐 LOCALIZATION RULES

- All displayed text must use `LocalizedString`.  
- Organize in functional string tables.  
- Add FR (french) + EN (english) + ES (spanish) entries at minimum.  
- Use Smart Strings with placeholders.

---

## 12. 💾 GAME STATE PERSISTENCE INVARIANTS

Whenever gameplay code introduces, removes, or changes any **game state** that must survive across sessions (e.g., current squad, campaign progress, difficulty, unlocked content, options that affect gameplay), the agent must:

- Identify where that state is owned (ScriptableObject, MonoBehaviour, service, model, etc.).  
- Ensure the state is **captured into the save model** (`SaveGameData` or equivalent DTO) via a dedicated `IGameStateSaveProvider`.  
- Ensure the state is **restored from the save model** (once load is implemented) via a dedicated load path (not yet implemented for this project, but changes must be forward-compatible).  
- Add or update tests under `Assets/Scripts/Tests/` that verify:
  - The new/changed property is present in the serialized save JSON.  
  - Corrupt or missing data for that property does not crash the game and falls back to a safe default.

### Required Questions for the Agent

For every new or modified game state property, explicitly answer:

- **Is this state transient or persistent?**  
  - If persistent, explain where it should live in `SaveGameData` (e.g., `PlayerSquad`, `Campaign`, `Options`, `Progression`).  
- **Where is the single source of truth?**  
  - Point to the owning type and field (e.g., `WorldSquadPlacementController._playerSquad`, `SimpleTurnOrderController.TurnIndex`).  
- **How will this state be serialized?**  
  - Which simple, JSON-safe representation will be used (ids, indices, flags, numeric values), and how it can be evolved safely.  
- **What is the default if it is missing or corrupt?**  
  - Describe a safe default behavior when old saves do not contain the new field or contain invalid data.

### Guidance for Save-Related Changes

When adding game state that must be saved:

- Prefer extending `SaveGameData` with **small, explicit DTOs** (e.g., `PlayerSquadSaveData`, `CampaignStateSaveData`) rather than dumping raw components or ScriptableObjects.  
- Implement or extend an `IGameStateSaveProvider` that:
  - Reads from the runtime owner(s) of the state (e.g., `PlayerSquad`, controllers, services).  
  - Populates the corresponding section of `SaveGameData` using simple serializable fields.  
- Keep the **UI and Battle domains free of file IO**; only Core-domain services (e.g., `SaveGameService`) perform disk access.  
- When removing a property from the game state:
  - Keep deserialization robust by treating the old field as optional when reading existing JSON.  
  - Document the migration behavior if old saves are expected to be loaded.

When in doubt, the agent should:

- Search for existing save-related types (`SaveGameService`, `SaveGameData`, `IGameStateSaveProvider`) and **extend** them rather than creating parallel persistence systems.  
- Propose new DTOs and provider methods in the Core domain, and keep them decoupled from presentation/UI concerns.

For more details on the current save/load architecture and JSON format, see:  
`Docs/SaveSystem.md`

---

## 13. 🖼 UI MODAL OVERLAY INVARIANTS

- All blocking overlays (pause menus, confirmation dialogs, turn banners, etc.) must be driven by a `CanvasGroup` that controls both `alpha` and `blocksRaycasts`.  
- While visible, modal overlays must use `blocksRaycasts = true` to prevent clicks on underlying HUD or world UI; when hidden, they must restore `blocksRaycasts = false` and any related HUD `CanvasGroup.alpha` state.  
- Modal overlays must animate using unscaled time (`Time.unscaledDeltaTime`) and must not introduce additional turn/interaction state beyond `ITurnOrderController` / `IBattleTurnController.SetInteractionLocked`.  
- Confirmation-style overlays must expose a single reusable API accepting `LocalizedString` title/message/button labels and per-call callbacks, instead of hardcoded or duplicated UI flows.  
- For confirmation flows in the UI domain (Quit, Load, delete save, reset settings, etc.), agents must first consider reusing or extending `SevenBattles.UI.ConfirmationMessageBoxHUD` before introducing any new confirmation UI component.

---

## 13. 🧬 DOMAIN-SPECIFIC BATTLE INVARIANTS

### Movement
- Must go through `SimpleTurnOrderController`.  
- Must use its BFS logic.  
- Must use legal tile caching.  
- Never implement parallel movement systems.

### Highlighting
- Primary highlight: active unit tile only (never cursor-driven).  
- Secondary highlight: cursor-driven preview only.

### AP (Action Points)
- Only from `UnitStatsData.ActionPoints` → HUD.  
- Never repurpose other stats.

### HeroEditor4D
- Must use `UnitVisualUtil` and controller reflection helpers.  
- Never reference HeroEditor4D types directly in Battle/UI assemblies.

### Active Unit Stats / Health
- Whenever a battle system mutates the runtime combat stats of the current active unit (e.g., Life, Force, etc.), it **must** raise `ITurnOrderController.ActiveUnitStatsChanged`.  
- UI health bars and other stat-driven HUD elements must rely on `ITurnOrderController.TryGetActiveUnitStats` + `ActiveUnitStatsChanged` instead of polling runtime components directly.

### Turn Index & Banners
- `IBattleTurnController.TurnIndex` is the **only** source of truth for the battle turn number; UI must not maintain its own counters.  
- Turn-based overlays (e.g., “Turn X” banners) must use `TurnIndex` + `LocalizedString` smart strings, and must acquire/release `SetInteractionLocked` in a balanced way (no permanent locks).  
- Any CanvasGroup-based overlay that blocks input must clear `blocksRaycasts` and restore related HUD `CanvasGroup.alpha` state when it hides.

---

# SevenBattles Engineering | Unity 6  
**“Reuse first, wire clean, test always, commit clean.”**
