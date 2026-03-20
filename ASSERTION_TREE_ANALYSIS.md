# Assertion Tree Tab Analysis for Your Project Structure

## Your Project Structure

```
Project (root)
├── Variables: { "env": "dev" }
├── TestPlan A
│   ├── Variables: { "A": "a" }
│   └── Threads
│       └── Children: []
└── TestPlan B
    ├── Variables: { "B": "b" }
    ├── Extractors: [ { Source: "PreviewResponse", JsonPath: "$.summary.assertionSummary", VariableName: "B" } ]
    └── Threads
        └── Script
            └── Assertions: [ { Mode: "Assert", Source: "PreviewVariables", JsonPath: "$.A", Condition: "Equals", Expected: "a" } ]
```

## What You'll See in Assertion Tree Tab

When you select the **Script** component under **TestPlan B → Threads → Script** and open the assertion tree tab, you'll see:

### **1. Assertion Source Options Dropdown**

The dropdown will contain these options:

```
┌─────────────────────────────────┐
│ Assertion Source:               │
├─────────────────────────────────┤
│ PreviewVariables                │  ← Currently selected
│ PreviewRequest                  │
│ PreviewResponse                 │
│ PreviewLogs                     │
│ Variable.env                    │  ← From Project (global)
│ Variable.B                      │  ← From TestPlan B (local)
└─────────────────────────────────┘
```

**Why these options?**
- **PreviewVariables, PreviewRequest, PreviewResponse, PreviewLogs**: Base sources always available
- **Variable.env**: From Project scope (global variable)
- **Variable.B**: From TestPlan B scope (local variable)
- **Variable.A is NOT available**: Because TestPlan A is a sibling, not an ancestor of the Script component

### **2. Assertion Configuration**

The assertion will show:

```
┌─────────────────────────────────┐
│ Assertion Configuration:        │
├─────────────────────────────────┤
│ Mode: Assert                    │
│ Source: PreviewVariables        │
│ JsonPath: $.A                   │
│ Condition: Equals               │
│ Expected: a                     │
└─────────────────────────────────┘
```

### **3. PreviewVariables JSON Structure**

When the assertion evaluates, it will look at the **PreviewVariables** source, which contains all variables from the execution context as JSON:

```json
{
  "env": "dev",
  "B": "b"
}
```

**Important Notes:**
- **Variable "A" is NOT in this JSON**: Because "A" is defined in TestPlan A, which is a sibling of TestPlan B, not an ancestor
- **Variable "B" IS in this JSON**: Because "B" is defined in TestPlan B, which is an ancestor of the Script component
- **Variable "env" IS in this JSON**: Because "env" is defined in Project, which is the root ancestor

### **4. Assertion Evaluation Result**

The assertion will **FAIL** because:

```
Assertion: $.A == "a"
Actual Value: null (because "A" doesn't exist in PreviewVariables)
Expected Value: "a"
Result: FAILED
```

**Why it fails:**
- The assertion looks for `$.A` in the PreviewVariables JSON
- PreviewVariables contains: `{ "env": "dev", "B": "b" }`
- There is no "A" key in this JSON
- The JSON path `$.A` returns null
- null does not equal "a"
- Assertion fails

## Complete Assertion Tree Tab View

```
┌─────────────────────────────────────────────────────────────┐
│ Script - Assertions                                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ Assertion Source: [PreviewVariables ▼]                      │
│                                                             │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Assertion #1                                            │ │
│ ├─────────────────────────────────────────────────────────┤ │
│ │ Mode: [Assert ▼]                                        │ │
│ │ Source: [PreviewVariables ▼]                            │ │
│ │ JsonPath: [$.A________________________]                 │ │
│ │ Condition: [Equals ▼]                                   │ │
│ │ Expected: [a_________________________]                  │ │
│ │                                                         │ │
│ │ Status: ✖ Failed                                        │ │
│ │ Message: Expected "a" but got null                      │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                             │
│ Preview Data:                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ {                                                       │ │
│ │   "env": "dev",                                         │ │
│ │   "B": "b"                                              │ │
│ │ }                                                       │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## How to Fix the Assertion

If you want the assertion to pass, you have two options:

### **Option 1: Change the Assertion to Check for "B"**

```json
{
  "Mode": "Assert",
  "Source": "PreviewVariables",
  "JsonPath": "$.B",
  "Condition": "Equals",
  "Expected": "b"
}
```

This would pass because:
- PreviewVariables contains: `{ "env": "dev", "B": "b" }`
- JSON path `$.B` returns "b"
- "b" equals "b"
- Assertion passes

### **Option 2: Change the Assertion to Use Variable.A Source**

```json
{
  "Mode": "Assert",
  "Source": "Variable.A",
  "JsonPath": "",
  "Condition": "Equals",
  "Expected": "a"
}
```

**But this won't work** because:
- Variable.A is not in scope for TestPlan B
- The AssertionSourceOptions dropdown won't show "Variable.A" as an option
- You can only select from variables in the current scope

### **Option 3: Move Script to TestPlan A**

If you want to check for variable "A", move the Script component to TestPlan A:

```
Project
├── TestPlan A
│   ├── Variables: { "A": "a" }
│   └── Threads
│       └── Script
│           └── Assertions: [ { Source: "PreviewVariables", JsonPath: "$.A", ... } ]
└── TestPlan B
    └── ...
```

Then the assertion would pass because:
- PreviewVariables would contain: `{ "env": "dev", "A": "a" }`
- JSON path `$.A` returns "a"
- "a" equals "a"
- Assertion passes

## Summary

When you open the assertion tree tab for **TestPlan B → Threads → Script → Assertions → Assert**, you'll see:

1. **Assertion Source Options**: PreviewVariables, PreviewRequest, PreviewResponse, PreviewLogs, Variable.env, Variable.B
2. **Assertion Configuration**: Source=PreviewVariables, JsonPath=$.A, Condition=Equals, Expected=a
3. **PreviewVariables JSON**: `{ "env": "dev", "B": "b" }` (no "A" variable)
4. **Assertion Result**: FAILED because "A" doesn't exist in PreviewVariables

The assertion fails because variable "A" is defined in TestPlan A (a sibling), not in TestPlan B (the ancestor). Variables are scoped to their TestPlan, so "A" is not accessible from TestPlan B's context.
