using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Battle.Units;

namespace SevenBattles.Battle.Tiles
{
    public struct TileStatBonus
    {
        public int Life;
        public int Attack;
        public int Shoot;
        public int Spell;
        public int Speed;
        public int Luck;
        public int Defense;
        public int Protection;
        public int Initiative;
        public int Morale;

        public bool IsZero =>
            Life == 0 &&
            Attack == 0 &&
            Shoot == 0 &&
            Spell == 0 &&
            Speed == 0 &&
            Luck == 0 &&
            Defense == 0 &&
            Protection == 0 &&
            Initiative == 0 &&
            Morale == 0;

        public static TileStatBonus Subtract(TileStatBonus current, TileStatBonus previous)
        {
            return new TileStatBonus
            {
                Life = current.Life - previous.Life,
                Attack = current.Attack - previous.Attack,
                Shoot = current.Shoot - previous.Shoot,
                Spell = current.Spell - previous.Spell,
                Speed = current.Speed - previous.Speed,
                Luck = current.Luck - previous.Luck,
                Defense = current.Defense - previous.Defense,
                Protection = current.Protection - previous.Protection,
                Initiative = current.Initiative - previous.Initiative,
                Morale = current.Morale - previous.Morale
            };
        }
    }

    public static class BattleTileEffectRules
    {
        public static TileStatBonus GetStatBonus(BattlefieldTileColor color)
        {
            switch (color)
            {
                case BattlefieldTileColor.Red:
                    return new TileStatBonus
                    {
                        Attack = 1,
                        Shoot = 1,
                        Spell = 1,
                        Speed = 1,
                        Luck = 1,
                        Defense = 1,
                        Protection = 1,
                        Initiative = 1,
                        Morale = 1
                    };
                case BattlefieldTileColor.Gray:
                    return new TileStatBonus
                    {
                        Attack = 1,
                        Shoot = 1
                    };
                default:
                    return default;
            }
        }

        public static int GetSpellAmountBonus(BattlefieldTileColor color, DamageElement element)
        {
            switch (color)
            {
                case BattlefieldTileColor.Blue:
                    return element == DamageElement.Frost ? 1 : 0;
                case BattlefieldTileColor.Yellow:
                    return element == DamageElement.Lightning ? 1 : 0;
                case BattlefieldTileColor.Green:
                    return element == DamageElement.Poison ? 1 : 0;
                default:
                    return 0;
            }
        }

        public static bool TryGetTileColor(IBattlefieldService service, UnitBattleMetadata meta, out BattlefieldTileColor color)
        {
            color = BattlefieldTileColor.None;
            if (service == null || meta == null || !meta.HasTile)
            {
                return false;
            }

            return service.TryGetTileColor(meta.Tile, out color);
        }
    }
}
