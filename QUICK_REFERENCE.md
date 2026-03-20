# Test Automation Project - Quick Reference Guide

## What is This Project?

A **WPF-based Test Automation Framework** that allows you to:
- Create visual test plans with drag-and-drop components
- Execute HTTP, GraphQL, SQL, and other API tests
- Use variables and data extraction across components
- Add assertions to validate responses
- Run tests in parallel with threading support
- Save and load test projects as JSON files

## Core Concepts

### 1. **Components** (Building Blocks)
Each component performs a specific action:

| Component | Purpose | Example Use |
|-----------|---------|-------------|
| **Http** | Make REST API calls | Test login endpoint |
| **GraphQl** | Execute GraphQL queries | Fetch user data |
| **Sql** | Run SQL queries | Verify database state |
| **Dataset** | Load test data from files | Data-driven testing |
| **Script** | Execute C# code | Custom logic |
| **Timer** | Add delays | Wait for async operations |
| **Loop** | Repeat N times | Retry logic |
| **Foreach** | Iterate over items | Process list of users |
| **If** | Conditional execution | Branch based on status |
| **Threads** | Parallel execution | Performance testing |
| **VariableExtractor** | Extract values | Save response data |
| **Assert** | Validate results | Check status code |

### 2. **Variables** (Data Flow)
Variables pass data between components:

```csharp
// Set a variable
context.SetVariable("userId", "12345");

// Use a variable in settings
"Url": "https://api.example.com/users/${userId}"

// Extract a variable from response
Extractor.Source = "ResponseBody"
Extractor.JsonPath = "$.data.id"
Extractor.VariableName = "extractedId"
```

### 3. **Assertions** (Validation)
Assertions verify expected results:

```csharp
// Assert HTTP status is 200
Assertion.Source = "ResponseStatus"
Assertion.Condition = "Equals"
Assertion.Expected = "200"
Assertion.Mode = "Assert"  // Fail if wrong
```

**Assertion Modes:**
- **Assert**: Fail test if assertion fails
- **Expect**: Log failure but continue
- **Assert and Stop**: Fail and stop execution

**Assertion Conditions:**
- Equals, NotEquals
- Contains, NotContains
- StartsWith, EndsWith
- GreaterThan, LessThan
- IsEmpty, IsNotEmpty
- Regex, Script

### 4. **Execution Flow**

```
1. User clicks "Run"
2. Create ExecutionContext (holds variables)
3. Build component tree from UI
4. For each component:
   a. Resolve variables in settings
   b. Execute component
   c. Extract variables from result
   d. Build preview data
   e. Evaluate assertions
   f. Store execution result
5. Update UI with results
```

## How to Use

### Creating a Simple HTTP Test

1. **Add Http Component**
   - Method: GET
   - Url: https://api.example.com/users

2. **Add Assertion**
   - Source: ResponseStatus
   - Condition: Equals
   - Expected: 200

3. **Run Test**
   - Click "Run" button
   - View results in preview

### Using Variables

1. **Extract Variable from Response**
   - Add VariableExtractor after Http
   - Source: ResponseBody
   - JsonPath: $.data.token
   - VariableName: authToken

2. **Use Variable in Next Request**
   - Add Http component
   - Headers: {"Authorization": "Bearer ${authToken}"}

### Data-Driven Testing

1. **Add Dataset Component**
   - Format: Csv
   - SourcePath: test-data.csv

2. **Add Foreach Component**
   - SourceVariable: datasetRows
   - OutputVariable: currentRow

3. **Add Http as Child**
   - Body: ${currentRow}

### Conditional Logic

1. **Add If Component**
   - Condition: ${status} == 200

2. **Add Components for True Branch**
   - These execute if condition is true

### Parallel Execution

1. **Add Threads Component**
   - ThreadCount: 5
   - RampUpSeconds: 1

2. **Add Components as Children**
   - These execute in parallel

## Key Files

### Main Application
- [`MainWindow.xaml.cs`](Test Automation/MainWindow.xaml.cs) - Main UI logic
- [`MainWindow.xaml`](Test Automation/MainWindow.xaml) - UI layout

### Components
- [`Component.cs`](Test Automation/componentes/Component.cs) - Base class
- [`Http.cs`](Test Automation/componentes/Http.cs) - HTTP requests
- [`Sql.cs`](Test Automation/componentes/Sql.cs) - SQL queries
- [`Dataset.cs`](Test Automation/componentes/Dataset.cs) - Data loading

### Services
- [`ComponentExecutor.cs`](Test Automation/services/ComponentExecutor.cs) - Execution orchestrator
- [`VariableService.cs`](Test Automation/services/VariableService.cs) - Variable management
- [`AssertionService.cs`](Test Automation/services/AssertionService.cs) - Assertion evaluation

### Models
- [`PlanNode.cs`](Test Automation/models/editor/PlanNode.cs) - UI node model
- [`ExecutionContext.cs`](Test Automation/models/ExecutionModels.cs) - Runtime state
- [`ComponentModels.cs`](Test Automation/models/ComponentModels.cs) - Component data

## Common Patterns

### Pattern 1: API Test with Token Extraction
```
1. Http (Login)
   - POST /login
   - Body: {"username": "${username}", "password": "${password}"}
   - Extract: authToken from $.token

2. Http (Get User)
   - GET /users/me
   - Headers: {"Authorization": "Bearer ${authToken}"}
   - Assert: Status == 200
```

### Pattern 2: Data-Driven Test
```
1. Dataset (Load CSV)
   - Format: Csv
   - SourcePath: users.csv

2. Foreach (Iterate Rows)
   - SourceVariable: datasetRows
   - OutputVariable: currentUser

3. Http (Create User)
   - POST /users
   - Body: ${currentUser}
   - Assert: Status == 201
```

### Pattern 3: Conditional Flow
```
1. Http (Check Status)
   - GET /status
   - Extract: status from $.status

2. If (${status} == "active")
   - Http (Process Active)
     - POST /process
   - Else
   - Http (Handle Inactive)
     - POST /handle
```

### Pattern 4: Retry Logic
```
1. Loop (3 iterations)
   - Http (Attempt Request)
     - GET /data
   - VariableExtractor
     - Extract: success from $.success
   - If (${success} == true)
     - Break loop
```

## Variable Syntax

### In Settings
```csharp
// Simple variable
"Url": "https://api.example.com/users/${userId}"

// Multiple variables
"Body": {"name": "${name}", "email": "${email}"}

// Nested properties
"Url": "https://${host}/api/${version}/users"
```

### In Conditions
```csharp
// Compare variable
"Condition": "${status} == 200"

// Compare with string
"Condition": "${message} == \"success\""

// Numeric comparison
"Condition": "${count} > 10"
```

## Assertion Examples

### HTTP Status Check
```csharp
Source: ResponseStatus
Condition: Equals
Expected: 200
Mode: Assert
```

### Response Body Check
```csharp
Source: ResponseBody
JsonPath: $.data.status
Condition: Equals
Expected: "active"
Mode: Assert
```

### Contains Check
```csharp
Source: ResponseBody
Condition: Contains
Expected: "success"
Mode: Expect
```

### Regex Check
```csharp
Source: ResponseBody
JsonPath: $.data.email
Condition: Regex
Expected: "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$"
Mode: Assert
```

## Authentication Types

### HTTP Authentication
- **WindowsIntegrated**: Default Windows auth
- **Basic**: Username/Password
- **Bearer**: Token-based
- **ApiKey**: API key in header/query
- **OAuth2**: OAuth 2.0 token

### SQL Authentication
- **WindowsIntegrated**: Windows auth
- **SqlAuth**: SQL Server auth
- **PostgresAuth**: PostgreSQL auth
- **MySqlAuth**: MySQL auth

## Dataset Formats

### Excel
```csharp
Format: Excel
SourcePath: data.xlsx
SheetName: Sheet1
```

### CSV
```csharp
Format: Csv
SourcePath: data.csv
CsvDelimiter: ","
CsvHasHeader: true
```

### JSON
```csharp
Format: Json
SourcePath: data.json
JsonArrayPath: $.users
```

### XML
```csharp
Format: Xml
SourcePath: data.xml
XmlRowPath: //user
```

## Execution Types

### PreExecute
- Runs before main execution
- Setup tasks (e.g., create test data)

### Normal
- Main execution
- Default execution type

### PostExecute
- Runs after main execution
- Cleanup tasks (e.g., delete test data)

## Threading

### Sequential (Default)
- Components execute one after another
- Simple and predictable

### Parallel (Threads Component)
- Multiple threads execute simultaneously
- Shared execution context
- Configure thread count and ramp-up time

## Error Handling

### Component Errors
- Caught and stored in ExecutionResult
- Marked as failed
- Execution continues

### Assertion Failures
- **Assert**: Mark as failed, continue
- **Expect**: Log failure, continue
- **Assert and Stop**: Mark as failed, stop execution

## Best Practices

### 1. Component Organization
- Use meaningful names
- Group related components
- Keep hierarchies shallow
- Use TestPlan as root

### 2. Variable Management
- Use descriptive names
- Extract only necessary values
- Avoid name conflicts
- Use for dynamic data

### 3. Assertion Strategy
- Assert critical validations
- Expect non-critical validations
- Use Assert and Stop for critical failures

### 4. Error Handling
- Handle component errors
- Validate extracted variables
- Check assertion results
- Use appropriate modes

### 5. Performance
- Use parallel execution when appropriate
- Minimize unnecessary components
- Optimize dataset loading
- Use appropriate timeouts

## Troubleshooting

### Variable Not Found
- Check variable name spelling
- Ensure variable is set before use
- Verify variable scope

### Assertion Failing
- Check expected value
- Verify JSON path
- Review assertion condition
- Check actual value in preview

### Component Not Executing
- Verify component is enabled
- Check parent component status
- Review execution context
- Check for errors in preview

### Dataset Not Loading
- Verify file path
- Check file format
- Ensure file exists
- Review file permissions

## Quick Start Checklist

- [ ] Create new project
- [ ] Add TestPlan component as root
- [ ] Add Http component for API call
- [ ] Configure method and URL
- [ ] Add assertion for status code
- [ ] Run test and verify results
- [ ] Add variable extraction if needed
- [ ] Save project for reuse

## Summary

This test automation framework provides:
- **Visual editor** for building test plans
- **Multiple component types** for different testing needs
- **Variable system** for data flow between components
- **Assertion system** for validation
- **Threading support** for parallel execution
- **Project management** for saving/loading tests

The framework is designed to be flexible and extensible, allowing you to create complex test scenarios while maintaining a visual, easy-to-understand interface.
