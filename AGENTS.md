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

---

## 2. 🧩 PLAN — Integration before invention

After the reuse scan, the agent must propose a **short implementation plan**:

- Describe how existing code will be reused or extended.  
- If new code is required, justify its creation.  
- Respect the **Unity 6 architecture** (SceneFlow, LifetimeContentService, asmdefs) and the **SOLID** principles.

---

## 3. 📜 CONTRACTS — Declare dependencies clearly

Every implementation must list:

- **Existing APIs** or systems **called** (e.g., `LifetimeContentService`, `SceneFlowController`, `Addressables`).  
- **New public APIs** or interfaces **exposed**.

This ensures integration safety and consistent cross-module awareness.

---

## 4. 🧪 TESTS — Mandatory for all non-trivial logic

For any functional code (logic, utilities, or services), the agent **must**:

1. Create or update a corresponding test class under `/Tests/`.  
2. Use **NUnit** conventions (`[Test]`, `[SetUp]`).  
3. Mock dependencies with **NSubstitute**.  
4. Reuse existing test helpers or factories — never duplicate logic.  
5. If the system cannot be unit-tested (e.g., runtime-only behavior), explain why and propose an **integration-test** alternative.

---

## 5. 🧾 COMMIT MESSAGE — Follow Conventional Commits

Every change must end with a **Conventional Commits**-style message:

<type>(<scope>): <short description>

**Examples**
- `feat(ai): add behavior tree for NPC pathfinding`
- `fix(combat): prevent null ref in skill targeting`
- `test(services): add LifetimeContentService release tests`
- `refactor(core): extract object pooling utility`

> ✅ Always use lowercase types and concise, meaningful descriptions.

---

## 6. ⚙️ UNITY CODE QUALITY RULES

- Respect absolutely the SOLID principles
- Follow **Unity’s official C# naming conventions**  
  - `PascalCase` for types and methods  
  - `_camelCase` for private fields  
  - `ALL_CAPS` for constants  
- All comments **must be in English**.  
- Prioritize **memory and performance** (limit GC pressure, manage Addressables handles, profile allocations).  
- Respect **project modularization** (one domain per `.asmdef`).  
- All UI strings must use the **Unity Localization** system — never hardcode text.

---

## 7 🧭 GAME DOMAINS — Functional Architecture Overview

To maintain modularity and enforce clean code organization, all systems in **SevenBattles** must belong to a defined **domain**.  
Each domain represents a functional area of the game, with its own folder, assembly definition, and test suite.

### Core Domains

| Domain | Purpose | Example Subsystems / Features |
|---------|----------|-------------------------------|
| **Core** | Foundational services and cross-domain utilities. | Game lifecycle, SceneFlow, LifetimeContentService, Save/Load, Logging, Input, Localization. |
| **Menu** | Main menu and meta-navigation logic. | Main Menu, Settings, Language selection, Tournament start flow. |
| **Preparation** | Player’s pre-battle management phase. | Recruitment, shop, equipment, team setup, upgrades, start battle. |
| **Battle** | Core combat gameplay systems. | Units, skills, turn logic, AI, VFX, victory/defeat flow. |
| **UI** | Shared user interface components. | Common widgets, HUD, notifications, popups. |
| **AI** | Non-player decision systems. | Battle AI, recruitment AI, opponent logic. |
| **Tests** | Automated validation and testing. | Unit tests, play mode tests, integration tests. |

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

**Rules**
- Each domain has its **own `.asmdef`** and optional `Tests.asmdef`.  
- `Core` must not depend on any other domain.  
- Other domains may depend on `Core`, but never on each other directly (only via interfaces).  
- Place shared logic (helpers, services) in `Core/`.  
- Tests should mirror the runtime structure (`Battle → BattleTests`, etc.).  

---

> 💡 **Tip:** Keep domain dependencies explicit and minimal.  
> A clean architecture simplifies maintenance, testing, and reuse across future SevenBattles projects.


## 8. 🧱 OUTPUT FORMAT — Mandatory section order

Every AI-generated implementation must follow this structure **before any code is written**:

1. **Reuse Scan** (table)  
2. **Plan Summary**  
3. **Contracts List**  
4. **Unit Test Plan**  
5. **Commit Message**  
6. **Code Implementation**

### Example
```md
## Reuse Scan
| File | API/Class | What to Reuse | Gaps |
|------|------------|---------------|------|
| Core/Utils/ObjectPool.cs | ObjectPool<T> | Handles pooling | Missing async preload |

## Plan
Extend `ObjectPool<T>` with async preload support using Addressables.

## Contracts
- Calls: `Addressables`, `LifetimeContentService`
- Exposes: `AsyncObjectPool<T>`

## Unit Tests
Add `AsyncObjectPoolTests.cs` covering preload, reuse, and release.

## Commit Message
feat(core): extend ObjectPool with async preload support
9. 🚦 ENFORCEMENT
Agents must refuse to generate new code until the reuse scan and contracts are completed.

Any PR or commit lacking tests or reuse justification is rejected automatically.

Documentation updates (docs:) are exempt from the testing rule but must still include a valid commit message.

SevenBattles Engineering | Unity 6 LTS

“Reuse first, test always, commit clean.”