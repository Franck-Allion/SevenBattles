using System;
using System.Collections.Generic;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;

namespace SevenBattles.Core.Players
{
    /// <summary>
    /// Persistent player-owned unit entry with stable identity.
    /// </summary>
    [Serializable]
    public sealed class OwnedUnitData
    {
        public string OwnedUnitId;
        public string CustomName;
        public UnitDefinition Definition;
        public int Level = UnitSpellLoadout.DefaultLevel;
        public int Xp;
        public SpellDefinition[] Spells = Array.Empty<SpellDefinition>();

        public int EffectiveLevel => Level > 0 ? Level : UnitSpellLoadout.DefaultLevel;
        public int EffectiveXp => Xp > 0 ? Xp : 0;
        public string UnitId => Definition != null ? Definition.Id : null;

        public static OwnedUnitData Clone(OwnedUnitData source)
        {
            if (source == null)
            {
                return null;
            }

            return new OwnedUnitData
            {
                OwnedUnitId = source.OwnedUnitId,
                CustomName = source.CustomName,
                Definition = source.Definition,
                Level = source.EffectiveLevel,
                Xp = source.EffectiveXp,
                Spells = source.Spells != null ? (SpellDefinition[])source.Spells.Clone() : Array.Empty<SpellDefinition>()
            };
        }
    }

    /// <summary>
    /// Centralized naming policy for player-owned units.
    /// </summary>
    public static class OwnedUnitNamingPolicy
    {
        public const int MaxCustomNameLength = 20;
        private const string DefaultBaseName = "Unit";

        /// <summary>
        /// Sanitizes a raw custom name (trim + max length). Returns empty if the result is invalid.
        /// </summary>
        public static string SanitizeCustomName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            string trimmed = rawName.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            if (trimmed.Length > MaxCustomNameLength)
            {
                trimmed = trimmed.Substring(0, MaxCustomNameLength);
            }

            return trimmed;
        }

        /// <summary>
        /// Returns the final display name for a single unit rename operation.
        /// Empty user input falls back to an auto-generated default.
        /// </summary>
        public static string NormalizeSingleName(
            string requestedName,
            OwnedUnitData owner,
            IReadOnlyList<OwnedUnitData> allOwnedUnits)
        {
            string sanitized = SanitizeCustomName(requestedName);
            if (!string.IsNullOrEmpty(sanitized))
            {
                return sanitized;
            }

            var occupied = BuildOccupiedNameSet(allOwnedUnits, owner != null ? owner.OwnedUnitId : null);
            return GenerateDefaultName(owner, occupied);
        }

        /// <summary>
        /// Normalizes names in-place for the full owned-unit list.
        /// Existing non-empty custom names are sanitized and preserved.
        /// Missing/empty names are replaced by deterministic defaults.
        /// </summary>
        public static void NormalizeAllInPlace(IReadOnlyList<OwnedUnitData> allOwnedUnits)
        {
            if (allOwnedUnits == null || allOwnedUnits.Count == 0)
            {
                return;
            }

            var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < allOwnedUnits.Count; i++)
            {
                OwnedUnitData unit = allOwnedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                string sanitized = SanitizeCustomName(unit.CustomName);
                unit.CustomName = sanitized;
                if (!string.IsNullOrEmpty(sanitized))
                {
                    occupied.Add(sanitized);
                }
            }

            for (int i = 0; i < allOwnedUnits.Count; i++)
            {
                OwnedUnitData unit = allOwnedUnits[i];
                if (unit == null || !string.IsNullOrEmpty(unit.CustomName))
                {
                    continue;
                }

                string generated = GenerateDefaultName(unit, occupied);
                unit.CustomName = generated;
                occupied.Add(generated);
            }
        }

        public static string ResolveDisplayName(OwnedUnitData unit)
        {
            if (unit == null)
            {
                return string.Empty;
            }

            string sanitized = SanitizeCustomName(unit.CustomName);
            if (!string.IsNullOrEmpty(sanitized))
            {
                return sanitized;
            }

            return ResolveBaseName(unit.Definition);
        }

        private static HashSet<string> BuildOccupiedNameSet(IReadOnlyList<OwnedUnitData> units, string excludedOwnedUnitId)
        {
            var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (units == null || units.Count == 0)
            {
                return occupied;
            }

            for (int i = 0; i < units.Count; i++)
            {
                OwnedUnitData unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(excludedOwnedUnitId) &&
                    string.Equals(unit.OwnedUnitId, excludedOwnedUnitId, StringComparison.Ordinal))
                {
                    continue;
                }

                string sanitized = SanitizeCustomName(unit.CustomName);
                if (!string.IsNullOrEmpty(sanitized))
                {
                    occupied.Add(sanitized);
                }
            }

            return occupied;
        }

        private static string GenerateDefaultName(OwnedUnitData owner, HashSet<string> occupied)
        {
            string baseName = ResolveBaseName(owner != null ? owner.Definition : null);
            for (int i = 1; i <= 99999; i++)
            {
                string candidate = $"{baseName}-{i}";
                if (occupied == null || !occupied.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{baseName}-1";
        }

        private static string ResolveBaseName(UnitDefinition definition)
        {
            string baseName = null;
            if (definition != null)
            {
                if (!string.IsNullOrWhiteSpace(definition.name))
                {
                    baseName = definition.name;
                }
                else if (!string.IsNullOrWhiteSpace(definition.Id))
                {
                    baseName = definition.Id;
                }
            }

            baseName = SanitizeCustomName(baseName);
            if (string.IsNullOrEmpty(baseName))
            {
                return DefaultBaseName;
            }

            return baseName;
        }
    }
}
