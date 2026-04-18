using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Enlisted.Mod.Core.Logging
{
	/// <summary>
	/// Log levels for controlling verbosity per category.
	/// Higher values include all lower levels (e.g., Info includes Warn and Error).
	/// </summary>
	public enum LogLevel
	{
		Off = 0,    // No logging
		Error = 1,  // Only errors
		Warn = 2,   // Errors and warnings
		Info = 3,   // Normal operational messages
		Debug = 4,  // Detailed debugging info
		Trace = 5   // Very verbose, for deep debugging
	}

	/// <summary>
	/// File logger for the mod with category-based log levels, throttling, anti-spam features,
	/// and a three-session rotation that keeps the latest sessions easy to identify.
	/// Writes session logs to Modules/Enlisted/Debugging/ as Session-A/B/C with timestamps.
	///
	/// Features:
	/// - Per-category log levels (configure in settings.json)
	/// - Message throttling to prevent spam from repeated messages
	/// - LogOnce() for one-time messages per session
	/// - LogSummary() for periodic summary reports
	/// - Session log rotation (up to three: A newest, C oldest)
	/// </summary>
	public static class ModLogger
	{
 	private static readonly object Sync = new object();
 	private static string _logFilePath; // Will be set in Initialize()
 	private static string _sessionId = "Session-A";
 	private const string SessionPrefix = "Session-";
 	private static readonly string[] SessionSlots = { "Session-A", "Session-B", "Session-C" };
		private const string CombinedPointerFile = "Current_Session_README.txt";

		// Per-category log levels (default to Info)
		private static Dictionary<string, LogLevel> _categoryLevels = new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase);
		private static LogLevel _defaultLevel = LogLevel.Info;

		// Throttling system - tracks recent messages to prevent spam
		private static readonly Dictionary<string, ThrottleEntry> ThrottleCache = new Dictionary<string, ThrottleEntry>();
		private static int _throttleSeconds = 5;

		// LogOnce tracking - messages that should only appear once per session
		private static readonly HashSet<string> LoggedOnceKeys = new HashSet<string>();

		// Summary tracking - accumulates counts for periodic summaries
		private static readonly Dictionary<string, SummaryEntry> SummaryData = new Dictionary<string, SummaryEntry>();

		// Exception detail de-duplication: only write each unique exception detail once per session.
		// This gives end-user logs real stack traces without requiring Debug log level, but avoids spam.
		private static readonly HashSet<string> LoggedExceptionDetails = new HashSet<string>();

		/// <summary>
		/// Tracks repeated messages for throttling.
		/// </summary>
		private class ThrottleEntry
		{
			public DateTime LastLogTime { get; set; }
			public int SuppressedCount { get; set; }
			public string Level { get; set; }
			public string Category { get; set; }
			public string LastMessage { get; set; }
		}

		/// <summary>
		/// Tracks data for summary logging.
		/// </summary>
		private class SummaryEntry
		{
			public int Count { get; set; }
			public int TotalValue { get; set; }
			public DateTime LastUpdate { get; set; }
		}

		/// <summary>
		/// Initialize the logger and configure log levels from settings.
		/// </summary>
		public static void Initialize(string customPath = null)
		{
			try
			{
				lock (Sync)
				{
					_logFilePath = string.IsNullOrWhiteSpace(customPath) ? ResolveDefaultLogPath() : customPath;
					_logFilePath = PrepareSessionLogFile(_logFilePath);
					_sessionId = Path.GetFileNameWithoutExtension(_logFilePath) ?? "Session-A";

					// Clear session-specific tracking
					LoggedOnceKeys.Clear();
					ThrottleCache.Clear();
					SummaryData.Clear();

					// Write initialization message - this will test if logging works
					WriteInternal("INFO", "Init", $"Logger initialized (session: {_sessionId}, path: {_logFilePath})");
				}
			}
			catch (Exception ex)
			{
				// Log to debug output so we can see what went wrong during initialization
				System.Diagnostics.Debug.WriteLine($"[Enlisted] Logger initialization failed: {ex.Message}\n{ex.StackTrace}");
				// Try to set a fallback path with session rotation
				try
				{
					_logFilePath = PrepareSessionLogFile(ResolveDocumentsPath());
					_sessionId = Path.GetFileNameWithoutExtension(_logFilePath) ?? "Session-A";
				}
				catch
				{
					// If even fallback fails, we'll use Debug output only
				}
			}
		}

		/// <summary>
		/// Configure log levels per category. Call after Initialize() and after loading settings.
		/// </summary>
		/// <param name="categoryLevels">Dictionary mapping category names to log levels</param>
		/// <param name="defaultLevel">Default level for categories not explicitly configured</param>
		/// <param name="throttleSeconds">Seconds to suppress repeated identical messages (0 to disable)</param>
		public static void ConfigureLevels(Dictionary<string, LogLevel> categoryLevels, LogLevel defaultLevel = LogLevel.Info, int throttleSeconds = 5)
		{
			lock (Sync)
			{
				_categoryLevels = categoryLevels ?? new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase);
				_defaultLevel = defaultLevel;
				_throttleSeconds = Math.Max(0, throttleSeconds);

				WriteInternal("INFO", "Init", $"Log levels configured: default={defaultLevel}, throttle={_throttleSeconds}s, categories={_categoryLevels.Count}");
			}
		}

		/// <summary>
		/// Get the effective log level for a category.
		/// </summary>
		public static LogLevel GetCategoryLevel(string category)
		{
			if (string.IsNullOrEmpty(category))
			{
				return _defaultLevel;
			}

			lock (Sync)
			{
				if (_categoryLevels.TryGetValue(category, out var level))
				{
					return level;
				}
				return _defaultLevel;
			}
		}

		/// <summary>
		/// Check if a message at the given level would be logged for the category.
		/// </summary>
		public static bool IsEnabled(string category, LogLevel level)
		{
			return level <= GetCategoryLevel(category);
		}

		#region Standard Logging Methods

		/// <summary>
		/// Log a trace-level message (most verbose).
		/// </summary>
		public static void Trace(string category, string message)
		{
			if (IsEnabled(category, LogLevel.Trace))
			{
				LogWithThrottle("TRACE", category, message);
			}
		}

		/// <summary>
		/// Log a debug-level message.
		/// </summary>
		public static void Debug(string category, string message)
		{
			if (IsEnabled(category, LogLevel.Debug))
			{
				LogWithThrottle("DEBUG", category, message);
			}
		}

		/// <summary>
		/// Log an info-level message.
		/// </summary>
		public static void Info(string category, string message)
		{
			if (IsEnabled(category, LogLevel.Info))
			{
				LogWithThrottle("INFO", category, message);
			}
		}

		/// <summary>
		/// Log a warning-level message.
		/// </summary>
		public static void Warn(string category, string message)
		{
			if (IsEnabled(category, LogLevel.Warn))
			{
				LogWithThrottle("WARN", category, message);
			}
		}

		/// <summary>
		/// Log an error-level message with optional exception details.
		/// Errors are never throttled.
		/// </summary>
		public static void Error(string category, string message, Exception ex = null)
		{
			if (!IsEnabled(category, LogLevel.Error))
			{
				return;
			}

			var text = ex == null ? message : $"{message} | Exception: {ex.GetType().Name}: {ex.Message}";
			WriteInternal("ERROR", category, text);

			// End-user diagnostics: include full exception details once per unique exception.
			// (ex.ToString() includes stack trace and inner exceptions.)
			if (ex != null)
			{
				var detail = ex.ToString();
				if (!string.IsNullOrWhiteSpace(detail))
				{
					lock (Sync)
					{
						if (LoggedExceptionDetails.Add(detail))
						{
							WriteInternal("ERROR", category, "Exception detail (first occurrence this session):");
							WriteInternal("ERROR", category, detail);
						}
					}
				}
			}
		}

		/// <summary>
		/// When true, coded errors and warnings also display an on-screen message so
		/// developers and players see the failure without tailing the log file.
		/// Toggle off for silent release builds via ModLogger.ShowCodedMessagesOnScreen.
		/// </summary>
		public static bool ShowCodedMessagesOnScreen { get; set; } = true;

		/// <summary>
		/// Log an error with a stable support code (searchable in user logs).
		/// Also surfaces the code + message on-screen in red when
		/// <see cref="ShowCodedMessagesOnScreen"/> is true.
		/// </summary>
		public static void ErrorCode(string category, string code, string message, Exception ex = null)
		{
			var prefix = string.IsNullOrWhiteSpace(code) ? string.Empty : $"[{code}] ";
			Error(category, $"{prefix}{message}", ex);
			DisplayCodedMessageOnScreen(code, category, message, Colors.Red);
		}

		/// <summary>
		/// Log a warning with a stable support code (searchable in user logs).
		/// Also surfaces the code + message on-screen in yellow when
		/// <see cref="ShowCodedMessagesOnScreen"/> is true.
		/// </summary>
		public static void WarnCode(string category, string code, string message)
		{
			var prefix = string.IsNullOrWhiteSpace(code) ? string.Empty : $"[{code}] ";
			Warn(category, $"{prefix}{message}");
			DisplayCodedMessageOnScreen(code, category, message, Colors.Yellow);
		}

		/// <summary>
		/// Push a coded log entry to the on-screen info panel. Swallows any display
		/// failure so logging never breaks because of UI state.
		/// </summary>
		private static void DisplayCodedMessageOnScreen(string code, string category, string message, Color color)
		{
			if (!ShowCodedMessagesOnScreen)
			{
				return;
			}
			try
			{
				const int MaxMessageChars = 140;
				var body = message ?? string.Empty;
				if (body.Length > MaxMessageChars)
				{
					body = body.Substring(0, MaxMessageChars - 3) + "...";
				}
				var line = string.IsNullOrWhiteSpace(code)
					? $"[{category}] {body}"
					: $"[{code}] {body}";
				InformationManager.DisplayMessage(new InformationMessage(line, color));
			}
			catch
			{
				// Never let a display failure break error logging — file log already captured it.
			}
		}

		#endregion

		#region Special Logging Methods

		/// <summary>
		/// Log a message only once per session. Useful for startup messages or first-time events.
		/// </summary>
		/// <param name="key">Unique key to identify this message (e.g., "battle_system_init")</param>
		/// <param name="category">Log category</param>
		/// <param name="message">Message to log</param>
		/// <param name="level">Log level (default Info)</param>
		public static void LogOnce(string key, string category, string message, LogLevel level = LogLevel.Info)
		{
			if (!IsEnabled(category, level))
			{
				return;
			}

			lock (Sync)
			{
				if (!LoggedOnceKeys.Add(key))
				{
					return;
				}
			}

			var levelStr = level.ToString().ToUpper();
			WriteInternal(levelStr, category, message);
		}

		/// <summary>
		/// Log a coded warning only once per session.
		/// Useful for DLC missing / reflection guardrails where repetition would spam logs.
		/// </summary>
		public static void WarnCodeOnce(string key, string category, string code, string message)
		{
			if (!IsEnabled(category, LogLevel.Warn))
			{
				return;
			}

			bool shouldLog;
			lock (Sync)
			{
				shouldLog = LoggedOnceKeys.Add(key);
			}

			if (!shouldLog)
			{
				return;
			}

			WarnCode(category, code, message);
		}

		/// <summary>
		/// Log a coded error (with full exception details) only once per session.
		/// Intended for high-signal failures that can recur frequently (e.g., UI fallback exceptions).
		/// </summary>
		public static void ErrorCodeOnce(string key, string category, string code, string message, Exception ex = null)
		{
			if (!IsEnabled(category, LogLevel.Error))
			{
				return;
			}

			bool shouldLog;
			lock (Sync)
			{
				shouldLog = LoggedOnceKeys.Add(key);
			}

			if (!shouldLog)
			{
				return;
			}

			ErrorCode(category, code, message, ex);
		}

		/// <summary>
		/// Increment a summary counter. Use with FlushSummary() to log aggregated data.
		/// Useful for tracking things like "X battles participated" or "Y gold earned".
		/// </summary>
		/// <param name="key">Unique key for this summary (e.g., "battles_won")</param>
		/// <param name="incrementCount">Amount to add to count (default 1)</param>
		/// <param name="incrementValue">Optional value to accumulate (e.g., gold amount)</param>
		public static void IncrementSummary(string key, int incrementCount = 1, int incrementValue = 0)
		{
			lock (Sync)
			{
				if (!SummaryData.TryGetValue(key, out var entry))
				{
					entry = new SummaryEntry();
					SummaryData[key] = entry;
				}
				entry.Count += incrementCount;
				entry.TotalValue += incrementValue;
				entry.LastUpdate = DateTime.Now;
			}
		}

		/// <summary>
		/// Log and reset a summary counter.
		/// </summary>
		/// <param name="key">Summary key to flush</param>
		/// <param name="category">Log category</param>
		/// <param name="messageFormat">Message format string with {0} for count and {1} for total value</param>
		/// <param name="level">Log level (default Info)</param>
		public static void FlushSummary(string key, string category, string messageFormat, LogLevel level = LogLevel.Info)
		{
			if (!IsEnabled(category, level))
			{
				return;
			}

			SummaryEntry entry;
			lock (Sync)
			{
				if (!SummaryData.TryGetValue(key, out entry) || entry.Count == 0)
				{
					return;
				}

				SummaryData.Remove(key);
			}

			var message = string.Format(messageFormat, entry.Count, entry.TotalValue);
			var levelStr = level.ToString().ToUpper();
			WriteInternal(levelStr, category, message);
		}

		/// <summary>
		/// Log a state transition. Useful for tracking game state changes.
		/// </summary>
		public static void StateChange(string category, string fromState, string toState, string details = null)
		{
			if (!IsEnabled(category, LogLevel.Info))
			{
				return;
			}

			var message = $"State: [{fromState}] -> [{toState}]";
			if (!string.IsNullOrEmpty(details))
			{
				message += $" | {details}";
			}

			LogWithThrottle("INFO", category, message);
		}

		/// <summary>
		/// Log an action result. Useful for tracking success/failure of operations.
		/// </summary>
		public static void ActionResult(string category, string action, bool success, string details = null)
		{
			var level = success ? LogLevel.Info : LogLevel.Warn;
			if (!IsEnabled(category, level))
			{
				return;
			}

			var status = success ? "OK" : "FAILED";
			var message = $"{action}: {status}";
			if (!string.IsNullOrEmpty(details))
			{
				message += $" | {details}";
			}

			var levelStr = level.ToString().ToUpper();
			LogWithThrottle(levelStr, category, message);
		}

		/// <summary>
		/// Keyed throttling: throttle by a stable key rather than the full message.
		/// Useful for "same event, changing details" without spamming.
		/// </summary>
		public static void LogKeyedThrottled(string level, string category, string key, string message)
		{
			if (string.Equals(level, "TRACE", StringComparison.OrdinalIgnoreCase) && !IsEnabled(category, LogLevel.Trace))
			{
				return;
			}
			if (string.Equals(level, "DEBUG", StringComparison.OrdinalIgnoreCase) && !IsEnabled(category, LogLevel.Debug))
			{
				return;
			}
			if (string.Equals(level, "INFO", StringComparison.OrdinalIgnoreCase) && !IsEnabled(category, LogLevel.Info))
			{
				return;
			}
			if (string.Equals(level, "WARN", StringComparison.OrdinalIgnoreCase) && !IsEnabled(category, LogLevel.Warn))
			{
				return;
			}
			if (string.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase) && !IsEnabled(category, LogLevel.Error))
			{
				return;
			}

			LogWithKeyedThrottle(level, category, key, message);
		}

		/// <summary>
		/// Convenience for INFO-level keyed throttling.
		/// </summary>
		public static void InfoThrottled(string category, string key, string message)
		{
			LogKeyedThrottled("INFO", category, key, message);
		}

		#endregion

		#region Internal Methods

		/// <summary>
		/// Log with throttling to prevent spam from repeated identical messages.
		/// </summary>
		private static void LogWithThrottle(string level, string category, string message)
		{
			if (_throttleSeconds <= 0)
			{
				WriteInternal(level, category, message);
				return;
			}

			var key = $"{category}|{message}";
			var now = DateTime.Now;

			lock (Sync)
			{
				if (ThrottleCache.TryGetValue(key, out var entry))
				{
					var elapsed = (now - entry.LastLogTime).TotalSeconds;
					if (elapsed < _throttleSeconds)
					{
						// Still within throttle window - suppress and count
						entry.SuppressedCount++;
						return;
					}

					// Throttle window expired - log with suppression count if any
					if (entry.SuppressedCount > 0)
					{
						WriteInternal(level, category, $"{message} (repeated {entry.SuppressedCount}x)");
					}
					else
					{
						WriteInternal(level, category, message);
					}

					entry.LastLogTime = now;
					entry.SuppressedCount = 0;
				}
				else
				{
					// First time seeing this message
					ThrottleCache[key] = new ThrottleEntry
					{
						LastLogTime = now,
						SuppressedCount = 0,
						Level = level,
						Category = category
					};
					WriteInternal(level, category, message);
				}
			}

			// Periodically clean old entries to prevent memory growth
			CleanThrottleCache();
		}

		private static void LogWithKeyedThrottle(string level, string category, string key, string message)
		{
			if (_throttleSeconds <= 0)
			{
				WriteInternal(level, category, message);
				return;
			}

			var safeKey = string.IsNullOrWhiteSpace(key) ? message : key;
			var cacheKey = $"{level}|{category}|{safeKey}";
			var now = DateTime.Now;

			lock (Sync)
			{
				if (ThrottleCache.TryGetValue(cacheKey, out var entry))
				{
					var elapsed = (now - entry.LastLogTime).TotalSeconds;
					if (elapsed < _throttleSeconds)
					{
						entry.SuppressedCount++;
						entry.LastMessage = message;
						return;
					}

					var toWrite = entry.LastMessage ?? message;
					if (entry.SuppressedCount > 0)
					{
						WriteInternal(level, category, $"{toWrite} (repeated {entry.SuppressedCount}x)");
					}
					else
					{
						WriteInternal(level, category, toWrite);
					}

					entry.LastLogTime = now;
					entry.SuppressedCount = 0;
					entry.LastMessage = message;
				}
				else
				{
					ThrottleCache[cacheKey] = new ThrottleEntry
					{
						LastLogTime = now,
						SuppressedCount = 0,
						Level = level,
						Category = category,
						LastMessage = message
					};
					WriteInternal(level, category, message);
				}
			}

			CleanThrottleCache();
		}

		/// <summary>
		/// Remove old throttle cache entries to prevent unbounded memory growth.
		/// </summary>
		private static void CleanThrottleCache()
		{
			// Only clean occasionally to avoid overhead
			if (ThrottleCache.Count < 100)
			{
				return;
			}

			var now = DateTime.Now;
			var expiredKeys = new List<string>();

			lock (Sync)
			{
				foreach (var kvp in ThrottleCache)
				{
					if ((now - kvp.Value.LastLogTime).TotalSeconds > _throttleSeconds * 10)
					{
						expiredKeys.Add(kvp.Key);
					}
				}

				foreach (var key in expiredKeys)
				{
					ThrottleCache.Remove(key);
				}
			}
		}

		private static void WriteInternal(string level, string category, string message)
		{
			// Ensure path is set (in case Initialize wasn't called)
			if (string.IsNullOrWhiteSpace(_logFilePath))
			{
				_logFilePath = ResolveDefaultLogPath();
				if (string.IsNullOrWhiteSpace(_logFilePath))
				{
					_logFilePath = ResolveDocumentsPath();
				}
			}

			try
			{
				lock (Sync)
				{
					var logDir = Path.GetDirectoryName(_logFilePath);
					EnsureDirectoryExists(logDir);
					var line = FormatLine(level, category, message);
					File.AppendAllText(_logFilePath, line, Encoding.UTF8);
				}
			}
			catch (Exception ex)
			{
				// Fallback to Documents path
				try
				{
					var fallback = ResolveDocumentsPath();
					var logDir = Path.GetDirectoryName(fallback);
					EnsureDirectoryExists(logDir);
					var line = FormatLine(level, category, message);
					File.AppendAllText(fallback, line, Encoding.UTF8);

					// If we had to use fallback, update the main path for future writes
					if (_logFilePath != fallback)
					{
						System.Diagnostics.Debug.WriteLine($"[Enlisted] Primary log path failed, using fallback: {fallback} (Error: {ex.Message})");
						_logFilePath = fallback;
					}
				}
				catch (Exception fallbackEx)
				{
					// Last resort: Debug output
					System.Diagnostics.Debug.WriteLine($"[Enlisted][{level}][{category}] {message}");
					System.Diagnostics.Debug.WriteLine($"[Enlisted] Both primary and fallback log paths failed. Primary: {ex.Message}, Fallback: {fallbackEx.Message}");
				}
			}
		}

		private static string FormatLine(string level, string category, string message)
		{
			// Include full date to make multi-session logs easier to correlate.
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			// User-friendly format: [time] [LEVEL] [Category] Message
			return $"[{timestamp}] [{level,-5}] [{category}] {message}\r\n";
		}

		#endregion

		#region Path Resolution

		private static string ResolveDefaultLogPath()
		{
			try
			{
				var assembly = typeof(ModLogger).Assembly;
				var assemblyLocation = assembly.Location;

				// Assembly.Location can be null or empty in some scenarios (e.g., dynamic loading, single-file apps)
				if (string.IsNullOrWhiteSpace(assemblyLocation))
				{
					// Fallback: use Documents folder if we can't determine assembly location
					System.Diagnostics.Debug.WriteLine("[Enlisted] Assembly.Location is null/empty, using Documents fallback");
					return ResolveDocumentsPath();
				}

				var dllDir = Path.GetDirectoryName(assemblyLocation);
				if (string.IsNullOrWhiteSpace(dllDir))
				{
					System.Diagnostics.Debug.WriteLine("[Enlisted] Could not get directory from assembly location, using Documents fallback");
					return ResolveDocumentsPath();
				}

				// Navigate from bin/Win64_Shipping_Client/Enlisted.dll up to Modules/Enlisted/Debugging/
				var binDir = Directory.GetParent(dllDir);
				if (binDir == null)
				{
					System.Diagnostics.Debug.WriteLine("[Enlisted] Could not get parent directory, using Documents fallback");
					return ResolveDocumentsPath();
				}

				var enlistedRoot = binDir.Parent;
				if (enlistedRoot == null)
				{
					System.Diagnostics.Debug.WriteLine("[Enlisted] Could not get module root directory, using Documents fallback");
					return ResolveDocumentsPath();
				}

				var dir = Path.Combine(enlistedRoot.FullName, "Debugging");

				// Return a path template - PrepareSessionLogFile will extract the directory
				// and create the actual timestamped Session-A file. The "_.log" is just a placeholder.
				return Path.Combine(dir, "_.log");
			}
			catch (Exception ex)
			{
				// Log to debug output so we can see what went wrong
				System.Diagnostics.Debug.WriteLine($"[Enlisted] Failed to resolve log path: {ex.Message}\n{ex.StackTrace}");
				return ResolveDocumentsPath();
			}
		}

		private static string ResolveDocumentsPath(string fileName = null)
		{
			var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			var dir = Path.Combine(docs, "Mount and Blade II Bannerlord", "Logs", "Enlisted");
			// Use a placeholder filename if none provided - PrepareSessionLogFile extracts the directory anyway
			return Path.Combine(dir, fileName ?? "_.log");
		}

		private static void EnsureDirectoryExists(string directory)
		{
			if (string.IsNullOrWhiteSpace(directory))
			{
				return;
			}
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
		}

		private static string PrepareSessionLogFile(string preferredPath)
		{
			try
			{
 			var basePath = string.IsNullOrWhiteSpace(preferredPath) ? ResolveDocumentsPath() : preferredPath;
 			var logDir = Path.GetDirectoryName(basePath);
 			if (string.IsNullOrWhiteSpace(logDir))
 			{
 				logDir = Path.GetDirectoryName(ResolveDocumentsPath());
 			}

 			logDir ??= "Debugging";
 			EnsureDirectoryExists(logDir);
 			var utcNow = DateTime.UtcNow;

		// Collect existing session logs
		var sessionFiles = Directory.GetFiles(logDir, $"{SessionPrefix}*.log", SearchOption.TopDirectoryOnly)
				.Select(path => new FileInfo(path))
				.OrderByDescending(f => f.CreationTimeUtc)
				.ToList();

			// Keep at most the two newest existing sessions (B and C after shifting)
				var toShift = sessionFiles.Take(SessionSlots.Length - 1).ToList();
				var toDelete = sessionFiles.Skip(SessionSlots.Length - 1).ToList();
				foreach (var old in toDelete)
				{
					TryDeleteFile(old.FullName);
				}

				// Rename existing sessions to maintain slot ordering (newest -> B, next -> C)
				for (var i = 0; i < toShift.Count && i + 1 < SessionSlots.Length; i++)
				{
					var stamp = ExtractTimestamp(toShift[i]) ?? toShift[i].CreationTimeUtc;
					var targetName = $"{SessionSlots[i + 1]}_{stamp:yyyy-MM-dd_HH-mm-ss}.log";
					var targetPath = Path.Combine(logDir, targetName);
					TryMoveFile(toShift[i].FullName, targetPath);
				}

				// Create the new session log as Session-A
				var newLogName = $"{SessionSlots[0]}_{utcNow:yyyy-MM-dd_HH-mm-ss}.log";
				var newLogPath = Path.Combine(logDir, newLogName);
				WriteSessionHeader(newLogPath, utcNow);
				WriteCombinedPointer(logDir, newLogName, null);
				return newLogPath;
			}
			catch
			{
				// Last resort: return the preferred path to let the caller fallback further
				return string.IsNullOrWhiteSpace(preferredPath) ? ResolveDocumentsPath() : preferredPath;
			}
		}

		private static DateTime? ExtractTimestamp(FileInfo file)
		{
			try
			{
				var name = Path.GetFileNameWithoutExtension(file.Name);
				if (string.IsNullOrWhiteSpace(name))
				{
					return null;
				}

				var parts = name.Split('_');
				if (parts.Length == 0)
				{
					return null;
				}

				var tail = parts[parts.Length - 1];
				if (DateTime.TryParseExact(tail, "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture,
						DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
				{
					return parsed;
				}
			}
			catch
			{
				// ignore parse errors
			}
			return null;
		}

		private static void WriteSessionHeader(string path, DateTime utcNow)
		{
			try
			{
				var local = utcNow.ToLocalTime();
				var header = new StringBuilder();
				header.AppendLine("=== ENLISTED LOG SESSION START ===");
				header.AppendLine($"Session File: {Path.GetFileName(path)}");
				header.AppendLine($"Started (UTC): {utcNow:yyyy-MM-dd HH:mm:ss}");
				header.AppendLine($"Started (Local): {local:yyyy-MM-dd HH:mm:ss}");
				header.AppendLine("Build: Enlisted RETAIL");
				header.AppendLine(new string('-', 60));
				File.WriteAllText(path, header.ToString(), Encoding.UTF8);
			}
			catch
			{
				// best-effort header
			}
		}

		internal static void WriteCombinedPointer(string directory, string sessionFile, string conflictsFile)
		{
			try
			{
				EnsureDirectoryExists(directory);

				// If values are missing, infer the latest by timestamp (not alphabetical)
				if (string.IsNullOrWhiteSpace(sessionFile))
				{
					sessionFile = Directory.GetFiles(directory, $"{SessionPrefix}*.log", SearchOption.TopDirectoryOnly)
						.Select(path => new FileInfo(path))
						.Select(fi => new { File = fi, Stamp = ExtractTimestamp(fi) ?? fi.CreationTimeUtc })
						.OrderByDescending(x => x.Stamp)
						.Select(x => x.File.Name)
						.FirstOrDefault();
				}

				if (string.IsNullOrWhiteSpace(conflictsFile))
				{
					conflictsFile = Directory.GetFiles(directory, "Conflicts-*.log", SearchOption.TopDirectoryOnly)
						.Select(path => new FileInfo(path))
						.Select(fi => new { File = fi, Stamp = ExtractTimestamp(fi) ?? fi.CreationTimeUtc })
						.OrderByDescending(x => x.Stamp)
						.Select(x => x.File.Name)
						.FirstOrDefault();
				}

				var pointerPath = Path.Combine(directory, CombinedPointerFile);
				var sb = new StringBuilder();
				sb.AppendLine("Log pointer (sessions + conflicts)");
				sb.AppendLine();
				sb.AppendLine("Session logs");
				sb.AppendLine("- Session-A: current/latest session");
				sb.AppendLine("- Session-B: previous session");
				sb.AppendLine("- Session-C: third-most-recent session");
				if (!string.IsNullOrWhiteSpace(sessionFile))
				{
					sb.AppendLine($"Current file: {sessionFile}");
				}
				sb.AppendLine();
				sb.AppendLine("Conflicts");
				sb.AppendLine("- Conflicts-A: current/latest");
				sb.AppendLine("- Conflicts-B: previous");
				sb.AppendLine("- Conflicts-C: third-most-recent");
				if (!string.IsNullOrWhiteSpace(conflictsFile))
				{
					sb.AppendLine($"Current file: {conflictsFile}");
				}
				sb.AppendLine();
				sb.AppendLine("Send both the session and conflicts logs (choose one path):");
				sb.AppendLine("- Option 1: Upload both logs to pastebin.com and post the links on the BUG forums: https://www.nexusmods.com/mountandblade2bannerlord/mods/9193?tab=posts");
				sb.AppendLine("- Option 2: Retrieve the support email from the tagged posts on that page and email both logs with a brief crash description.");

				File.WriteAllText(pointerPath, sb.ToString(), Encoding.UTF8);
			}
			catch
			{
				// best effort; ignore pointer failures
			}
		}

		private static void TryDeleteFile(string path)
		{
			try { File.Delete(path); } catch { /* best effort */ }
		}

		private static void TryMoveFile(string source, string destination)
		{
			try
			{
				if (File.Exists(destination))
				{
					File.Delete(destination);
				}
				File.Move(source, destination);
			}
			catch
			{
				// best effort; if move fails, the old file remains
			}
		}

		#endregion
	}
}
