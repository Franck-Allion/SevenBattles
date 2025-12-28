using System;
using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core
{
    public interface IBattlefieldService
    {
        BattlefieldDefinition Current { get; }
        event Action<BattlefieldDefinition> BattlefieldChanged;
        bool TryGetTileColor(Vector2Int tile, out BattlefieldTileColor color);
    }
}
