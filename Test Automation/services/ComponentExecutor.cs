using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Test_Automation.Componentes;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using ExecutionContext = Test_Automation.Models.ExecutionContext;
using LogLevel = Test_Automation.Services.LogLevel;

namespace Test_Automation.Services
{
    /// <summary>
    /// Service for executing components and managing execution flow.
    /// </summary>
    public class ComponentExecutor
    {
        private readonly IVariableService _variableService;
        private readonly IAssertionService _assertionService;
        private readonly IConditionService _conditionService;
        private readonly PreviewBuilder _previewBuilder = new PreviewBuilder();

        private static readonly System.Threading.AsyncLocal<int?> CurrentThreadIndex = new System.Threading.AsyncLocal<int?>();
        private static readonly System.Threading.AsyncLocal<string?> CurrentThreadGroupId = new System.Threading.AsyncLocal<string?>();

        public event Action<ExecutionResult>? ComponentStarted;
        public event Action<ExecutionResult>? ComponentCompleted;
        public event Action<TraceEventArgs>? Trace;

        public ComponentExecutor()
            : this(new VariableService(), new AssertionService(), new ConditionService())
        {
        }

        public ComponentExecutor(IVariableService variableService, IAssertionService assertionService, IConditionService conditionService)
        {
            _variableService = variableService ?? throw new ArgumentNullException(nameof(variableService));
            _assertionService = assertionService ?? throw new ArgumentNullException(nameof(assertionService));
            _conditionService = conditionService ?? throw new ArgumentNullException(nameof(conditionService));
        }

        public async Task<ExecutionResult> ExecuteComponent(Component component, ExecutionContext context)
        {
            Logger.Log($"ExecuteComponent START: {component.Name} ({component.GetType().Name})",
                LogLevel.Verbose, componentId: component.Id, componentName: component.Name, executionId: context.ExecutionId);
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThrowIfStopped(context, component.Name);

            var result = new ExecutionResult
            {
                ComponentId = component.Id,
                ComponentName = component.Name,
                Status = "running",
                ThreadIndex = CurrentThreadIndex.Value ?? 0,
                ThreadGroupId = CurrentThreadGroupId.Value ?? string.Empty,
                ExecutionId = context.ExecutionId
            };

            ComponentStarted?.Invoke(result);
            Logger.Log($"About to execute: {component.Name}", LogLevel.Verbose, 
                componentId: component.Id, componentName: component.Name, executionId: context.ExecutionId);
            TraceLog(component, result, $"[ComponentExecutor]Execute start: {component.Name} ({component.GetType().Name})");

            var originalSettings = component.Settings;
            var originalExtractors = component.Extractors;
            var originalAssertions = component.Assertions;

            try
            {
                TraceLog(component, result, $"[ComponentExecutor] Resolving settings, extractors, and assertions...", TraceLevel.Verbose);
                var settingsToResolve = originalSettings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                component.Settings = _variableService.ResolveSettings(settingsToResolve, context);
                component.Extractors = _variableService.ResolveExtractors(originalExtractors ?? new List<VariableExtractionRule>(), context);

                // Resolve assertions locally WITHOUT modifying shared component.Assertions (thread-safe)
                var resolvedAssertions = _assertionService.ResolveAssertions(
                    (originalAssertions ?? new List<AssertionRule>()).Select(a => a.Clone()).ToList(),
                    context);

                TraceLog(component, result, $"[ComponentExecutor] Executing component implementation: {component.GetType().Name}", TraceLevel.Verbose);
                var componentData = await component.Execute(context);
                result.Data = componentData;
                
                // Set Output based on component type
                result.Output = componentData switch
                {
                    LoopData loop => loop.CurrentIteration.ToString(),
                    ForeachData fe => fe.CurrentIndex.ToString(),
                    WhileData whileData => whileData.Properties.TryGetValue("IterationsExecuted", out var iter) ? iter.ToString() : "0",
                    ScriptData script => script.ExecutionResult,
                    HttpData http => http.ResponseBody,
                    GraphQlData gql => gql.ResponseBody,
                    SqlData sql => sql.QueryResult != null ? JsonSerializer.Serialize(sql.QueryResult) : string.Empty,
                    _ => componentData?.ToString() ?? string.Empty
                };

                if (componentData != null)
                {
                    componentData.Properties["startTime"] = result.StartTime;
                    componentData.Properties["threadIndex"] = result.ThreadIndex;
                    componentData.Properties["threadGroupId"] = result.ThreadGroupId;
                }

                // Build and attach preview data BEFORE extraction so extractors can use the visual properties
                TraceLog(component, result, "[PREVIEWComponentExecutor] Building preview data...", TraceLevel.Verbose);
                _previewBuilder.BuildAndAttachPreviewData(component, result, context);

                TraceLog(component, result, $"[ComponentExecutor] Applying {component.Extractors?.Count ?? 0} variable extractors", TraceLevel.Verbose);
                _variableService.ApplyVariableExtractors(component, context, componentData, (msg, level) => TraceLog(component, result, msg, level), result);

                // Re-build preview data after extraction to include extracted values in the UI
                TraceLog(component, result, "[ComponentExecutor] Rebuilding after variable extraction...", TraceLevel.Verbose);
                _previewBuilder.BuildAndAttachPreviewData(component, result, context);

                // Pass resolved assertions directly (thread-safe - doesn't use shared component.Assertions)
                var assertionResults = await _assertionService.EvaluateAssertionsAsync(component, componentData, context, (msg, level) => TraceLog(component, result, msg, level), result, resolvedAssertions);
                result.AssertionResults = assertionResults;
                result.AssertFailedCount = assertionResults.Count(item => !item.Passed && IsAssertMode(item.Mode));
                result.ExpectFailedCount = assertionResults.Count(item => !item.Passed && !IsAssertMode(item.Mode));
                result.AssertPassedCount = assertionResults.Count(item => item.Passed);

                TraceLog(component, result, $"[ComponentExecutor] Assertion evaluation complete. Passed: {result.AssertPassedCount}, Failed: {result.AssertFailedCount}");

                // Update preview data one last time with assertion results
                TraceLog(component, result, "[ComponentExecutor] Final rebuild with assertion results", TraceLevel.Verbose);
                _previewBuilder.BuildAndAttachPreviewData(component, result, context);

                var stopRequestedByAssertion = assertionResults.Any(item => !item.Passed && IsStopOnAssertFailureMode(item.Mode));
                if (stopRequestedByAssertion)
                {
                    context.RequestStop();
                }

                if (result.AssertFailedCount > 0)
                {
                    result.Error = string.Join(" | ", assertionResults
                        .Where(item => !item.Passed && IsAssertMode(item.Mode))
                        .Select(item => item.Message));
                    result.MarkAsCompleted(false);
                }
                else
                {
                    result.MarkAsCompleted(true);
                }
            }
            catch (OperationCanceledException ex)
            {
                result.Error = ex.Message;
                result.MarkAsCompleted(false);
                result.Status = "stopped";
                throw;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.MarkAsCompleted(false);
            }
            finally
            {
                component.Settings = originalSettings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                component.Extractors = originalExtractors ?? new List<VariableExtractionRule>();
                component.Assertions = originalAssertions ?? new List<AssertionRule>();
                ComponentCompleted?.Invoke(result);
                TraceLog(component, result, $"Execute end: {component.Name} status={result.Status} durationMs={result.DurationMs}");
            }

            return result;
        }

        public async Task<ExecutionResult> ExecuteComponentTree(Component component, ExecutionContext context)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThrowIfStopped(context, component.Name);

            if (component is Threads threads)
            {
                return await ExecuteThreads(threads, context);
            }

            // Check if TestPlan has ThreadCount > 1 for multi-threaded execution
            if (component is TestPlan testPlan && 
                testPlan.Settings.TryGetValue("ThreadCount", out var tpThreadCountStr) &&
                int.TryParse(tpThreadCountStr, out var tpThreadCount) &&
                tpThreadCount > 1)
            {
                return await ExecuteTestPlanWithThreads(testPlan, context, tpThreadCount);
            }

            if (component is Loop loop)
            {
                return await ExecuteLoop(loop, context);
            }

            if (component is If conditional)
            {
                return await ExecuteIf(conditional, context);
            }

            if (component is Foreach foreachComponent)
            {
                return await ExecuteForeach(foreachComponent, context);
            }

            if (component is While whileComponent)
            {
                return await ExecuteWhile(whileComponent, context);
            }

            return await ExecuteWithChildren(component, context);
        }

        private async Task<ExecutionResult> ExecuteWithChildren(Component component, ExecutionContext context)
        {
            var result = await ExecuteComponent(component, context);

            foreach (var child in component.Children)
            {
                ThrowIfStopped(context, child.Name);
                var childResult = await ExecuteComponentTree(child, context);
                lock (context.Results)
                {
                    context.Results.Add(childResult);
                }
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteLoop(Loop loop, ExecutionContext context)
        {
            var result = await ExecuteComponent(loop, context);

            var iterations = 1;
            if (loop.Settings.TryGetValue("Iterations", out var value)
                && int.TryParse(value, out var parsed)
                && parsed > 0)
            {
                iterations = parsed;
            }

            var loopData = result.Data as LoopData;
            if (loopData != null)
            {
                loopData.Iterations = iterations;
            }

            for (var i = 0; i < iterations; i++)
            {
                TraceLog(loop, result, $"[ComponentExecutor] Starting loop iteration {i+1} of {iterations}", TraceLevel.Verbose);
                ThrowIfStopped(context, $"{loop.Name} iteration {i}");
                context.SetVariable("loop_index", i);

                if (loopData != null)
                {
                    loopData.CurrentIteration = i;
                }

                // Update variables from extractors during each iteration so children can access them
                if (loop.Extractors != null)
                {
                    foreach (var extractor in loop.Extractors)
                    {
                        // For extractors that get currentIteration, update the variable now
                        if (extractor.JsonPath?.Contains("currentIteration") == true ||
                            extractor.JsonPath?.Contains("CurrentIteration") == true)
                        {
                            context.SetVariable(extractor.VariableName, i);
                            TraceLog(loop, result, $"[ComponentExecutor] Updated variable '{extractor.VariableName}' = {i} for iteration", TraceLevel.Verbose);
                        }
                        else if (extractor.JsonPath?.Contains("iterations") == true)
                        {
                            context.SetVariable(extractor.VariableName, iterations);
                            TraceLog(loop, result, $"[ComponentExecutor] Updated variable '{extractor.VariableName}' = {iterations}", TraceLevel.Verbose);
                        }
                    }
                }

                foreach (var child in loop.Children)
                {
                    var childResult = await ExecuteComponentTree(child, context);
                    lock (context.Results)
                    {
                        context.Results.Add(childResult);
                    }
                }
            }

            // Re-apply variable extractors after loop completes so they capture final state
            if (loop.Extractors != null && loop.Extractors.Count > 0)
            {
                TraceLog(loop, result, $"[ComponentExecutor] Re-applying {loop.Extractors.Count} variable extractors after loop completion", TraceLevel.Verbose);
                _variableService.ApplyVariableExtractors(loop, context, result.Data as ComponentData, (msg, level) => TraceLog(loop, result, msg, level), result);
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteIf(If conditional, ExecutionContext context)
        {
            var result = await ExecuteComponent(conditional, context);
            var condition = conditional.Settings.TryGetValue("Condition", out var value) ? value : string.Empty;
            TraceLog(conditional, result, $"[ComponentExecutor] Evaluating condition for If component: {condition}", TraceLevel.Verbose);
            var conditionMet = _conditionService.Evaluate(condition, context);
            TraceLog(conditional, result, $"[ComponentExecutor] Condition evaluated to: {conditionMet}", TraceLevel.Verbose);

            var ifData = result.Data as IfData;
            if (ifData != null)
            {
                ifData.Condition = condition ?? string.Empty;
                ifData.ConditionMet = conditionMet;
            }

            if (!conditionMet)
            {
                return result;
            }

            foreach (var child in conditional.Children)
            {
                ThrowIfStopped(context, child.Name);
                var childResult = await ExecuteComponentTree(child, context);
                lock (context.Results)
                {
                    context.Results.Add(childResult);
                }
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteForeach(Foreach foreachComponent, ExecutionContext context)
        {
            var result = await ExecuteComponent(foreachComponent, context);

            var sourceVariable = foreachComponent.Settings.TryGetValue("SourceVariable", out var source) ? source : string.Empty;
            var outputVariable = foreachComponent.Settings.TryGetValue("OutputVariable", out var output) ? output?.Trim() : string.Empty;
            
            TraceLog(foreachComponent, result, $"Foreach '{foreachComponent.Name}': SourceVariable='{sourceVariable}', OutputVariable='{outputVariable}'");
            
            // Check if the variable exists
            var variableValue = context.GetVariable(sourceVariable);
            TraceLog(foreachComponent, result, $"Foreach '{foreachComponent.Name}': Variable '{sourceVariable}' value type: {variableValue?.GetType().Name ?? "null"}, value preview: {(variableValue?.ToString()?.Substring(0, Math.Min(100, variableValue?.ToString()?.Length ?? 0)) ?? "null")}");
            
            TraceLog(foreachComponent, result, $"[ComponentExecutor] Resolving items from source variable: {sourceVariable}", TraceLevel.Verbose);
            var items = ResolveCollection(context, sourceVariable).ToList();
            TraceLog(foreachComponent, result, $"[ComponentExecutor] Resolved {items.Count} items", TraceLevel.Verbose);
            
            TraceLog(foreachComponent, result, $"Foreach '{foreachComponent.Name}': Resolved {items.Count} items from '{sourceVariable}'");

            var foreachData = result.Data as ForeachData;
            if (foreachData != null)
            {
                foreachData.Collection = items;
                foreachData.OutputVariable = outputVariable ?? string.Empty;
            }

            for (var i = 0; i < items.Count; i++)
            {
                ThrowIfStopped(context, $"{foreachComponent.Name} iteration {i}");

                context.SetVariable("CurrentItem", items[i]);
                context.SetVariable("CurrentIndex", i);
                if (!string.IsNullOrWhiteSpace(outputVariable))
                {
                    context.SetVariable(outputVariable, items[i]);
                }

                if (foreachData != null)
                {
                    foreachData.CurrentIndex = i;
                    foreachData.CurrentItem = items[i];
                }

                foreach (var child in foreachComponent.Children)
                {
                    var childResult = await ExecuteComponentTree(child, context);
                    lock (context.Results)
                    {
                        context.Results.Add(childResult);
                    }
                }
            }

            // Re-apply variable extractors after foreach completes so they capture final state
            if (foreachComponent.Extractors != null && foreachComponent.Extractors.Count > 0)
            {
                TraceLog(foreachComponent, result, $"[ComponentExecutor] Re-applying {foreachComponent.Extractors.Count} variable extractors after foreach completion", TraceLevel.Verbose);
                _variableService.ApplyVariableExtractors(foreachComponent, context, result.Data as ComponentData, (msg, level) => TraceLog(foreachComponent, result, msg, level), result);
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteWhile(While whileComponent, ExecutionContext context)
        {
            var result = await ExecuteComponent(whileComponent, context);
            
            // Parse settings
            var conditionJson = whileComponent.Settings.TryGetValue("ConditionJson", out var json) ? json : "[]";
            var maxIterations = 1000;
            if (whileComponent.Settings.TryGetValue("MaxIterations", out var maxStr) && int.TryParse(maxStr, out var parsed))
                maxIterations = parsed;
            var timeoutMs = 0;
            if (whileComponent.Settings.TryGetValue("TimeoutMs", out var timeoutStr) && int.TryParse(timeoutStr, out var parsedTimeout))
                timeoutMs = parsedTimeout;
            var evaluationMode = whileComponent.Settings.TryGetValue("EvaluationMode", out var mode) ? mode : "While";
            var isDoWhile = string.Equals(evaluationMode, "DoWhile", StringComparison.OrdinalIgnoreCase);
            
            List<ConditionRow> conditionRows;
            try
            {
                conditionRows = System.Text.Json.JsonSerializer.Deserialize<List<ConditionRow>>(conditionJson) ?? new List<ConditionRow>();
            }
            catch
            {
                conditionRows = new List<ConditionRow>();
            }
            
            var whileData = result.Data as WhileData;
            if (whileData != null)
            {
                whileData.ConditionRows = conditionRows;
                whileData.MaxIterations = maxIterations;
                whileData.TimeoutMs = timeoutMs;
                whileData.EvaluationMode = evaluationMode;
                whileData.ChildComponents = whileComponent.Children.Select(c => c.Id).ToList();
            }
            
            var iteration = 0;
            var startTime = DateTime.UtcNow;
            string? pendingAction = null;
            
            // Helper to evaluate condition rows
            bool EvaluateCondition()
            {
                if (conditionRows.Count == 0)
                    return true;
                
                bool? overallResult = null;
                foreach (var row in conditionRows)
                {
                    // Build condition string: assume Variable is a variable placeholder, Operator is one of ==, !=, >, etc.
                    // For simplicity, we ignore Source and JsonPath for now.
                    var left = row.Variable ?? string.Empty;
                    var op = MapOperator(row.Operator);
                    var right = row.Expected ?? string.Empty;
                    var conditionStr = $"{left} {op} {right}";
                    var rowResult = _conditionService.Evaluate(conditionStr, context);
                    
                    // Combine with logical operator
                    if (row.LogicalOperator.Equals("Or", StringComparison.OrdinalIgnoreCase))
                    {
                        if (overallResult.HasValue && overallResult.Value == false)
                            overallResult = rowResult;
                        else if (!overallResult.HasValue)
                            overallResult = rowResult;
                    }
                    else // And or default
                    {
                        if (!overallResult.HasValue)
                            overallResult = rowResult;
                        else
                            overallResult = overallResult.Value && rowResult;
                    }
                    
                    // Track action
                    if (!string.IsNullOrEmpty(row.Action) && !row.Action.Equals("None", StringComparison.OrdinalIgnoreCase))
                        pendingAction = row.Action;
                    
                    // Early exit if AND condition already false
                    if (row.LogicalOperator.Equals("And", StringComparison.OrdinalIgnoreCase) && overallResult.HasValue && !overallResult.Value)
                        break;
                }
                return overallResult ?? false;
            }
            
            // Loop execution
            while (true)
            {
                // Check timeout
                if (timeoutMs > 0 && (DateTime.UtcNow - startTime).TotalMilliseconds > timeoutMs)
                    break;
                
                // Check max iterations
                if (iteration >= maxIterations)
                    break;
                
                // Evaluate condition (pre-loop for while, post-loop for do-while)
                bool conditionMet;
                if (!isDoWhile)
                {
                    conditionMet = EvaluateCondition();
                    if (!conditionMet)
                        break;
                }
                
                // Check for pending break/continue from previous iteration
                if (pendingAction != null)
                {
                    if (pendingAction.Equals("Break", StringComparison.OrdinalIgnoreCase))
                        break;
                    if (pendingAction.Equals("Continue", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip child execution, but still need to evaluate condition again (loop continues)
                        pendingAction = null;
                        iteration++;
                        continue;
                    }
                }
                
                // Execute child components
                foreach (var child in whileComponent.Children)
                {
                    ThrowIfStopped(context, child.Name);
                    var childResult = await ExecuteComponentTree(child, context);
                    lock (context.Results)
                    {
                        context.Results.Add(childResult);
                    }
                }
                
                iteration++;
                if (whileData != null)
                {
                    whileData.Properties["IterationsExecuted"] = iteration;
                }
                
                // If do-while, evaluate condition after execution
                if (isDoWhile)
                {
                    conditionMet = EvaluateCondition();
                    if (!conditionMet)
                        break;
                }
            }
            
            // Re-apply variable extractors after while completes
            if (whileComponent.Extractors != null && whileComponent.Extractors.Count > 0)
            {
                TraceLog(whileComponent, result, $"[ComponentExecutor] Re-applying {whileComponent.Extractors.Count} variable extractors after while completion", TraceLevel.Verbose);
                _variableService.ApplyVariableExtractors(whileComponent, context, result.Data as ComponentData, (msg, level) => TraceLog(whileComponent, result, msg, level), result);
            }
            
            return result;
        }
        
        private static string MapOperator(string operatorName)
        {
            return operatorName switch
            {
                "Equals" => "==",
                "NotEquals" => "!=",
                "GreaterThan" => ">",
                "GreaterOrEqual" => ">=",
                "LessThan" => "<",
                "LessOrEqual" => "<=",
                _ => "=="
            };
        }

        private async Task<ExecutionResult> ExecuteThreads(Threads threads, ExecutionContext context)
        {
            var result = await ExecuteComponent(threads, context);

            var threadCount = 1;
            if (threads.Settings.TryGetValue("ThreadCount", out var value)
                && int.TryParse(value, out var parsed)
                && parsed > 0)
            {
                threadCount = parsed;
            }

            var threadsData = result.Data as ThreadsData;
            if (threadsData != null)
            {
                threadsData.ThreadCount = threadCount;
            }

            TraceLog(threads, result, $"[ComponentExecutor] Starting {threadCount} threads with ISOLATED variable scoping", TraceLevel.Info);

            // Create a list to track thread-local contexts for optional post-merge
            var threadContexts = new List<(int ThreadIndex, ExecutionContext Context)>();

            var taskList = Enumerable.Range(1, threadCount)
                .Select(index => Task.Run(async () =>
                {
                    CurrentThreadIndex.Value = index;
                    CurrentThreadGroupId.Value = threads.Id;

                    // Create a thread-local context with isolated variables
                    var threadContext = context.CreateThreadLocalContext(index);
                    threadContexts.Add((index, threadContext));

                    TraceLog(threads, result, $"[THREAD-{index}] Started with isolated variable scope", TraceLevel.Verbose);

                    foreach (var child in threads.Children)
                    {
                        ThrowIfStopped(context, child.Name);
                        var childResult = await ExecuteComponentTree(child, threadContext);
                        lock (context.Results)
                        {
                            context.Results.Add(childResult);
                        }
                    }

                    // Log what variables this thread set
                    var localVars = threadContext.GetLocalVariablesOnly();
                    if (localVars.Count > 0)
                    {
                        TraceLog(threads, result, $"[THREAD-{index}] Set {localVars.Count} thread-local variables: [{string.Join(", ", localVars.Keys.Take(10))}]", TraceLevel.Verbose);
                    }

                    TraceLog(threads, result, $"[THREAD-{index}] Completed", TraceLevel.Verbose);
                }, context.StopToken))
                .ToList();

            await Task.WhenAll(taskList);

            TraceLog(threads, result, $"[ComponentExecutor] All {threadCount} threads completed", TraceLevel.Info);

            // Log thread-local variable summary
            foreach (var (threadIndex, threadContext) in threadContexts)
            {
                var localVars = threadContext.GetLocalVariablesOnly();
                TraceLog(threads, result, $"[THREAD-{threadIndex}] Final local variables ({localVars.Count}): [{string.Join(", ", localVars.Select(kv => $"{kv.Key}={TruncateForLogging(kv.Value, 30)}").Take(5))}]", TraceLevel.Verbose);
            }

            // Merge thread-local variables back to parent context so they are visible to subsequent components
            TraceLog(threads, result, $"[ComponentExecutor] Merging thread-local variables to parent context", TraceLevel.Info);
            foreach (var (threadIndex, threadContext) in threadContexts)
            {
                threadContext.MergeThreadLocalVariables();
            }

            // Re-apply variable extractors after threads complete so they capture final state
            // Note: Extractors run on the parent context, not thread-local contexts
            if (threads.Extractors != null && threads.Extractors.Count > 0)
            {
                TraceLog(threads, result, $"[ComponentExecutor] Re-applying {threads.Extractors.Count} variable extractors after threads completion", TraceLevel.Verbose);
                _variableService.ApplyVariableExtractors(threads, context, result.Data as ComponentData, (msg, level) => TraceLog(threads, result, msg, level), result);
            }

            return result;
        }

        private static string TruncateForLogging(object? value, int maxLength = 200)
        {
            if (value == null) return "null";
            var text = value.ToString() ?? string.Empty;
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "...[truncated]";
        }

        /// <summary>
        /// Executes a TestPlan's children in multiple threads when ThreadCount > 1.
        /// Each thread runs all children sequentially with isolated variable scoping.
        /// </summary>
        private async Task<ExecutionResult> ExecuteTestPlanWithThreads(TestPlan testPlan, ExecutionContext context, int threadCount)
        {
            var result = await ExecuteComponent(testPlan, context);

            var testPlanData = result.Data as TestPlanData;
            if (testPlanData != null)
            {
                testPlanData.Status = "running";
            }

            TraceLog(testPlan, result, $"[ComponentExecutor] Starting TestPlan with {threadCount} threads with ISOLATED variable scoping", TraceLevel.Info);

            // Create a list to track thread-local contexts for optional post-merge
            var threadContexts = new List<(int ThreadIndex, ExecutionContext Context)>();

            var taskList = Enumerable.Range(1, threadCount)
                .Select(index => Task.Run(async () =>
                {
                    CurrentThreadIndex.Value = index;
                    CurrentThreadGroupId.Value = testPlan.Id;

                    // Create a thread-local context with isolated variables
                    var threadContext = context.CreateThreadLocalContext(index);
                    threadContexts.Add((index, threadContext));

                    TraceLog(testPlan, result, $"[TESTPLAN-THREAD-{index}] Started with isolated variable scope", TraceLevel.Verbose);

                    foreach (var child in testPlan.Children)
                    {
                        ThrowIfStopped(context, child.Name);
                        var childResult = await ExecuteComponentTree(child, threadContext);
                        lock (context.Results)
                        {
                            context.Results.Add(childResult);
                        }
                    }

                    // Log what variables this thread set
                    var localVars = threadContext.GetLocalVariablesOnly();
                    if (localVars.Count > 0)
                    {
                        TraceLog(testPlan, result, $"[TESTPLAN-THREAD-{index}] Set {localVars.Count} thread-local variables: [{string.Join(", ", localVars.Keys.Take(10))}]", TraceLevel.Verbose);
                    }

                    TraceLog(testPlan, result, $"[TESTPLAN-THREAD-{index}] Completed", TraceLevel.Verbose);
                }, context.StopToken))
                .ToList();

            await Task.WhenAll(taskList);

            TraceLog(testPlan, result, $"[ComponentExecutor] All {threadCount} TestPlan threads completed", TraceLevel.Info);

            // Log thread-local variable summary
            foreach (var (threadIndex, threadContext) in threadContexts)
            {
                var localVars = threadContext.GetLocalVariablesOnly();
                TraceLog(testPlan, result, $"[TESTPLAN-THREAD-{threadIndex}] Final local variables ({localVars.Count}): [{string.Join(", ", localVars.Select(kv => $"{kv.Key}={TruncateForLogging(kv.Value, 30)}").Take(5))}]", TraceLevel.Verbose);
            }

            // Update test plan status
            if (testPlanData != null)
            {
                testPlanData.Status = "completed";
                testPlanData.EndTime = DateTime.UtcNow;
            }

            return result;
        }

        private static IEnumerable<object> ResolveCollection(ExecutionContext context, string? sourceVariable)
        {
            if (string.IsNullOrWhiteSpace(sourceVariable))
            {
                return Array.Empty<object>();
            }

            var value = context.GetVariable(sourceVariable);
            if (value == null)
            {
                return Array.Empty<object>();
            }

            if (value is IEnumerable<object> objectEnumerable)
            {
                return objectEnumerable;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(item ?? string.Empty);
                }
                return list;
            }

            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                return jsonElement.EnumerateArray().Select(item => (object)item.Clone()).ToList();
            }

            if (value is string text)
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        return doc.RootElement.EnumerateArray().Select(item => (object)item.Clone()).ToList();
                    }
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        // Single object - return as single-item list
                        return new List<object> { doc.RootElement.Clone() };
                    }
                }
                catch
                {
                }

                // If not valid JSON, split by comma
                return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Cast<object>()
                    .ToList();
            }

            return new[] { value };
        }

        private void TraceLog(Component component, ExecutionResult result, string message, TraceLevel level = TraceLevel.Info)
        {
            // Also log to centralized Logger
            var logLevel = level switch
            {
                TraceLevel.Verbose => LogLevel.Verbose,
                TraceLevel.Warning => LogLevel.Warning,
                TraceLevel.Error => LogLevel.Error,
                _ => LogLevel.Info
            };
            Logger.Log(message, logLevel, componentId: component?.Id, componentName: component?.Name, executionId: result?.ExecutionId);

            // Keep backward compatibility with old system
            var args = new TraceEventArgs
            {
                ComponentId = component?.Id ?? string.Empty,
                Message = message,
                Level = level,
                Timestamp = DateTime.UtcNow
            };
            if (result != null)
            {
                lock (result.Logs)
                {
                    result.Logs.Add(args);
                }
            }
            Trace?.Invoke(args);
        }

        private static void ThrowIfStopped(ExecutionContext context, string scope)
        {
            if (!context.IsRunning || context.StopToken.IsCancellationRequested)
            {
                throw new OperationCanceledException($"Execution stopped: {scope}");
            }
        }

        private static bool IsAssertMode(string mode)
        {
            return !string.Equals(mode, "Expect", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStopOnAssertFailureMode(string mode)
        {
            return string.Equals(mode, "Assert", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "Expect-Stop", StringComparison.OrdinalIgnoreCase);
        }
    }
}
