using System;
using System.Collections.Generic;
using System.Linq;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace ProPresenter_StageDisplayLayout_AutoSwitcher
{
    // Bridges Serilog log events to the UI via a simple static event of formatted lines
    // and keeps an in-memory buffer so the UI can show all logs from the start.
    public static class SerilogUiBridge
    {
        private static readonly MessageTemplateTextFormatter Formatter = new(
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        private static readonly object Sync = new();
        private static readonly List<string> Lines = new();

        public static event Action<string>? LineEmitted;

        public static void Emit(LogEvent logEvent)
        {
            try
            {
                using var sw = new System.IO.StringWriter();
                Formatter.Format(logEvent, sw);
                var text = sw.ToString();

                lock (Sync)
                {
                    Lines.Add(text);
                }

                LineEmitted?.Invoke(text);
            }
            catch
            {
                // Ignore any formatting or callback errors
            }
        }

        // Returns a snapshot copy of all lines so far to avoid locking the UI thread for long.
        public static IReadOnlyList<string> GetAllLinesSnapshot()
        {
            lock (Sync)
            {
                return Lines.ToList();
            }
        }
    }
}
