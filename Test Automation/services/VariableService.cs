using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Test_Automation.Componentes;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using ExecutionContext = Test_Automation.Models.ExecutionContext;
using LogLevel = Test_Automation.Services.LogLevel;

namespace Test_Automation.Services
{
    public class VariableService : IVariableService
    {
        public Dictionary<string, string> ResolveSettings(Dictionary<string, string> settings, ExecutionContext context, Action<string, TraceLevel>? trace = null)
        {
            TraceFunction(trace, $"settingsCount={settings?.Count ?? 0}");
            if (settings == null || settings.Count == 0)
            {
                var empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                TraceFunction(trace, output: "EmptyDictionary");
                return empty;
            }

            var resolvedSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in settings)
            {
                resolvedSettings[setting.Key] = ResolveText(setting.Value, context, trace);
            }
            TraceFunction(trace, output: $"ResolvedDictionary(count={resolvedSettings.Count})");
            return resolvedSettings;
        }

        public List<VariableExtractionRule> ResolveExtractors(List<VariableExtractionRule> extractors, ExecutionContext context, Action<string, TraceLevel>? trace = null)
        {
            TraceFunction(trace, $"extractorsCount={extractors?.Count ?? 0}");
            if (extractors == null || extractors.Count == 0)
            {
                TraceFunction(trace, output: "EmptyList");
                return new List<VariableExtractionRule>();
            }

            var resolved = extractors.Select(e => new VariableExtractionRule(
                ResolveText(e.Source, context, trace),
                ResolveText(e.JsonPath, context, trace),
                ResolveText(e.VariableName, context, trace)
            )).ToList();
            TraceFunction(trace, output: $"ResolvedList(count={resolved.Count})");
            return resolved;
        }

        public void ApplyVariableExtractors(Componentes.Component component, ExecutionContext context, ComponentData? componentData, Action<string, TraceLevel> trace, ExecutionResult result)
        {
            TraceFunction(trace, $"component={component?.Name}, hasData={componentData != null}");
            if (component == null || (component.Extractors?.Count ?? 0) == 0 || componentData == null)
            {
                TraceFunction(trace, output: "Completed (Skipped)");
                return;
            }

            foreach (var extractor in component.Extractors!)
            {
                if (string.IsNullOrWhiteSpace(extractor.VariableName))
                {
                    trace($"[VERBOSE] Skipping extractor with empty variable name for {component?.Name ?? "unknown"}.", TraceLevel.Verbose);
                    continue;
                }

                // Handle Variable source - get value from context variables
                if (string.Equals(extractor.Source, "Variable", StringComparison.OrdinalIgnoreCase) || extractor.Source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
                {
                    var varName = extractor.Source.Contains(".") ? extractor.Source.Substring(extractor.Source.IndexOf(".") + 1) : extractor.Source;
                    var value = context.GetVariable(varName);
                    if (value == null)
                    {
                        // Warn when source variable doesn't exist
                        trace($"[WARNING] Extractor '{extractor.VariableName}': Source variable '{varName}' not found. Setting to empty string.", TraceLevel.Warning);
                    }
                    else
                    {
                        trace($"[VERBOSE] Extractor Variable source resolution: {varName} -> {value}", TraceLevel.Verbose);
                        trace($"Extractor '{extractor.VariableName}': Variable source '{varName}' = {value}", TraceLevel.Info);
                    }
                    context.SetVariable(extractor.VariableName, value ?? string.Empty);
                    continue;
                }

                trace($"[VERBOSE] Resolving source value for: {extractor.Source}", TraceLevel.Verbose);
                var sourceValue = GetSourceValue(extractor.Source, componentData, context, result, trace);
                
                trace($"[VERBOSE] Source resolution result: {sourceValue?.GetType().Name ?? "null"}", TraceLevel.Verbose);
                
                if (sourceValue == null)
                {
                    trace($"Extractor for '{extractor.VariableName}': source '{extractor.Source}' returned null.", TraceLevel.Info);
                    continue;
                }

                trace($"[VERBOSE] Evaluating JsonPath '{extractor.JsonPath}' on source value", TraceLevel.Verbose);
                var extractedValue = ExtractValue(sourceValue, extractor.JsonPath);

                if (extractedValue == null)
                {
                    // JSON path didn't match - try to use the raw source value
                    trace($"[VERBOSE] JsonPath evaluation returned null, falling back to raw source value", TraceLevel.Verbose);
                    context.SetVariable(extractor.VariableName, sourceValue);
                }
                else
                {
                    trace($"[VERBOSE] Extraction successful: {extractedValue}", TraceLevel.Verbose);
                    context.SetVariable(extractor.VariableName, extractedValue);
                }
            }
            TraceFunction(trace, output: "Success");
        }

        private string ResolveText(string? text, ExecutionContext context, Action<string, TraceLevel>? trace = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            TraceFunction(trace, $"text='{text}'");
            var result = Regex.Replace(text, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value.Trim();
                if (!context.HasVariable(key))
                {
                    return match.Value;
                }
                var val = ConvertVariableToText(context.GetVariable(key));
                TraceFunction(trace, $"ResolveText.InnerMatch: {match.Value} -> {val}");
                return val;
            });

            if (result != text)
            {
                TraceFunction(trace, output: $"'{result}'");
            }
            return result;
        }

        private object? GetSourceValue(string source, ComponentData componentData, ExecutionContext? context = null, ExecutionResult? result = null, Action<string, TraceLevel>? trace = null)
        {
            TraceFunction(trace, $"source='{source}'");
            // Handle Variable source
            if (source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
            {
                var varName = source.Substring("Variable.".Length);
                var val = context?.GetVariable(varName) ?? varName;
                TraceFunction(trace, output: "VariableResult");
                return val;
            }

            // Handle PreviewVariables
            if (string.Equals(source, "PreviewVariables", StringComparison.OrdinalIgnoreCase))
            {
                if (context == null) return null;
                var variables = context.GetAllVariablesForPreview();
                var json = System.Text.Json.JsonSerializer.Serialize(variables);
                TraceFunction(trace, output: "PreviewVariablesResult");
                return json;
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
                if (result == null) return JsonSerializer.Serialize(new { message = "Response will be available after execution." });
                
                object? data = null;
                if (componentData is HttpData http) data = new { runs = new[] { new { responseStatus = http.ResponseStatus, responseBody = http.ResponseBody } } };
                else if (componentData is GraphQlData gql) data = new { runs = new[] { new { responseStatus = gql.ResponseStatus, responseBody = gql.ResponseBody } } };
                else if (componentData is SqlData sql) data = new { runs = new[] { new { rows = sql.QueryResult } } };
                else if (componentData is ForeachData fe) data = fe.CurrentItem;
                else data = new { runs = new[] { new { status = result.Status, data = componentData } } };

                var json = JsonSerializer.Serialize(data);
                TraceFunction(trace, output: "PreviewResponseResult");
                return json;
            }

            if (string.Equals(source, "PreviewRequest", StringComparison.OrdinalIgnoreCase))
            {
                if (result == null) return JsonSerializer.Serialize(new { message = "Request will be available after execution." });
                object? data = null;
                if (componentData is HttpData http) data = new { runs = new[] { new { method = http.Method, url = http.Url, body = http.Body } } };
                else if (componentData is GraphQlData gql) data = new { runs = new[] { new { query = gql.Query, variables = gql.Variables } } };
                else data = new { runs = new[] { new { data = componentData } } };

                var json = JsonSerializer.Serialize(data);
                TraceFunction(trace, output: "PreviewRequestResult");
                return json;
            }

            if (componentData != null && componentData.Properties.TryGetValue(source, out var propValue))
            {
                TraceFunction(trace, output: "PropertyResult");
                return propValue;
            }

            TraceFunction(trace, output: "NullResult");
            return null;
        }

        private object? ExtractValue(object? sourceValue, string jsonPath)
        {
            if (sourceValue == null) return null;
            var sourceText = ConvertVariableToText(sourceValue);
            if (string.IsNullOrWhiteSpace(jsonPath) || string.IsNullOrWhiteSpace(sourceText)) return sourceValue;

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
            } catch { }
            return sourceValue;
        }

        private string ConvertVariableToText(object? value)
        {
            if (value == null) return string.Empty;
            if (value is JsonElement json) return json.ValueKind == JsonValueKind.String ? json.GetString() ?? string.Empty : json.GetRawText();
            return value.ToString() ?? string.Empty;
        }

        private static void TraceFunction(Action<string, TraceLevel>? trace, string inputs = "", string output = "", string? componentId = null, string? executionId = null, [CallerMemberName] string methodName = "")
        {
            // Also log to centralized Logger
            var msg = string.IsNullOrEmpty(output) 
                ? $"VariableService.{methodName}({inputs})"
                : $"VariableService.{methodName} -> {output}";
            Logger.Log(msg, LogLevel.Verbose, input: inputs, output: output, componentId: componentId, executionId: executionId);

            // Keep backward compatibility with old callback
            if (trace == null) return;
            var oldMsg = string.IsNullOrEmpty(output) 
                ? $"[VERBOSE] Test_Automation.Services.VariableService.{methodName}({inputs})"
                : $"[VERBOSE] Test_Automation.Services.VariableService.{methodName} -> {output}";
            trace(oldMsg, TraceLevel.Verbose);
        }
    }

    public static class JsonElementExtensions
    {
        public static bool TryGetPropertyByJsonPath(this JsonElement element, string jsonPath, out JsonElement value)
        {
            value = default;
            var path = jsonPath?.Trim() ?? string.Empty;
            if (path.StartsWith("$.")) path = path.Substring(2);
            else if (path.StartsWith("$[")) path = path.Substring(1);
            else if (path == "$") path = string.Empty;

            if (string.IsNullOrEmpty(path)) { value = element; return true; }

            var tokens = TokenizePath(path);
            JsonElement current = element;

            foreach (var token in tokens)
            {
                if (current.ValueKind == JsonValueKind.String)
                {
                    try { using var innerDoc = JsonDocument.Parse(current.GetString()!); current = innerDoc.RootElement.Clone(); }
                    catch { return false; }
                }

                if (token.StartsWith("[") && token.EndsWith("]"))
                {
                    var indexStr = token.Substring(1, token.Length - 2);
                    if (!int.TryParse(indexStr, out var idx)) return false;
                    if (current.ValueKind != JsonValueKind.Array) return false;
                    var arr = current.EnumerateArray().ToList();
                    if (idx < 0 || idx >= arr.Count) return false;
                    current = arr[idx];
                }
                else
                {
                    var bracketIdx = token.IndexOf('[');
                    if (bracketIdx >= 0)
                    {
                        var propName = token.Substring(0, bracketIdx);
                        var bracketPart = token.Substring(bracketIdx);
                        if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propName, out var propElement)) return false;
                        current = propElement;

                        var innerTokens = TokenizeBrackets(bracketPart);
                        foreach (var innerToken in innerTokens)
                        {
                            if (current.ValueKind == JsonValueKind.String)
                            {
                                try { using var innerDoc = JsonDocument.Parse(current.GetString()!); current = innerDoc.RootElement.Clone(); }
                                catch { return false; }
                            }
                            if (innerToken.StartsWith("[") && innerToken.EndsWith("]"))
                            {
                                var iStr = innerToken.Substring(1, innerToken.Length - 2);
                                if (!int.TryParse(iStr, out var iIdx)) return false;
                                if (current.ValueKind != JsonValueKind.Array) return false;
                                var arr = current.EnumerateArray().ToList();
                                if (iIdx < 0 || iIdx >= arr.Count) return false;
                                current = arr[iIdx];
                            } else return false;
                        }
                    }
                    else
                    {
                        if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(token, out var next)) return false;
                        current = next;
                    }
                }
            }
            value = current;
            return true;
        }

        private static List<string> TokenizePath(string path)
        {
            var tokens = new List<string>();
            var sb = new System.Text.StringBuilder();
            int depth = 0;
            foreach (var ch in path)
            {
                if (ch == '[') depth++; if (ch == ']') depth--;
                if (ch == '.' && depth == 0) { if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); } }
                else sb.Append(ch);
            }
            if (sb.Length > 0) tokens.Add(sb.ToString());
            return tokens;
        }

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
