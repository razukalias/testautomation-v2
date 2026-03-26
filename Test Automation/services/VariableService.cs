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

            // Log variable snapshot before extraction
            LogVariableSnapshot(context, "Before extraction", trace, component.Id, context.ExecutionId);
            
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
                var extractedValue = ExtractValue(sourceValue, extractor.JsonPath, trace);

                if (extractedValue == null)
                {
                    // JSON path didn't match - only use raw source if no JsonPath specified
                    if (string.IsNullOrWhiteSpace(extractor.JsonPath) || extractor.JsonPath == "$")
                    {
                        trace($"[VERBOSE] No JsonPath specified, using raw source value", TraceLevel.Verbose);
                        context.SetVariable(extractor.VariableName, sourceValue);
                    }
                    else
                    {
                        trace($"[WARNING] JsonPath '{extractor.JsonPath}' not found in source - variable '{extractor.VariableName}' not set", TraceLevel.Warning);
                        // Don't set the variable - JsonPath didn't match
                    }
                }
                else
                {
                    trace($"[VERBOSE] Extraction successful: {TruncateForLogging(extractedValue, 200)}", TraceLevel.Verbose);
                    context.SetVariable(extractor.VariableName, extractedValue);
                }
            }
            
            // Log variable snapshot after extraction completes
            LogVariableSnapshot(context, "After extraction", trace, component.Id, context.ExecutionId);
            
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
                // Script component
                if (result?.Data is ScriptData script) return script.ExecutionResult;
                // HTTP component
                if (result?.Data is HttpData http) return http.ResponseBody;
                // GraphQL component
                if (result?.Data is GraphQlData gql) return gql.ResponseBody;
                // SQL component
                if (result?.Data is SqlData sql) return sql.QueryResult != null ? JsonSerializer.Serialize(sql.QueryResult) : string.Empty;
                // Dataset component
                if (result?.Data is DatasetData ds) return ds.Rows != null ? JsonSerializer.Serialize(ds.Rows) : string.Empty;
                // Foreach component
                if (result?.Data is ForeachData fe) return fe.CurrentItem != null ? JsonSerializer.Serialize(fe.CurrentItem) : string.Empty;
                // Loop component
                if (result?.Data is Test_Automation.Models.LoopData loop) return loop.CurrentIteration.ToString();
                // If component
                if (result?.Data is Test_Automation.Models.IfData ifData) return JsonSerializer.Serialize(new { condition = ifData.Condition, conditionMet = ifData.ConditionMet });
                // Threads component
                if (result?.Data is Test_Automation.Models.ThreadsData threads) return JsonSerializer.Serialize(new { threadCount = threads.ThreadCount, rampUpTime = threads.RampUpTime });
                // Timer component
                if (result?.Data is TimerData timer) return JsonSerializer.Serialize(new { delayMs = timer.DelayMs, executed = timer.Executed });
                // Config component
                if (result?.Data is ConfigData config) return JsonSerializer.Serialize(config.Configurations);
                // TestPlan component
                if (result?.Data is TestPlanData tp) return JsonSerializer.Serialize(new { testPlanName = tp.TestPlanName, status = tp.Status });
                // Assert component
                if (result?.Data is AssertData assert) return JsonSerializer.Serialize(new { passed = assert.Passed, errorMessage = assert.ErrorMessage, expected = assert.ExpectedValue, actual = assert.ActualValue });
                // VariableExtractor component
                if (result?.Data is VariableExtractorData varExt) return JsonSerializer.Serialize(new { variableName = varExt.VariableName, extractedValue = varExt.ExtractedValue });
                // Fallback - check Properties for result
                if (result?.Data is Test_Automation.Models.ComponentData compData && compData.Properties.TryGetValue("result", out var rawResult)) return rawResult?.ToString() ?? string.Empty;
                // Generic fallback
                return result?.Output ?? string.Empty;
            }

            // Handle UI source names (PreviewResponse, PreviewRequest, etc.)
            if (string.Equals(source, "PreviewResponse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "Body", StringComparison.OrdinalIgnoreCase))
            {
                if (result == null) return JsonSerializer.Serialize(new { message = "Response will be available after execution." });
                
                object? data = null;
                
                // HTTP component
                if (componentData is HttpData http) 
                    data = new { runs = new[] { new { responseStatus = http.ResponseStatus, responseBody = http.ResponseBody, output = http.ResponseBody } } };
                // GraphQL component
                else if (componentData is GraphQlData gql) 
                    data = new { runs = new[] { new { responseStatus = gql.ResponseStatus, responseBody = gql.ResponseBody, output = gql.ResponseBody } } };
                // SQL component
                else if (componentData is SqlData sql) 
                    data = new { runs = new[] { new { rows = sql.QueryResult, output = JsonSerializer.Serialize(sql.QueryResult) } } };
                // Dataset component
                else if (componentData is DatasetData ds) 
                    data = new { runs = new[] { new { rows = ds.Rows, output = JsonSerializer.Serialize(ds.Rows) } } };
                // Script component - get result from Properties
                else if (componentData is ScriptData script) 
                    data = new { runs = new[] { new { status = result.Status, output = script.ExecutionResult, scriptCode = script.ScriptCode, data = componentData } } };
                // Loop component - include LoopData properties directly in data for easy access
                else if (componentData is Test_Automation.Models.LoopData loop) 
                    data = new { runs = new[] { new { 
                        status = result.Status, 
                        output = JsonSerializer.Serialize(new { iterations = loop.Iterations, currentIteration = loop.CurrentIteration, childComponents = loop.ChildComponents }), 
                        data = new { iterations = loop.Iterations, currentIteration = loop.CurrentIteration, childComponents = loop.ChildComponents, id = ((ComponentData)componentData).Properties.GetValueOrDefault("id")?.ToString(), componentName = ((ComponentData)componentData).Properties.GetValueOrDefault("componentName")?.ToString() }
                    } } };
                // Foreach component
                else if (componentData is ForeachData fe) 
                    data = new { runs = new[] { new { status = result.Status, output = JsonSerializer.Serialize(new { currentIndex = fe.CurrentIndex, currentItem = fe.CurrentItem, outputVariable = fe.OutputVariable }), currentItem = fe.CurrentItem, data = componentData } } };
                // If component
                else if (componentData is Test_Automation.Models.IfData ifData) 
                    data = new { runs = new[] { new { status = result.Status, output = JsonSerializer.Serialize(new { condition = ifData.Condition, conditionMet = ifData.ConditionMet }), data = componentData } } };
                // Threads component
                else if (componentData is Test_Automation.Models.ThreadsData threads) 
                    data = new { runs = new[] { new { status = result.Status, output = JsonSerializer.Serialize(new { threadCount = threads.ThreadCount, rampUpTime = threads.RampUpTime }), data = componentData } } };
                // Timer component
                else if (componentData is TimerData timer) 
                    data = new { runs = new[] { new { status = result.Status, output = JsonSerializer.Serialize(new { delayMs = timer.DelayMs, executed = timer.Executed }), data = componentData } } };
                // Config component
                else if (componentData is ConfigData config) 
                    data = new { runs = new[] { new { status = result.Status, output = JsonSerializer.Serialize(config.Configurations), data = componentData } } };
                // TestPlan component
                else if (componentData is TestPlanData tp) 
                    data = new { runs = new[] { new { status = result.Status, output = JsonSerializer.Serialize(new { testPlanName = tp.TestPlanName, status = tp.Status }), data = componentData } } };
                // Assert component
                else if (componentData is AssertData assert) 
                    data = new { runs = new[] { new { status = result.Status, output = JsonSerializer.Serialize(new { passed = assert.Passed, errorMessage = assert.ErrorMessage, expected = assert.ExpectedValue, actual = assert.ActualValue }), data = componentData } } };
                // VariableExtractor component
                else if (componentData is VariableExtractorData varExt) 
                    data = new { runs = new[] { new { status = result.Status, output = JsonSerializer.Serialize(new { variableName = varExt.VariableName, extractedValue = varExt.ExtractedValue }), data = componentData } } };
                // Fallback - check if Properties has result
                else if (componentData is Test_Automation.Models.ComponentData compData && compData.Properties.TryGetValue("result", out var scriptResult)) 
                    data = new { runs = new[] { new { status = result.Status, output = scriptResult?.ToString() ?? string.Empty, data = componentData } } };
                // Generic fallback
                else 
                    data = new { runs = new[] { new { status = result.Status, output = result.Output ?? string.Empty, data = componentData } } };

                var json = JsonSerializer.Serialize(data);
                TraceFunction(trace, output: "PreviewResponseResult");
                return json;
            }

            if (string.Equals(source, "PreviewRequest", StringComparison.OrdinalIgnoreCase))
            {
                if (result == null) return JsonSerializer.Serialize(new { message = "Request will be available after execution." });
                object? data = null;
                if (componentData is HttpData http) data = new { runs = new[] { new { method = http.Method, url = http.Url, body = http.Body, output = http.ResponseBody } } };
                else if (componentData is GraphQlData gql) data = new { runs = new[] { new { query = gql.Query, variables = gql.Variables, output = gql.ResponseBody } } };
                else if (componentData is SqlData sql) data = new { runs = new[] { new { query = sql.Query, output = JsonSerializer.Serialize(sql.QueryResult) } } };
                else data = new { runs = new[] { new { data = componentData, output = result.Output ?? string.Empty } } };

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

        private object? ExtractValue(object? sourceValue, string jsonPath, Action<string, TraceLevel>? trace = null)
        {
            if (sourceValue == null) 
            {
                trace?.Invoke($"[VERBOSE] ExtractValue: sourceValue is null, returning null", TraceLevel.Verbose);
                return null;
            }
            var sourceText = ConvertVariableToText(sourceValue);
            if (string.IsNullOrWhiteSpace(jsonPath) || string.IsNullOrWhiteSpace(sourceText)) 
            {
                trace?.Invoke($"[VERBOSE] ExtractValue: jsonPath or sourceText empty, returning sourceValue: {TruncateForLogging(sourceValue, 100)}", TraceLevel.Verbose);
                return sourceValue;
            }

            trace?.Invoke($"[VERBOSE] ExtractValue: Source type={sourceValue.GetType().Name}, JsonPath='{jsonPath}'", TraceLevel.Verbose);
            trace?.Invoke($"[VERBOSE] ExtractValue: Source text (first 200 chars)='{TruncateForLogging(sourceText, 200)}'", TraceLevel.Verbose);

            try
            {
                using var doc = JsonDocument.Parse(sourceText);
                if (doc.RootElement.TryGetPropertyByJsonPath(jsonPath, out var element, 
                    step => trace?.Invoke($"[VERBOSE] JsonPath step: {step}", TraceLevel.Verbose)))
                {
                    trace?.Invoke($"[VERBOSE] JsonPath result: type={element.ValueKind}, value={TruncateForLogging(element.GetRawText(), 100)}", TraceLevel.Verbose);
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
                    trace?.Invoke($"[VERBOSE] JsonPath '{jsonPath}' not found in source", TraceLevel.Verbose);
                }
            }
            catch (JsonException ex)
            {
                trace?.Invoke($"[VERBOSE] JsonException parsing source: {ex.Message}", TraceLevel.Verbose);
                // Try lenient parsing for JavaScript-style objects like {a:1} or {name:'value'}
                var lenient = LenientJsonFix(sourceText);
                if (lenient != sourceText)
                {
                    trace?.Invoke($"[VERBOSE] Trying lenient JSON fix: '{TruncateForLogging(lenient, 200)}'", TraceLevel.Verbose);
                    try
                    {
                        using var doc2 = JsonDocument.Parse(lenient);
                        if (doc2.RootElement.TryGetPropertyByJsonPath(jsonPath, out var element2,
                            step => trace?.Invoke($"[VERBOSE] Lenient JsonPath step: {step}", TraceLevel.Verbose)))
                        {
                            trace?.Invoke($"[VERBOSE] Lenient JsonPath result: type={element2.ValueKind}, value={TruncateForLogging(element2.GetRawText(), 100)}", TraceLevel.Verbose);
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
                        trace?.Invoke($"[VERBOSE] Lenient parsing also failed: {ex2.Message}", TraceLevel.Verbose);
                    }
                }
            }
            trace?.Invoke($"[VERBOSE] ExtractValue: Returning null - JsonPath '{jsonPath}' not found", TraceLevel.Verbose);
            return null;
        }

        private static string TruncateForLogging(object? value, int maxLength = 200)
        {
            if (value == null) return "null";
            var text = value.ToString() ?? string.Empty;
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "...[truncated]";
        }

        private static string LenientJsonFix(string text)
        {
            var trimmed = text.Trim();
            var fixed_ = System.Text.RegularExpressions.Regex.Replace(
                trimmed, @"(?<=[{,\[])\s*([A-Za-z_]\w*)\s*(?=:)", "\"$1\"");
            fixed_ = System.Text.RegularExpressions.Regex.Replace(fixed_, @"'([^']*)'", "\"$1\"");
            return fixed_;
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

        private void LogVariableSnapshot(ExecutionContext context, string stage, Action<string, TraceLevel>? trace = null, 
            string? componentId = null, string? executionId = null)
        {
            if (trace == null && Logger.GetMinimumLevel() > LogLevel.Verbose) return;
            
            try
            {
                var variables = context.GetAllVariablesForPreview();
                var varCount = variables.Count;
                
                if (varCount == 0)
                {
                    trace?.Invoke($"[VARIABLES] {stage}: No variables in context", TraceLevel.Verbose);
                    Logger.Log($"[VARIABLES] {stage}: No variables in context", LogLevel.Verbose, 
                        componentId: componentId, executionId: executionId);
                    return;
                }
                
                var varNames = string.Join(", ", variables.Keys.Take(15).OrderBy(k => k));
                if (variables.Count > 15) varNames += "...";
                
                var sampleValues = string.Join(", ", variables
                    .OrderBy(kv => kv.Key)
                    .Take(5)
                    .Select(kv => $"{kv.Key}={TruncateForLogging(kv.Value, 50)}"));
                
                var message = $"[VARIABLES] {stage}: {varCount} variables. Names: [{varNames}]";
                trace?.Invoke(message, TraceLevel.Verbose);
                Logger.Log(message, LogLevel.Verbose, componentId: componentId, executionId: executionId);
                
                if (varCount <= 10) // Show values for small sets
                {
                    var valuesMessage = $"[VARIABLES] {stage} Values: [{sampleValues}]";
                    trace?.Invoke(valuesMessage, TraceLevel.Verbose);
                    Logger.Log(valuesMessage, LogLevel.Verbose, componentId: componentId, executionId: executionId);
                }
                else
                {
                    var samplesMessage = $"[VARIABLES] {stage} Samples (first 5): [{sampleValues}]";
                    trace?.Invoke(samplesMessage, TraceLevel.Verbose);
                    Logger.Log(samplesMessage, LogLevel.Verbose, componentId: componentId, executionId: executionId);
                }
            }
            catch (Exception ex)
            {
                trace?.Invoke($"[VARIABLES] {stage}: Error logging variables: {ex.Message}", TraceLevel.Warning);
                Logger.Log($"[VARIABLES] {stage}: Error logging variables: {ex.Message}", LogLevel.Warning, 
                    componentId: componentId, executionId: executionId);
            }
        }
    }

    public static class JsonElementExtensions
    {
        public static bool TryGetPropertyByJsonPath(this JsonElement element, string jsonPath, out JsonElement value)
        {
            return TryGetPropertyByJsonPath(element, jsonPath, out value, null);
        }

        public static bool TryGetPropertyByJsonPath(this JsonElement element, string jsonPath, out JsonElement value, Action<string>? logStep)
        {
            value = default;
            var path = jsonPath?.Trim() ?? string.Empty;
            logStep?.Invoke($"JsonPath: '{jsonPath}', normalized: '{path}'");
            
            if (path.StartsWith("$.")) path = path.Substring(2);
            else if (path.StartsWith("$[")) path = path.Substring(1);
            else if (path == "$") path = string.Empty;

            if (string.IsNullOrEmpty(path)) 
            { 
                logStep?.Invoke($"Empty path, returning root element");
                value = element; 
                return true; 
            }

            var tokens = TokenizePath(path);
            logStep?.Invoke($"Tokenized into {tokens.Count} tokens: [{string.Join(", ", tokens)}]");
            
            JsonElement current = element;

            for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
            {
                var token = tokens[tokenIndex];
                logStep?.Invoke($"Token[{tokenIndex}]: '{token}', current type: {current.ValueKind}");
                
                if (current.ValueKind == JsonValueKind.String)
                {
                    var stringValue = current.GetString();
                    if (!string.IsNullOrWhiteSpace(stringValue) && 
                        (stringValue.TrimStart().StartsWith("{") || stringValue.TrimStart().StartsWith("[")))
                    {
                        try 
                        { 
                            using var innerDoc = JsonDocument.Parse(stringValue); 
                            // Clone and serialize to avoid disposal issues
                            var rawText = innerDoc.RootElement.GetRawText();
                            using var newDoc = JsonDocument.Parse(rawText);
                            current = newDoc.RootElement.Clone();
                            logStep?.Invoke($"Parsed nested JSON string, new type: {current.ValueKind}");
                        }
                        catch (Exception ex)
                        { 
                            logStep?.Invoke($"Failed to parse nested JSON string: {ex.Message}");
                            return false; 
                        }
                    }
                }

                if (token.StartsWith("[") && token.EndsWith("]"))
                {
                    var indexStr = token.Substring(1, token.Length - 2);
                    if (!int.TryParse(indexStr, out var idx)) 
                    {
                        logStep?.Invoke($"Invalid array index: '{indexStr}'");
                        return false;
                    }
                    if (current.ValueKind != JsonValueKind.Array) 
                    {
                        logStep?.Invoke($"Expected array for index [{idx}], but got {current.ValueKind}");
                        return false;
                    }
                    var arr = current.EnumerateArray().ToList();
                    if (idx < 0 || idx >= arr.Count) 
                    {
                        logStep?.Invoke($"Array index [{idx}] out of bounds, array has {arr.Count} elements");
                        return false;
                    }
                    current = arr[idx];
                    logStep?.Invoke($"Array access [{idx}] successful, new type: {current.ValueKind}");
                }
                else
                {
                    var bracketIdx = token.IndexOf('[');
                    if (bracketIdx >= 0)
                    {
                        var propName = token.Substring(0, bracketIdx);
                        var bracketPart = token.Substring(bracketIdx);
                        logStep?.Invoke($"Property with array accessor: '{propName}' with brackets '{bracketPart}'");
                        
                        if (current.ValueKind != JsonValueKind.Object) 
                        {
                            logStep?.Invoke($"Expected object for property '{propName}', but got {current.ValueKind}");
                            return false;
                        }
                        if (!current.TryGetProperty(propName, out var propElement)) 
                        {
                            logStep?.Invoke($"Property '{propName}' not found in object");
                            return false;
                        }
                        current = propElement;
                        logStep?.Invoke($"Property '{propName}' found, type: {current.ValueKind}");

                        var innerTokens = TokenizeBrackets(bracketPart);
                        logStep?.Invoke($"Processing {innerTokens.Count} bracket tokens: [{string.Join(", ", innerTokens)}]");
                        
                        foreach (var innerToken in innerTokens)
                        {
                            if (current.ValueKind == JsonValueKind.String)
                            {
                                var stringValue = current.GetString();
                                if (!string.IsNullOrWhiteSpace(stringValue) && 
                                    (stringValue.TrimStart().StartsWith("{") || stringValue.TrimStart().StartsWith("[")))
                                {
                                    try 
                                    { 
                                        using var innerDoc = JsonDocument.Parse(stringValue); 
                                        var rawText = innerDoc.RootElement.GetRawText();
                                        using var newDoc = JsonDocument.Parse(rawText);
                                        current = newDoc.RootElement.Clone();
                                        logStep?.Invoke($"Parsed nested JSON string in brackets, new type: {current.ValueKind}");
                                    }
                                    catch (Exception ex)
                                    { 
                                        logStep?.Invoke($"Failed to parse nested JSON in brackets: {ex.Message}");
                                        return false; 
                                    }
                                }
                            }
                            if (innerToken.StartsWith("[") && innerToken.EndsWith("]"))
                            {
                                var iStr = innerToken.Substring(1, innerToken.Length - 2);
                                if (!int.TryParse(iStr, out var iIdx)) 
                                {
                                    logStep?.Invoke($"Invalid array index in brackets: '{iStr}'");
                                    return false;
                                }
                                if (current.ValueKind != JsonValueKind.Array) 
                                {
                                    logStep?.Invoke($"Expected array for index [{iIdx}] in brackets, but got {current.ValueKind}");
                                    return false;
                                }
                                var arr = current.EnumerateArray().ToList();
                                if (iIdx < 0 || iIdx >= arr.Count) 
                                {
                                    logStep?.Invoke($"Array index [{iIdx}] in brackets out of bounds, array has {arr.Count} elements");
                                    return false;
                                }
                                current = arr[iIdx];
                                logStep?.Invoke($"Bracket array access [{iIdx}] successful, new type: {current.ValueKind}");
                            } else 
                            {
                                logStep?.Invoke($"Invalid bracket token: '{innerToken}'");
                                return false;
                            }
                        }
                    }
                    else
                    {
                        if (current.ValueKind != JsonValueKind.Object) 
                        {
                            logStep?.Invoke($"Expected object for property '{token}', but got {current.ValueKind}");
                            return false;
                        }
                        if (!current.TryGetProperty(token, out var next)) 
                        {
                            logStep?.Invoke($"Property '{token}' not found in object");
                            return false;
                        }
                        current = next;
                        logStep?.Invoke($"Property '{token}' found, type: {current.ValueKind}");
                    }
                }
            }
            
            logStep?.Invoke($"JsonPath navigation completed successfully, final type: {current.ValueKind}");
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
