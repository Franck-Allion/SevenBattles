using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using SevenBattles.UI;

namespace SevenBattles.Tests.UI
{
    public class BattleResultHUDRewardTests
    {
        [SetUp]
        public void SetUp()
        {
            BattleVictoryRewardTransfer.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BattleVictoryRewardTransfer.Clear();
        }

        private sealed class FakeBattleSessionService : MonoBehaviour, IBattleSessionService
        {
            public BattleSessionConfig CurrentSession { get; private set; }

            public void InitializeSession(BattleSessionConfig config)
            {
                CurrentSession = config;
            }

            public void ClearSession()
            {
                CurrentSession = null;
            }
        }

        [Test]
        public void ApplyRewardsToPlayerContext_UpdatesResourcesAndInventory()
        {
            var go = new GameObject("BattleResultHUD");
            var hud = go.AddComponent<BattleResultHUD>();

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            context.Inventory = inventory;
            context.SetResources(10, 2);

            var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipment.Id = "eq.sword";
            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.Id = "spell.firebolt";
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Id = "item.potion";

            var rewards = new BattleRewardResult(
                100,
                new[]
                {
                    new BattleRewardResultEntry(BattleRewardType.Gems, 5),
                    new BattleRewardResultEntry(equipment),
                    new BattleRewardResultEntry(spell),
                    new BattleRewardResultEntry(item, 2)
                });

            SetPrivate(hud, "_playerContext", context);
            InvokePrivate(hud, "ApplyRewardsToPlayerContext", rewards);

            Assert.AreEqual(110, context.Gold);
            Assert.AreEqual(7, context.Gems);

            var equipmentEntry = inventory.FindEntry(equipment.Id);
            var spellEntry = inventory.FindEntry(spell.Id);
            var itemEntry = inventory.FindEntry(item.Id);

            Assert.IsNotNull(equipmentEntry);
            Assert.AreEqual(InventoryEntry.EntryKind.Equipment, equipmentEntry.Kind);
            Assert.AreEqual(1, equipmentEntry.Quantity);

            Assert.IsNotNull(spellEntry);
            Assert.AreEqual(InventoryEntry.EntryKind.Spell, spellEntry.Kind);
            Assert.AreEqual(1, spellEntry.Quantity);

            Assert.IsNotNull(itemEntry);
            Assert.AreEqual(InventoryEntry.EntryKind.Item, itemEntry.Kind);
            Assert.AreEqual(2, itemEntry.Quantity);

            Object.DestroyImmediate(item);
            Object.DestroyImmediate(spell);
            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(context);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyRewardsToPlayerContext_ForTournamentBattle_MarksRoundCompleted()
        {
            var go = new GameObject("BattleResultHUD");
            var hud = go.AddComponent<BattleResultHUD>();

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetTournamentProgress(1, new[] { false, false, false, false, false, false, false });

            var sessionGo = new GameObject("SessionService");
            var sessionService = sessionGo.AddComponent<FakeBattleSessionService>();
            sessionService.InitializeSession(new BattleSessionConfig
            {
                BattleType = "tournament",
                CampaignMissionId = TournamentMissionIdUtil.BuildMissionId(1)
            });

            var rewards = new BattleRewardResult(50, new BattleRewardResultEntry[0]);

            SetPrivate(hud, "_playerContext", context);
            SetPrivate(hud, "_battleSessionService", sessionService);
            InvokePrivate(hud, "ApplyRewardsToPlayerContext", rewards);

            Assert.IsTrue(context.IsTournamentBattleCompleted(1));
            Assert.AreEqual(2, context.CurrentTournamentRoundIndex);

            Object.DestroyImmediate(sessionGo);
            Object.DestroyImmediate(context);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyRewardsToPlayerContext_SetsPreparationTransferUsingPreAndPostCurrency()
        {
            var go = new GameObject("BattleResultHUD");
            var hud = go.AddComponent<BattleResultHUD>();

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetResources(40, 3);

            var rewards = new BattleRewardResult(
                20,
                new[]
                {
                    new BattleRewardResultEntry(BattleRewardType.Gold, 5),
                    new BattleRewardResultEntry(BattleRewardType.Gems, 4)
                });

            SetPrivate(hud, "_playerContext", context);
            InvokePrivate(hud, "ApplyRewardsToPlayerContext", rewards);

            Assert.AreEqual(65, context.Gold);
            Assert.AreEqual(7, context.Gems);
            Assert.IsTrue(BattleVictoryRewardTransfer.TryConsume(out var pending));
            Assert.AreEqual(40, pending.FromGold);
            Assert.AreEqual(3, pending.FromGems);
            Assert.AreEqual(25, pending.GoldGained);
            Assert.AreEqual(4, pending.GemsGained);
            Assert.AreEqual(65, pending.ToGold);
            Assert.AreEqual(7, pending.ToGems);

            Object.DestroyImmediate(context);
            Object.DestroyImmediate(go);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found.");
            method.Invoke(target, args);
        }
    }
}
