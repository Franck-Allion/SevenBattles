using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Save;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Save;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class BattleBoardGameStateSaveProviderTests
    {
        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        [Test]
        public void PopulateGameState_CapturesPlayerAndEnemyPlacements()
        {
            var playerGo = new GameObject("PlayerUnit");
            var enemyGo = new GameObject("EnemyUnit");

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Id = "UnitA";

            var playerMeta = UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(1, 2));
            var enemyMeta = UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(3, 4));

            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10 });

            var providerGo = new GameObject("Provider");
            var provider = providerGo.AddComponent<BattleBoardGameStateSaveProvider>();

            var data = new SaveGameData();
            provider.PopulateGameState(data);

            Assert.IsNotNull(data.UnitPlacements, "UnitPlacements should not be null after PopulateGameState.");
            Assert.AreEqual(2, data.UnitPlacements.Length, "There should be two unit placements captured.");

            var playerPlacement = data.UnitPlacements[0].Team == "player"
                ? data.UnitPlacements[0]
                : data.UnitPlacements[1];
            var enemyPlacement = data.UnitPlacements[0].Team == "enemy"
                ? data.UnitPlacements[0]
                : data.UnitPlacements[1];

            Assert.IsFalse(string.IsNullOrEmpty(playerPlacement.InstanceId), "Player placement should have a non-empty InstanceId.");
            Assert.IsFalse(string.IsNullOrEmpty(enemyPlacement.InstanceId), "Enemy placement should have a non-empty InstanceId.");

            Assert.AreEqual("UnitA", playerPlacement.UnitId);
            Assert.AreEqual("player", playerPlacement.Team);
            Assert.AreEqual(1, playerPlacement.X);
            Assert.AreEqual(2, playerPlacement.Y);
            Assert.IsFalse(playerPlacement.Dead, "Player unit should be alive by default.");
            Assert.IsTrue(
                playerPlacement.Facing == "up" ||
                playerPlacement.Facing == "down" ||
                playerPlacement.Facing == "left" ||
                playerPlacement.Facing == "right",
                "Player placement facing should be one of 'up', 'down', 'left', or 'right'.");

            Assert.AreEqual("UnitA", enemyPlacement.UnitId);
            Assert.AreEqual("enemy", enemyPlacement.Team);
            Assert.AreEqual(3, enemyPlacement.X);
            Assert.AreEqual(4, enemyPlacement.Y);
            Assert.IsFalse(enemyPlacement.Dead, "Enemy unit should be alive when Life > 0.");
            Assert.IsNotNull(enemyPlacement.Stats, "Enemy stats should be populated.");
            Assert.AreEqual(10, enemyPlacement.Stats.Life);
            Assert.AreEqual(enemyStats.MaxLife, enemyPlacement.Stats.MaxLife);
            Assert.IsTrue(
                enemyPlacement.Facing == "up" ||
                enemyPlacement.Facing == "down" ||
                enemyPlacement.Facing == "left" ||
                enemyPlacement.Facing == "right",
                "Enemy placement facing should be one of 'up', 'down', 'left', or 'right'.");

            Object.DestroyImmediate(providerGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void PopulateGameState_HandlesMissingTileGracefully()
        {
            var unitGo = new GameObject("UnitWithNoTile");
            var meta = unitGo.AddComponent<UnitBattleMetadata>();
            SetPrivate(meta, "_isPlayerControlled", true);

            var providerGo = new GameObject("Provider");
            var provider = providerGo.AddComponent<BattleBoardGameStateSaveProvider>();

            var data = new SaveGameData();

            Assert.DoesNotThrow(() => provider.PopulateGameState(data), "PopulateGameState should handle units without tiles gracefully.");
            Assert.IsNotNull(data.UnitPlacements);
            Assert.AreEqual(1, data.UnitPlacements.Length);
            Assert.AreEqual(-1, data.UnitPlacements[0].X);
            Assert.AreEqual(-1, data.UnitPlacements[0].Y);

            Object.DestroyImmediate(providerGo);
            Object.DestroyImmediate(unitGo);
        }

        [Test]
        public void PopulateGameState_QuantizesFacingToCardinalDirections()
        {
            var upGo = new GameObject("UpUnit");
            var upMeta = upGo.AddComponent<UnitBattleMetadata>();
            SetPrivate(upMeta, "_isPlayerControlled", true);
            upMeta.Tile = new Vector2Int(0, 0);
            upMeta.Facing = new Vector2(0.1f, 1f);

            var downGo = new GameObject("DownUnit");
            var downMeta = downGo.AddComponent<UnitBattleMetadata>();
            SetPrivate(downMeta, "_isPlayerControlled", false);
            downMeta.Tile = new Vector2Int(1, 0);
            downMeta.Facing = new Vector2(-0.1f, -1f);

            var leftGo = new GameObject("LeftUnit");
            var leftMeta = leftGo.AddComponent<UnitBattleMetadata>();
            SetPrivate(leftMeta, "_isPlayerControlled", true);
            leftMeta.Tile = new Vector2Int(2, 0);
            leftMeta.Facing = new Vector2(-1f, 0.2f);

            var rightGo = new GameObject("RightUnit");
            var rightMeta = rightGo.AddComponent<UnitBattleMetadata>();
            SetPrivate(rightMeta, "_isPlayerControlled", false);
            rightMeta.Tile = new Vector2Int(3, 0);
            rightMeta.Facing = new Vector2(1f, -0.2f);

            var providerGo = new GameObject("Provider");
            var provider = providerGo.AddComponent<BattleBoardGameStateSaveProvider>();

            var data = new SaveGameData();
            provider.PopulateGameState(data);

            Assert.IsNotNull(data.UnitPlacements);
            Assert.AreEqual(4, data.UnitPlacements.Length, "Expected four unit placements.");

            bool hasUp = false, hasDown = false, hasLeft = false, hasRight = false;
            for (int i = 0; i < data.UnitPlacements.Length; i++)
            {
                var facing = data.UnitPlacements[i].Facing;
                if (facing == "up") hasUp = true;
                if (facing == "down") hasDown = true;
                if (facing == "left") hasLeft = true;
                if (facing == "right") hasRight = true;
            }

            Assert.IsTrue(hasUp, "One placement should be quantized to 'up'.");
            Assert.IsTrue(hasDown, "One placement should be quantized to 'down'.");
            Assert.IsTrue(hasLeft, "One placement should be quantized to 'left'.");
            Assert.IsTrue(hasRight, "One placement should be quantized to 'right'.");

            Object.DestroyImmediate(providerGo);
            Object.DestroyImmediate(upGo);
            Object.DestroyImmediate(downGo);
            Object.DestroyImmediate(leftGo);
            Object.DestroyImmediate(rightGo);
        }
    }
}
