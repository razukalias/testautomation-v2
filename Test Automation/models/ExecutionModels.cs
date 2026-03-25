using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Threading;
using Test_Automation.Services;

namespace Test_Automation.Models
{
    public enum TraceLevel
    {
        Verbose,
        Info,
        Warning,
        Error
    }

    public class TraceEventArgs
    {
        public TraceLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ComponentId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Execution context for a test plan run
    /// </summary>
    public class ExecutionContext
    {
        private ConcurrentDictionary<string, object> _variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _stopSource = new CancellationTokenSource();

        // Static field to store last execution's variables for UI access
        private static ConcurrentDictionary<string, object>? _lastExecutionVariables;
        public static ConcurrentDictionary<string, object>? LastExecutionVariables => _lastExecutionVariables;

        [JsonPropertyName("executionId")]
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("variables")]
        public ConcurrentDictionary<string, object> Variables
        {
            get => _variables;
            set => _variables = value != null
                ? new ConcurrentDictionary<string, object>(value, StringComparer.OrdinalIgnoreCase)
                : new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("endTime")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "running";

        [JsonPropertyName("results")]
        public List<ExecutionResult> Results { get; set; } = new List<ExecutionResult>();

        [JsonPropertyName("isRunning")]
        public bool IsRunning { get; set; } = true;

        [JsonIgnore]
        public CancellationToken StopToken => _stopSource.Token;

        public void RequestStop()
        {
            IsRunning = false;
            Status = "stopping";
            if (!_stopSource.IsCancellationRequested)
            {
                _stopSource.Cancel();
            }
        }

        public void SaveVariablesForUi()
        {
            // Save a copy of variables for UI to access
            _lastExecutionVariables = new ConcurrentDictionary<string, object>(_variables);
        }

        public void ResetStopRequest()
        {
            if (_stopSource.IsCancellationRequested)
            {
                _stopSource.Dispose();
                _stopSource = new CancellationTokenSource();
            }

            IsRunning = true;
            if (string.Equals(Status, "stopping", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Status, "stopped", StringComparison.OrdinalIgnoreCase))
            {
                Status = "running";
            }
        }

        public void SetVariable(string key, object value)
        {
            var normalizedKey = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return;
            }

            Variables[normalizedKey] = value;
            
            // Log variable operation at verbose level
            Logger.Log($"[VARIABLE] SetVariable: '{normalizedKey}' = '{TruncateValue(value, 100)}'", 
                Test_Automation.Services.LogLevel.Verbose, executionId: ExecutionId);
        }

        public object? GetVariable(string key)
        {
            var normalizedKey = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return null;
            }

            var value = Variables.TryGetValue(normalizedKey, out var val) ? val : null;
            
            // Log variable operation at verbose level
            Logger.Log($"[VARIABLE] GetVariable: '{normalizedKey}' = '{TruncateValue(value, 100)}'", 
                Test_Automation.Services.LogLevel.Verbose, executionId: ExecutionId);
            
            return value;
        }

        public bool HasVariable(string key)
        {
            var normalizedKey = key?.Trim() ?? string.Empty;
            var exists = !string.IsNullOrWhiteSpace(normalizedKey) && Variables.ContainsKey(normalizedKey);
            
            // Log variable operation at verbose level
            Logger.Log($"[VARIABLE] HasVariable: '{normalizedKey}' = {exists}", 
                Test_Automation.Services.LogLevel.Verbose, executionId: ExecutionId);
            
            return exists;
        }

        private static string TruncateValue(object? value, int maxLength = 200)
        {
            if (value == null) return "null";
            var text = value.ToString() ?? string.Empty;
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "...[truncated]";
        }

        /// <summary>
        /// Stores the hierarchical variable structure for PreviewVariables display/assertion.
        /// This is built from node variables (project and testplan) and used for hierarchical access.
        /// </summary>
        private Dictionary<string, object>? _hierarchicalVariables;
        public Dictionary<string, object>? HierarchicalVariables
        {
            get => _hierarchicalVariables;
            set => _hierarchicalVariables = value;
        }

        /// <summary>
        /// Gets variables for PreviewVariables - builds hierarchical structure similar to UI.
        /// Returns a nested dictionary that can be properly serialized to JSON with nested objects.
        /// </summary>
        public Dictionary<string, object> GetAllVariablesForPreview()
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // First, add all flat variables (backward compatibility) — coerce JSON strings to objects
            foreach (var kvp in Variables)
            {
                result[kvp.Key] = CoercePreviewValue(kvp.Value ?? string.Empty);
            }

            // If we have stored hierarchical variables (from node definitions), merge them
            if (_hierarchicalVariables != null)
            {
                // Add projectVariables as nested object
                if (_hierarchicalVariables.TryGetValue("projectVariables", out var projVars) && projVars is Dictionary<string, object> projDict)
                {
                    // Create nested projectVariables object
                    var projectVarsNested = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in projDict)
                    {
                        projectVarsNested[kvp.Key] = CoercePreviewValue(kvp.Value);
                        // Also at top level for easier access (if not already exists)
                        if (!result.ContainsKey(kvp.Key))
                        {
                            result[kvp.Key] = CoercePreviewValue(kvp.Value);
                        }
                    }
                    result["projectVariables"] = projectVarsNested;
                }

                // Add testPlans as nested object
                if (_hierarchicalVariables.TryGetValue("testPlans", out var tpVars) && tpVars is Dictionary<string, object> tpDict)
                {
                    var testPlansNested = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (var planKvp in tpDict)
                    {
                        if (planKvp.Value is Dictionary<string, object> planVars)
                        {
                            // Create nested testPlan object with its variables
                            var planVarsNested = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            foreach (var varKvp in planVars)
                            {
                                planVarsNested[varKvp.Key] = CoercePreviewValue(varKvp.Value);
                                // Also at top level if not exists
                                if (!result.ContainsKey(varKvp.Key))
                                {
                                    result[varKvp.Key] = CoercePreviewValue(varKvp.Value);
                                }
                            }
                            testPlansNested[planKvp.Key] = planVarsNested;
                        }
                        else
                        {
                            testPlansNested[planKvp.Key] = CoercePreviewValue(planKvp.Value);
                        }
                    }
                    result["testPlans"] = testPlansNested;
                }
            }

            return result;
        }

        private static object CoercePreviewValue(object? value)
        {
            if (value == null) return string.Empty;

            var text = value switch
            {
                System.Text.Json.JsonElement je => je.ValueKind == System.Text.Json.JsonValueKind.String
                    ? je.GetString() ?? string.Empty
                    : je.GetRawText(),
                _ => value.ToString() ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var trimmed = text.Trim();
            if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
            {
                // Try strict JSON first
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                    return doc.RootElement.Clone();
                }
                catch (System.Text.Json.JsonException)
                {
                    // Try lenient: quote unquoted keys and wrap single-quoted values with double quotes
                    var fixed_ = System.Text.RegularExpressions.Regex.Replace(
                        trimmed,
                        @"(?<=[{,\[])\s*([A-Za-z_]\w*)\s*(?=:)",
                        "\"$1\"");
                    fixed_ = System.Text.RegularExpressions.Regex.Replace(fixed_, @"'([^']*)'", "\"$1\"");
                    if (fixed_ != trimmed)
                    {
                        try
                        {
                            using var doc2 = System.Text.Json.JsonDocument.Parse(fixed_);
                            return doc2.RootElement.Clone();
                        }
                        catch (System.Text.Json.JsonException) { }
                    }
                }
            }

            return text;
        }
    }

    /// <summary>
    /// Execution result for individual component
    /// </summary>
    public class AssertionEvaluationResult
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "Assert";

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("jsonPath")]
        public string JsonPath { get; set; } = string.Empty;

        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("expected")]
        public string Expected { get; set; } = string.Empty;

        [JsonPropertyName("actual")]
        public string Actual { get; set; } = string.Empty;

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class ExecutionResult
    {
        [JsonPropertyName("componentId")]
        public string ComponentId { get; set; } = string.Empty;

        [JsonPropertyName("componentName")]
        public string ComponentName { get; set; } = string.Empty;

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("endTime")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("duration")]
        public long DurationMs { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending"; // pending, running, passed, failed

        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("threadIndex")]
        public int ThreadIndex { get; set; }

        [JsonPropertyName("threadGroupId")]
        public string ThreadGroupId { get; set; } = string.Empty;

        [JsonPropertyName("executionId")]
        public string ExecutionId { get; set; } = string.Empty;

        [JsonPropertyName("assertions")]
        public List<AssertionEvaluationResult> AssertionResults { get; set; } = new List<AssertionEvaluationResult>();

        [JsonPropertyName("assertFailedCount")]
        public int AssertFailedCount { get; set; }

        [JsonPropertyName("expectFailedCount")]
        public int ExpectFailedCount { get; set; }

        [JsonPropertyName("assertPassedCount")]
        public int AssertPassedCount { get; set; }

        [JsonPropertyName("logs")]
        public List<TraceEventArgs> Logs { get; set; } = new List<TraceEventArgs>();

        [JsonPropertyName("previewData")]
        public ComponentPreviewData? PreviewData { get; set; }

        public void MarkAsCompleted(bool success = true)
        {
            EndTime = DateTime.UtcNow;
            DurationMs = (long)(EndTime.Value - StartTime).TotalMilliseconds;
            Status = success ? "passed" : "failed";
            Passed = success;
        }
    }

    /// <summary>
    /// Test execution summary
    /// </summary>
    public class ExecutionSummary
    {
        [JsonPropertyName("executionId")]
        public string ExecutionId { get; set; } = string.Empty;

        [JsonPropertyName("totalComponents")]
        public int TotalComponents { get; set; }

        [JsonPropertyName("passedComponents")]
        public int PassedComponents { get; set; }

        [JsonPropertyName("failedComponents")]
        public int FailedComponents { get; set; }

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public DateTime EndTime { get; set; }

        [JsonPropertyName("totalDurationMs")]
        public long TotalDurationMs { get; set; }

        [JsonPropertyName("successRate")]
        public double SuccessRate => TotalComponents > 0 ? (double)PassedComponents / TotalComponents * 100 : 0;

        [JsonPropertyName("status")]
        public string Status => FailedComponents == 0 ? "passed" : "failed";
    }
}
