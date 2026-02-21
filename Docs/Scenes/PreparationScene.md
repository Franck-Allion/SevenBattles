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
      - Completed battles hide their enemy prefab and remain visible as completed ellipses.
      - Only the next unlocked battle is clickable/hoverable.
    - `TournamentBattleStartController` handles click -> confirmation -> battle start.
      - Blocks battle start when the active squad is empty and shows an OK-only localized popup.
    - `TournamentBattleEllipseGizmo` draws ellipse layout gizmos for authoring.
  - `ConfirmationHUD`
    - `ConfirmationMessageBoxHUD` (copied from BattleScene).
  - `PopupMenu`
    - `PreparationPopupMenuLocalizationController` localizes action labels for menu buttons.
  - `ResourcesPanel`
    - `PreparationResourcesPanelPresenter` updates `CoinValue` and `GemValue` TMP labels from `PlayerContext`.
    - Consumes `BattleVictoryRewardTransfer` to animate post-battle gold/gems gains from pre-battle values to final totals.

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
    - `_playerContext` -> `Assets/Data/PlayerContext.asset` (drives completed battles + unlocked round).
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
    - Keep the root GameObject active in scene (`SetActive(true)`); runtime visibility is controlled by `CanvasGroup.alpha` in the presenter.
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
  - `_emptySquadTitle` -> table `UI.Common`, key `Confirm.StartBattleRequiresUnitTitle`.
  - `_emptySquadMessage` -> table `UI.Common`, key `Confirm.StartBattleRequiresUnitMessage`.
  - `_emptySquadOkLabel` -> table `UI.Common`, key `Common.OK`.
  - Transition order (`SceneTransitionFader`) -> fade-out, preload manifest, load scene, fade-in.
- `TournamentPathPreview` (`TournamentBattleEllipseGizmo`)
  - `_tournament` -> `Assets/Data/Tournaments/Tournament-Castle.asset`.
- `PopupMenu` (`PreparationPopupMenuLocalizationController`)
  - `_shopLabelTMP` -> `PopupMenu/ShopButtonMenu/Text` (`TMP_Text`).
  - `_squadLabelTMP` -> `PopupMenu/SquadButtonMenu/Text` (`TMP_Text`).
  - `_shopButton` -> `PopupMenu/ShopButtonMenu` (`Button`) (optional; auto-found by name when empty).
  - `_squadButton` -> `PopupMenu/SquadButtonMenu` (`Button`) (optional; auto-found by name when empty).
  - `_squadBackButton` -> `SquadPanel/.../Button_Back` (`Button`) (optional; auto-found by name when empty).
  - `_squadBackButtonObjectName` -> name used for auto-discovery when `_squadBackButton` is empty (default: `Button_Back`).
  - `_shopLabel` -> table `UI.Common`, key `Preparation.Popup.Shop`.
  - `_squadLabel` -> table `UI.Common`, key `Preparation.Popup.Squad`.
  - `_buttonHoverCursorTexture` -> cursor texture shown while hovering menu buttons (example: `Assets/Art/Cursors/hand002.png`).
  - `_buttonHoverCursorHotspot` -> cursor hotspot for `_buttonHoverCursorTexture` (match texture tip).
  - `_portraitHoverCursorTexture` -> optional cursor texture shown while hovering unit portraits in the squad panel (falls back to `_buttonHoverCursorTexture` when empty).
  - `_portraitHoverCursorHotspot` -> cursor hotspot for `_portraitHoverCursorTexture`.
  - `_defaultCursorTexture` -> default preparation cursor (example: `Assets/Art/Cursors/pointer002.png`).
  - `_defaultCursorHotspot` -> cursor hotspot for `_defaultCursorTexture`.
  - `_clickAudioSource` -> optional UI `AudioSource` used to play menu button click SFX.
  - `_clickSfxClip` -> SFX clip played when clicking Shop/Squad buttons.
  - `_clickSfxVolume` -> click SFX volume multiplier.
  - `_clickSfxCooldown` -> minimum time between click SFX plays.
  - `_squadPanel` -> optional explicit reference to `SquadPanel` root (`GameObject`).
  - `_squadPanelObjectName` -> name used for auto-discovery when `_squadPanel` is empty (default: `SquadPanel`).
  - `_squadPanelCanvasGroup` -> optional `CanvasGroup` on `SquadPanel` for fade/input state (auto-added when missing).
  - `_squadPanelFadeDuration` / `_squadPanelStartScale` / `_squadPanelRevealCurve` -> controls the unscaled-time show/hide transition when opening from `SquadButtonMenu` and closing from `Button_Back`.
- `ResourcesPanel` (`PreparationResourcesPanelPresenter`)
  - `_playerContext` -> `Assets/Data/PlayerContext.asset`.
  - `_goldValueTMP` -> `Canvas/ResourcesPanel/Coin/CoinValue` (`TMP_Text`).
  - `_gemsValueTMP` -> `Canvas/ResourcesPanel/Gem/GemValue` (`TMP_Text`).
  - `_goldNumberPrefab` -> `Assets/Prefabs/DamageNumber/GoldNumber.prefab`.
  - `_gemNumberPrefab` -> `Assets/Prefabs/DamageNumber/GemNumber.prefab`.
  - `_goldCollectionSfxClip` -> fallback coin cascade clip used when variants are not assigned.
  - `_goldCollectionSfxVariants` -> optional random clip pool (example: `Loot_Simple_Coins_1`, `Loot_Simple_Coins_2`, `Loot_Simple_Coins_3`).
  - `_goldCollectionAudioSource` -> optional dedicated `AudioSource` for gold coin ticks (auto-resolved/created when not assigned).
  - `_goldCollectionSfxVolume` -> coin tick volume multiplier.
  - `_goldCollectionMaxTicksPerSecond` -> hard cap for coin tick cadence to prevent audio spam.
  - `_goldCollectionAmountForMaxCadence` / `_goldCollectionSmallRewardTickInterval` / `_goldCollectionLargeRewardStartTickInterval` / `_goldCollectionSlowdownMultiplier` -> controls burst-then-slow cascade feel based on gold gained.
  - `_goldCollectionPitchJitter` -> optional slight random pitch variation for naturalness.
  - `_goldNumberSpawnAnchor` / `_gemNumberSpawnAnchor` -> optional per-currency spawn anchor overrides (`RectTransform`).
  - `_currencyNumberOffset` -> base spawn offset from the anchor top-right corner.
  - `_goldNumberOffset` / `_gemNumberOffset` -> optional per-currency additional offset.
  - `_animateBattleVictoryRewards` -> enabled to animate pending battle victory rewards on scene entry.
  - `_currencyNumberSpawnDepth` -> world projection depth used when converting gold/gem TMP screen positions to floating number spawn positions.
  - Debug testing (play mode): enable `_enableDebugSpawnHotkeys` and use `_debugSpawnGoldKey` / `_debugSpawnGemsKey` to spawn test floating numbers without completing a battle.
