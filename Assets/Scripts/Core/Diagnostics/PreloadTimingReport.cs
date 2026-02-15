using System;
using System.Globalization;
using System.Text;
using SevenBattles.Core.Preload;

namespace SevenBattles.Core.Diagnostics
{
    public struct PreloadTimingReport
    {
        private const int DefaultBuilderCapacity = 256;

        [ThreadStatic]
        private static StringBuilder s_cachedBuilder;

        private readonly string[] _taskNames;

        public int TaskCount;
        public float TotalMs;
        public PreloadResult[] Results;

        public bool AllSucceeded
        {
            get
            {
                if (Results == null)
                {
                    return TaskCount <= 0;
                }

                int count = System.Math.Min(TaskCount, Results.Length);
                for (int i = 0; i < count; i++)
                {
                    if (!Results[i].Success)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public PreloadTimingReport(int taskCount, float totalMs, PreloadResult[] results, string[] taskNames)
        {
            TaskCount = taskCount;
            TotalMs = totalMs;
            Results = results;
            _taskNames = taskNames;
        }

        public string ToSummary()
        {
            var builder = AcquireBuilder();
            builder.Append("[Preload] Complete: ");
            builder.Append(TaskCount);
            builder.Append(" tasks, total ");
            builder.Append(TotalMs.ToString("F1", CultureInfo.InvariantCulture));
            builder.Append("ms");

            if (Results != null && TaskCount > 0)
            {
                int count = System.Math.Min(TaskCount, Results.Length);
                if (count > 0)
                {
                    builder.Append(" (");
                    for (int i = 0; i < count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        AppendTaskName(builder, i);
                        builder.Append(": ");
                        builder.Append(Results[i].DurationMs.ToString("F1", CultureInfo.InvariantCulture));
                        builder.Append("ms");
                    }
                    builder.Append(')');
                }
            }

            return builder.ToString();
        }

        private static StringBuilder AcquireBuilder()
        {
            if (s_cachedBuilder == null)
            {
                s_cachedBuilder = new StringBuilder(DefaultBuilderCapacity);
            }
            else
            {
                s_cachedBuilder.Clear();
            }

            return s_cachedBuilder;
        }

        private void AppendTaskName(StringBuilder builder, int index)
        {
            if (_taskNames != null && index >= 0 && index < _taskNames.Length && !string.IsNullOrWhiteSpace(_taskNames[index]))
            {
                builder.Append(_taskNames[index]);
                return;
            }

            builder.Append("Task");
            builder.Append(index + 1);
        }
    }
}
