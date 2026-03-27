using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Linq;

namespace Test_Automation.Models.Editor
{
    public class JsonStructureNode : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _valueType = "string";
        private int _arrayLength = 5;
        private string _itemType = "string";
        private bool _isExpanded = true;
        private int _nestingLevel;
        private bool _isArrayItem;

        public string Key
        {
            get => _key;
            set
            {
                if (_key == value) return;
                _key = value;
                OnPropertyChanged();
            }
        }

        public string ValueType
        {
            get => _valueType;
            set
            {
                if (_valueType == value) return;
                _valueType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowArraySettings));
                OnPropertyChanged(nameof(ShowObjectChildren));
                OnPropertyChanged(nameof(ShowSimpleValue));
            }
        }

        public int ArrayLength
        {
            get => _arrayLength;
            set
            {
                if (_arrayLength == value) return;
                _arrayLength = value;
                OnPropertyChanged();
            }
        }

        public string ItemType
        {
            get => _itemType;
            set
            {
                if (_itemType == value) return;
                _itemType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowArrayItemObjectChildren));
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public int NestingLevel
        {
            get => _nestingLevel;
            set
            {
                if (_nestingLevel == value) return;
                _nestingLevel = value;
                OnPropertyChanged();
            }
        }

        public bool IsArrayItem
        {
            get => _isArrayItem;
            set
            {
                if (_isArrayItem == value) return;
                _isArrayItem = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<JsonStructureNode> Children { get; set; } = new();
        public ObservableCollection<JsonStructureNode> ArrayItemChildren { get; set; } = new();

        public bool ShowArraySettings => ValueType == "array";
        public bool ShowObjectChildren => ValueType == "object";
        public bool ShowArrayItemObjectChildren => ValueType == "array" && ItemType == "object";
        public bool ShowSimpleValue => ValueType != "object" && ValueType != "array";

        public static string[] ValueTypes => new[]
        {
            "string", "text", "integer", "int", "double", "float", "decimal", "long",
            "guid", "uuid", "boolean", "bool", "date", "datetime", "email",
            "firstname", "lastname", "fullname", "phone", "ip", "url", "color",
            "object", "array"
        };

        public static string[] ArrayItemTypes => new[]
        {
            "string", "integer", "double", "float", "decimal", "guid", "boolean",
            "date", "email", "firstname", "lastname", "fullname", "phone", "ip", "url", "object"
        };

        public JsonStructureNode()
        {
        }

        public JsonStructureNode(string key, string valueType, int nestingLevel = 0, bool isArrayItem = false)
        {
            _key = key;
            _valueType = valueType;
            _nestingLevel = nestingLevel;
            _isArrayItem = isArrayItem;
        }

        public JsonStructureNode Clone()
        {
            var clone = new JsonStructureNode
            {
                Key = Key,
                ValueType = ValueType,
                ArrayLength = ArrayLength,
                ItemType = ItemType,
                NestingLevel = NestingLevel,
                IsArrayItem = IsArrayItem,
                IsExpanded = IsExpanded
            };

            foreach (var child in Children)
            {
                clone.Children.Add(child.Clone());
            }

            foreach (var child in ArrayItemChildren)
            {
                clone.ArrayItemChildren.Add(child.Clone());
            }

            return clone;
        }

        public object ToTemplateObject()
        {
            return ValueType switch
            {
                "object" => Children.ToDictionary(c => c.Key, c => c.ToTemplateValue()),
                "array" => new Dictionary<string, object>
                {
                    ["__isArray"] = true,
                    ["__length"] = ArrayLength,
                    ["__itemType"] = ItemType,
                    ["__itemTemplate"] = ItemType == "object" 
                        ? (object)ArrayItemChildren.ToDictionary(c => c.Key, c => c.ToTemplateValue()) 
                        : (object)ItemType
                },
                _ => (object)$"__type:{ValueType}"
            };
        }

        private object ToTemplateValue()
        {
            return ValueType switch
            {
                "object" => Children.ToDictionary(c => c.Key, c => c.ToTemplateValue()),
                "array" => new Dictionary<string, object>
                {
                    ["__isArray"] = true,
                    ["__length"] = ArrayLength,
                    ["__itemType"] = ItemType,
                    ["__itemTemplate"] = ItemType == "object" 
                        ? (object)ArrayItemChildren.ToDictionary(c => c.Key, c => c.ToTemplateValue()) 
                        : (object)ItemType
                },
                _ => (object)$"__type:{ValueType}"
            };
        }

        public string ToJsonTemplate()
        {
            var template = Children.ToDictionary(c => c.Key, c => c.ToTemplateValue());
            return JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
