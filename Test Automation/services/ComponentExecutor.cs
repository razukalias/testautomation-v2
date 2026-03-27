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
