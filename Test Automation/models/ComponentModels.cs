using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Test_Automation.Models.Editor;

namespace Test_Automation.Models
{
    /// <summary>
    /// Base model for component input/output
    /// </summary>
    public class ComponentData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("componentName")]
        public string ComponentName { get; set; } = string.Empty;

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Http component model
    /// </summary>
    public class HttpData : ComponentData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("method")]
        public string Method { get; set; } = "GET";

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("responseStatus")]
        public int? ResponseStatus { get; set; }

        [JsonPropertyName("responseBody")]
        public string ResponseBody { get; set; } = string.Empty;

        [JsonPropertyName("responseHeaders")]
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// GraphQL component model
    /// </summary>
    public class GraphQlData : ComponentData
    {
        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("variables")]
        public string Variables { get; set; } = "{}";

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("responseStatus")]
        public int? ResponseStatus { get; set; }

        [JsonPropertyName("responseBody")]
        public string ResponseBody { get; set; } = string.Empty;
    }

    /// <summary>
    /// SQL component model
    /// </summary>
    public class SqlData : ComponentData
    {
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = "SqlServer";

        [JsonPropertyName("connectionString")]
        public string ConnectionString { get; set; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("queryResult")]
        public List<Dictionary<string, object>> QueryResult { get; set; } = new List<Dictionary<string, object>>();
    }

    /// <summary>
    /// Dataset component model
    /// </summary>
    public class DatasetData : ComponentData
    {
        [JsonPropertyName("dataSource")]
        public string DataSource { get; set; } = string.Empty;

        [JsonPropertyName("rows")]
        public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();

        [JsonPropertyName("currentRow")]
        public int CurrentRow { get; set; } = 0;
    }

    /// <summary>
    /// Assert component model
    /// </summary>
    public class AssertData : ComponentData
    {
        [JsonPropertyName("expectedValue")]
        public object? ExpectedValue { get; set; }

        [JsonPropertyName("actualValue")]
        public object? ActualValue { get; set; }

        [JsonPropertyName("operator")]
        public string Operator { get; set; } = "equals";

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
/// Variable Extractor component model
/// </summary>
public class VariableExtractorData : ComponentData
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty;

    [JsonPropertyName("variableName")]
    public string VariableName { get; set; } = string.Empty;

    [JsonPropertyName("jsonPath")]
    public string JsonPath { get; set; } = string.Empty;

    [JsonPropertyName("extractedValue")]
    public string ExtractedValue { get; set; } = string.Empty;
}

    /// <summary>
    /// Timer component model
    /// </summary>
    public class TimerData : ComponentData
    {
        [JsonPropertyName("delayMs")]
        public int DelayMs { get; set; }

        [JsonPropertyName("executed")]
        public bool Executed { get; set; }
    }

    /// <summary>
    /// Script component model
    /// </summary>
    public class ScriptData : ComponentData
    {
        [JsonPropertyName("scriptCode")]
        public string ScriptCode { get; set; } = string.Empty;

        [JsonPropertyName("scriptLanguage")]
        public string ScriptLanguage { get; set; } = "csharp";

        [JsonPropertyName("executionResult")]
        public string ExecutionResult { get; set; } = string.Empty;
    }

    /// <summary>
    /// Loop component model
    /// </summary>
    public class LoopData : ComponentData
    {
        [JsonPropertyName("iterations")]
        public int Iterations { get; set; }

        [JsonPropertyName("currentIteration")]
        public int CurrentIteration { get; set; } = 0;

        [JsonPropertyName("childComponents")]
        public List<string> ChildComponents { get; set; } = new List<string>();
    }

    /// <summary>
    /// Foreach component model
    /// </summary>
    public class ForeachData : ComponentData
    {
        [JsonPropertyName("collection")]
        public List<object> Collection { get; set; } = new List<object>();

        [JsonPropertyName("currentIndex")]
        public int CurrentIndex { get; set; } = 0;

        [JsonPropertyName("currentItem")]
        public object? CurrentItem { get; set; }

        [JsonPropertyName("outputVariable")]
        public string OutputVariable { get; set; } = string.Empty;

        [JsonPropertyName("childComponents")]
        public List<string> ChildComponents { get; set; } = new List<string>();
    }

    /// <summary>
    /// If component model
    /// </summary>
    public class IfData : ComponentData
    {
        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("conditionMet")]
        public bool ConditionMet { get; set; }

        [JsonPropertyName("trueComponents")]
        public List<string> TrueComponents { get; set; } = new List<string>();

        [JsonPropertyName("falseComponents")]
        public List<string> FalseComponents { get; set; } = new List<string>();
    }

    /// <summary>
    /// Config component model
    /// </summary>
    public class ConfigData : ComponentData
    {
        [JsonPropertyName("configurations")]
        public Dictionary<string, object> Configurations { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// TestPlan component model
    /// </summary>
    public class TestPlanData : ComponentData
    {
        [JsonPropertyName("testPlanName")]
        public string TestPlanName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("components")]
        public List<string> Components { get; set; } = new List<string>();

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public DateTime EndTime { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending";
    }

    /// <summary>
    /// Threads component model
    /// </summary>
    public class ThreadsData : ComponentData
    {
        [JsonPropertyName("threadCount")]
        public int ThreadCount { get; set; } = 1;

        [JsonPropertyName("rampUpTime")]
        public int RampUpTime { get; set; } = 0;

        [JsonPropertyName("childComponents")]
        public List<string> ChildComponents { get; set; } = new List<string>();
    }

    /// <summary>
    /// Random Generator component model
    /// </summary>
    public class RandomGeneratorData : ComponentData
    {
        [JsonPropertyName("generatedValue")]
        public string GeneratedValue { get; set; } = string.Empty;

        [JsonPropertyName("generatedNumber")]
        public double GeneratedNumber { get; set; }

        [JsonPropertyName("generatedId")]
        public string GeneratedId { get; set; } = string.Empty;

        [JsonPropertyName("outputType")]
        public string OutputType { get; set; } = string.Empty;
    }

    /// <summary>
    /// While component model
    /// </summary>
    public class WhileData : ComponentData
    {
        [JsonPropertyName("conditionRows")]
        public List<ConditionRow> ConditionRows { get; set; } = new List<ConditionRow>();

        [JsonPropertyName("maxIterations")]
        public int MaxIterations { get; set; } = 1000;

        [JsonPropertyName("timeoutMs")]
        public int TimeoutMs { get; set; } = 0;

        [JsonPropertyName("evaluationMode")]
        public string EvaluationMode { get; set; } = "While";

        [JsonPropertyName("childComponents")]
        public List<string> ChildComponents { get; set; } = new List<string>();
    }

    /// <summary>
    /// File component model
    /// </summary>
    public class FileData : ComponentData
    {
        [JsonPropertyName("operation")]
        public string Operation { get; set; } = string.Empty;

        [JsonPropertyName("sourcePath")]
        public string SourcePath { get; set; } = string.Empty;

        [JsonPropertyName("destinationPath")]
        public string DestinationPath { get; set; } = string.Empty;

        [JsonPropertyName("destinationFolder")]
        public string DestinationFolder { get; set; } = string.Empty;

        [JsonPropertyName("destinationFileName")]
        public string DestinationFileName { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("encoding")]
        public string Encoding { get; set; } = "UTF-8";

        [JsonPropertyName("overwrite")]
        public bool Overwrite { get; set; } = false;

        [JsonPropertyName("append")]
        public bool Append { get; set; } = false;

        [JsonPropertyName("fileFilter")]
        public string FileFilter { get; set; } = "*.*";

        [JsonPropertyName("outputVariable")]
        public string OutputVariable { get; set; } = string.Empty;

        [JsonPropertyName("readMode")]
        public string ReadMode { get; set; } = "All"; // All or Selected

        [JsonPropertyName("selectedFilePaths")]
        public List<string> SelectedFilePaths { get; set; } = new List<string>();

        [JsonPropertyName("recursive")]
        public bool Recursive { get; set; } = false;

        [JsonPropertyName("includeMetadata")]
        public bool IncludeMetadata { get; set; } = false;

        [JsonPropertyName("result")]
        public string Result { get; set; } = string.Empty;

        [JsonPropertyName("files")]
        public List<string> Files { get; set; } = new List<string>();

        [JsonPropertyName("metadata")]
        public List<Dictionary<string, object>> Metadata { get; set; } = new List<Dictionary<string, object>>();

        [JsonPropertyName("fileContents")]
        public Dictionary<string, string> FileContents { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("success")]
        public bool Success { get; set; } = false;

        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Excel component model
    /// </summary>
    public class ExcelData : ComponentData
    {
        [JsonPropertyName("operation")]
        public string Operation { get; set; } = string.Empty;

        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = string.Empty;

        [JsonPropertyName("fileMode")]
        public string FileMode { get; set; } = "Existing"; // "New" or "Existing"

        [JsonPropertyName("sheetName")]
        public string SheetName { get; set; } = string.Empty;

        [JsonPropertyName("column")]
        public string Column { get; set; } = string.Empty;

        [JsonPropertyName("row")]
        public int Row { get; set; } = 1;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("values")]
        public string Values { get; set; } = string.Empty; // JSON array for range operations

        [JsonPropertyName("deleteStartColumn")]
        public string DeleteStartColumn { get; set; } = string.Empty;

        [JsonPropertyName("deleteStartRow")]
        public int DeleteStartRow { get; set; } = 1;

        [JsonPropertyName("deleteEndColumn")]
        public string DeleteEndColumn { get; set; } = string.Empty;

        [JsonPropertyName("deleteEndRow")]
        public int DeleteEndRow { get; set; } = 1;

        [JsonPropertyName("result")]
        public string Result { get; set; } = string.Empty;

        [JsonPropertyName("success")]
        public bool Success { get; set; } = false;

        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;

        [JsonPropertyName("sheetNames")]
        public List<string> SheetNames { get; set; } = new List<string>();
    }
}
