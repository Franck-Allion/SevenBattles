using System;
using UnityEngine;

namespace SevenBattles.Core.Players
{
    /// <summary>
    /// Persistent progression state for tournament battles.
    /// Round indices are 1-based.
    /// </summary>
    [Serializable]
    public sealed class TournamentProgressState
    {
        [SerializeField, Min(1)]
        private int _currentRoundIndex = 1;

        [SerializeField]
        private bool[] _completedBattleFlags = Array.Empty<bool>();

        public int CurrentRoundIndex => Mathf.Max(1, _currentRoundIndex);

        public bool[] CompletedBattleFlags => _completedBattleFlags ?? Array.Empty<bool>();

        public bool IsBattleCompleted(int roundIndex)
        {
            if (roundIndex < 1)
            {
                return false;
            }

            if (_completedBattleFlags == null)
            {
                return false;
            }

            int idx = roundIndex - 1;
            return idx >= 0 && idx < _completedBattleFlags.Length && _completedBattleFlags[idx];
        }

        public bool MarkBattleCompleted(int roundIndex, int totalBattles)
        {
            int safeTotalBattles = Mathf.Max(1, totalBattles);
            if (roundIndex < 1 || roundIndex > safeTotalBattles)
            {
                return false;
            }

            EnsureCompletedArraySize(safeTotalBattles);

            bool changed = false;
            int completedIndex = roundIndex - 1;
            if (!_completedBattleFlags[completedIndex])
            {
                _completedBattleFlags[completedIndex] = true;
                changed = true;
            }

            int nextRound = ComputeNextRoundIndex(safeTotalBattles);
            if (_currentRoundIndex != nextRound)
            {
                _currentRoundIndex = nextRound;
                changed = true;
            }

            return changed;
        }

        public bool SetState(int currentRoundIndex, bool[] completedBattleFlags, int totalBattles)
        {
            int safeTotalBattles = Mathf.Max(1, totalBattles);
            bool[] sanitizedCompleted = new bool[safeTotalBattles];
            if (completedBattleFlags != null && completedBattleFlags.Length > 0)
            {
                Array.Copy(completedBattleFlags, sanitizedCompleted, Mathf.Min(safeTotalBattles, completedBattleFlags.Length));
            }

            int sanitizedCurrentRound = Mathf.Clamp(currentRoundIndex, 1, safeTotalBattles);
            if (sanitizedCompleted[sanitizedCurrentRound - 1])
            {
                sanitizedCurrentRound = ComputeFirstIncompleteOrLast(sanitizedCompleted);
            }

            bool changed = !AreFlagsEqual(_completedBattleFlags, sanitizedCompleted) || _currentRoundIndex != sanitizedCurrentRound;
            _completedBattleFlags = sanitizedCompleted;
            _currentRoundIndex = sanitizedCurrentRound;
            return changed;
        }

        public bool[] GetCompletedFlagsCopy()
        {
            var source = _completedBattleFlags ?? Array.Empty<bool>();
            if (source.Length == 0)
            {
                return Array.Empty<bool>();
            }

            var copy = new bool[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private void EnsureCompletedArraySize(int totalBattles)
        {
            if (_completedBattleFlags == null)
            {
                _completedBattleFlags = new bool[totalBattles];
                return;
            }

            if (_completedBattleFlags.Length == totalBattles)
            {
                return;
            }

            var resized = new bool[totalBattles];
            Array.Copy(_completedBattleFlags, resized, Mathf.Min(_completedBattleFlags.Length, totalBattles));
            _completedBattleFlags = resized;
        }

        private int ComputeNextRoundIndex(int totalBattles)
        {
            for (int i = 0; i < totalBattles; i++)
            {
                if (!_completedBattleFlags[i])
                {
                    return i + 1;
                }
            }

            return totalBattles;
        }

        private static int ComputeFirstIncompleteOrLast(bool[] completedBattleFlags)
        {
            if (completedBattleFlags == null || completedBattleFlags.Length == 0)
            {
                return 1;
            }

            for (int i = 0; i < completedBattleFlags.Length; i++)
            {
                if (!completedBattleFlags[i])
                {
                    return i + 1;
                }
            }

            return completedBattleFlags.Length;
        }

        private static bool AreFlagsEqual(bool[] a, bool[] b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
