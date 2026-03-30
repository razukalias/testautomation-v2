using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Base64 : Component
    {
        public Base64()
        {
            Name = "Base64";
        }

        public override async Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var data = new Base64Data
            {
                Id = this.Id,
                ComponentName = this.Name
            };

            try
            {
                // Get settings
                data.Input = GetSettingValue("Input", string.Empty);
                data.Operation = GetSettingValue("Operation", "Encode");
                data.DataType = GetSettingValue("DataType", "Text");
                data.FilePath = GetSettingValue("FilePath", string.Empty);
                data.Encoding = GetSettingValue("Encoding", "UTF-8");
                data.OutputVariable = GetSettingValue("OutputVariable", string.Empty);

                var encoding = GetEncoding(data.Encoding);

                // Determine input data (bytes)
                byte[] inputBytes;
                bool inputFromFilePath = !string.IsNullOrWhiteSpace(data.FilePath);

                if (inputFromFilePath)
                {
                    if (!File.Exists(data.FilePath))
                    {
                        throw new FileNotFoundException($"File not found: {data.FilePath}");
                    }

                    if (string.Equals(data.DataType, "Binary", StringComparison.OrdinalIgnoreCase))
                    {
                        // Read file as binary bytes
                        inputBytes = await File.ReadAllBytesAsync(data.FilePath);
                    }
                    else
                    {
                        // Read file as text and convert to bytes using encoding
                        var text = await File.ReadAllTextAsync(data.FilePath, encoding);
                        inputBytes = encoding.GetBytes(text);
                    }
                }
                else
                {
                    // Use Input string
                    if (string.Equals(data.DataType, "Binary", StringComparison.OrdinalIgnoreCase))
                    {
                        // Input is a string representation of byte array like "[10,100,200]"
                        inputBytes = ParseByteArrayString(data.Input);
                    }
                    else
                    {
                        // Input is plain text
                        inputBytes = encoding.GetBytes(data.Input);
                    }
                }

                // Perform operation
                if (string.Equals(data.Operation, "Encode", StringComparison.OrdinalIgnoreCase))
                {
                    // Encode bytes to base64
                    data.Output = Convert.ToBase64String(inputBytes);
                }
                else if (string.Equals(data.Operation, "Decode", StringComparison.OrdinalIgnoreCase))
                {
                    // Decode base64 to bytes
                    var decodedBytes = Convert.FromBase64String(data.Input);
                    
                    if (string.Equals(data.DataType, "Binary", StringComparison.OrdinalIgnoreCase))
                    {
                        // Output as byte array string representation
                        data.Output = ByteArrayToString(decodedBytes);
                    }
                    else
                    {
                        // Output as text
                        data.Output = encoding.GetString(decodedBytes);
                    }
                }
                else
                {
                    throw new ArgumentException($"Unknown operation: {data.Operation}");
                }

                data.Success = true;

                // Populate properties for assertions
                data.Properties["input"] = data.Input;
                data.Properties["output"] = data.Output;
                data.Properties["operation"] = data.Operation;
                data.Properties["dataType"] = data.DataType;
                data.Properties["success"] = data.Success;

                // Store result in variable if specified
                if (!string.IsNullOrEmpty(data.OutputVariable))
                {
                    context.SetVariable(data.OutputVariable, data.Output);
                }
            }
            catch (Exception ex)
            {
                data.ErrorMessage = ex.Message;
                data.Success = false;
            }

            return data;
        }

        private Encoding GetEncoding(string encodingName)
        {
            return encodingName.ToLower() switch
            {
                "utf-8" => Encoding.UTF8,
                "ascii" => Encoding.ASCII,
                "utf-16" or "unicode" => Encoding.Unicode,
                "latin1" or "iso-8859-1" => Encoding.Latin1,
                _ => Encoding.UTF8
            };
        }

        private string GetSettingValue(string key, string fallback)
        {
            return Settings.TryGetValue(key, out var value) ? value : fallback;
        }

        private byte[] ParseByteArrayString(string input)
        {
            // Remove whitespace and brackets
            var trimmed = input.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return Array.Empty<byte>();
            }

            // Split by commas
            var parts = trimmed.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (byte.TryParse(parts[i].Trim(), out byte b))
                {
                    bytes[i] = b;
                }
                else
                {
                    throw new FormatException($"Invalid byte value: {parts[i]}");
                }
            }
            return bytes;
        }

        private string ByteArrayToString(byte[] bytes)
        {
            return "[" + string.Join(",", bytes) + "]";
        }
    }
}
