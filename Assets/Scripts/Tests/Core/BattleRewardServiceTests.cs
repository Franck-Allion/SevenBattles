using System;
using NUnit.Framework;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Items;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class BattleRewardServiceTests
    {
        private BattleRewardService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new BattleRewardService();
        }

        [Test]
        public void ComputeRewards_NullTable_ReturnsGoldZeroAndNoBonus()
        {
            BattleRewardResult result = _service.ComputeRewards(null, new SequenceRandom(0.25));

            Assert.AreEqual(0, result.GoldAmount);
            Assert.IsNotNull(result.BonusRewards);
            Assert.AreEqual(0, result.BonusRewards.Length);
        }

        [Test]
        public void ComputeRewards_AllWeightsZero_ReturnsGoldOnly()
        {
            var table = new BattleRewardTable
            {
                GoldMin = 123,
                GoldMax = 123,
                MaxBonusRewards = 2,
                BonusPool = new[]
                {
                    new BattleRewardEntry
                    {
                        Type = BattleRewardType.Item,
                        ItemRef = ScriptableObject.CreateInstance<ItemDefinition>(),
                        Weight = 0f
                    },
                    new BattleRewardEntry
                    {
                        Type = BattleRewardType.Spell,
                        SpellRef = ScriptableObject.CreateInstance<SpellDefinition>(),
                        Weight = 0f
                    }
                }
            };

            BattleRewardResult result = _service.ComputeRewards(table, new SequenceRandom(0.1));

            Assert.AreEqual(123, result.GoldAmount);
            Assert.AreEqual(0, result.BonusRewards.Length);
        }

        [Test]
        public void ComputeRewards_RemovesPickedEntries_AvoidsDuplicates()
        {
            var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipment.Name = "Iron Sword";
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Name = "Potion";

            var table = new BattleRewardTable
            {
                GoldMin = 50,
                GoldMax = 50,
                MaxBonusRewards = 2,
                BonusPool = new[]
                {
                    new BattleRewardEntry
                    {
                        Type = BattleRewardType.Equipment,
                        EquipmentRef = equipment,
                        Weight = 1f
                    },
                    new BattleRewardEntry
                    {
                        Type = BattleRewardType.Item,
                        ItemRef = item,
                        Weight = 1f
                    }
                }
            };

            // slot0 drop, pick first entry; slot1 drop, pick remaining entry.
            BattleRewardResult result = _service.ComputeRewards(table, new SequenceRandom(0.1, 0.0, 0.1, 0.0));

            Assert.AreEqual(50, result.GoldAmount);
            Assert.AreEqual(2, result.BonusRewards.Length);
            Assert.AreEqual(BattleRewardType.Equipment, result.BonusRewards[0].Type);
            Assert.AreEqual(BattleRewardType.Item, result.BonusRewards[1].Type);
            Assert.AreSame(equipment, result.BonusRewards[0].EquipmentDef);
            Assert.AreSame(item, result.BonusRewards[1].ItemDef);
        }

        private sealed class SequenceRandom : System.Random
        {
            private readonly double[] _samples;
            private int _index;

            public SequenceRandom(params double[] samples)
            {
                _samples = samples ?? Array.Empty<double>();
            }

            protected override double Sample()
            {
                if (_samples.Length == 0)
                {
                    return 0.5d;
                }

                double value = _samples[_index < _samples.Length ? _index++ : _samples.Length - 1];
                if (value < 0d)
                {
                    return 0d;
                }

                if (value >= 1d)
                {
                    return 0.999999d;
                }

                return value;
            }
        }
    }
}
