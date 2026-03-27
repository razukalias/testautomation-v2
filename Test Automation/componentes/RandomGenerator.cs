using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class RandomGenerator : Component
    {
        private static readonly Random _random = new Random();

        private static readonly string[] LoremWords = {
            "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
            "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore",
            "magna", "aliqua", "enim", "ad", "minim", "veniam", "quis", "nostrud",
            "exercitation", "ullamco", "laboris", "nisi", "aliquip", "ex", "ea", "commodo",
            "consequat", "duis", "aute", "irure", "in", "reprehenderit", "voluptate",
            "velit", "esse", "cillum", "fugiat", "nulla", "pariatur", "excepteur", "sint",
            "occaecat", "cupidatat", "non", "proident", "sunt", "culpa", "qui", "officia",
            "deserunt", "mollit", "anim", "id", "est", "laborum"
        };

        private static readonly string[] FirstNames = {
            "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda",
            "William", "Elizabeth", "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica",
            "Thomas", "Sarah", "Charles", "Karen", "Emma", "Oliver", "Ava", "Liam", "Sophia"
        };

        private static readonly string[] LastNames = {
            "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
            "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson",
            "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson"
        };

        private static readonly string[] Domains = {
            "gmail.com", "yahoo.com", "outlook.com", "hotmail.com", "aol.com",
            "icloud.com", "protonmail.com", "mail.com", "zoho.com"
        };

        public RandomGenerator()
        {
            Name = "RandomGenerator";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var outputType = GetSetting("OutputType", "number");
            var minStr = GetSetting("Min", "0");
            var maxStr = GetSetting("Max", "100");
            var lengthStr = GetSetting("Length", "10");
            var decimalPlacesStr = GetSetting("DecimalPlaces", "2");
            var format = GetSetting("Format", "");
            var charset = GetSetting("Charset", "alphanumeric");
            var includeUpper = GetSettingBool("IncludeUpper", true);
            var includeLower = GetSettingBool("IncludeLower", true);
            var includeNumbers = GetSettingBool("IncludeNumbers", true);
            var includeSpecial = GetSettingBool("IncludeSpecial", false);
            var jsonStructure = GetSetting("JsonStructure", "");
            var arrayLengthStr = GetSetting("ArrayLength", "5");
            var itemType = GetSetting("ItemType", "string");
            var emailDomain = GetSetting("EmailDomain", "");
            var variableName = GetSetting("VariableName", "");

            double.TryParse(minStr, out double min);
            double.TryParse(maxStr, out double max);
            int.TryParse(lengthStr, out int length);
            int.TryParse(decimalPlacesStr, out int decimalPlaces);
            int.TryParse(arrayLengthStr, out int arrayLength);

            if (max <= min) max = min + 1;
            if (length <= 0) length = 10;
            if (decimalPlaces < 0) decimalPlaces = 2;
            if (arrayLength <= 0) arrayLength = 5;

            string generatedValue = string.Empty;
            double generatedNumber = 0;
            string generatedId = string.Empty;

            switch (outputType.ToLower())
            {
                // Number types
                case "integer":
                case "int":
                    generatedNumber = _random.Next((int)min, (int)max);
                    generatedValue = ((int)generatedNumber).ToString();
                    break;

                case "number":
                case "double":
                    generatedNumber = _random.NextDouble() * (max - min) + min;
                    generatedValue = generatedNumber.ToString("F" + decimalPlaces);
                    break;

                case "float":
                    generatedNumber = (float)(_random.NextDouble() * (max - min) + min);
                    generatedValue = generatedNumber.ToString("F" + decimalPlaces);
                    break;

                case "decimal":
                    var decimalValue = (decimal)(_random.NextDouble() * (max - min) + min);
                    generatedValue = decimalValue.ToString("F" + decimalPlaces);
                    break;

                case "long":
                case "bigint":
                    long longMin = (long)min;
                    long longMax = (long)max;
                    long longResult = longMin + (long)(_random.NextDouble() * (longMax - longMin));
                    generatedNumber = longResult;
                    generatedValue = longResult.ToString();
                    break;

                // GUID types
                case "guid":
                    generatedId = Guid.NewGuid().ToString();
                    generatedValue = generatedId;
                    break;

                case "guid-n":
                case "guidn":
                    generatedId = Guid.NewGuid().ToString("N");
                    generatedValue = generatedId;
                    break;

                case "guid-d":
                case "guidd":
                    generatedId = Guid.NewGuid().ToString("D");
                    generatedValue = generatedId;
                    break;

                case "guid-b":
                case "guidb":
                    generatedId = Guid.NewGuid().ToString("B");
                    generatedValue = generatedId;
                    break;

                case "guid-p":
                case "guidp":
                    generatedId = Guid.NewGuid().ToString("P");
                    generatedValue = generatedId;
                    break;

                case "guid-x":
                case "guidx":
                    generatedId = Guid.NewGuid().ToString("X");
                    generatedValue = generatedId;
                    break;

                case "uuid":
                    generatedId = Guid.NewGuid().ToString("N").Substring(0, Math.Min(length, 32));
                    generatedValue = generatedId;
                    break;

                // String types
                case "string":
                case "text":
                    generatedValue = GenerateString(length, charset, includeUpper, includeLower, includeNumbers, includeSpecial);
                    break;

                case "utf8":
                case "utf-8":
                    generatedValue = GenerateUtf8String(length);
                    break;

                case "ascii":
                    generatedValue = GenerateAsciiString(length);
                    break;

                case "hex":
                    generatedValue = GenerateHexString(length);
                    break;

                case "base64":
                    generatedValue = GenerateBase64String(length);
                    break;

                case "alphanumeric":
                    generatedValue = GenerateAlphanumeric(length);
                    break;

                case "alpha":
                case "letters":
                    generatedValue = GenerateAlphaString(length, includeUpper, includeLower);
                    break;

                case "numeric":
                case "digits":
                    generatedValue = GenerateNumericString(length);
                    break;

                case "symbol":
                case "special":
                    generatedValue = GenerateSpecialChars(length);
                    break;

                case "lorem":
                case "loremipsum":
                    generatedValue = GenerateLoremIpsum(length);
                    break;

                case "word":
                case "words":
                    generatedValue = GenerateWords(length);
                    break;

                case "sentence":
                    generatedValue = GenerateSentence(length);
                    break;

                case "paragraph":
                    generatedValue = GenerateParagraph(length);
                    break;

                // Person data
                case "firstname":
                    generatedValue = FirstNames[_random.Next(FirstNames.Length)];
                    break;

                case "lastname":
                    generatedValue = LastNames[_random.Next(LastNames.Length)];
                    break;

                case "fullname":
                    generatedValue = $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}";
                    break;

                case "email":
                    var firstName = FirstNames[_random.Next(FirstNames.Length)].ToLower();
                    var lastName = LastNames[_random.Next(LastNames.Length)].ToLower();
                    var domain = !string.IsNullOrWhiteSpace(emailDomain) ? emailDomain : Domains[_random.Next(Domains.Length)];
                    var num = _random.Next(1, 999);
                    generatedValue = $"{firstName}.{lastName}{num}@{domain}";
                    break;

                case "username":
                    var fn = FirstNames[_random.Next(FirstNames.Length)].ToLower();
                    var ln = LastNames[_random.Next(LastNames.Length)].ToLower();
                    generatedValue = $"{fn}{ln}{_random.Next(1, 9999)}";
                    break;

                // Date/Time
                case "datetime":
                case "date":
                    generatedValue = GenerateRandomDate();
                    break;

                case "time":
                    generatedValue = GenerateRandomTime();
                    break;

                case "timestamp":
                case "unix":
                    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var now = DateTime.UtcNow;
                    long unixTime = (long)(now - epoch).TotalSeconds + _random.Next(-31536000, 31536000);
                    generatedValue = unixTime.ToString();
                    break;

                // Network
                case "ip":
                case "ipv4":
                    generatedValue = $"{_random.Next(1, 255)}.{_random.Next(0, 255)}.{_random.Next(0, 255)}.{_random.Next(1, 254)}";
                    break;

                case "ipv6":
                    generatedValue = GenerateIPv6();
                    break;

                case "mac":
                    generatedValue = GenerateMacAddress();
                    break;

                case "url":
                    generatedValue = GenerateUrl();
                    break;

                case "hostname":
                    generatedValue = GenerateHostname();
                    break;

                // Phone
                case "phone":
                case "phonenumber":
                    generatedValue = $"+{_random.Next(1, 99)}{_random.Next(100000000, 999999999)}";
                    break;

                // Address
                case "zipcode":
                case "postalcode":
                    generatedValue = _random.Next(10000, 99999).ToString();
                    break;

                // Boolean
                case "bool":
                case "boolean":
                    generatedValue = _random.Next(2) == 1 ? "true" : "false";
                    break;

                // Color
                case "color":
                case "hexcolor":
                    generatedValue = $"#{_random.Next(0x1000000):X6}";
                    break;

                case "rgbcolor":
                    generatedValue = $"rgb({_random.Next(256)}, {_random.Next(256)}, {_random.Next(256)})";
                    break;

                // Complex types
                case "json":
                case "jsonobject":
                    generatedValue = GenerateJson(jsonStructure);
                    break;

                case "array":
                case "list":
                    generatedValue = GenerateArray(arrayLength, itemType, jsonStructure);
                    break;

                case "object":
                    generatedValue = GenerateObject(jsonStructure);
                    break;

                // Case conversion
                case "uppercase":
                    generatedValue = GenerateString(length, charset, true, false, includeNumbers, includeSpecial).ToUpper();
                    break;

                case "lowercase":
                    generatedValue = GenerateString(length, charset, false, true, includeNumbers, includeSpecial).ToLower();
                    break;

                case "camelcase":
                    generatedValue = GenerateCamelCase(length);
                    break;

                case "pascalcase":
                    generatedValue = GeneratePascalCase(length);
                    break;

                case "snakecase":
                    generatedValue = GenerateSnakeCase(length);
                    break;

                case "kebabcase":
                    generatedValue = GenerateKebabCase(length);
                    break;

                default:
                    generatedNumber = _random.NextDouble() * (max - min) + min;
                    generatedValue = generatedNumber.ToString("F" + decimalPlaces);
                    break;
            }

            var data = new RandomGeneratorData
            {
                Id = Id,
                ComponentName = Name,
                GeneratedValue = generatedValue,
                GeneratedNumber = generatedNumber,
                GeneratedId = generatedId,
                OutputType = outputType
            };

            data.Properties["result"] = generatedValue;
            data.Properties["outputType"] = outputType;
            data.Properties["length"] = length;

            if (!string.IsNullOrWhiteSpace(variableName))
            {
                context.SetVariable(variableName.Trim(), generatedValue);
            }

            return Task.FromResult<ComponentData>(data);
        }

        private string GetSetting(string key, string defaultValue)
        {
            return Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : defaultValue;
        }

        private bool GetSettingBool(string key, bool defaultValue)
        {
            if (Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return bool.TryParse(value, out var result) && result;
            }
            return defaultValue;
        }

        private string GenerateString(int length, string charset, bool includeUpper, bool includeLower, bool includeNumbers, bool includeSpecial)
        {
            var chars = new StringBuilder();
            if (includeLower) chars.Append("abcdefghijklmnopqrstuvwxyz");
            if (includeUpper) chars.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            if (includeNumbers) chars.Append("0123456789");
            if (includeSpecial) chars.Append("!@#$%^&*()_+-=[]{}|;:,.<>?");

            if (chars.Length == 0) chars.Append("abcdefghijklmnopqrstuvwxyz0123456789");

            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[_random.Next(chars.Length)];
            }
            return new string(result);
        }

        private string GenerateUtf8String(int length)
        {
            var result = new StringBuilder();
            // Mix of ASCII printable and extended UTF-8 characters
            for (int i = 0; i < length; i++)
            {
                int category = _random.Next(5);
                switch (category)
                {
                    case 0: // Basic Latin
                        result.Append((char)_random.Next(0x21, 0x7F));
                        break;
                    case 1: // Latin Extended
                        result.Append((char)_random.Next(0xC0, 0x250));
                        break;
                    case 2: // Cyrillic
                        result.Append((char)_random.Next(0x410, 0x4FF));
                        break;
                    case 3: // Greek
                        result.Append((char)_random.Next(0x391, 0x3C9));
                        break;
                    case 4: // Currency and symbols
                        result.Append((char)_random.Next(0x20A0, 0x20CF));
                        break;
                }
            }
            return result.ToString();
        }

        private string GenerateAsciiString(int length)
        {
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = (char)_random.Next(0x21, 0x7E);
            }
            return new string(result);
        }

        private string GenerateHexString(int length)
        {
            var result = new char[length];
            const string hexChars = "0123456789ABCDEF";
            for (int i = 0; i < length; i++)
            {
                result[i] = hexChars[_random.Next(hexChars.Length)];
            }
            return new string(result);
        }

        private string GenerateBase64String(int length)
        {
            var bytes = new byte[length];
            _random.NextBytes(bytes);
            return Convert.ToBase64String(bytes).Substring(0, length);
        }

        private string GenerateAlphanumeric(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[_random.Next(chars.Length)];
            }
            return new string(result);
        }

        private string GenerateAlphaString(int length, bool includeUpper, bool includeLower)
        {
            var chars = new StringBuilder();
            if (includeLower) chars.Append("abcdefghijklmnopqrstuvwxyz");
            if (includeUpper) chars.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            if (chars.Length == 0) chars.Append("abcdefghijklmnopqrstuvwxyz");

            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[_random.Next(chars.Length)];
            }
            return new string(result);
        }

        private string GenerateNumericString(int length)
        {
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = (char)('0' + _random.Next(10));
            }
            return new string(result);
        }

        private string GenerateSpecialChars(int length)
        {
            const string specials = "!@#$%^&*()_+-=[]{}|;:,.<>?~`";
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = specials[_random.Next(specials.Length)];
            }
            return new string(result);
        }

        private string GenerateLoremIpsum(int wordCount)
        {
            var words = new List<string>();
            for (int i = 0; i < wordCount; i++)
            {
                words.Add(LoremWords[_random.Next(LoremWords.Length)]);
            }
            if (words.Count > 0) words[0] = char.ToUpper(words[0][0]) + words[0].Substring(1);
            return string.Join(" ", words) + ".";
        }

        private string GenerateWords(int wordCount)
        {
            var words = new List<string>();
            for (int i = 0; i < wordCount; i++)
            {
                words.Add(LoremWords[_random.Next(LoremWords.Length)]);
            }
            if (words.Count > 0) words[0] = char.ToUpper(words[0][0]) + words[0].Substring(1);
            return string.Join(" ", words);
        }

        private string GenerateSentence(int wordCount)
        {
            var words = new List<string>();
            for (int i = 0; i < wordCount; i++)
            {
                words.Add(LoremWords[_random.Next(LoremWords.Length)]);
            }
            if (words.Count > 0) words[0] = char.ToUpper(words[0][0]) + words[0].Substring(1);
            return string.Join(" ", words) + ".";
        }

        private string GenerateParagraph(int sentenceCount)
        {
            var sentences = new List<string>();
            for (int i = 0; i < sentenceCount; i++)
            {
                int words = _random.Next(5, 15);
                sentences.Add(GenerateSentence(words));
            }
            return string.Join(" ", sentences);
        }

        private string GenerateRandomDate()
        {
            var start = new DateTime(2020, 1, 1);
            int range = (DateTime.Today - start).Days;
            var date = start.AddDays(_random.Next(range));
            return date.ToString("yyyy-MM-dd");
        }

        private string GenerateRandomTime()
        {
            return $"{_random.Next(24):D2}:{_random.Next(60):D2}:{_random.Next(60):D2}";
        }

        private string GenerateIPv6()
        {
            var parts = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                parts.Add(_random.Next(0x10000).ToString("X4"));
            }
            return string.Join(":", parts);
        }

        private string GenerateMacAddress()
        {
            var parts = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                parts.Add(_random.Next(256).ToString("X2"));
            }
            return string.Join(":", parts);
        }

        private string GenerateUrl()
        {
            string[] schemes = { "http", "https" };
            string[] tlds = { "com", "org", "net", "io", "dev", "app" };
            string scheme = schemes[_random.Next(schemes.Length)];
            string domain = GenerateAlphaString(8, false, true);
            string tld = tlds[_random.Next(tlds.Length)];
            return $"{scheme}://{domain}.{tld}";
        }

        private string GenerateHostname()
        {
            string[] tlds = { "com", "org", "net", "io", "dev", "app", "local" };
            return $"{GenerateAlphaString(6, false, true)}.{tlds[_random.Next(tlds.Length)]}";
        }

        private string GenerateCamelCase(int wordCount)
        {
            var words = new List<string>();
            for (int i = 0; i < wordCount; i++)
            {
                string word = LoremWords[_random.Next(LoremWords.Length)];
                words.Add(i == 0 ? word.ToLower() : char.ToUpper(word[0]) + word.Substring(1));
            }
            return string.Join("", words);
        }

        private string GeneratePascalCase(int wordCount)
        {
            var words = new List<string>();
            for (int i = 0; i < wordCount; i++)
            {
                string word = LoremWords[_random.Next(LoremWords.Length)];
                words.Add(char.ToUpper(word[0]) + word.Substring(1));
            }
            return string.Join("", words);
        }

        private string GenerateSnakeCase(int wordCount)
        {
            var words = new List<string>();
            for (int i = 0; i < wordCount; i++)
            {
                words.Add(LoremWords[_random.Next(LoremWords.Length)].ToLower());
            }
            return string.Join("_", words);
        }

        private string GenerateKebabCase(int wordCount)
        {
            var words = new List<string>();
            for (int i = 0; i < wordCount; i++)
            {
                words.Add(LoremWords[_random.Next(LoremWords.Length)].ToLower());
            }
            return string.Join("-", words);
        }

        private string GenerateJson(string structure)
        {
            if (string.IsNullOrWhiteSpace(structure))
            {
                // Default simple JSON
                var obj = new Dictionary<string, object>
                {
                    ["id"] = Guid.NewGuid().ToString(),
                    ["name"] = FirstNames[_random.Next(FirstNames.Length)] + " " + LastNames[_random.Next(LastNames.Length)],
                    ["email"] = $"{FirstNames[_random.Next(FirstNames.Length)].ToLower()}@gmail.com",
                    ["age"] = _random.Next(18, 80),
                    ["active"] = _random.Next(2) == 1,
                    ["score"] = Math.Round(_random.NextDouble() * 100, 2)
                };
                return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
            }

            // Parse custom structure
            try
            {
                using var doc = JsonDocument.Parse(structure);
                return GenerateJsonFromTemplate(doc.RootElement);
            }
            catch
            {
                return structure;
            }
        }

        private string GenerateJsonFromTemplate(JsonElement template)
        {
            var result = new Dictionary<string, object>();

            foreach (var prop in template.EnumerateObject())
            {
                result[prop.Name] = GenerateJsonValue(prop.Value);
            }

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }

        private object GenerateJsonValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                string typeHint = element.GetString() ?? "";
                return GenerateFromTypeHint(typeHint);
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                // Check if this is an array definition with __isArray marker
                if (element.TryGetProperty("__isArray", out var isArrayProp) && isArrayProp.GetBoolean())
                {
                    return GenerateArrayFromDefinition(element);
                }

                // Regular object - generate each property
                var obj = new Dictionary<string, object>();
                foreach (var prop in element.EnumerateObject())
                {
                    // Skip internal properties
                    if (prop.Name.StartsWith("__")) continue;
                    obj[prop.Name] = GenerateJsonValue(prop.Value);
                }
                return obj;
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var arr = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    arr.Add(GenerateJsonValue(item));
                }
                return arr;
            }

            return element.ToString();
        }

        private object GenerateFromTypeHint(string typeHint)
        {
            if (string.IsNullOrWhiteSpace(typeHint)) return "";
            
            // Handle __type: prefix format
            if (typeHint.StartsWith("__type:", StringComparison.OrdinalIgnoreCase))
            {
                typeHint = typeHint.Substring(7);
            }

            return typeHint.ToLower() switch
            {
                "string" => GenerateString(10, "alphanumeric", true, true, true, false),
                "text" => GenerateLoremIpsum(5),
                "integer" or "int" => _random.Next(1, 1000),
                "number" or "double" => Math.Round(_random.NextDouble() * 1000, 2),
                "float" => (float)Math.Round(_random.NextDouble() * 1000, 2),
                "decimal" => (decimal)Math.Round(_random.NextDouble() * 1000, 2),
                "long" or "bigint" => _random.Next(1, 100000),
                "guid" or "uuid" => Guid.NewGuid().ToString(),
                "boolean" or "bool" => _random.Next(2) == 1,
                "date" => GenerateRandomDate(),
                "datetime" => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                "time" => GenerateRandomTime(),
                "email" => $"{FirstNames[_random.Next(FirstNames.Length)].ToLower()}.{LastNames[_random.Next(LastNames.Length)].ToLower()}@gmail.com",
                "firstname" => FirstNames[_random.Next(FirstNames.Length)],
                "lastname" => LastNames[_random.Next(LastNames.Length)],
                "fullname" => $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}",
                "phone" => $"+1{_random.Next(200, 999)}{_random.Next(200, 999)}{_random.Next(1000, 9999)}",
                "ip" or "ipv4" => $"{_random.Next(1, 255)}.{_random.Next(0, 255)}.{_random.Next(0, 255)}.{_random.Next(1, 254)}",
                "url" => GenerateUrl(),
                "color" or "hexcolor" => $"#{_random.Next(0x1000000):X6}",
                "zipcode" or "postalcode" => _random.Next(10000, 99999).ToString(),
                "username" => $"{FirstNames[_random.Next(FirstNames.Length)].ToLower()}{_random.Next(1, 999)}",
                _ => typeHint
            };
        }

        private List<object> GenerateArrayFromDefinition(JsonElement arrayDef)
        {
            var result = new List<object>();
            
            // Get array length
            int length = 3; // default
            if (arrayDef.TryGetProperty("__length", out var lengthProp))
            {
                if (lengthProp.ValueKind == JsonValueKind.Number)
                {
                    length = lengthProp.GetInt32();
                }
                else if (lengthProp.ValueKind == JsonValueKind.String)
                {
                    int.TryParse(lengthProp.GetString(), out length);
                }
            }
            if (length <= 0) length = 3;

            // Get item type
            string itemType = "string";
            if (arrayDef.TryGetProperty("__itemType", out var itemTypeProp))
            {
                itemType = itemTypeProp.GetString() ?? "string";
            }

            // Check if items have a template
            JsonElement? itemTemplate = null;
            if (arrayDef.TryGetProperty("__items", out var itemsProp))
            {
                itemTemplate = itemsProp;
            }

            // Generate array items
            for (int i = 0; i < length; i++)
            {
                if (itemTemplate.HasValue)
                {
                    if (itemTemplate.Value.ValueKind == JsonValueKind.String)
                    {
                        // Items are simple type
                        result.Add(GenerateFromTypeHint(itemTemplate.Value.GetString() ?? "string"));
                    }
                    else if (itemTemplate.Value.ValueKind == JsonValueKind.Object)
                    {
                        // Items are objects with properties
                        var itemObj = new Dictionary<string, object>();
                        foreach (var prop in itemTemplate.Value.EnumerateObject())
                        {
                            if (prop.Name.StartsWith("__")) continue;
                            itemObj[prop.Name] = GenerateJsonValue(prop.Value);
                        }
                        result.Add(itemObj);
                    }
                    else
                    {
                        result.Add(GenerateFromTypeHint(itemType));
                    }
                }
                else
                {
                    result.Add(GenerateFromTypeHint(itemType));
                }
            }

            return result;
        }

        private string GenerateArray(int length, string itemType, string structure)
        {
            var items = new List<object>();

            for (int i = 0; i < length; i++)
            {
                items.Add(GenerateArrayItem(itemType, structure));
            }

            return JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        }

        private object GenerateArrayItem(string itemType, string structure)
        {
            return itemType.ToLower() switch
            {
                "string" => GenerateString(10, "alphanumeric", true, true, true, false),
                "integer" or "int" => _random.Next(1, 1000),
                "number" or "double" => Math.Round(_random.NextDouble() * 1000, 2),
                "float" => (float)Math.Round(_random.NextDouble() * 1000, 2),
                "decimal" => (decimal)Math.Round(_random.NextDouble() * 1000, 2),
                "guid" or "uuid" => Guid.NewGuid().ToString(),
                "boolean" or "bool" => _random.Next(2) == 1,
                "date" => GenerateRandomDate(),
                "email" => $"{FirstNames[_random.Next(FirstNames.Length)].ToLower()}@gmail.com",
                "name" => $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}",
                "json" or "object" => JsonSerializer.Deserialize<Dictionary<string, object>>(GenerateJson(structure)),
                _ => GenerateString(8, "alphanumeric", true, true, true, false)
            };
        }

        private string GenerateObject(string structure)
        {
            if (string.IsNullOrWhiteSpace(structure))
            {
                return GenerateJson("");
            }

            try
            {
                using var doc = JsonDocument.Parse(structure);
                return GenerateJsonFromTemplate(doc.RootElement);
            }
            catch
            {
                return GenerateJson("");
            }
        }
    }
}
