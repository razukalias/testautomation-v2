# How the Test Automation Project Works

## Executive Summary

This is a **WPF-based Test Automation Framework** that provides a visual interface for creating, managing, and executing test plans. The application allows users to build test workflows using a drag-and-drop component system, execute tests against various APIs and databases, and validate results using assertions.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    User Interface (WPF)                      │
│  - Visual Test Plan Editor                                  │
│  - Tree-based Component Hierarchy                           │
│  - Real-time Preview & Execution                            │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Component System                           │
│  - 14 Component Types (Http, Sql, Dataset, Script, etc.)   │
│  - Each component performs a specific action                │
│  - Components can be nested and organized                   │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Service Layer                              │
│  - ComponentExecutor: Orchestrates execution                │
│  - VariableService: Manages variables                       │
│  - AssertionService: Evaluates assertions                   │
│  - ScriptEngine: Executes C# scripts                        │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Data Models                                │
│  - PlanNode: UI representation                              │
│  - ExecutionContext: Runtime state                          │
│  - ExecutionResult: Component results                       │
│  - ComponentData: Component-specific data                   │
└─────────────────────────────────────────────────────────────┘
```

## Core Workflow

### 1. **Design Phase** (User creates test plan)
```
User opens application
    │
    ▼
Creates new project
    │
    ▼
Adds components to tree:
    - TestPlan (root)
        - Http (login request)
        - VariableExtractor (save token)
        - Http (use token)
        - Assert (validate response)
    │
    ▼
Configures component settings:
    - Method: POST
    - Url: https://api.example.com/login
    - Body: {"username": "${username}", "password": "${password}"}
    │
    ▼
Saves project as JSON file
```

### 2. **Execution Phase** (User runs test plan)
```
User clicks "Run" button
    │
    ▼
System creates ExecutionContext:
    - Generates execution ID
    - Initializes variables
    - Sets start time
    │
    ▼
System builds component tree:
    - Converts PlanNode to Component
    - Sets parent-child relationships
    - Copies settings and extractors
    │
    ▼
System executes components:
    For each component:
        1. Resolve variables in settings
           "${username}" → "john_doe"
        2. Execute component
           - Http: Makes HTTP request
           - Sql: Executes SQL query
           - Script: Runs C# code
        3. Extract variables from result
           ResponseBody → $.token → authToken
        4. Build preview data
           - Request details
           - Response details
           - Variable extraction results
        5. Evaluate assertions
           ResponseStatus == 200 → Passed
        6. Store execution result
    │
    ▼
System updates UI:
    - Shows execution results
    - Displays preview data
    - Updates assertion counts
    - Shows extracted variables
```

### 3. **Validation Phase** (System validates results)
```
For each component:
    │
    ▼
Get assertion rules:
    - Source: ResponseStatus
    - Condition: Equals
    - Expected: 200
    │
    ▼
Get actual value:
    - From component data
    - Apply JSON path if needed
    │
    ▼
Compare values:
    - Actual: 200
    - Expected: 200
    - Result: Passed
    │
    ▼
Update assertion counts:
    - Passed: 1
    - Failed: 0
    │
    ▼
Check assertion mode:
    - Assert: Continue execution
    - Expect: Log and continue
    - Assert and Stop: Stop if failed
```

## Key Components Explained

### **ComponentExecutor** (The Brain)
- Orchestrates entire execution process
- Manages component tree traversal
- Coordinates services (variables, assertions, preview)
- Handles threading and parallel execution
- Stores execution results

### **VariableService** (Data Flow Manager)
- Resolves variables in settings using `${variableName}` syntax
- Extracts values from component data using JSON paths
- Manages variable scope across components
- Supports multiple sources (Response, Request, Variables, etc.)

### **AssertionService** (Validator)
- Evaluates assertions against component data
- Supports 14 different conditions (Equals, Contains, Regex, etc.)
- Handles three assertion modes (Assert, Expect, Assert and Stop)
- Tracks assertion results and counts

### **ScriptEngine** (Code Executor)
- Compiles and executes C# scripts dynamically
- Provides access to execution context variables
- Handles compilation and runtime errors
- Supports script validation

### **PreviewBuilder** (UI Data Provider)
- Builds preview data for each component
- Includes request/response details
- Shows variable extraction results
- Displays assertion results

## Data Flow Example

### HTTP Login Test with Token Extraction

```
1. User configures Http component:
   Method: POST
   Url: https://api.example.com/login
   Body: {"username": "${username}", "password": "${password}"}

2. VariableService resolves settings:
   - Finds ${username} pattern
   - Looks up "username" in ExecutionContext
   - Replaces with "john_doe"
   - Same for ${password}
   - Result: {"username": "john_doe", "password": "secret123"}

3. Http component executes:
   - Makes POST request to URL
   - Sends body with credentials
   - Receives response: {"token": "abc123", "userId": "456"}

4. VariableService applies extractors:
   - Extractor: Source=ResponseBody, JsonPath=$.token, VariableName=authToken
   - Gets response body
   - Applies JSON path: $.token → "abc123"
   - Stores in ExecutionContext: authToken = "abc123"

5. PreviewBuilder creates preview:
   - Request: POST https://api.example.com/login
   - Request Body: {"username": "john_doe", "password": "secret123"}
   - Response Status: 200
   - Response Body: {"token": "abc123", "userId": "456"}
   - Variable Extraction: authToken = "abc123"

6. AssertionService evaluates assertions:
   - Assertion: ResponseStatus == 200
   - Actual: 200
   - Expected: 200
   - Result: Passed

7. UI updates:
   - Shows execution result: Passed
   - Displays preview data
   - Updates assertion count: 1 passed
   - Shows extracted variable: authToken = "abc123"
```

## Component Types and Their Purposes

### **Data Retrieval Components**
- **Http**: Make REST API calls (GET, POST, PUT, DELETE, etc.)
- **GraphQl**: Execute GraphQL queries and mutations
- **Sql**: Run SQL queries against databases (SqlServer, PostgreSQL, MySQL, SQLite)
- **Dataset**: Load test data from files (Excel, CSV, JSON, XML)

### **Logic Control Components**
- **If**: Conditional execution based on conditions
- **Loop**: Repeat child components N times
- **Foreach**: Iterate over collection items
- **Threads**: Execute components in parallel

### **Utility Components**
- **Timer**: Add delays between components
- **VariableExtractor**: Extract values into variables
- **Script**: Execute custom C# code
- **Assert**: Validate results (legacy component)
- **Config**: Manage configuration settings
- **TestPlan**: Root container for test plan

## Variable System

### **Setting Variables**
```csharp
// In component settings
"Url": "https://api.example.com/users/${userId}"

// Variable resolution
${userId} → "12345"
Result: "https://api.example.com/users/12345"
```

### **Extracting Variables**
```csharp
// From HTTP response
Extractor.Source = "ResponseBody"
Extractor.JsonPath = "$.data.token"
Extractor.VariableName = "authToken"

// Result
Response: {"data": {"token": "abc123"}}
Extracted: authToken = "abc123"
```

### **Using Variables**
```csharp
// In next component
"Headers": {"Authorization": "Bearer ${authToken}"}

// Result
Headers: {"Authorization": "Bearer abc123"}
```

## Assertion System

### **Assertion Components**
- **Source**: Where to get actual value (ResponseStatus, ResponseBody, Variable, etc.)
- **JsonPath**: Path to specific value in JSON (e.g., $.data.status)
- **Condition**: How to compare (Equals, Contains, GreaterThan, etc.)
- **Expected**: Expected value
- **Mode**: How to handle failure (Assert, Expect, Assert and Stop)

### **Assertion Modes**
- **Assert**: Fail test if assertion fails, continue execution
- **Expect**: Log failure but continue execution
- **Assert and Stop**: Fail test and stop execution immediately

### **Assertion Conditions**
- **Equals**: Value equals expected
- **NotEquals**: Value does not equal expected
- **Contains**: Value contains expected
- **NotContains**: Value does not contain expected
- **StartsWith**: Value starts with expected
- **EndsWith**: Value ends with expected
- **GreaterThan**: Value is greater than expected
- **GreaterOrEqual**: Value is greater or equal to expected
- **LessThan**: Value is less than expected
- **LessOrEqual**: Value is less or equal to expected
- **IsEmpty**: Value is empty
- **IsNotEmpty**: Value is not empty
- **Regex**: Value matches regex pattern
- **Script**: Custom script evaluation

## Threading Model

### **Sequential Execution** (Default)
```
Component1 → Component2 → Component3 → Component4
```
- Simple and predictable
- One component at a time
- Default execution mode

### **Parallel Execution** (Threads Component)
```
Thread1: Component1 → Component2
Thread2: Component3 → Component4
Thread3: Component5 → Component6
```
- Multiple threads execute simultaneously
- Shared execution context
- Configure thread count and ramp-up time
- Useful for performance testing

## Project File Format

### **JSON Structure**
```json
{
  "version": 1,
  "project": {
    "id": "guid",
    "type": "Project",
    "name": "My Test Plan",
    "enabled": true,
    "settings": {
      "Description": "Test plan description",
      "Environment": "dev"
    },
    "variables": {},
    "extractors": [],
    "assertions": [],
    "children": [
      {
        "id": "guid",
        "type": "Http",
        "name": "Login Request",
        "enabled": true,
        "settings": {
          "Method": "POST",
          "Url": "https://api.example.com/login"
        },
        "extractors": [],
        "assertions": [],
        "children": []
      }
    ]
  }
}
```

## Error Handling

### **Component Execution Errors**
- Caught during component execution
- Stored in ExecutionResult.Error
- Component marked as failed
- Execution continues (unless Assert and Stop)

### **Assertion Failures**
- **Assert mode**: Mark as failed, continue execution
- **Expect mode**: Log failure, continue execution
- **Assert and Stop mode**: Mark as failed, stop execution

### **Script Errors**
- Compilation errors with line/column information
- Runtime errors with stack trace
- Error details stored in ExecutionResult

## Best Practices

### **1. Component Organization**
- Use meaningful component names
- Group related components together
- Keep component hierarchies shallow
- Use TestPlan as root container

### **2. Variable Management**
- Use descriptive variable names
- Extract only necessary values
- Avoid variable name conflicts
- Use variables for dynamic data

### **3. Assertion Strategy**
- Assert critical validations (status codes, required fields)
- Expect non-critical validations (optional fields)
- Use Assert and Stop for critical failures

### **4. Error Handling**
- Handle component execution errors
- Validate extracted variables
- Check assertion results
- Use appropriate assertion modes

### **5. Performance**
- Use parallel execution when appropriate
- Minimize unnecessary components
- Optimize dataset loading
- Use appropriate timeouts

## Common Use Cases

### **1. API Testing**
- Test REST API endpoints
- Validate response status codes
- Check response body content
- Extract and use authentication tokens

### **2. Data-Driven Testing**
- Load test data from CSV/Excel files
- Iterate over data rows
- Execute tests for each data set
- Validate results for each iteration

### **3. Integration Testing**
- Test multiple API endpoints
- Verify data flow between services
- Validate database state
- Check error handling

### **4. Performance Testing**
- Execute tests in parallel
- Measure response times
- Test under load
- Validate scalability

### **5. Regression Testing**
- Save test plans as projects
- Re-run tests after changes
- Validate no regressions
- Automate in CI/CD pipeline

## Summary

This test automation framework provides:

1. **Visual Editor**: Drag-and-drop interface for building test plans
2. **Component System**: 14 component types for different testing needs
3. **Variable System**: Data flow between components using variables
4. **Assertion System**: Validation using multiple conditions and modes
5. **Threading Support**: Parallel execution for performance testing
6. **Project Management**: Save and load test plans as JSON files
7. **Preview System**: Real-time execution preview and results
8. **Error Handling**: Comprehensive error handling and reporting

The framework is designed to be:
- **Flexible**: Support multiple testing scenarios
- **Extensible**: Easy to add new component types
- **User-Friendly**: Visual interface for non-technical users
- **Powerful**: Support complex testing logic
- **Maintainable**: Clear separation of concerns

## Getting Started

1. **Open Application**: Launch the WPF application
2. **Create Project**: File → New Project
3. **Add Components**: Drag components from toolbox
4. **Configure Settings**: Set component properties
5. **Add Assertions**: Define validation rules
6. **Run Test**: Click "Run" button
7. **View Results**: Check preview and assertion results
8. **Save Project**: File → Save Project

## Conclusion

This test automation framework provides a comprehensive solution for creating, managing, and executing test plans. Its component-based architecture allows for flexible test creation, while the service layer handles complex execution logic. The visual editor makes it easy to build and configure tests, and the preview system provides real-time feedback during execution.

The framework supports multiple types of testing including API testing, database testing, data-driven testing, and custom script execution. Its variable management and assertion systems provide powerful validation capabilities, while the threading model enables performance testing scenarios.
