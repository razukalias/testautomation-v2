using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using Test_Automation.Services;
using static Test_Automation.Services.Logger;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Componentes
{
    public class While : Component
    {
        public While()
        {
            Name = "While";
        }

        public override async Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            // Parse settings
            var conditionJson = Settings.TryGetValue("ConditionJson", out var json) ? json : "[]";
            var maxIterations = 1000;
            if (Settings.TryGetValue("MaxIterations", out var maxStr) && int.TryParse(maxStr, out var parsed))
                maxIterations = parsed;
            var timeoutMs = 0;
            if (Settings.TryGetValue("TimeoutMs", out var timeoutStr) && int.TryParse(timeoutStr, out var parsedTimeout))
                timeoutMs = parsedTimeout;
            var evaluationMode = Settings.TryGetValue("EvaluationMode", out var mode) ? mode : "While";
            var isDoWhile = string.Equals(evaluationMode, "DoWhile", StringComparison.OrdinalIgnoreCase);
            var indexVariable = Settings.TryGetValue("IndexVariable", out var index) ? index?.Trim() : string.Empty;

            List<ConditionRow> conditionRows;
            try
            {
                conditionRows = JsonSerializer.Deserialize<List<ConditionRow>>(conditionJson) ?? new List<ConditionRow>();
            }
            catch
            {
                conditionRows = new List<ConditionRow>();
            }

            var data = new WhileData
            {
                Id = this.Id,
                ComponentName = this.Name,
                ConditionRows = conditionRows,
                MaxIterations = maxIterations,
                TimeoutMs = timeoutMs,
                EvaluationMode = evaluationMode,
                ChildComponents = Children.Select(c => c.Id).ToList()
            };

            // Safety limits
            var iteration = 0;
            var startTime = DateTime.UtcNow;

            // Helper to evaluate condition
            async Task<bool> EvaluateConditionAsync()
            {
                if (conditionRows.Count == 0)
                    return true; // empty condition always true

                bool? overallResult = null;
                string? lastAction = null;

                foreach (var row in conditionRows)
                {
                    // Get actual value from source and JsonPath
                    var actualValue = await GetActualValueAsync(row.Source, row.Variable, context);
                    var actualText = ConvertToText(actualValue);

                    // Compare
                    bool passed;
                    string message;
                    if (string.Equals(row.Operator, "Script", StringComparison.OrdinalIgnoreCase))
                    {
                        var scriptResult = await ScriptEngine.ExecuteAsync("CSharp", row.Expected ?? string.Empty, context, actualText, (msg, level) => { });
                        passed = scriptResult.Success && scriptResult.Result is bool b && b;
                        message = scriptResult.Success ? (passed ? "Script returned true" : "Script returned false") : $"Script error: {scriptResult.Error}";
                    }
                    else
                    {
                        (passed, message) = Compare(actualValue, row.Operator, row.Expected);
                    }

                    // Determine row result based on logical operator
                    bool rowResult = passed;
                    if (row.LogicalOperator.Equals("Or", StringComparison.OrdinalIgnoreCase))
                    {
                        // OR with previous: if previous was false, this row can make overall true
                        if (overallResult.HasValue && overallResult.Value == false)
                            overallResult = rowResult;
                        else if (!overallResult.HasValue)
                            overallResult = rowResult;
                        // else overall already true, keep true
                    }
                    else // And or default
                    {
                        // AND with previous: if previous was true, keep result; else overall false
                        if (!overallResult.HasValue)
                            overallResult = rowResult;
                        else
                            overallResult = overallResult.Value && rowResult;
                    }

                    // Track action (break/continue)
                    if (!string.IsNullOrEmpty(row.Action) && !row.Action.Equals("None", StringComparison.OrdinalIgnoreCase))
                        lastAction = row.Action;

                    // Early exit if AND condition already false
                    if (row.LogicalOperator.Equals("And", StringComparison.OrdinalIgnoreCase) && overallResult.HasValue && !overallResult.Value)
                        break;
                }

                // If there's a break/continue action, we need to communicate to the loop
                // For now, we just evaluate condition; action handling will be done in loop.
                return overallResult ?? false;
            }

            // Loop execution
            while (true)
            {
                // Check timeout
                if (timeoutMs > 0 && (DateTime.UtcNow - startTime).TotalMilliseconds > timeoutMs)
                    break;

                // Check max iterations
                if (iteration >= maxIterations)
                    break;

                // Evaluate condition (pre-loop for while, post-loop for do-while)
                bool conditionMet;
                if (!isDoWhile)
                {
                    conditionMet = await EvaluateConditionAsync();
                    if (!conditionMet)
                        break;
                }

                // Execute child components (if any)
                if (!string.IsNullOrWhiteSpace(indexVariable))
                {
                    context.SetVariable(indexVariable, iteration);
                }
                foreach (var child in Children)
                {
                    // TODO: Execute child component with context
                    // This would require component execution engine; for now we skip.
                    // We'll need to integrate with ComponentExecutor.
                    // For simplicity, we'll just log.
                }

                iteration++;

                // If do-while, evaluate condition after execution
                if (isDoWhile)
                {
                    conditionMet = await EvaluateConditionAsync();
                    if (!conditionMet)
                        break;
                }
            }

            data.Properties["IterationsExecuted"] = iteration;
            return data;
        }

        private Task<object?> GetActualValueAsync(string source, string jsonPath, ExecutionContext context)
        {
            // Handle Variable sources
            if (source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
            {
                var varName = source.Substring("Variable.".Length);
                return Task.FromResult<object?>(context.GetVariable(varName));
            }
            else if (source.StartsWith("PreviewVariables.", StringComparison.OrdinalIgnoreCase))
            {
                var varName = source.Substring("PreviewVariables.".Length);
                return Task.FromResult<object?>(context.GetVariable(varName));
            }
            else if (string.Equals(source, "PreviewVariables", StringComparison.OrdinalIgnoreCase))
            {
                var variables = context.GetAllVariablesForPreview();
                return Task.FromResult<object?>(JsonSerializer.Serialize(variables));
            }
            else
            {
                // For other sources (PreviewOutput, PreviewResponse, etc.), we need component data.
                // Since we don't have component data here, we'll return null.
                // In a full implementation, we'd need to get the source value from the parent component's execution result.
                // For now, we'll assume the source is a variable placeholder.
                return Task.FromResult<object?>(null);
            }
        }

        private static string ConvertToText(object? value)
        {
            return value switch
            {
                null => string.Empty,
                string s => s,
                _ => value.ToString() ?? string.Empty
            };
        }

        private static (bool Passed, string Message) Compare(object? actual, string condition, string expected)
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
                        var regex = new Regex(expected);
                        passed = regex.IsMatch(actualText ?? string.Empty);
                        return (passed, passed ? $"Value matches regex '{expected}'." : $"Value does not match regex '{expected}'.");
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Invalid regex pattern '{expected}': {ex.Message}");
                    }
                default:
                    return (false, $"Unknown condition operator: {condition}");
            }
        }
    }
}