# Tournament Map (Preparation domain)

## Purpose
- Represent a tournament as a fixed sequence of 7 battles with editable ellipse zones and enemy visuals.
- Drive the preparation scene battle map without hardcoded coordinates.

## Data Assets (Core)
- `Assets/Scripts/Core/Battle/TournamentDefinition.cs`
  - `TournamentDefinition.Battles` holds 7 `TournamentBattleDefinition` entries in order.
- `Assets/Scripts/Core/Battle/TournamentBattleDefinition.cs`
  - Fields: `Battlefield`, `EnemySquad`, `EnemyPrefab`, `Ellipse`, `PrefabOffset`, `PrefabScale`.
- `Assets/Scripts/Core/Players/TournamentProgressState.cs`
  - Persistent completed-battle flags + current unlocked round index (stored on `PlayerContext`).
- `Assets/Scripts/Core/Battle/TournamentMissionIdUtil.cs`
  - Encodes/decodes the tournament round into `BattleSessionConfig.CampaignMissionId` for battle->preparation progression handoff.
- `Assets/Scripts/Core/Players/PlayerSquad.cs`
  - `LocalizedSquadName` stores the localized squad title used by preparation hover UI.
  - `UnitLoadouts` still provides the per-unit roster/spell loadouts.
- `Assets/Scripts/Core/Battle/EllipseDefinition.cs`
  - `Center`/`Radii`/`RotationDegrees` stored in local coordinates of the map root.

## Runtime Components (Preparation)
- `Assets/Scripts/Preparation/TournamentBattleMapPresenter.cs`
  - Spawns one ellipse outline + enemy prefab per battle and handles hover highlighting.
  - Reads `PlayerContext` tournament progression:
    - Completed battles keep ellipse visible and switch to completed color.
    - Enemy prefab is hidden for completed battles.
    - Only the current unlocked battle is hoverable/clickable.
  - Forces HeroEditor4D character prefabs to face down for consistent preview.
  - Uses `PlayerContext.CurrentTournamentRoundIndex` when available, with `_currentRoundIndex` as fallback.
  - Uses `EllipseDefinition.ContainsPoint` for hover hit testing.
  - Optional cursor texture for the next battle hover (assign in inspector).
- `Assets/Scripts/Preparation/TournamentOpponentHoverPanelPresenter.cs`
  - Shows an anchored opponent panel while hovering any battle node.
  - Populates unit rows and optional squad-name `TMP_Text` from `TournamentBattleDefinition.EnemySquad`.
  - Uses `PlayerSquad.LocalizedSquadName` when configured, with asset-name fallback.
- `Assets/Scripts/Preparation/TournamentBattleStartController.cs`
  - Listens for clicks on the current battle ellipse, shows confirmation, and starts BattleScene with injected battle data.
  - Sets `BattleSessionConfig.CampaignMissionId` with the selected tournament round id.
  - Prevents replay of already completed battles.
- `Assets/Scripts/Preparation/TournamentBattleEllipseGizmo.cs`
  - Draws ellipse gizmos in the scene view for authoring.
- `Assets/Scripts/Preparation/Editor/TournamentBattleEllipseGizmoEditor.cs`
  - SceneView handles to move ellipse centers, resize radii, and rotate ellipses directly in the preview.

## Extension Points
- Update `TournamentDefinition` assets to create new 7-battle tournaments.
- Adjust `Ellipse` and `PrefabOffset`/`PrefabScale` per battle to fit a new map image.
- Use the `TournamentBattleEllipseGizmo` SceneView handles to edit ellipse centers/radii/rotation.
