using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Test_Automation.Componentes;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using Test_Automation.Services;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    public class AssertionService : IAssertionService
    {
        public List<AssertionRule> ResolveAssertions(List<AssertionRule> assertions, ExecutionContext context, Action<string, TraceLevel>? trace = null)
        {
            TraceFunction(trace, $"assertionsCount={assertions?.Count ?? 0}");
            if (assertions == null || assertions.Count == 0)
            {
                TraceFunction(trace, output: "EmptyList");
                return new List<AssertionRule>();
            }

            var resolved = assertions.Select(a => new AssertionRule(
                a.Source,
                a.JsonPath,
                a.Condition,
                ResolveText(a.Expected, context, trace),
                a.Mode
            )).ToList();
            TraceFunction(trace, output: $"ResolvedList(count={resolved.Count})");
            return resolved;
        }

        public List<AssertionEvaluationResult> EvaluateAssertions(Component component, ComponentData? componentData, ExecutionContext context, Action<string, TraceLevel> trace, ExecutionResult result)
        {
            TraceFunction(trace, $"component={component?.Name}, hasData={componentData != null}");
            var results = new List<AssertionEvaluationResult>();
            if (component == null || component.Assertions == null || componentData == null)
            {
                TraceFunction(trace, output: "Skipped (missing component, assertions or data)");
                return results;
            }

            trace($"Evaluating {component.Assertions?.Count ?? 0} assertions for {component.Name}.", TraceLevel.Info);

            for (var index = 0; index < (component.Assertions?.Count ?? 0); index++)
            {
                var assertion = component.Assertions![index];
                
                // Handle Variable source - get value from context
                object? sourceValue;
                if (assertion.Source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
                {
                    var varName = assertion.Source.Substring("Variable.".Length);
                    sourceValue = context.GetVariable(varName);
                    Logger.Log($"[ASSERT] Variable.{varName} = {sourceValue}", LogLevel.Verbose, componentName: component?.Name);
                    trace($"[ASSERT] Assertion source Variable.{varName} = {sourceValue}", TraceLevel.Info);
                }
                else if (assertion.Source.StartsWith("PreviewVariables.", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle PreviewVariables.varname - get specific variable from context
                    var varName = assertion.Source.Substring("PreviewVariables.".Length);
                    sourceValue = context.GetVariable(varName);
                    Logger.Log($"[ASSERT] PreviewVariables.{varName} = {sourceValue}", LogLevel.Verbose, componentName: component?.Name);
                    Logger.Log($"[ASSERT] Context has variable '{varName}': {context.HasVariable(varName)}", LogLevel.Verbose, componentName: component?.Name);
                    trace($"[ASSERT] Assertion source PreviewVariables.{varName} = {sourceValue}", TraceLevel.Info);
                    trace($"[ASSERT] Checking context for variable '{varName}': {context.HasVariable(varName)}", TraceLevel.Info);
                }
                else if (string.Equals(assertion.Source, "PreviewVariables", StringComparison.OrdinalIgnoreCase))
                {
                    // Return all variables as JSON for PreviewVariables source
                    // Use the hierarchical structure that matches the UI (includes projectVariables and testPlans paths)
                    var variables = context.GetAllVariablesForPreview();
                    sourceValue = System.Text.Json.JsonSerializer.Serialize(variables);
                    Logger.Log($"[ASSERT] PreviewVariables JSON = {sourceValue}", LogLevel.Verbose, componentName: component?.Name);
                    trace($"[ASSERT] Assertion source PreviewVariables = {sourceValue}", TraceLevel.Info);
                }
                else
                {
                    Logger.Log($"[ASSERT] Using GetSourceValue for source: {assertion.Source}", LogLevel.Verbose, componentName: component?.Name);
                    trace($"[ASSERT] Using GetSourceValue for source: {assertion.Source}", TraceLevel.Info);
                    sourceValue = GetSourceValue(assertion.Source, componentData, result);
                    trace($"[VERBOSE] GetSourceValue result type: {sourceValue?.GetType().Name ?? "null"}", TraceLevel.Verbose);
                    trace($"[ASSERT] GetSourceValue returned: {sourceValue}", TraceLevel.Info);
                }
                
                var actualValue = ExtractValue(sourceValue, assertion.JsonPath, trace);
                var (passed, message) = Compare(actualValue, assertion.Condition, assertion.Expected);

                var evalResult = new AssertionEvaluationResult
                {
                    Index = index,
                    Passed = passed,
                    Message = message,
                    Mode = assertion.Mode,
                    Source = assertion.Source,
                    JsonPath = assertion.JsonPath,
                    Condition = assertion.Condition,
                    Expected = assertion.Expected ?? string.Empty,
                    Actual = ConvertToText(actualValue)
                };
                results.Add(evalResult);

                trace($"Assertion '{assertion.Condition}' on '{assertion.Source}': expected='{assertion.Expected}', actual='{actualValue}'. Passed: {passed}.", TraceLevel.Info);
            }
            
            // Log variable snapshot after assertion evaluation
            LogVariableSnapshot(context, "After assertions", trace, component.Id, context.ExecutionId);
            
            TraceFunction(trace, output: $"Completed({results.Count})");
            return results;
        }

        public async Task<List<AssertionEvaluationResult>> EvaluateAssertionsAsync(Component component, ComponentData? componentData, ExecutionContext context, Action<string, TraceLevel> trace, ExecutionResult result)
        {
            TraceFunction(trace, $"component={component?.Name}, hasData={componentData != null}");
            var results = new List<AssertionEvaluationResult>();
            if (component == null || component.Assertions == null || componentData == null)
            {
                TraceFunction(trace, output: "Skipped (missing component, assertions or data)");
                return results;
            }

            trace($"Evaluating {component.Assertions?.Count ?? 0} assertions for {component.Name}.", TraceLevel.Info);
            
            // Log variable snapshot before assertion evaluation
            LogVariableSnapshot(context, "Before assertions", trace, component.Id, context.ExecutionId);

            for (var index = 0; index < (component.Assertions?.Count ?? 0); index++)
            {
                var assertion = component.Assertions![index];
                
                // Handle Variable source - get value from context
                object? sourceValue;
                if (assertion.Source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
                {
                    var varName = assertion.Source.Substring("Variable.".Length);
                    sourceValue = context.GetVariable(varName);
                    Logger.Log($"[ASSERT] Variable.{varName} = {sourceValue}", LogLevel.Verbose, componentName: component?.Name);
                    trace($"[ASSERT] Assertion source Variable.{varName} = {sourceValue}", TraceLevel.Info);
                }
                else if (assertion.Source.StartsWith("PreviewVariables.", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle PreviewVariables.varname - get specific variable from context
                    var varName = assertion.Source.Substring("PreviewVariables.".Length);
                    sourceValue = context.GetVariable(varName);
                    Logger.Log($"[ASSERT] PreviewVariables.{varName} = {sourceValue}", LogLevel.Verbose, componentName: component?.Name);
                    Logger.Log($"[ASSERT] Context has variable '{varName}': {context.HasVariable(varName)}", LogLevel.Verbose, componentName: component?.Name);
                    trace($"[ASSERT] Assertion source PreviewVariables.{varName} = {sourceValue}", TraceLevel.Info);
                    trace($"[ASSERT] Checking context for variable '{varName}': {context.HasVariable(varName)}", TraceLevel.Info);
                }
                else if (string.Equals(assertion.Source, "PreviewVariables", StringComparison.OrdinalIgnoreCase))
                {
                    // Return all variables as JSON for PreviewVariables source
                    var variables = context.GetAllVariablesForPreview();
                    sourceValue = System.Text.Json.JsonSerializer.Serialize(variables);
                    Logger.Log($"[ASSERT] PreviewVariables JSON = {sourceValue}", LogLevel.Verbose, componentName: component?.Name);
                    trace($"[ASSERT] Assertion source PreviewVariables = {sourceValue}", TraceLevel.Info);
                }
                else
                {
                    Logger.Log($"[ASSERT] Using GetSourceValue for source: {assertion.Source}", LogLevel.Verbose, componentName: component?.Name);
                    trace($"[ASSERT] Using GetSourceValue for source: {assertion.Source}", TraceLevel.Info);
                    sourceValue = GetSourceValue(assertion.Source, componentData, result);
                    Logger.Log($"[ASSERT] GetSourceValue returned: {sourceValue}", LogLevel.Verbose, componentName: component?.Name);
                    trace($"[ASSERT] GetSourceValue returned: {sourceValue}", TraceLevel.Info);
                }
                
                var actualValue = ExtractValue(sourceValue, assertion.JsonPath, trace);
                var actualText = ConvertToText(actualValue);
                
                // Handle Script condition specially with async evaluation
                bool passed;
                string message;
                if (string.Equals(assertion.Condition, "Script", StringComparison.OrdinalIgnoreCase))
                {
                    trace($"[VERBOSE] Executing assertion script: {assertion.Expected?.Substring(0, Math.Min(assertion.Expected?.Length ?? 0, 100))}...", TraceLevel.Verbose);
                    var scriptResult = await ScriptEngine.ExecuteAsync("CSharp", assertion.Expected ?? string.Empty, context, actualText, trace);
                    var scriptPassed = scriptResult.Success && scriptResult.Result is bool b && b;
                    passed = scriptPassed;
                    message = scriptResult.Success 
                        ? (passed ? $"Script evaluation returned true." : $"Script evaluation returned false. Result: {scriptResult.Result}")
                        : $"Script error: {scriptResult.Error}";
                }
                else
                {
                    (passed, message) = Compare(actualValue, assertion.Condition, assertion.Expected);
                }

                var evalResult = new AssertionEvaluationResult
                {
                    Index = index,
                    Passed = passed,
                    Message = message,
                    Mode = assertion.Mode,
                    Source = assertion.Source,
                    JsonPath = assertion.JsonPath,
                    Condition = assertion.Condition,
                    Expected = assertion.Expected ?? string.Empty,
                    Actual = actualText
                };
                results.Add(evalResult);

                trace($"Assertion '{assertion.Condition}' on '{assertion.Source}': expected='{assertion.Expected}', actual='{actualValue}'. Passed: {passed}.", TraceLevel.Info);
            }
            
            // Log variable snapshot after assertion evaluation
            LogVariableSnapshot(context, "After assertions", trace, component.Id, context.ExecutionId);
            
            TraceFunction(trace, output: $"Completed({results.Count})");
            return results;
        }

        private (bool Passed, string Message) Compare(object? actual, string condition, string expected)
        {
            var actualText = ConvertToText(actual);

            switch (condition)
            {
                case "Equals":
                    var passed = string.Equals(actualText, expected, StringComparison.Ordinal);
                    return (passed, $"Expected '{expected}', but got '{actualText}'.");
                case "NotEquals":
                    passed = !string.Equals(actualText, expected, StringComparison.Ordinal);
                    return (passed, $"Expected value not to be '{expected}', but it was.");
                case "Contains":
                    passed = actualText?.Contains(expected, StringComparison.Ordinal) ?? false;
                    return (passed, $"Expected value to contain '{expected}', but it did not.");
                case "NotContains":
                    passed = !(actualText?.Contains(expected, StringComparison.Ordinal) ?? false);
                    return (passed, $"Expected value not to contain '{expected}', but it did.");
                case "StartsWith":
                    passed = actualText?.StartsWith(expected, StringComparison.Ordinal) ?? false;
                    return (passed, $"Expected value to start with '{expected}', but got '{actualText}'.");
                case "EndsWith":
                    passed = actualText?.EndsWith(expected, StringComparison.Ordinal) ?? false;
                    return (passed, $"Expected value to end with '{expected}', but got '{actualText}'.");
                case "GreaterThan":
                    if (decimal.TryParse(actualText, NumberStyles.Any, CultureInfo.InvariantCulture, out var actualNum) &&
                        decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var expectedNum))
                    {
                        return (actualNum > expectedNum, $"Expected value to be greater than '{expected}', but got '{actualText}'.");
                    }
                    return (false, "Cannot compare non-numeric values for GreaterThan.");
                case "GreaterOrEqual":
                    if (decimal.TryParse(actualText, NumberStyles.Any, CultureInfo.InvariantCulture, out actualNum) &&
                        decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out expectedNum))
                    {
                        return (actualNum >= expectedNum, $"Expected value to be greater than or equal to '{expected}', but got '{actualText}'.");
                    }
                    return (false, "Cannot compare non-numeric values for GreaterOrEqual.");
                case "LessThan":
                    if (decimal.TryParse(actualText, NumberStyles.Any, CultureInfo.InvariantCulture, out actualNum) &&
                        decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out expectedNum))
                    {
                        return (actualNum < expectedNum, $"Expected value to be less than '{expected}', but got '{actualText}'.");
                    }
                    return (false, "Cannot compare non-numeric values for LessThan.");
                case "LessOrEqual":
                    if (decimal.TryParse(actualText, NumberStyles.Any, CultureInfo.InvariantCulture, out actualNum) &&
                        decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out expectedNum))
                    {
                        return (actualNum <= expectedNum, $"Expected value to be less than or equal to '{expected}', but got '{actualText}'.");
                    }
                    return (false, "Cannot compare non-numeric values for LessOrEqual.");
                case "IsEmpty":
                    passed = string.IsNullOrEmpty(actualText);
                    return (passed, passed ? "Value is empty as expected." : $"Expected value to be empty, but got '{actualText}'.");
                case "IsNotEmpty":
                    passed = !string.IsNullOrEmpty(actualText);
                    return (passed, passed ? $"Value is not empty as expected: '{actualText}'." : "Expected value to not be empty, but it was empty.");
                case "Regex":
                    try
                    {
                        var regex = new System.Text.RegularExpressions.Regex(expected);
                        passed = regex.IsMatch(actualText ?? string.Empty);
                        return (passed, passed ? $"Value matches regex '{expected}'." : $"Value does not match regex '{expected}'.");
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Invalid regex pattern '{expected}': {ex.Message}");
                    }
                case "Script":
                    // Script condition: expected should be a C# script expression that returns bool
                    // The actual value is passed as a variable "value" for the script to evaluate
                    return (false, "Script evaluation is not yet implemented. Expected: " + expected);
                default:
                    return (false, $"Unsupported assertion condition: {condition}");
            }
        }

        private object? GetSourceValue(string source, ComponentData componentData, ExecutionResult? result = null)
        {
            // Handle Variable source - this will be resolved using context in EvaluateAssertions
            if (source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
            {
                return source; // Return the full source string to be resolved later
            }

            // Handle PreviewVariables - get all current variables from context
            if (string.Equals(source, "PreviewVariables", StringComparison.OrdinalIgnoreCase))
            {
                // This will be handled in EvaluateAssertions where we have access to context
                return source;
            }

            // Handle PreviewOutput - raw component output without metadata
            if (string.Equals(source, "PreviewOutput", StringComparison.OrdinalIgnoreCase))
            {
                if (result?.Data is ScriptData script) return script.ExecutionResult;
                if (result?.Data is HttpData http) return http.ResponseBody;
                if (result?.Data is GraphQlData gql) return gql.ResponseBody;
                if (result?.Data is SqlData sql) return sql.QueryResult != null ? JsonSerializer.Serialize(sql.QueryResult) : string.Empty;
                if (result?.Data is ForeachData fe) return fe.CurrentItem != null ? JsonSerializer.Serialize(fe.CurrentItem) : string.Empty;
                if (result?.Data is Test_Automation.Models.ComponentData compData && compData.Properties.TryGetValue("result", out var rawResult)) return rawResult?.ToString() ?? string.Empty;
                return result?.Output ?? string.Empty;
            }

            // Handle UI source names (PreviewResponse, PreviewRequest, etc.)
            if (string.Equals(source, "PreviewResponse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "Body", StringComparison.OrdinalIgnoreCase))
            {
                // If we don't have a result yet, return a message matching the UI
                if (result == null)
                {
                    return JsonSerializer.Serialize(new { message = "Response will be available after execution." });
                }

                // Match the UI structure exactly based on component type
                if (componentData is HttpData http)
                {
                    return JsonSerializer.Serialize(new
                    {
                        runs = new[] { new {
                            threadIndex = result.ThreadIndex,
                            startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            durationMs = result.DurationMs,
                            status = result.Status,
                            responseStatus = http.ResponseStatus,
                            responseBody = http.ResponseBody
                        }}
                    });
                }
                
                if (componentData is GraphQlData gql)
                {
                    return JsonSerializer.Serialize(new
                    {
                        runs = new[] { new {
                            threadIndex = result.ThreadIndex,
                            startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            durationMs = result.DurationMs,
                            status = result.Status,
                            responseStatus = gql.ResponseStatus,
                            responseBody = gql.ResponseBody
                        }}
                    });
                }

                if (componentData is SqlData sql)
                {
                    return JsonSerializer.Serialize(new
                    {
                        runs = new[] { new {
                            threadIndex = result.ThreadIndex,
                            startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            durationMs = result.DurationMs,
                            status = result.Status,
                            rows = sql.QueryResult
                        }}
                    });
                }

                if (componentData is ForeachData fe)
                {
                    // Foreach is unique in UI: shows currentItem directly
                    return JsonSerializer.Serialize(fe.CurrentItem);
                }

                if (componentData is ThreadsData th)
                {
                    // Threads UI shows childResults
                    return JsonSerializer.Serialize(new
                    {
                        childResults = result.PreviewData?.ChildResults ?? new List<ComponentPreviewData>(),
                        message = "Last thread results"
                    });
                }

                if (componentData is TestPlanData tp)
                {
                    // TestPlan UI structure (simplified for current result)
                    return JsonSerializer.Serialize(new
                    {
                        status = tp.Status,
                        runs = new[] { new {
                            componentId = result.ComponentId,
                            componentName = result.ComponentName,
                            status = result.Status,
                            response = result.Output
                        }}
                    });
                }

                // Default generic "runs" structure used by Loop, If, Dataset, etc.
                return JsonSerializer.Serialize(new
                {
                    runs = new[] { new {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        error = result.Error,
                        data = componentData
                    }}
                });
            }

            if (string.Equals(source, "PreviewRequest", StringComparison.OrdinalIgnoreCase))
            {
                if (result == null)
                {
                    return JsonSerializer.Serialize(new { message = "Request will be available after execution." });
                }

                if (componentData is HttpData http)
                {
                    return JsonSerializer.Serialize(new
                    {
                        runs = new[] { new {
                            threadIndex = result.ThreadIndex,
                            startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            durationMs = result.DurationMs,
                            status = result.Status,
                            method = http.Method,
                            url = http.Url,
                            headers = http.Headers,
                            body = http.Body
                        }}
                    });
                }

                if (componentData is GraphQlData gql)
                {
                    return JsonSerializer.Serialize(new
                    {
                        runs = new[] { new {
                            threadIndex = result.ThreadIndex,
                            startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                            durationMs = result.DurationMs,
                            status = result.Status,
                            endpoint = gql.Endpoint,
                            query = gql.Query,
                            variables = gql.Variables,
                            headers = gql.Headers
                        }}
                    });
                }

                // Generic "runs" structure for request
                return JsonSerializer.Serialize(new
                {
                    runs = new[] { new {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        status = result.Status,
                        data = componentData
                    }}
                });
            }

            if (string.Equals(source, "JsonPreview", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(source, "FlowJson", StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Serialize(componentData, new JsonSerializerOptions { WriteIndented = true });
            }

            if (string.Equals(source, "PreviewLogs", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(source, "Logs", StringComparison.OrdinalIgnoreCase))
            {
                if (result == null) return null;
                if (!string.IsNullOrEmpty(result.Output)) return result.Output;
                return string.Join("\n", result.Logs.Select(l => $"[{l.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {l.Message}"));
            }

            if (string.Equals(source, "AssertionPreview", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(source, "Assertions", StringComparison.OrdinalIgnoreCase))
            {
                if (result == null) return JsonSerializer.Serialize(new { summary = new { total = 0, passed = 0, failed = 0 }, details = new List<object>() });
                
                var passed = result.AssertionResults?.Count(r => r.Passed) ?? 0;
                var total = result.AssertionResults?.Count ?? 0;
                var failed = total - passed;

                return JsonSerializer.Serialize(new
                {
                    summary = new
                    {
                        total,
                        passed,
                        assertFailed = result.AssertionResults?.Count(r => !r.Passed && !string.Equals(r.Mode, "Expect", StringComparison.OrdinalIgnoreCase)) ?? 0,
                        expectFailed = result.AssertionResults?.Count(r => !r.Passed && string.Equals(r.Mode, "Expect", StringComparison.OrdinalIgnoreCase)) ?? 0
                    },
                    details = result.AssertionResults
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // HTTP Specific Previews
            if (string.Equals(source, "HttpRequestHeadersPreview", StringComparison.OrdinalIgnoreCase))
            {
                var headers = (componentData as HttpData)?.Headers ?? new Dictionary<string, string>();
                return JsonSerializer.Serialize(headers, new JsonSerializerOptions { WriteIndented = true });
            }

            if (string.Equals(source, "HttpRequestCookiesPreview", StringComparison.OrdinalIgnoreCase))
            {
                var headers = (componentData as HttpData)?.Headers ?? new Dictionary<string, string>();
                return JsonSerializer.Serialize(ExtractCookiesFromHeaders(headers, "Cookie"), new JsonSerializerOptions { WriteIndented = true });
            }

            if (string.Equals(source, "HttpRequestMetadataPreview", StringComparison.OrdinalIgnoreCase))
            {
                var http = componentData as HttpData;
                return JsonSerializer.Serialize(new
                {
                    method = http?.Method,
                    url = http?.Url,
                    status = result?.Status,
                    durationMs = result?.DurationMs,
                    threadIndex = result?.ThreadIndex,
                    startTime = result?.StartTime,
                    endTime = result?.EndTime
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            if (string.Equals(source, "HttpResponseHeadersPreview", StringComparison.OrdinalIgnoreCase))
            {
                var headers = (componentData as HttpData)?.ResponseHeaders ?? new Dictionary<string, string>();
                return JsonSerializer.Serialize(headers, new JsonSerializerOptions { WriteIndented = true });
            }

            if (string.Equals(source, "HttpResponseCookiesPreview", StringComparison.OrdinalIgnoreCase))
            {
                var headers = (componentData as HttpData)?.ResponseHeaders ?? new Dictionary<string, string>();
                return JsonSerializer.Serialize(ExtractCookiesFromHeaders(headers, "Set-Cookie"), new JsonSerializerOptions { WriteIndented = true });
            }

            if (string.Equals(source, "HttpResponseMetadataPreview", StringComparison.OrdinalIgnoreCase))
            {
                var http = componentData as HttpData;
                return JsonSerializer.Serialize(new
                {
                    status = result?.Status,
                    httpStatus = http?.ResponseStatus,
                    bodyLength = (http?.ResponseBody ?? string.Empty).Length,
                    durationMs = result?.DurationMs,
                    threadIndex = result?.ThreadIndex,
                    error = result?.Error
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Handle Dataset-specific source
            if (string.Equals(source, "DatasetRows", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "Rows", StringComparison.OrdinalIgnoreCase))
            {
                if (componentData is DatasetData dataset)
                {
                    return dataset.Rows;
                }
                return null;
            }

            if (string.Equals(source, "Status", StringComparison.OrdinalIgnoreCase))
            {
                return componentData switch
                {
                    HttpData http => http.ResponseStatus?.ToString(),
                    GraphQlData gql => gql.ResponseStatus?.ToString(),
                    IfData ifd => ifd.ConditionMet.ToString(),
                    TestPlanData tp => tp.Status,
                    _ => null
                };
            }
            
            if (componentData != null && componentData.Properties.TryGetValue(source, out var propValue))
            {
                return propValue;
            }

            return null;
        }

        private static List<string> ExtractCookiesFromHeaders(Dictionary<string, string> headers, string cookieHeaderName)
        {
            if (headers == null || !headers.TryGetValue(cookieHeaderName, out var cookieHeader) || string.IsNullOrWhiteSpace(cookieHeader))
            {
                return new List<string>();
            }

            return cookieHeader
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(cookie => cookie.Trim())
                .Where(cookie => !string.IsNullOrWhiteSpace(cookie))
                .ToList();
        }

        private string SerializeQueryResult(List<Dictionary<string, object>>? queryResult)
        {
            if (queryResult == null || queryResult.Count == 0)
            {
                return "[]";
            }

            try
            {
                return System.Text.Json.JsonSerializer.Serialize(queryResult);
            }
            catch
            {
                return "[]";
            }
        }

        private string SerializeDatasetRows(List<Dictionary<string, object>>? rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return "[]";
            }

            try
            {
                return System.Text.Json.JsonSerializer.Serialize(rows);
            }
            catch
            {
                return "[]";
            }
        }

        private object? ExtractValue(object? sourceValue, string jsonPath, Action<string, TraceLevel>? trace = null)
        {
            if (sourceValue == null) 
            {
                trace?.Invoke($"[VERBOSE] Assertion ExtractValue: sourceValue is null, returning null", TraceLevel.Verbose);
                return null;
            }

            var sourceText = ConvertToText(sourceValue);
            if (string.IsNullOrWhiteSpace(jsonPath) || string.IsNullOrWhiteSpace(sourceText))
            {
                trace?.Invoke($"[VERBOSE] Assertion ExtractValue: jsonPath or sourceText empty, returning sourceValue: {TruncateForLogging(sourceValue, 100)}", TraceLevel.Verbose);
                return sourceValue;
            }

            trace?.Invoke($"[VERBOSE] Assertion ExtractValue: Source type={sourceValue.GetType().Name}, JsonPath='{jsonPath}'", TraceLevel.Verbose);
            trace?.Invoke($"[VERBOSE] Assertion ExtractValue: Source text (first 200 chars)='{TruncateForLogging(sourceText, 200)}'", TraceLevel.Verbose);

            try
            {
                using var doc = JsonDocument.Parse(sourceText);
                if (doc.RootElement.TryGetPropertyByJsonPath(jsonPath, out var element, 
                    step => trace?.Invoke($"[VERBOSE] Assertion JsonPath step: {step}", TraceLevel.Verbose)))
                {
                    trace?.Invoke($"[VERBOSE] Assertion JsonPath result: type={element.ValueKind}, value={TruncateForLogging(element.GetRawText(), 100)}", TraceLevel.Verbose);
                    return element.ValueKind switch
                    {
                        JsonValueKind.String => element.GetString(),
                        JsonValueKind.Number => element.GetDecimal(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => element.GetRawText()
                    };
                }
                else
                {
                    trace?.Invoke($"[VERBOSE] Assertion JsonPath '{jsonPath}' not found in source", TraceLevel.Verbose);
                }
            }
            catch (JsonException ex)
            {
                trace?.Invoke($"[VERBOSE] Assertion JsonException parsing source: {ex.Message}", TraceLevel.Verbose);
                // Try lenient parsing for JavaScript-style objects like {a:1} or {name:'value'}
                var lenient = LenientJsonFix(sourceText);
                if (lenient != sourceText)
                {
                    trace?.Invoke($"[VERBOSE] Assertion trying lenient JSON fix: '{TruncateForLogging(lenient, 200)}'", TraceLevel.Verbose);
                    try
                    {
                        using var doc2 = JsonDocument.Parse(lenient);
                        if (doc2.RootElement.TryGetPropertyByJsonPath(jsonPath, out var element2,
                            step => trace?.Invoke($"[VERBOSE] Assertion lenient JsonPath step: {step}", TraceLevel.Verbose)))
                        {
                            trace?.Invoke($"[VERBOSE] Assertion lenient JsonPath result: type={element2.ValueKind}, value={TruncateForLogging(element2.GetRawText(), 100)}", TraceLevel.Verbose);
                            return element2.ValueKind switch
                            {
                                JsonValueKind.String => element2.GetString(),
                                JsonValueKind.Number => element2.GetDecimal(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Null => null,
                                _ => element2.GetRawText()
                            };
                        }
                    }
                    catch (JsonException ex2) 
                    { 
                        trace?.Invoke($"[VERBOSE] Assertion lenient parsing also failed: {ex2.Message}", TraceLevel.Verbose);
                    }
                }
            }
            trace?.Invoke($"[VERBOSE] Assertion ExtractValue: Returning null as JsonPath not found", TraceLevel.Verbose);
            return null;
        }

        private static string LenientJsonFix(string text)
        {
            var trimmed = text.Trim();
            var fixed_ = System.Text.RegularExpressions.Regex.Replace(
                trimmed, @"(?<=[{,\[])\s*([A-Za-z_]\w*)\s*(?=:)", "\"$1\"");
            fixed_ = System.Text.RegularExpressions.Regex.Replace(fixed_, @"'([^']*)'", "\"$1\"");
            return fixed_;
        }
        
        private string ResolveText(string? text, ExecutionContext context, Action<string, TraceLevel>? trace = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            TraceFunction(trace, $"text='{text}'");
            var result = System.Text.RegularExpressions.Regex.Replace(text, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value.Trim();
                if (!context.HasVariable(key))
                {
                    return match.Value;
                }
                var val = ConvertToText(context.GetVariable(key));
                TraceFunction(trace, $"ResolveText.InnerMatch: {match.Value} -> {val}");
                return val;
            });

            if (result != text)
            {
                TraceFunction(trace, output: $"'{result}'");
            }

            return result;
        }

        private string ConvertToText(object? value)
        {
            if (value == null) return string.Empty;
            if (value is JsonElement json)
            {
                return json.ValueKind == JsonValueKind.String
                    ? json.GetString() ?? string.Empty
                    : json.GetRawText();
            }
            return value.ToString() ?? string.Empty;
        }

        private static string TruncateForLogging(object? value, int maxLength = 200)
        {
            if (value == null) return "null";
            var text = value.ToString() ?? string.Empty;
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "...[truncated]";
        }
        private static void TraceFunction(Action<string, TraceLevel>? trace, string inputs = "", string output = "", [CallerMemberName] string methodName = "")
        {
            // Also log to centralized Logger
            var msg = string.IsNullOrEmpty(output) 
                ? $"AssertionService.{methodName}({inputs})"
                : $"AssertionService.{methodName} -> {output}";
            Logger.Log(msg, LogLevel.Verbose, input: inputs, output: output);

            // Keep backward compatibility with old callback
            if (trace == null) return;
            var oldMsg = string.IsNullOrEmpty(output) 
                ? $"[VERBOSE] Test_Automation.Services.AssertionService.{methodName}({inputs})"
                : $"[VERBOSE] Test_Automation.Services.AssertionService.{methodName} -> {output}";
            trace(oldMsg, TraceLevel.Verbose);
        }

        private static void LogVariableSnapshot(ExecutionContext context, string stage, Action<string, TraceLevel>? trace = null, 
            string? componentId = null, string? executionId = null)
        {
            if (trace == null && Logger.GetMinimumLevel() > LogLevel.Verbose) return;
            
            try
            {
                var variables = context.GetAllVariablesForPreview();
                var varCount = variables.Count;
                
                if (varCount == 0)
                {
                    trace?.Invoke($"[ASSERT-VARIABLES] {stage}: No variables in context", TraceLevel.Verbose);
                    Logger.Log($"[ASSERT-VARIABLES] {stage}: No variables in context", LogLevel.Verbose, 
                        componentId: componentId, executionId: executionId);
                    return;
                }
                
                var varNames = string.Join(", ", variables.Keys.Take(15).OrderBy(k => k));
                if (variables.Count > 15) varNames += "...";
                
                var sampleValues = string.Join(", ", variables
                    .OrderBy(kv => kv.Key)
                    .Take(5)
                    .Select(kv => $"{kv.Key}={TruncateForLogging(kv.Value, 50)}"));
                
                var message = $"[ASSERT-VARIABLES] {stage}: {varCount} variables. Names: [{varNames}]";
                trace?.Invoke(message, TraceLevel.Verbose);
                Logger.Log(message, LogLevel.Verbose, componentId: componentId, executionId: executionId);
                
                if (varCount <= 10) // Show values for small sets
                {
                    var valuesMessage = $"[ASSERT-VARIABLES] {stage} Values: [{sampleValues}]";
                    trace?.Invoke(valuesMessage, TraceLevel.Verbose);
                    Logger.Log(valuesMessage, LogLevel.Verbose, componentId: componentId, executionId: executionId);
                }
                else
                {
                    var samplesMessage = $"[ASSERT-VARIABLES] {stage} Samples (first 5): [{sampleValues}]";
                    trace?.Invoke(samplesMessage, TraceLevel.Verbose);
                    Logger.Log(samplesMessage, LogLevel.Verbose, componentId: componentId, executionId: executionId);
                }
            }
            catch (Exception ex)
            {
                trace?.Invoke($"[ASSERT-VARIABLES] {stage}: Error logging variables: {ex.Message}", TraceLevel.Warning);
                Logger.Log($"[ASSERT-VARIABLES] {stage}: Error logging variables: {ex.Message}", LogLevel.Warning, 
                    componentId: componentId, executionId: executionId);
            }
        }
    }
}
