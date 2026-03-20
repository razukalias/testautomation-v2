# Test Automation Project - Architecture Diagrams

## System Architecture Overview

```mermaid
graph TB
    subgraph "WPF Application Layer"
        MainWindow[MainWindow.xaml.cs]
        ScriptEditor[ScriptEditorWindow.xaml.cs]
    end

    subgraph "Component Layer"
        Component[Component.cs]
        Http[Http.cs]
        GraphQl[GraphQl.cs]
        Sql[Sql.cs]
        Dataset[Dataset.cs]
        Script[Script.cs]
        Timer[Timer.cs]
        Loop[Loop.cs]
        Foreach[Foreach.cs]
        If[If.cs]
        Threads[Threads.cs]
        VariableExtractor[VariableExtractor.cs]
        Assert[Assert.cs]
        Config[Config.cs]
        TestPlan[TestPlan.cs]
    end

    subgraph "Service Layer"
        ComponentExecutor[ComponentExecutor.cs]
        VariableService[VariableService.cs]
        AssertionService[AssertionService.cs]
        ConditionService[ConditionService.cs]
        ScriptEngine[ScriptEngine.cs]
        PreviewBuilder[PreviewBuilder.cs]
    end

    subgraph "Model Layer"
        PlanNode[PlanNode.cs]
        ExecutionContext[ExecutionContext.cs]
        ExecutionResult[ExecutionResult.cs]
        ComponentData[ComponentModels.cs]
        PreviewModels[PreviewModels.cs]
    end

    subgraph "Project File Models"
        ProjectFileModel[ProjectFileModel.cs]
        NodeFileModel[NodeFileModel.cs]
        AssertionFileModel[AssertionFileModel.cs]
        VariableExtractionFileModel[VariableExtractionFileModel.cs]
    end

    MainWindow --> ComponentExecutor
    MainWindow --> PlanNode
    ComponentExecutor --> Component
    ComponentExecutor --> VariableService
    ComponentExecutor --> AssertionService
    ComponentExecutor --> ConditionService
    ComponentExecutor --> PreviewBuilder
    Component --> ComponentData
    ComponentExecutor --> ExecutionContext
    ComponentExecutor --> ExecutionResult
    PlanNode --> NodeFileModel
    ExecutionContext --> ExecutionResult
```

## Component Hierarchy

```mermaid
graph TD
    TestPlan[TestPlan]
    Threads[Threads]
    Loop[Loop]
    Foreach[Foreach]
    If[If]
    Http[Http]
    GraphQl[GraphQl]
    Sql[Sql]
    Dataset[Dataset]
    Script[Script]
    Timer[Timer]
    VariableExtractor[VariableExtractor]
    Assert[Assert]
    Config[Config]

    TestPlan --> Threads
    TestPlan --> Loop
    TestPlan --> Foreach
    TestPlan --> If
    TestPlan --> Http
    TestPlan --> GraphQl
    TestPlan --> Sql
    TestPlan --> Dataset
    TestPlan --> Script
    TestPlan --> Timer
    TestPlan --> VariableExtractor
    TestPlan --> Assert
    TestPlan --> Config

    Threads --> Http
    Threads --> GraphQl
    Threads --> Sql
    Threads --> Dataset
    Threads --> Script
    Threads --> Timer
    Threads --> VariableExtractor
    Threads --> Assert

    Loop --> Http
    Loop --> GraphQl
    Loop --> Sql
    Loop --> Dataset
    Loop --> Script
    Loop --> Timer
    Loop --> VariableExtractor
    Loop --> Assert

    Foreach --> Http
    Foreach --> GraphQl
    Foreach --> Sql
    Foreach --> Dataset
    Foreach --> Script
    Foreach --> Timer
    Foreach --> VariableExtractor
    Foreach --> Assert

    If --> Http
    If --> GraphQl
    If --> Sql
    If --> Dataset
    If --> Script
    If --> Timer
    If --> VariableExtractor
    If --> Assert
```

## Execution Flow

```mermaid
sequenceDiagram
    participant User
    participant MainWindow
    participant ComponentExecutor
    participant VariableService
    participant Component
    participant AssertionService
    participant PreviewBuilder
    participant ExecutionContext

    User->>MainWindow: Click "Run"
    MainWindow->>ComponentExecutor: ExecuteComponentTree(root, context)
    ComponentExecutor->>ExecutionContext: Create execution context
    ComponentExecutor->>ComponentExecutor: Build component tree

    loop For each component
        ComponentExecutor->>VariableService: ResolveSettings(settings, context)
        VariableService-->>ComponentExecutor: Resolved settings
        ComponentExecutor->>Component: Execute(context)
        Component-->>ComponentExecutor: ComponentData
        ComponentExecutor->>VariableService: ApplyVariableExtractors(component, context, data)
        VariableService->>ExecutionContext: SetVariable(name, value)
        ComponentExecutor->>PreviewBuilder: BuildAndAttachPreviewData(component, result, context)
        PreviewBuilder-->>ComponentExecutor: PreviewData
        ComponentExecutor->>AssertionService: EvaluateAssertions(component, data, context)
        AssertionService-->>ComponentExecutor: AssertionResults
        ComponentExecutor->>ComponentExecutor: Store ExecutionResult
    end

    ComponentExecutor-->>MainWindow: ExecutionSummary
    MainWindow->>User: Display results
```

## Variable Resolution Flow

```mermaid
flowchart TD
    Start([Start]) --> GetSettings[Get Component Settings]
    GetSettings --> FindPattern{Find ${...} pattern?}
    FindPattern -->|No| ReturnSettings[Return Settings]
    FindPattern -->|Yes| ExtractVarName[Extract Variable Name]
    ExtractVarName --> LookupVar{Variable exists?}
    LookupVar -->|No| KeepPattern[Keep ${...} pattern]
    LookupVar -->|Yes| GetVarValue[Get Variable Value]
    GetVarValue --> ReplacePattern[Replace ${...} with value]
    ReplacePattern --> MorePatterns{More patterns?}
    KeepPattern --> MorePatterns
    MorePatterns -->|Yes| FindPattern
    MorePatterns -->|No| ReturnResolved[Return Resolved Settings]
    ReturnSettings --> End([End])
    ReturnResolved --> End
```

## Variable Extraction Flow

```mermaid
flowchart TD
    Start([Start]) --> GetExtractors[Get Component Extractors]
    GetExtractors --> HasExtractors{Has extractors?}
    HasExtractors -->|No| End([End])
    HasExtractors -->|Yes| LoopExtractors[Loop through extractors]

    LoopExtractors --> GetSource[Get Source Value]
    GetSource --> SourceType{Source type?}

    SourceType -->|Variable| GetFromContext[Get from ExecutionContext]
    SourceType -->|PreviewRequest| GetFromPreview[Get from Preview Data]
    SourceType -->|PreviewResponse| GetFromPreview
    SourceType -->|PreviewVariables| GetFromPreview
    SourceType -->|PreviewLogs| GetFromPreview
    SourceType -->|ComponentData| GetFromComponent[Get from ComponentData]

    GetFromContext --> ApplyJsonPath[Apply JSON Path]
    GetFromPreview --> ApplyJsonPath
    GetFromComponent --> ApplyJsonPath

    ApplyJsonPath --> HasValue{Has value?}
    HasValue -->|No| UseRawValue[Use Raw Source Value]
    HasValue -->|Yes| StoreValue[Store Extracted Value]
    UseRawValue --> StoreValue
    StoreValue --> SetVariable[Set Variable in Context]
    SetVariable --> MoreExtractors{More extractors?}
    MoreExtractors -->|Yes| LoopExtractors
    MoreExtractors -->|No| End
```

## Assertion Evaluation Flow

```mermaid
flowchart TD
    Start([Start]) --> GetAssertions[Get Component Assertions]
    GetAssertions --> HasAssertions{Has assertions?}
    HasAssertions -->|No| End([End])
    HasAssertions -->|Yes| LoopAssertions[Loop through assertions]

    LoopAssertions --> GetSource[Get Source Value]
    GetSource --> SourceType{Source type?}

    SourceType -->|Variable| GetFromContext[Get from ExecutionContext]
    SourceType -->|PreviewVariables| GetFromContext
    SourceType -->|ComponentData| GetFromComponent[Get from ComponentData]

    GetFromContext --> ApplyJsonPath[Apply JSON Path]
    GetFromComponent --> ApplyJsonPath

    ApplyJsonPath --> GetActual[Get Actual Value]
    GetActual --> GetExpected[Get Expected Value]
    GetExpected --> CompareValues[Compare Values]

    CompareValues --> Condition{Condition type?}
    Condition -->|Equals| CheckEquals[Check if equal]
    Condition -->|NotEquals| CheckNotEquals[Check if not equal]
    Condition -->|Contains| CheckContains[Check if contains]
    Condition -->|GreaterThan| CheckGreaterThan[Check if greater]
    Condition -->|LessThan| CheckLessThan[Check if less]
    Condition -->|Regex| CheckRegex[Check regex match]
    Condition -->|Script| ExecuteScript[Execute script]

    CheckEquals --> Result{Passed?}
    CheckNotEquals --> Result
    CheckContains --> Result
    CheckGreaterThan --> Result
    CheckLessThan --> Result
    CheckRegex --> Result
    ExecuteScript --> Result

    Result -->|Yes| MarkPassed[Mark as Passed]
    Result -->|No| MarkFailed[Mark as Failed]
    MarkPassed --> CheckMode{Check Mode}
    MarkFailed --> CheckMode

    CheckMode -->|Assert| ContinueExecution[Continue Execution]
    CheckMode -->|Expect| ContinueExecution
    CheckMode -->|Assert and Stop| StopExecution[Stop Execution]

    ContinueExecution --> MoreAssertions{More assertions?}
    StopExecution --> MoreAssertions
    MoreAssertions -->|Yes| LoopAssertions
    MoreAssertions -->|No| End
```

## Component Data Models

```mermaid
classDiagram
    class ComponentData {
        +string Id
        +string ComponentName
        +Dictionary~string, object~ Properties
        +DateTime Timestamp
    }

    class HttpData {
        +string Url
        +string Method
        +Dictionary~string, string~ Headers
        +string Body
        +int? ResponseStatus
        +string ResponseBody
        +Dictionary~string, string~ ResponseHeaders
    }

    class GraphQlData {
        +string Endpoint
        +string Query
        +string Variables
        +Dictionary~string, string~ Headers
        +int? ResponseStatus
        +string ResponseBody
    }

    class SqlData {
        +string Provider
        +string ConnectionString
        +string Query
        +List~Dictionary~string, object~~ QueryResult
    }

    class DatasetData {
        +string DataSource
        +List~Dictionary~string, object~~ Rows
        +int CurrentRow
    }

    class ScriptData {
        +string ScriptCode
        +string ScriptLanguage
        +string ExecutionResult
    }

    class TimerData {
        +int DelayMs
        +bool Executed
    }

    class LoopData {
        +int Iterations
        +int CurrentIteration
        +List~string~ ChildComponents
    }

    class ForeachData {
        +List~object~ Collection
        +int CurrentIndex
        +object? CurrentItem
        +string OutputVariable
        +List~string~ ChildComponents
    }

    class IfData {
        +string Condition
        +bool ConditionMet
        +List~string~ TrueComponents
        +List~string~ FalseComponents
    }

    class ThreadsData {
        +int ThreadCount
        +int RampUpTime
        +List~string~ ChildComponents
    }

    class VariableExtractorData {
        +string Source
        +string Pattern
        +string VariableName
        +string ExtractedValue
    }

    class AssertData {
        +object? ExpectedValue
        +object? ActualValue
        +string Operator
        +bool Passed
        +string ErrorMessage
    }

    class ConfigData {
        +Dictionary~string, object~ Configurations
    }

    class TestPlanData {
        +string TestPlanName
        +string Description
        +List~string~ Components
        +DateTime StartTime
        +DateTime EndTime
        +string Status
    }

    ComponentData <|-- HttpData
    ComponentData <|-- GraphQlData
    ComponentData <|-- SqlData
    ComponentData <|-- DatasetData
    ComponentData <|-- ScriptData
    ComponentData <|-- TimerData
    ComponentData <|-- LoopData
    ComponentData <|-- ForeachData
    ComponentData <|-- IfData
    ComponentData <|-- ThreadsData
    ComponentData <|-- VariableExtractorData
    ComponentData <|-- AssertData
    ComponentData <|-- ConfigData
    ComponentData <|-- TestPlanData
```

## Service Dependencies

```mermaid
graph LR
    ComponentExecutor --> VariableService
    ComponentExecutor --> AssertionService
    ComponentExecutor --> ConditionService
    ComponentExecutor --> PreviewBuilder
    ComponentExecutor --> ScriptEngine

    VariableService --> ExecutionContext
    AssertionService --> ExecutionContext
    ConditionService --> ExecutionContext
    ScriptEngine --> ExecutionContext

    VariableService --> ComponentData
    AssertionService --> ComponentData
    PreviewBuilder --> ComponentData
    PreviewBuilder --> ExecutionResult
```

## Project File Structure

```mermaid
graph TD
    ProjectFile[ProjectFileModel.json]
    ProjectFile --> Version[Version: 1]
    ProjectFile --> Project[Project: NodeFileModel]

    Project --> Id[Id: GUID]
    Project --> Type[Type: "Project"]
    Project --> Name[Name: "My Test Plan"]
    Project --> Enabled[Enabled: true]
    Project --> Settings[Settings: Dictionary]
    Project --> Variables[Variables: Dictionary]
    Project --> Extractors[Extractors: List]
    Project --> Assertions[Assertions: List]
    Project --> Children[Children: List]

    Children --> Child1[Child Node 1]
    Children --> Child2[Child Node 2]
    Children --> Child3[Child Node 3]

    Child1 --> Child1Settings[Settings]
    Child1 --> Child1Extractors[Extractors]
    Child1 --> Child1Assertions[Assertions]
    Child1 --> Child1Children[Children]

    Child2 --> Child2Settings[Settings]
    Child2 --> Child2Extractors[Extractors]
    Child2 --> Child2Assertions[Assertions]
    Child2 --> Child2Children[Children]

    Child3 --> Child3Settings[Settings]
    Child3 --> Child3Extractors[Extractors]
    Child3 --> Child3Assertions[Assertions]
    Child3 --> Child3Children[Children]
```

## Authentication Flow

```mermaid
flowchart TD
    Start([Start]) --> GetAuthType[Get AuthType Setting]
    GetAuthType --> AuthType{AuthType?}

    AuthType -->|WindowsIntegrated| UseDefault[Use Default Auth]
    AuthType -->|Basic| GetCredentials[Get Username/Password]
    AuthType -->|Bearer| GetToken[Get Bearer Token]
    AuthType -->|ApiKey| GetApiKey[Get API Key]
    AuthType -->|OAuth2| GetOAuth[Get OAuth Token]

    GetCredentials --> EncodeBase64[Encode Base64]
    EncodeBase64 --> AddHeader[Add Authorization Header]
    GetToken --> AddHeader
    GetApiKey --> KeyLocation{Key Location?}
    KeyLocation -->|Header| AddHeader
    KeyLocation -->|Query| AddQueryParam[Add Query Parameter]
    GetOAuth --> AddHeader
    UseDefault --> ExecuteRequest[Execute Request]
    AddHeader --> ExecuteRequest
    AddQueryParam --> ExecuteRequest
    ExecuteRequest --> End([End])
```

## Threading Model

```mermaid
graph TD
    TestPlan[TestPlan]
    Threads[Threads Component]
    Thread1[Thread 1]
    Thread2[Thread 2]
    Thread3[Thread 3]
    Component1[Component 1]
    Component2[Component 2]
    Component3[Component 3]

    TestPlan --> Threads
    Threads --> Thread1
    Threads --> Thread2
    Threads --> Thread3

    Thread1 --> Component1
    Thread2 --> Component2
    Thread3 --> Component3

    subgraph "Parallel Execution"
        Thread1
        Thread2
        Thread3
    end

    subgraph "Shared Context"
        ExecutionContext[ExecutionContext]
        Variables[Variables Dictionary]
        Results[Results List]
    end

    Thread1 --> ExecutionContext
    Thread2 --> ExecutionContext
    Thread3 --> ExecutionContext
```

## Error Handling Flow

```mermaid
flowchart TD
    Start([Start]) --> ExecuteComponent[Execute Component]
    ExecuteComponent --> Success{Success?}

    Success -->|Yes| ApplyExtractors[Apply Extractors]
    Success -->|No| CatchError[Catch Exception]

    CatchError --> StoreError[Store Error in Result]
    StoreError --> MarkFailed[Mark as Failed]
    MarkFailed --> CheckAssertions[Check Assertions]

    ApplyExtractors --> BuildPreview[Build Preview]
    BuildPreview --> EvaluateAssertions[Evaluate Assertions]
    EvaluateAssertions --> AssertionPassed{All Passed?}

    AssertionPassed -->|Yes| MarkPassed[Mark as Passed]
    AssertionPassed -->|No| CheckAssertionMode[Check Assertion Mode]

    CheckAssertionMode --> Mode{Mode?}
    Mode -->|Assert| MarkFailed2[Mark as Failed]
    Mode -->|Expect| LogFailure[Log Failure]
    Mode -->|Assert and Stop| StopExecution[Stop Execution]

    MarkPassed --> ContinueExecution[Continue Execution]
    MarkFailed2 --> ContinueExecution
    LogFailure --> ContinueExecution
    StopExecution --> End([End])
    ContinueExecution --> End
```

## Data-Driven Testing Flow

```mermaid
flowchart TD
    Start([Start]) --> LoadDataset[Load Dataset]
    LoadDataset --> DatasetType{Dataset Type?}

    DatasetType -->|Excel| LoadExcel[Load Excel File]
    DatasetType -->|CSV| LoadCSV[Load CSV File]
    DatasetType -->|JSON| LoadJSON[Load JSON File]
    DatasetType -->|XML| LoadXML[Load XML File]

    LoadExcel --> ParseData[Parse Data]
    LoadCSV --> ParseData
    LoadJSON --> ParseData
    LoadXML --> ParseData

    ParseData --> CreateRows[Create Row List]
    CreateRows --> LoopRows[Loop Through Rows]

    LoopRows --> SetCurrentRow[Set Current Row Variable]
    SetCurrentRow --> ExecuteComponents[Execute Child Components]
    ExecuteComponents --> ExtractVariables[Extract Variables]
    ExtractVariables --> EvaluateAssertions[Evaluate Assertions]
    EvaluateAssertions --> MoreRows{More Rows?}

    MoreRows -->|Yes| LoopRows
    MoreRows -->|No| End([End])
```

## Summary

These diagrams illustrate the complete architecture and flow of the Test Automation project:

1. **System Architecture**: Shows the layered architecture with WPF UI, Components, Services, and Models
2. **Component Hierarchy**: Illustrates how components can be nested and organized
3. **Execution Flow**: Demonstrates the sequence of operations during test execution
4. **Variable Resolution**: Shows how variables are resolved in settings
5. **Variable Extraction**: Illustrates how values are extracted from component data
6. **Assertion Evaluation**: Shows the assertion evaluation process
7. **Component Data Models**: Displays the inheritance hierarchy of component data
8. **Service Dependencies**: Shows how services interact with each other
9. **Project File Structure**: Illustrates the JSON file format for saving projects
10. **Authentication Flow**: Shows how different authentication types are handled
11. **Threading Model**: Illustrates parallel execution with threads
12. **Error Handling**: Shows how errors are handled during execution
13. **Data-Driven Testing**: Illustrates the flow of data-driven tests

These diagrams provide a comprehensive visual understanding of how the test automation framework works.
