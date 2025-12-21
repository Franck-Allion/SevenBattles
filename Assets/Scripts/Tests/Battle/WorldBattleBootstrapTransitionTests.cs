using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using System.Collections;
using SevenBattles.Battle.Start;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Turn;

namespace SevenBattles.Tests.Battle
{
    public class WorldBattleBootstrapTransitionTests
    {
        private class FakePlacementController : MonoBehaviour, ISquadPlacementController
        {
            public int SquadSize => 0;
            public bool IsReady => true;
            public bool IsLocked => true;

            public event System.Action<int> WizardSelected;
            public event System.Action<int> WizardPlaced;
            public event System.Action<int> WizardRemoved;
            public event System.Action<bool> ReadyChanged;
            public event System.Action PlacementLocked;

            public bool IsPlaced(int index) => false;
            public Sprite GetPortrait(int index) => null;
            public void SelectWizard(int index) { }
            public void ConfirmAndLock() { }

            public void FirePlacementLocked()
            {
                PlacementLocked?.Invoke();
            }
        }

        private class FakeTurnController : MonoBehaviour, IBattleTurnController
        {
            public bool HasActiveUnit => true;
            public bool IsActiveUnitPlayerControlled => true;
            public Sprite ActiveUnitPortrait => null;
            public SpellDefinition[] ActiveUnitSpells => System.Array.Empty<SpellDefinition>();
            public event System.Action ActiveUnitChanged;
            public event System.Action ActiveUnitActionPointsChanged;
            public event System.Action ActiveUnitStatsChanged;
            public bool IsInteractionLocked { get; private set; }
            public bool StartBattleCalled { get; private set; }
            public int ActiveUnitCurrentActionPoints => 0;
            public int ActiveUnitMaxActionPoints => 0;
            public int TurnIndex => 0;
            public bool HasBattleEnded => false;
            public BattleOutcome Outcome => BattleOutcome.None;
            public event System.Action<BattleOutcome> BattleEnded;

            public bool TryGetActiveUnitStats(out UnitStatsViewData stats)
            {
                stats = default;
                return false;
            }

            public bool TryGetActiveUnitSpellAmountPreview(SpellDefinition spell, out SpellAmountPreview preview)
            {
                preview = default;
                return false;
            }

            public void RequestEndTurn()
            {
            }

            public void StartBattle()
            {
                StartBattleCalled = true;
            }

            public void SetInteractionLocked(bool locked)
            {
                IsInteractionLocked = locked;
            }
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fi.SetValue(obj, value);
        }

        private static void CallPrivate(object obj, string method)
        {
            var mi = obj.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Invoke(obj, null);
        }
    }
}
