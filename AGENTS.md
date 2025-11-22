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

## 12. 🧬 DOMAIN-SPECIFIC BATTLE INVARIANTS

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

---

# SevenBattles Engineering | Unity 6  
**“Reuse first, wire clean, test always, commit clean.”**
