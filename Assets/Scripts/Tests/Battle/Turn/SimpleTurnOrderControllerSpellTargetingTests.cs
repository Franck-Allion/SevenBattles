using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Spells;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using System.Reflection;

namespace SevenBattles.Tests.Battle
{
    public class SimpleTurnOrderControllerSpellTargetingTests
    {
        [Test]
        public void IsTileLegalSpellTarget_ReturnsTrue_ForEnemyUnitInRange_WhenEnoughAp()
        {
            var (ctrl, boardGo, ctrlGo, cleanup) = CreateControllerWithBoard(columns: 6, rows: 6);

            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            var enemyDef = ScriptableObject.CreateInstance<UnitDefinition>();

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 3, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, enemyDef, new Vector2Int(4, 2));

            CallPrivate(ctrl, "BeginBattle");

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.ActionPointCost = 1;
            spell.TargetFilter = SpellTargetFilter.EnemyUnit;
            spell.MinCastRange = 1;
            spell.MaxCastRange = 4;

            bool eligible = (bool)CallPrivate(ctrl, "IsTileLegalSpellTarget", spell, new Vector2Int(4, 2));
            Assert.IsTrue(eligible);

            Object.DestroyImmediate(spell);
            cleanup(new Object[] { playerGo, enemyGo, playerDef, enemyDef });
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
        }

        [Test]
        public void IsTileLegalSpellTarget_ReturnsFalse_ForFriendlyUnit_WhenEnemyOnly()
        {
            var (ctrl, boardGo, ctrlGo, cleanup) = CreateControllerWithBoard(columns: 6, rows: 6);

            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 3, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            var allyGo = new GameObject("AllyUnit");
            var allyStats = allyGo.AddComponent<UnitStats>();
            allyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 1 });
            UnitBattleMetadata.Ensure(allyGo, true, playerDef, new Vector2Int(3, 2));

            CallPrivate(ctrl, "BeginBattle");

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.ActionPointCost = 1;
            spell.TargetFilter = SpellTargetFilter.EnemyUnit;
            spell.MinCastRange = 1;
            spell.MaxCastRange = 4;

            bool eligible = (bool)CallPrivate(ctrl, "IsTileLegalSpellTarget", spell, new Vector2Int(3, 2));
            Assert.IsFalse(eligible);

            Object.DestroyImmediate(spell);
            cleanup(new Object[] { playerGo, allyGo, playerDef });
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
        }

        [Test]
        public void IsTileLegalSpellTarget_ReturnsTrue_ForSelf_WhenFriendlyAndMinRangeZero()
        {
            var (ctrl, boardGo, ctrlGo, cleanup) = CreateControllerWithBoard(columns: 6, rows: 6);

            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 3, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            CallPrivate(ctrl, "BeginBattle");

            var heal = ScriptableObject.CreateInstance<SpellDefinition>();
            heal.ActionPointCost = 2;
            heal.TargetFilter = SpellTargetFilter.FriendlyUnit;
            heal.MinCastRange = 0;
            heal.MaxCastRange = 3;

            bool eligible = (bool)CallPrivate(ctrl, "IsTileLegalSpellTarget", heal, new Vector2Int(2, 2));
            Assert.IsTrue(eligible);

            Object.DestroyImmediate(heal);
            cleanup(new Object[] { playerGo, playerDef });
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
        }

        [Test]
        public void IsTileLegalSpellTarget_ReturnsFalse_WhenOutOfRange()
        {
            var (ctrl, boardGo, ctrlGo, cleanup) = CreateControllerWithBoard(columns: 8, rows: 8);

            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            var enemyDef = ScriptableObject.CreateInstance<UnitDefinition>();

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 3, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(1, 1));

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, enemyDef, new Vector2Int(7, 7));

            CallPrivate(ctrl, "BeginBattle");

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.ActionPointCost = 1;
            spell.TargetFilter = SpellTargetFilter.EnemyUnit;
            spell.MinCastRange = 1;
            spell.MaxCastRange = 3;

            bool eligible = (bool)CallPrivate(ctrl, "IsTileLegalSpellTarget", spell, new Vector2Int(7, 7));
            Assert.IsFalse(eligible);

            Object.DestroyImmediate(spell);
            cleanup(new Object[] { playerGo, enemyGo, playerDef, enemyDef });
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
        }

        [Test]
        public void IsTileLegalSpellTarget_ReturnsFalse_WhenNotEnoughAp()
        {
            var (ctrl, boardGo, ctrlGo, cleanup) = CreateControllerWithBoard(columns: 6, rows: 6);

            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            var allyDef = ScriptableObject.CreateInstance<UnitDefinition>();

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            var allyGo = new GameObject("AllyUnit");
            var allyStats = allyGo.AddComponent<UnitStats>();
            allyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 1 });
            UnitBattleMetadata.Ensure(allyGo, true, allyDef, new Vector2Int(3, 2));

            CallPrivate(ctrl, "BeginBattle");

            var heal = ScriptableObject.CreateInstance<SpellDefinition>();
            heal.ActionPointCost = 2;
            heal.TargetFilter = SpellTargetFilter.FriendlyUnit;
            heal.MinCastRange = 0;
            heal.MaxCastRange = 3;

            bool eligible = (bool)CallPrivate(ctrl, "IsTileLegalSpellTarget", heal, new Vector2Int(3, 2));
            Assert.IsFalse(eligible);

            Object.DestroyImmediate(heal);
            cleanup(new Object[] { playerGo, allyGo, playerDef, allyDef });
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
        }

        private static (SimpleTurnOrderController ctrl, GameObject boardGo, GameObject ctrlGo, System.Action<Object[]> cleanup) CreateControllerWithBoard(int columns, int rows)
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", columns);
            SetPrivate(board, "_rows", rows);
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            // Add dependencies first so Awake finds them
            var spellCtrl = ctrlGo.AddComponent<BattleSpellController>();
            SetPrivate(spellCtrl, "_board", board);

            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);
            // No need to SetPrivate _spellController, Awake handles it

            System.Action<Object[]> cleanup = (objs) =>
            {
                if (objs == null) return;
                for (int i = 0; i < objs.Length; i++)
                {
                    if (objs[i] != null)
                    {
                        Object.DestroyImmediate(objs[i]);
                    }
                }
            };

            return (ctrl, boardGo, ctrlGo, cleanup);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Field '{field}' not found on {obj.GetType().Name}");
            f.SetValue(obj, value);
        }

        private static object CallPrivate(object obj, string method, params object[] args)
        {
            var m = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, $"Method '{method}' not found on {obj.GetType().Name}");
            return m.Invoke(obj, args);
        }
    }
}
