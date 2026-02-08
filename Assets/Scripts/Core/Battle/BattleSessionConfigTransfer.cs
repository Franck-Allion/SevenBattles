using System;

namespace SevenBattles.Core.Battle
{
    /// <summary>
    /// Transient handoff for BattleSessionConfig between scenes (e.g., Preparation -> BattleScene).
    /// </summary>
    public static class BattleSessionConfigTransfer
    {
        private static BattleSessionConfig _pending;

        public static bool HasPending => _pending != null;

        public static void SetPending(BattleSessionConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _pending = config;
        }

        public static bool TryConsume(out BattleSessionConfig config)
        {
            if (_pending == null)
            {
                config = null;
                return false;
            }

            config = _pending;
            _pending = null;
            return true;
        }

        public static void Clear()
        {
            _pending = null;
        }
    }
}
