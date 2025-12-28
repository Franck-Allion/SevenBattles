using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Spells;
using SevenBattles.Battle.Tiles;
using SevenBattles.Battle.Save;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class TileColorEffectsTests
    {
        private sealed class FakeBattlefieldService : MonoBehaviour, IBattlefieldService
        {
            private readonly Dictionary<Vector2Int, BattlefieldTileColor> _tileColors = new Dictionary<Vector2Int, BattlefieldTileColor>();

            public BattlefieldDefinition Current => null;
            public event Action<BattlefieldDefinition> BattlefieldChanged;

            public void SetTileColor(Vector2Int tile, BattlefieldTileColor color)
            {
                _tileColors[tile] = color;
                BattlefieldChanged?.Invoke(null);
            }

            public bool TryGetTileColor(Vector2Int tile, out BattlefieldTileColor color)
            {
                if (_tileColors.TryGetValue(tile, out color))
                {
                    return true;
                }

                color = BattlefieldTileColor.None;
                return true;
            }
        }

        [Test]
        public void RedTile_BonusAppliesAndRemoves()
        {
            var battlefieldGo = new GameObject("BattlefieldService");
            var battlefield = battlefieldGo.AddComponent<FakeBattlefieldService>();
            var tile = new Vector2Int(0, 0);
            battlefield.SetTileColor(tile, BattlefieldTileColor.Red);

            var unitGo = new GameObject("Unit");
            var stats = unitGo.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData
            {
                Life = 10,
                Attack = 2,
                Shoot = 3,
                Spell = 4,
                Speed = 5,
                Luck = 6,
                Defense = 7,
                Protection = 8,
                Initiative = 9,
                Morale = 10,
                ActionPoints = 1
            });
            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            UnitBattleMetadata.Ensure(unitGo, true, def, tile);

            var controllerGo = new GameObject("TurnController");
            var controller = controllerGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(controller, "_battlefieldServiceBehaviour", battlefield);
            CallPrivate(controller, "BeginBattle");

            Assert.AreEqual(10, stats.MaxLife);
            Assert.AreEqual(10, stats.Life);
            Assert.AreEqual(3, stats.Attack);
            Assert.AreEqual(4, stats.Shoot);
            Assert.AreEqual(5, stats.Spell);
            Assert.AreEqual(6, stats.Speed);
            Assert.AreEqual(7, stats.Luck);
            Assert.AreEqual(8, stats.Defense);
            Assert.AreEqual(9, stats.Protection);
            Assert.AreEqual(10, stats.Initiative);
            Assert.AreEqual(11, stats.Morale);

            battlefield.SetTileColor(tile, BattlefieldTileColor.None);
            CallPrivate(controller, "ApplyTileStatBonusesForAllUnits");

            Assert.AreEqual(10, stats.MaxLife);
            Assert.AreEqual(10, stats.Life);
            Assert.AreEqual(2, stats.Attack);
            Assert.AreEqual(3, stats.Shoot);
            Assert.AreEqual(4, stats.Spell);
            Assert.AreEqual(5, stats.Speed);
            Assert.AreEqual(6, stats.Luck);
            Assert.AreEqual(7, stats.Defense);
            Assert.AreEqual(8, stats.Protection);
            Assert.AreEqual(9, stats.Initiative);
            Assert.AreEqual(10, stats.Morale);

            UnityEngine.Object.DestroyImmediate(controllerGo);
            UnityEngine.Object.DestroyImmediate(unitGo);
            UnityEngine.Object.DestroyImmediate(def);
            UnityEngine.Object.DestroyImmediate(battlefieldGo);
        }

        [Test]
        public void GrayTile_BonusOnlyAffectsAttackAndShoot()
        {
            var battlefieldGo = new GameObject("BattlefieldService");
            var battlefield = battlefieldGo.AddComponent<FakeBattlefieldService>();
            var tile = new Vector2Int(1, 0);
            battlefield.SetTileColor(tile, BattlefieldTileColor.Gray);

            var unitGo = new GameObject("Unit");
            var stats = unitGo.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 5, Attack = 1, Shoot = 2, Spell = 3, Speed = 4, ActionPoints = 1 });
            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            UnitBattleMetadata.Ensure(unitGo, true, def, tile);

            var controllerGo = new GameObject("TurnController");
            var controller = controllerGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(controller, "_battlefieldServiceBehaviour", battlefield);
            CallPrivate(controller, "BeginBattle");

            Assert.AreEqual(2, stats.Attack);
            Assert.AreEqual(3, stats.Shoot);
            Assert.AreEqual(3, stats.Spell);
            Assert.AreEqual(4, stats.Speed);

            UnityEngine.Object.DestroyImmediate(controllerGo);
            UnityEngine.Object.DestroyImmediate(unitGo);
            UnityEngine.Object.DestroyImmediate(def);
            UnityEngine.Object.DestroyImmediate(battlefieldGo);
        }

        [Test]
        public void SpellPreview_AddsElementTileBonus()
        {
            var battlefieldGo = new GameObject("BattlefieldService");
            var battlefield = battlefieldGo.AddComponent<FakeBattlefieldService>();
            var tile = new Vector2Int(2, 0);
            battlefield.SetTileColor(tile, BattlefieldTileColor.Blue);

            var casterGo = new GameObject("Caster");
            var stats = casterGo.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 5, Spell = 1, ActionPoints = 1 });
            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            var meta = UnitBattleMetadata.Ensure(casterGo, true, def, tile);

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.PrimaryAmountKind = SpellPrimaryAmountKind.Damage;
            spell.PrimaryDamageElement = DamageElement.Frost;
            spell.PrimaryBaseAmount = 4;
            spell.PrimarySpellStatScaling = 0f;

            var ctrlGo = new GameObject("SpellController");
            var ctrl = ctrlGo.AddComponent<BattleSpellController>();
            SetPrivate(ctrl, "_battlefieldServiceBehaviour", battlefield);

            Assert.IsTrue(ctrl.TryGetSpellAmountPreview(spell, meta, stats, out var preview));
            Assert.AreEqual(4, preview.BaseAmount);
            Assert.AreEqual(5, preview.ModifiedAmount);

            UnityEngine.Object.DestroyImmediate(ctrlGo);
            UnityEngine.Object.DestroyImmediate(spell);
            UnityEngine.Object.DestroyImmediate(casterGo);
            UnityEngine.Object.DestroyImmediate(def);
            UnityEngine.Object.DestroyImmediate(battlefieldGo);
        }

        [Test]
        public void SaveProvider_StripsTileBonusFromStats()
        {
            var battlefieldGo = new GameObject("BattlefieldService");
            var battlefield = battlefieldGo.AddComponent<FakeBattlefieldService>();
            var tile = new Vector2Int(0, 1);
            battlefield.SetTileColor(tile, BattlefieldTileColor.Red);

            var unitGo = new GameObject("Unit");
            var stats = unitGo.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 10, Attack = 2, ActionPoints = 1 });
            stats.ApplyStatDelta(BattleTileEffectRules.GetStatBonus(BattlefieldTileColor.Red));

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            UnitBattleMetadata.Ensure(unitGo, true, def, tile);

            var providerGo = new GameObject("SaveProvider");
            var provider = providerGo.AddComponent<BattleBoardGameStateSaveProvider>();
            SetPrivate(provider, "_battlefieldServiceBehaviour", battlefield);

            var data = new SevenBattles.Core.Save.SaveGameData();
            provider.PopulateGameState(data);

            Assert.IsNotNull(data.UnitPlacements);
            Assert.AreEqual(1, data.UnitPlacements.Length);
            var saved = data.UnitPlacements[0].Stats;
            Assert.IsNotNull(saved);
            Assert.AreEqual(10, saved.Life);
            Assert.AreEqual(10, saved.MaxLife);
            Assert.AreEqual(2, saved.Attack);

            UnityEngine.Object.DestroyImmediate(providerGo);
            UnityEngine.Object.DestroyImmediate(unitGo);
            UnityEngine.Object.DestroyImmediate(def);
            UnityEngine.Object.DestroyImmediate(battlefieldGo);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private static void CallPrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found.");
            method.Invoke(target, null);
        }
    }
}
