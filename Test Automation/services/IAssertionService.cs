using System.Collections.Generic;
using System.Threading.Tasks;
using Test_Automation.Models;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    public interface IAssertionService
    {
        List<Models.Editor.AssertionRule> ResolveAssertions(List<Models.Editor.AssertionRule> assertions, ExecutionContext context, System.Action<string, TraceLevel>? trace = null);
        List<AssertionEvaluationResult> EvaluateAssertions(Componentes.Component component, ComponentData? componentData, ExecutionContext context, System.Action<string, TraceLevel> trace, ExecutionResult result);
        Task<List<AssertionEvaluationResult>> EvaluateAssertionsAsync(Componentes.Component component, ComponentData? componentData, ExecutionContext context, System.Action<string, TraceLevel> trace, ExecutionResult result);
        Task<List<AssertionEvaluationResult>> EvaluateAssertionsAsync(Componentes.Component component, ComponentData? componentData, ExecutionContext context, System.Action<string, TraceLevel> trace, ExecutionResult result, List<Models.Editor.AssertionRule>? resolvedAssertions);
    }
}
