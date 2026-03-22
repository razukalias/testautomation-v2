using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Test_Automation.Services
{
    /// <summary>
    /// Log levels for filtering - Error/Warning/Info/Verbose
    /// </summary>
    public enum LogLevel
    {
        Error = 0,
        Warning = 1,
        Info = 2,
        Verbose = 3
    }

    /// <summary>
    /// Single log entry with automatic metadata capture
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;

        // Automatic metadata from caller
        public string ClassName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }

        // Optional data
        public string? Input { get; set; }
        public string? Output { get; set; }
        public string? Exception { get; set; }

        // Component context for filtering
        public string? ComponentId { get; set; }
        public string? ComponentName { get; set; }
        public string? ExecutionId { get; set; }

        public override string ToString()
        {
            var inputStr = !string.IsNullOrEmpty(Input) ? $"\n  INPUT: {Input}" : "";
            var outputStr = !string.IsNullOrEmpty(Output) ? $"\n  OUTPUT: {Output}" : "";
            var exStr = !string.IsNullOrEmpty(Exception) ? $"\n  EXCEPTION: {Exception}" : "";
            return $"[{Timestamp:HH:mm:ss.fff}] [{Level}] {ClassName}.{MethodName}: {Message}{inputStr}{outputStr}{exStr}";
        }
    }

    /// <summary>
    /// Centralized logging system - single Log() function replaces Debug.WriteLine, TraceLog, TraceFunction
    /// </summary>
    public static class Logger
    {
        private static readonly ConcurrentQueue<LogEntry> _logHistory = new();
        private static LogLevel _minimumLevel = LogLevel.Info;
        
        // For real-time UI updates
        public static event Action<LogEntry>? OnLogAdded;

        /// <summary>
        /// Main entry point - single Log function used everywhere
        /// </summary>
        public static void Log(
            string message,
            LogLevel level = LogLevel.Info,
            string? input = null,
            object? output = null,
            Exception? exception = null,
            string? componentId = null,
            string? componentName = null,
            string? executionId = null,
            [CallerMemberName] string? methodName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            // Filter by level
            if (level > _minimumLevel) return;

            var entry = new LogEntry
            {
                Level = level,
                Message = message,
                ClassName = ExtractClassName(filePath),
                MethodName = methodName ?? "Unknown",
                FilePath = filePath ?? "",
                LineNumber = lineNumber,
                Input = input != null ? Truncate(input, 500) : null,
                Output = output != null ? Truncate(output.ToString()!, 500) : null,
                Exception = exception?.ToString(),
                ComponentId = componentId,
                ComponentName = componentName,
                ExecutionId = executionId
            };

            _logHistory.Enqueue(entry);
            OnLogAdded?.Invoke(entry);

            // Also write to debug output for development
            System.Diagnostics.Debug.WriteLine(
                $"[{entry.Timestamp:HH:mm:ss.fff}] [{level}] {entry.ClassName}.{entry.MethodName}: {message}");
        }

        #region Convenience Methods

        public static void LogError(
            string message,
            Exception? exception = null,
            string? componentId = null,
            [CallerMemberName] string? method = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            Log(message, LogLevel.Error, exception: exception, componentId: componentId,
                methodName: method, filePath: file, lineNumber: line);
        }

        public static void LogWarning(
            string message,
            string? componentId = null,
            [CallerMemberName] string? method = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            Log(message, LogLevel.Warning, componentId: componentId,
                methodName: method, filePath: file, lineNumber: line);
        }

        public static void LogInfo(
            string message,
            string? input = null,
            string? componentId = null,
            [CallerMemberName] string? method = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            Log(message, LogLevel.Info, input: input, componentId: componentId,
                methodName: method, filePath: file, lineNumber: line);
        }

        public static void LogVerbose(
            string message,
            string? input = null,
            object? output = null,
            string? componentId = null,
            [CallerMemberName] string? method = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            Log(message, LogLevel.Verbose, input: input, output: output, componentId: componentId,
                methodName: method, filePath: file, lineNumber: line);
        }

        /// <summary>
        /// Log method start - for verbose tracing
        /// </summary>
        public static void LogMethodStart(
            object? input = null,
            string? componentId = null,
            string? componentName = null,
            string? executionId = null,
            [CallerMemberName] string? method = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            var inputStr = input != null ? Truncate(input.ToString()!, 500) : null;
            Log($"▶ START {method}", LogLevel.Verbose, 
                input: inputStr, 
                componentId: componentId, 
                componentName: componentName,
                executionId: executionId,
                methodName: method, filePath: file, lineNumber: line);
        }

        /// <summary>
        /// Log method end - for verbose tracing
        /// </summary>
        public static void LogMethodEnd(
            object? output = null,
            string? componentId = null,
            string? componentName = null,
            string? executionId = null,
            [CallerMemberName] string? method = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            var outputStr = output != null ? Truncate(output.ToString()!, 500) : null;
            Log($"◀ END {method}", LogLevel.Verbose,
                output: outputStr,
                componentId: componentId,
                componentName: componentName,
                executionId: executionId,
                methodName: method, filePath: file, lineNumber: line);
        }

        #endregion

        #region ExecuteWithLogging Wrappers (Automatic A-Z Verbose)

        /// <summary>
        /// Wrapper for sync methods - automatically logs start/end with input/output
        /// </summary>
        public static T ExecuteWithLogging<T>(
            Func<T> action,
            string? input = null,
            string? componentId = null,
            string? componentName = null,
            string? executionId = null,
            [CallerMemberName] string? method = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            LogMethodStart(input, componentId, componentName, executionId, method, file, line);
            try
            {
                var result = action();
                LogMethodEnd(result, componentId, componentName, executionId, method, file, line);
                return result;
            }
            catch (Exception ex)
            {
                LogError($"Error in {method}: {ex.Message}", ex, componentId, method, file, line);
                throw;
            }
        }

        /// <summary>
        /// Wrapper for async methods - automatically logs start/end with input/output
        /// </summary>
        public static async Task<T> ExecuteWithLoggingAsync<T>(
            Func<Task<T>> action,
            string? input = null,
            string? componentId = null,
            string? componentName = null,
            string? executionId = null,
            [CallerMemberName] string? method = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            LogMethodStart(input, componentId, componentName, executionId, method, file, line);
            try
            {
                var result = await action();
                LogMethodEnd(result, componentId, componentName, executionId, method, file, line);
                return result;
            }
            catch (Exception ex)
            {
                LogError($"Error in {method}: {ex.Message}", ex, componentId, method, file, line);
                throw;
            }
        }

        /// <summary>
        /// Wrapper for async methods returning void - automatically logs start/end
        /// </summary>
        public static async Task ExecuteWithLoggingAsync(
            Func<Task> action,
            string? input = null,
            string? componentId = null,
            string? componentName = null,
            string? executionId = null,
            [CallerMemberName] string? method = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            LogMethodStart(input, componentId, componentName, executionId, method, file, line);
            try
            {
                await action();
                LogMethodEnd("completed", componentId, componentName, executionId, method, file, line);
            }
            catch (Exception ex)
            {
                LogError($"Error in {method}: {ex.Message}", ex, componentId, method, file, line);
                throw;
            }
        }

        #endregion

        #region Filtering Methods for UI

        /// <summary>
        /// Get logs for specific component (for Log tab filtering)
        /// </summary>
        public static LogEntry[] GetLogsForComponent(
            string componentId, 
            LogLevel minimumLevel = LogLevel.Verbose)
        {
            return _logHistory
                .Where(l => l.ComponentId == componentId && l.Level >= minimumLevel)
                .OrderBy(l => l.Timestamp)
                .ToArray();
        }

        /// <summary>
        /// Get logs for execution (all components in a run)
        /// </summary>
        public static LogEntry[] GetLogsForExecution(
            string executionId, 
            LogLevel minimumLevel = LogLevel.Info)
        {
            return _logHistory
                .Where(l => l.ExecutionId == executionId && l.Level >= minimumLevel)
                .OrderBy(l => l.Timestamp)
                .ToArray();
        }

        /// <summary>
        /// Get logs for last execution (Last Run mode)
        /// </summary>
        public static LogEntry[] GetLogsForLastRun(LogLevel minimumLevel = LogLevel.Info)
        {
            var lastExecutionId = _logHistory
                .Where(l => !string.IsNullOrEmpty(l.ExecutionId))
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefault()?.ExecutionId;

            if (string.IsNullOrEmpty(lastExecutionId)) return Array.Empty<LogEntry>();

            return GetLogsForExecution(lastExecutionId, minimumLevel);
        }

        /// <summary>
        /// Get all logs (Full History mode)
        /// </summary>
        public static LogEntry[] GetAllLogs(LogLevel minimumLevel = LogLevel.Verbose)
        {
            return _logHistory
                .Where(l => l.Level >= minimumLevel)
                .OrderBy(l => l.Timestamp)
                .ToArray();
        }

        /// <summary>
        /// Get logs for specific component with execution context
        /// </summary>
        public static LogEntry[] GetLogsForComponentExecution(
            string componentId,
            string executionId,
            LogLevel minimumLevel = LogLevel.Verbose)
        {
            return _logHistory
                .Where(l => l.ComponentId == componentId 
                         && l.ExecutionId == executionId 
                         && l.Level >= minimumLevel)
                .OrderBy(l => l.Timestamp)
                .ToArray();
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Set minimum log level - controls what gets logged
        /// </summary>
        public static void SetMinimumLevel(LogLevel level)
        {
            _minimumLevel = level;
        }

        public static LogLevel GetMinimumLevel() => _minimumLevel;

        /// <summary>
        /// Clear all logs (for new test run)
        /// </summary>
        public static void Clear()
        {
            while (_logHistory.TryDequeue(out _)) { }
        }

        #endregion

        #region Helpers

        private static string ExtractClassName(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "Unknown";
            var fileName = filePath.Split('\\', '/').LastOrDefault() ?? "";
            return fileName.Replace(".cs", "");
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length > max ? s[..max] + "..." : s;
        }

        #endregion
    }
}