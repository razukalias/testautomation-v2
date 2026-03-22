using System.Collections.Generic;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    public interface IVariableService
    {
        Dictionary<string, string> ResolveSettings(Dictionary<string, string> settings, ExecutionContext context, System.Action<string, TraceLevel>? trace = null);
        List<VariableExtractionRule> ResolveExtractors(List<VariableExtractionRule> extractors, ExecutionContext context, System.Action<string, TraceLevel>? trace = null);
        void ApplyVariableExtractors(Componentes.Component component, ExecutionContext context, ComponentData? componentData, System.Action<string, TraceLevel> trace, ExecutionResult result);
    }
}
