using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Test_Automation.Componentes;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    public class VariableService : IVariableService
    {
        public Dictionary<string, string> ResolveSettings(Dictionary<string, string> settings, ExecutionContext context)
        {
            if (settings == null || settings.Count == 0)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var resolvedSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in settings)
            {
                resolvedSettings[setting.Key] = ResolveText(setting.Value, context);
            }
            return resolvedSettings;
        }

        public List<VariableExtractionRule> ResolveExtractors(List<VariableExtractionRule> extractors, ExecutionContext context)
        {
            if (extractors == null || extractors.Count == 0)
            {
                return new List<VariableExtractionRule>();
            }

            return extractors.Select(e => new VariableExtractionRule(
                ResolveText(e.Source, context),
                ResolveText(e.JsonPath, context),
                ResolveText(e.VariableName, context)
            )).ToList();
        }

        public void ApplyVariableExtractors(Component component, ExecutionContext context, ComponentData? componentData, Action<string> trace, ExecutionResult result)
        {
            if (component.Extractors == null || component.Extractors.Count == 0 || componentData == null)
            {
                return;
            }

            trace($"Applying {component.Extractors.Count} variable extractors for {component.Name}.");

            foreach (var extractor in component.Extractors)
            {
                if (string.IsNullOrWhiteSpace(extractor.VariableName))
                {
                    trace($"Skipping extractor with empty variable name for {component.Name}.");
                    continue;
                }

                // Handle Variable source - get value from context variables
                if (extractor.Source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
                {
                    var varName = extractor.Source.Substring("Variable.".Length);
                    var value = context.GetVariable(varName);
                    trace($"Extractor '{extractor.VariableName}': Variable source '{varName}' = {value}");
                    context.SetVariable(extractor.VariableName, value ?? string.Empty);
                    continue;
                }

                var sourceValue = GetSourceValue(extractor.Source, componentData, context, result);
                
                trace($"Extractor '{extractor.VariableName}': GetSourceValue returned type={sourceValue?.GetType().Name ?? "null"}");
                
                if (sourceValue == null)
                {
                    trace($"Extractor for '{extractor.VariableName}': source '{extractor.Source}' returned null. Component type: {componentData.GetType().Name}");
                    continue;
                }

                var extractedValue = ExtractValue(sourceValue, extractor.JsonPath);

                if (extractedValue == null)
                {
                    // JSON path didn't match - try to use the raw source value
                    trace($"Extractor for '{extractor.VariableName}': jsonPath='{extractor.JsonPath}' returned null. Using source value directly.");
                    trace($"  Source value type: {sourceValue.GetType().Name}, preview: {(sourceValue.ToString()?.Substring(0, Math.Min(100, sourceValue.ToString()?.Length ?? 0)))}");
                    context.SetVariable(extractor.VariableName, sourceValue);
                    trace($"  Set variable '{extractor.VariableName}' to: {sourceValue}");
                }
                else
                {
                    trace($"Extractor for '{extractor.VariableName}': extracted value type={extractedValue.GetType().Name}, preview={(extractedValue.ToString()?.Substring(0, Math.Min(50, extractedValue.ToString()?.Length ?? 0)))}");
                    context.SetVariable(extractor.VariableName, extractedValue);
                }
            }
        }

        private string ResolveText(string? text, ExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return Regex.Replace(text, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value.Trim();
                if (!context.HasVariable(key))
                {
                    return match.Value;
                }
                return ConvertVariableToText(context.GetVariable(key));
            });
        }

        private object? GetSourceValue(string source, ComponentData componentData, ExecutionContext? context = null, ExecutionResult? result = null)
        {
            // Handle Variable source - read from component's variables (set during execution)
            if (source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
            {
                var varName = source.Substring("Variable.".Length);
                return context?.GetVariable(varName) ?? varName;
            }

            // Handle PreviewVariables - get all current variables from context
            if (string.Equals(source, "PreviewVariables", StringComparison.OrdinalIgnoreCase))
            {
                if (context == null) return null;
                var variables = context.GetAllVariablesForPreview();
                return System.Text.Json.JsonSerializer.Serialize(variables);
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

        private string SerializeDatasetRowsWithPath(List<Dictionary<string, object>>? rows, string? jsonPath)
        {
            if (rows == null || rows.Count == 0)
            {
                return "[]";
            }

            // If no jsonPath specified or path is empty, return all rows
            if (string.IsNullOrWhiteSpace(jsonPath) || jsonPath == "$")
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Serialize(rows);
                }
                catch
                {
                    return "[]";
                }
            }

            // Try to apply the JSON path
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(rows);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetPropertyByJsonPath(jsonPath, out var element))
                {
                    return element.GetRawText();
                }
            }
            catch
            {
                // If path doesn't match, return all rows
            }

            // Fallback to all rows
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(rows);
            }
            catch
            {
                return "[]";
            }
        }

        private object? ExtractValue(object? sourceValue, string jsonPath)
        {
            if (sourceValue == null) return null;

            var sourceText = ConvertVariableToText(sourceValue);
            if (string.IsNullOrWhiteSpace(jsonPath) || string.IsNullOrWhiteSpace(sourceText))
            {
                return sourceValue;
            }

            try
            {
                using var doc = JsonDocument.Parse(sourceText);
                if (doc.RootElement.TryGetPropertyByJsonPath(jsonPath, out var element))
                {
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
            }
            catch (JsonException)
            {
                // Not a valid JSON, so cannot apply JsonPath
            }

            // If JSON path didn't match, return the original source value
            return sourceValue;
        }

        private string ConvertVariableToText(object? value)
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
    }

    public static class JsonElementExtensions
    {
        public static bool TryGetPropertyByJsonPath(this JsonElement element, string jsonPath, out JsonElement value)
        {
            value = default;

            // Normalize: strip leading $. or $ while respecting $[ syntax
            var path = jsonPath?.Trim() ?? string.Empty;
            if (path.StartsWith("$."))
                path = path.Substring(2);
            else if (path.StartsWith("$["))
                path = path.Substring(1); // Keep the [
            else if (path == "$")
                path = string.Empty;

            // Root selector
            if (string.IsNullOrEmpty(path))
            {
                value = element;
                return true;
            }

            // Tokenize the path into segments, splitting on '.' while respecting bracket notation.
            // e.g. "dataset[0].name" → ["dataset[0]", "name"]
            //      "[0].name"        → ["[0]", "name"]
            var tokens = TokenizePath(path);
            JsonElement current = element;

            foreach (var token in tokens)
            {
                // If the current value is a JSON string that itself contains JSON, parse it first
                // so that "$.dataset[0].name" works when dataset is stored as a string variable.
                if (current.ValueKind == JsonValueKind.String)
                {
                    var raw = current.GetString();
                    if (raw != null && (raw.TrimStart().StartsWith("[") || raw.TrimStart().StartsWith("{")))
                    {
                        try
                        {
                            // We need to keep the parsed document alive; clone the root element.
                            using var innerDoc = JsonDocument.Parse(raw);
                            current = innerDoc.RootElement.Clone();
                        }
                        catch (JsonException)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                if (token.StartsWith("[") && token.EndsWith("]"))
                {
                    // Array index: [0], [1], ...
                    var indexStr = token.Substring(1, token.Length - 2);
                    if (!int.TryParse(indexStr, out var idx))
                        return false;
                    if (current.ValueKind != JsonValueKind.Array)
                        return false;
                    var arr = current.EnumerateArray().ToList();
                    if (idx < 0 || idx >= arr.Count)
                        return false;
                    current = arr[idx];
                }
                else
                {
                    // Property name, potentially with trailing bracket(s): e.g. "dataset[0]"
                    var bracketIdx = token.IndexOf('[');
                    if (bracketIdx >= 0)
                    {
                        // Split into property part and bracket part(s)
                        var propName = token.Substring(0, bracketIdx);
                        var bracketPart = token.Substring(bracketIdx); // e.g. "[0]"

                        if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propName, out var propElement))
                            return false;
                        current = propElement;

                        // Now handle the bracket part via recursion-like approach
                        // Parse inner index
                        var innerTokens = TokenizeBrackets(bracketPart);
                        foreach (var innerToken in innerTokens)
                        {
                            // Unwrap embedded JSON string if needed
                            if (current.ValueKind == JsonValueKind.String)
                            {
                                var raw = current.GetString();
                                if (raw != null && (raw.TrimStart().StartsWith("[") || raw.TrimStart().StartsWith("{")))
                                {
                                    try
                                    {
                                        using var innerDoc = JsonDocument.Parse(raw);
                                        current = innerDoc.RootElement.Clone();
                                    }
                                    catch (JsonException) { return false; }
                                }
                                else { return false; }
                            }

                            if (innerToken.StartsWith("[") && innerToken.EndsWith("]"))
                            {
                                var indexStr = innerToken.Substring(1, innerToken.Length - 2);
                                if (!int.TryParse(indexStr, out var idx)) return false;
                                if (current.ValueKind != JsonValueKind.Array) return false;
                                var arr = current.EnumerateArray().ToList();
                                if (idx < 0 || idx >= arr.Count) return false;
                                current = arr[idx];
                            }
                            else { return false; }
                        }
                    }
                    else
                    {
                        // Plain property name
                        if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(token, out var next))
                            return false;
                        current = next;
                    }
                }
            }

            value = current;
            return true;
        }

        /// <summary>Splits a dot-separated path into tokens, keeping bracket groups intact.</summary>
        private static List<string> TokenizePath(string path)
        {
            var tokens = new List<string>();
            var sb = new System.Text.StringBuilder();
            int depth = 0;

            foreach (var ch in path)
            {
                if (ch == '[') depth++;
                if (ch == ']') depth--;

                if (ch == '.' && depth == 0)
                {
                    if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            if (sb.Length > 0) tokens.Add(sb.ToString());
            return tokens;
        }

        /// <summary>Splits a bracket-only suffix like "[0][1]" into ["[0]","[1]"].</summary>
        private static List<string> TokenizeBrackets(string brackets)
        {
            var tokens = new List<string>();
            var sb = new System.Text.StringBuilder();
            foreach (var ch in brackets)
            {
                if (ch == '[' && sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                sb.Append(ch);
            }
            if (sb.Length > 0) tokens.Add(sb.ToString());
            return tokens;
        }
    }
}
