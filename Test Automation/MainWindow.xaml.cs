using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Test_Automation.Factories;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using Test_Automation.Models.ProjectFiles;
using Test_Automation.Services;

namespace Test_Automation
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static readonly JsonSerializerOptions PrettyJsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // Helper for resolving ${...} variable references in project/testplan variables
        private readonly VariableService _variableService = new();

        public ObservableCollection<PlanNode> RootNodes { get; } = new ObservableCollection<PlanNode>();
        public ObservableCollection<string> ExtractorSourceOptions { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AssertionSourceOptions { get; } = new ObservableCollection<string>();
        private static readonly string[] BaseExtractorSources =
        {
            "PreviewOutput",
            "PreviewResponse",
            "PreviewRequest",
            "PreviewVariables"
        };
        public ObservableCollection<string> AuthTypeOptions { get; } = new ObservableCollection<string>
        {
            "WindowsIntegrated",
            "None",
            "Basic",
            "Bearer",
            "ApiKey",
            "OAuth2"
        };

        public ObservableCollection<string> AssertionConditionOptions { get; } = new ObservableCollection<string>
        {
            "Equals",
            "NotEquals",
            "Contains",
            "NotContains",
            "StartsWith",
            "EndsWith",
            "GreaterThan",
            "GreaterOrEqual",
            "LessThan",
            "LessOrEqual",
            "IsEmpty",
            "IsNotEmpty",
            "Regex",
            "Script"
        };

        public ObservableCollection<string> AssertionModeOptions { get; } = new ObservableCollection<string>
        {
            "Assert",
            "Expect",
            "Assert and Stop"
        };

        public ObservableCollection<string> LogicalOperatorOptions { get; } = new ObservableCollection<string>
        {
            "And",
            "Or"
        };

        public ObservableCollection<string> ActionOptions { get; } = new ObservableCollection<string>
        {
            "None",
            "Break",
            "Continue"
        };

        public ObservableCollection<string> EnvironmentOptions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> TraceLevelOptions { get; } = new ObservableCollection<string>
        {
            "Off",
            "Errors",
            "Component Execution",
            "Verbose"
        };

        public ObservableCollection<string> HttpMethodOptions { get; } = new ObservableCollection<string>
        {
            "GET",
            "POST",
            "PUT",
            "PATCH",
            "DELETE",
            "HEAD",
            "OPTIONS"
        };

        public ObservableCollection<string> SqlProviderOptions { get; } = new ObservableCollection<string>
        {
            "SqlServer",
            "PostgreSql",
            "MySql",
            "Sqlite"
        };

        public ObservableCollection<string> DatasetFormatOptions { get; } = new ObservableCollection<string>
        {
            "Auto",
            "Excel",
            "Csv",
            "Json",
            "Xml"
        };

        public ObservableCollection<string> ExcelOperationOptions { get; } = new ObservableCollection<string>
        {
            "Write",
            "Append",
            "CreateSheet",
            "DeleteRows",
            "DeleteColumns",
            "ClearCells"
        };

        public ObservableCollection<string> ExcelSheetNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> DatasetSheetNames { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> ProjectRunModeOptions { get; } = new ObservableCollection<string>
        {
            "Sequence",
            "Parallel"
        };

        public ObservableCollection<ExecutionType> ExecutionTypeOptions { get; } = new ObservableCollection<ExecutionType>
        {
            ExecutionType.PreExecute,
            ExecutionType.Normal,
            ExecutionType.PostExecute
        };

        public ObservableCollection<string> PreviewDataModeOptions { get; } = new ObservableCollection<string>
        {
            "Last Run",
            "Full History"
        };

        private PlanNode? _selectedNode;
        private string _selectedEnvironment = string.Empty;
        private string _selectedTraceLevel = "Component Execution";
        private bool _isSyncingEnvironment;
        private bool _isRefreshingEnvironmentOptions;
        private bool _isNormalizingVariables;
        private int _variableUsageVersion;
        private Dictionary<string, List<string>> _variableUsageMap = new(StringComparer.OrdinalIgnoreCase);
        private ObservableCollection<string> _variableKeyOptions = new();
        private string? _currentProjectFilePath;
        public string CurrentProjectFilePath => string.IsNullOrWhiteSpace(_currentProjectFilePath) ? "Unsaved Project" : _currentProjectFilePath;
        private string _jsonPreview = "{}";
        private string _previewRequest = "Select a component to see request preview.";
        private string _previewResponse = "Select a component to see response preview.";
        private string _previewOutput = "Select a component to see output.";
        private string _httpRequestHeadersPreview = "Select an HTTP component to see request headers.";
        private string _httpRequestCookiesPreview = "Select an HTTP component to see request cookies.";
        private string _httpRequestMetadataPreview = "Select an HTTP component to see request metadata.";
        private string _httpResponseHeadersPreview = "Select an HTTP component to see response headers.";
        private string _httpResponseCookiesPreview = "Select an HTTP component to see response cookies.";
        private string _httpResponseMetadataPreview = "Select an HTTP component to see response metadata.";
        private string _previewLogs = "Logs will appear here.";
        private string _variablesPreview = "{}";
        private string _assertionPreview = "Select a component to see assertion preview.";
        private readonly System.Collections.Concurrent.ConcurrentQueue<Test_Automation.Models.TraceEventArgs> _logQueue = new();
        private System.Windows.Threading.DispatcherTimer? _logFlushTimer;
        private Test_Automation.Models.ExecutionContext? _lastExecutionContext;
        private Test_Automation.Models.ExecutionContext? _activeExecutionContext;
        private readonly List<Test_Automation.Models.ExecutionContext> _activeProjectExecutionContexts = new();
        private bool _isRunInProgress;
        private string _selectedProjectRunMode = "Sequence";
        private string _selectedPreviewDataMode = "Last Run";
        private bool _isApplyingCatalogSelection;
        private bool _isSynchronizingCatalogEditor;
        private readonly ObservableCollection<ApiCatalogBaseUrlEntry> _projectUrlCatalogEntries = new();
        private readonly ObservableCollection<string> _catalogBaseUrlOptions = new();
        private readonly ObservableCollection<string> _catalogEndpointOptions = new();
        private readonly ObservableCollection<string> _sqlAuthTypeOptions = new();
        private DataTable _datasetPreviewTable = new("DatasetPreview");
        private string _datasetPreviewStatus = "Choose a dataset source and click Refresh Preview.";
        private readonly List<ApiCatalogBaseUrlEntry> _parsedApiCatalog = new();
        private bool _isApplyingMainLayoutBounds;
        private static readonly string MainLayoutStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TestAutomation",
            "layout.main.json");
        private static readonly string AppStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TestAutomation",
            "appstate.json");

        private sealed class MainLayoutState
        {
            public double LeftColumnWidth { get; set; }
            public double LeftTopRowHeight { get; set; }
            public double EditorTopRowHeight { get; set; }
            public double EditorBottomRowHeight { get; set; }
            public double AssertionRowsHeight { get; set; }
            public double AssertionTreeHeight { get; set; }
            public double AssertionTreeLeftWidth { get; set; }
            public double AssertionTreeRightWidth { get; set; }
        }

        private sealed class AppState
        {
            public string? LastProjectFilePath { get; set; }
        }

        public sealed class ApiCatalogBaseUrlEntry
            : INotifyPropertyChanged
        {
            private string _name = string.Empty;
            private string _baseUrl = string.Empty;

            public string Name
            {
                get => _name;
                set
                {
                    if (_name == value) return;
                    _name = value;
                    OnPropertyChanged();
                }
            }

            public string BaseUrl
            {
                get => _baseUrl;
                set
                {
                    if (_baseUrl == value) return;
                    _baseUrl = value;
                    OnPropertyChanged();
                }
            }

            public ObservableCollection<ApiCatalogEndpointEntry> Endpoints { get; set; } = new();

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public sealed class ApiCatalogEndpointEntry
            : INotifyPropertyChanged
        {
            private string _name = string.Empty;
            private string _path = string.Empty;
            private string _method = string.Empty;
            private string _body = string.Empty;
            private string _headers = string.Empty;
            private string _query = string.Empty;
            private string _variables = string.Empty;

            public string Name
            {
                get => _name;
                set
                {
                    if (_name == value) return;
                    _name = value;
                    OnPropertyChanged();
                }
            }

            public string Path
            {
                get => _path;
                set
                {
                    if (_path == value) return;
                    _path = value;
                    OnPropertyChanged();
                }
            }

            public string Method
            {
                get => _method;
                set
                {
                    if (_method == value) return;
                    _method = value;
                    OnPropertyChanged();
                }
            }

            public string Body
            {
                get => _body;
                set
                {
                    if (_body == value) return;
                    _body = value;
                    OnPropertyChanged();
                }
            }

            public ObservableCollection<ApiCatalogParameterEntry> Parameters { get; set; } = new();

            public string Headers
            {
                get => _headers;
                set
                {
                    if (_headers == value) return;
                    _headers = value;
                    OnPropertyChanged();
                }
            }

            public string Query
            {
                get => _query;
                set
                {
                    if (_query == value) return;
                    _query = value;
                    OnPropertyChanged();
                }
            }

            public string Variables
            {
                get => _variables;
                set
                {
                    if (_variables == value) return;
                    _variables = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public sealed class ApiCatalogParameterEntry : INotifyPropertyChanged
        {
            private string _key = string.Empty;
            private string _value = string.Empty;

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

            public string Value
            {
                get => _value;
                set
                {
                    if (_value == value) return;
                    _value = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // Deserializes "parameters" from either legacy string form or new [{key,value}] array form.
        private sealed class CatalogParameterCollectionConverter
            : System.Text.Json.Serialization.JsonConverter<ObservableCollection<ApiCatalogParameterEntry>>
        {
            public override ObservableCollection<ApiCatalogParameterEntry> Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                var result = new ObservableCollection<ApiCatalogParameterEntry>();
                if (reader.TokenType == JsonTokenType.String)
                {
                    var raw = (reader.GetString() ?? string.Empty).TrimStart('?');
                    foreach (var part in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var eqIdx = part.IndexOf('=');
                        result.Add(eqIdx >= 0
                            ? new ApiCatalogParameterEntry { Key = Uri.UnescapeDataString(part[..eqIdx]), Value = Uri.UnescapeDataString(part[(eqIdx + 1)..]) }
                            : new ApiCatalogParameterEntry { Key = Uri.UnescapeDataString(part), Value = string.Empty });
                    }
                    return result;
                }
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    using var doc = JsonDocument.ParseValue(ref reader);
                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        result.Add(new ApiCatalogParameterEntry
                        {
                            Key   = element.TryGetProperty("key",   out var k) ? k.GetString() ?? string.Empty : string.Empty,
                            Value = element.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                        });
                    }
                    return result;
                }
                reader.Skip();
                return result;
            }

            public override void Write(
                Utf8JsonWriter writer,
                ObservableCollection<ApiCatalogParameterEntry> value,
                JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                foreach (var p in value)
                {
                    writer.WriteStartObject();
                    writer.WriteString("key",   p.Key   ?? string.Empty);
                    writer.WriteString("value", p.Value ?? string.Empty);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
        }

        public PlanNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode == value) return;
                _selectedNode = value;
                OnPropertyChanged();
                SyncCatalogSelectionFromCurrentNode();
                NotifySelectedNodeEditorProperties();
                RebuildExtractorSourceOptions();
                RefreshComponentPreview();
                RefreshDatasetPreview();
                RefreshAssertionJsonTreePanel();
            }
        }

        public string JsonPreview
        {
            get => _jsonPreview;
            set
            {
                if (_jsonPreview == value) return;
                _jsonPreview = value;
                OnPropertyChanged();
            }
        }

        public string PreviewRequest
        {
            get => _previewRequest;
            set
            {
                if (_previewRequest == value) return;
                _previewRequest = value;
                OnPropertyChanged();
            }
        }

        public string PreviewResponse
        {
            get => _previewResponse;
            set
            {
                if (_previewResponse == value) return;
                _previewResponse = value;
                OnPropertyChanged();
            }
        }

        public string PreviewOutput
        {
            get => _previewOutput;
            set
            {
                if (_previewOutput == value) return;
                _previewOutput = value;
                OnPropertyChanged();
            }
        }

        public string PreviewLogs
        {
            get => _previewLogs;
            set
            {
                if (_previewLogs == value) return;
                _previewLogs = value;
                OnPropertyChanged();
            }
        }

        public string HttpRequestHeadersPreview
        {
            get => _httpRequestHeadersPreview;
            set
            {
                if (_httpRequestHeadersPreview == value) return;
                _httpRequestHeadersPreview = value;
                OnPropertyChanged();
            }
        }

        public string HttpRequestCookiesPreview
        {
            get => _httpRequestCookiesPreview;
            set
            {
                if (_httpRequestCookiesPreview == value) return;
                _httpRequestCookiesPreview = value;
                OnPropertyChanged();
            }
        }

        public string HttpRequestMetadataPreview
        {
            get => _httpRequestMetadataPreview;
            set
            {
                if (_httpRequestMetadataPreview == value) return;
                _httpRequestMetadataPreview = value;
                OnPropertyChanged();
            }
        }

        public string HttpResponseHeadersPreview
        {
            get => _httpResponseHeadersPreview;
            set
            {
                if (_httpResponseHeadersPreview == value) return;
                _httpResponseHeadersPreview = value;
                OnPropertyChanged();
            }
        }

        public string HttpResponseCookiesPreview
        {
            get => _httpResponseCookiesPreview;
            set
            {
                if (_httpResponseCookiesPreview == value) return;
                _httpResponseCookiesPreview = value;
                OnPropertyChanged();
            }
        }

        public string HttpResponseMetadataPreview
        {
            get => _httpResponseMetadataPreview;
            set
            {
                if (_httpResponseMetadataPreview == value) return;
                _httpResponseMetadataPreview = value;
                OnPropertyChanged();
            }
        }

        public string VariablesPreview
        {
            get => _variablesPreview;
            set
            {
                if (_variablesPreview == value) return;
                _variablesPreview = value;
                OnPropertyChanged();
            }
        }

        public string AssertionPreview
        {
            get => _assertionPreview;
            set
            {
                if (_assertionPreview == value) return;
                _assertionPreview = value;
                OnPropertyChanged();
            }
        }

        public bool IsProjectSelected => SelectedNode?.Type == "Project";
        public bool IsComponentSelected => SelectedNode != null && SelectedNode.Type != "Project";
        public bool HasSelectedNodeChildren => SelectedNode != null && SelectedNode.Children.Count > 0;
        public bool IsHttpSelected => SelectedNode?.Type == "Http";
        public bool IsGraphQlSelected => SelectedNode?.Type == "GraphQl";
        public bool IsSqlSelected => SelectedNode?.Type == "Sql";
        public bool IsDatasetSelected => SelectedNode?.Type == "Dataset";
        public bool IsTimerSelected => SelectedNode?.Type == "Timer";
        public bool IsLoopSelected => SelectedNode?.Type == "Loop";
        public bool IsIfSelected => SelectedNode?.Type == "If";
        public bool IsThreadsSelected => SelectedNode?.Type == "Threads";
        public bool IsForeachSelected => SelectedNode?.Type == "Foreach";
        public bool IsAssertSelected => SelectedNode?.Type == "Assert";
        public bool IsVariableExtractorSelected => SelectedNode?.Type == "VariableExtractor";
        public bool IsScriptSelected => SelectedNode?.Type == "Script";
        public bool IsRandomGeneratorSelected => SelectedNode?.Type == "RandomGenerator";
        public bool IsBase64Selected => SelectedNode?.Type == "Base64";
        public bool IsWhileSelected => SelectedNode?.Type == "While";
        public bool IsFileSelected => SelectedNode?.Type == "File";
        public bool IsExcelSelected => SelectedNode?.Type == "Excel";
        public bool IsTestPlanSelected => SelectedNode?.Type == "TestPlan";

        public ExecutionType SelectedExecutionType
        {
            get => SelectedNode?.ExecutionType ?? ExecutionType.Normal;
            set
            {
                if (SelectedNode != null && SelectedNode.Type == "TestPlan")
                {
                    SelectedNode.ExecutionType = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TestPlanThreadCount
        {
            get => SelectedNode?.Type == "TestPlan" ? GetSettingValue("ThreadCount", "1") : "1";
            set
            {
                if (SelectedNode != null && SelectedNode.Type == "TestPlan")
                {
                    SetSettingValue("ThreadCount", value ?? "1");
                    OnPropertyChanged();
                }
            }
        }

        public IEnumerable<NodeSetting> ProjectVariablesForEditor =>
            SelectedNode?.Type == "Project"
                ? SelectedNode.Variables.Where(variable => !string.Equals(variable.Key, "env", StringComparison.OrdinalIgnoreCase))
                : Enumerable.Empty<NodeSetting>();

        public IEnumerable<NodeSetting> TestPlanVariablesForEditor =>
            SelectedNode?.Type == "TestPlan"
                ? SelectedNode.Variables
                : Enumerable.Empty<NodeSetting>();

        public int VariableUsageVersion
        {
            get => _variableUsageVersion;
            private set
            {
                if (_variableUsageVersion == value) return;
                _variableUsageVersion = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> VariableKeyOptions => _variableKeyOptions;

        public string ProjectDescription
        {
            get => GetSettingValue("Description", string.Empty);
            set => SetSettingValue("Description", value);
        }

        public string ProjectEnvironment
        {
            get => GetSettingValue("Environment", "dev");
            set
            {
                SetSettingValue("Environment", value);
                RefreshEnvironmentOptions();
            }
        }

        public string ProjectUrlCatalogJson
        {
            get => GetProjectSettingValue("UrlCatalog", "[]");
            set
            {
                SetProjectSettingValue("UrlCatalog", value ?? "[]");
                if (!_isSynchronizingCatalogEditor)
                {
                    RefreshApiCatalogState();
                }
            }
        }

        public ObservableCollection<ApiCatalogBaseUrlEntry> ProjectUrlCatalogEntries => _projectUrlCatalogEntries;

        public ObservableCollection<string> CatalogBaseUrlOptions => _catalogBaseUrlOptions;
        public ObservableCollection<string> CatalogEndpointOptions => _catalogEndpointOptions;

        public string SelectedHttpCatalogBase
        {
            get => string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("CatalogBase", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var next = value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(next))
                {
                    return;
                }

                SetSettingValue("CatalogBase", next);
                if (_isApplyingCatalogSelection)
                {
                    return;
                }

                RefreshCatalogEndpointOptions();
                OnPropertyChanged();
            }
        }

        public string SelectedHttpCatalogEndpoint
        {
            get => string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("CatalogEndpoint", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var next = value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(next))
                {
                    return;
                }

                SetSettingValue("CatalogEndpoint", next);
                OnPropertyChanged();
            }
        }

        public string SelectedGraphQlCatalogBase
        {
            get => string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("CatalogBase", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var next = value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(next))
                {
                    return;
                }

                SetSettingValue("CatalogBase", next);
                if (_isApplyingCatalogSelection)
                {
                    return;
                }

                RefreshCatalogEndpointOptions();
                OnPropertyChanged();
            }
        }

        public string SelectedGraphQlCatalogEndpoint
        {
            get => string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("CatalogEndpoint", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var next = value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(next))
                {
                    return;
                }

                SetSettingValue("CatalogEndpoint", next);
                OnPropertyChanged();
            }
        }

        public string SelectedEnvironment
        {
            get => _selectedEnvironment;
            set
            {
                var next = value ?? string.Empty;
                if (string.Equals(_selectedEnvironment, next, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedEnvironment = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HttpUrlResolved));
                OnPropertyChanged(nameof(SqlConnectionResolved));
                OnPropertyChanged(nameof(SqlQueryResolved));
                OnPropertyChanged(nameof(DatasetResolvedSourcePath));

                if (string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshDatasetPreview();
                }

                if (_isSyncingEnvironment)
                {
                    return;
                }

                if (SetProjectVariable("env", _selectedEnvironment))
                {
                    UpdateProjectVariablesPreview();
                    RefreshJsonPreview();
                }
            }
        }

        public string SelectedTraceLevel
        {
            get => _selectedTraceLevel;
            set
            {
                var normalized = TraceLevelOptions.Contains(value) ? value : "Component Execution";
                if (string.Equals(_selectedTraceLevel, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedTraceLevel = normalized;
                OnPropertyChanged();
            }
        }

        public string SelectedProjectRunMode
        {
            get => _selectedProjectRunMode;
            set
            {
                var normalized = ProjectRunModeOptions.Contains(value) ? value : "Sequence";
                if (string.Equals(_selectedProjectRunMode, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedProjectRunMode = normalized;
                OnPropertyChanged();
            }
        }

        public string SelectedPreviewDataMode
        {
            get => _selectedPreviewDataMode;
            set
            {
                var normalized = PreviewDataModeOptions.Contains(value) ? value : "Last Run";
                if (string.Equals(_selectedPreviewDataMode, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedPreviewDataMode = normalized;
                OnPropertyChanged();
                RefreshComponentPreview();
            }
        }

        private bool IsFullPreviewHistoryMode => string.Equals(SelectedPreviewDataMode, "Full History", StringComparison.OrdinalIgnoreCase);

        private List<ExecutionResult> FilterPreviewResults(IEnumerable<ExecutionResult> source, bool lastPerComponent)
        {
            var ordered = source
                .OrderBy(result => result.EndTime ?? result.StartTime)
                .ToList();

            if (IsFullPreviewHistoryMode || ordered.Count == 0)
            {
                return ordered;
            }

            if (!lastPerComponent)
            {
                return new List<ExecutionResult> { ordered[^1] };
            }

            return ordered
                .GroupBy(result => result.ComponentId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .First())
                .OrderBy(result => result.EndTime ?? result.StartTime)
                .ToList();
        }

        public string HttpMethod
        {
            get => GetSettingValue("Method", "GET");
            set => SetSettingValue("Method", value);
        }

        public string HttpUrl
        {
            get => GetSettingValue("Url", string.Empty);
            set => SetSettingValue("Url", value);
        }

        public string HttpUrlResolved => ResolveWithProjectVariables(HttpUrl);

        public string HttpBody
        {
            get => GetSettingValue("Body", string.Empty);
            set => SetSettingValue("Body", value);
        }

        public string HttpHeaders
        {
            get => GetSettingValue("Headers", "{}");
            set => SetSettingValue("Headers", value);
        }

        public string HttpAuthType
        {
            get => GetSettingValue("AuthType", "WindowsIntegrated");
            set
            {
                SetSettingValue("AuthType", value);
                RaiseHttpAuthVisibilityChanged();
            }
        }

        public bool HttpShowBasicFields => string.Equals(HttpAuthType, "Basic", StringComparison.OrdinalIgnoreCase);
        public bool HttpShowBearerFields => string.Equals(HttpAuthType, "Bearer", StringComparison.OrdinalIgnoreCase);
        public bool HttpShowApiKeyFields => string.Equals(HttpAuthType, "ApiKey", StringComparison.OrdinalIgnoreCase);
        public bool HttpShowOAuthFields => string.Equals(HttpAuthType, "OAuth2", StringComparison.OrdinalIgnoreCase);

        public string HttpAuthUsername
        {
            get => GetSettingValue("AuthUsername", string.Empty);
            set => SetSettingValue("AuthUsername", value);
        }

        public string HttpAuthPassword
        {
            get => GetSettingValue("AuthPassword", string.Empty);
            set => SetSettingValue("AuthPassword", value);
        }

        public string HttpAuthToken
        {
            get => GetSettingValue("AuthToken", string.Empty);
            set => SetSettingValue("AuthToken", value);
        }

        public string HttpApiKeyName
        {
            get => GetSettingValue("ApiKeyName", string.Empty);
            set => SetSettingValue("ApiKeyName", value);
        }

        public string HttpApiKeyValue
        {
            get => GetSettingValue("ApiKeyValue", string.Empty);
            set => SetSettingValue("ApiKeyValue", value);
        }

        public string HttpApiKeyLocation
        {
            get => GetSettingValue("ApiKeyLocation", "Header");
            set => SetSettingValue("ApiKeyLocation", value);
        }

        public string HttpOAuthTokenUrl
        {
            get => GetSettingValue("OAuthTokenUrl", string.Empty);
            set => SetSettingValue("OAuthTokenUrl", value);
        }

        public string HttpOAuthClientId
        {
            get => GetSettingValue("OAuthClientId", string.Empty);
            set => SetSettingValue("OAuthClientId", value);
        }

        public string HttpOAuthClientSecret
        {
            get => GetSettingValue("OAuthClientSecret", string.Empty);
            set => SetSettingValue("OAuthClientSecret", value);
        }

        public string HttpOAuthScope
        {
            get => GetSettingValue("OAuthScope", string.Empty);
            set => SetSettingValue("OAuthScope", value);
        }

        public string HttpResponseBodyVariable
        {
            get => GetSettingValue("ResponseBodyVariable", string.Empty);
            set => SetSettingValue("ResponseBodyVariable", value);
        }

        public string HttpResponseStatusCodeVariable
        {
            get => GetSettingValue("ResponseStatusCodeVariable", string.Empty);
            set => SetSettingValue("ResponseStatusCodeVariable", value);
        }

        public string HttpResponseDurationVariable
        {
            get => GetSettingValue("ResponseDurationVariable", string.Empty);
            set => SetSettingValue("ResponseDurationVariable", value);
        }

        public string GraphQlEndpoint
        {
            get => GetSettingValue("Endpoint", "https://api.example.com/graphql");
            set => SetSettingValue("Endpoint", value);
        }

        public string GraphQlQuery
        {
            get => GetSettingValue("Query", "query { health }");
            set => SetSettingValue("Query", value);
        }

        public string GraphQlVariables
        {
            get => GetSettingValue("Variables", "{}");
            set => SetSettingValue("Variables", value);
        }

        public string GraphQlHeaders
        {
            get => GetSettingValue("Headers", "{}");
            set => SetSettingValue("Headers", value);
        }

        public string GraphQlAuthType
        {
            get => GetSettingValue("AuthType", "WindowsIntegrated");
            set
            {
                SetSettingValue("AuthType", value);
                RaiseGraphQlAuthVisibilityChanged();
            }
        }

        public bool GraphQlShowBasicFields => string.Equals(GraphQlAuthType, "Basic", StringComparison.OrdinalIgnoreCase);
        public bool GraphQlShowBearerFields => string.Equals(GraphQlAuthType, "Bearer", StringComparison.OrdinalIgnoreCase);
        public bool GraphQlShowApiKeyFields => string.Equals(GraphQlAuthType, "ApiKey", StringComparison.OrdinalIgnoreCase);
        public bool GraphQlShowOAuthFields => string.Equals(GraphQlAuthType, "OAuth2", StringComparison.OrdinalIgnoreCase);

        public string GraphQlAuthUsername
        {
            get => GetSettingValue("AuthUsername", string.Empty);
            set => SetSettingValue("AuthUsername", value);
        }

        public string GraphQlAuthPassword
        {
            get => GetSettingValue("AuthPassword", string.Empty);
            set => SetSettingValue("AuthPassword", value);
        }

        public string GraphQlAuthToken
        {
            get => GetSettingValue("AuthToken", string.Empty);
            set => SetSettingValue("AuthToken", value);
        }

        public string GraphQlApiKeyName
        {
            get => GetSettingValue("ApiKeyName", string.Empty);
            set => SetSettingValue("ApiKeyName", value);
        }

        public string GraphQlApiKeyValue
        {
            get => GetSettingValue("ApiKeyValue", string.Empty);
            set => SetSettingValue("ApiKeyValue", value);
        }

        public string GraphQlApiKeyLocation
        {
            get => GetSettingValue("ApiKeyLocation", "Header");
            set => SetSettingValue("ApiKeyLocation", value);
        }

        public string GraphQlOAuthTokenUrl
        {
            get => GetSettingValue("OAuthTokenUrl", string.Empty);
            set => SetSettingValue("OAuthTokenUrl", value);
        }

        public string GraphQlOAuthClientId
        {
            get => GetSettingValue("OAuthClientId", string.Empty);
            set => SetSettingValue("OAuthClientId", value);
        }

        public string GraphQlOAuthClientSecret
        {
            get => GetSettingValue("OAuthClientSecret", string.Empty);
            set => SetSettingValue("OAuthClientSecret", value);
        }

        public string GraphQlOAuthScope
        {
            get => GetSettingValue("OAuthScope", string.Empty);
            set => SetSettingValue("OAuthScope", value);
        }

        public string SqlConnection
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Connection", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("Connection", value);
            }
        }

        public string SqlConnectionResolved => ResolveWithProjectVariables(SqlConnection);

        public string SqlProvider
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? NormalizeSqlProvider(GetSettingValue("Provider", "SqlServer"))
                : "SqlServer";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var normalized = NormalizeSqlProvider(value);
                SetSettingValue("Provider", normalized);
                RefreshSqlAuthTypeOptions();

                if (!_sqlAuthTypeOptions.Contains(SqlAuthType))
                {
                    SqlAuthType = GetDefaultSqlAuthType(normalized);
                }

                RaiseSqlAuthVisibilityChanged();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SqlAuthTypeOptions));
            }
        }

        public string SqlQuery
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Query", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("Query", value);
            }
        }

        public string SqlQueryResolved => ResolveWithProjectVariables(SqlQuery);

        public string SqlAuthType
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("AuthType", GetDefaultSqlAuthType(SqlProvider))
                : GetDefaultSqlAuthType("SqlServer");
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var normalized = value ?? string.Empty;
                if (!_sqlAuthTypeOptions.Contains(normalized))
                {
                    normalized = GetDefaultSqlAuthType(SqlProvider);
                }

                SetSettingValue("AuthType", normalized);
                RaiseSqlAuthVisibilityChanged();
            }
        }

        public ObservableCollection<string> SqlAuthTypeOptions => _sqlAuthTypeOptions;

        public bool SqlShowBasicFields => string.Equals(SqlAuthType, "Basic", StringComparison.OrdinalIgnoreCase);

        public string SqlAuthUsername
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("AuthUsername", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("AuthUsername", value);
            }
        }

        public string SqlAuthPassword
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("AuthPassword", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("AuthPassword", value);
            }
        }

        public string DatasetFormat
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Format", "Auto")
                : "Auto";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("Format", value);
                RefreshDatasetPreview();
                OnPropertyChanged(nameof(IsDatasetExcel));
                OnPropertyChanged(nameof(IsDatasetCsv));
                OnPropertyChanged(nameof(IsDatasetJson));
                OnPropertyChanged(nameof(IsDatasetXml));
            }
        }

        // Visibility properties for format-specific fields
        public bool IsDatasetExcel
        {
            get
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                    return false;
                
                var format = DatasetFormat;
                if (string.Equals(format, "Excel", StringComparison.OrdinalIgnoreCase))
                    return true;
                
                // For Auto format, check file extension
                if (string.Equals(format, "Auto", StringComparison.OrdinalIgnoreCase))
                {
                    var ext = Path.GetExtension(DatasetSourcePath)?.ToLowerInvariant() ?? "";
                    return ext == ".xlsx" || ext == ".xlsm" || ext == ".xls";
                }
                
                return false;
            }
        }

        public bool IsDatasetCsv
        {
            get
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                    return false;
                
                var format = DatasetFormat;
                if (string.Equals(format, "Csv", StringComparison.OrdinalIgnoreCase))
                    return true;
                
                // For Auto format, check file extension
                if (string.Equals(format, "Auto", StringComparison.OrdinalIgnoreCase))
                {
                    var ext = Path.GetExtension(DatasetSourcePath)?.ToLowerInvariant() ?? "";
                    return ext == ".csv";
                }
                
                return false;
            }
        }

        public bool IsDatasetJson
        {
            get
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                    return false;
                
                var format = DatasetFormat;
                if (string.Equals(format, "Json", StringComparison.OrdinalIgnoreCase))
                    return true;
                
                // For Auto format, check file extension
                if (string.Equals(format, "Auto", StringComparison.OrdinalIgnoreCase))
                {
                    var ext = Path.GetExtension(DatasetSourcePath)?.ToLowerInvariant() ?? "";
                    return ext == ".json";
                }
                
                return false;
            }
        }

        public bool IsDatasetXml
        {
            get
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                    return false;
                
                var format = DatasetFormat;
                if (string.Equals(format, "Xml", StringComparison.OrdinalIgnoreCase))
                    return true;
                
                // For Auto format, check file extension
                if (string.Equals(format, "Auto", StringComparison.OrdinalIgnoreCase))
                {
                    var ext = Path.GetExtension(DatasetSourcePath)?.ToLowerInvariant() ?? "";
                    return ext == ".xml";
                }
                
                return false;
            }
        }

        public string DatasetSourcePath
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("SourcePath", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("SourcePath", value);
                OnPropertyChanged(nameof(DatasetResolvedSourcePath));
                RefreshDatasetSheetNames();
                RefreshDatasetPreview();
                OnPropertyChanged(nameof(IsDatasetExcel));
                OnPropertyChanged(nameof(IsDatasetCsv));
                OnPropertyChanged(nameof(IsDatasetJson));
                OnPropertyChanged(nameof(IsDatasetXml));
            }
        }

        public string DatasetResolvedSourcePath => ResolveWithProjectVariables(DatasetSourcePath);

        public string DatasetSheetName
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("SheetName", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("SheetName", value);
                RefreshDatasetPreview();
            }
        }

        public string DatasetOutputVariable
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("OutputVariable", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("OutputVariable", value);
            }
        }

        public string DatasetCsvDelimiter
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("CsvDelimiter", ",")
                : ",";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("CsvDelimiter", value);
                RefreshDatasetPreview();
            }
        }

        public bool DatasetHasHeader
        {
            get
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check HasHeader first, fall back to CsvHasHeader for backward compatibility
                var raw = GetSettingValue("HasHeader", GetSettingValue("CsvHasHeader", "true"));
                return bool.TryParse(raw, out var parsed) ? parsed : true;
            }
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("HasHeader", value ? "true" : "false");
                RefreshDatasetPreview();
            }
        }

        public string DatasetJsonArrayPath
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("JsonArrayPath", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("JsonArrayPath", value);
                RefreshDatasetPreview();
            }
        }

        public string DatasetXmlRowPath
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("XmlRowPath", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("XmlRowPath", value);
                RefreshDatasetPreview();
            }
        }

        public string DatasetMaxRows
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("MaxRows", "0")
                : "0";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("MaxRows", value);
                RefreshDatasetPreview();
            }
        }

        public DataView DatasetPreviewRows => _datasetPreviewTable.DefaultView;

        public string DatasetPreviewStatus
        {
            get => _datasetPreviewStatus;
            set
            {
                if (_datasetPreviewStatus == value)
                {
                    return;
                }

                _datasetPreviewStatus = value;
                OnPropertyChanged();
            }
        }

        #region Excel Properties

        public bool ExcelFileModeNew
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? string.Equals(GetSettingValue("FileMode", "Existing"), "New", StringComparison.OrdinalIgnoreCase)
                : false;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("FileMode", value ? "New" : "Existing");
                OnPropertyChanged(nameof(ExcelFileModeNew));
                OnPropertyChanged(nameof(ExcelFileModeExisting));
                OnPropertyChanged(nameof(ExcelShowSheetName));
                OnPropertyChanged(nameof(ExcelShowSheetDropdown));
                OnPropertyChanged(nameof(ExcelJsonToolTip));
                UpdateExcelExamples();
            }
        }

        public bool ExcelFileModeExisting
        {
            get => !ExcelFileModeNew;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("FileMode", value ? "Existing" : "New");
                OnPropertyChanged(nameof(ExcelFileModeNew));
                OnPropertyChanged(nameof(ExcelFileModeExisting));
                OnPropertyChanged(nameof(ExcelShowSheetName));
                OnPropertyChanged(nameof(ExcelShowSheetDropdown));
                OnPropertyChanged(nameof(ExcelJsonToolTip));
                UpdateExcelExamples();
            }
        }

        public string ExcelFilePath
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("FilePath", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("FilePath", value);
                OnPropertyChanged(nameof(ExcelFilePath));
            }
        }

        public string ExcelFolderPath
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("FolderPath", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("FolderPath", value);
                OnPropertyChanged(nameof(ExcelFolderPath));
            }
        }

        public string ExcelFileName
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("FileName", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("FileName", value);
                OnPropertyChanged(nameof(ExcelFileName));
            }
        }

        public string ExcelSheetName
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("SheetName", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("SheetName", value);
                OnPropertyChanged(nameof(ExcelSheetName));
            }
        }

        public string ExcelSelectedSheet
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("SelectedSheet", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("SelectedSheet", value);
                OnPropertyChanged(nameof(ExcelSelectedSheet));
            }
        }

        public string ExcelOperation
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Operation", "Write")
                : "Write";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("Operation", value);
                OnPropertyChanged(nameof(ExcelOperation));
                OnPropertyChanged(nameof(ExcelJsonToolTip));
                UpdateExcelExamples();
            }
        }

        public string ExcelColumn
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Column", "A")
                : "A";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("Column", value);
                OnPropertyChanged(nameof(ExcelColumn));
            }
        }

        public string ExcelRow
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Row", "1")
                : "1";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("Row", value);
                OnPropertyChanged(nameof(ExcelRow));
            }
        }

        public string ExcelValue
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Value", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("Value", value);
                OnPropertyChanged(nameof(ExcelValue));
            }
        }

        public string ExcelValuesJson
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Values", "[]")
                : "[]";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("Values", value);
                OnPropertyChanged(nameof(ExcelValuesJson));
            }
        }

        public string ExcelDeleteStartColumn
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("DeleteStartColumn", "A")
                : "A";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("DeleteStartColumn", value);
                OnPropertyChanged(nameof(ExcelDeleteStartColumn));
            }
        }

        public string ExcelDeleteStartRow
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("DeleteStartRow", "1")
                : "1";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("DeleteStartRow", value);
                OnPropertyChanged(nameof(ExcelDeleteStartRow));
            }
        }

        public string ExcelDeleteEndColumn
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("DeleteEndColumn", "A")
                : "A";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("DeleteEndColumn", value);
                OnPropertyChanged(nameof(ExcelDeleteEndColumn));
            }
        }

        public string ExcelDeleteEndRow
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("DeleteEndRow", "1")
                : "1";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("DeleteEndRow", value);
                OnPropertyChanged(nameof(ExcelDeleteEndRow));
            }
        }

        public string ExcelJsonData
        {
            get => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("JsonData", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return;
                SetSettingValue("JsonData", value);
                OnPropertyChanged(nameof(ExcelJsonData));
            }
        }

        private string _excelExamplesTextValue = string.Empty;
        public string ExcelExamplesTextValue
        {
            get => _excelExamplesTextValue;
            set
            {
                _excelExamplesTextValue = value;
                OnPropertyChanged(nameof(ExcelExamplesTextValue));
            }
        }

        // Visibility helpers - simplified for new JSON-based UI
        public bool ExcelShowSheetName => ExcelFileModeNew && string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase);
        public bool ExcelShowSheetDropdown => ExcelFileModeExisting && string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase);
        public bool ExcelShowOperation => ExcelFileModeExisting && string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase);
        public bool ExcelShowJsonInput => string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase);
        
        public string ExcelJsonToolTip
        {
            get
            {
                if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                    
                if (ExcelFileModeNew)
                    return "Enter JSON with 'sheets' array. Each sheet has 'name', optional 'headers', and 'rows' array.";
                
                var op = ExcelOperation?.ToLowerInvariant() ?? "write";
                return op switch
                {
                    "write" => "Write: use 'startCell' and 'values' array. For append mode, use 'mode':'append' with 'startColumn'.",
                    "append" => "Append: use 'startColumn' and 'values' array. Data is added after the last row.",
                    "deleterows" => "Delete rows: use 'startRow' and 'endRow'. Rows are removed and shifted up.",
                    "deletecolumns" => "Delete columns: use 'startColumn' and 'endColumn'. Columns are removed and shifted left.",
                    "clearcells" => "Clear cells: use 'startCell' and 'endCell'. Content is cleared but rows/columns remain.",
                    "createsheet" => "Create sheet: use 'name' for the new sheet.",
                    _ => "Enter JSON data for the selected operation"
                };
            }
        }

        // Helper to load sheet names from file
        public void RefreshExcelSheetNames()
        {
            if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
                return;

            var savedSelectedSheet = ExcelSelectedSheet; // Save current selection
            
            ExcelSheetNames.Clear();
            var filePath = ResolveWithProjectVariables(ExcelFilePath);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook(filePath);
                foreach (var sheet in workbook.Worksheets)
                {
                    ExcelSheetNames.Add(sheet.Name);
                }
                
                // Restore selection or set to first sheet
                if (!string.IsNullOrWhiteSpace(savedSelectedSheet) && 
                    ExcelSheetNames.Contains(savedSelectedSheet))
                {
                    ExcelSelectedSheet = savedSelectedSheet;
                }
                else if (ExcelSheetNames.Count > 0 && string.IsNullOrWhiteSpace(savedSelectedSheet))
                {
                    ExcelSelectedSheet = ExcelSheetNames[0];
                }
            }
            catch (Exception)
            {
                // ignore errors
            }
        }

        // Update examples text in the expander
        public void UpdateExcelExamples()
        {
            string examples;
            
            if (ExcelFileModeNew)
            {
                examples = "{" + "\n" +
                    "  \"sheets\": [" + "\n" +
                    "    {" + "\n" +
                    "      \"name\": \"Sheet1\"," + "\n" +
                    "      \"headers\": [\"Name\", \"Age\", \"City\"]," + "\n" +
                    "      \"rows\": [" + "\n" +
                    "        [\"John\", \"30\", \"New York\"]," + "\n" +
                    "        [\"Jane\", \"25\", \"Los Angeles\"]," + "\n" +
                    "        [\"Bob\", \"35\", \"Chicago\"]" + "\n" +
                    "      ]" + "\n" +
                    "    }," + "\n" +
                    "    {" + "\n" +
                    "      \"name\": \"Sheet2\"," + "\n" +
                    "      \"headers\": [\"Product\", \"Price\", \"Stock\"]," + "\n" +
                    "      \"rows\": [" + "\n" +
                    "        [\"Laptop\", \"999\", \"50\"]," + "\n" +
                    "        [\"Mouse\", \"25\", \"200\"]," + "\n" +
                    "        [\"Keyboard\", \"75\", \"100\"]" + "\n" +
                    "      ]" + "\n" +
                    "    }" + "\n" +
                    "  ]" + "\n" +
                    "}";
            }
            else
            {
                var op = ExcelOperation?.ToLowerInvariant() ?? "write";
                examples = op switch
                {
                    "write" => "{" + "\n" +
                        "  \"startCell\": \"A1\"," + "\n" +
                        "  \"values\": [" + "\n" +
                        "    [\"Name\", \"Age\", \"City\"]," + "\n" +
                        "    [\"John\", \"30\", \"New York\"]," + "\n" +
                        "    [\"Jane\", \"25\", \"Los Angeles\"]," + "\n" +
                        "    [\"Bob\", \"35\", \"Chicago\"]" + "\n" +
                        "  ]" + "\n" +
                        "}" + "\n\n" +
                        "// Or use append mode:" + "\n" +
                        "{" + "\n" +
                        "  \"mode\": \"append\"," + "\n" +
                        "  \"startColumn\": \"A\"," + "\n" +
                        "  \"values\": [[" + "\n" +
                        "    \"David\", \"28\", \"Boston\"" + "\n" +
                        "  ]]" + "\n" +
                        "}",
                    "append" => "{" + "\n" +
                        "  \"startColumn\": \"A\"," + "\n" +
                        "  \"values\": [" + "\n" +
                        "    [\"David\", \"28\", \"Boston\"]," + "\n" +
                        "    [\"Emily\", \"32\", \"Seattle\"]" + "\n" +
                        "  ]" + "\n" +
                        "}",
                    "deleterows" => "{" + "\n" +
                        "  \"startRow\": 2," + "\n" +
                        "  \"endRow\": 5" + "\n" +
                        "}",
                    "deletecolumns" => "{" + "\n" +
                        "  \"startColumn\": \"B\"," + "\n" +
                        "  \"endColumn\": \"D\"" + "\n" +
                        "}",
                    "clearcells" => "{" + "\n" +
                        "  \"startCell\": \"A1\"," + "\n" +
                        "  \"endCell\": \"C10\"" + "\n" +
                        "}",
                    "createsheet" => "{" + "\n" +
                        "  \"name\": \"NewSheet\"" + "\n" +
                        "}",
                    _ => ""
                };
            }
            
            ExcelExamplesTextValue = examples;
        }

        private void CopyExcelExamples_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ExcelExamplesTextValue))
            {
                Clipboard.SetText(ExcelExamplesTextValue);
                // Optional: Show a brief notification
                if (sender is Button btn)
                {
                    var originalContent = btn.Content;
                    btn.Content = "Copied!";
                    btn.Dispatcher.InvokeAsync(async () =>
                    {
                        await Task.Delay(1500);
                        btn.Content = originalContent;
                    });
                }
            }
        }

        #endregion

        private static string NormalizeSqlProvider(string? provider)
        {
            if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                return "PostgreSql";
            }

            if (string.Equals(provider, "MySql", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "MySQL", StringComparison.OrdinalIgnoreCase))
            {
                return "MySql";
            }

            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "SQLite", StringComparison.OrdinalIgnoreCase))
            {
                return "Sqlite";
            }

            return "SqlServer";
        }

        private static string GetDefaultSqlAuthType(string provider)
        {
            return string.Equals(NormalizeSqlProvider(provider), "SqlServer", StringComparison.OrdinalIgnoreCase)
                ? "WindowsIntegrated"
                : "None";
        }

        private void RefreshSqlAuthTypeOptions()
        {
            _sqlAuthTypeOptions.Clear();

            var provider = string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? SqlProvider
                : "SqlServer";

            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                _sqlAuthTypeOptions.Add("WindowsIntegrated");
                _sqlAuthTypeOptions.Add("None");
                _sqlAuthTypeOptions.Add("Basic");
            }
            else if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                _sqlAuthTypeOptions.Add("None");
            }
            else
            {
                _sqlAuthTypeOptions.Add("None");
                _sqlAuthTypeOptions.Add("Basic");
            }

            OnPropertyChanged(nameof(SqlAuthTypeOptions));
        }

        public string TimerDelayMs
        {
            get => GetSettingValue("DelayMs", "1000");
            set => SetSettingValue("DelayMs", value);
        }

        public string LoopIterations
        {
            get => GetSettingValue("Iterations", "1");
            set => SetSettingValue("Iterations", value);
        }

        public string ForeachSourceVariable
        {
            get => GetSettingValue("SourceVariable", string.Empty);
            set => SetSettingValue("SourceVariable", value);
        }

        public string ForeachOutputVariable
        {
            get => GetSettingValue("OutputVariable", string.Empty);
            set => SetSettingValue("OutputVariable", value);
        }

        public string IfCondition
        {
            get => GetSettingValue("Condition", string.Empty);
            set => SetSettingValue("Condition", value);
        }

        public string WhileMaxIterations
        {
            get => GetSettingValue("MaxIterations", "1000");
            set => SetSettingValue("MaxIterations", value);
        }

        public string WhileTimeoutMs
        {
            get => GetSettingValue("TimeoutMs", "0");
            set => SetSettingValue("TimeoutMs", value);
        }

        public string WhileEvaluationMode
        {
            get => GetSettingValue("EvaluationMode", "While");
            set => SetSettingValue("EvaluationMode", value);
        }

        public string WhileConditionJson
        {
            get => GetSettingValue("ConditionJson", "[]");
            set => SetSettingValue("ConditionJson", value);
        }

        public ObservableCollection<string> WhileEvaluationModeOptions { get; } = new ObservableCollection<string> { "While", "DoWhile" };

        public ObservableCollection<ConditionRow> WhileConditionRows { get; } = new ObservableCollection<ConditionRow>();

        public string ThreadCount
        {
            get => GetSettingValue("ThreadCount", "1");
            set => SetSettingValue("ThreadCount", value);
        }

        public string RampUpSeconds
        {
            get => GetSettingValue("RampUpSeconds", "1");
            set => SetSettingValue("RampUpSeconds", value);
        }

        public string AssertExpected
        {
            get => GetSettingValue("Expected", string.Empty);
            set => SetSettingValue("Expected", value);
        }

        public string AssertActual
        {
            get => GetSettingValue("Actual", string.Empty);
            set => SetSettingValue("Actual", value);
        }

        public string ExtractorPattern
        {
            get => GetSettingValue("Pattern", string.Empty);
            set => SetSettingValue("Pattern", value);
        }

        public string ExtractorVariableName
        {
            get => GetSettingValue("VariableName", string.Empty);
            set => SetSettingValue("VariableName", value);
        }

        public string ScriptLanguage
        {
            get => GetSettingValue("Language", "CSharp");
            set => SetSettingValue("Language", value);
        }

        public string ScriptCode
        {
            get => GetSettingValue("Code", string.Empty);
            set => SetSettingValue("Code", value);
        }

        #region RandomGenerator Properties

        private static readonly string[] _randomOutputTypeOptions = new[]
        {
            "integer", "number", "float", "decimal", "long",
            "guid", "guid-n", "uuid",
            "string", "utf8", "ascii", "hex", "base64", "alphanumeric", "alpha", "numeric", "lorem",
            "firstname", "lastname", "fullname", "email", "username",
            "datetime", "date", "time", "timestamp",
            "ip", "ipv6", "mac", "url", "hostname", "phone",
            "bool", "color", "zipcode",
            "json", "array",
            "uppercase", "lowercase", "camelcase", "pascalcase", "snakecase", "kebabcase"
        };

        private static readonly string[] _randomItemTypeOptions = new[]
        {
            "string", "integer", "double", "float", "decimal", "guid", "boolean", "date", "email", "name", "json"
        };

        public string[] RandomOutputTypeOptions => _randomOutputTypeOptions;
        public string[] RandomItemTypeOptions => _randomItemTypeOptions;

        public string RandomOutputType
        {
            get => GetSettingValue("OutputType", "number");
            set
            {
                SetSettingValue("OutputType", value);
                OnPropertyChanged(nameof(RandomShowStringOptions));
                OnPropertyChanged(nameof(RandomShowArrayOptions));
                OnPropertyChanged(nameof(RandomShowJsonOptions));
                OnPropertyChanged(nameof(RandomShowEmailOption));
            }
        }

        public string RandomMin
        {
            get => GetSettingValue("Min", "0");
            set => SetSettingValue("Min", value);
        }

        public string RandomMax
        {
            get => GetSettingValue("Max", "100");
            set => SetSettingValue("Max", value);
        }

        public string RandomLength
        {
            get => GetSettingValue("Length", "10");
            set => SetSettingValue("Length", value);
        }

        public string RandomDecimalPlaces
        {
            get => GetSettingValue("DecimalPlaces", "2");
            set => SetSettingValue("DecimalPlaces", value);
        }

        public string RandomArrayLength
        {
            get => GetSettingValue("ArrayLength", "5");
            set => SetSettingValue("ArrayLength", value);
        }

        public string RandomItemType
        {
            get => GetSettingValue("ItemType", "string");
            set => SetSettingValue("ItemType", value);
        }

        public string RandomEmailDomain
        {
            get => GetSettingValue("EmailDomain", "");
            set => SetSettingValue("EmailDomain", value);
        }

        public string RandomVariableName
        {
            get => GetSettingValue("VariableName", "");
            set => SetSettingValue("VariableName", value);
        }

        public string RandomJsonStructure
        {
            get => GetSettingValue("JsonStructure", "");
            set => SetSettingValue("JsonStructure", value);
        }

        public bool RandomIncludeUpper
        {
            get => GetSettingBoolValue("IncludeUpper", true);
            set => SetSettingValue("IncludeUpper", value.ToString());
        }

        public bool RandomIncludeLower
        {
            get => GetSettingBoolValue("IncludeLower", true);
            set => SetSettingValue("IncludeLower", value.ToString());
        }

        public bool RandomIncludeNumbers
        {
            get => GetSettingBoolValue("IncludeNumbers", true);
            set => SetSettingValue("IncludeNumbers", value.ToString());
        }

        public bool RandomIncludeSpecial
        {
            get => GetSettingBoolValue("IncludeSpecial", false);
            set => SetSettingValue("IncludeSpecial", value.ToString());
        }

        public bool RandomShowStringOptions
        {
            get
            {
                var type = RandomOutputType?.ToLower() ?? "";
                return type == "string" || type == "text" || type == "alphanumeric" || type == "alpha";
            }
        }

        public bool RandomShowArrayOptions
        {
            get
            {
                var type = RandomOutputType?.ToLower() ?? "";
                return type == "array" || type == "list";
            }
        }

        public bool RandomShowJsonOptions
        {
            get
            {
                var type = RandomOutputType?.ToLower() ?? "";
                return type == "json" || type == "jsonobject" || type == "object";
            }
        }

        public bool RandomShowEmailOption
        {
            get
            {
                var type = RandomOutputType?.ToLower() ?? "";
                return type == "email";
            }
        }

        private bool GetSettingBoolValue(string key, bool defaultValue)
        {
            var value = GetSettingValue(key, null);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return bool.TryParse(value, out var result) && result;
            }
            return defaultValue;
        }

        #endregion

        #region Base64 Properties

        private static readonly string[] _base64OperationOptions = new[] { "Encode", "Decode" };
        private static readonly string[] _base64DataTypeOptions = new[] { "Text", "Binary" };
        private static readonly string[] _base64EncodingOptions = new[] { "UTF-8", "ASCII", "UTF-16", "Latin1" };

        public string[] Base64OperationOptions => _base64OperationOptions;
        public string[] Base64DataTypeOptions => _base64DataTypeOptions;
        public string[] Base64EncodingOptions => _base64EncodingOptions;

        public string Base64Input
        {
            get => GetSettingValue("Input", "");
            set => SetSettingValue("Input", value);
        }

        public string Base64Operation
        {
            get => GetSettingValue("Operation", "Encode");
            set => SetSettingValue("Operation", value);
        }

        public string Base64DataType
        {
            get => GetSettingValue("DataType", "Text");
            set => SetSettingValue("DataType", value);
        }

        public string Base64FilePath
        {
            get => GetSettingValue("FilePath", "");
            set => SetSettingValue("FilePath", value);
        }

        public string Base64Encoding
        {
            get => GetSettingValue("Encoding", "UTF-8");
            set => SetSettingValue("Encoding", value);
        }

        public string Base64OutputVariable
        {
            get => GetSettingValue("OutputVariable", "");
            set => SetSettingValue("OutputVariable", value);
        }

        #endregion

        #region File Properties

        private static readonly string[] _fileOperationOptions = new[]
        {
            "Read", "Write", "Copy", "Move", "Delete", "List", 
            "CreateFolder", "CreateFile", "ReadFiles"
        };

        private static readonly string[] _fileEncodingOptions = new[]
        {
            "UTF-8", "ASCII", "UTF-16", "Latin1"
        };

        private static readonly string[] _fileReadModeOptions = new[]
        {
            "All", "Selected"
        };

        private static readonly string[] _fileWriteModeOptions = new[]
        {
            "Overwrite", "Append"
        };

        public string[] FileOperationOptions => _fileOperationOptions;
        public string[] FileEncodingOptions => _fileEncodingOptions;
        public string[] FileReadModeOptions => _fileReadModeOptions;
        public string[] FileWriteModeOptions => _fileWriteModeOptions;

        public string FileOperation
        {
            get => GetSettingValue("Operation", "Read");
            set
            {
                SetSettingValue("Operation", value);
                OnPropertyChanged(nameof(FileOperation));
                OnPropertyChanged(nameof(FileShowSourcePath));
                OnPropertyChanged(nameof(FileShowDestinationPath));
                OnPropertyChanged(nameof(FileShowContent));
                OnPropertyChanged(nameof(FileShowEncoding));
                OnPropertyChanged(nameof(FileShowOverwrite));
                OnPropertyChanged(nameof(FileShowFileFilter));
                OnPropertyChanged(nameof(FileShowOutputVariable));
                OnPropertyChanged(nameof(FileShowRecursive));
                OnPropertyChanged(nameof(FileShowIncludeMetadata));
                OnPropertyChanged(nameof(FileShowSourceFileBrowse));
                OnPropertyChanged(nameof(FileShowSourceFolderBrowse));
                OnPropertyChanged(nameof(FileShowDestinationFileBrowse));
                OnPropertyChanged(nameof(FileShowDestinationFolderBrowse));
                OnPropertyChanged(nameof(FileShowDestinationFolder));
                OnPropertyChanged(nameof(FileShowDestinationFileName));
                OnPropertyChanged(nameof(FileShowAppend));
                OnPropertyChanged(nameof(FileShowReadMode));
                OnPropertyChanged(nameof(FileShowSelectedFilesBrowse));
                OnPropertyChanged(nameof(FileShowWriteMode));
            }
        }

        public string FileSourcePath
        {
            get => GetSettingValue("SourcePath", string.Empty);
            set
            {
                SetSettingValue("SourcePath", value);
                OnPropertyChanged(nameof(FileSourcePath));
                OnPropertyChanged(nameof(FileSourcePathResolved));
            }
        }

        public string FileSourcePathResolved => ResolveWithProjectVariables(FileSourcePath);

        public string FileDestinationPath
        {
            get => GetSettingValue("DestinationPath", string.Empty);
            set
            {
                SetSettingValue("DestinationPath", value);
                OnPropertyChanged(nameof(FileDestinationPath));
                OnPropertyChanged(nameof(FileDestinationPathResolved));
            }
        }

        public string FileDestinationPathResolved => ResolveWithProjectVariables(FileDestinationPath);

        public string FileContent
        {
            get => GetSettingValue("Content", string.Empty);
            set
            {
                SetSettingValue("Content", value);
                OnPropertyChanged(nameof(FileContent));
            }
        }

        public string FileEncoding
        {
            get => GetSettingValue("Encoding", "UTF-8");
            set
            {
                SetSettingValue("Encoding", value);
                OnPropertyChanged(nameof(FileEncoding));
            }
        }

        public bool FileOverwrite
        {
            get => GetSettingBoolValue("Overwrite", false);
            set
            {
                SetSettingValue("Overwrite", value.ToString());
                OnPropertyChanged(nameof(FileOverwrite));
                if (value)
                {
                    // If Overwrite is set to true, set Append to false
                    SetSettingValue("Append", "false");
                    OnPropertyChanged(nameof(FileAppend));
                }
            }
        }

        public string FileFilter
        {
            get => GetSettingValue("FileFilter", "*.*");
            set
            {
                SetSettingValue("FileFilter", value);
                OnPropertyChanged(nameof(FileFilter));
            }
        }

        public string FileOutputVariable
        {
            get => GetSettingValue("OutputVariable", string.Empty);
            set
            {
                SetSettingValue("OutputVariable", value);
                OnPropertyChanged(nameof(FileOutputVariable));
            }
        }

        public bool FileRecursive
        {
            get => GetSettingBoolValue("Recursive", false);
            set
            {
                SetSettingValue("Recursive", value.ToString());
                OnPropertyChanged(nameof(FileRecursive));
            }
        }

        public bool FileIncludeMetadata
        {
            get => GetSettingBoolValue("IncludeMetadata", false);
            set
            {
                SetSettingValue("IncludeMetadata", value.ToString());
                OnPropertyChanged(nameof(FileIncludeMetadata));
            }
        }

        public string FileWriteMode
        {
            get => GetSettingValue("WriteMode", "Overwrite");
            set
            {
                SetSettingValue("WriteMode", value);
                OnPropertyChanged(nameof(FileWriteMode));
            }
        }

        public string FileDestinationFolder
        {
            get => GetSettingValue("DestinationFolder", string.Empty);
            set
            {
                SetSettingValue("DestinationFolder", value);
                OnPropertyChanged(nameof(FileDestinationFolder));
            }
        }

        public string FileDestinationFileName
        {
            get => GetSettingValue("DestinationFileName", string.Empty);
            set
            {
                SetSettingValue("DestinationFileName", value);
                OnPropertyChanged(nameof(FileDestinationFileName));
            }
        }

        public bool FileAppend
        {
            get => GetSettingBoolValue("Append", false);
            set
            {
                SetSettingValue("Append", value.ToString());
                OnPropertyChanged(nameof(FileAppend));
                if (value)
                {
                    // If Append is set to true, set Overwrite to false
                    SetSettingValue("Overwrite", "false");
                    OnPropertyChanged(nameof(FileOverwrite));
                }
            }
        }

        public string FileReadMode
        {
            get => GetSettingValue("ReadMode", "All");
            set
            {
                SetSettingValue("ReadMode", value);
                OnPropertyChanged(nameof(FileReadMode));
                OnPropertyChanged(nameof(FileShowSelectedFilesBrowse));
            }
        }

        public string FileSelectedFilePaths
        {
            get => GetSettingValue("SelectedFilePaths", "[]");
            set
            {
                SetSettingValue("SelectedFilePaths", value);
                OnPropertyChanged(nameof(FileSelectedFilePaths));
            }
        }

        // Visibility properties based on selected operation
        public bool FileShowSourcePath => 
            string.Equals(FileOperation, "Read", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Write", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Copy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Move", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Delete", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "List", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFolder", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "ReadFiles", StringComparison.OrdinalIgnoreCase);

        public bool FileShowDestinationPath => false; // Hidden, using DestinationFolder and DestinationFileName instead

        public bool FileShowContent => 
            string.Equals(FileOperation, "Write", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFile", StringComparison.OrdinalIgnoreCase);

        public bool FileShowEncoding => 
            string.Equals(FileOperation, "Read", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Write", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "ReadFiles", StringComparison.OrdinalIgnoreCase);

        public bool FileShowOverwrite => 
            string.Equals(FileOperation, "Write", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Copy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Move", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFile", StringComparison.OrdinalIgnoreCase);

        public bool FileShowFileFilter => 
            string.Equals(FileOperation, "List", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "ReadFiles", StringComparison.OrdinalIgnoreCase);

        public bool FileShowOutputVariable => 
            string.Equals(FileOperation, "Read", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "List", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "ReadFiles", StringComparison.OrdinalIgnoreCase);

        public bool FileShowRecursive => 
            string.Equals(FileOperation, "List", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "ReadFiles", StringComparison.OrdinalIgnoreCase);

        public bool FileShowIncludeMetadata => 
            string.Equals(FileOperation, "Read", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "List", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "ReadFiles", StringComparison.OrdinalIgnoreCase);

        public bool FileShowWriteMode => 
            string.Equals(FileOperation, "Write", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFile", StringComparison.OrdinalIgnoreCase);

        // Browse button visibility
        public bool FileShowSourceFileBrowse => 
            string.Equals(FileOperation, "Read", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Write", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Copy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Move", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Delete", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFile", StringComparison.OrdinalIgnoreCase);

        public bool FileShowSourceFolderBrowse => 
            string.Equals(FileOperation, "List", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFolder", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "ReadFiles", StringComparison.OrdinalIgnoreCase);

        public bool FileShowDestinationFileBrowse => 
            string.Equals(FileOperation, "Copy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Move", StringComparison.OrdinalIgnoreCase);

        public bool FileShowDestinationFolderBrowse => false; // Not needed for now

        public bool FileShowDestinationFolder => 
            string.Equals(FileOperation, "Copy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Move", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFile", StringComparison.OrdinalIgnoreCase);

        public bool FileShowDestinationFileName => 
            string.Equals(FileOperation, "Copy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "Move", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFile", StringComparison.OrdinalIgnoreCase);

        public bool FileShowAppend => 
            string.Equals(FileOperation, "Write", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FileOperation, "CreateFile", StringComparison.OrdinalIgnoreCase);

        public bool FileShowReadMode => 
            string.Equals(FileOperation, "ReadFiles", StringComparison.OrdinalIgnoreCase);

        public bool FileShowSelectedFilesBrowse => 
            string.Equals(FileOperation, "ReadFiles", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(FileReadMode, "Selected", StringComparison.OrdinalIgnoreCase);

        public string FileResultPreview
        {
            get
            {
                // This could be populated after execution, for now return placeholder
                return "Result will appear here after execution.";
            }
        }

        #endregion

        #region Json Structure Builder

        private ObservableCollection<JsonStructureNode> _jsonStructureNodes = new();

        public ObservableCollection<JsonStructureNode> JsonStructureNodes
        {
            get
            {
                if (!_jsonStructureLoaded)
                {
                    LoadJsonStructureFromSettings();
                    _jsonStructureLoaded = true;
                }
                return _jsonStructureNodes;
            }
        }

        private bool _jsonStructureLoaded;

        private void LoadJsonStructureFromSettings()
        {
            if (_jsonStructureLoaded) return;
            _jsonStructureLoaded = true;

            var jsonText = RandomJsonStructure;
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                // Add a default node
                _jsonStructureNodes.Add(new JsonStructureNode("id", "guid", 0));
                _jsonStructureNodes.Add(new JsonStructureNode("name", "fullname", 0));
                _jsonStructureNodes.Add(new JsonStructureNode("email", "email", 0));
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                ParseJsonElement(doc.RootElement, _jsonStructureNodes, 0);
            }
            catch
            {
                _jsonStructureNodes.Add(new JsonStructureNode("id", "guid", 0));
                _jsonStructureNodes.Add(new JsonStructureNode("name", "fullname", 0));
            }
        }

        private void ParseJsonElement(JsonElement element, ObservableCollection<JsonStructureNode> collection, int nestingLevel)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    var node = new JsonStructureNode { Key = prop.Name, NestingLevel = nestingLevel };
                    
                    switch (prop.Value.ValueKind)
                    {
                        case JsonValueKind.Object:
                            node.ValueType = "object";
                            ParseJsonElement(prop.Value, node.Children, nestingLevel + 1);
                            break;
                        
                        case JsonValueKind.Array:
                            node.ValueType = "array";
                            node.ArrayLength = prop.Value.GetArrayLength();
                            if (prop.Value.EnumerateArray().FirstOrDefault().ValueKind == JsonValueKind.Object)
                            {
                                node.ItemType = "object";
                                if (prop.Value.GetArrayLength() > 0)
                                {
                                    ParseJsonElement(prop.Value[0], node.ArrayItemChildren, nestingLevel + 1);
                                }
                            }
                            else
                            {
                                node.ItemType = "string";
                            }
                            break;
                        
                        case JsonValueKind.String:
                            var strVal = prop.Value.GetString() ?? "";
                            node.ValueType = strVal.StartsWith("__type:") ? strVal.Substring(7) : "string";
                            break;
                        
                        case JsonValueKind.Number:
                            node.ValueType = "integer";
                            break;
                        
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            node.ValueType = "boolean";
                            break;
                        
                        default:
                            node.ValueType = "string";
                            break;
                    }
                    
                    collection.Add(node);
                }
            }
        }

        public void AddJsonKey(JsonStructureNode? parent = null, bool isArrayItem = false)
        {
            var target = parent != null ? (isArrayItem ? parent.ArrayItemChildren : parent.Children) : _jsonStructureNodes;
            var nestingLevel = parent?.NestingLevel ?? 0;
            if (isArrayItem) nestingLevel++;
            target.Add(new JsonStructureNode($"key{target.Count + 1}", "string", nestingLevel, isArrayItem));
            SaveJsonStructureToSettings();
        }

        public void AddJsonNestedObject(JsonStructureNode parent, bool isArrayItem = false)
        {
            var target = isArrayItem ? parent.ArrayItemChildren : parent.Children;
            var nestingLevel = parent.NestingLevel + 1;
            var newNode = new JsonStructureNode($"nested{target.Count + 1}", "object", nestingLevel, isArrayItem);
            newNode.Children.Add(new JsonStructureNode("subKey", "string", nestingLevel + 1));
            target.Add(newNode);
            parent.IsExpanded = true;
            SaveJsonStructureToSettings();
        }

        public void AddJsonArray(JsonStructureNode parent, bool isArrayItem = false)
        {
            var target = isArrayItem ? parent.ArrayItemChildren : parent.Children;
            var nestingLevel = parent.NestingLevel + 1;
            var newNode = new JsonStructureNode($"array{target.Count + 1}", "array", nestingLevel, isArrayItem);
            newNode.ItemType = "string";
            newNode.ArrayLength = 3;
            target.Add(newNode);
            parent.IsExpanded = true;
            SaveJsonStructureToSettings();
        }

        public void RemoveJsonNode(JsonStructureNode node)
        {
            // Find and remove from the appropriate collection
            foreach (var root in _jsonStructureNodes)
            {
                if (RemoveNodeFromCollection(root, node))
                {
                    SaveJsonStructureToSettings();
                    return;
                }
            }
            _jsonStructureNodes.Remove(node);
            SaveJsonStructureToSettings();
        }

        private bool RemoveNodeFromCollection(JsonStructureNode parent, JsonStructureNode target)
        {
            if (parent.Children.Remove(target)) return true;
            if (parent.ArrayItemChildren.Remove(target)) return true;
            
            foreach (var child in parent.Children)
            {
                if (RemoveNodeFromCollection(child, target)) return true;
            }
            foreach (var child in parent.ArrayItemChildren)
            {
                if (RemoveNodeFromCollection(child, target)) return true;
            }
            
            return false;
        }

        public void SaveJsonStructureToSettings()
        {
            try
            {
                var dict = new Dictionary<string, object>();
                foreach (var node in _jsonStructureNodes)
                {
                    dict[node.Key] = BuildTemplateValue(node);
                }
                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                RandomJsonStructure = json;
                OnPropertyChanged(nameof(RandomJsonStructure));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveJsonStructureToSettings error: {ex.Message}");
            }
        }

        private object BuildTemplateValue(JsonStructureNode node)
        {
            switch (node.ValueType)
            {
                case "object":
                    var objDict = new Dictionary<string, object>();
                    foreach (var child in node.Children)
                    {
                        objDict[child.Key] = BuildTemplateValue(child);
                    }
                    return objDict;

                case "array":
                    var arrayDict = new Dictionary<string, object>
                    {
                        ["__isArray"] = true,
                        ["__length"] = node.ArrayLength,
                        ["__itemType"] = node.ItemType
                    };

                    if (node.ItemType == "object")
                    {
                        var itemsDict = new Dictionary<string, object>();
                        if (node.ArrayItemChildren.Count > 0)
                        {
                            foreach (var child in node.ArrayItemChildren)
                            {
                                itemsDict[child.Key] = BuildTemplateValue(child);
                            }
                        }
                        else
                        {
                            // Add default keys for object array items
                            itemsDict["id"] = "__type:guid";
                            itemsDict["value"] = "__type:string";
                        }
                        arrayDict["__items"] = itemsDict;
                    }
                    else
                    {
                        arrayDict["__items"] = node.ItemType;
                    }
                    return arrayDict;

                default:
                    return $"__type:{node.ValueType}";
            }
        }

        #endregion

        #region JSON Structure Builder Event Handlers

        private void AddJsonKey_Click(object sender, RoutedEventArgs e)
        {
            AddJsonKey();
            OnPropertyChanged(nameof(JsonStructureNodes));
            OnPropertyChanged(nameof(RandomJsonStructure));
        }

        private void AddJsonObject_Click(object sender, RoutedEventArgs e)
        {
            var newNode = new JsonStructureNode($"object{_jsonStructureNodes.Count + 1}", "object", 0);
            newNode.Children.Add(new JsonStructureNode("key1", "string", 1));
            _jsonStructureNodes.Add(newNode);
            SaveJsonStructureToSettings();
            OnPropertyChanged(nameof(JsonStructureNodes));
        }

        private void AddJsonArray_Click(object sender, RoutedEventArgs e)
        {
            var newNode = new JsonStructureNode($"array{_jsonStructureNodes.Count + 1}", "array", 0);
            newNode.ItemType = "string";
            newNode.ArrayLength = 3;
            _jsonStructureNodes.Add(newNode);
            SaveJsonStructureToSettings();
            OnPropertyChanged(nameof(JsonStructureNodes));
        }

        private void AddArrayItemKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is JsonStructureNode parent)
            {
                parent.ArrayItemChildren.Add(new JsonStructureNode($"itemKey{parent.ArrayItemChildren.Count + 1}", "string", parent.NestingLevel + 1, true));
                parent.IsExpanded = true;
                SaveJsonStructureToSettings();
                OnPropertyChanged(nameof(JsonStructureNodes));
                OnPropertyChanged(nameof(RandomJsonStructure));
            }
        }

        private void ClearJsonStructure_Click(object sender, RoutedEventArgs e)
        {
            _jsonStructureNodes.Clear();
            SaveJsonStructureToSettings();
            OnPropertyChanged(nameof(JsonStructureNodes));
            OnPropertyChanged(nameof(RandomJsonStructure));
        }

        private void RefreshJsonStructure()
        {
            _jsonStructureLoaded = false;
            _jsonStructureNodes.Clear();
            OnPropertyChanged(nameof(JsonStructureNodes));
        }

        private void ToggleExpand_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is JsonStructureNode node)
            {
                node.IsExpanded = !node.IsExpanded;
            }
        }

        private void AddChildKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is JsonStructureNode parent)
            {
                parent.Children.Add(new JsonStructureNode($"key{parent.Children.Count + 1}", "string", parent.NestingLevel + 1));
                parent.IsExpanded = true;
                SaveJsonStructureToSettings();
                OnPropertyChanged(nameof(JsonStructureNodes));
                OnPropertyChanged(nameof(RandomJsonStructure));
            }
        }

        private void AddNestedObject_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is JsonStructureNode parent)
            {
                var newObj = new JsonStructureNode($"object{parent.Children.Count + 1}", "object", parent.NestingLevel + 1);
                newObj.Children.Add(new JsonStructureNode("subKey", "string", parent.NestingLevel + 2));
                parent.Children.Add(newObj);
                parent.IsExpanded = true;
                SaveJsonStructureToSettings();
                OnPropertyChanged(nameof(JsonStructureNodes));
                OnPropertyChanged(nameof(RandomJsonStructure));
            }
        }

        private void AddNestedArray_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is JsonStructureNode parent)
            {
                var newArray = new JsonStructureNode($"array{parent.Children.Count + 1}", "array", parent.NestingLevel + 1);
                newArray.ItemType = "string";
                newArray.ArrayLength = 3;
                parent.Children.Add(newArray);
                parent.IsExpanded = true;
                SaveJsonStructureToSettings();
                OnPropertyChanged(nameof(JsonStructureNodes));
                OnPropertyChanged(nameof(RandomJsonStructure));
            }
        }

        private void AddArrayItemChild_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is JsonStructureNode parent)
            {
                parent.ArrayItemChildren.Add(new JsonStructureNode($"itemKey{parent.ArrayItemChildren.Count + 1}", "string", parent.NestingLevel + 1, true));
                SaveJsonStructureToSettings();
                OnPropertyChanged(nameof(JsonStructureNodes));
            }
        }

        private void RemoveJsonNode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is JsonStructureNode node)
            {
                RemoveJsonNode(node);
                OnPropertyChanged(nameof(JsonStructureNodes));
                OnPropertyChanged(nameof(RandomJsonStructure));
            }
        }

        private void RemoveArrayItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is JsonStructureNode node)
            {
                // Find parent and remove
                foreach (var root in _jsonStructureNodes)
                {
                    if (RemoveArrayItemFromParent(root, node))
                    {
                        SaveJsonStructureToSettings();
                        OnPropertyChanged(nameof(JsonStructureNodes));
                        OnPropertyChanged(nameof(RandomJsonStructure));
                        return;
                    }
                }
            }
        }

        private bool RemoveArrayItemFromParent(JsonStructureNode parent, JsonStructureNode target)
        {
            if (parent.ArrayItemChildren.Remove(target)) return true;
            
            foreach (var child in parent.Children)
            {
                if (RemoveArrayItemFromParent(child, target)) return true;
            }
            
            return false;
        }

        #endregion

        private Point _dragStartPoint;
        private PlanNode? _draggedNode;
        private bool _isSyncingAssertionTreeSource;
        private bool _isSyncingWhileConditionRows;
        private string _assertionTreeSource = "PreviewVariables";

        private sealed class AssertionTreeNodeTag
        {
            public string Source { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string Expected { get; set; } = string.Empty;
            public bool IsLeaf { get; set; }
        }

        private static readonly string[] StepTypes =
        {
            "Http", "GraphQl", "Sql", "Dataset", "Assert", "VariableExtractor", "Script", "Timer", "RandomGenerator", "Base64", "While", "File", "Excel"
        };

        public MainWindow()
        {
            InitializeComponent();
            _logFlushTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _logFlushTimer.Tick += LogFlushTimer_Tick;
            _logFlushTimer.Start();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            SizeChanged += MainWindow_SizeChanged;
            RootNodes.CollectionChanged += RootNodes_CollectionChanged;
            UpdateWindowTitle();
            RefreshSqlAuthTypeOptions();
            RefreshEnvironmentOptions();
            RebuildVariableUsageMap();
            RefreshJsonPreview();
            RefreshApiCatalogState();
            WhileConditionRows.CollectionChanged += WhileConditionRows_CollectionChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadMainLayoutState();
            RestoreLastProjectIfAvailable();
            Dispatcher.BeginInvoke(new Action(EnforceMainLayoutMinimums), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            SaveMainLayoutState();
            SaveAppState();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            EnforceMainLayoutMinimums();
        }

        private void EnforceMainLayoutMinimums()
        {
            if (_isApplyingMainLayoutBounds || MainContentGrid == null)
            {
                return;
            }

            try
            {
                _isApplyingMainLayoutBounds = true;

                const double topMin = 240;
                const double bottomMin = 80;

                var splitterHeight = 6d;
                var available = Math.Max(MainContentGrid.ActualHeight - splitterHeight, 0);
                if (available <= 0)
                {
                    return;
                }

                var desiredTop = EditorTopRow.ActualHeight;
                var desiredBottom = EditorBottomRow.ActualHeight;
                if (double.IsNaN(desiredTop) || desiredTop <= 0)
                {
                    desiredTop = topMin;
                }

                if (double.IsNaN(desiredBottom) || desiredBottom <= 0)
                {
                    desiredBottom = bottomMin;
                }

                if (available >= topMin + bottomMin)
                {
                    desiredTop = Math.Max(desiredTop, topMin);
                    desiredBottom = Math.Max(desiredBottom, bottomMin);

                    var total = desiredTop + desiredBottom;
                    if (total > available)
                    {
                        desiredTop = Math.Max(topMin, available - desiredBottom);
                        desiredBottom = Math.Max(bottomMin, available - desiredTop);
                    }
                }
                else
                {
                    // Window too small for ideal mins: keep preview visible with a protected floor.
                    var protectedBottom = Math.Max(80, available * 0.2);
                    desiredBottom = Math.Min(available, protectedBottom);
                    desiredTop = Math.Max(0, available - desiredBottom);
                }

                EditorTopRow.Height = new GridLength(Math.Max(desiredTop, 0), GridUnitType.Pixel);
                EditorBottomRow.Height = new GridLength(Math.Max(desiredBottom, 0), GridUnitType.Pixel);
            }
            finally
            {
                _isApplyingMainLayoutBounds = false;
            }
        }

        private void LoadMainLayoutState()
        {
            try
            {
                if (!File.Exists(MainLayoutStatePath))
                {
                    return;
                }

                var json = File.ReadAllText(MainLayoutStatePath);
                var state = JsonSerializer.Deserialize<MainLayoutState>(json);
                if (state == null)
                {
                    return;
                }

                ApplyPixelLength(MainLeftColumn, state.LeftColumnWidth, min: 220);
                ApplyPixelLength(LeftTopControlsRow, state.LeftTopRowHeight, min: 140);
                ApplyPixelLength(EditorTopRow, state.EditorTopRowHeight, min: 280);
                ApplyPixelLength(EditorBottomRow, state.EditorBottomRowHeight, min: 80);
                ApplyPixelLength(AssertionRowsAreaRow, state.AssertionRowsHeight, min: 100);
                ApplyPixelLength(AssertionTreeAreaRow, state.AssertionTreeHeight, min: 220);
                ApplyPixelLength(AssertionTreeLeftColumn, state.AssertionTreeLeftWidth, min: 160);
                ApplyPixelLength(AssertionTreeRightColumn, state.AssertionTreeRightWidth, min: 220);
            }
            catch
            {
                // Ignore corrupt or inaccessible layout files and continue with defaults.
            }
        }

        private void SaveMainLayoutState()
        {
            try
            {
                var state = new MainLayoutState
                {
                    LeftColumnWidth = MainLeftColumn.ActualWidth,
                    LeftTopRowHeight = LeftTopControlsRow.ActualHeight,
                    EditorTopRowHeight = EditorTopRow.ActualHeight,
                    EditorBottomRowHeight = EditorBottomRow.ActualHeight,
                    AssertionRowsHeight = AssertionRowsAreaRow.ActualHeight,
                    AssertionTreeHeight = AssertionTreeAreaRow.ActualHeight,
                    AssertionTreeLeftWidth = AssertionTreeLeftColumn.ActualWidth,
                    AssertionTreeRightWidth = AssertionTreeRightColumn.ActualWidth
                };

                var directory = Path.GetDirectoryName(MainLayoutStatePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(state, PrettyJsonOptions);
                File.WriteAllText(MainLayoutStatePath, json);
            }
            catch
            {
                // Ignore save issues to avoid interrupting app shutdown.
            }
        }

        private void RestoreLastProjectIfAvailable()
        {
            try
            {
                if (!File.Exists(AppStatePath))
                {
                    return;
                }

                var json = File.ReadAllText(AppStatePath);
                var state = JsonSerializer.Deserialize<AppState>(json);
                var lastPath = state?.LastProjectFilePath;
                if (string.IsNullOrWhiteSpace(lastPath))
                {
                    return;
                }

                if (!File.Exists(lastPath))
                {
                    MessageBox.Show(
                        $"Last project file was not found:\n{lastPath}",
                        "Last Project",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    _currentProjectFilePath = null;
                OnPropertyChanged(nameof(CurrentProjectFilePath));
                    UpdateWindowTitle();
                    SaveAppState();
                    return;
                }

                TryLoadProjectFromFile(lastPath, "Last Project");
            }
            catch
            {
                // Ignore corrupt or inaccessible app state and continue with defaults.
            }
        }

        private void SaveAppState()
        {
            try
            {
                var state = new AppState
                {
                    LastProjectFilePath = _currentProjectFilePath
                };

                var directory = Path.GetDirectoryName(AppStatePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(state, PrettyJsonOptions);
                File.WriteAllText(AppStatePath, json);
            }
            catch
            {
                // Ignore save issues to avoid interrupting app behavior.
            }
        }

        private bool TryLoadProjectFromFile(string filePath, string dialogTitle)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var model = JsonSerializer.Deserialize<ProjectFileModel>(json);

                if (model?.Project == null)
                {
                    MessageBox.Show("Invalid project file format.", dialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                var root = FromFileModel(model.Project, null);
                if (root.Type != "Project")
                {
                    MessageBox.Show("Root node must be of type Project.", dialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                RootNodes.Clear();
                RootNodes.Add(root);
                SelectedNode = root;
                _currentProjectFilePath = filePath;
                OnPropertyChanged(nameof(CurrentProjectFilePath));
                UpdateWindowTitle();
                RefreshEnvironmentOptions();
                RefreshJsonPreview();
                UpdateProjectVariablesPreview();
                SaveAppState();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load project: {ex.Message}", dialogTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void ApplyPixelLength(ColumnDefinition definition, double value, double min)
        {
            if (definition == null || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return;
            }

            definition.Width = new GridLength(Math.Max(value, min), GridUnitType.Pixel);
        }

        private static void ApplyPixelLength(RowDefinition definition, double value, double min)
        {
            if (definition == null || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return;
            }

            definition.Height = new GridLength(Math.Max(value, min), GridUnitType.Pixel);
        }

        private void StopRunButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRunInProgress)
            {
                return;
            }

            var requested = false;
            if (_activeExecutionContext != null)
            {
                _activeExecutionContext.Status = "stopping";
                _activeExecutionContext.RequestStop();
                requested = true;
            }

            foreach (var context in _activeProjectExecutionContexts)
            {
                context.Status = "stopping";
                context.RequestStop();
                requested = true;
            }

            if (!requested)
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            PreviewLogs = string.Join("\n", new[]
            {
                PreviewLogs,
                $"[{timestamp}] Stop requested by user. Finishing current step..."
            });
        }

        private void SetRunState(
            bool isRunning,
            Test_Automation.Models.ExecutionContext? context = null,
            IEnumerable<Test_Automation.Models.ExecutionContext>? contexts = null)
        {
            _isRunInProgress = isRunning;
            _activeExecutionContext = isRunning ? context : null;
            _activeProjectExecutionContexts.Clear();
            if (isRunning && contexts != null)
            {
                foreach (var runContext in contexts)
                {
                    _activeProjectExecutionContexts.Add(runContext);
                }
            }

            if (RunTestPlanButton != null)
            {
                RunTestPlanButton.IsEnabled = !isRunning;
            }

            if (RunProjectTestPlansButton != null)
            {
                RunProjectTestPlansButton.IsEnabled = !isRunning;
            }

            if (ProjectRunModeComboBox != null)
            {
                ProjectRunModeComboBox.IsEnabled = !isRunning;
            }

            if (StopRunButton != null)
            {
                StopRunButton.IsEnabled = isRunning;
            }
        }

        private bool _isDarkTheme = false;

        private void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            App.ChangeTheme(_isDarkTheme ? "DarkTheme" : "LightTheme");
        }

        private void AddProjectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RootNodes.Any(node => node.Type == "Project"))
                {
                    MessageBox.Show("Only one Project root is allowed.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var root = new PlanNode("Project", "Project");
                RootNodes.Add(root);
                SelectedNode = root;
                _currentProjectFilePath = null;
                OnPropertyChanged(nameof(CurrentProjectFilePath));
                UpdateWindowTitle();
                SaveAppState();
                RefreshEnvironmentOptions();
                RefreshJsonPreview();
                UpdateProjectVariablesPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add project: {ex.Message}", "Add Project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetProjectNode(out var projectNode))
            {
                MessageBox.Show("Create or load a Project first.", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentProjectFilePath))
            {
                SaveProjectAsButton_Click(sender, e);
                return;
            }

            try
            {
                SaveProjectToFile(_currentProjectFilePath, projectNode);
                MessageBox.Show("Project saved successfully.", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save project: {ex.Message}", "Save Project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProjectAsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetProjectNode(out var projectNode))
            {
                MessageBox.Show("Create or load a Project first.", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = !string.IsNullOrWhiteSpace(_currentProjectFilePath)
                    ? Path.GetFileName(_currentProjectFilePath)
                    : $"{projectNode.Name}.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                SaveProjectToFile(dialog.FileName, projectNode);
                _currentProjectFilePath = dialog.FileName;
                OnPropertyChanged(nameof(CurrentProjectFilePath));
                UpdateWindowTitle();
                SaveAppState();
                MessageBox.Show("Project saved successfully.", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save project: {ex.Message}", "Save Project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void SaveProjectToFile(string filePath, PlanNode projectNode)
        {
            var model = new ProjectFileModel
            {
                Version = 1,
                Project = ToFileModel(projectNode)
            };

            var json = JsonSerializer.Serialize(model, PrettyJsonOptions);

            File.WriteAllText(filePath, json);
        }

        private bool TryGetProjectNode(out PlanNode projectNode)
        {
            var found = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (found == null)
            {
                projectNode = null!;
                return false;
            }

            projectNode = found;
            return true;
        }

        private void UpdateWindowTitle()
        {
            var suffix = string.IsNullOrWhiteSpace(_currentProjectFilePath)
                ? "Unsaved"
                : Path.GetFileName(_currentProjectFilePath);
            Title = $"Test Automation - {suffix}";
        }

        private void LoadProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            TryLoadProjectFromFile(dialog.FileName, "Load Project");
        }

        private async void RunTestPlanButton_Click(object sender, RoutedEventArgs e)
        {
            var testPlanNode = ResolveTestPlanNode();
            if (testPlanNode == null)
            {
                MessageBox.Show("Select a TestPlan node to run.", "Run TestPlan", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var testPlanComponent = BuildComponentTree(testPlanNode) as Test_Automation.Componentes.TestPlan;
            if (testPlanComponent == null)
            {
                MessageBox.Show("Unable to build the TestPlan component.", "Run TestPlan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResetAssertionStateForSubtree(testPlanNode);

            var executor = CreateExecutorWithHighlight();
            var runner = new TestPlanRunner(executor);
            var startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            PreviewLogs = $"[{startTimestamp}] Running TestPlan: {testPlanNode.Name}";
            VariablesPreview = "{}";

            try
            {
                var context = new Test_Automation.Models.ExecutionContext();
                context.ResetStopRequest();
                context.Status = "running";
                SetRunState(true, context);
                ApplyProjectVariables(context);
                ApplyTestPlanVariables(testPlanNode, context);
                SetContextHierarchicalVariables(context, testPlanNode);
                var summary = await runner.RunTestPlanWithContext(testPlanComponent, context);
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Status: {summary.Status}",
                    $"[{endTimestamp}] Total: {summary.TotalComponents}, Passed: {summary.PassedComponents}, Failed: {summary.FailedComponents}"
                });

                _lastExecutionContext = context;
                // Build hierarchical variable structure using RUNTIME context values (includes extractor-updated values)
                var projectNode = RootNodes.FirstOrDefault(n => n.Type == "Project");
                // Use runtime context values so extractor-updated variables are reflected
                var projectVariables = BuildRuntimeProjectVariables(context, projectNode);
                var testPlanVars = BuildRuntimeTestPlanVariables(context, testPlanNode);
                // Use consistent structure with "testPlans" wrapper
                var testPlansDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { testPlanNode.Name, testPlanVars }
                };
                var varsDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "projectVariables", projectVariables },
                    { "testPlans", testPlansDict }
                };
                // Store hierarchical variables in context for assertion service
                context.HierarchicalVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "projectVariables", projectVariables },
                    { "testPlans", testPlansDict }
                };
                VariablesPreview = JsonSerializer.Serialize(varsDict, PrettyJsonOptions);

                RefreshComponentPreview();
                AppendRuntimeTraceBufferToPreviewLogs();
            }
            catch (OperationCanceledException)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run stopped by user."
                });
                AppendRuntimeTraceBufferToPreviewLogs();
                UpdateProjectVariablesPreview();
            }
            catch (Exception ex)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run failed: {ex.Message}"
                });
                AppendRuntimeTraceBufferToPreviewLogs();
                UpdateProjectVariablesPreview();
            }
            finally
            {
                SetRunState(false);
            }
        }

        private async void RunProjectTestPlansButton_Click(object sender, RoutedEventArgs e)
        {
            var testPlanNodes = GetRunnableProjectTestPlans();
            if (testPlanNodes.Count == 0)
            {
                MessageBox.Show("Add at least one enabled TestPlan under the Project.", "Run Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var runnablePlans = testPlanNodes
                .Select(node => (Node: node, Component: BuildComponentTree(node) as Test_Automation.Componentes.TestPlan))
                .Where(entry => entry.Component != null)
                .Select(entry => (Node: entry.Node, Component: entry.Component!))
                .ToList();

            if (runnablePlans.Count == 0)
            {
                MessageBox.Show("Unable to build enabled TestPlan components.", "Run Project", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var plan in runnablePlans)
            {
                ResetAssertionStateForSubtree(plan.Node);
            }

            var contexts = runnablePlans.Select(_ => new Test_Automation.Models.ExecutionContext()).ToList();
            
            // Debug: Verify each context is unique
            for (var i = 0; i < contexts.Count; i++)
            {
                Logger.Log($"[DEBUG] Context[{i}] created with ID: {contexts[i].ExecutionId}", LogLevel.Verbose);
            }
            
            for (var i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                var testPlanNode = runnablePlans[i].Node;
                Logger.Log($"[DEBUG] Processing test plan '{testPlanNode.Name}' with context {context.ExecutionId}", LogLevel.Verbose);
                context.ResetStopRequest();
                context.Status = "running";
                ApplyProjectVariables(context);
                ApplyTestPlanVariables(testPlanNode, context);

                // Set hierarchical variables for assertions
                SetContextHierarchicalVariables(context, testPlanNode);

                // Debug: Verify the variable was set
                var testVar = context.GetVariable("L");
                if (testVar != null)
                {
                    Logger.Log($"[DEBUG] After ApplyTestPlanVariables, variable 'L' = '{testVar}' in context {context.ExecutionId}", LogLevel.Verbose);
                }
            }

            var mode = string.Equals(SelectedProjectRunMode, "Parallel", StringComparison.OrdinalIgnoreCase)
                ? "Parallel"
                : "Sequence";
            var startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            PreviewLogs = $"[{startTimestamp}] Running {runnablePlans.Count} TestPlan(s) in {mode} mode.";
            VariablesPreview = "{}";

            try
            {
                SetRunState(true, null, contexts);

                var plansWithContext = runnablePlans
                    .Select((plan, index) => (plan.Node, plan.Component, Context: contexts[index]))
                    .ToList();

                var summaries = string.Equals(mode, "Parallel", StringComparison.OrdinalIgnoreCase)
                    ? await RunProjectPlansParallelAsync(plansWithContext)
                    : await RunProjectPlansSequentialAsync(plansWithContext);

                var totalComponents = summaries.Sum(entry => entry.Summary.TotalComponents);
                var passedComponents = summaries.Sum(entry => entry.Summary.PassedComponents);
                var failedComponents = summaries.Sum(entry => entry.Summary.FailedComponents);
                var anyStopped = plansWithContext.Any(entry => string.Equals(entry.Context.Status, "stopped", StringComparison.OrdinalIgnoreCase));
                var status = anyStopped
                    ? "stopped"
                    : (failedComponents > 0 ? "failed" : "passed");
                var executedPlans = summaries.Count();

                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Status: {status}",
                    $"[{endTimestamp}] Total Components: {totalComponents}, Passed: {passedComponents}, Failed: {failedComponents}",
                    $"[{endTimestamp}] TestPlans Run: {executedPlans}"
                }.Concat(summaries.Select(entry =>
                    $"[{endTimestamp}] - {entry.Node.Name}: {entry.Summary.Status} (Total: {entry.Summary.TotalComponents}, Passed: {entry.Summary.PassedComponents}, Failed: {entry.Summary.FailedComponents})")));

                var mergedVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var context in contexts)
                {
                    foreach (var variable in context.Variables)
                    {
                        mergedVariables[variable.Key] = variable.Value;
                    }
                }

                var mergedExecutionContext = new Test_Automation.Models.ExecutionContext
                {
                    Status = status,
                    IsRunning = false,
                    Results = contexts
                        .SelectMany(context => context.Results)
                        .OrderBy(result => result.StartTime)
                        .ToList()
                };

                foreach (var variable in mergedVariables)
                {
                    mergedExecutionContext.SetVariable(variable.Key, variable.Value);
                }

                _lastExecutionContext = mergedExecutionContext;
                // Build hierarchical variable structure with all testplans using RUNTIME context values
                var projectNode = RootNodes.FirstOrDefault(n => n.Type == "Project");
                // Use runtime context values so extractor-updated variables are reflected
                var projectVariables = BuildRuntimeProjectVariables(mergedExecutionContext, projectNode);
                var allTestPlanVars = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var plan in testPlanNodes)
                {
                    var tpVars = BuildRuntimeTestPlanVariables(mergedExecutionContext, plan);
                    if (tpVars.Count > 0)
                        allTestPlanVars[plan.Name] = tpVars;
                }
                // Store hierarchical variables in context for assertion service to use
                mergedExecutionContext.HierarchicalVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "projectVariables", projectVariables },
                    { "testPlans", allTestPlanVars }
                };
                VariablesPreview = JsonSerializer.Serialize(new
                {
                    projectVariables = projectVariables,
                    testPlans = allTestPlanVars
                }, PrettyJsonOptions);

                RefreshComponentPreview();
                AppendRuntimeTraceBufferToPreviewLogs();
            }
            catch (OperationCanceledException)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run stopped by user."
                });
                AppendRuntimeTraceBufferToPreviewLogs();
                UpdateProjectVariablesPreview();
            }
            catch (Exception ex)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run failed: {ex.Message}"
                });
                AppendRuntimeTraceBufferToPreviewLogs();
                UpdateProjectVariablesPreview();
            }
            finally
            {
                SetRunState(false);
            }
        }

        private List<PlanNode> GetRunnableProjectTestPlans()
        {
            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (projectNode == null)
            {
                return new List<PlanNode>();
            }

            // Get all enabled TestPlans
            var testPlans = projectNode.Children
                .Where(node => string.Equals(node.Type, "TestPlan", StringComparison.OrdinalIgnoreCase) && node.IsEnabled)
                .ToList();

            // Sort by ExecutionType: PreExecute first, then Normal, then PostExecute
            // Within each type, maintain the original order
            return testPlans
                .OrderBy(node => node.ExecutionType == ExecutionType.PostExecute ? 2 : (node.ExecutionType == ExecutionType.Normal ? 1 : 0))
                .ToList();
        }

        private async Task<List<(PlanNode Node, ExecutionSummary Summary)>> RunProjectPlansSequentialAsync(
            IReadOnlyList<(PlanNode Node, Test_Automation.Componentes.TestPlan Component, Test_Automation.Models.ExecutionContext Context)> plans)
        {
            var summaries = new List<(PlanNode Node, ExecutionSummary Summary)>();
            for (var index = 0; index < plans.Count; index++)
            {
                var plan = plans[index];
                if (plan.Context.StopToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(plan.Context.StopToken);
                }

                var runner = new TestPlanRunner(CreateExecutorWithHighlight());
                var summary = await runner.RunTestPlanWithContext(plan.Component, plan.Context);
                summaries.Add((plan.Node, summary));

                if (string.Equals(plan.Context.Status, "stopped", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(plan.Context.Status, "stopping", StringComparison.OrdinalIgnoreCase))
                {
                    throw new OperationCanceledException(plan.Context.StopToken);
                }
            }

            return summaries;
        }

        private async Task<List<(PlanNode Node, ExecutionSummary Summary)>> RunProjectPlansParallelAsync(
            IReadOnlyList<(PlanNode Node, Test_Automation.Componentes.TestPlan Component, Test_Automation.Models.ExecutionContext Context)> plans)
        {
            var summaries = new List<(PlanNode Node, ExecutionSummary Summary)>();

            // Split plans by execution type
            var preExecutePlans = plans.Where(p => p.Node.ExecutionType == ExecutionType.PreExecute).ToList();
            var normalPlans = plans.Where(p => p.Node.ExecutionType == ExecutionType.Normal).ToList();
            var postExecutePlans = plans.Where(p => p.Node.ExecutionType == ExecutionType.PostExecute).ToList();

            // Step 1: Run PreExecute plans sequentially (in order)
            foreach (var plan in preExecutePlans)
            {
                if (plan.Context.StopToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(plan.Context.StopToken);
                }
                var runner = new TestPlanRunner(CreateExecutorWithHighlight());
                var summary = await runner.RunTestPlanWithContext(plan.Component, plan.Context);
                summaries.Add((plan.Node, summary));
            }

            // Step 2: Run Normal plans in parallel
            if (normalPlans.Count > 0)
            {
                var normalTasks = normalPlans.Select(async plan =>
                {
                    var runner = new TestPlanRunner(CreateExecutorWithHighlight());
                    var summary = await runner.RunTestPlanWithContext(plan.Component, plan.Context);
                    return (plan.Node, Summary: summary, plan.Context);
                });

                var normalResults = await Task.WhenAll(normalTasks);
                if (normalResults.Any(result => string.Equals(result.Context.Status, "stopped", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(result.Context.Status, "stopping", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new OperationCanceledException();
                }

                summaries.AddRange(normalResults.Select(r => (r.Node, r.Summary)));
            }

            // Step 3: Run PostExecute plans sequentially (in order)
            foreach (var plan in postExecutePlans)
            {
                if (plan.Context.StopToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(plan.Context.StopToken);
                }
                var runner = new TestPlanRunner(CreateExecutorWithHighlight());
                var summary = await runner.RunTestPlanWithContext(plan.Component, plan.Context);
                summaries.Add((plan.Node, summary));
            }

            return summaries;
        }

        private void PlanTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = FindParentTreeViewItem(e.OriginalSource as DependencyObject);
            if (item == null)
            {
                return;
            }

            item.IsSelected = true;
            if (item.DataContext is not PlanNode selectedNode)
            {
                return;
            }

            item.ContextMenu = BuildContextMenuForNode(selectedNode);
        }

        private ContextMenu BuildContextMenuForNode(PlanNode selectedNode)
        {
            var menu = new ContextMenu();

            foreach (var childType in GetAllowedChildren(selectedNode.Type))
            {
                var addItem = new MenuItem { Header = $"Add {childType}" };
                addItem.Click += (_, _) => AddChildNode(selectedNode, childType);
                menu.Items.Add(addItem);
            }

            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new Separator());
            }

            if (!string.Equals(selectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                var runItem = new MenuItem { Header = "Run (This + Children)" };
                runItem.Click += async (_, _) => await RunSelectedNodeWithChildrenAsync(selectedNode);
                menu.Items.Add(runItem);
                var clearPreviewItem = new MenuItem { Header = "Clear Preview (This + Children)" };
                clearPreviewItem.Click += (_, _) => ClearSelectedNodePreviewWithChildren(selectedNode);
                menu.Items.Add(clearPreviewItem);
                menu.Items.Add(new Separator());
            }
            else
            {
                var runSeqItem = new MenuItem { Header = "Run in Sequence" };
                runSeqItem.Click += (_, _) => 
                {
                    SelectedProjectRunMode = "Sequence";
                    RunProjectTestPlansButton_Click(this, new RoutedEventArgs());
                };
                menu.Items.Add(runSeqItem);

                var runParItem = new MenuItem { Header = "Run in Parallel" };
                runParItem.Click += (_, _) => 
                {
                    SelectedProjectRunMode = "Parallel";
                    RunProjectTestPlansButton_Click(this, new RoutedEventArgs());
                };
                menu.Items.Add(runParItem);
                
                var clearPreviewItem = new MenuItem { Header = "Clear Preview (This + Children)" };
                clearPreviewItem.Click += (_, _) => ClearSelectedNodePreviewWithChildren(selectedNode);
                menu.Items.Add(clearPreviewItem);
                
                menu.Items.Add(new Separator());
            }

            var cloneItem = new MenuItem { Header = "Clone" };
            cloneItem.Click += (_, _) => CloneNode(selectedNode);
            menu.Items.Add(cloneItem);

            var removeItem = new MenuItem { Header = "Delete" };
            removeItem.Click += (_, _) => RemoveNode(selectedNode);
            menu.Items.Add(removeItem);

            return menu;
        }

        private PlanNode? ResolveTestPlanNode()
        {
            if (SelectedNode != null)
            {
                var current = SelectedNode;
                while (current != null)
                {
                    if (current.Type == "TestPlan")
                    {
                        return current;
                    }

                    current = current.Parent;
                }
            }

            var project = RootNodes.FirstOrDefault(node => node.Type == "Project");
            return project?.Children.FirstOrDefault(node => node.Type == "TestPlan");
        }

        private async Task RunSelectedNodeWithChildrenAsync(PlanNode selectedNode)
        {
            if (selectedNode == null)
            {
                return;
            }

            if (string.Equals(selectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Select a component node to run.", "Run Component", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var component = BuildComponentTree(selectedNode);
            if (component == null)
            {
                MessageBox.Show("Unable to build the selected component.", "Run Component", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResetAssertionStateForSubtree(selectedNode);

            var executor = CreateExecutorWithHighlight();
            var startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            PreviewLogs = $"[{startTimestamp}] Running: {selectedNode.Name}";
            VariablesPreview = "{}";

            try
            {
                var context = new Test_Automation.Models.ExecutionContext();
                context.ResetStopRequest();
                context.Status = "running";
                SetRunState(true, context);
                ApplyProjectVariables(context);
                // Apply TestPlan local variables if running from within a TestPlan
                var parentTestPlan = FindParentTestPlan(selectedNode);
                if (parentTestPlan != null)
                {
                    ApplyTestPlanVariables(parentTestPlan, context);
                    SetContextHierarchicalVariables(context, parentTestPlan);
                }
                var result = await executor.ExecuteComponentTree(component, context);
                context.Results.Add(result);

                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Status: {(result.Passed ? "passed" : "failed")}",
                    $"[{endTimestamp}] Results: {context.Results.Count}"
                });

                _lastExecutionContext = context;
                // Build hierarchical variable structure using RUNTIME context values (includes extractor-updated values)
                var projectNode = RootNodes.FirstOrDefault(n => n.Type == "Project");
                // Use runtime context values so extractor-updated variables are reflected
                var projectVariables = BuildRuntimeProjectVariables(context, projectNode);
                var testPlansDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (parentTestPlan != null)
                {
                    var testPlanVars = BuildRuntimeTestPlanVariables(context, parentTestPlan);
                    testPlansDict[parentTestPlan.Name] = testPlanVars;
                }
                var varsDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "projectVariables", projectVariables },
                    { "testPlans", testPlansDict }
                };
                // Store hierarchical variables in context for assertion service
                context.HierarchicalVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "projectVariables", projectVariables },
                    { "testPlans", testPlansDict }
                };
                VariablesPreview = JsonSerializer.Serialize(varsDict, PrettyJsonOptions);

                RefreshComponentPreview();
                AppendRuntimeTraceBufferToPreviewLogs();
            }
            catch (OperationCanceledException)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run stopped by user."
                });
                AppendRuntimeTraceBufferToPreviewLogs();
                UpdateProjectVariablesPreview();
            }
            catch (Exception ex)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run failed: {ex.Message}"
                });
                AppendRuntimeTraceBufferToPreviewLogs();
                UpdateProjectVariablesPreview();
            }
            finally
            {
                SetRunState(false);
            }
        }

        private void ApplyProjectVariables(Test_Automation.Models.ExecutionContext context)
        {
            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (projectNode == null)
            {
                return;
            }

            // First, set all raw variables to context so they can reference each other
            foreach (var variable in projectNode.Variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Key))
                {
                    continue;
                }

                context.SetVariable(variable.Key, variable.Value);
            }

            // Now resolve ${...} patterns in variable values
            foreach (var variable in projectNode.Variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Key))
                {
                    continue;
                }

                // Resolve ${...} patterns using the same logic as component settings
                var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [variable.Key] = variable.Value
                };
                var resolved = _variableService.ResolveSettings(settings, context);
                if (resolved.TryGetValue(variable.Key, out var resolvedValue) && resolvedValue != variable.Value)
                {
                    context.SetVariable(variable.Key, resolvedValue);
                }
            }
        }

        private void ApplyTestPlanVariables(PlanNode testPlanNode, Test_Automation.Models.ExecutionContext context)
        {
            // Apply TestPlan local variables (these override project variables with the same name)
            Logger.Log($"[DEBUG] ApplyTestPlanVariables for '{testPlanNode.Name}' - applying {testPlanNode.Variables.Count} variables to context {context.ExecutionId}", LogLevel.Verbose);

            // First, set all raw variables to context so they can reference each other
            foreach (var variable in testPlanNode.Variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Key))
                {
                    continue;
                }

                Logger.Log($"[DEBUG]   Setting variable '{variable.Key}' = '{variable.Value}' in context {context.ExecutionId}", LogLevel.Verbose);
                context.SetVariable(variable.Key, variable.Value);
            }

            // Now resolve ${...} patterns in variable values
            foreach (var variable in testPlanNode.Variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Key))
                {
                    continue;
                }

                // Resolve ${...} patterns using the same logic as component settings
                var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [variable.Key] = variable.Value
                };
                var resolved = _variableService.ResolveSettings(settings, context);
                if (resolved.TryGetValue(variable.Key, out var resolvedValue) && resolvedValue != variable.Value)
                {
                    context.SetVariable(variable.Key, resolvedValue);
                }
            }
        }

        /// <summary>
        /// Builds a dictionary of project variables using RUNTIME context values.
        /// Uses the node to identify which keys are project-level, but reads current values from context.
        /// This ensures extractor-assigned values are reflected in the post-run preview.
        /// </summary>
        private Dictionary<string, object> BuildRuntimeProjectVariables(
            Test_Automation.Models.ExecutionContext context,
            PlanNode? projectNode)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (projectNode == null) return result;

            foreach (var variable in projectNode.Variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Key)) continue;
                var runtimeValue = context.GetVariable(variable.Key);
                result[variable.Key] = CoercePreviewValue(runtimeValue ?? (object)(variable.Value ?? string.Empty));
            }
            return result;
        }

        /// <summary>
        /// Builds a dictionary of test-plan variables using RUNTIME context values.
        /// Uses the node to identify which keys are testplan-level, but reads current values from context.
        /// This ensures extractor-assigned values are reflected in the post-run preview.
        /// </summary>
        private Dictionary<string, object> BuildRuntimeTestPlanVariables(
            Test_Automation.Models.ExecutionContext context,
            PlanNode? testPlanNode)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (testPlanNode == null) return result;

            foreach (var variable in testPlanNode.Variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Key)) continue;
                var runtimeValue = context.GetVariable(variable.Key);
                result[variable.Key] = CoercePreviewValue(runtimeValue ?? (object)(variable.Value ?? string.Empty));
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
                        catch (System.Text.Json.JsonException)
                        {
                            // Still not valid JSON – keep as string
                        }
                    }
                }
            }

            return text;
        }

        private void SetContextHierarchicalVariables(Test_Automation.Models.ExecutionContext context, PlanNode testPlanNode)
        {
            // Get project variables
            var projectNode = RootNodes.FirstOrDefault(n => n.Type == "Project");
            var projectVariables = projectNode != null
                ? BuildDictionaryWithOverwrite(projectNode.Variables)
                    .ToDictionary(e => e.Key, e => (object)e.Value, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Get test plan variables
            var testPlanVars = testPlanNode != null
                ? BuildDictionaryWithOverwrite(testPlanNode.Variables)
                    .ToDictionary(e => e.Key, e => (object)e.Value, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Build hierarchical structure
            var testPlansDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { testPlanNode.Name, testPlanVars }
            };

            context.HierarchicalVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "projectVariables", projectVariables },
                { "testPlans", testPlansDict }
            };
        }

        /// <summary>
        /// Gets all available variables for dropdown selection (Project variables + TestPlan local variables)
        /// </summary>
        public ObservableCollection<string> AvailableVariablesForExtractor
        {
            get
            {
                var variables = new ObservableCollection<string>();
                var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
                if (projectNode != null)
                {
                    foreach (var v in projectNode.Variables)
                    {
                        if (!string.IsNullOrWhiteSpace(v.Key) && !variables.Contains(v.Key))
                        {
                            variables.Add(v.Key);
                        }
                    }
                }

                // Add TestPlan local variables if a TestPlan is selected
                if (SelectedNode != null)
                {
                    // Check if selected node is a TestPlan or is inside a TestPlan
                    var testPlanNode = FindParentTestPlan(SelectedNode);
                    if (testPlanNode != null)
                    {
                        foreach (var v in testPlanNode.Variables)
                        {
                            if (!string.IsNullOrWhiteSpace(v.Key) && !variables.Contains(v.Key))
                            {
                                variables.Add(v.Key);
                            }
                        }
                    }
                }

                return variables;
            }
        }

        private PlanNode? FindParentTestPlan(PlanNode node)
        {
            if (node.Type == "TestPlan")
            {
                return node;
            }

            if (node.Parent != null)
            {
                return FindParentTestPlan(node.Parent);
            }

            return null;
        }

        private void RefreshEnvironmentOptions()
        {
            if (_isRefreshingEnvironmentOptions)
            {
                return;
            }

            _isRefreshingEnvironmentOptions = true;
            _isSyncingEnvironment = true;
            try
            {
                var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
                var environmentText = GetProjectSettingValue(projectNode, "Environment", "dev");
                var parsedOptions = environmentText
                    .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(option => option.Trim())
                    .Where(option => !string.IsNullOrWhiteSpace(option))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (parsedOptions.Count == 0)
                {
                    parsedOptions.Add("dev");
                }

                var optionsUnchanged = EnvironmentOptions.Count == parsedOptions.Count
                    && EnvironmentOptions.Zip(parsedOptions, (current, next) => string.Equals(current, next, StringComparison.OrdinalIgnoreCase)).All(match => match);

                if (!optionsUnchanged)
                {
                    EnvironmentOptions.Clear();
                    foreach (var option in parsedOptions)
                    {
                        EnvironmentOptions.Add(option);
                    }
                }

                var envVariable = GetProjectVariableValue(projectNode, "env", string.Empty);
                var selected = _selectedEnvironment;

                if (string.IsNullOrWhiteSpace(selected)
                    || !parsedOptions.Any(option => string.Equals(option, selected, StringComparison.OrdinalIgnoreCase)))
                {
                    selected = envVariable;
                }

                if (!parsedOptions.Any(option => string.Equals(option, selected, StringComparison.OrdinalIgnoreCase)))
                {
                    selected = parsedOptions[0];
                }

                if (!string.Equals(_selectedEnvironment, selected, StringComparison.Ordinal))
                {
                    _selectedEnvironment = selected;
                    OnPropertyChanged(nameof(SelectedEnvironment));
                    OnPropertyChanged(nameof(HttpUrlResolved));
                    OnPropertyChanged(nameof(SqlConnectionResolved));
                    OnPropertyChanged(nameof(SqlQueryResolved));
                    OnPropertyChanged(nameof(DatasetResolvedSourcePath));

                    if (string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                    {
                        RefreshDatasetPreview();
                    }
                }

                if (SetProjectVariable("env", selected))
                {
                    UpdateProjectVariablesPreview();
                }
            }
            finally
            {
                _isSyncingEnvironment = false;
                _isRefreshingEnvironmentOptions = false;
            }
        }

        private string GetProjectSettingValue(PlanNode? projectNode, string key, string fallback)
        {
            if (projectNode == null)
            {
                return fallback;
            }

            var setting = projectNode.Settings.FirstOrDefault(current => string.Equals(current.Key, key, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(setting?.Value) ? fallback : setting.Value;
        }

        private string GetProjectVariableValue(PlanNode? projectNode, string key, string fallback)
        {
            if (projectNode == null)
            {
                return fallback;
            }

            var variable = projectNode.Variables.FirstOrDefault(current => string.Equals(current.Key, key, StringComparison.OrdinalIgnoreCase));
            return variable?.Value ?? fallback;
        }

        private bool SetProjectVariable(string key, string value)
        {
            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (projectNode == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var existing = projectNode.Variables.FirstOrDefault(variable => string.Equals(variable.Key, key, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                projectNode.Variables.Add(new NodeSetting(key, value));
                return true;
            }

            var changed = false;
            if (!string.Equals(existing.Key, key, StringComparison.Ordinal))
            {
                existing.Key = key;
                changed = true;
            }

            if (!string.Equals(existing.Value, value, StringComparison.Ordinal))
            {
                existing.Value = value;
                changed = true;
            }

            return changed;
        }

        private void UpdateProjectVariablesPreview()
        {
            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (projectNode == null)
            {
                VariablesPreview = "{}";
                return;
            }

            var projectVariables = BuildDictionaryWithOverwrite(projectNode.Variables)
                .ToDictionary(entry => entry.Key, entry => (object)entry.Value, StringComparer.OrdinalIgnoreCase);

            // Find the TestPlan ancestor for the selected component
            PlanNode? testPlanNode = null;
            var current = SelectedNode;
            while (current != null)
            {
                if (string.Equals(current.Type, "TestPlan", StringComparison.OrdinalIgnoreCase))
                {
                    testPlanNode = current;
                    break;
                }
                current = current.Parent;
            }


            var context = _lastExecutionContext;

            // Build preview using runtime context values if available (preserves extractor-assigned values)
            // IMPORTANT: Do NOT write static values back into the context here - that would overwrite extracted values
            Dictionary<string, object> projectVarsForDisplay;
            Dictionary<string, object> testPlanVarsForDisplay;
            if (context != null)
            {
                projectVarsForDisplay = BuildRuntimeProjectVariables(context, projectNode);
                testPlanVarsForDisplay = testPlanNode != null
                    ? BuildRuntimeTestPlanVariables(context, testPlanNode)
                    : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // No runtime context yet — fall back to static definitions
                projectVarsForDisplay = projectVariables.ToDictionary(
                    entry => entry.Key, entry => CoercePreviewValue(entry.Value), StringComparer.OrdinalIgnoreCase);
                testPlanVarsForDisplay = testPlanNode != null
                    ? BuildDictionaryWithOverwrite(testPlanNode.Variables)
                        .ToDictionary(entry => entry.Key, entry => CoercePreviewValue(entry.Value), StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            var scopedTestPlanVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (testPlanNode != null && testPlanVarsForDisplay.Count > 0)
            {
                scopedTestPlanVariables[testPlanNode.Name] = testPlanVarsForDisplay;
            }

            if (context != null)
            {
                // Update hierarchical structure in context (for assertions) without overwriting flat variables
                context.HierarchicalVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "projectVariables", projectVarsForDisplay },
                    { "testPlans", scopedTestPlanVariables }
                };
            }

            // Show only scoped variables
            VariablesPreview = JsonSerializer.Serialize(new
            {
                projectVariables = projectVarsForDisplay,
                testPlans = scopedTestPlanVariables
            }, PrettyJsonOptions);
        }

        private ComponentExecutor CreateExecutorWithHighlight()
        {
            var executor = new ComponentExecutor();
            executor.ComponentStarted += result => _ = SetNodeHighlightAsync(result.ComponentId, true);
            executor.ComponentCompleted += result => _ = HandleComponentCompletedAsync(result);
            executor.Trace += args => AppendRuntimeLogAsync(args);
            return executor;
        }

                private void AppendRuntimeLogAsync(Test_Automation.Models.TraceEventArgs args)
        {
            _logQueue.Enqueue(args);
        }

        private void LogFlushTimer_Tick(object? sender, EventArgs e)
        {
            if (_logQueue.IsEmpty) return;
            var sb = new System.Text.StringBuilder();
            bool hasValidLogs = false;
            
            while (_logQueue.TryDequeue(out var log))
            {
                if (string.Equals(SelectedTraceLevel, "Errors", StringComparison.OrdinalIgnoreCase) && log.Level != Test_Automation.Models.TraceLevel.Error) continue;
                if (string.Equals(SelectedTraceLevel, "Off", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(SelectedTraceLevel, "Component Execution", StringComparison.OrdinalIgnoreCase) && log.Level == Test_Automation.Models.TraceLevel.Verbose) continue;
                
                var time = log.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                sb.AppendLine($"[{time}] {log.Message}");
                hasValidLogs = true;
            }

            if (!hasValidLogs) return;

            if (string.IsNullOrWhiteSpace(PreviewLogs) || string.Equals(PreviewLogs, "Logs will appear here.", StringComparison.Ordinal) || PreviewLogs.Contains("No logs available"))
            {
                PreviewLogs = sb.ToString();
            }
            else
            {
                PreviewLogs += sb.ToString();
            }
        }

        private static bool IsErrorTraceMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("exception", StringComparison.OrdinalIgnoreCase)
                || message.Contains("error", StringComparison.OrdinalIgnoreCase)
                || message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || message.Contains("missing", StringComparison.OrdinalIgnoreCase);
        }

                private void AppendRuntimeTraceBufferToPreviewLogs()
        {
        }

        private void RebuildPreviewLogsForSelectedNode()
        {
            if (SelectedNode == null) 
            {
                PreviewLogs = "Logs will appear here.";
                return;
            }
            
            var results = string.Equals(SelectedPreviewDataMode, "Full History", StringComparison.OrdinalIgnoreCase) 
                ? GetExecutionResultsForScope(SelectedNode) 
                : FilterPreviewResults(GetExecutionResultsForScope(SelectedNode), lastPerComponent: true);
                
            var sb = new System.Text.StringBuilder();
            var allLogs = results
                .SelectMany(r => r.Logs ?? System.Linq.Enumerable.Empty<Test_Automation.Models.TraceEventArgs>())
                .OrderBy(l => l.Timestamp)
                .ToList();
                
            foreach (var log in allLogs)
            {
                if (string.Equals(SelectedTraceLevel, "Errors", StringComparison.OrdinalIgnoreCase) && log.Level != Test_Automation.Models.TraceLevel.Error) continue;
                if (string.Equals(SelectedTraceLevel, "Off", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(SelectedTraceLevel, "Component Execution", StringComparison.OrdinalIgnoreCase) && log.Level == Test_Automation.Models.TraceLevel.Verbose) continue;

                var time = log.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                sb.AppendLine($"[{time}] {log.Message}");
            }
            
            var traceHistory = sb.Length > 0 ? sb.ToString() : "No trace history available for this component in the current Trace Level / History Mode.";
            
            if (string.IsNullOrWhiteSpace(PreviewLogs) || PreviewLogs.StartsWith("Logs will appear here.") || PreviewLogs.Contains("No trace history"))
            {
                PreviewLogs = traceHistory;
            }
            else
            {
                var prefix = PreviewLogs.Split(new[] { "\n\n=== Trace History ===" }, StringSplitOptions.None)[0];
                PreviewLogs = prefix + "\n\n=== Trace History ===\n" + traceHistory;
            }
        }

        private async Task HandleComponentCompletedAsync(ExecutionResult result)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var node = FindNodeById(result.ComponentId);
                if (node == null)
                {
                    return;
                }

                node.IsHighlighted = false;
                ApplyAssertionStatusesToNode(node, result);
            });
        }

        private static void ApplyAssertionStatusesToNode(PlanNode node, ExecutionResult result)
        {
            foreach (var rule in node.Assertions)
            {
                rule.LastResultState = "NotRun";
                rule.LastMessage = string.Empty;
            }

            node.AssertFailedCount = 0;
            node.ExpectFailedCount = 0;
            node.AssertionPassedCount = 0;

            if (result.AssertionResults == null || result.AssertionResults.Count == 0)
            {
                return;
            }

            foreach (var evaluation in result.AssertionResults)
            {
                if (evaluation.Index < 0 || evaluation.Index >= node.Assertions.Count)
                {
                    continue;
                }

                var rule = node.Assertions[evaluation.Index];
                rule.LastMessage = evaluation.Message;

                if (evaluation.Passed)
                {
                    rule.LastResultState = "Passed";
                    node.AssertionPassedCount++;
                    continue;
                }

                if (string.Equals(evaluation.Mode, "Expect", StringComparison.OrdinalIgnoreCase))
                {
                    rule.LastResultState = "ExpectFailed";
                    node.ExpectFailedCount++;
                }
                else
                {
                    rule.LastResultState = "AssertFailed";
                    node.AssertFailedCount++;
                }
            }
        }

        private static void ResetAssertionStateForSubtree(PlanNode node)
        {
            node.AssertFailedCount = 0;
            node.ExpectFailedCount = 0;
            node.AssertionPassedCount = 0;
            foreach (var rule in node.Assertions)
            {
                rule.LastResultState = "NotRun";
                rule.LastMessage = string.Empty;
            }

            foreach (var child in node.Children)
            {
                ResetAssertionStateForSubtree(child);
            }
        }

        private async Task SetNodeHighlightAsync(string componentId, bool isHighlighted)
        {
            if (string.IsNullOrWhiteSpace(componentId))
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                var node = FindNodeById(componentId);
                if (node == null)
                {
                    return;
                }

                node.IsHighlighted = isHighlighted;
            });
        }

        private PlanNode? FindNodeById(string componentId)
        {
            foreach (var root in RootNodes)
            {
                var match = FindNodeById(root, componentId);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static PlanNode? FindNodeById(PlanNode node, string componentId)
        {
            if (string.Equals(node.Id, componentId, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var match = FindNodeById(child, componentId);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private Test_Automation.Componentes.Component? BuildComponentTree(PlanNode node)
        {
            if (node == null || !node.IsEnabled || node.Type == "Project")
            {
                return null;
            }

            var component = ComponentFactory.CreateComponent(node.Type);
            component.SetName(node.Name);
            component.SetId(node.Id);

            var settings = new Dictionary<string, string>();
            foreach (var setting in node.Settings)
            {
                if (string.IsNullOrWhiteSpace(setting.Key))
                {
                    continue;
                }

                settings[setting.Key] = setting.Value;
            }

            component.Settings = settings;

            var extractors = node.Extractors
                .Select(rule => new VariableExtractionRule(rule.Source, rule.JsonPath, rule.VariableName))
                .ToList();

            component.Extractors = extractors;

            var assertions = node.Assertions
                .Select(rule => new AssertionRule(rule.Source, rule.JsonPath, rule.Condition, rule.Expected, rule.Mode))
                .ToList();

            component.Assertions = assertions;

            foreach (var child in node.Children)
            {
                var childComponent = BuildComponentTree(child);
                if (childComponent != null)
                {
                    component.AddChild(childComponent);
                }
            }

            return component;
        }

        private static string[] GetAllowedChildren(string parentType)
        {
            if (parentType == "Project")
            {
                return new[] { "TestPlan" };
            }

            if (parentType == "TestPlan")
            {
                // TestPlan can have Threads and all step types directly
                return new[] { "Threads", "Config", "If", "Loop", "Foreach" }.Concat(StepTypes).ToArray();
            }

            if (parentType == "Threads" || parentType == "If" || parentType == "Loop" || parentType == "Foreach" || parentType == "While")
            {
                return new[] { "Config", "If", "Loop", "Foreach" }.Concat(StepTypes).ToArray();
            }

            return Array.Empty<string>();
        }

        private void AddChildNode(PlanNode parent, string childType)
        {
            var child = new PlanNode(childType, childType) { Parent = parent };
            parent.Children.Add(child);
            SelectedNode = child;
            RefreshJsonPreview();
        }

        private void RemoveNode(PlanNode node)
        {
            if (node.Parent == null)
            {
                RootNodes.Remove(node);
                if (ReferenceEquals(SelectedNode, node))
                {
                    SelectedNode = null;
                }
                RefreshJsonPreview();
                return;
            }

            node.Parent.Children.Remove(node);
            if (ReferenceEquals(SelectedNode, node))
            {
                SelectedNode = null;
            }
            RefreshJsonPreview();
        }

        private void CloneNode(PlanNode sourceNode)
        {
            var clonedNode = DeepCloneNode(sourceNode);

            if (sourceNode.Parent == null)
            {
                if (clonedNode.Type == "Project" && RootNodes.Any(node => node.Type == "Project"))
                {
                    MessageBox.Show("Only one Project root is allowed.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sourceIndex = RootNodes.IndexOf(sourceNode);
                var insertAt = sourceIndex >= 0 ? sourceIndex + 1 : RootNodes.Count;
                RootNodes.Insert(insertAt, clonedNode);
                SelectedNode = clonedNode;
                RefreshJsonPreview();
                return;
            }

            clonedNode.Parent = sourceNode.Parent;
            var siblings = sourceNode.Parent.Children;
            var currentIndex = siblings.IndexOf(sourceNode);
            var targetIndex = currentIndex >= 0 ? currentIndex + 1 : siblings.Count;
            siblings.Insert(targetIndex, clonedNode);
            SelectedNode = clonedNode;
            RefreshJsonPreview();
        }

        private PlanNode DeepCloneNode(PlanNode source)
        {
            var copy = new PlanNode(source.Type, source.Name)
            {
                IsEnabled = source.IsEnabled
            };

            copy.Settings.Clear();
            foreach (var setting in source.Settings)
            {
                copy.Settings.Add(new NodeSetting(setting.Key, setting.Value));
            }

            copy.Variables.Clear();
            foreach (var variable in source.Variables)
            {
                copy.Variables.Add(new NodeSetting(variable.Key, variable.Value));
            }

            copy.Extractors.Clear();
            foreach (var extractor in source.Extractors)
            {
                copy.Extractors.Add(new VariableExtractionRule(extractor.Source, extractor.JsonPath, extractor.VariableName));
            }

            copy.Assertions.Clear();
            foreach (var assertion in source.Assertions)
            {
                copy.Assertions.Add(new AssertionRule(assertion.Source, assertion.JsonPath, assertion.Condition, assertion.Expected, assertion.Mode));
            }

            foreach (var child in source.Children)
            {
                var clonedChild = DeepCloneNode(child);
                clonedChild.Parent = copy;
                copy.Children.Add(clonedChild);
            }

            return copy;
        }

        private void PlanTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedNode = e.NewValue as PlanNode;
            if (!_isRunInProgress)
            {
                UpdateProjectVariablesPreview();
            }
            RefreshJsonPreview();
        }

        private void AddSettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null)
            {
                return;
            }

            var setting = new NodeSetting("Key", "Value");
            SelectedNode.Settings.Add(setting);
            RefreshJsonPreview();
        }

        private void RemoveSettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || sender is not Button button || button.DataContext is not NodeSetting setting)
            {
                return;
            }

            SelectedNode.Settings.Remove(setting);
            RefreshJsonPreview();
        }

        private void AddProjectVariableButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type != "Project")
            {
                return;
            }

            SelectedNode.Variables.Add(new NodeSetting("var", "Value"));
            RefreshJsonPreview();
        }

        private void RemoveProjectVariableButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type != "Project" || sender is not Button button || button.DataContext is not NodeSetting setting)
            {
                return;
            }

            SelectedNode.Variables.Remove(setting);
            RefreshJsonPreview();
        }

        private void AddTestPlanVariableButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type != "TestPlan")
            {
                return;
            }

            SelectedNode.Variables.Add(new NodeSetting("var", "Value"));
            OnPropertyChanged(nameof(TestPlanVariablesForEditor));
            RebuildVariableUsageMap();
        }

        private void RemoveTestPlanVariableButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type != "TestPlan" || sender is not Button button || button.DataContext is not NodeSetting setting)
            {
                return;
            }

            SelectedNode.Variables.Remove(setting);
            OnPropertyChanged(nameof(TestPlanVariablesForEditor));
            RebuildVariableUsageMap();
        }

        private void ViewAssertionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null) return;

            // Verify this is a parent component type
            var parentTypes = new[] { "Project", "TestPlan", "Loop", "Foreach", "If", "Threads" };
            if (!parentTypes.Contains(SelectedNode.Type)) return;

            // Check if there are any child components
            if (SelectedNode.Children.Count == 0)
            {
                MessageBox.Show("This component has no child components.",
                                "No Children", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check if there's execution data
            if (_lastExecutionContext == null)
            {
                MessageBox.Show("No execution data available. Run the test plan first.",
                                "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if preview data mode is full history
            bool isFullHistory = string.Equals(SelectedPreviewDataMode, "Full History",
                                               StringComparison.OrdinalIgnoreCase);

            // Open the Assertion Viewer dialog
            var viewer = new AssertionViewerWindow(SelectedNode, _lastExecutionContext, isFullHistory)
            {
                Owner = this
            };

            viewer.ShowDialog();
        }

        private void AddProjectUrlBaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var entry = new ApiCatalogBaseUrlEntry
            {
                Name = "New Base URL",
                BaseUrl = "https://api.example.com"
            };

            RegisterApiCatalogBaseEntry(entry);
            _projectUrlCatalogEntries.Add(entry);
            SyncApiCatalogSettingFromEditor();
        }

        private void RemoveProjectUrlBaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase)
                || sender is not Button button || button.DataContext is not ApiCatalogBaseUrlEntry entry)
            {
                return;
            }

            UnregisterApiCatalogBaseEntry(entry);
            _projectUrlCatalogEntries.Remove(entry);
            SyncApiCatalogSettingFromEditor();
        }

        private void AddProjectUrlEndpointButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase)
                || sender is not Button button || button.DataContext is not ApiCatalogBaseUrlEntry entry)
            {
                return;
            }

            var endpoint = new ApiCatalogEndpointEntry
            {
                Name = "New Endpoint",
                Path = "/resource",
                Method = "GET",
                Variables = "{}"
            };

            entry.Endpoints.Add(endpoint);
        }

        private void RemoveProjectUrlEndpointButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase)
                || sender is not Button button || button.DataContext is not ApiCatalogEndpointEntry endpoint)
            {
                return;
            }

            var owner = _projectUrlCatalogEntries.FirstOrDefault(entry => entry.Endpoints.Contains(endpoint));
            if (owner == null)
            {
                return;
            }

            owner.Endpoints.Remove(endpoint);
        }

        private void BrowseDatasetFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Select Dataset File",
                Filter = "All supported|*.xlsx;*.xlsm;*.xls;*.csv;*.json;*.xml|Excel|*.xlsx;*.xlsm;*.xls|CSV|*.csv|JSON|*.json|XML|*.xml|All files|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            DatasetSourcePath = dialog.FileName;
            RefreshDatasetPreview();
        }

        private void RefreshDatasetPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDatasetPreview();
        }

        private void RefreshDatasetSheetNamesButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDatasetSheetNames();
        }

        public void RefreshDatasetSheetNames()
        {
            DatasetSheetNames.Clear();
            var filePath = ResolveWithProjectVariables(DatasetSourcePath);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                var format = DatasetFormat;
                if (!string.Equals(format, "Excel", StringComparison.OrdinalIgnoreCase))
                {
                    var extension = Path.GetExtension(filePath)?.Trim().ToLowerInvariant() ?? string.Empty;
                    if (extension != ".xlsx" && extension != ".xlsm" && extension != ".xls")
                        return;
                }

                using var workbook = new ClosedXML.Excel.XLWorkbook(filePath);
                foreach (var sheet in workbook.Worksheets)
                {
                    DatasetSheetNames.Add(sheet.Name);
                }
            }
            catch (Exception)
            {
                // ignore errors
            }
        }

        private void BrowseExcelFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Select Excel File",
                Filter = "Excel files (*.xlsx;*.xlsm;*.xls)|*.xlsx;*.xlsm;*.xls|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            ExcelFilePath = dialog.FileName;
            RefreshExcelSheetNames();
        }

        private void BrowseExcelFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "Excel", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dialog = new OpenFolderDialog
            {
                Title = "Select Folder for New Excel File"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            ExcelFolderPath = dialog.FolderName;
        }

        private void RefreshExcelSheetNamesButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshExcelSheetNames();
        }

        private void ExcelHelpButton_Click(object sender, RoutedEventArgs e)
        {
            var helpText = @"Excel Component Help:

Operations:
- WriteCell: Write a single value to a specified cell (column & row).
- WriteRange: Write multiple cells from a JSON array of arrays starting at a cell.
- AppendRow: Append rows at the end of the sheet from a JSON array of arrays.
- CreateSheet: Create a new sheet with the given name.
- DeleteRows: Clear content of rows in a range (start row to end row).
- DeleteColumns: Clear content of columns in a range (start column to end column).
- ClearCells: Clear content of a rectangular range (start cell to end cell).

Parameters:
- File Mode: New or Existing file.
- File Path: Path to the Excel file.
- Sheet Name: For new files, name of the sheet to create.
- Sheet: For existing files, select sheet from dropdown.
- Column/Row: Cell reference (column letter or number, row number).
- Value: Single value for WriteCell (supports variables like {{var}}).
- Values JSON: JSON array of arrays for WriteRange/AppendRow.
- Range: Start/End column/row for Delete/Clear operations.

Tips:
- Column can be letter (A) or number (1).
- Use Refresh button to load sheet names from file.
- Variables are resolved before writing (use {{variableName}}).";
            MessageBox.Show(helpText, "Excel Component Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowseFileSourceFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "File", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Select Source File",
                Filter = "All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            FileSourcePath = dialog.FileName;
        }

        private void BrowseFileSourceFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "File", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Use OpenFileDialog to simulate folder selection
            var dialog = new OpenFileDialog
            {
                Title = "Select Source Folder",
                CheckFileExists = false,
                ValidateNames = false,
                FileName = "Folder Selection"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            // Extract directory path from selected "file"
            var filePath = dialog.FileName;
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                FileSourcePath = directoryPath;
            }
        }

        private void BrowseFileDestinationFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "File", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Select Destination File",
                Filter = "All files (*.*)|*.*",
                CheckFileExists = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            FileDestinationPath = dialog.FileName;
        }

        private void BrowseFileDestinationFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "File", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Select Destination Folder",
                CheckFileExists = false,
                ValidateNames = false,
                FileName = "Folder Selection"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var filePath = dialog.FileName;
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                FileDestinationPath = directoryPath;
            }
        }

        private void SelectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "File", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Select Files",
                Filter = "All files (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var selectedFiles = dialog.FileNames.ToList();
            FileSelectedFilePaths = System.Text.Json.JsonSerializer.Serialize(selectedFiles);
        }

        private void RefreshDatasetPreview()
        {
            _datasetPreviewTable = new DataTable("DatasetPreview");

            if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
            {
                DatasetPreviewStatus = "Select a Dataset component to preview rows.";
                OnPropertyChanged(nameof(DatasetPreviewRows));
                return;
            }

            var resolvedSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Format"] = DatasetFormat,
                ["SourcePath"] = ResolveWithProjectVariables(DatasetSourcePath),
                ["SheetName"] = ResolveWithProjectVariables(DatasetSheetName),
                ["CsvDelimiter"] = ResolveWithProjectVariables(DatasetCsvDelimiter),
                ["HasHeader"] = DatasetHasHeader ? "true" : "false",
                ["JsonArrayPath"] = ResolveWithProjectVariables(DatasetJsonArrayPath),
                ["XmlRowPath"] = ResolveWithProjectVariables(DatasetXmlRowPath),
                ["MaxRows"] = ResolveWithProjectVariables(DatasetMaxRows)
            };

            try
            {
                var rows = Test_Automation.Componentes.Dataset.LoadRows(resolvedSettings);
                
                // Preserve original column order from source (first row's keys)
                var columnNames = rows.Count > 0
                    ? rows[0].Keys.ToList()
                    : rows.SelectMany(row => row.Keys)
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .ToList();

                if (columnNames.Count == 0)
                {
                    _datasetPreviewTable.Columns.Add("NoData", typeof(string));
                    var infoRow = _datasetPreviewTable.NewRow();
                    infoRow["NoData"] = "No rows were loaded from source.";
                    _datasetPreviewTable.Rows.Add(infoRow);
                    DatasetPreviewStatus = $"Loaded 0 row(s) from {resolvedSettings["SourcePath"]}.";
                    OnPropertyChanged(nameof(DatasetPreviewRows));
                    return;
                }

                foreach (var columnName in columnNames)
                {
                    _datasetPreviewTable.Columns.Add(columnName, typeof(string));
                }

                foreach (var row in rows)
                {
                    var tableRow = _datasetPreviewTable.NewRow();
                    foreach (var columnName in columnNames)
                    {
                        tableRow[columnName] = row.TryGetValue(columnName, out var value)
                            ? (value?.ToString() ?? string.Empty)
                            : string.Empty;
                    }

                    _datasetPreviewTable.Rows.Add(tableRow);
                }

                DatasetPreviewStatus = $"Loaded {rows.Count} row(s) from {resolvedSettings["SourcePath"]}.";
            }
            catch (Exception ex)
            {
                DatasetPreviewStatus = $"Dataset preview failed: {ex.Message}";
            }

            OnPropertyChanged(nameof(DatasetPreviewRows));
        }

        private void DatasetPreviewGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Use indexer binding so keys like "data.year" are treated as literal column names.
            if (e.Column is DataGridTextColumn textColumn)
            {
                textColumn.Binding = new Binding($"[{e.PropertyName}]");
            }
        }

        private void AddEndpointParameterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: ApiCatalogEndpointEntry endpoint })
            {
                return;
            }

            endpoint.Parameters.Add(new ApiCatalogParameterEntry());
        }

        private void RemoveEndpointParameterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: ApiCatalogParameterEntry param })
            {
                return;
            }

            foreach (var baseEntry in _projectUrlCatalogEntries)
            {
                foreach (var ep in baseEntry.Endpoints)
                {
                    if (ep.Parameters.Contains(param))
                    {
                        ep.Parameters.Remove(param);
                        return;
                    }
                }
            }
        }

        private void ApplyHttpCatalogSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedHttpCatalogBase) || string.IsNullOrWhiteSpace(SelectedHttpCatalogEndpoint))
            {
                MessageBox.Show("Choose both Base URL and Endpoint before applying.", "Apply Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplySelectedCatalogEndpointToCurrentNode();
        }

        private void ClearHttpCatalogSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _isApplyingCatalogSelection = true;
            try
            {
                SetSettingValue("CatalogBase", string.Empty);
                SetSettingValue("CatalogEndpoint", string.Empty);
            }
            finally
            {
                _isApplyingCatalogSelection = false;
            }

            RefreshCatalogEndpointOptions();
            OnPropertyChanged(nameof(SelectedHttpCatalogBase));
            OnPropertyChanged(nameof(SelectedHttpCatalogEndpoint));
        }

        private void ApplyGraphQlCatalogSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedGraphQlCatalogBase) || string.IsNullOrWhiteSpace(SelectedGraphQlCatalogEndpoint))
            {
                MessageBox.Show("Choose both Base URL and Endpoint before applying.", "Apply Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplySelectedCatalogEndpointToCurrentNode();
        }

        private void ClearGraphQlCatalogSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _isApplyingCatalogSelection = true;
            try
            {
                SetSettingValue("CatalogBase", string.Empty);
                SetSettingValue("CatalogEndpoint", string.Empty);
            }
            finally
            {
                _isApplyingCatalogSelection = false;
            }

            RefreshCatalogEndpointOptions();
            OnPropertyChanged(nameof(SelectedGraphQlCatalogBase));
            OnPropertyChanged(nameof(SelectedGraphQlCatalogEndpoint));
        }

        private void PlanTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedNode = null;
            if (IsClickOnDragHandle(e))
            {
                var item = FindParentTreeViewItem(e.OriginalSource as DependencyObject);
                _draggedNode = item?.DataContext as PlanNode;
            }
        }

        private void PlanTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedNode == null)
            {
                return;
            }

            var mousePos = e.GetPosition(null);
            var diff = _dragStartPoint - mousePos;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(PlanTreeView, new DataObject(typeof(PlanNode), _draggedNode), DragDropEffects.Move);
            _draggedNode = null;
        }

        private void PlanTreeView_DragOver(object sender, DragEventArgs e)
        {
            if (!TryGetDragNodes(e, out var sourceNode, out var targetNode))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var canReorder = CanReorder(sourceNode, targetNode);
            var canMove = CanMoveToParent(sourceNode, targetNode) || CanMoveToSiblingParent(sourceNode, targetNode);
            e.Effects = (canReorder || canMove) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void PlanTreeView_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDragNodes(e, out var sourceNode, out var targetNode))
            {
                e.Handled = true;
                return;
            }

            if (!CanReorder(sourceNode, targetNode))
            {
                if (TryMoveToParent(sourceNode, targetNode))
                {
                    e.Handled = true;
                    RefreshJsonPreview();
                    return;
                }

                if (TryMoveToSiblingParent(sourceNode, targetNode))
                {
                    e.Handled = true;
                    RefreshJsonPreview();
                    return;
                }

                e.Handled = true;
                return;
            }

            var sourceParent = sourceNode.Parent;

            if (sourceParent == null)
            {
                ReorderInCollection(RootNodes, sourceNode, targetNode);
                e.Handled = true;
                RefreshJsonPreview();
                return;
            }

            ReorderInCollection(sourceParent.Children, sourceNode, targetNode);
            e.Handled = true;
            RefreshJsonPreview();
        }

        private void TreeViewItem_DragEnter(object sender, DragEventArgs e)
        {
            UpdateDragOverVisual(sender, e, true);
        }

        private void TreeViewItem_DragOver(object sender, DragEventArgs e)
        {
            UpdateDragOverVisual(sender, e, false);
        }

        private void TreeViewItem_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                item.ClearValue(Control.BackgroundProperty);
            }
        }

        private void TreeViewItem_Drop(object sender, DragEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                item.ClearValue(Control.BackgroundProperty);
            }
        }

        private void UpdateDragOverVisual(object sender, DragEventArgs e, bool autoExpand)
        {
            if (sender is not TreeViewItem item)
            {
                return;
            }

            if (item.DataContext is not PlanNode targetNode)
            {
                return;
            }

            if (!TryGetDragSource(e, out var sourceNode))
            {
                item.ClearValue(Control.BackgroundProperty);
                return;
            }

            var canReorder = CanReorder(sourceNode, targetNode);
            var canMove = CanMoveToParent(sourceNode, targetNode) || CanMoveToSiblingParent(sourceNode, targetNode);

            if (canReorder || canMove)
            {
                item.Background = Brushes.LightGreen;
                if (autoExpand && !item.IsExpanded)
                {
                    item.IsExpanded = true;
                }
            }
            else
            {
                item.Background = Brushes.LightCoral;
            }
        }

        private bool TryGetDragSource(DragEventArgs e, out PlanNode sourceNode)
        {
            sourceNode = null!;

            if (!e.Data.GetDataPresent(typeof(PlanNode)))
            {
                return false;
            }

            sourceNode = e.Data.GetData(typeof(PlanNode)) as PlanNode ?? null!;
            return sourceNode != null;
        }

        private bool TryGetDragNodes(DragEventArgs e, out PlanNode sourceNode, out PlanNode targetNode)
        {
            sourceNode = null!;
            targetNode = null!;

            if (!e.Data.GetDataPresent(typeof(PlanNode)))
            {
                return false;
            }

            var source = e.Data.GetData(typeof(PlanNode)) as PlanNode;
            if (source == null)
            {
                return false;
            }

            var position = e.GetPosition(PlanTreeView);
            var target = GetNodeAtPosition(position);
            if (target == null)
            {
                return false;
            }

            sourceNode = source;
            targetNode = target;
            return true;
        }

        private bool CanReorder(PlanNode sourceNode, PlanNode targetNode)
        {
            if (ReferenceEquals(sourceNode, targetNode))
            {
                return false;
            }

            return ReferenceEquals(sourceNode.Parent, targetNode.Parent);
        }

        private bool CanMoveToParent(PlanNode sourceNode, PlanNode targetNode)
        {
            if (ReferenceEquals(sourceNode, targetNode))
            {
                return false;
            }

            if (!CanAcceptChild(targetNode, sourceNode))
            {
                return false;
            }

            return !IsDescendant(targetNode, sourceNode);
        }

        private bool CanMoveToSiblingParent(PlanNode sourceNode, PlanNode targetNode)
        {
            if (targetNode.Parent == null)
            {
                return false;
            }

            if (!CanAcceptChild(targetNode.Parent, sourceNode))
            {
                return false;
            }

            return !IsDescendant(targetNode.Parent, sourceNode);
        }

        private bool TryMoveToParent(PlanNode sourceNode, PlanNode targetNode)
        {
            if (!CanMoveToParent(sourceNode, targetNode))
            {
                return false;
            }

            RemoveFromParent(sourceNode);
            sourceNode.Parent = targetNode;
            targetNode.Children.Add(sourceNode);
            return true;
        }

        private bool TryMoveToSiblingParent(PlanNode sourceNode, PlanNode targetNode)
        {
            var newParent = targetNode.Parent;
            if (newParent == null || !CanMoveToSiblingParent(sourceNode, targetNode))
            {
                return false;
            }

            RemoveFromParent(sourceNode);
            sourceNode.Parent = newParent;
            var insertIndex = newParent.Children.IndexOf(targetNode);
            if (insertIndex < 0)
            {
                newParent.Children.Add(sourceNode);
            }
            else
            {
                newParent.Children.Insert(insertIndex, sourceNode);
            }

            return true;
        }

        private void RemoveFromParent(PlanNode node)
        {
            if (node.Parent == null)
            {
                RootNodes.Remove(node);
                return;
            }

            node.Parent.Children.Remove(node);
        }

        private static bool CanAcceptChild(PlanNode parent, PlanNode child)
        {
            return GetAllowedChildren(parent.Type)
                .Any(type => string.Equals(type, child.Type, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsDescendant(PlanNode potentialChild, PlanNode potentialAncestor)
        {
            var current = potentialChild.Parent;
            while (current != null)
            {
                if (ReferenceEquals(current, potentialAncestor))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private PlanNode? GetNodeAtPosition(Point position)
        {
            var hit = PlanTreeView.InputHitTest(position) as DependencyObject;
            var targetItem = FindParentTreeViewItem(hit);
            return targetItem?.DataContext as PlanNode;
        }

        private static void ReorderInCollection(ObservableCollection<PlanNode> collection, PlanNode sourceNode, PlanNode targetNode)
        {
            var oldIndex = collection.IndexOf(sourceNode);
            var newIndex = collection.IndexOf(targetNode);
            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            {
                return;
            }

            collection.Move(oldIndex, newIndex);
        }

        private static TreeViewItem? FindParentTreeViewItem(DependencyObject? child)
        {
            while (child != null)
            {
                if (child is TreeViewItem item)
                {
                    return item;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private bool IsClickOnDragHandle(MouseButtonEventArgs e)
        {
            var treeViewItem = FindParentTreeViewItem(e.OriginalSource as DependencyObject);
            if (treeViewItem == null)
                return false;

            var dragHandle = FindDragHandle(treeViewItem);
            if (dragHandle == null)
                return false;

            // Check if the original source is the drag handle or its child
            var source = e.OriginalSource as DependencyObject;
            while (source != null && source != treeViewItem)
            {
                if (source == dragHandle)
                    return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }

        private static Border? FindDragHandle(TreeViewItem treeViewItem)
        {
            // Find the Border with name "DragHandle" in the visual tree
            return FindChildByName(treeViewItem, "DragHandle") as Border;
        }

        private static DependencyObject? FindChildByName(DependencyObject parent, string name)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name)
                    return child;
                var result = FindChildByName(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void RegisterNode(PlanNode node)
        {
            node.PropertyChanged += PlanNode_PropertyChanged;
            node.Children.CollectionChanged += NodeChildren_CollectionChanged;
            node.Settings.CollectionChanged += NodeSettings_CollectionChanged;
            node.Variables.CollectionChanged += NodeVariables_CollectionChanged;
            node.Extractors.CollectionChanged += NodeExtractors_CollectionChanged;
            node.Assertions.CollectionChanged += NodeAssertions_CollectionChanged;

            foreach (var setting in node.Settings)
            {
                setting.PropertyChanged += NodeSetting_PropertyChanged;
            }

            foreach (var variable in node.Variables)
            {
                variable.PropertyChanged += NodeVariableSetting_PropertyChanged;
            }

            foreach (var extractor in node.Extractors)
            {
                extractor.PropertyChanged += NodeExtractor_PropertyChanged;
            }

            foreach (var assertion in node.Assertions)
            {
                assertion.PropertyChanged += NodeAssertion_PropertyChanged;
            }
        }

        private void UnregisterNode(PlanNode node)
        {
            node.PropertyChanged -= PlanNode_PropertyChanged;
            node.Children.CollectionChanged -= NodeChildren_CollectionChanged;
            node.Settings.CollectionChanged -= NodeSettings_CollectionChanged;
            node.Variables.CollectionChanged -= NodeVariables_CollectionChanged;
            node.Extractors.CollectionChanged -= NodeExtractors_CollectionChanged;
            node.Assertions.CollectionChanged -= NodeAssertions_CollectionChanged;

            foreach (var setting in node.Settings)
            {
                setting.PropertyChanged -= NodeSetting_PropertyChanged;
            }

            foreach (var variable in node.Variables)
            {
                variable.PropertyChanged -= NodeVariableSetting_PropertyChanged;
            }

            foreach (var extractor in node.Extractors)
            {
                extractor.PropertyChanged -= NodeExtractor_PropertyChanged;
            }

            foreach (var assertion in node.Assertions)
            {
                assertion.PropertyChanged -= NodeAssertion_PropertyChanged;
            }

            foreach (var child in node.Children)
            {
                UnregisterNode(child);
            }
        }

        private void RootNodes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<PlanNode>())
                {
                    UnregisterNode(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<PlanNode>())
                {
                    RegisterNode(item);
                }
            }

            RefreshJsonPreview();
            RefreshEnvironmentOptions();
            RefreshApiCatalogState();
            RebuildVariableUsageMap();
        }

        private void NodeChildren_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<PlanNode>())
                {
                    UnregisterNode(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<PlanNode>())
                {
                    RegisterNode(item);
                }
            }

            RefreshJsonPreview();
            RebuildVariableUsageMap();
        }

        private void NodeSettings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<NodeSetting>())
                {
                    item.PropertyChanged -= NodeSetting_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<NodeSetting>())
                {
                    item.PropertyChanged += NodeSetting_PropertyChanged;
                }
            }

            RebuildExtractorSourceOptions();
            RefreshJsonPreview();
            RefreshEnvironmentOptions();
            RefreshApiCatalogState();
            RebuildVariableUsageMap();
        }

        private void NodeVariables_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var node = FindNodeByVariablesCollection(sender);
            if (node != null)
            {
                NormalizeDuplicateVariables(node);
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<NodeSetting>())
                {
                    item.PropertyChanged -= NodeVariableSetting_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<NodeSetting>())
                {
                    item.PropertyChanged += NodeVariableSetting_PropertyChanged;
                }
            }

            RefreshJsonPreview();
            if (node != null && string.Equals(node.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                UpdateProjectVariablesPreview();
                RefreshEnvironmentOptions();
                OnPropertyChanged(nameof(HttpUrlResolved));
                OnPropertyChanged(nameof(SqlConnectionResolved));
                OnPropertyChanged(nameof(SqlQueryResolved));
                OnPropertyChanged(nameof(DatasetResolvedSourcePath));

                if (string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshDatasetPreview();
                }
            }
            RebuildVariableUsageMap();
            OnPropertyChanged(nameof(ProjectVariablesForEditor));
        }

        private void NodeExtractors_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<VariableExtractionRule>())
                {
                    item.PropertyChanged -= NodeExtractor_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<VariableExtractionRule>())
                {
                    item.PropertyChanged += NodeExtractor_PropertyChanged;
                }
            }

            RefreshJsonPreview();
            RebuildVariableUsageMap();
        }

        private void NodeAssertions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<AssertionRule>())
                {
                    item.PropertyChanged -= NodeAssertion_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<AssertionRule>())
                {
                    item.PropertyChanged += NodeAssertion_PropertyChanged;
                }
            }

            RefreshJsonPreview();
        }

        private void PlanNode_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();
            RefreshJsonPreview();
            RebuildVariableUsageMap();
        }

        private void NodeSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();
            RebuildExtractorSourceOptions();
            RefreshJsonPreview();

            var shouldRebuildVariableMap = false;
            if (sender is NodeSetting changedSetting)
            {
                var ownerNode = RootNodes
                    .Select(root => FindNodeContainingSetting(root, changedSetting))
                    .FirstOrDefault(found => found != null);

                if (ownerNode != null
                    && string.Equals(ownerNode.Type, "Project", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(changedSetting.Key, "Environment", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshEnvironmentOptions();
                }

                shouldRebuildVariableMap = e.PropertyName == nameof(NodeSetting.Key)
                    || (e.PropertyName == nameof(NodeSetting.Value) && IsVariableSettingKey(changedSetting.Key));

                if (ownerNode != null
                    && string.Equals(ownerNode.Type, "Project", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(changedSetting.Key, "UrlCatalog", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshApiCatalogState();
                }

                if (ownerNode != null
                    && ReferenceEquals(ownerNode, SelectedNode)
                    && string.Equals(ownerNode.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(changedSetting.Key, "SourcePath", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "Format", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "SheetName", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "CsvDelimiter", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "CsvHasHeader", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "JsonArrayPath", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "XmlRowPath", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "MaxRows", StringComparison.OrdinalIgnoreCase))
                    {
                        RefreshDatasetPreview();
                    }
                }

                if (ownerNode != null
                    && ReferenceEquals(ownerNode, SelectedNode)
                    && (string.Equals(changedSetting.Key, "CatalogBase", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "CatalogEndpoint", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "UrlCatalog", StringComparison.OrdinalIgnoreCase)))
                {
                    SyncCatalogSelectionFromCurrentNode();
                }
            }

            if (shouldRebuildVariableMap)
            {
                RebuildVariableUsageMap();
            }
        }

        private void NodeVariableSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var shouldRebuildVariableMap = false;
            PlanNode? ownerNode = null;
            if (sender is NodeSetting variable)
            {
                ownerNode = RootNodes
                    .Select(root => FindNodeContainingVariable(root, variable))
                    .FirstOrDefault(found => found != null);

                if (ownerNode != null)
                {
                    if (e.PropertyName == nameof(NodeSetting.Key))
                    {
                        NormalizeDuplicateVariables(ownerNode, variable);
                        shouldRebuildVariableMap = true;
                    }
                }
            }

            RefreshJsonPreview();
            if (ownerNode != null && string.Equals(ownerNode.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                UpdateProjectVariablesPreview();
                OnPropertyChanged(nameof(HttpUrlResolved));
                OnPropertyChanged(nameof(SqlConnectionResolved));
                OnPropertyChanged(nameof(SqlQueryResolved));
                OnPropertyChanged(nameof(DatasetResolvedSourcePath));

                if (string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshDatasetPreview();
                }
                if (string.Equals((sender as NodeSetting)?.Key, "env", StringComparison.OrdinalIgnoreCase)
                    || e.PropertyName == nameof(NodeSetting.Key))
                {
                    RefreshEnvironmentOptions();
                }
            }

            if (shouldRebuildVariableMap)
            {
                RebuildVariableUsageMap();
            }

            OnPropertyChanged(nameof(ProjectVariablesForEditor));
        }

        private void NodeExtractor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();
            RefreshJsonPreview();
            RebuildVariableUsageMap();
        }

        private void NodeAssertion_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();

            var affectsPersistence = e.PropertyName == nameof(AssertionRule.Source)
                || e.PropertyName == nameof(AssertionRule.JsonPath)
                || e.PropertyName == nameof(AssertionRule.Mode)
                || e.PropertyName == nameof(AssertionRule.Condition)
                || e.PropertyName == nameof(AssertionRule.Expected);

            if (affectsPersistence && sender is AssertionRule rule)
            {
                rule.LastResultState = "NotRun";
                rule.LastMessage = "Rule changed. Run component to get fresh status.";
            }

            if (affectsPersistence)
            {
                RefreshJsonPreview();
            }
        }

        private void RefreshJsonPreview()
        {
            var model = RootNodes.Select(BuildNodeObject).ToList();
            JsonPreview = JsonSerializer.Serialize(model, PrettyJsonOptions);

            RefreshComponentPreview();
            RefreshAssertionJsonTreePanel();
        }

        private void RefreshComponentPreview()
        {
            RebuildAssertionSourceOptions();

            if (SelectedNode == null)
            {
                PreviewRequest = "Select a component to see request preview.";
                PreviewResponse = "Select a component to see response preview.";
                PreviewOutput = "Select a component to see output.";
                PreviewLogs = "Logs will appear here.";
                AssertionPreview = "Select a component to see assertion preview.";
                ResetHttpDetailPreviews();
                return;
            }

            var nodeType = SelectedNode.Type;
            var nodeId = SelectedNode.Id;
            var nodeName = SelectedNode.Name;
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var nodeExecutionResults = FilterPreviewResults(GetExecutionResults(nodeId), lastPerComponent: false);

            if (!string.Equals(nodeType, "Http", StringComparison.OrdinalIgnoreCase))
            {
                ResetHttpDetailPreviews();
            }

            if (string.Equals(nodeType, "Project", StringComparison.OrdinalIgnoreCase))
            {
                PreviewOutput = string.Empty;
                PopulateProjectPreview(now, nodeName);
                return;
            }

            if (string.Equals(nodeType, "TestPlan", StringComparison.OrdinalIgnoreCase))
            {
                PreviewOutput = string.Empty;
                PopulateTestPlanPreview(now, nodeName);
                return;
            }

            var scopedResults = FilterPreviewResults(GetExecutionResultsForScope(SelectedNode), lastPerComponent: true);
            SetAssertionPreview(nodeType, nodeName, scopedResults);

            if (nodeType == "Http")
            {
                var method = GetSettingValue("Method", "GET");
                var url = ResolveWithProjectVariables(GetSettingValue("Url", "https://api.example.com"));
                var latestHttpExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var lastHttp = GetLastExecutionData<HttpData>(nodeId);
                PreviewOutput = lastHttp?.ResponseBody ?? string.Empty;
                var httpRequestRuns = nodeExecutionResults
                    .Where(result => result.Data is HttpData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        method = (result.Data as HttpData)?.Method,
                        url = (result.Data as HttpData)?.Url,
                        headers = (result.Data as HttpData)?.Headers,
                        body = (result.Data as HttpData)?.Body
                    })
                    .ToList();
                var httpRuns = nodeExecutionResults
                    .Where(result => result.Data is HttpData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        responseStatus = (result.Data as HttpData)?.ResponseStatus,
                        responseBody = (result.Data as HttpData)?.ResponseBody
                    })
                    .ToList();

                if (latestHttpExecution != null
                    && latestHttpExecution.Data is not HttpData
                    && string.Equals(latestHttpExecution.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    HttpRequestHeadersPreview = "{}";
                    HttpRequestCookiesPreview = "[]";
                    HttpRequestMetadataPreview = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        method,
                        url,
                        status = latestHttpExecution.Status,
                        durationMs = latestHttpExecution.DurationMs,
                        threadIndex = latestHttpExecution.ThreadIndex,
                        startTime = latestHttpExecution.StartTime,
                        endTime = latestHttpExecution.EndTime,
                        error = latestHttpExecution.Error
                    }, PrettyJsonOptions);

                    HttpResponseHeadersPreview = "{}";
                    HttpResponseCookiesPreview = "[]";
                    HttpResponseMetadataPreview = JsonSerializer.Serialize(new
                    {
                        status = latestHttpExecution.Status,
                        error = latestHttpExecution.Error,
                        message = "Request failed before receiving an HTTP response.",
                        durationMs = latestHttpExecution.DurationMs,
                        threadIndex = latestHttpExecution.ThreadIndex
                    }, PrettyJsonOptions);

                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Http",
                        method,
                        url,
                        status = latestHttpExecution.Status,
                        error = latestHttpExecution.Error,
                        threadIndex = latestHttpExecution.ThreadIndex,
                        durationMs = latestHttpExecution.DurationMs
                    }, PrettyJsonOptions);

                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = latestHttpExecution.Status,
                        error = latestHttpExecution.Error,
                        message = "Request failed before receiving an HTTP response."
                    }, PrettyJsonOptions);

                    PreviewLogs = $"[{now}] HTTP preview refreshed\n[{now}] Target: {method} {url}\n[{now}] Last run failed: {latestHttpExecution.Error}";
                    AppendExtractionPreview(now);
                    RebuildPreviewLogsForSelectedNode();
                    return;
                }

                var latestHttpData = latestHttpExecution?.Data as HttpData;
                var requestHeaders = latestHttpData?.Headers ?? BuildRequestHeadersFromSettings();
                HttpRequestHeadersPreview = JsonSerializer.Serialize(requestHeaders, PrettyJsonOptions);
                HttpRequestCookiesPreview = JsonSerializer.Serialize(ExtractCookiesFromHeaders(requestHeaders, "Cookie"), PrettyJsonOptions);
                HttpRequestMetadataPreview = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    method,
                    url,
                    authType = GetSettingValue("AuthType", "WindowsIntegrated"),
                    bodyLength = (latestHttpData?.Body ?? HttpBody ?? string.Empty).Length,
                    status = latestHttpExecution?.Status ?? "not-run",
                    durationMs = latestHttpExecution?.DurationMs,
                    threadIndex = latestHttpExecution?.ThreadIndex,
                    startTime = latestHttpExecution?.StartTime,
                    endTime = latestHttpExecution?.EndTime
                }, PrettyJsonOptions);

                var responseHeaders = latestHttpData?.ResponseHeaders ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                HttpResponseHeadersPreview = JsonSerializer.Serialize(responseHeaders, PrettyJsonOptions);
                HttpResponseCookiesPreview = JsonSerializer.Serialize(ExtractCookiesFromHeaders(responseHeaders, "Set-Cookie"), PrettyJsonOptions);
                HttpResponseMetadataPreview = JsonSerializer.Serialize(new
                {
                    status = latestHttpExecution?.Status ?? "not-run",
                    httpStatus = latestHttpData?.ResponseStatus,
                    contentType = TryGetHeaderValue(responseHeaders, "Content-Type"),
                    bodyLength = (latestHttpData?.ResponseBody ?? string.Empty).Length,
                    durationMs = latestHttpExecution?.DurationMs,
                    threadIndex = latestHttpExecution?.ThreadIndex,
                    error = latestHttpExecution?.Error
                }, PrettyJsonOptions);

                if (httpRequestRuns.Count > 0)
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        runs = httpRequestRuns
                    }, PrettyJsonOptions);
                }
                else
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Http",
                        method,
                        url
                    }, PrettyJsonOptions);
                }

                if (httpRuns.Count > 0)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        runs = httpRuns
                    }, PrettyJsonOptions);
                }
                else if (lastHttp != null)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = lastHttp.ResponseStatus,
                        body = lastHttp.ResponseBody,
                        headers = lastHttp.Headers
                    }, PrettyJsonOptions);
                }
                else
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        message = "Response will be available after execution.",
                        component = nodeName,
                        type = "Http"
                    }, PrettyJsonOptions);
                }

                PreviewLogs = $"[{now}] HTTP preview refreshed\n[{now}] Target: {method} {url}";
                AppendExtractionPreview(now);
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            if (nodeType == "GraphQl")
            {
                var endpoint = GetSettingValue("Endpoint", "https://api.example.com/graphql");
                var query = GetSettingValue("Query", "query { health }");
                var variables = GetSettingValue("Variables", "{}");
                var latestGraphExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var lastGraphQl = GetLastExecutionData<GraphQlData>(nodeId);
                PreviewOutput = lastGraphQl?.ResponseBody ?? string.Empty;                var graphRequestRuns = nodeExecutionResults
                    .Where(result => result.Data is GraphQlData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        endpoint = (result.Data as GraphQlData)?.Endpoint,
                        query = (result.Data as GraphQlData)?.Query,
                        variables = (result.Data as GraphQlData)?.Variables,
                        headers = (result.Data as GraphQlData)?.Headers
                    })
                    .ToList();
                var graphRuns = nodeExecutionResults
                    .Where(result => result.Data is GraphQlData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        responseStatus = (result.Data as GraphQlData)?.ResponseStatus,
                        responseBody = (result.Data as GraphQlData)?.ResponseBody
                    })
                    .ToList();

                if (latestGraphExecution != null
                    && latestGraphExecution.Data is not GraphQlData
                    && string.Equals(latestGraphExecution.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "GraphQl",
                        endpoint,
                        query,
                        variables,
                        status = latestGraphExecution.Status,
                        error = latestGraphExecution.Error,
                        threadIndex = latestGraphExecution.ThreadIndex,
                        durationMs = latestGraphExecution.DurationMs
                    }, PrettyJsonOptions);

                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = latestGraphExecution.Status,
                        error = latestGraphExecution.Error,
                        message = "Request failed before receiving a GraphQL response."
                    }, PrettyJsonOptions);

                    PreviewLogs = $"[{now}] GraphQL preview refreshed\n[{now}] Endpoint: {endpoint}\n[{now}] Last run failed: {latestGraphExecution.Error}";
                    AppendExtractionPreview(now);
                    RebuildPreviewLogsForSelectedNode();
                    return;
                }

                if (graphRequestRuns.Count > 0)
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        runs = graphRequestRuns
                    }, PrettyJsonOptions);
                }
                else
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "GraphQl",
                        endpoint,
                        query,
                        variables
                    }, PrettyJsonOptions);
                }

                if (graphRuns.Count > 0)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        runs = graphRuns
                    }, PrettyJsonOptions);
                }
                else if (lastGraphQl != null)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = lastGraphQl.ResponseStatus,
                        body = lastGraphQl.ResponseBody
                    }, PrettyJsonOptions);
                }
                else
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        message = "Response will be available after execution.",
                        component = nodeName,
                        type = "GraphQl"
                    }, PrettyJsonOptions);
                }

                PreviewLogs = $"[{now}] GraphQL preview refreshed\n[{now}] Endpoint: {endpoint}";
                AppendExtractionPreview(now);
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            if (nodeType == "Sql")
            {
                var provider = NormalizeSqlProvider(GetSettingValue("Provider", "SqlServer"));
                var connection = GetSettingValue("Connection", "Server=.;Database=master;Trusted_Connection=True;");
                var query = GetSettingValue("Query", "SELECT 1");
                var latestSqlExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var lastSql = GetLastExecutionData<SqlData>(nodeId);
                PreviewOutput = lastSql?.QueryResult != null ? JsonSerializer.Serialize(lastSql.QueryResult, PrettyJsonOptions) : string.Empty;
                var sqlRequestRuns = nodeExecutionResults
                    .Where(result => result.Data is SqlData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        provider = (result.Data as SqlData)?.Provider,
                        connection = (result.Data as SqlData)?.ConnectionString,
                        query = (result.Data as SqlData)?.Query
                    })
                    .ToList();
                var sqlRuns = nodeExecutionResults
                    .Where(result => result.Data is SqlData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        rows = (result.Data as SqlData)?.QueryResult
                    })
                    .ToList();

                if (latestSqlExecution != null
                    && latestSqlExecution.Data is not SqlData
                    && string.Equals(latestSqlExecution.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Sql",
                        provider,
                        connection,
                        query,
                        status = latestSqlExecution.Status,
                        error = latestSqlExecution.Error,
                        threadIndex = latestSqlExecution.ThreadIndex,
                        durationMs = latestSqlExecution.DurationMs
                    }, PrettyJsonOptions);

                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = latestSqlExecution.Status,
                        error = latestSqlExecution.Error,
                        message = "Execution failed before returning SQL data."
                    }, PrettyJsonOptions);

                    PreviewLogs = $"[{now}] SQL preview refreshed\n[{now}] Executing: {query}\n[{now}] Last run failed: {latestSqlExecution.Error}";
                    AppendExtractionPreview(now);
                    RebuildPreviewLogsForSelectedNode();
                    return;
                }

                if (sqlRequestRuns.Count > 0)
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        runs = sqlRequestRuns
                    }, PrettyJsonOptions);
                }
                else
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Sql",
                        provider,
                        connection,
                        query
                    }, PrettyJsonOptions);
                }

                if (sqlRuns.Count > 0)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        runs = sqlRuns
                    }, PrettyJsonOptions);
                }
                else if (lastSql != null)
                {
                    lastSql.Properties.TryGetValue("rowsAffected", out var rowsAffected);
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        rows = lastSql.QueryResult,
                        affectedRows = rowsAffected
                    }, PrettyJsonOptions);
                }
                else
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        message = "Response will be available after execution.",
                        component = nodeName,
                        type = "Sql"
                    }, PrettyJsonOptions);
                }

                PreviewLogs = $"[{now}] SQL preview refreshed\n[{now}] Executing: {query}";
                AppendExtractionPreview(now);
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            if (nodeType == "Threads")
            {
                PreviewOutput = string.Empty;
                var threadCount = GetSettingValue("ThreadCount", "1");
                var rampUp = GetSettingValue("RampUpSeconds", "1");
                var childIds = GetDescendantIds(SelectedNode).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var lastResults = FilterPreviewResults((_lastExecutionContext?.Results ?? new List<ExecutionResult>())
                    .Where(result => childIds.Contains(result.ComponentId))
                    , lastPerComponent: true)
                    .Select(result => (object)new
                    {
                        name = result.ComponentName,
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        passed = result.Passed,
                        error = result.Error,
                        data = result.Data
                    })
                    .ToList();

                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Threads",
                    threadCount,
                    rampUpSeconds = rampUp
                }, PrettyJsonOptions);

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    childResults = lastResults,
                    message = lastResults.Count == 0
                        ? "Run the TestPlan to see thread results."
                        : "Last thread results"
                }, PrettyJsonOptions);

                PreviewLogs = $"[{now}] Threads preview refreshed\n[{now}] ThreadCount: {threadCount}, RampUpSeconds: {rampUp}";
                AppendExtractionPreview(now);
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            if (nodeType == "Script")
            {
                var language = GetSettingValue("Language", "CSharp");
                var code = GetSettingValue("Code", string.Empty);
                var latestScriptExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                PreviewOutput = (latestScriptExecution?.Data as ScriptData)?.ExecutionResult ?? string.Empty;
                var scriptRequestRuns = nodeExecutionResults
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        language = (result.Data as ScriptData)?.ScriptLanguage,
                        code = (result.Data as ScriptData)?.ScriptCode
                    })
                    .ToList();
                var scriptRuns = nodeExecutionResults
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        output = (result.Data as ScriptData)?.ExecutionResult,
                        error = result.Error
                    })
                    .ToList();

                if (latestScriptExecution != null
                    && latestScriptExecution.Data is not ScriptData
                    && string.Equals(latestScriptExecution.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Script",
                        language,
                        code,
                        status = latestScriptExecution.Status,
                        error = latestScriptExecution.Error,
                        threadIndex = latestScriptExecution.ThreadIndex,
                        durationMs = latestScriptExecution.DurationMs
                    }, PrettyJsonOptions);

                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = latestScriptExecution.Status,
                        error = latestScriptExecution.Error,
                        message = "Script failed before returning execution data."
                    }, PrettyJsonOptions);

                    PreviewLogs = $"[{now}] Script preview refreshed\n[{now}] Last run failed: {latestScriptExecution.Error}";
                    AppendExtractionPreview(now);
                    RebuildPreviewLogsForSelectedNode();
                    return;
                }

                if (scriptRequestRuns.Count > 0)
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        runs = scriptRequestRuns
                    }, PrettyJsonOptions);
                }
                else
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Script",
                        language,
                        code
                    }, PrettyJsonOptions);
                }

                if (scriptRuns.Count > 0)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        runs = scriptRuns
                    }, PrettyJsonOptions);
                }
                else
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        message = "Response will be available after execution.",
                        component = nodeName,
                        type = "Script"
                    }, PrettyJsonOptions);
                }

                PreviewLogs = $"[{now}] Script preview refreshed";
                AppendExtractionPreview(now);
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            if (nodeType == "Foreach")
            {
                var sourceVariable = GetSettingValue("SourceVariable", string.Empty);
                var outputVariableSetting = GetSettingValue("OutputVariable", string.Empty);
                var effectiveOutputVar = string.IsNullOrWhiteSpace(outputVariableSetting)
                    ? "CurrentItem"
                    : outputVariableSetting.Trim();
                var latestForeachExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var lastForeachData = GetLastExecutionData<ForeachData>(nodeId);
                PreviewOutput = lastForeachData?.CurrentItem != null ? JsonSerializer.Serialize(lastForeachData.CurrentItem, PrettyJsonOptions) : string.Empty;
                var collection = lastForeachData?.Collection ?? new List<object>();
                var currentIndex = lastForeachData?.CurrentIndex ?? (collection.Count > 0 ? collection.Count - 1 : -1);
                var currentItem = lastForeachData?.CurrentItem;

                if (currentItem == null && currentIndex >= 0 && currentIndex < collection.Count)
                {
                    currentItem = collection[currentIndex];
                }

                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Foreach",
                    sourceVariable,
                    outputVariable = effectiveOutputVar,
                    totalItems = collection.Count,
                    collection
                }, PrettyJsonOptions);

                // Preview shows only the current item being processed
                if (currentItem != null)
                {
                    PreviewResponse = JsonSerializer.Serialize(currentItem, PrettyJsonOptions);
                }
                else
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        message = "Collection is empty or no item selected."
                    }, PrettyJsonOptions);
                }

                PreviewLogs = $"[{now}] Foreach preview refreshed\n[{now}] Source: {sourceVariable}\n[{now}] Output: {effectiveOutputVar}\n[{now}] Current index: {currentIndex}\n[{now}] Total items: {collection.Count}";
                AppendExtractionPreview(now);
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            if (nodeType == "Loop")
            {
                var iterations = int.TryParse(GetSettingValue("Iterations", "1"), out var iter) ? iter : 1;
                var latestLoopExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var lastLoopData = GetLastExecutionData<Test_Automation.Models.LoopData>(nodeId);
                
                // Show currentIteration in output tab
                PreviewOutput = lastLoopData?.CurrentIteration.ToString() ?? "0";
                
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Loop",
                    iterations,
                    currentIteration = lastLoopData?.CurrentIteration ?? 0
                }, PrettyJsonOptions);

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    runs = nodeExecutionResults.Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        data = result.Data
                    }).ToList()
                }, PrettyJsonOptions);

                PreviewLogs = $"[{now}] Loop preview refreshed\n[{now}] Iterations: {iterations}\n[{now}] Current iteration: {lastLoopData?.CurrentIteration ?? 0}";
                AppendExtractionPreview(now);
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            if (nodeType == "Dataset")
            {
                var sourcePath = GetSettingValue("SourcePath", string.Empty);
                var format = GetSettingValue("Format", "Auto");
                var sheetName = GetSettingValue("SheetName", string.Empty);
                var hasHeader = bool.TryParse(GetSettingValue("HasHeader", "true"), out var hh) ? hh : true;
                
                var latestDatasetExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var lastDataset = GetLastExecutionData<DatasetData>(nodeId);
                
                // Show actual rows data in output tab (not metadata)
                PreviewOutput = lastDataset?.Rows != null 
                    ? JsonSerializer.Serialize(lastDataset.Rows, PrettyJsonOptions) 
                    : string.Empty;

                var datasetRuns = nodeExecutionResults
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        rowCount = (result.Data as DatasetData)?.Rows?.Count ?? 0,
                        dataSource = (result.Data as DatasetData)?.DataSource
                    })
                    .ToList();

                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Dataset",
                    format,
                    sourcePath,
                    sheetName,
                    hasHeader
                }, PrettyJsonOptions);

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    runs = datasetRuns
                }, PrettyJsonOptions);

                PreviewLogs = $"[{now}] Dataset preview refreshed\n[{now}] Source: {sourcePath}\n[{now}] Rows: {lastDataset?.Rows?.Count ?? 0}";
                AppendExtractionPreview(now);
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            if (nodeType == "RandomGenerator")
            {
                var outputType = GetSettingValue("OutputType", "number");
                var latestRandomExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var lastRandomData = GetLastExecutionData<RandomGeneratorData>(nodeId);
                PreviewOutput = lastRandomData?.GeneratedValue ?? string.Empty;

                var randomRuns = nodeExecutionResults
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        generatedValue = (result.Data as RandomGeneratorData)?.GeneratedValue,
                        outputType = (result.Data as RandomGeneratorData)?.OutputType
                    })
                    .ToList();

                PreviewRequest = JsonSerializer.Serialize(new
                {
                    type = "RandomGenerator",
                    outputType,
                    generatedValue = lastRandomData?.GeneratedValue
                }, PrettyJsonOptions);

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    runs = randomRuns
                }, PrettyJsonOptions);

                PreviewLogs = $"[{now}] RandomGenerator preview refreshed\n[{now}] OutputType: {outputType}\n[{now}] Generated: {lastRandomData?.GeneratedValue}";
                AppendExtractionPreview(now);
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            var settings = SelectedNode.Settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.Key))
                .ToDictionary(setting => setting.Key, setting => setting.Value);

            var genericRuns = nodeExecutionResults
                .Select(result => new
                {
                    threadIndex = result.ThreadIndex,
                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    durationMs = result.DurationMs,
                    status = result.Status,
                    error = result.Error,
                    data = result.Data
                })
                .ToList();
            var latestGenericExecution = nodeExecutionResults
                .OrderByDescending(result => result.EndTime ?? result.StartTime)
                .FirstOrDefault();
            PreviewOutput = latestGenericExecution?.Data is Test_Automation.Models.ComponentData compData && compData.Properties.Count > 0
                ? JsonSerializer.Serialize(compData.Properties, PrettyJsonOptions)
                : latestGenericExecution?.Output ?? string.Empty;

            if (latestGenericExecution != null
                && latestGenericExecution.Data == null
                && string.Equals(latestGenericExecution.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = nodeType,
                    settings,
                    status = latestGenericExecution.Status,
                    error = latestGenericExecution.Error,
                    threadIndex = latestGenericExecution.ThreadIndex,
                    durationMs = latestGenericExecution.DurationMs
                }, PrettyJsonOptions);

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    status = latestGenericExecution.Status,
                    error = latestGenericExecution.Error,
                    message = "Execution failed before returning component data."
                }, PrettyJsonOptions);

                PreviewLogs = $"[{now}] {nodeType} preview refreshed.\n[{now}] Last run failed: {latestGenericExecution.Error}";
                AppendExtractionPreview(now);
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            if (genericRuns.Count > 0)
            {
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    runs = genericRuns
                }, PrettyJsonOptions);
            }
            else
            {
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = nodeType,
                    settings
                }, PrettyJsonOptions);
            }

            if (genericRuns.Count > 0)
            {
                PreviewResponse = JsonSerializer.Serialize(new
                {
                    runs = genericRuns
                }, PrettyJsonOptions);
            }
            else
            {
                PreviewResponse = JsonSerializer.Serialize(new
                {
                    message = "Preview available when this component is executed.",
                    component = nodeName,
                    type = nodeType
                }, PrettyJsonOptions);
            }

            PreviewLogs = $"[{now}] {nodeType} preview refreshed.";
            AppendExtractionPreview(now);
        }

        private void PopulateTestPlanPreview(string timestamp, string nodeName)
        {
            if (SelectedNode == null)
            {
                return;
            }

            var testPlanNode = SelectedNode;
            var componentIds = GetDescendantIds(testPlanNode)
                .Append(testPlanNode.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var allPlanResults = (_lastExecutionContext?.Results ?? new List<ExecutionResult>())
                .Where(result => componentIds.Contains(result.ComponentId))
                .OrderBy(result => result.StartTime)
                .ToList();

            var planResults = FilterPreviewResults(allPlanResults, lastPerComponent: true);

            SetAssertionPreview("TestPlan", nodeName, planResults);

            var assertionSummary = BuildAssertionSummary(planResults);
            var componentBreakdown = planResults
                .GroupBy(result => new { result.ComponentId, result.ComponentName })
                .Select(group =>
                {
                    var latest = group
                        .OrderByDescending(result => result.EndTime ?? result.StartTime)
                        .First();
                    var componentAssertionSummary = BuildAssertionSummary(group);

                    return new
                    {
                        componentId = group.Key.ComponentId,
                        componentName = group.Key.ComponentName,
                        componentType = FindNodeById(group.Key.ComponentId)?.Type ?? "Unknown",
                        runs = group.Count(),
                        latestStatus = latest.Status,
                        latestDurationMs = latest.DurationMs,
                        latestThreadIndex = latest.ThreadIndex,
                        latestStartTime = latest.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        latestEndTime = latest.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        latestError = string.IsNullOrWhiteSpace(latest.Error) ? null : latest.Error,
                        assertionSummary = new
                        {
                            assertFailed = componentAssertionSummary.AssertFailed,
                            expectFailed = componentAssertionSummary.ExpectFailed,
                            passed = componentAssertionSummary.Passed
                        },
                        assertionDetails = BuildAssertionDetails(group),
                        latestData = latest.Data
                    };
                })
                .OrderBy(component => component.componentName)
                .ToList();

            var requestRuns = planResults
                .Select(result => new
                {
                    componentId = result.ComponentId,
                    componentName = result.ComponentName,
                    componentType = FindNodeById(result.ComponentId)?.Type ?? "Unknown",
                    threadIndex = result.ThreadIndex,
                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    status = result.Status,
                    durationMs = result.DurationMs,
                    request = BuildComponentRequestSnapshot(result.Data)
                })
                .ToList();

            var responseRuns = planResults
                .Select(result => new
                {
                    componentId = result.ComponentId,
                    componentName = result.ComponentName,
                    componentType = FindNodeById(result.ComponentId)?.Type ?? "Unknown",
                    threadIndex = result.ThreadIndex,
                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    status = result.Status,
                    passed = result.Passed,
                    durationMs = result.DurationMs,
                    error = result.Error,
                    response = BuildComponentResponseSnapshot(result.Data)
                })
                .ToList();

            var planStatus = planResults.Count == 0
                ? "not-run"
                : (planResults.Any(result => string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase))
                    ? "failed"
                    : "passed");

            var runtimeVariables = _lastExecutionContext?.Variables
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Build hierarchical variable structure for TestPlan scope
            var testPlanVariables = BuildDictionaryWithOverwrite(testPlanNode.Variables)
                .ToDictionary(entry => entry.Key, entry => CoercePreviewValue(entry.Value), StringComparer.OrdinalIgnoreCase);

            // Find parent Project node to get project variables
            var parentProjectNode = testPlanNode.Parent;
            var projectVariables = parentProjectNode != null
                ? BuildDictionaryWithOverwrite(parentProjectNode.Variables)
                    .ToDictionary(entry => entry.Key, entry => CoercePreviewValue(entry.Value), StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Build dynamic structure with testplan name as key
            var variablesDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "projectVariables", projectVariables }
            };
            variablesDict[testPlanNode.Name] = testPlanVariables;

            VariablesPreview = JsonSerializer.Serialize(variablesDict, PrettyJsonOptions);

            PreviewRequest = JsonSerializer.Serialize(new
            {
                component = nodeName,
                type = "TestPlan",
                id = testPlanNode.Id,
                enabled = testPlanNode.IsEnabled,
                plannedComponents = GetDescendantIds(testPlanNode).Count(),
                executedComponents = componentBreakdown.Count,
                runs = requestRuns,
                structure = BuildPreviewNodeStructure(testPlanNode)
            }, PrettyJsonOptions);

            PreviewResponse = JsonSerializer.Serialize(new
            {
                status = planStatus,
                summary = new
                {
                    executedComponents = componentBreakdown.Count,
                    totalRuns = planResults.Count,
                    passedRuns = planResults.Count(result => result.Passed),
                    failedRuns = planResults.Count(result => !result.Passed),
                    assertionSummary = new
                    {
                        assertFailed = assertionSummary.AssertFailed,
                        expectFailed = assertionSummary.ExpectFailed,
                        passed = assertionSummary.Passed
                    },
                    assertionDetails = BuildAssertionDetails(planResults)
                },
                runs = responseRuns,
                components = componentBreakdown
            }, PrettyJsonOptions);

            if (planResults.Count == 0)
            {
                PreviewLogs = string.Join("\n", new[]
                {
                    $"[{timestamp}] TestPlan preview refreshed.",
                    $"[{timestamp}] No component executions found for this TestPlan.",
                    $"[{timestamp}] Run this TestPlan or use Project run to populate assertions/results."
                });
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            var logLines = new List<string>
            {
                $"[{timestamp}] TestPlan preview refreshed.",
                $"[{timestamp}] Runs: {planResults.Count}, Components: {componentBreakdown.Count}, Passed: {planResults.Count(result => result.Passed)}, Failed: {planResults.Count(result => !result.Passed)}",
                $"[{timestamp}] Assertions: passed={assertionSummary.Passed}, assertFailed={assertionSummary.AssertFailed}, expectFailed={assertionSummary.ExpectFailed}"
            };

            foreach (var component in componentBreakdown)
            {
                logLines.Add($"[{timestamp}] - {component.componentName} ({component.componentType}): status={component.latestStatus}, runs={component.runs}, assertFailed={component.assertionSummary.assertFailed}, expectFailed={component.assertionSummary.expectFailed}");
            }

            PreviewLogs = string.Join("\n", logLines);
            AppendExtractionPreview(timestamp);
            RebuildPreviewLogsForSelectedNode();
        }

        private void PopulateProjectPreview(string timestamp, string nodeName)
        {
            if (SelectedNode == null)
            {
                return;
            }

            var projectNode = SelectedNode;
            var testPlanNodes = projectNode.Children
                .Where(node => string.Equals(node.Type, "TestPlan", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var allResults = _lastExecutionContext?.Results ?? new List<ExecutionResult>();
            var previewResults = FilterPreviewResults(allResults, lastPerComponent: true);
            var projectAssertionSummary = BuildAssertionSummary(previewResults);
            SetAssertionPreview("Project", nodeName, previewResults);

            var requestRuns = previewResults
                .Select(result => new
                {
                    componentId = result.ComponentId,
                    componentName = result.ComponentName,
                    componentType = FindNodeById(result.ComponentId)?.Type ?? "Unknown",
                    threadIndex = result.ThreadIndex,
                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    status = result.Status,
                    durationMs = result.DurationMs,
                    request = BuildComponentRequestSnapshot(result.Data)
                })
                .ToList();

            var testPlanRequests = testPlanNodes
                .Select(testPlan =>
                {
                    var componentIds = GetDescendantIds(testPlan)
                        .Append(testPlan.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var planResults = allResults
                        .Where(result => componentIds.Contains(result.ComponentId))
                        .OrderBy(result => result.StartTime)
                        .ToList();
                    planResults = FilterPreviewResults(planResults, lastPerComponent: true);

                    var componentRequests = planResults
                        .GroupBy(result => new { result.ComponentId, result.ComponentName })
                        .Select(group =>
                        {
                            var latest = group
                                .OrderByDescending(result => result.EndTime ?? result.StartTime)
                                .First();

                            return new
                            {
                                componentId = group.Key.ComponentId,
                                componentName = group.Key.ComponentName,
                                componentType = FindNodeById(group.Key.ComponentId)?.Type ?? "Unknown",
                                latestStatus = latest.Status,
                                latestThreadIndex = latest.ThreadIndex,
                                latestStartTime = latest.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestEndTime = latest.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestDurationMs = latest.DurationMs,
                                latestRequest = BuildComponentRequestSnapshot(latest.Data),
                                runs = group.Select(result => new
                                {
                                    threadIndex = result.ThreadIndex,
                                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                    status = result.Status,
                                    durationMs = result.DurationMs,
                                    request = BuildComponentRequestSnapshot(result.Data)
                                }).ToList()
                            };
                        })
                        .OrderBy(component => component.componentName)
                        .ToList();

                    var planStatus = planResults.Count == 0
                        ? "not-run"
                        : (planResults.Any(result => string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase))
                            ? "failed"
                            : "passed");

                    return new
                    {
                        id = testPlan.Id,
                        name = testPlan.Name,
                        status = planStatus,
                        totalRuns = planResults.Count,
                        components = componentRequests
                    };
                })
                .ToList();

            var responseRuns = previewResults
                .Select(result => new
                {
                    componentId = result.ComponentId,
                    componentName = result.ComponentName,
                    componentType = FindNodeById(result.ComponentId)?.Type ?? "Unknown",
                    threadIndex = result.ThreadIndex,
                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    status = result.Status,
                    passed = result.Passed,
                    durationMs = result.DurationMs,
                    error = result.Error,
                    response = BuildComponentResponseSnapshot(result.Data)
                })
                .ToList();

            var testPlanExecutions = testPlanNodes
                .Select(testPlan =>
                {
                    var componentIds = GetDescendantIds(testPlan)
                        .Append(testPlan.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var planResults = allResults
                        .Where(result => componentIds.Contains(result.ComponentId))
                        .OrderBy(result => result.StartTime)
                        .ToList();
                    planResults = FilterPreviewResults(planResults, lastPerComponent: true);

                    var componentRuns = planResults
                        .GroupBy(result => new { result.ComponentId, result.ComponentName })
                        .Select(group =>
                        {
                            var latest = group
                                .OrderByDescending(result => result.EndTime ?? result.StartTime)
                                .First();
                            var componentAssertionSummary = BuildAssertionSummary(group);

                            return new
                            {
                                componentId = group.Key.ComponentId,
                                componentName = group.Key.ComponentName,
                                componentType = FindNodeById(group.Key.ComponentId)?.Type ?? "Unknown",
                                runs = group.Count(),
                                latestStatus = latest.Status,
                                latestDurationMs = latest.DurationMs,
                                latestThreadIndex = latest.ThreadIndex,
                                latestStartTime = latest.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestEndTime = latest.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestError = string.IsNullOrWhiteSpace(latest.Error) ? null : latest.Error,
                                assertionSummary = new
                                {
                                    assertFailed = componentAssertionSummary.AssertFailed,
                                    expectFailed = componentAssertionSummary.ExpectFailed,
                                    passed = componentAssertionSummary.Passed
                                },
                                assertionDetails = BuildAssertionDetails(group),
                                latestData = latest.Data
                            };
                        })
                        .OrderBy(component => component.componentName)
                        .ToList();

                    var planAssertionSummary = BuildAssertionSummary(planResults);
                    var planStatus = planResults.Count == 0
                        ? "not-run"
                        : (planResults.Any(result => string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase))
                            ? "failed"
                            : "passed");

                    return new
                    {
                        id = testPlan.Id,
                        name = testPlan.Name,
                        enabled = testPlan.IsEnabled,
                        status = planStatus,
                        plannedComponents = GetDescendantIds(testPlan).Count(),
                        executedComponents = componentRuns.Count,
                        totalRuns = planResults.Count,
                        assertionSummary = new
                        {
                            assertFailed = planAssertionSummary.AssertFailed,
                            expectFailed = planAssertionSummary.ExpectFailed,
                            passed = planAssertionSummary.Passed
                        },
                        assertionDetails = BuildAssertionDetails(planResults),
                        components = componentRuns
                    };
                })
                .ToList();

            var testPlanResponses = testPlanNodes
                .Select(testPlan =>
                {
                    var componentIds = GetDescendantIds(testPlan)
                        .Append(testPlan.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var planResults = allResults
                        .Where(result => componentIds.Contains(result.ComponentId))
                        .OrderBy(result => result.StartTime)
                        .ToList();
                    planResults = FilterPreviewResults(planResults, lastPerComponent: true);

                    var componentResponses = planResults
                        .GroupBy(result => new { result.ComponentId, result.ComponentName })
                        .Select(group =>
                        {
                            var latest = group
                                .OrderByDescending(result => result.EndTime ?? result.StartTime)
                                .First();

                            return new
                            {
                                componentId = group.Key.ComponentId,
                                componentName = group.Key.ComponentName,
                                componentType = FindNodeById(group.Key.ComponentId)?.Type ?? "Unknown",
                                latestStatus = latest.Status,
                                latestPassed = latest.Passed,
                                latestDurationMs = latest.DurationMs,
                                latestThreadIndex = latest.ThreadIndex,
                                latestStartTime = latest.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestEndTime = latest.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestError = string.IsNullOrWhiteSpace(latest.Error) ? null : latest.Error,
                                latestResponse = BuildComponentResponseSnapshot(latest.Data),
                                runs = group.Select(result => new
                                {
                                    threadIndex = result.ThreadIndex,
                                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                    status = result.Status,
                                    passed = result.Passed,
                                    durationMs = result.DurationMs,
                                    error = result.Error,
                                    response = BuildComponentResponseSnapshot(result.Data)
                                }).ToList()
                            };
                        })
                        .OrderBy(component => component.componentName)
                        .ToList();

                    var planStatus = planResults.Count == 0
                        ? "not-run"
                        : (planResults.Any(result => string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase))
                            ? "failed"
                            : "passed");

                    return new
                    {
                        id = testPlan.Id,
                        name = testPlan.Name,
                        status = planStatus,
                        totalRuns = planResults.Count,
                        passedRuns = planResults.Count(result => result.Passed),
                        failedRuns = planResults.Count(result => !result.Passed),
                        components = componentResponses
                    };
                })
                .ToList();

            var projectVariables = BuildDictionaryWithOverwrite(projectNode.Variables)
                .ToDictionary(entry => entry.Key, entry => CoercePreviewValue(entry.Value), StringComparer.OrdinalIgnoreCase);

            // Build hierarchical variable structure for Project scope - includes all TestPlan variables
            var allTestPlanVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var testPlan in testPlanNodes)
            {
                var tpVars = BuildDictionaryWithOverwrite(testPlan.Variables)
                    .ToDictionary(entry => entry.Key, entry => CoercePreviewValue(entry.Value), StringComparer.OrdinalIgnoreCase);
                if (tpVars.Count > 0)
                {
                    allTestPlanVariables[testPlan.Name] = tpVars;
                }
            }

            VariablesPreview = JsonSerializer.Serialize(new
            {
                projectVariables = projectVariables,
                testPlans = allTestPlanVariables
            }, PrettyJsonOptions);

            PreviewRequest = JsonSerializer.Serialize(new
            {
                summary = new
                {
                    environment = SelectedEnvironment,
                    runMode = SelectedProjectRunMode,
                    previewDataMode = SelectedPreviewDataMode,
                    testPlanCount = testPlanNodes.Count,
                    plannedComponents = GetDescendantIds(projectNode).Count(),
                    executedComponents = previewResults.Select(result => result.ComponentId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    totalRuns = requestRuns.Count
                },
                testPlans = testPlanRequests
            }, PrettyJsonOptions);

            PreviewResponse = JsonSerializer.Serialize(new
            {
                status = _lastExecutionContext?.Status ?? "not-run",
                summary = new
                {
                    executedTestPlans = testPlanExecutions.Count(plan => !string.Equals(plan.status, "not-run", StringComparison.OrdinalIgnoreCase)),
                    executedComponents = previewResults.Select(result => result.ComponentId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    totalRuns = previewResults.Count,
                    passedRuns = previewResults.Count(result => result.Passed),
                    failedRuns = previewResults.Count(result => !result.Passed)
                },
                testPlans = testPlanResponses
            }, PrettyJsonOptions);

            if (previewResults.Count == 0)
            {
                PreviewLogs = string.Join("\n", new[]
                {
                    $"[{timestamp}] Project preview refreshed.",
                    $"[{timestamp}] No component executions found yet.",
                    $"[{timestamp}] Run using 'Run TestPlans' to see combined trace across all TestPlans."
                });
                RebuildPreviewLogsForSelectedNode();
                return;
            }

            var logLines = new List<string>
            {
                $"[{timestamp}] Project preview refreshed.",
                $"[{timestamp}] TestPlans: {testPlanNodes.Count}, Runs: {previewResults.Count}, Passed: {previewResults.Count(result => result.Passed)}, Failed: {previewResults.Count(result => !result.Passed)}"
            };

            foreach (var plan in testPlanExecutions)
            {
                logLines.Add($"[{timestamp}] - {plan.name}: {plan.status}, runs={plan.totalRuns}, components={plan.executedComponents}, assertFailed={plan.assertionSummary.assertFailed}, expectFailed={plan.assertionSummary.expectFailed}");
            }

            PreviewLogs = string.Join("\n", logLines);
            AppendExtractionPreview(timestamp);
            RebuildPreviewLogsForSelectedNode();
        }

        private static (int AssertFailed, int ExpectFailed, int Passed) BuildAssertionSummary(IEnumerable<ExecutionResult> results)
        {
            var assertFailed = 0;
            var expectFailed = 0;
            var passed = 0;

            foreach (var result in results)
            {
                if (result.AssertionResults == null)
                {
                    continue;
                }

                foreach (var assertion in result.AssertionResults)
                {
                    if (assertion.Passed)
                    {
                        passed++;
                        continue;
                    }

                    if (string.Equals(assertion.Mode, "Expect", StringComparison.OrdinalIgnoreCase))
                    {
                        expectFailed++;
                    }
                    else
                    {
                        assertFailed++;
                    }
                }
            }

            return (assertFailed, expectFailed, passed);
        }

        private static List<object> BuildAssertionDetails(IEnumerable<ExecutionResult> results)
        {
            return results
                .Where(result => result.AssertionResults != null && result.AssertionResults.Count > 0)
                .SelectMany(result => result.AssertionResults.Select(assertion => (object)new
                {
                    componentId = result.ComponentId,
                    componentName = result.ComponentName,
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    componentStatus = result.Status,
                    passed = assertion.Passed,
                    mode = assertion.Mode,
                    source = assertion.Source,
                    jsonPath = assertion.JsonPath,
                    condition = assertion.Condition,
                    expected = assertion.Expected,
                    actual = assertion.Actual,
                    message = assertion.Message
                }))
                .ToList();
        }

        private void SetAssertionPreview(string scopeType, string scopeName, IEnumerable<ExecutionResult> results)
        {
            var materialized = results.ToList();
            var summary = BuildAssertionSummary(materialized);
            var details = BuildAssertionDetails(materialized);

            AssertionPreview = JsonSerializer.Serialize(new
            {
                scope = new
                {
                    type = scopeType,
                    name = scopeName
                },
                previewDataMode = SelectedPreviewDataMode,
                summary = new
                {
                    total = details.Count,
                    passed = summary.Passed,
                    assertFailed = summary.AssertFailed,
                    expectFailed = summary.ExpectFailed
                },
                details
            }, PrettyJsonOptions);
        }

        private static object BuildComponentRequestSnapshot(object? data)
        {
            return data switch
            {
                HttpData http => new
                {
                    method = http.Method,
                    url = http.Url,
                    headers = http.Headers,
                    body = http.Body
                },
                GraphQlData graphQl => new
                {
                    endpoint = graphQl.Endpoint,
                    query = graphQl.Query,
                    variables = graphQl.Variables,
                    headers = graphQl.Headers
                },
                SqlData sql => new
                {
                    connectionString = sql.ConnectionString,
                    query = sql.Query
                },
                ScriptData script => new
                {
                    language = script.ScriptLanguage,
                    code = script.ScriptCode
                },
                AssertData assertion => new
                {
                    expected = assertion.ExpectedValue,
                    actual = assertion.ActualValue,
                    op = assertion.Operator
                },
                TimerData timer => new
                {
                    delayMs = timer.DelayMs
                },
                null => new
                {
                    message = "No request payload"
                },
                _ => data
            };
        }

        private static object BuildComponentResponseSnapshot(object? data)
        {
            return data switch
            {
                HttpData http => new
                {
                    responseStatus = http.ResponseStatus,
                    responseHeaders = http.ResponseHeaders,
                    responseBody = http.ResponseBody
                },
                GraphQlData graphQl => new
                {
                    responseStatus = graphQl.ResponseStatus,
                    responseBody = graphQl.ResponseBody
                },
                SqlData sql => new
                {
                    rows = sql.QueryResult,
                    rowsCount = sql.QueryResult?.Count ?? 0
                },
                ScriptData script => new
                {
                    output = script.ExecutionResult
                },
                AssertData assertion => new
                {
                    passed = assertion.Passed,
                    errorMessage = assertion.ErrorMessage,
                    expected = assertion.ExpectedValue,
                    actual = assertion.ActualValue
                },
                TimerData timer => new
                {
                    executed = timer.Executed,
                    delayMs = timer.DelayMs
                },
                null => new
                {
                    message = "No response payload"
                },
                _ => data
            };
        }

        private static object BuildPreviewNodeStructure(PlanNode node)
        {
            return new
            {
                id = node.Id,
                name = node.Name,
                type = node.Type,
                enabled = node.IsEnabled,
                children = node.Children.Select(BuildPreviewNodeStructure).ToList()
            };
        }

        private void AppendExtractionPreview(string timestamp)
        {
            if (SelectedNode == null || SelectedNode.Extractors.Count == 0)
            {
                return;
            }

            var lines = new List<string>
            {
                $"[{timestamp}] Variable extraction preview:",
                $"[{timestamp}] Note: preview only (runtime variables are created only during Run)."
            };

            foreach (var extractor in SelectedNode.Extractors)
            {
                if (string.IsNullOrWhiteSpace(extractor.VariableName) || string.IsNullOrWhiteSpace(extractor.Source))
                {
                    continue;
                }

                var sourceValue = ResolvePreviewSourceValue(extractor.Source);
                if (string.IsNullOrEmpty(sourceValue))
                {
                    lines.Add($"[{timestamp}] - {extractor.VariableName}: <missing source> ({extractor.Source})");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(extractor.JsonPath))
                {
                    lines.Add($"[{timestamp}] - {extractor.VariableName} = {sourceValue}");
                    continue;
                }

                var jsonPath = extractor.JsonPath.Trim();
                if (string.Equals(jsonPath, "$", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(jsonPath, "$.", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add($"[{timestamp}] - {extractor.VariableName} = {sourceValue}");
                    continue;
                }

                if (TryExtractJsonPath(sourceValue, extractor.JsonPath, out var extracted))
                {
                    lines.Add($"[{timestamp}] - {extractor.VariableName} = {extracted}");
                }
                else
                {
                    lines.Add($"[{timestamp}] - {extractor.VariableName}: <path not found>");
                }
            }

            PreviewLogs = string.Join("\n", new[] { PreviewLogs }.Concat(lines));
        }

        private void ResetHttpDetailPreviews()
        {
            HttpRequestHeadersPreview = "Select an HTTP component to see request headers.";
            HttpRequestCookiesPreview = "Select an HTTP component to see request cookies.";
            HttpRequestMetadataPreview = "Select an HTTP component to see request metadata.";
            HttpResponseHeadersPreview = "Select an HTTP component to see response headers.";
            HttpResponseCookiesPreview = "Select an HTTP component to see response cookies.";
            HttpResponseMetadataPreview = "Select an HTTP component to see response metadata.";
        }

        private Dictionary<string, string> BuildRequestHeadersFromSettings()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rawHeaders = GetSettingValue("Headers", "{}");
            if (!string.IsNullOrWhiteSpace(rawHeaders))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(rawHeaders);
                    if (parsed != null)
                    {
                        foreach (var item in parsed)
                        {
                            headers[item.Key] = item.Value;
                        }
                    }
                }
                catch
                {
                }
            }

            return headers;
        }

        private static List<string> ExtractCookiesFromHeaders(Dictionary<string, string> headers, string cookieHeaderName)
        {
            if (!headers.TryGetValue(cookieHeaderName, out var cookieHeader) || string.IsNullOrWhiteSpace(cookieHeader))
            {
                return new List<string>();
            }

            return cookieHeader
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(cookie => cookie.Trim())
                .Where(cookie => !string.IsNullOrWhiteSpace(cookie))
                .ToList();
        }

        private static string? TryGetHeaderValue(Dictionary<string, string> headers, string headerName)
        {
            return headers.TryGetValue(headerName, out var value) ? value : null;
        }

        private void ClearRequestButton_Click(object sender, RoutedEventArgs e)
        {
            PreviewRequest = string.Empty;
        }

        private void ClearResponseButton_Click(object sender, RoutedEventArgs e)
        {
            PreviewResponse = string.Empty;
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            PreviewLogs = string.Empty;

        }

        private void ClearVariablesButton_Click(object sender, RoutedEventArgs e)
        {
            VariablesPreview = string.Empty;
        }

        private void ClearAssertionsButton_Click(object sender, RoutedEventArgs e)
        {
            AssertionPreview = string.Empty;
        }

        private void ClearChildrenPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || _lastExecutionContext == null)
            {
                return;
            }

            var descendantIds = GetDescendantIds(SelectedNode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _lastExecutionContext.Results.RemoveAll(result => descendantIds.Contains(result.ComponentId));
            RefreshComponentPreview();
        }

        private void ClearSelectedNodePreviewWithChildren(PlanNode selectedNode)
        {
            if (selectedNode == null || _lastExecutionContext == null)
            {
                return;
            }

            var idsToClear = GetDescendantIds(selectedNode)
                .Concat(new[] { selectedNode.Id })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _lastExecutionContext.Results.RemoveAll(result => idsToClear.Contains(result.ComponentId));
            RefreshComponentPreview();
        }

        private TData? GetLastExecutionData<TData>(string componentId) where TData : ComponentData
        {
            if (_lastExecutionContext == null)
            {
                return null;
            }

            return _lastExecutionContext.Results
                .Where(result => result.Data is TData && string.Equals(result.ComponentId, componentId, StringComparison.OrdinalIgnoreCase))
                .Select(result => result.Data)
                .OfType<TData>()
                .LastOrDefault();
        }

        private List<ExecutionResult> GetExecutionResults(string componentId)
        {
            if (_lastExecutionContext == null)
            {
                return new List<ExecutionResult>();
            }

            return _lastExecutionContext.Results
                .Where(result => string.Equals(result.ComponentId, componentId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(result => result.ThreadIndex)
                .ThenBy(result => result.StartTime)
                .ToList();
        }

        private List<ExecutionResult> GetExecutionResultsForScope(PlanNode node)
        {
            if (_lastExecutionContext == null)
            {
                return new List<ExecutionResult>();
            }

            var scopedIds = GetDescendantIds(node)
                .Append(node.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _lastExecutionContext.Results
                .Where(result => scopedIds.Contains(result.ComponentId))
                .OrderBy(result => result.ThreadIndex)
                .ThenBy(result => result.StartTime)
                .ToList();
        }

        private static IEnumerable<string> GetDescendantNames(PlanNode node)
        {
            foreach (var child in node.Children)
            {
                yield return child.Name;
                foreach (var descendant in GetDescendantNames(child))
                {
                    yield return descendant;
                }
            }
        }

        private static IEnumerable<string> GetDescendantIds(PlanNode node)
        {
            foreach (var child in node.Children)
            {
                yield return child.Id;
                foreach (var descendant in GetDescendantIds(child))
                {
                    yield return descendant;
                }
            }
        }

        private string? ResolvePreviewSourceValue(string source)
        {
            if (string.IsNullOrWhiteSpace(source) || SelectedNode == null)
            {
                return null;
            }

            if (string.Equals(source, "PreviewRequest", StringComparison.OrdinalIgnoreCase))
            {
                return PreviewRequest;
            }

            if (string.Equals(source, "PreviewResponse", StringComparison.OrdinalIgnoreCase))
            {
                return ExpandEmbeddedJsonStringsInJsonText(PreviewResponse);
            }

            if (string.Equals(source, "PreviewLogs", StringComparison.OrdinalIgnoreCase))
            {
                return PreviewLogs;
            }

            if (string.Equals(source, "AssertionPreview", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(source, "Assertions", StringComparison.OrdinalIgnoreCase))
            {
                return AssertionPreview;
            }

            if (string.Equals(source, "HttpRequestHeadersPreview", StringComparison.OrdinalIgnoreCase))
            {
                return HttpRequestHeadersPreview;
            }

            if (string.Equals(source, "HttpRequestCookiesPreview", StringComparison.OrdinalIgnoreCase))
            {
                return HttpRequestCookiesPreview;
            }

            if (string.Equals(source, "HttpRequestMetadataPreview", StringComparison.OrdinalIgnoreCase))
            {
                return HttpRequestMetadataPreview;
            }

            if (string.Equals(source, "HttpResponseHeadersPreview", StringComparison.OrdinalIgnoreCase))
            {
                return HttpResponseHeadersPreview;
            }

            if (string.Equals(source, "HttpResponseCookiesPreview", StringComparison.OrdinalIgnoreCase))
            {
                return HttpResponseCookiesPreview;
            }

            if (string.Equals(source, "HttpResponseMetadataPreview", StringComparison.OrdinalIgnoreCase))
            {
                return HttpResponseMetadataPreview;
            }

            if (string.Equals(source, "JsonPreview", StringComparison.OrdinalIgnoreCase))
            {
                return JsonPreview;
            }

            if (string.Equals(source, "PreviewVariables", StringComparison.OrdinalIgnoreCase))
            {
                // Return variables from last execution as JSON
                try
                {
                    // Use a HashSet to track unique variable names and avoid duplicates
                    var uniqueVars = new LinkedList<KeyValuePair<string, string>>();
                    var seenVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // First try _lastExecutionContext.Variables
                    var context = _lastExecutionContext;
                    if (context?.Variables != null && context.Variables.Count > 0)
                    {
                        foreach (var kvp in context.Variables)
                        {
                            if (!string.IsNullOrWhiteSpace(kvp.Key) && !seenVars.Contains(kvp.Key))
                            {
                                seenVars.Add(kvp.Key);
                                uniqueVars.AddLast(new KeyValuePair<string, string>(kvp.Key, kvp.Value?.ToString() ?? string.Empty));
                            }
                        }
                    }
                    
                    // Also check VariablesPreview property for additional unique variables
                    if (!string.IsNullOrEmpty(VariablesPreview) && !string.Equals(VariablesPreview, "{}", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(VariablesPreview);
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                if (!string.IsNullOrWhiteSpace(prop.Name) && !seenVars.Contains(prop.Name))
                                {
                                    seenVars.Add(prop.Name);
                                    uniqueVars.AddLast(new KeyValuePair<string, string>(prop.Name, prop.Value.ToString()));
                                }
                            }
                        }
                        catch { }
                    }
                    
                    // Try to get variables from execution results' PreviewData.VariableExtractions
                    if (context?.Results != null)
                    {
                        foreach (var result in context.Results)
                        {
                            if (result.PreviewData?.VariableExtractions != null)
                            {
                                foreach (var extraction in result.PreviewData.VariableExtractions)
                                {
                                    if (!string.IsNullOrWhiteSpace(extraction.VariableName) && !seenVars.Contains(extraction.VariableName))
                                    {
                                        seenVars.Add(extraction.VariableName);
                                        uniqueVars.AddLast(new KeyValuePair<string, string>(extraction.VariableName, extraction.ExtractedValue));
                                    }
                                }
                            }
                        }
                    }

                    if (uniqueVars.Count > 0)
                    {
                        var varsDict = uniqueVars.ToDictionary(k => k.Key, v => v.Value);
                        return System.Text.Json.JsonSerializer.Serialize(varsDict);
                    }
                }
                catch
                {
                    // Ignore errors accessing runtime variables
                }
                return null;
            }

            var settings = SelectedNode.Settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.Key))
                .ToDictionary(setting => setting.Key, setting => setting.Value);

            if (settings.TryGetValue(source, out var value))
            {
                return value;
            }

            // Handle Variable sources (Variable.xxx)
            if (source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
            {
                var varName = source.Substring("Variable.".Length);

                // First try runtime variables from last execution
                try
                {
                    var runtimeVars = Test_Automation.Models.ExecutionContext.LastExecutionVariables;
                    if (runtimeVars != null && runtimeVars.TryGetValue(varName, out var runtimeValue))
                    {
                        return runtimeValue?.ToString();
                    }
                }
                catch
                {
                    // Ignore errors accessing runtime variables
                }

                // Then try static node variables (ObservableCollection<NodeSetting>)
                if (SelectedNode.Variables != null)
                {
                    var staticVar = SelectedNode.Variables.FirstOrDefault(v => 
                        string.Equals(v.Key, varName, StringComparison.OrdinalIgnoreCase));
                    if (staticVar != null)
                    {
                        return staticVar.Value;
                    }
                }
            }

            return null;
        }

        private static string ExpandEmbeddedJsonStringsInJsonText(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return jsonText;
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                var normalized = ConvertJsonElementForExtraction(doc.RootElement);
                return JsonSerializer.Serialize(normalized, PrettyJsonOptions);
            }
            catch
            {
                return jsonText;
            }
        }

        private static object? ConvertJsonElementForExtraction(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in element.EnumerateObject())
                    {
                        obj[property.Name] = ConvertJsonElementForExtraction(property.Value);
                    }

                    return obj;
                }
                case JsonValueKind.Array:
                {
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElementForExtraction(item));
                    }

                    return list;
                }
                case JsonValueKind.String:
                {
                    var text = element.GetString() ?? string.Empty;
                    if (TryParseJsonString(text, out var parsed))
                    {
                        return ConvertJsonElementForExtraction(parsed);
                    }

                    return text;
                }
                case JsonValueKind.Number:
                    return element.GetRawText();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                default:
                    return null;
            }
        }

        private static bool TryParseJsonString(string? text, out JsonElement parsedElement)
        {
            parsedElement = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (!(trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                && !(trimmed.StartsWith("[") && trimmed.EndsWith("]")))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                parsedElement = doc.RootElement.Clone();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryExtractJsonPath(string json, string path, out string extracted)
        {
            extracted = string.Empty;
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var element = doc.RootElement;
                var normalized = path.Trim();
                if (normalized.StartsWith("$"))
                {
                    normalized = normalized.TrimStart('$');
                    if (normalized.StartsWith("."))
                    {
                        normalized = normalized.Substring(1);
                    }
                }

                var segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
                foreach (var segment in segments)
                {
                    if (!TryResolveSegment(ref element, segment))
                    {
                        return false;
                    }
                }

                extracted = element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString() ?? string.Empty,
                    JsonValueKind.Number => element.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => "null",
                    _ => element.GetRawText()
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveSegment(ref JsonElement element, string segment)
        {
            var remaining = segment;
            while (remaining.Length > 0)
            {
                var bracketIndex = remaining.IndexOf('[');
                if (bracketIndex < 0)
                {
                    return TryResolvePropertyOrIndex(ref element, remaining);
                }

                var propertyName = remaining.Substring(0, bracketIndex);
                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (!TryResolvePropertyOrIndex(ref element, propertyName))
                    {
                        return false;
                    }
                }

                var endBracket = remaining.IndexOf(']', bracketIndex + 1);
                if (endBracket < 0)
                {
                    return false;
                }

                var indexValue = remaining.Substring(bracketIndex + 1, endBracket - bracketIndex - 1);
                if (!int.TryParse(indexValue, out var index))
                {
                    return false;
                }

                if (element.ValueKind == JsonValueKind.String
                    && TryParseJsonString(element.GetString(), out var parsedFromString))
                {
                    element = parsedFromString;
                }

                if (element.ValueKind != JsonValueKind.Array || index < 0 || index >= element.GetArrayLength())
                {
                    return false;
                }

                element = element[index];
                remaining = remaining.Substring(endBracket + 1);
            }

            return true;
        }

        private static bool TryResolvePropertyOrIndex(ref JsonElement element, string token)
        {
            if (element.ValueKind == JsonValueKind.String
                && TryParseJsonString(element.GetString(), out var parsedFromString))
            {
                element = parsedFromString;
            }

            if (element.ValueKind == JsonValueKind.Array && int.TryParse(token, out var index))
            {
                if (index < 0 || index >= element.GetArrayLength())
                {
                    return false;
                }

                element = element[index];
                return true;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetPropertyIgnoreCase(element, token, out var next))
            {
                return false;
            }

            element = next;
            return true;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string token, out JsonElement next)
        {
            if (element.TryGetProperty(token, out next))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, token, StringComparison.OrdinalIgnoreCase))
                {
                    next = property.Value;
                    return true;
                }
            }

            next = default;
            return false;
        }

        private static object BuildNodeObject(PlanNode node)
        {
            var settings = BuildDictionaryWithOverwrite(node.Settings);

            var variables = BuildDictionaryWithOverwrite(node.Variables);

            var extractors = node.Extractors
                .Where(extractor => !string.IsNullOrWhiteSpace(extractor.Source) || !string.IsNullOrWhiteSpace(extractor.VariableName))
                .Select(extractor => new
                {
                    source = extractor.Source,
                    jsonPath = extractor.JsonPath,
                    variableName = extractor.VariableName
                })
                .ToList();

            var assertions = node.Assertions
                .Where(assertion => !string.IsNullOrWhiteSpace(assertion.Source))
                .Select(assertion => new
                {
                    mode = assertion.Mode,
                    source = assertion.Source,
                    jsonPath = assertion.JsonPath,
                    condition = assertion.Condition,
                    expected = assertion.Expected
                })
                .ToList();

            return new
            {
                type = node.Type,
                name = node.Name,
                enabled = node.IsEnabled,
                settings,
                variables,
                extractors,
                assertions,
                children = node.Children.Select(BuildNodeObject).ToList()
            };
        }

        private static NodeFileModel ToFileModel(PlanNode node)
        {
            return new NodeFileModel
            {
                Id = node.Id,
                Type = node.Type,
                Name = node.Name,
                Enabled = node.IsEnabled,
                Settings = BuildDictionaryWithOverwrite(node.Settings),
                Variables = BuildDictionaryWithOverwrite(node.Variables),
                Extractors = node.Extractors
                    .Where(extractor => !string.IsNullOrWhiteSpace(extractor.Source) || !string.IsNullOrWhiteSpace(extractor.VariableName))
                    .Select(extractor => new VariableExtractionFileModel
                    {
                        Source = extractor.Source,
                        JsonPath = extractor.JsonPath,
                        VariableName = extractor.VariableName
                    })
                    .ToList(),
                Assertions = node.Assertions
                    .Where(assertion => !string.IsNullOrWhiteSpace(assertion.Source))
                    .Select(assertion => new AssertionFileModel
                    {
                        Mode = assertion.Mode,
                        Source = assertion.Source,
                        JsonPath = assertion.JsonPath,
                        Condition = assertion.Condition,
                        Expected = assertion.Expected
                    })
                    .ToList(),
                Children = node.Children.Select(ToFileModel).ToList()
            };
        }

        private static PlanNode FromFileModel(NodeFileModel model, PlanNode? parent)
        {
            var node = new PlanNode(model.Type, model.Name, string.IsNullOrWhiteSpace(model.Id) ? null : model.Id)
            {
                Parent = parent,
                IsEnabled = model.Enabled
            };

            node.Settings.Clear();
            foreach (var setting in model.Settings)
            {
                node.Settings.Add(new NodeSetting(setting.Key, setting.Value));
            }

            node.Variables.Clear();
            if (model.Variables != null)
            {
                foreach (var variable in model.Variables)
                {
                    node.Variables.Add(new NodeSetting(variable.Key, variable.Value));
                }
            }

            node.Extractors.Clear();
            foreach (var extractor in model.Extractors)
            {
                node.Extractors.Add(new VariableExtractionRule(extractor.Source, extractor.JsonPath, extractor.VariableName));
            }

            node.Assertions.Clear();
            if (model.Assertions != null)
            {
                foreach (var assertion in model.Assertions)
                {
                    node.Assertions.Add(new AssertionRule(assertion.Source, assertion.JsonPath, assertion.Condition, assertion.Expected, assertion.Mode));
                }
            }

            foreach (var child in model.Children)
            {
                var childNode = FromFileModel(child, node);
                node.Children.Add(childNode);
            }

            return node;
        }

        private string GetSettingValue(string key, string fallback)
        {
            if (SelectedNode == null)
            {
                return fallback;
            }

            var normalizedKey = (key ?? string.Empty).Trim();
            var setting = SelectedNode.Settings.FirstOrDefault(current =>
                string.Equals(current.Key?.Trim(), normalizedKey, StringComparison.OrdinalIgnoreCase));
            return setting?.Value ?? fallback;
        }

        private void SetSettingValue(string key, string value)
        {
            if (SelectedNode == null)
            {
                return;
            }

            var normalizedKey = (key ?? string.Empty).Trim();
            var matchingSettings = SelectedNode.Settings
                .Where(current => string.Equals(current.Key?.Trim(), normalizedKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var setting = matchingSettings.FirstOrDefault();
            if (setting == null)
            {
                setting = new NodeSetting(normalizedKey, value);
                SelectedNode.Settings.Add(setting);
            }
            else if (setting.Value != value)
            {
                setting.Value = value;
            }

            // Keep only one setting per key so reads are deterministic.
            foreach (var duplicate in matchingSettings.Skip(1).ToList())
            {
                SelectedNode.Settings.Remove(duplicate);
            }

            RefreshJsonPreview();
        }

        public string GetVariableUniquenessLabel(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return string.Empty;
            }

            if (!_variableUsageMap.TryGetValue(variableName.Trim(), out var paths) || paths.Count == 0)
            {
                return "Unique";
            }

            return paths.Count > 1 ? $"Repeated ({paths.Count})" : "Unique";
        }

        public string GetVariableUsageTooltip(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return "Variable name is empty.";
            }

            if (!_variableUsageMap.TryGetValue(variableName.Trim(), out var paths) || paths.Count == 0)
            {
                return "No usage path found.";
            }

            return string.Join("\n", paths);
        }

        private void RebuildVariableUsageMap()
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in RootNodes)
            {
                BuildVariableUsageMap(root, map);
            }

            _variableUsageMap = map;
            VariableUsageVersion++;
            RefreshVariableKeyOptions();
        }

        private void RefreshVariableKeyOptions()
        {
            _variableKeyOptions.Clear();
            _variableKeyOptions.Add(string.Empty);
            foreach (var key in _variableUsageMap.Keys.OrderBy(k => k))
            {
                _variableKeyOptions.Add(key);
            }
        }

        private static void BuildVariableUsageMap(PlanNode node, Dictionary<string, List<string>> map)
        {
            var path = BuildNodePath(node);

            foreach (var variable in node.Variables)
            {
                AddVariableUsage(map, variable.Key, path);
            }

            foreach (var setting in node.Settings)
            {
                if (IsVariableSettingKey(setting.Key))
                {
                    AddVariableUsage(map, setting.Value, $"{path} -> {setting.Key}");
                }
            }

            foreach (var extractor in node.Extractors)
            {
                AddVariableUsage(map, extractor.VariableName, $"{path} -> Extractor");
            }

            if (string.Equals(node.Type, "VariableExtractor", StringComparison.OrdinalIgnoreCase))
            {
                var variableNameSetting = node.Settings.FirstOrDefault(setting => string.Equals(setting.Key, "VariableName", StringComparison.OrdinalIgnoreCase));
                AddVariableUsage(map, variableNameSetting?.Value, path);
            }

            foreach (var child in node.Children)
            {
                BuildVariableUsageMap(child, map);
            }
        }

        private static void AddVariableUsage(Dictionary<string, List<string>> map, string? variableName, string path)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return;
            }

            var key = variableName.Trim();
            if (!map.TryGetValue(key, out var paths))
            {
                paths = new List<string>();
                map[key] = paths;
            }

            if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        private static string BuildNodePath(PlanNode node)
        {
            var parts = new List<string>();
            var current = node;
            while (current != null)
            {
                if (!string.Equals(current.Type, "Project", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(current.Name);
                }

                current = current.Parent;
            }

            parts.Reverse();
            return parts.Count == 0 ? "Project" : string.Join("->", parts);
        }

        private static Dictionary<string, string> BuildDictionaryWithOverwrite(IEnumerable<NodeSetting> source)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in source)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                dictionary[entry.Key.Trim()] = entry.Value;
            }

            return dictionary;
        }

        private PlanNode? GetProjectNode()
        {
            return RootNodes.FirstOrDefault(node => string.Equals(node.Type, "Project", StringComparison.OrdinalIgnoreCase));
        }

        private string GetProjectSettingValue(string key, string fallback)
        {
            var projectNode = GetProjectNode();
            if (projectNode == null)
            {
                return fallback;
            }

            var setting = projectNode.Settings.FirstOrDefault(current => string.Equals(current.Key, key, StringComparison.OrdinalIgnoreCase));
            return setting?.Value ?? fallback;
        }

        private void SetProjectSettingValue(string key, string value)
        {
            var projectNode = GetProjectNode();
            if (projectNode == null)
            {
                return;
            }

            var setting = projectNode.Settings.FirstOrDefault(current => string.Equals(current.Key, key, StringComparison.OrdinalIgnoreCase));
            if (setting == null)
            {
                setting = new NodeSetting(key, value);
                projectNode.Settings.Add(setting);
            }
            else if (!string.Equals(setting.Value, value, StringComparison.Ordinal))
            {
                setting.Value = value;
            }

            RefreshJsonPreview();
        }

        private void RefreshApiCatalogState()
        {
            if (_isSynchronizingCatalogEditor)
            {
                RebuildParsedApiCatalogFromEditor();
                SyncCatalogSelectionFromCurrentNode();
                return;
            }

            foreach (var existing in _projectUrlCatalogEntries.ToList())
            {
                UnregisterApiCatalogBaseEntry(existing);
            }

            _projectUrlCatalogEntries.Clear();

            foreach (var entry in ParseApiCatalog(ProjectUrlCatalogJson))
            {
                RegisterApiCatalogBaseEntry(entry);
                _projectUrlCatalogEntries.Add(entry);
            }

            RebuildParsedApiCatalogFromEditor();
            OnPropertyChanged(nameof(ProjectUrlCatalogEntries));
            OnPropertyChanged(nameof(ProjectUrlCatalogJson));
            SyncCatalogSelectionFromCurrentNode();
        }

        private void RegisterApiCatalogBaseEntry(ApiCatalogBaseUrlEntry entry)
        {
            entry.PropertyChanged += ApiCatalogBaseEntry_PropertyChanged;
            entry.Endpoints.CollectionChanged += ApiCatalogEndpoints_CollectionChanged;

            foreach (var endpoint in entry.Endpoints)
            {
                RegisterApiCatalogEndpointEntry(endpoint);
            }
        }

        private void UnregisterApiCatalogBaseEntry(ApiCatalogBaseUrlEntry entry)
        {
            entry.PropertyChanged -= ApiCatalogBaseEntry_PropertyChanged;
            entry.Endpoints.CollectionChanged -= ApiCatalogEndpoints_CollectionChanged;

            foreach (var endpoint in entry.Endpoints)
            {
                UnregisterApiCatalogEndpointEntry(endpoint);
            }
        }

        private void RegisterApiCatalogEndpointEntry(ApiCatalogEndpointEntry endpoint)
        {
            endpoint.PropertyChanged += ApiCatalogEndpointEntry_PropertyChanged;
            endpoint.Parameters.CollectionChanged += ApiCatalogEndpointParameters_CollectionChanged;
            foreach (var param in endpoint.Parameters)
            {
                RegisterApiCatalogParameterEntry(param);
            }
        }

        private void UnregisterApiCatalogEndpointEntry(ApiCatalogEndpointEntry endpoint)
        {
            endpoint.PropertyChanged -= ApiCatalogEndpointEntry_PropertyChanged;
            endpoint.Parameters.CollectionChanged -= ApiCatalogEndpointParameters_CollectionChanged;
            foreach (var param in endpoint.Parameters)
            {
                UnregisterApiCatalogParameterEntry(param);
            }
        }

        private void RegisterApiCatalogParameterEntry(ApiCatalogParameterEntry param)
        {
            param.PropertyChanged += ApiCatalogParameterEntry_PropertyChanged;
        }

        private void UnregisterApiCatalogParameterEntry(ApiCatalogParameterEntry param)
        {
            param.PropertyChanged -= ApiCatalogParameterEntry_PropertyChanged;
        }

        private void ApiCatalogBaseEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SyncApiCatalogSettingFromEditor();
        }

        private void ApiCatalogEndpointEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SyncApiCatalogSettingFromEditor();
        }

        private void ApiCatalogEndpoints_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var endpoint in e.OldItems.OfType<ApiCatalogEndpointEntry>())
                {
                    UnregisterApiCatalogEndpointEntry(endpoint);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var endpoint in e.NewItems.OfType<ApiCatalogEndpointEntry>())
                {
                    RegisterApiCatalogEndpointEntry(endpoint);
                }
            }

            SyncApiCatalogSettingFromEditor();
        }

        private void ApiCatalogEndpointParameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var param in e.OldItems.OfType<ApiCatalogParameterEntry>())
                {
                    UnregisterApiCatalogParameterEntry(param);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var param in e.NewItems.OfType<ApiCatalogParameterEntry>())
                {
                    RegisterApiCatalogParameterEntry(param);
                }
            }

            SyncApiCatalogSettingFromEditor();
        }

        private void ApiCatalogParameterEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SyncApiCatalogSettingFromEditor();
        }

        private void SyncApiCatalogSettingFromEditor()
        {
            if (_isSynchronizingCatalogEditor)
            {
                return;
            }

            _isSynchronizingCatalogEditor = true;
            try
            {
                RebuildParsedApiCatalogFromEditor();

                var serialized = JsonSerializer.Serialize(_projectUrlCatalogEntries.Select(entry => new
                {
                    name = entry.Name,
                    baseUrl = entry.BaseUrl,
                    endpoints = entry.Endpoints.Select(endpoint => new
                    {
                        name = endpoint.Name,
                        path = endpoint.Path,
                        method = endpoint.Method,
                        body = endpoint.Body,
                        parameters = endpoint.Parameters.Select(p => new { key = p.Key, value = p.Value }).ToList(),
                        headers = endpoint.Headers,
                        query = endpoint.Query,
                        variables = endpoint.Variables
                    }).ToList()
                }).ToList(), PrettyJsonOptions);

                SetProjectSettingValue("UrlCatalog", serialized);
                OnPropertyChanged(nameof(ProjectUrlCatalogJson));
                SyncCatalogSelectionFromCurrentNode();
            }
            finally
            {
                _isSynchronizingCatalogEditor = false;
            }
        }

        private void RebuildParsedApiCatalogFromEditor()
        {
            _parsedApiCatalog.Clear();
            var normalizedEntries = _projectUrlCatalogEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToList();

            foreach (var entry in normalizedEntries)
            {
                var parsedEntry = new ApiCatalogBaseUrlEntry
                {
                    Name = entry.Name.Trim(),
                    BaseUrl = entry.BaseUrl?.Trim() ?? string.Empty
                };

                foreach (var endpoint in entry.Endpoints.Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Name)))
                {
                    parsedEntry.Endpoints.Add(new ApiCatalogEndpointEntry
                    {
                        Name = endpoint.Name.Trim(),
                        Path = endpoint.Path ?? string.Empty,
                        Method = endpoint.Method ?? string.Empty,
                        Body = endpoint.Body ?? string.Empty,
                        Parameters = new ObservableCollection<ApiCatalogParameterEntry>(
                            endpoint.Parameters.Select(p => new ApiCatalogParameterEntry { Key = p.Key, Value = p.Value })),
                        Headers = endpoint.Headers ?? string.Empty,
                        Query = endpoint.Query ?? string.Empty,
                        Variables = endpoint.Variables ?? string.Empty
                    });
                }

                _parsedApiCatalog.Add(parsedEntry);
            }

            _catalogBaseUrlOptions.Clear();
            foreach (var entry in _parsedApiCatalog.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                _catalogBaseUrlOptions.Add(entry.Name);
            }

            OnPropertyChanged(nameof(CatalogBaseUrlOptions));
            OnPropertyChanged(nameof(CatalogEndpointOptions));
        }

        private void SyncCatalogSelectionFromCurrentNode()
        {
            RefreshCatalogEndpointOptions();
            OnPropertyChanged(nameof(CatalogEndpointOptions));
            OnPropertyChanged(nameof(SelectedHttpCatalogBase));
            OnPropertyChanged(nameof(SelectedHttpCatalogEndpoint));
            OnPropertyChanged(nameof(SelectedGraphQlCatalogBase));
            OnPropertyChanged(nameof(SelectedGraphQlCatalogEndpoint));
        }

        private void RefreshCatalogEndpointOptions()
        {
            _isApplyingCatalogSelection = true;
            try
            {
                _catalogEndpointOptions.Clear();

                // Read directly from settings to avoid property-setter side-effects.
                var baseName = GetSettingValue("CatalogBase", string.Empty);
                var selectedEndpoint = GetSettingValue("CatalogEndpoint", string.Empty);

                var baseEntry = _parsedApiCatalog.FirstOrDefault(entry => string.Equals(entry.Name, baseName, StringComparison.OrdinalIgnoreCase));
                if (baseEntry == null)
                {
                    // Keep currently stored selection visible even if base/options are not yet ready.
                    if (!string.IsNullOrWhiteSpace(selectedEndpoint))
                    {
                        _catalogEndpointOptions.Add(selectedEndpoint);
                    }
                    return;
                }

                foreach (var endpoint in baseEntry.Endpoints.OrderBy(endpoint => endpoint.Name, StringComparer.OrdinalIgnoreCase))
                {
                    _catalogEndpointOptions.Add(endpoint.Name);
                }

                if (_catalogEndpointOptions.Any(name => string.Equals(name, selectedEndpoint, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                // Keep persisted value visible/selected even if it no longer matches current catalog.
                if (!string.IsNullOrWhiteSpace(selectedEndpoint))
                {
                    _catalogEndpointOptions.Add(selectedEndpoint);
                }
            }
            finally
            {
                _isApplyingCatalogSelection = false;
            }
        }

        private void ApplySelectedCatalogEndpointToCurrentNode()
        {
            if (SelectedNode == null)
            {
                return;
            }

            var isHttp = string.Equals(SelectedNode.Type, "Http", StringComparison.OrdinalIgnoreCase);
            var isGraphQl = string.Equals(SelectedNode.Type, "GraphQl", StringComparison.OrdinalIgnoreCase);
            if (!isHttp && !isGraphQl)
            {
                return;
            }

            // Resolve from the latest stored project catalog to avoid stale in-memory options.
            var latestCatalog = ParseApiCatalog(ProjectUrlCatalogJson);

            var baseName = isHttp ? SelectedHttpCatalogBase : SelectedGraphQlCatalogBase;
            var endpointName = isHttp ? SelectedHttpCatalogEndpoint : SelectedGraphQlCatalogEndpoint;

            var baseEntry = latestCatalog.FirstOrDefault(entry => string.Equals(entry.Name, baseName, StringComparison.OrdinalIgnoreCase));
            if (baseEntry == null)
            {
                MessageBox.Show("Selected Base URL was not found in Project URLs.", "Apply Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var endpoint = baseEntry.Endpoints.FirstOrDefault(entry => string.Equals(entry.Name, endpointName, StringComparison.OrdinalIgnoreCase));
            if (endpoint == null)
            {
                MessageBox.Show("Selected Endpoint was not found under the chosen Base URL.", "Apply Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var resolvedUrl = CombineBaseUrlAndPath(baseEntry.BaseUrl, endpoint.Path);

            _isApplyingCatalogSelection = true;
            try
            {
                if (isHttp)
                {
                    HttpUrl = AppendHttpParameters(resolvedUrl, endpoint.Parameters);

                    if (!string.IsNullOrWhiteSpace(endpoint.Method))
                    {
                        HttpMethod = endpoint.Method;
                    }

                    if (endpoint.Body != null)
                    {
                        HttpBody = endpoint.Body;
                    }

                    if (!string.IsNullOrWhiteSpace(endpoint.Headers))
                    {
                        HttpHeaders = endpoint.Headers;
                        OnPropertyChanged(nameof(HttpHeaders));
                    }

                    OnPropertyChanged(nameof(HttpUrl));
                    OnPropertyChanged(nameof(HttpUrlResolved));
                    OnPropertyChanged(nameof(HttpMethod));
                    OnPropertyChanged(nameof(HttpBody));
                }
                else
                {
                    GraphQlEndpoint = resolvedUrl;

                    if (!string.IsNullOrWhiteSpace(endpoint.Query))
                    {
                        GraphQlQuery = endpoint.Query;
                    }
                    else if (!string.IsNullOrWhiteSpace(endpoint.Body))
                    {
                        GraphQlQuery = endpoint.Body;
                    }

                    if (!string.IsNullOrWhiteSpace(endpoint.Variables))
                    {
                        GraphQlVariables = endpoint.Variables;
                    }

                    OnPropertyChanged(nameof(GraphQlEndpoint));
                    OnPropertyChanged(nameof(GraphQlQuery));
                    OnPropertyChanged(nameof(GraphQlVariables));
                }
            }
            finally
            {
                _isApplyingCatalogSelection = false;
            }

            RefreshComponentPreview();
        }

        private static string CombineBaseUrlAndPath(string baseUrl, string path)
        {
            var normalizedBase = (baseUrl ?? string.Empty).Trim();
            var normalizedPath = (path ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return normalizedBase;
            }

            if (Uri.TryCreate(normalizedPath, UriKind.Absolute, out _))
            {
                return normalizedPath;
            }

            if (string.IsNullOrWhiteSpace(normalizedBase))
            {
                return normalizedPath;
            }

            return $"{normalizedBase.TrimEnd('/')}/{normalizedPath.TrimStart('/')}";
        }

        private static string AppendHttpParameters(string url, IEnumerable<ApiCatalogParameterEntry> parameters)
        {
            var pairs = parameters
                .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                .Select(p => Uri.EscapeDataString(p.Key.Trim()) + "=" + Uri.EscapeDataString(p.Value?.Trim() ?? string.Empty))
                .ToList();

            if (pairs.Count == 0)
            {
                return url;
            }

            var separator = url.Contains("?", StringComparison.Ordinal) ? "&" : "?";
            return url + separator + string.Join("&", pairs);
        }

        private static List<ApiCatalogBaseUrlEntry> ParseApiCatalog(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<ApiCatalogBaseUrlEntry>();
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new CatalogParameterCollectionConverter() }
                };

                var array = JsonSerializer.Deserialize<List<ApiCatalogBaseUrlEntry>>(raw, options);
                if (array != null)
                {
                    return NormalizeCatalog(array);
                }

                var wrapped = JsonSerializer.Deserialize<ApiCatalogWrapper>(raw, options);
                if (wrapped?.BaseUrls != null)
                {
                    return NormalizeCatalog(wrapped.BaseUrls);
                }
            }
            catch
            {
                // Invalid JSON should not block editing; options will simply be empty.
            }

            return new List<ApiCatalogBaseUrlEntry>();
        }

        private static List<ApiCatalogBaseUrlEntry> NormalizeCatalog(IEnumerable<ApiCatalogBaseUrlEntry> source)
        {
            return source
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .Select(entry =>
                {
                    var normalizedEntry = new ApiCatalogBaseUrlEntry
                    {
                        Name = entry.Name.Trim(),
                        BaseUrl = entry.BaseUrl?.Trim() ?? string.Empty
                    };

                    foreach (var endpoint in (entry.Endpoints ?? new ObservableCollection<ApiCatalogEndpointEntry>())
                        .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Name)))
                    {
                        normalizedEntry.Endpoints.Add(new ApiCatalogEndpointEntry
                        {
                            Name = endpoint.Name.Trim(),
                            Path = endpoint.Path ?? string.Empty,
                            Method = endpoint.Method ?? string.Empty,
                            Body = endpoint.Body ?? string.Empty,
                            Parameters = new ObservableCollection<ApiCatalogParameterEntry>(
                                (endpoint.Parameters ?? new ObservableCollection<ApiCatalogParameterEntry>())
                                .Select(p => new ApiCatalogParameterEntry { Key = p.Key, Value = p.Value })),
                            Headers = endpoint.Headers ?? string.Empty,
                            Query = endpoint.Query ?? string.Empty,
                            Variables = endpoint.Variables ?? string.Empty
                        });
                    }

                    return normalizedEntry;
                })
                .ToList();
        }

        private sealed class ApiCatalogWrapper
        {
            public List<ApiCatalogBaseUrlEntry> BaseUrls { get; set; } = new();
        }

        private string ResolveWithProjectVariables(string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                return template;
            }

            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            var variables = projectNode != null
                ? BuildDictionaryWithOverwrite(projectNode.Variables)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(SelectedEnvironment))
            {
                variables["env"] = SelectedEnvironment;
            }

            return System.Text.RegularExpressions.Regex.Replace(template, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value;
                return variables.TryGetValue(key, out var value) ? value : match.Value;
            });
        }

        private void NormalizeDuplicateVariables(PlanNode node, NodeSetting? preferred = null)
        {
            if (_isNormalizingVariables)
            {
                return;
            }

            _isNormalizingVariables = true;
            try
            {
                if (preferred != null && !string.IsNullOrWhiteSpace(preferred.Key))
                {
                    var duplicates = node.Variables
                        .Where(variable => !ReferenceEquals(variable, preferred)
                            && string.Equals(variable.Key, preferred.Key, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var duplicate in duplicates)
                    {
                        node.Variables.Remove(duplicate);
                    }
                }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var index = node.Variables.Count - 1; index >= 0; index--)
                {
                    var variable = node.Variables[index];
                    if (string.IsNullOrWhiteSpace(variable.Key))
                    {
                        continue;
                    }

                    var key = variable.Key.Trim();
                    if (!seen.Add(key))
                    {
                        node.Variables.RemoveAt(index);
                    }
                    else if (!string.Equals(variable.Key, key, StringComparison.Ordinal))
                    {
                        variable.Key = key;
                    }
                }
            }
            finally
            {
                _isNormalizingVariables = false;
            }
        }

        private PlanNode? FindNodeByVariablesCollection(object? sender)
        {
            if (sender is not ObservableCollection<NodeSetting> variables)
            {
                return null;
            }

            foreach (var root in RootNodes)
            {
                var found = FindNodeByVariablesCollection(root, variables);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static PlanNode? FindNodeByVariablesCollection(PlanNode node, ObservableCollection<NodeSetting> variables)
        {
            if (ReferenceEquals(node.Variables, variables))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var found = FindNodeByVariablesCollection(child, variables);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static PlanNode? FindNodeContainingVariable(PlanNode node, NodeSetting variable)
        {
            if (node.Variables.Contains(variable))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var found = FindNodeContainingVariable(child, variable);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static PlanNode? FindNodeContainingSetting(PlanNode node, NodeSetting setting)
        {
            if (node.Settings.Contains(setting))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var found = FindNodeContainingSetting(child, setting);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static bool IsVariableSettingKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var normalized = key.Trim();
            if (string.Equals(normalized, "Variables", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return normalized.EndsWith("Variable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "VariableName", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith("VariableName", StringComparison.OrdinalIgnoreCase);
        }

        private void NotifySelectedNodeEditorProperties()
        {
            OnPropertyChanged(nameof(IsProjectSelected));
            OnPropertyChanged(nameof(IsComponentSelected));
            OnPropertyChanged(nameof(HasSelectedNodeChildren));
            OnPropertyChanged(nameof(IsHttpSelected));
            OnPropertyChanged(nameof(IsGraphQlSelected));
            OnPropertyChanged(nameof(IsSqlSelected));
            OnPropertyChanged(nameof(IsDatasetSelected));
            OnPropertyChanged(nameof(IsTimerSelected));
            OnPropertyChanged(nameof(IsLoopSelected));
            OnPropertyChanged(nameof(IsIfSelected));
            OnPropertyChanged(nameof(IsThreadsSelected));
            OnPropertyChanged(nameof(IsForeachSelected));
            OnPropertyChanged(nameof(IsAssertSelected));
            OnPropertyChanged(nameof(IsVariableExtractorSelected));
            OnPropertyChanged(nameof(IsScriptSelected));
            OnPropertyChanged(nameof(IsTestPlanSelected));
            OnPropertyChanged(nameof(SelectedExecutionType));
            OnPropertyChanged(nameof(TestPlanThreadCount));
            OnPropertyChanged(nameof(AvailableVariablesForExtractor));
            OnPropertyChanged(nameof(ProjectVariablesForEditor));
            OnPropertyChanged(nameof(TestPlanVariablesForEditor));

            OnPropertyChanged(nameof(ProjectDescription));
            OnPropertyChanged(nameof(ProjectEnvironment));
            OnPropertyChanged(nameof(ProjectUrlCatalogJson));
            // SelectedHttpCatalogBase/Endpoint and SelectedGraphQl* are handled by
            // SyncCatalogSelectionFromCurrentNode(), which runs before this method.
            OnPropertyChanged(nameof(SelectedEnvironment));
            OnPropertyChanged(nameof(HttpMethod));
            OnPropertyChanged(nameof(HttpUrl));
            OnPropertyChanged(nameof(HttpUrlResolved));
            OnPropertyChanged(nameof(HttpRequestHeadersPreview));
            OnPropertyChanged(nameof(HttpRequestCookiesPreview));
            OnPropertyChanged(nameof(HttpRequestMetadataPreview));
            OnPropertyChanged(nameof(HttpResponseHeadersPreview));
            OnPropertyChanged(nameof(HttpResponseCookiesPreview));
            OnPropertyChanged(nameof(HttpResponseMetadataPreview));
            OnPropertyChanged(nameof(HttpBody));
            OnPropertyChanged(nameof(HttpHeaders));
            OnPropertyChanged(nameof(HttpAuthType));
            RaiseHttpAuthVisibilityChanged();
            OnPropertyChanged(nameof(HttpAuthUsername));
            OnPropertyChanged(nameof(HttpAuthPassword));
            OnPropertyChanged(nameof(HttpAuthToken));
            OnPropertyChanged(nameof(HttpApiKeyName));
            OnPropertyChanged(nameof(HttpApiKeyValue));
            OnPropertyChanged(nameof(HttpApiKeyLocation));
            OnPropertyChanged(nameof(HttpOAuthTokenUrl));
            OnPropertyChanged(nameof(HttpOAuthClientId));
            OnPropertyChanged(nameof(HttpOAuthClientSecret));
            OnPropertyChanged(nameof(HttpOAuthScope));
            OnPropertyChanged(nameof(GraphQlEndpoint));
            OnPropertyChanged(nameof(GraphQlQuery));
            OnPropertyChanged(nameof(GraphQlVariables));
            OnPropertyChanged(nameof(GraphQlHeaders));
            OnPropertyChanged(nameof(GraphQlAuthType));
            RaiseGraphQlAuthVisibilityChanged();
            OnPropertyChanged(nameof(GraphQlAuthUsername));
            OnPropertyChanged(nameof(GraphQlAuthPassword));
            OnPropertyChanged(nameof(GraphQlAuthToken));
            OnPropertyChanged(nameof(GraphQlApiKeyName));
            OnPropertyChanged(nameof(GraphQlApiKeyValue));
            OnPropertyChanged(nameof(GraphQlApiKeyLocation));
            OnPropertyChanged(nameof(GraphQlOAuthTokenUrl));
            OnPropertyChanged(nameof(GraphQlOAuthClientId));
            OnPropertyChanged(nameof(GraphQlOAuthClientSecret));
            OnPropertyChanged(nameof(GraphQlOAuthScope));
            RefreshSqlAuthTypeOptions();
            OnPropertyChanged(nameof(SqlProvider));
            OnPropertyChanged(nameof(SqlConnection));
            OnPropertyChanged(nameof(SqlConnectionResolved));
            OnPropertyChanged(nameof(SqlQuery));
            OnPropertyChanged(nameof(SqlQueryResolved));
            OnPropertyChanged(nameof(SqlAuthType));
            OnPropertyChanged(nameof(SqlAuthTypeOptions));
            RaiseSqlAuthVisibilityChanged();
            OnPropertyChanged(nameof(SqlAuthUsername));
            OnPropertyChanged(nameof(SqlAuthPassword));
            OnPropertyChanged(nameof(DatasetFormat));
            OnPropertyChanged(nameof(DatasetSourcePath));
            OnPropertyChanged(nameof(DatasetResolvedSourcePath));
            OnPropertyChanged(nameof(DatasetSheetName));
            OnPropertyChanged(nameof(DatasetCsvDelimiter));
            OnPropertyChanged(nameof(DatasetHasHeader));
            OnPropertyChanged(nameof(DatasetJsonArrayPath));
            OnPropertyChanged(nameof(DatasetXmlRowPath));
            OnPropertyChanged(nameof(DatasetMaxRows));
            OnPropertyChanged(nameof(DatasetPreviewRows));
            OnPropertyChanged(nameof(DatasetPreviewStatus));
            OnPropertyChanged(nameof(IsDatasetExcel));
            OnPropertyChanged(nameof(IsDatasetCsv));
            OnPropertyChanged(nameof(IsDatasetJson));
            OnPropertyChanged(nameof(IsDatasetXml));
            OnPropertyChanged(nameof(TimerDelayMs));
            OnPropertyChanged(nameof(LoopIterations));
            OnPropertyChanged(nameof(ForeachSourceVariable));
            OnPropertyChanged(nameof(ForeachOutputVariable));
            OnPropertyChanged(nameof(IfCondition));
            OnPropertyChanged(nameof(ThreadCount));
            OnPropertyChanged(nameof(RampUpSeconds));
            OnPropertyChanged(nameof(AssertExpected));
            OnPropertyChanged(nameof(AssertActual));
            OnPropertyChanged(nameof(ExtractorPattern));
            OnPropertyChanged(nameof(ExtractorVariableName));
            OnPropertyChanged(nameof(ScriptLanguage));
            OnPropertyChanged(nameof(ScriptCode));
            OnPropertyChanged(nameof(IsRandomGeneratorSelected));
            OnPropertyChanged(nameof(IsBase64Selected));
            OnPropertyChanged(nameof(RandomOutputType));
            OnPropertyChanged(nameof(RandomMin));
            OnPropertyChanged(nameof(RandomMax));
            OnPropertyChanged(nameof(RandomLength));
            OnPropertyChanged(nameof(RandomDecimalPlaces));
            OnPropertyChanged(nameof(RandomArrayLength));
            OnPropertyChanged(nameof(RandomItemType));
            OnPropertyChanged(nameof(RandomEmailDomain));
            OnPropertyChanged(nameof(RandomVariableName));
            OnPropertyChanged(nameof(RandomJsonStructure));
            OnPropertyChanged(nameof(RandomIncludeUpper));
            OnPropertyChanged(nameof(RandomIncludeLower));
            OnPropertyChanged(nameof(RandomIncludeNumbers));
            OnPropertyChanged(nameof(RandomIncludeSpecial));
            OnPropertyChanged(nameof(RandomShowStringOptions));
            OnPropertyChanged(nameof(RandomShowArrayOptions));
            OnPropertyChanged(nameof(RandomShowJsonOptions));
            OnPropertyChanged(nameof(RandomShowEmailOption));
            OnPropertyChanged(nameof(Base64Input));
            OnPropertyChanged(nameof(Base64Operation));
            OnPropertyChanged(nameof(Base64DataType));
            OnPropertyChanged(nameof(Base64FilePath));
            OnPropertyChanged(nameof(Base64Encoding));
            OnPropertyChanged(nameof(Base64OutputVariable));
            OnPropertyChanged(nameof(IsFileSelected));
            OnPropertyChanged(nameof(FileOperation));
            OnPropertyChanged(nameof(FileSourcePath));
            OnPropertyChanged(nameof(FileSourcePathResolved));
            OnPropertyChanged(nameof(FileDestinationPath));
            OnPropertyChanged(nameof(FileDestinationPathResolved));
            OnPropertyChanged(nameof(FileContent));
            OnPropertyChanged(nameof(FileEncoding));
            OnPropertyChanged(nameof(FileOverwrite));
            OnPropertyChanged(nameof(FileFilter));
            OnPropertyChanged(nameof(FileOutputVariable));
            OnPropertyChanged(nameof(FileRecursive));
            OnPropertyChanged(nameof(FileIncludeMetadata));
            OnPropertyChanged(nameof(FileShowSourcePath));
            OnPropertyChanged(nameof(FileShowDestinationPath));
            OnPropertyChanged(nameof(FileShowContent));
            OnPropertyChanged(nameof(FileShowEncoding));
            OnPropertyChanged(nameof(FileShowOverwrite));
            OnPropertyChanged(nameof(FileShowFileFilter));
            OnPropertyChanged(nameof(FileShowOutputVariable));
            OnPropertyChanged(nameof(FileShowRecursive));
            OnPropertyChanged(nameof(FileShowIncludeMetadata));
            OnPropertyChanged(nameof(FileShowSourceFileBrowse));
            OnPropertyChanged(nameof(FileShowSourceFolderBrowse));
            OnPropertyChanged(nameof(FileShowDestinationFileBrowse));
            OnPropertyChanged(nameof(FileShowDestinationFolderBrowse));
            OnPropertyChanged(nameof(FileDestinationFolder));
            OnPropertyChanged(nameof(FileDestinationFileName));
            OnPropertyChanged(nameof(FileAppend));
            OnPropertyChanged(nameof(FileReadMode));
            OnPropertyChanged(nameof(FileSelectedFilePaths));
            OnPropertyChanged(nameof(FileShowDestinationFolder));
            OnPropertyChanged(nameof(FileShowDestinationFileName));
            OnPropertyChanged(nameof(FileShowAppend));
            OnPropertyChanged(nameof(FileShowReadMode));
            OnPropertyChanged(nameof(FileShowSelectedFilesBrowse));
            OnPropertyChanged(nameof(FileResultPreview));
            OnPropertyChanged(nameof(IsExcelSelected));
            OnPropertyChanged(nameof(ExcelFileModeNew));
            OnPropertyChanged(nameof(ExcelFileModeExisting));
            OnPropertyChanged(nameof(ExcelFilePath));
            OnPropertyChanged(nameof(ExcelFolderPath));
            OnPropertyChanged(nameof(ExcelFileName));
            OnPropertyChanged(nameof(ExcelSheetName));
            OnPropertyChanged(nameof(ExcelSelectedSheet));
            OnPropertyChanged(nameof(ExcelOperation));
            OnPropertyChanged(nameof(ExcelColumn));
            OnPropertyChanged(nameof(ExcelRow));
            OnPropertyChanged(nameof(ExcelValue));
            OnPropertyChanged(nameof(ExcelValuesJson));
            OnPropertyChanged(nameof(ExcelDeleteStartColumn));
            OnPropertyChanged(nameof(ExcelDeleteStartRow));
            OnPropertyChanged(nameof(ExcelDeleteEndColumn));
            OnPropertyChanged(nameof(ExcelDeleteEndRow));
            OnPropertyChanged(nameof(ExcelJsonData));
            OnPropertyChanged(nameof(ExcelJsonToolTip));
            OnPropertyChanged(nameof(ExcelShowSheetName));
            OnPropertyChanged(nameof(ExcelShowSheetDropdown));
            OnPropertyChanged(nameof(ExcelShowOperation));
            OnPropertyChanged(nameof(ExcelShowJsonInput));
            if (IsExcelSelected)
            {
                RefreshExcelSheetNames();
                UpdateExcelExamples();
            }
            OnPropertyChanged(nameof(IsWhileSelected));
            OnPropertyChanged(nameof(WhileMaxIterations));
            OnPropertyChanged(nameof(WhileTimeoutMs));
            OnPropertyChanged(nameof(WhileEvaluationMode));
            OnPropertyChanged(nameof(WhileConditionJson));
            OnPropertyChanged(nameof(WhileEvaluationModeOptions));
            if (SelectedNode?.Type == "While")
            {
                UpdateWhileConditionRowsFromJson();
            }
        }

        private void WhileConditionRows_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Unsubscribe from old items
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is ConditionRow row)
                        row.PropertyChanged -= WhileConditionRow_PropertyChanged;
                }
            }

            // Subscribe to new items
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is ConditionRow row)
                        row.PropertyChanged += WhileConditionRow_PropertyChanged;
                }
            }

            if (_isSyncingWhileConditionRows) return;
            UpdateWhileConditionJsonFromRows();
        }

        private void WhileConditionRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingWhileConditionRows) return;
            UpdateWhileConditionJsonFromRows();
        }

        private void UpdateWhileConditionJsonFromRows()
        {
            if (SelectedNode?.Type != "While") return;
            _isSyncingWhileConditionRows = true;
            try
            {
                var json = JsonSerializer.Serialize(WhileConditionRows.ToList(), new JsonSerializerOptions { WriteIndented = true });
                WhileConditionJson = json;
            }
            finally
            {
                _isSyncingWhileConditionRows = false;
            }
        }

        private void UpdateWhileConditionRowsFromJson()
        {
            if (SelectedNode?.Type != "While") return;
            var json = WhileConditionJson;
            try
            {
                var rows = JsonSerializer.Deserialize<List<ConditionRow>>(json) ?? new List<ConditionRow>();
                _isSyncingWhileConditionRows = true;
                WhileConditionRows.Clear();
                foreach (var row in rows)
                {
                    WhileConditionRows.Add(row);
                }
                _isSyncingWhileConditionRows = false;
            }
            catch
            {
                _isSyncingWhileConditionRows = false;
                // ignore invalid JSON
            }
        }

        private void RaiseHttpAuthVisibilityChanged()
        {
            OnPropertyChanged(nameof(HttpShowBasicFields));
            OnPropertyChanged(nameof(HttpShowBearerFields));
            OnPropertyChanged(nameof(HttpShowApiKeyFields));
            OnPropertyChanged(nameof(HttpShowOAuthFields));
        }

        private void RaiseGraphQlAuthVisibilityChanged()
        {
            OnPropertyChanged(nameof(GraphQlShowBasicFields));
            OnPropertyChanged(nameof(GraphQlShowBearerFields));
            OnPropertyChanged(nameof(GraphQlShowApiKeyFields));
            OnPropertyChanged(nameof(GraphQlShowOAuthFields));
        }

        private void RaiseSqlAuthVisibilityChanged()
        {
            OnPropertyChanged(nameof(SqlShowBasicFields));
        }

        private void RebuildExtractorSourceOptions()
        {
            ExtractorSourceOptions.Clear();

            // Only add base sources - simplified to core options for all components
            foreach (var source in BaseExtractorSources)
            {
                ExtractorSourceOptions.Add(source);
            }

            // Removed: Variable option, settings keys, and node variables
            // Now showing only: PreviewResponse, PreviewRequest, PreviewVariables

            RefreshAssertionJsonTreePanel();
        }

        private void AddVariablesToSourceOptions(PlanNode node)
        {
            // Collect all variables from this node and ancestors
            var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = node;
            
            while (current != null)
            {
                foreach (var variable in current.Variables)
                {
                    if (!string.IsNullOrWhiteSpace(variable.Key))
                    {
                        variables.Add(variable.Key);
                    }
                }
                current = current.Parent;
            }

            // Add variables to options with "Variable." prefix to distinguish from settings
            foreach (var varName in variables.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                var varSource = $"Variable.{varName}";
                if (!ExtractorSourceOptions.Contains(varSource))
                {
                    ExtractorSourceOptions.Add(varSource);
                }
            }

            // Add runtime variables from last execution
            try
            {
                var runtimeVars = Test_Automation.Models.ExecutionContext.LastExecutionVariables;
                if (runtimeVars != null)
                {
                    foreach (var varName in runtimeVars.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
                    {
                        var varSource = $"Variable.{varName}";
                        if (!ExtractorSourceOptions.Contains(varSource))
                        {
                            ExtractorSourceOptions.Add(varSource);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors accessing runtime variables
            }
        }

        /// <summary>
        /// Rebuilds assertion source options filtered by testplan scope.
        /// Only includes global variables (Project) + local variables from the TestPlan ancestor.
        /// </summary>
        private void RebuildAssertionSourceOptions()
        {
            AssertionSourceOptions.Clear();

            // Add base sources - simplified to core options for all components
            AssertionSourceOptions.Add("PreviewOutput");
            AssertionSourceOptions.Add("PreviewVariables");
            AssertionSourceOptions.Add("PreviewRequest");
            AssertionSourceOptions.Add("PreviewResponse");

            if (SelectedNode == null)
            {
                return;
            }

            // Find the TestPlan ancestor and Project
            PlanNode? testPlanNode = null;
            PlanNode? projectNode = null;
            var current = SelectedNode;

            while (current != null)
            {
                if (string.Equals(current.Type, "TestPlan", StringComparison.OrdinalIgnoreCase))
                {
                    testPlanNode = current;
                }
                else if (string.Equals(current.Type, "Project", StringComparison.OrdinalIgnoreCase))
                {
                    projectNode = current;
                }
                current = current.Parent;
            }

            // Removed: variable options added from Project and TestPlan
            // Now showing only: PreviewVariables, PreviewRequest, PreviewResponse
        }

        private void AddExtractorButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null)
            {
                return;
            }

            SelectedNode.Extractors.Add(new VariableExtractionRule(string.Empty, string.Empty, string.Empty));
            RefreshJsonPreview();
        }

        private void RemoveExtractorButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || sender is not Button button || button.DataContext is not VariableExtractionRule extractor)
            {
                return;
            }

            SelectedNode.Extractors.Remove(extractor);
            RefreshJsonPreview();
        }

        private void AddConditionRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode?.Type != "While") return;
            WhileConditionRows.Add(new ConditionRow());
        }

        private void RemoveConditionRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ConditionRow row)
            {
                WhileConditionRows.Remove(row);
            }
        }

        private void ExploreJsonPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || sender is not Button button || button.DataContext == null)
            {
                return;
            }

            var (source, currentPath, applyPath) = button.DataContext switch
            {
                VariableExtractionRule extractor =>
                    (extractor.Source, extractor.JsonPath, new Action<string>(path => extractor.JsonPath = path)),
                AssertionRule assertion =>
                    (assertion.Source, assertion.JsonPath, new Action<string>(path => assertion.JsonPath = path)),
                _ => (string.Empty, string.Empty, new Action<string>(_ => { }))
            };

            if (string.IsNullOrWhiteSpace(source))
            {
                MessageBox.Show("Select a source first.", "JSON Path Explorer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sourceValue = ResolvePreviewSourceValue(source);
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                MessageBox.Show("Selected source is empty. Run or preview the component first.", "JSON Path Explorer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!TryParseJsonRoot(sourceValue, out var rootElement))
            {
                MessageBox.Show("Selected source is not a valid JSON document.", "JSON Path Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedPath = ShowJsonPathPicker(rootElement, currentPath);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            applyPath(selectedPath);
            RefreshJsonPreview();
        }

        private void AddAssertionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type == "Project")
            {
                return;
            }

            var defaultSource = "PreviewVariables";
            SelectedNode.Assertions.Add(new AssertionRule(defaultSource, "$", "Equals", string.Empty, "Assert"));
            RefreshJsonPreview();
        }

        private void RemoveAssertionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || sender is not Button button || button.DataContext is not AssertionRule assertion)
            {
                return;
            }

            SelectedNode.Assertions.Remove(assertion);
            RefreshJsonPreview();
        }

        //private void AssertionTreeSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (_isSyncingAssertionTreeSource)
        //    {
        //        return;
        //    }

        //    _assertionTreeSource = AssertionTreeSourceComboBox?.SelectedItem as string ?? string.Empty;
        //    RefreshAssertionJsonTreePanel();
        //}

        private void RefreshAssertionTreeButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshAssertionJsonTreePanel();
        }

        private void AssertionSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Refresh the JSON tree panel when assertion source changes
            RefreshAssertionJsonTreePanel();
        }

        private void AssertionJsonTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (AssertionJsonTreeView?.SelectedItem is not TreeViewItem selectedItem
                || selectedItem.Tag is not AssertionTreeNodeTag tag)
            {
                if (AssertionTreeSelectedPathTextBlock != null)
                {
                    AssertionTreeSelectedPathTextBlock.Text = "Path: (none)";
                }

                if (AssertionTreeValuePreviewTextBox != null)
                {
                    AssertionTreeValuePreviewTextBox.Text = "Select a tree node to preview value.";
                }

                return;
            }

            if (AssertionTreeSelectedPathTextBlock != null)
            {
                AssertionTreeSelectedPathTextBlock.Text = $"Path: {tag.Path}";
            }

            if (AssertionTreeValuePreviewTextBox != null)
            {
                AssertionTreeValuePreviewTextBox.Text = selectedItem.DataContext is JsonElement element
                    ? GetJsonElementPreview(element)
                    : tag.Expected;
            }
        }

        private void AddSelectedTreeNodeAssertionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type == "Project")
            {
                return;
            }

            if (AssertionJsonTreeView?.SelectedItem is not TreeViewItem selectedItem
                || selectedItem.Tag is not AssertionTreeNodeTag tag)
            {
                MessageBox.Show("Select a tree item first.", "Assertion Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.Equals(tag.Path, "$", StringComparison.Ordinal))
            {
                MessageBox.Show("Select a node below root '$' to create an assertion row.", "Assertion Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddOrUpdateAssertionFromTreeTag(tag);
            RefreshJsonPreview();
        }

        private void GenerateTreeAssertionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type == "Project" || AssertionJsonTreeView == null)
            {
                return;
            }

            // Generate from selected node subtree; fall back to root when nothing is selected.
            var selectedTreeItem = AssertionJsonTreeView.SelectedItem as TreeViewItem;
            var generationRoot = selectedTreeItem ?? AssertionJsonTreeView.Items.OfType<TreeViewItem>().FirstOrDefault();
            if (generationRoot == null)
            {
                MessageBox.Show("No tree items available to generate assertions.", "Assertion Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var allTags = EnumerateAssertionTreeTags(generationRoot)
                .Where(tag => tag.IsLeaf)
                .Where(tag => !string.Equals(tag.Path, "$", StringComparison.Ordinal))
                .ToList();

            if (allTags.Count == 0)
            {
                MessageBox.Show("Selected tree node has no child paths to generate assertion rows.", "Assertion Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var changedCount = 0;
            foreach (var tag in allTags)
            {
                if (AddOrUpdateAssertionFromTreeTag(tag))
                {
                    changedCount++;
                }
            }

            RefreshJsonPreview();
            MessageBox.Show($"Generated/updated {changedCount} assertion row(s).", "Assertion Tree", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private bool AddOrUpdateAssertionFromTreeTag(AssertionTreeNodeTag tag)
        {
            if (SelectedNode == null || SelectedNode.Type == "Project")
            {
                return false;
            }

            var existing = SelectedNode.Assertions.FirstOrDefault(assertion =>
                string.Equals(assertion.Source, tag.Source, StringComparison.OrdinalIgnoreCase)
                && string.Equals(assertion.JsonPath, tag.Path, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                SelectedNode.Assertions.Add(new AssertionRule(tag.Source, tag.Path, "Equals", tag.Expected, "Assert"));
                return true;
            }

            var changed = false;
            if (!string.Equals(existing.Expected, tag.Expected, StringComparison.Ordinal))
            {
                existing.Expected = tag.Expected;
                changed = true;
            }

            if (!string.Equals(existing.Condition, "Equals", StringComparison.OrdinalIgnoreCase))
            {
                existing.Condition = "Equals";
                changed = true;
            }

            return changed;
        }

        private IEnumerable<AssertionTreeNodeTag> EnumerateAssertionTreeTags(TreeViewItem item)
        {
            if (item.Tag is AssertionTreeNodeTag tag)
            {
                yield return tag;
            }

            foreach (var child in item.Items.OfType<TreeViewItem>())
            {
                foreach (var nested in EnumerateAssertionTreeTags(child))
                {
                    yield return nested;
                }
            }
        }

        private void RefreshAssertionJsonTreePanel()
        {
            if (AssertionJsonTreeView == null
                || AssertionTreeValuePreviewTextBox == null
                || AssertionTreeSelectedPathTextBlock == null)
            {
                return;
            }

            // Always use PreviewVariables as the fixed source
            _assertionTreeSource = "PreviewVariables";

            // Clear existing items before rebuilding
            AssertionJsonTreeView.Items.Clear();

            // Use VariablesPreview directly to match exactly what's shown in the preview Variables tab
            var sourceValue = VariablesPreview;
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                AssertionTreeValuePreviewTextBox.Text = "Source has no data yet. Run or preview component first.";
                return;
            }

            if (!TryParseJsonRoot(sourceValue, out var rootElement))
            {
                AssertionTreeValuePreviewTextBox.Text = "Selected source is not valid JSON.";
                return;
            }

            var rootItem = CreateAssertionTreePanelItem("$", "$", rootElement, _assertionTreeSource);
            rootItem.IsExpanded = true;
            AssertionJsonTreeView.Items.Add(rootItem);
            rootItem.IsSelected = true;
        }

        private static TreeViewItem CreateAssertionTreePanelItem(string label, string path, JsonElement element, string source)
        {
            var item = new TreeViewItem
            {
                Header = BuildJsonTreeLabel(label, element),
                Tag = new AssertionTreeNodeTag
                {
                    Source = source,
                    Path = path,
                    Expected = BuildExpectedValueFromElement(element),
                    IsLeaf = true
                },
                DataContext = element
            };

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = path + "." + property.Name;
                    item.Items.Add(CreateAssertionTreePanelItem(property.Name, childPath, property.Value, source));
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var child in element.EnumerateArray())
                {
                    var childLabel = "[" + index + "]";
                    var childPath = path + childLabel;
                    item.Items.Add(CreateAssertionTreePanelItem(childLabel, childPath, child, source));
                    index++;
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var stringValue = element.GetString() ?? string.Empty;
                if (TryParseJsonString(stringValue, out var embeddedJson))
                {
                    if (embeddedJson.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in embeddedJson.EnumerateObject())
                        {
                            var childPath = path + "." + property.Name;
                            item.Items.Add(CreateAssertionTreePanelItem(property.Name, childPath, property.Value, source));
                        }
                    }
                    else if (embeddedJson.ValueKind == JsonValueKind.Array)
                    {
                        var index = 0;
                        foreach (var child in embeddedJson.EnumerateArray())
                        {
                            var childLabel = "[" + index + "]";
                            var childPath = path + childLabel;
                            item.Items.Add(CreateAssertionTreePanelItem(childLabel, childPath, child, source));
                            index++;
                        }
                    }
                }
            }

            if (item.Tag is AssertionTreeNodeTag tag)
            {
                tag.IsLeaf = item.Items.Count == 0;
            }

            return item;
        }

        private void ExploreAssertionExpectedJsonButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || sender is not Button button || button.DataContext is not AssertionRule assertion)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(assertion.Source))
            {
                MessageBox.Show("Select a source first.", "Assertion JSON Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sourceValue = ResolvePreviewSourceValue(assertion.Source);
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                MessageBox.Show("Selected source is empty. Run or preview the component first.", "Assertion JSON Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!TryParseJsonRoot(sourceValue, out var rootElement))
            {
                MessageBox.Show("Selected source is not a valid JSON document.", "Assertion JSON Tree", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selection = ShowJsonSelectionPicker(rootElement, assertion.JsonPath, "Assertion JSON Tree", "Use Node");
            if (!selection.HasValue)
            {
                return;
            }

            assertion.JsonPath = selection.Value.Path;
            assertion.Expected = selection.Value.ExpectedValue;
            RefreshJsonPreview();
        }

        private void ScriptCodeTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Script", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var editor = new ScriptEditorWindow("Script Component Editor", ScriptLanguage, ScriptCode)
            {
                Owner = this
            };

            if (editor.ShowDialog() == true)
            {
                ScriptLanguage = string.IsNullOrWhiteSpace(editor.ScriptLanguage) ? "CSharp" : editor.ScriptLanguage;
                ScriptCode = editor.ScriptText;
            }

            e.Handled = true;
        }

        private void SqlQueryTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Sql", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            const string sqlInstructions =
                "SQL query editor. This editor supports multi-line SQL text.\n\n"
                + "Tips:\n"
                + "- Use ${varName} placeholders to inject runtime/project variables.\n"
                + "- Example: SELECT * FROM users WHERE env = '${env}'\n"
                + "- Use Save to apply changes to the SQL component query.";

            var editor = new ScriptEditorWindow(
                title: "SQL Query Editor",
                language: "Sql",
                script: SqlQuery,
                openScriptTabOnLoad: true,
                allowExecutionActions: false,
                lockLanguage: true,
                instructionsOverride: sqlInstructions)
            {
                Owner = this
            };

            if (editor.ShowDialog() == true)
            {
                SqlQuery = editor.ScriptText;
            }

            e.Handled = true;
        }

        private void AssertionExpectedTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not AssertionRule assertion)
            {
                return;
            }

            if (string.Equals(assertion.Condition, "Script", StringComparison.OrdinalIgnoreCase))
            {
                var (language, scriptBody) = ParseAssertionScript(assertion.Expected);
                var scriptEditor = new ScriptEditorWindow("Assertion Script Editor", language, scriptBody, openScriptTabOnLoad: true)
                {
                    Owner = this
                };

                if (scriptEditor.ShowDialog() == true)
                {
                    assertion.Expected = BuildAssertionScript(scriptEditor.ScriptLanguage, scriptEditor.ScriptText);
                    RefreshJsonPreview();
                }

                e.Handled = true;
                return;
            }

            var editedValue = ShowExpectedValueEditor(assertion.Expected);
            if (editedValue != null)
            {
                assertion.Expected = editedValue;
                RefreshJsonPreview();
            }

            e.Handled = true;
        }

        private string? ShowExpectedValueEditor(string currentValue)
        {
            var dialog = new Window
            {
                Title = "Assertion Expected Editor",
                Width = 800,
                Height = 460,
                MinWidth = 620,
                MinHeight = 320,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize
            };

            var root = new Grid
            {
                Margin = new Thickness(12)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var editor = new TextBox
            {
                Text = currentValue ?? string.Empty,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };
            Grid.SetRow(editor, 0);
            root.Children.Add(editor);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 90,
                IsCancel = true
            };

            var saveButton = new Button
            {
                Content = "Save",
                Width = 90,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = true
            };
            saveButton.Click += (_, _) => dialog.DialogResult = true;

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(saveButton);

            Grid.SetRow(buttons, 1);
            root.Children.Add(buttons);

            dialog.Content = root;
            editor.Focus();

            return dialog.ShowDialog() == true ? editor.Text : null;
        }

        private static (string Language, string Script) ParseAssertionScript(string input)
        {
            var script = input ?? string.Empty;
            var normalized = script.Replace("\r\n", "\n");
            var lines = normalized.Split('\n');
            if (lines.Length == 0)
            {
                return ("CSharp", script);
            }

            var firstLine = lines[0].Trim();
            if (!firstLine.StartsWith("//lang", StringComparison.OrdinalIgnoreCase))
            {
                return ("CSharp", script);
            }

            var parts = firstLine.Split(':', 2, StringSplitOptions.TrimEntries);
            var language = parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1])
                ? parts[1]
                : "CSharp";
            var remainingScript = string.Join("\n", lines.Skip(1));
            return (language, remainingScript);
        }

        private static string BuildAssertionScript(string language, string script)
        {
            var normalizedLanguage = string.IsNullOrWhiteSpace(language) ? "CSharp" : language.Trim();
            var normalizedScript = script ?? string.Empty;
            if (string.Equals(normalizedLanguage, "CSharp", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedScript;
            }

            return $"//lang:{normalizedLanguage}\n{normalizedScript}";
        }

        private static bool TryParseJsonRoot(string json, out JsonElement rootElement)
        {
            rootElement = default;
            try
            {
                using var doc = JsonDocument.Parse(json);
                rootElement = doc.RootElement.Clone();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string? ShowJsonPathPicker(JsonElement rootElement, string currentPath)
        {
            var selection = ShowJsonSelectionPicker(rootElement, currentPath, "JSON Path Explorer", "Use Path");
            return selection?.Path;
        }

        private (string Path, string ExpectedValue)? ShowJsonSelectionPicker(
            JsonElement rootElement,
            string currentPath,
            string title,
            string confirmButtonText)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 900,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 700,
                MinHeight = 350
            };

            var root = new Grid
            {
                Margin = new Thickness(12)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = "Select JSON path:",
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tree = new TreeView
            {
                Margin = new Thickness(0, 0, 0, 10)
            };

            var rootItem = CreateJsonTreeItem("$", "$", rootElement);
            rootItem.IsExpanded = true;
            tree.Items.Add(rootItem);

            var valuePreview = new TextBox
            {
                Margin = new Thickness(10, 0, 0, 10),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                IsReadOnly = true,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var selectedPathBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas")
            };

            string selectedExpectedValue = string.Empty;

            tree.SelectedItemChanged += (_, _) =>
            {
                if (tree.SelectedItem is not TreeViewItem selectedItem)
                {
                    return;
                }

                selectedPathBox.Text = selectedItem.Tag as string ?? string.Empty;
                if (selectedItem.DataContext is JsonElement selectedElement)
                {
                    valuePreview.Text = GetJsonElementPreview(selectedElement);
                    selectedExpectedValue = BuildExpectedValueFromElement(selectedElement);
                }
                else
                {
                    valuePreview.Text = string.Empty;
                    selectedExpectedValue = string.Empty;
                }
            };

            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                var matched = FindTreeItemByPath(rootItem, currentPath);
                if (matched != null)
                {
                    ExpandParents(matched);
                    matched.IsSelected = true;
                }
            }
            else
            {
                rootItem.IsSelected = true;
            }

            Grid.SetColumn(tree, 0);
            Grid.SetColumn(valuePreview, 1);
            contentGrid.Children.Add(tree);
            contentGrid.Children.Add(valuePreview);

            Grid.SetRow(contentGrid, 1);
            root.Children.Add(contentGrid);

            Grid.SetRow(selectedPathBox, 2);
            root.Children.Add(selectedPathBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancelButton.Click += (_, _) => dialog.DialogResult = false;

            var okButton = new Button
            {
                Content = confirmButtonText,
                Width = 90,
                IsDefault = true
            };
            okButton.Click += (_, _) => dialog.DialogResult = !string.IsNullOrWhiteSpace(selectedPathBox.Text);

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(okButton);
            Grid.SetRow(buttonPanel, 3);
            root.Children.Add(buttonPanel);

            dialog.Content = root;

            var accepted = dialog.ShowDialog();
            if (accepted != true || string.IsNullOrWhiteSpace(selectedPathBox.Text))
            {
                return null;
            }

            return (selectedPathBox.Text, selectedExpectedValue);
        }

        private static string BuildExpectedValueFromElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.GetRawText(),
                JsonValueKind.Array => element.GetRawText(),
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => GetJsonElementPreview(element)
            };
        }

        private static TreeViewItem CreateJsonTreeItem(string label, string path, JsonElement element)
        {
            var item = new TreeViewItem
            {
                Header = BuildJsonTreeLabel(label, element),
                Tag = path,
                DataContext = element
            };

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = path + "." + property.Name;
                    item.Items.Add(CreateJsonTreeItem(property.Name, childPath, property.Value));
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var child in element.EnumerateArray())
                {
                    var childLabel = "[" + index + "]";
                    var childPath = path + childLabel;
                    item.Items.Add(CreateJsonTreeItem(childLabel, childPath, child));
                    index++;
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var stringValue = element.GetString() ?? string.Empty;
                if (TryParseJsonString(stringValue, out var embeddedJson))
                {
                    if (embeddedJson.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in embeddedJson.EnumerateObject())
                        {
                            var childPath = path + "." + property.Name;
                            item.Items.Add(CreateJsonTreeItem(property.Name, childPath, property.Value));
                        }
                    }
                    else if (embeddedJson.ValueKind == JsonValueKind.Array)
                    {
                        var index = 0;
                        foreach (var child in embeddedJson.EnumerateArray())
                        {
                            var childLabel = "[" + index + "]";
                            var childPath = path + childLabel;
                            item.Items.Add(CreateJsonTreeItem(childLabel, childPath, child));
                            index++;
                        }
                    }
                }
            }

            return item;
        }

        private static string BuildJsonTreeLabel(string label, JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => label + " (object)",
                JsonValueKind.Array => label + " (array)",
                JsonValueKind.String => label + " = \"" + (element.GetString() ?? string.Empty) + "\"",
                JsonValueKind.Number => label + " = " + element.GetRawText(),
                JsonValueKind.True => label + " = true",
                JsonValueKind.False => label + " = false",
                JsonValueKind.Null => label + " = null",
                _ => label
            };
        }

        private static string GetJsonElementPreview(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => element.GetRawText()
            };
        }

        private static TreeViewItem? FindTreeItemByPath(TreeViewItem current, string path)
        {
            if (string.Equals(current.Tag as string, path, StringComparison.Ordinal))
            {
                return current;
            }

            foreach (var child in current.Items)
            {
                if (child is TreeViewItem childItem)
                {
                    var found = FindTreeItemByPath(childItem, path);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static void ExpandParents(TreeViewItem item)
        {
            var current = item;
            while (current != null)
            {
                current.IsExpanded = true;
                current = current.Parent as TreeViewItem;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}




