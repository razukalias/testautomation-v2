# Variables Preview Scoping Issue - Analysis and Solution

## Problem Description

When switching between TestPlan A → Threads → Script node → Variables preview tab, you can see the project data into the script. But when switching between TestPlan B and child script, you can see the exact scoped data.

## Root Cause Analysis

Looking at the [`UpdateProjectVariablesPreview()`](Test Automation/MainWindow.xaml.cs:2756) method:

```csharp
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

    // Collect all TestPlan variables for hierarchical structure
    var testPlanNodes = projectNode.Children
        .Where(node => string.Equals(node.Type, "TestPlan", StringComparison.OrdinalIgnoreCase))
        .ToList();

    var allTestPlanVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    foreach (var testPlan in testPlanNodes)
    {
        var tpVars = BuildDictionaryWithOverwrite(testPlan.Variables)
            .ToDictionary(entry => entry.Key, entry => (object)entry.Value, StringComparer.OrdinalIgnoreCase);
        if (tpVars.Count > 0)
        {
            allTestPlanVariables[testPlan.Name] = tpVars;
        }
    }

    var context = _lastExecutionContext ?? new Test_Automation.Models.ExecutionContext();
    foreach (var entry in projectVariables)
    {
        context.SetVariable(entry.Key, entry.Value);
    }

    _lastExecutionContext = context;

    // Use hierarchical structure
    VariablesPreview = JsonSerializer.Serialize(new
    {
        projectVariables = projectVariables,
        testPlans = allTestPlanVariables
    }, PrettyJsonOptions);
}
```

### The Problem

This method shows **ALL TestPlan variables** from **ALL TestPlans**, not just the ones in scope for the selected component. This is why:

1. **When viewing TestPlan A → Threads → Script:**
   - You see ALL variables: Project (env) + TestPlan A (A) + TestPlan B (B)
   - JSON structure: `{ "projectVariables": { "env": "dev" }, "testPlans": { "A": { "A": "a" }, "B": { "B": "b" } } }`

2. **When viewing TestPlan B → Threads → Script:**
   - You also see ALL variables: Project (env) + TestPlan A (A) + TestPlan B (B)
   - JSON structure: `{ "projectVariables": { "env": "dev" }, "testPlans": { "A": { "A": "a" }, "B": { "B": "b" } } }`

### Why You See "Exact Scoped Data"

The user mentioned they see "exact scoped data" when switching between TestPlan B and child script. This might be because:

1. **The Variables preview tab is showing a different view** - Maybe there's a separate view that shows only scoped variables
2. **The assertion source dropdown is showing scoped variables** - The `RebuildAssertionSourceOptions()` method correctly shows only scoped variables
3. **There's a different code path** that updates the Variables preview based on selected component

## Solution

To fix this issue, we need to modify the `UpdateProjectVariablesPreview()` method to show only the variables that are in scope for the selected component.

### Solution 1: Show Only Scoped Variables

Modify the method to find the TestPlan ancestor and show only variables from Project + that TestPlan:

```csharp
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

    // Collect only the TestPlan variables that are in scope
    var scopedTestPlanVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    if (testPlanNode != null)
    {
        var tpVars = BuildDictionaryWithOverwrite(testPlanNode.Variables)
            .ToDictionary(entry => entry.Key, entry => (object)entry.Value, StringComparer.OrdinalIgnoreCase);
        if (tpVars.Count > 0)
        {
            scopedTestPlanVariables[testPlanNode.Name] = tpVars;
        }
    }

    var context = _lastExecutionContext ?? new Test_Automation.Models.ExecutionContext();
    foreach (var entry in projectVariables)
    {
        context.SetVariable(entry.Key, entry.Value);
    }

    _lastExecutionContext = context;

    // Show only scoped variables
    VariablesPreview = JsonSerializer.Serialize(new
    {
        projectVariables = projectVariables,
        testPlans = scopedTestPlanVariables
    }, PrettyJsonOptions);
}
```

### Solution 2: Show All Variables with Scope Indicator

Keep the current behavior but add a scope indicator to show which variables are in scope:

```csharp
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

    // Collect all TestPlan variables
    var testPlanNodes = projectNode.Children
        .Where(node => string.Equals(node.Type, "TestPlan", StringComparison.OrdinalIgnoreCase))
        .ToList();

    var allTestPlanVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    var scopedTestPlanName = testPlanNode?.Name;

    foreach (var testPlan in testPlanNodes)
    {
        var tpVars = BuildDictionaryWithOverwrite(testPlan.Variables)
            .ToDictionary(entry => entry.Key, entry => (object)entry.Value, StringComparer.OrdinalIgnoreCase);
        if (tpVars.Count > 0)
        {
            // Add scope indicator
            var isScoped = string.Equals(testPlan.Name, scopedTestPlanName, StringComparison.OrdinalIgnoreCase);
            allTestPlanVariables[testPlan.Name] = new
            {
                variables = tpVars,
                isScoped = isScoped
            };
        }
    }

    var context = _lastExecutionContext ?? new Test_Automation.Models.ExecutionContext();
    foreach (var entry in projectVariables)
    {
        context.SetVariable(entry.Key, entry.Value);
    }

    _lastExecutionContext = context;

    // Show all variables with scope indicator
    VariablesPreview = JsonSerializer.Serialize(new
    {
        projectVariables = projectVariables,
        testPlans = allTestPlanVariables,
        scopedTestPlan = scopedTestPlanName
    }, PrettyJsonOptions);
}
```

### Solution 3: Add a Toggle for Scoped vs All Variables

Add a UI toggle to switch between showing all variables and showing only scoped variables:

```csharp
private bool _showOnlyScopedVariables = false;

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

    // Collect TestPlan variables based on toggle
    var testPlanNodes = projectNode.Children
        .Where(node => string.Equals(node.Type, "TestPlan", StringComparison.OrdinalIgnoreCase))
        .ToList();

    var testPlanVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    if (_showOnlyScopedVariables)
    {
        // Show only the TestPlan that is in scope
        if (testPlanNode != null)
        {
            var tpVars = BuildDictionaryWithOverwrite(testPlanNode.Variables)
                .ToDictionary(entry => entry.Key, entry => (object)entry.Value, StringComparer.OrdinalIgnoreCase);
            if (tpVars.Count > 0)
            {
                testPlanVariables[testPlanNode.Name] = tpVars;
            }
        }
    }
    else
    {
        // Show all TestPlans
        foreach (var testPlan in testPlanNodes)
        {
            var tpVars = BuildDictionaryWithOverwrite(testPlan.Variables)
                .ToDictionary(entry => entry.Key, entry => (object)entry.Value, StringComparer.OrdinalIgnoreCase);
            if (tpVars.Count > 0)
            {
                testPlanVariables[testPlan.Name] = tpVars;
            }
        }
    }

    var context = _lastExecutionContext ?? new Test_Automation.Models.ExecutionContext();
    foreach (var entry in projectVariables)
    {
        context.SetVariable(entry.Key, entry.Value);
    }

    _lastExecutionContext = context;

    // Show variables based on toggle
    VariablesPreview = JsonSerializer.Serialize(new
    {
        projectVariables = projectVariables,
        testPlans = testPlanVariables,
        showOnlyScoped = _showOnlyScopedVariables,
        scopedTestPlan = testPlanNode?.Name
    }, PrettyJsonOptions);
}
```

## Recommendation

I recommend **Solution 1** because:

1. **It's the simplest** - Just filter to show only scoped variables
2. **It matches user expectation** - Users expect to see only variables that are in scope
3. **It's consistent with assertion source** - The assertion source dropdown already shows only scoped variables
4. **It's less confusing** - No need to explain why variables from other TestPlans are shown

## Implementation

To implement Solution 1, you need to:

1. Modify the `UpdateProjectVariablesPreview()` method in [`MainWindow.xaml.cs`](Test Automation/MainWindow.xaml.cs:2756)
2. Find the TestPlan ancestor for the selected component
3. Collect only variables from Project + that TestPlan
4. Update the VariablesPreview JSON to show only scoped variables

## Expected Behavior After Fix

### When viewing TestPlan A → Threads → Script:
```json
{
  "projectVariables": {
    "env": "dev"
  },
  "testPlans": {
    "A": {
      "A": "a"
    }
  }
}
```

### When viewing TestPlan B → Threads → Script:
```json
{
  "projectVariables": {
    "env": "dev"
  },
  "testPlans": {
    "B": {
      "B": "b"
    }
  }
}
```

This matches the behavior of the assertion source dropdown, which shows only scoped variables.
