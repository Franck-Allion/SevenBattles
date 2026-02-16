using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace SevenBattles.Core.Diagnostics
{
    /// <summary>
    /// Tracks localization tables preloaded through ScenePreloader and logs
    /// whether displayed localized strings are backed by those preloaded tables.
    /// </summary>
    public static class LocalizationCacheDiagnostics
    {
        private static readonly HashSet<string> _preloadedTableNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly object _sync = new object();

        public static void MarkTablePreloaded(string tableName, Object context = null)
        {
            string normalized = Normalize(tableName);
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            bool added;
            lock (_sync)
            {
                added = _preloadedTableNames.Add(normalized);
            }

            if (added)
            {
                SBLog.Info($"[LocalizationCache] Preloaded table '{normalized}'.", context);
            }
        }

        public static void LogDisplay(LocalizedString localizedString, string displayPoint, Object context = null)
        {
            if (localizedString == null)
            {
                SBLog.Warn($"[LocalizationCache] Display '{displayPoint}': LocalizedString is null.", context);
                return;
            }

            string tableName = ResolveTableName(localizedString);
            string entryName = ResolveEntryName(localizedString);
            bool isPreloaded = IsTablePreloaded(tableName);

            if (isPreloaded)
            {
                SBLog.Info($"[LocalizationCache] Display '{displayPoint}': table='{tableName}', entry='{entryName}', source='cache(preloaded)'.", context);
            }
            else
            {
                SBLog.Warn($"[LocalizationCache] Display '{displayPoint}': table='{tableName}', entry='{entryName}', source='not-preloaded (possible lazy-load)'.", context);
            }
        }

        private static bool IsTablePreloaded(string tableName)
        {
            string normalized = Normalize(tableName);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            lock (_sync)
            {
                return _preloadedTableNames.Contains(normalized);
            }
        }

        private static string ResolveTableName(LocalizedString localizedString)
        {
            string byName = localizedString.TableReference.TableCollectionName;
            if (!string.IsNullOrWhiteSpace(byName))
            {
                return byName;
            }

            return localizedString.TableReference.ToString();
        }

        private static string ResolveEntryName(LocalizedString localizedString)
        {
            string byKey = localizedString.TableEntryReference.Key;
            if (!string.IsNullOrWhiteSpace(byKey))
            {
                return byKey;
            }

            long keyId = localizedString.TableEntryReference.KeyId;
            if (keyId != 0)
            {
                return keyId.ToString();
            }

            return "<none>";
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
