using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;
using Test_Automation.Services;

namespace Test_Automation.Componentes
{
    public class VariableExtractor : Component
    {
        public VariableExtractor()
        {
            Name = "VariableExtractor";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var pattern = Settings.TryGetValue("Pattern", out var configuredPattern)
                ? configuredPattern ?? string.Empty
                : string.Empty;
            var variableName = Settings.TryGetValue("VariableName", out var configuredVariableName)
                ? configuredVariableName ?? string.Empty
                : string.Empty;
            var jsonPath = Settings.TryGetValue("JsonPath", out var configuredJsonPath)
                ? configuredJsonPath ?? string.Empty
                : string.Empty;

            var data = new VariableExtractorData
            {
                Id = Id,
                ComponentName = Name,
                Pattern = pattern,
                VariableName = variableName,
                JsonPath = jsonPath
            };

            if (string.IsNullOrWhiteSpace(variableName))
            {
                data.Properties["warning"] = "VariableName is empty; extraction skipped.";
                return Task.FromResult<ComponentData>(data);
            }

            var extracted = ResolvePatternValue(pattern, context);
            
            // If JsonPath is specified, try to extract value from JSON
            if (!string.IsNullOrWhiteSpace(jsonPath) && !string.IsNullOrWhiteSpace(extracted))
            {
                try
                {
                    using var doc = JsonDocument.Parse(extracted);
                    if (doc.RootElement.TryGetPropertyByJsonPath(jsonPath, out var element))
                    {
                        extracted = ConvertVariableToText(element);
                    }
                }
                catch
                {
                    // If JSON parsing fails, keep the original extracted value
                }
            }

            data.ExtractedValue = extracted;
            data.Properties["resolvedValue"] = extracted;
            context.SetVariable(variableName.Trim(), extracted);
            return Task.FromResult<ComponentData>(data);
        }

        private static string ResolvePatternValue(string pattern, Test_Automation.Models.ExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return string.Empty;
            }

            var trimmed = pattern.Trim();
            if (context.HasVariable(trimmed))
            {
                return ConvertVariableToText(context.GetVariable(trimmed));
            }

            return Regex.Replace(pattern, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value.Trim();
                if (!context.HasVariable(key))
                {
                    // Return empty string instead of literal ${key} when variable doesn't exist
                    return string.Empty;
                }

                return ConvertVariableToText(context.GetVariable(key));
            });
        }

        private static string ConvertVariableToText(object? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is JsonElement json)
            {
                return json.ValueKind == JsonValueKind.String
                    ? json.GetString() ?? string.Empty
                    : json.GetRawText();
            }

            return value.ToString() ?? string.Empty;
        }
    }
}
