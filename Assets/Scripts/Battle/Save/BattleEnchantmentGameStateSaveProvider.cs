using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Spells;
using SevenBattles.Core.Save;

namespace SevenBattles.Battle.Save
{
    /// <summary>
    /// Captures active battlefield enchantments so they can be restored on load.
    /// </summary>
    public sealed class BattleEnchantmentGameStateSaveProvider : MonoBehaviour, IGameStateSaveProvider
    {
        private readonly List<BattleEnchantmentController.EnchantmentSnapshot> _buffer =
            new List<BattleEnchantmentController.EnchantmentSnapshot>();

        public void PopulateGameState(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var controller = UnityEngine.Object.FindFirstObjectByType<BattleEnchantmentController>();
            if (controller == null)
            {
                data.BattleEnchantments = Array.Empty<BattleEnchantmentSaveData>();
                return;
            }

            controller.CopyActiveEnchantments(_buffer);
            if (_buffer.Count == 0)
            {
                data.BattleEnchantments = Array.Empty<BattleEnchantmentSaveData>();
                return;
            }

            var list = new List<BattleEnchantmentSaveData>(_buffer.Count);
            for (int i = 0; i < _buffer.Count; i++)
            {
                var snapshot = _buffer[i];
                var spell = snapshot.Spell;
                if (spell == null || string.IsNullOrEmpty(spell.Id))
                {
                    continue;
                }

                list.Add(new BattleEnchantmentSaveData
                {
                    SpellId = spell.Id,
                    QuadIndex = snapshot.QuadIndex,
                    CasterInstanceId = snapshot.CasterInstanceId,
                    CasterUnitId = snapshot.CasterUnitId,
                    CasterTeam = snapshot.IsPlayerControlledCaster ? "player" : "enemy"
                });
            }

            data.BattleEnchantments = list.Count == 0 ? Array.Empty<BattleEnchantmentSaveData>() : list.ToArray();
        }
    }
}
