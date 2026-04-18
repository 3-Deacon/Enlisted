using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Enlisted.Mod.Core.Logging
{
    /// <summary>
    /// Maintains in-memory counters for Surfaced, Caught, and Expected emissions
    /// and rewrites a summary block at the tail of the session log on each
    /// Surfaced emission and on game exit. Uses sentinel lines to strip any
    /// prior footer block before appending the new one, so the footer is
    /// idempotent across repeated rewrites.
    /// </summary>
    internal static class SessionSummaryFooter
    {
        private const string SentinelStart = "=== SUMMARY ===";
        private const string SentinelEnd   = "===============";

        private static readonly object Sync = new object();

        private static readonly Dictionary<string, SurfacedEntry> Surfaced
            = new Dictionary<string, SurfacedEntry>();
        private static readonly Dictionary<string, int> CaughtSites
            = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> ExpectedKeys
            = new Dictionary<string, int>();

        private struct SurfacedEntry
        {
            public string Category;
            public string Summary;
            public string File;
            public int Line;
            public int Count;
        }

        /// <summary>
        /// Path to the current session log, set by <c>ModLogger.PrepareSessionLogFile</c>
        /// once the new Session-A file is created. Until this is set (or if IO
        /// fails on the first rewrite attempt) the footer is a no-op.
        /// </summary>
        internal static string SessionLogPath { get; set; }
        private static bool _footerDisabled;

        /// <summary>
        /// Bumps the count for a Surfaced code and rewrites the footer block.
        /// First occurrence of a code captures its category/summary/file:line
        /// so subsequent recordings only increment the counter.
        /// </summary>
        public static void RecordSurfaced(string code, string category, string summary, string file, int line)
        {
            lock (Sync)
            {
                if (!Surfaced.TryGetValue(code, out var e))
                {
                    e = new SurfacedEntry { Category = category, Summary = summary, File = file, Line = line, Count = 0 };
                }
                e.Count++;
                Surfaced[code] = e;
            }
            RewriteFooter();
        }

        /// <summary>
        /// Records a Caught emission by (file, line) site. Does NOT rewrite the
        /// footer — Caught rarely warrants the IO hit since these do not toast.
        /// Counts are included the next time a Surfaced rewrites or on Flush().
        /// </summary>
        public static void RecordCaught(string category, string file, int line)
        {
            var key = $"{file}:{line}";
            lock (Sync)
            {
                CaughtSites.TryGetValue(key, out var n);
                CaughtSites[key] = n + 1;
            }
        }

        /// <summary>
        /// Records an Expected emission keyed by its stable <paramref name="key"/>.
        /// Does NOT rewrite the footer; flushed lazily.
        /// </summary>
        public static void RecordExpected(string category, string key)
        {
            lock (Sync)
            {
                ExpectedKeys.TryGetValue(key, out var n);
                ExpectedKeys[key] = n + 1;
            }
        }

        /// <summary>
        /// Force a footer rewrite. Called on clean game exit so the final
        /// counts are persisted even if no Surfaced happened in the final window.
        /// </summary>
        public static void Flush() { RewriteFooter(); }

        /// <summary>
        /// Reset all in-memory counters and re-enable footer writing for a new session.
        /// Called from <c>ModLogger.Initialize()</c> so save-reload within the same
        /// process starts with a clean slate.
        /// </summary>
        public static void Reset()
        {
            lock (Sync)
            {
                Surfaced.Clear();
                CaughtSites.Clear();
                ExpectedKeys.Clear();
                _footerDisabled = false;
            }
        }

        private static void RewriteFooter()
        {
            if (_footerDisabled) return;
            var path = SessionLogPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                lock (Sync)
                {
                    var block = BuildFooterBlock();
                    var all = File.ReadAllText(path);
                    var idx = all.LastIndexOf(SentinelStart, StringComparison.Ordinal);
                    if (idx >= 0)
                    {
                        all = all.Substring(0, idx).TrimEnd() + Environment.NewLine;
                    }
                    else if (!all.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                    {
                        all += Environment.NewLine;
                    }
                    File.WriteAllText(path, all + block);
                }
            }
            catch { _footerDisabled = true; }
        }

        private static string BuildFooterBlock()
        {
            var sb = new StringBuilder();
            sb.AppendLine(SentinelStart);
            sb.AppendLine($"Surfaced errors: {Surfaced.Count}");
            foreach (var kvp in Surfaced)
            {
                var e = kvp.Value;
                sb.AppendLine($"  [{kvp.Key}] {e.Summary} - {e.File}:{e.Line} (x{e.Count})");
            }
            int caughtTotal = 0;
            foreach (var n in CaughtSites.Values) caughtTotal += n;
            sb.AppendLine($"Caught (non-surfaced): {caughtTotal} across {CaughtSites.Count} sites");
            int expectedTotal = 0;
            foreach (var n in ExpectedKeys.Values) expectedTotal += n;
            sb.AppendLine($"Expected (guard-rail): {expectedTotal} across {ExpectedKeys.Count} keys");
            sb.AppendLine(SentinelEnd);
            return sb.ToString();
        }
    }
}
