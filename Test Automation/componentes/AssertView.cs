using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    /// <summary>
    /// AssertView component - displays assertion results from other components
    /// </summary>
    public class AssertView : Component
    {
        public AssertView()
        {
            Name = "AssertView";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            // Get bound component IDs from settings
            var boundComponentIds = new List<string>();
            if (Settings.TryGetValue("BoundComponents", out var boundComponents))
            {
                boundComponentIds = boundComponents.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => id.Trim())
                    .ToList();
            }

            // Collect assertion results from bound components
            var assertionResults = new List<AssertionResultItem>();
            var totalPassed = 0;
            var totalFailed = 0;

            foreach (var componentId in boundComponentIds)
            {
                var executionResult = context.Results.FirstOrDefault(r => r.ComponentId == componentId);
                if (executionResult != null && executionResult.AssertionResults != null)
                {
                    foreach (var assertResult in executionResult.AssertionResults)
                    {
                        var item = new AssertionResultItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            ComponentId = componentId,
                            ComponentName = executionResult.ComponentName ?? "Unknown",
                            Source = assertResult.Source,
                            JsonPath = assertResult.JsonPath,
                            Condition = assertResult.Condition,
                            Expected = assertResult.Expected,
                            Actual = assertResult.Actual ?? string.Empty,
                            Passed = assertResult.Passed,
                            ErrorMessage = assertResult.Message ?? string.Empty,
                            Timestamp = executionResult.EndTime ?? executionResult.StartTime,
                            DurationMs = executionResult.DurationMs
                        };

                        assertionResults.Add(item);

                        if (assertResult.Passed)
                            totalPassed++;
                        else
                            totalFailed++;
                    }
                }
            }

            // Build history from context results
            var history = context.Results
                .Where(r => boundComponentIds.Contains(r.ComponentId))
                .GroupBy(r => r.StartTime.Date)
                .Select(g => new AssertionHistoryItem
                {
                    RunId = g.First().ExecutionId,
                    Timestamp = g.Key,
                    TotalPassed = g.Sum(r => r.AssertPassedCount),
                    TotalFailed = g.Sum(r => r.AssertFailedCount),
                    Status = g.Any(r => r.AssertFailedCount > 0) ? "failed" : "passed",
                    DurationMs = (long)g.Sum(r => r.DurationMs)
                })
                .OrderByDescending(h => h.Timestamp)
                .Take(10)
                .ToList();

            var data = new AssertViewData
            {
                Id = this.Id,
                ComponentName = this.Name,
                BoundComponentIds = boundComponentIds,
                AssertionResults = assertionResults,
                History = history,
                TotalPassed = totalPassed,
                TotalFailed = totalFailed,
                DurationMs = 0
            };

            return Task.FromResult<ComponentData>(data);
        }
    }
}
