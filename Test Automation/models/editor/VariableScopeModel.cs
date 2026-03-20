using System.Collections.Generic;

namespace Test_Automation.Models.Editor
{
    /// <summary>
    /// Represents a hierarchical model for variable scopes in the test automation system.
    /// This model organizes variables by their scope level (project, testplan, etc.)
    /// </summary>
    public class VariableScopeModel
    {
        /// <summary>
        /// Gets or sets the project-level variables (global variables defined in the Project node)
        /// </summary>
        public Dictionary<string, object> ProjectVariables { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the testplan-level variables (local variables defined in the TestPlan node)
        /// </summary>
        public Dictionary<string, object> TestPlanVariables { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the runtime variables (variables set during execution)
        /// </summary>
        public Dictionary<string, object> RuntimeVariables { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a flat dictionary representation for backward compatibility with existing UI components
        /// </summary>
        public Dictionary<string, object> ToFlatDictionary()
        {
            var result = new Dictionary<string, object>();

            // Add project variables
            foreach (var kvp in ProjectVariables)
            {
                result[kvp.Key] = kvp.Value;
            }

            // Add testplan variables (these will override project variables with the same key)
            foreach (var kvp in TestPlanVariables)
            {
                result[kvp.Key] = kvp.Value;
            }

            // Add runtime variables (these will override testplan variables with the same key)
            foreach (var kvp in RuntimeVariables)
            {
                result[kvp.Key] = kvp.Value;
            }

            return result;
        }
    }
}
