# Test Automation Project - Complete Overview

## Project Summary

This is a **WPF-based Test Automation Framework** written in C# that allows users to create, configure, and execute test plans with various components. The application provides a visual editor for building test workflows and supports multiple types of test components including HTTP requests, GraphQL queries, SQL database operations, datasets, scripts, and more.

## Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    WPF Application (MainWindow)              │
│  - Visual Test Plan Editor                                   │
│  - Tree-based Component Hierarchy                            │
│  - Real-time Preview & Execution                             │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Component Layer                            │
│  - Component (Base Class)                                    │
│  - Http, GraphQl, Sql, Dataset, Script, etc.                │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Service Layer                              │
│  - ComponentExecutor (Orchestrates execution)               │
│  - VariableService (Variable resolution & extraction)       │
│  - AssertionService (Assertion evaluation)                  │
│  - ConditionService (Conditional logic)                     │
│  - ScriptEngine (C# script execution)                       │
│  - PreviewBuilder (Builds preview data)                     │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Model Layer                                │
│  - PlanNode (UI representation)                             │
│  - ComponentData (Execution data)                           │
│  - ExecutionContext (Runtime state)                         │
│  - ExecutionResult (Component results)                      │
└─────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. **MainWindow (WPF UI)**
- **File**: [`MainWindow.xaml.cs`](Test Automation/MainWindow.xaml.cs)
- **Purpose**: Main application window providing visual test plan editor
- **Key Features**:
  - Tree-based component hierarchy visualization
  - Drag-and-drop component configuration
  - Real-time execution preview
  - Variable management and inspection
  - Assertion configuration and results
  - Project save/load functionality
  - API catalog management
  - Dataset preview and configuration

### 2. **Component System**

#### Base Component Class
- **File**: [`Component.cs`](Test Automation/componentes/Component.cs)
- **Purpose**: Abstract base class for all test components
- **Properties**:
  - `Id`: Unique identifier
  - `Name`: Component name
  - `Parent`: Parent component reference
  - `Children`: Child components list
  - `Settings`: Key-value configuration pairs
  - `Extractors`: Variable extraction rules
  - `Assertions`: Assertion rules
- **Methods**:
  - `Execute()`: Virtual method for component execution
  - `AddChild()`: Add child component
  - `RemoveChild()`: Remove child component

#### Available Components

| Component | File | Purpose |
|-----------|------|---------|
| **Http** | [`Http.cs`](Test Automation/componentes/Http.cs) | HTTP/REST API requests with authentication support |
| **GraphQl** | [`GraphQl.cs`](Test Automation/componentes/GraphQl.cs) | GraphQL API queries and mutations |
| **Sql** | [`Sql.cs`](Test Automation/componentes/Sql.cs) | SQL database operations (SqlServer, PostgreSQL, MySQL, SQLite) |
| **Dataset** | [`Dataset.cs`](Test Automation/componentes/Dataset.cs) | Load data from Excel, CSV, JSON, XML files |
| **Script** | [`Script.cs`](Test Automation/componentes/Script.cs) | Execute C# scripts dynamically |
| **Timer** | [`Timer.cs`](Test Automation/componentes/Timer.cs) | Add delays between components |
| **Loop** | [`Loop.cs`](Test Automation/componentes/Loop.cs) | Repeat child components N times |
| **Foreach** | [`Foreach.cs`](Test Automation/componentes/Foreach.cs) | Iterate over collection items |
| **If** | [`If.cs`](Test Automation/componentes/If.cs) | Conditional execution based on conditions |
| **Threads** | [`Threads.cs`](Test Automation/componentes/Threads.cs) | Parallel execution with thread control |
| **VariableExtractor** | [`VariableExtractor.cs`](Test Automation/componentes/VariableExtractor.cs) | Extract values into variables |
| **Assert** | [`Assert.cs`](Test Automation/componentes/Assert.cs) | Assertion component (legacy) |
| **Config** | [`Config.cs`](Test Automation/componentes/Config.cs) | Configuration management |
| **TestPlan** | [`TestPlan.cs`](Test Automation/componentes/TestPlan.cs) | Root container for test plan |

### 3. **Service Layer**

#### ComponentExecutor
- **File**: [`ComponentExecutor.cs`](Test Automation/services/ComponentExecutor.cs)
- **Purpose**: Orchestrates component execution
- **Key Responsibilities**:
  - Execute individual components
  - Execute component trees (parent + children)
  - Handle threading and parallel execution
  - Manage execution context
  - Coordinate variable resolution and assertion evaluation
  - Build preview data

#### VariableService
- **File**: [`VariableService.cs`](Test Automation/services/VariableService.cs)
- **Purpose**: Variable management and resolution
- **Key Features**:
  - Resolve variables in settings using `${variableName}` syntax
  - Apply variable extraction rules from component data
  - Support for multiple sources (Response, Request, Variables, etc.)
  - JSON path extraction from complex data structures

#### AssertionService
- **File**: [`AssertionService.cs`](Test Automation/services/AssertionService.cs)
- **Purpose**: Evaluate assertions against component data
- **Supported Conditions**:
  - Equals, NotEquals
  - Contains, NotContains
  - StartsWith, EndsWith
  - GreaterThan, GreaterOrEqual
  - LessThan, LessOrEqual
  - IsEmpty, IsNotEmpty
  - Regex pattern matching
  - Custom script evaluation
- **Assertion Modes**:
  - **Assert**: Fail test if assertion fails
  - **Expect**: Log failure but continue
  - **Assert and Stop**: Fail and stop execution

#### ConditionService
- **File**: [`ConditionService.cs`](Test Automation/services/ConditionService.cs)
- **Purpose**: Evaluate conditional expressions
- **Supported Operators**: `==`, `!=`, `>`, `>=`, `<`, `<=`
- **Features**:
  - Numeric comparison
  - String comparison
  - Variable resolution in conditions

#### ScriptEngine
- **File**: [`ScriptEngine.cs`](Test Automation/services/ScriptEngine.cs)
- **Purpose**: Execute C# scripts dynamically
- **Features**:
  - Roslyn-based C# script compilation
  - Access to execution context variables
  - Error handling with line/column information
  - Script validation

#### PreviewBuilder
- **File**: [`PreviewBuilder.cs`](Test Automation/services/PreviewBuilder.cs)
- **Purpose**: Build preview data for UI display
- **Features**:
  - Component-specific preview generation
  - Variable extraction results
  - Assertion results
  - Execution timing and status

### 4. **Model Layer**

#### PlanNode (UI Model)
- **File**: [`PlanNode.cs`](Test Automation/models/editor/PlanNode.cs)
- **Purpose**: Represents a component in the UI tree
- **Properties**:
  - `Id`, `Type`, `Name`: Component identification
  - `IsEnabled`: Enable/disable component
  - `IsExpanded`: UI expansion state
  - `IsHighlighted`: UI highlight state
  - `ExecutionType`: PreExecute, Normal, PostExecute
  - `Settings`: Configuration collection
  - `Variables`: Variable definitions
  - `Extractors`: Variable extraction rules
  - `Assertions`: Assertion rules
  - `Children`: Child nodes

#### ExecutionContext (Runtime Model)
- **File**: [`ExecutionModels.cs`](Test Automation/models/ExecutionModels.cs)
- **Purpose**: Maintains runtime state during execution
- **Properties**:
  - `ExecutionId`: Unique execution identifier
  - `Variables`: Concurrent dictionary of variables
  - `StartTime`, `EndTime`: Execution timing
  - `Status`: Execution status (running, passed, failed, stopped)
  - `Results`: List of component execution results
  - `StopToken`: Cancellation token for stopping execution
- **Methods**:
  - `SetVariable()`: Set variable value
  - `GetVariable()`: Get variable value
  - `HasVariable()`: Check if variable exists
  - `RequestStop()`: Request execution stop
  - `SaveVariablesForUi()`: Save variables for UI access

#### ExecutionResult
- **File**: [`ExecutionModels.cs`](Test Automation/models/ExecutionModels.cs)
- **Purpose**: Stores result of component execution
- **Properties**:
  - `ComponentId`, `ComponentName`: Component identification
  - `StartTime`, `EndTime`, `DurationMs`: Timing
  - `Status`: Component status (pending, running, passed, failed)
  - `Output`, `Error`: Execution output and errors
  - `Data`: Component-specific data
  - `AssertionResults`: List of assertion results
  - `PreviewData`: Preview data for UI

#### ComponentData Models
- **File**: [`ComponentModels.cs`](Test Automation/models/ComponentModels.cs)
- **Purpose**: Component-specific data structures
- **Types**:
  - `HttpData`: HTTP request/response data
  - `GraphQlData`: GraphQL query/response data
  - `SqlData`: SQL query results
  - `DatasetData`: Dataset rows and metadata
  - `ScriptData`: Script execution results
  - `TimerData`: Timer execution data
  - `LoopData`: Loop iteration data
  - `ForeachData`: Foreach iteration data
  - `IfData`: Conditional execution data
  - `ThreadsData`: Thread execution data
  - `VariableExtractorData`: Variable extraction data
  - `AssertData`: Assertion data
  - `ConfigData`: Configuration data
  - `TestPlanData`: Test plan metadata

### 5. **Project File Models**

#### ProjectFileModel
- **File**: [`ProjectFileModel.cs`](Test Automation/models/project/ProjectFileModel.cs)
- **Purpose**: Root project file structure
- **Properties**:
  - `Version`: Project file version
  - `Project`: Root node file model

#### NodeFileModel
- **File**: [`NodeFileModel.cs`](Test Automation/models/project/NodeFileModel.cs)
- **Purpose**: Serializable node structure
- **Properties**:
  - `Id`, `Type`, `Name`: Node identification
  - `Enabled`: Enable/disable state
  - `Settings`: Configuration dictionary
  - `Variables`: Variable dictionary
  - `Extractors`: Variable extraction rules
  - `Assertions`: Assertion rules
  - `Children`: Child nodes

## Execution Flow

### 1. **Test Plan Execution**

```
User Clicks "Run" Button
         │
         ▼
┌─────────────────────────────────┐
│  Create ExecutionContext        │
│  - Initialize variables         │
│  - Set execution ID             │
│  - Set start time               │
└─────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────┐
│  Build Component Tree           │
│  - Convert PlanNode to Component│
│  - Set parent-child relationships│
│  - Copy settings and extractors │
└─────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────┐
│  ExecuteComponentTree()         │
│  - Execute root component       │
│  - Recursively execute children │
│  - Handle threading if needed   │
└─────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────┐
│  For Each Component:            │
│  1. Resolve variables in        │
│     settings                    │
│  2. Execute component           │
│  3. Apply variable extractors   │
│  4. Build preview data          │
│  5. Evaluate assertions         │
│  6. Store execution result      │
└─────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────┐
│  Update UI                      │
│  - Show execution results       │
│  - Update assertion counts      │
│  - Display preview data         │
│  - Update variable values       │
└─────────────────────────────────┘
```

### 2. **Component Execution Detail**

```
Component.Execute(context)
         │
         ▼
┌─────────────────────────────────┐
│  VariableService.ResolveSettings│
│  - Replace ${var} with values   │
│  - Resolve all settings         │
└─────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────┐
│  Component-Specific Logic       │
│  - Http: Make HTTP request      │
│  - Sql: Execute SQL query       │
│  - Script: Run C# script        │
│  - Dataset: Load data file      │
│  - etc.                         │
└─────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────┐
│  Return ComponentData           │
│  - Component-specific results   │
│  - Response data                │
│  - Execution metadata           │
└─────────────────────────────────┘
```

### 3. **Variable Resolution**

Variables are resolved using the `${variableName}` syntax:

```csharp
// In settings
"Url": "https://api.example.com/users/${userId}"

// Resolution process
1. Find all ${...} patterns
2. Look up variable in ExecutionContext
3. Replace pattern with variable value
4. Return resolved string
```

### 4. **Variable Extraction**

After component execution, variables can be extracted from the result:

```csharp
// Example: Extract user ID from HTTP response
Extractor.Source = "ResponseBody"
Extractor.JsonPath = "$.data.userId"
Extractor.VariableName = "extractedUserId"

// Extraction process
1. Get source value from ComponentData
2. Apply JSON path to extract specific value
3. Store extracted value in ExecutionContext
4. Make available for subsequent components
```

### 5. **Assertion Evaluation**

Assertions are evaluated after component execution:

```csharp
// Example: Assert HTTP status is 200
Assertion.Source = "ResponseStatus"
Assertion.Condition = "Equals"
Assertion.Expected = "200"

// Evaluation process
1. Get actual value from ComponentData
2. Compare with expected value using condition
3. Store result (passed/failed)
4. Update assertion counts
5. Stop execution if "Assert and Stop" mode
```

## Key Features

### 1. **Multiple Component Types**
- HTTP/REST API testing
- GraphQL API testing
- SQL database testing
- Data-driven testing with datasets
- Custom C# script execution
- Conditional logic and loops
- Parallel execution with threads

### 2. **Variable Management**
- Global variables across components
- Variable extraction from responses
- Variable interpolation in settings
- Variable preview and inspection

### 3. **Assertion System**
- Multiple assertion conditions
- Three assertion modes (Assert, Expect, Assert and Stop)
- Assertion results tracking
- Visual assertion status indicators

### 4. **Preview System**
- Real-time execution preview
- Component-specific preview data
- Request/response inspection
- Variable extraction results
- Assertion results

### 5. **Project Management**
- Save/load test plans as JSON files
- Project versioning
- Component hierarchy preservation
- Settings and configuration persistence

### 6. **API Catalog**
- Manage API base URLs
- Store endpoint configurations
- Quick endpoint selection
- Parameter management

### 7. **Dataset Support**
- Excel file loading
- CSV file loading
- JSON file loading
- XML file loading
- Data-driven test execution

### 8. **Script Execution**
- C# script compilation and execution
- Access to execution context
- Script validation
- Error handling with diagnostics

## Authentication Support

### HTTP Authentication Types
- **WindowsIntegrated**: Default Windows authentication
- **Basic**: Username/password authentication
- **Bearer**: Token-based authentication
- **ApiKey**: API key in header or query parameter
- **OAuth2**: OAuth 2.0 token authentication

### SQL Authentication Types
- **WindowsIntegrated**: Windows authentication
- **SqlAuth**: SQL Server authentication
- **PostgresAuth**: PostgreSQL authentication
- **MySqlAuth**: MySQL authentication

## Data Flow Example

### HTTP Request with Variable Extraction and Assertion

```
1. User configures HTTP component:
   - Method: POST
   - Url: https://api.example.com/login
   - Body: {"username": "${username}", "password": "${password}"}
   - Headers: {"Content-Type": "application/json"}

2. VariableService resolves settings:
   - Replace ${username} with actual value
   - Replace ${password} with actual value

3. Http component executes:
   - Makes POST request to URL
   - Sends body and headers
   - Receives response

4. VariableService applies extractors:
   - Extract token from response: $.data.token
   - Store in variable: authToken

5. PreviewBuilder creates preview:
   - Request details
   - Response details
   - Variable extraction results

6. AssertionService evaluates assertions:
   - Check status code is 200
   - Check response contains "success"
   - Store assertion results

7. UI updates:
   - Show execution result
   - Display preview data
   - Update assertion counts
   - Show extracted variables
```

## Threading Model

### Sequential Execution
- Components execute one after another
- Default execution mode
- Simple and predictable

### Parallel Execution
- Use Threads component
- Configure thread count
- Configure ramp-up time
- Each thread executes child components
- Shared execution context

## Error Handling

### Component Execution Errors
- Caught and stored in ExecutionResult
- Marked as failed
- Execution continues (unless Assert and Stop)

### Assertion Failures
- **Assert mode**: Mark as failed, continue execution
- **Expect mode**: Log failure, continue execution
- **Assert and Stop mode**: Mark as failed, stop execution

### Script Errors
- Compilation errors with line/column info
- Runtime errors with stack trace
- Error details stored in ExecutionResult

## State Management

### ExecutionContext State
- Variables: Concurrent dictionary for thread safety
- Results: List of execution results
- Status: Running, passed, failed, stopped
- Timing: Start time, end time

### UI State
- PlanNode hierarchy
- Selected node
- Preview data
- Variable values
- Assertion results

## File Structure

```
Test Automation/
├── App.xaml.cs                    # Application entry point
├── MainWindow.xaml                # Main UI layout
├── MainWindow.xaml.cs             # Main UI logic
├── ScriptEditorWindow.xaml        # Script editor UI
├── ScriptEditorWindow.xaml.cs     # Script editor logic
├── componentes/                   # Component implementations
│   ├── Component.cs              # Base component class
│   ├── Http.cs                   # HTTP component
│   ├── GraphQl.cs                # GraphQL component
│   ├── Sql.cs                    # SQL component
│   ├── Dataset.cs                # Dataset component
│   ├── Script.cs                 # Script component
│   ├── Timer.cs                  # Timer component
│   ├── Loop.cs                   # Loop component
│   ├── Foreach.cs                # Foreach component
│   ├── If.cs                     # If component
│   ├── Threads.cs                # Threads component
│   ├── VariableExtractor.cs      # Variable extractor component
│   ├── Assert.cs                 # Assert component
│   ├── Config.cs                 # Config component
│   └── TestPlan.cs               # TestPlan component
├── services/                      # Service implementations
│   ├── ComponentExecutor.cs      # Execution orchestrator
│   ├── VariableService.cs        # Variable management
│   ├── AssertionService.cs       # Assertion evaluation
│   ├── ConditionService.cs       # Condition evaluation
│   ├── ScriptEngine.cs           # Script execution
│   ├── PreviewBuilder.cs         # Preview data builder
│   ├── IVariableService.cs       # Variable service interface
│   ├── IAssertionService.cs      # Assertion service interface
│   └── IConditionService.cs      # Condition service interface
├── models/                        # Data models
│   ├── ComponentModels.cs        # Component data models
│   ├── ExecutionModels.cs        # Execution models
│   ├── PreviewModels.cs          # Preview data models
│   ├── editor/                   # Editor models
│   │   ├── PlanNode.cs           # UI node model
│   │   ├── NodeSetting.cs        # Node setting model
│   │   ├── AssertionRule.cs      # Assertion rule model
│   │   ├── VariableExtractionRule.cs # Extraction rule model
│   │   └── VariableScopeModel.cs # Variable scope model
│   └── project/                  # Project file models
│       ├── ProjectFileModel.cs   # Project file model
│       ├── NodeFileModel.cs      # Node file model
│       ├── AssertionFileModel.cs # Assertion file model
│       └── VariableExtractionFileModel.cs # Extraction file model
├── factories/                     # Factory classes
│   └── ComponentFactory.cs       # Component factory
└── converters/                    # UI converters
    ├── VariableSettingUsageLabelConverter.cs
    ├── VariableSettingUsageTooltipConverter.cs
    ├── VariableUsageLabelConverter.cs
    └── VariableUsageTooltipConverter.cs

SmokeHarness/
├── Program.cs                     # Smoke test harness
└── SmokeHarness.csproj           # Project file

TestIntegration/
├── Program.cs                     # Integration test harness
└── TestIntegration.csproj        # Project file
```

## Usage Examples

### Example 1: Simple HTTP Test

```csharp
// Create HTTP component
var http = new Http();
http.Settings["Method"] = "GET";
http.Settings["Url"] = "https://api.example.com/users";

// Add assertion
var assertion = new AssertionRule(
    source: "ResponseStatus",
    jsonPath: "",
    condition: "Equals",
    expected: "200",
    mode: "Assert"
);
http.Assertions.Add(assertion);

// Execute
var executor = new ComponentExecutor();
var context = new ExecutionContext();
var result = await executor.ExecuteComponent(http, context);
```

### Example 2: Data-Driven Test

```csharp
// Create dataset component
var dataset = new Dataset();
dataset.Settings["Format"] = "Csv";
dataset.Settings["SourcePath"] = "test-data.csv";

// Create foreach component
var foreach = new Foreach();
foreach.Settings["SourceVariable"] = "datasetRows";
foreach.Settings["OutputVariable"] = "currentRow";

// Create HTTP component as child
var http = new Http();
http.Settings["Method"] = "POST";
http.Settings["Url"] = "https://api.example.com/users";
http.Settings["Body"] = "${currentRow}";

// Add variable extractor
var extractor = new VariableExtractionRule(
    source: "ResponseBody",
    jsonPath: "$.id",
    variableName: "userId"
);
http.Extractors.Add(extractor);

// Build hierarchy
foreach.AddChild(http);
dataset.AddChild(foreach);

// Execute
var executor = new ComponentExecutor();
var context = new ExecutionContext();
var result = await executor.ExecuteComponentTree(dataset, context);
```

### Example 3: Conditional Execution

```csharp
// Create if component
var ifNode = new If();
ifNode.Settings["Condition"] = "${status} == 200";

// Create components for true branch
var successHttp = new Http();
successHttp.Settings["Method"] = "GET";
successHttp.Settings["Url"] = "https://api.example.com/success";

// Create components for false branch
var errorHttp = new Http();
errorHttp.Settings["Method"] = "GET";
errorHttp.Settings["Url"] = "https://api.example.com/error";

// Build hierarchy
ifNode.AddChild(successHttp);
// Note: False branch components would be added differently

// Execute
var executor = new ComponentExecutor();
var context = new ExecutionContext();
context.SetVariable("status", "200");
var result = await executor.ExecuteComponentTree(ifNode, context);
```

## Best Practices

### 1. **Component Organization**
- Group related components together
- Use meaningful component names
- Keep component hierarchies shallow when possible
- Use TestPlan as root container

### 2. **Variable Management**
- Use descriptive variable names
- Extract only necessary values
- Avoid variable name conflicts
- Use variables for dynamic data

### 3. **Assertion Strategy**
- Use appropriate assertion modes
- Assert critical validations
- Expect non-critical validations
- Use Assert and Stop for critical failures

### 4. **Error Handling**
- Handle component execution errors
- Validate extracted variables
- Check assertion results
- Use appropriate assertion modes

### 5. **Performance**
- Use parallel execution when appropriate
- Minimize unnecessary components
- Optimize dataset loading
- Use appropriate timeouts

## Conclusion

This test automation framework provides a comprehensive solution for creating, managing, and executing test plans. Its component-based architecture allows for flexible test creation, while the service layer handles complex execution logic. The visual editor makes it easy to build and configure tests, and the preview system provides real-time feedback during execution.

The framework supports multiple types of testing including API testing, database testing, data-driven testing, and custom script execution. Its variable management and assertion systems provide powerful validation capabilities, while the threading model enables performance testing scenarios.
