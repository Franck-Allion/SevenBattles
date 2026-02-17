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
    - `TournamentBattleMapPresenter` spawns battle ellipses and enemy prefabs from the tournament definition.
    - `TournamentBattleStartController` handles click -> confirmation -> battle start.
    - `TournamentBattleEllipseGizmo` draws ellipse layout gizmos for authoring.
  - `ConfirmationHUD`
    - `ConfirmationMessageBoxHUD` (copied from BattleScene).
  - `PopupMenu`
    - `PreparationPopupMenuLocalizationController` localizes action labels for menu buttons.
  - `ResourcesPanel`
    - `PreparationResourcesPanelPresenter` updates `CoinValue` and `GemValue` TMP labels from `PlayerContext`.

**Extension Points:**
- New preparation controllers attach as new roots or under a future `_System` root if added.
- Additional UI can reuse `Assets/Scripts/UI/` utilities.

## Wiring Notes (Unity)
- `TournamentPathPreview` (`TournamentPathPreviewRenderer`)
  - `_tournament` -> `Assets/Data/Tournaments/Tournament-Default.asset` (or another `TournamentDefinition`).
  - `_spriteRenderer` -> `SpriteRenderer` on the same GameObject.
  - `TournamentPathPreview` (`TournamentBattleMapPresenter`)
    - `_tournament` -> `Assets/Data/Tournaments/Tournament-Castle.asset`.
    - `_camera` -> `Main Camera`.
    - `_battleRoot` -> `TournamentPathPreview` (`Transform`).
    - `_currentRoundIndex` -> `1` (current round highlight target).
    - `_defaultCursorTexture` -> `Assets/Art/Cursors/pointer002.png` (or the desired default cursor asset).
    - `_defaultCursorHotspot` -> set to match the cursor tip (e.g., `4,4`).
    - `_nextBattleCursorTexture` -> cursor texture asset for the next battle hover (optional).
    - `_nextBattleCursorHotspot` -> set to match the cursor texture hotspot.
  - `OpponentHoverPanel` (`TournamentOpponentHoverPanelPresenter`)
    - `_mapPresenter` -> `TournamentPathPreview` (`TournamentBattleMapPresenter`).
    - `_rootCanvasGroup` -> `OpponentHoverPanel` (`CanvasGroup`).
    - `_squadNameText` -> `OpponentHoverPanel/SquadNameText` (`TMP_Text`).
    - `_rows` -> child row GameObjects (portrait `Image`, level `TMP_Text` or `Text`).
  - Enemy squad data (`Assets/Data/*Squad.asset` / `PlayerSquad`)
    - `LocalizedSquadName` -> assign table + entry key to display translated names in `OpponentHoverPanel`.
- `TournamentPathPreview` (`TournamentBattleStartController`)
  - `_mapPresenter` -> `TournamentPathPreview` (`TournamentBattleMapPresenter`).
  - `_playerContext` -> `Assets/Data/PlayerContext.asset`.
  - `_confirmationBehaviour` -> `ConfirmationHUD/ConfirmationPanel` (`ConfirmationMessageBoxHUD`).
  - `_battleSceneName` -> `BattleScene`.
  - `_battleScenePreload` -> `Assets/Data/Preload/BattleScenePreload.asset`.
  - `_sceneFadeOutDuration` -> `0.5` (seconds).
  - `_sceneFadeInDuration` -> `0.5` (seconds).
  - `_sceneFadeColor` -> `#000000` (black).
  - Transition order (`SceneTransitionFader`) -> fade-out, preload manifest, load scene, fade-in.
- `TournamentPathPreview` (`TournamentBattleEllipseGizmo`)
  - `_tournament` -> `Assets/Data/Tournaments/Tournament-Castle.asset`.
- `PopupMenu` (`PreparationPopupMenuLocalizationController`)
  - `_shopLabelTMP` -> `PopupMenu/ShopButtonMenu/Text` (`TMP_Text`).
  - `_squadLabelTMP` -> `PopupMenu/SquadButtonMenu/Text` (`TMP_Text`).
  - `_shopLabel` -> table `UI.Common`, key `Preparation.Popup.Shop`.
  - `_squadLabel` -> table `UI.Common`, key `Preparation.Popup.Squad`.
- `ResourcesPanel` (`PreparationResourcesPanelPresenter`)
  - `_playerContext` -> `Assets/Data/PlayerContext.asset`.
  - `_goldValueTMP` -> `Canvas/ResourcesPanel/Coin/CoinValue` (`TMP_Text`).
  - `_gemsValueTMP` -> `Canvas/ResourcesPanel/Gem/GemValue` (`TMP_Text`).
