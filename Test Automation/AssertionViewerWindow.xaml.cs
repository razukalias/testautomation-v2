using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Test_Automation.Models;
using Test_Automation.Models.Editor;

namespace Test_Automation
{
    public partial class AssertionViewerWindow : Window
    {
        private readonly PlanNode _parentNode;
        private readonly Models.ExecutionContext _executionContext;
        private readonly bool _isFullHistoryMode;
        
        private List<PlanNode> _componentsWithAssertions = new();
        private List<ExecutionResult> _allResults = new();
        private List<AssertionDisplayItem> _allAssertions = new();
        private List<BarChartDataItem> _barChartData = new();
        
        private AssertionFilterType _currentFilter = AssertionFilterType.ShowAll;
        private PlanNode? _selectedComponent = null;

        public AssertionViewerWindow(PlanNode parentNode, Models.ExecutionContext executionContext, bool isFullHistoryMode = false)
        {
            InitializeComponent();
            
            _parentNode = parentNode;
            _executionContext = executionContext;
            _isFullHistoryMode = isFullHistoryMode;
            
            Title = $"Assertion History - {parentNode.Type}: {parentNode.Name}";
            
            InitializePreviewModeComboBox();
            LoadData();
        }

        private enum AssertionFilterType
        {
            ShowAll,
            FailedOnly
        }

        #region Data Models

        public class ComponentStepSummary
        {
            public string ComponentId { get; set; } = string.Empty;
            public string ComponentName { get; set; } = string.Empty;
            public string ComponentType { get; set; } = string.Empty;
            public int PassedCount { get; set; }
            public int FailedCount { get; set; }
            public int TotalCount => PassedCount + FailedCount;
            public string StatusIcon => FailedCount > 0 ? "✗" : "✓";
            public Brush StatusIconBrush => FailedCount > 0 
                ? new SolidColorBrush(Color.FromRgb(0xB0, 0x00, 0x20)) 
                : new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34));
        }

        public class AssertionDisplayItem
        {
            public string Status { get; set; } = string.Empty;
            public Brush StatusColor { get; set; } = Brushes.Black;
            public string ComponentName { get; set; } = string.Empty;
            public string Mode { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public string JsonPath { get; set; } = string.Empty;
            public string Condition { get; set; } = string.Empty;
            public string Expected { get; set; } = string.Empty;
            public string Actual { get; set; } = string.Empty;
            public bool Passed { get; set; }
            public string Message { get; set; } = string.Empty;
            public DateTime ExecutionTime { get; set; }
            public string ComponentId { get; set; } = string.Empty;
        }

        public class BarChartDataItem
        {
            public string StepLabel { get; set; } = string.Empty;
            public string ComponentName { get; set; } = string.Empty;
            public int PassedCount { get; set; }
            public int FailedCount { get; set; }
            public int TotalCount => PassedCount + FailedCount;
            public double PassedHeight => TotalCount > 0 ? (double)PassedCount / TotalCount * 100 : 0;
            public double FailedHeight => TotalCount > 0 ? (double)FailedCount / TotalCount * 100 : 0;
            public string ComponentId { get; set; } = string.Empty;
        }

        #endregion

        #region Initialization

        private void InitializePreviewModeComboBox()
        {
            PreviewModeComboBox.Items.Add("Last Run");
            PreviewModeComboBox.Items.Add("Full History");
            PreviewModeComboBox.SelectedIndex = _isFullHistoryMode ? 1 : 0;
            PreviewModeComboBox.SelectionChanged += PreviewModeComboBox_SelectionChanged;
        }

        private void LoadData()
        {
            // 1. Get all descendant component IDs that have defined assertions
            _componentsWithAssertions = GetDescendantsWithAssertions(_parentNode).ToList();
            
            if (_componentsWithAssertions.Count == 0)
            {
                MessageBox.Show("No child components with defined assertions found.", 
                                "No Assertions", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 2. Get execution results for those components
            _allResults = GetExecutionResultsForComponents(_componentsWithAssertions);
            
            // 3. Build and display data
            RefreshDisplay();
        }

        private IEnumerable<PlanNode> GetDescendantsWithAssertions(PlanNode node)
        {
            foreach (var child in node.Children)
            {
                // Include child if it has defined assertions
                if (child.Assertions.Count > 0)
                {
                    yield return child;
                }
                
                // Recursively get descendants
                foreach (var descendant in GetDescendantsWithAssertions(child))
                {
                    yield return descendant;
                }
            }
        }

        private List<ExecutionResult> GetExecutionResultsForComponents(List<PlanNode> components)
        {
            if (_executionContext == null || _executionContext.Results == null)
            {
                return new List<ExecutionResult>();
            }
            
            var componentIds = components.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            return _executionContext.Results
                .Where(r => componentIds.Contains(r.ComponentId))
                .OrderBy(r => r.StartTime)
                .ToList();
        }

        #endregion

        #region Data Processing

        private void RefreshDisplay()
        {
            // Filter results based on preview mode
            var filteredResults = FilterByPreviewMode(_allResults);
            
            // Build component summaries
            BuildComponentSummaries(filteredResults);
            
            // Build assertion list
            BuildAssertionList(filteredResults);
            
            // Build bar chart data
            BuildBarChartData();
            
            // Update summary text
            UpdateSummaryText();
        }

        private List<ExecutionResult> FilterByPreviewMode(List<ExecutionResult> results)
        {
            var selectedMode = PreviewModeComboBox.SelectedItem?.ToString() ?? "Last Run";
            
            if (selectedMode == "Full History")
            {
                return results;
            }
            else
            {
                // Last Run: Return only the most recent result per component
                return results
                    .GroupBy(r => r.ComponentId)
                    .Select(g => g.OrderByDescending(r => r.StartTime).First())
                    .OrderBy(r => r.StartTime)
                    .ToList();
            }
        }

        private void BuildComponentSummaries(List<ExecutionResult> results)
        {
            var summaries = new List<ComponentStepSummary>();
            
            foreach (var component in _componentsWithAssertions)
            {
                var componentResults = results
                    .Where(r => string.Equals(r.ComponentId, component.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (componentResults.Count == 0) continue;
                
                var summary = new ComponentStepSummary
                {
                    ComponentId = component.Id,
                    ComponentName = component.Name,
                    ComponentType = component.Type,
                    PassedCount = componentResults.Sum(r => r.AssertPassedCount),
                    FailedCount = componentResults.Sum(r => r.AssertFailedCount + r.ExpectFailedCount)
                };
                
                summaries.Add(summary);
            }
            
            ComponentsListBox.ItemsSource = summaries;
        }

        private void BuildAssertionList(List<ExecutionResult> results)
        {
            var assertions = new List<AssertionDisplayItem>();
            
            foreach (var result in results)
            {
                if (result.AssertionResults == null) continue;
                
                foreach (var assertion in result.AssertionResults)
                {
                    var item = new AssertionDisplayItem
                    {
                        Status = assertion.Passed ? "✓" : "✗",
                        StatusColor = assertion.Passed 
                            ? new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34))
                            : new SolidColorBrush(Color.FromRgb(0xB0, 0x00, 0x20)),
                        ComponentName = result.ComponentName,
                        Mode = assertion.Mode,
                        Source = assertion.Source,
                        JsonPath = assertion.JsonPath,
                        Condition = assertion.Condition,
                        Expected = assertion.Expected,
                        Actual = assertion.Actual,
                        Passed = assertion.Passed,
                        Message = assertion.Message,
                        ExecutionTime = result.StartTime,
                        ComponentId = result.ComponentId
                    };
                    
                    assertions.Add(item);
                }
            }
            
            // Sort: oldest first, failures first within same time
            _allAssertions = assertions
                .OrderBy(a => a.ExecutionTime)
                .ThenBy(a => a.Passed ? 1 : 0)
                .ToList();
            
            ApplyFilterAndUpdateGrid();
        }

        private void BuildBarChartData()
        {
            _barChartData.Clear();
            
            int step = 1;
            foreach (var component in _componentsWithAssertions)
            {
                var componentResults = _allResults
                    .Where(r => string.Equals(r.ComponentId, component.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (componentResults.Count == 0) continue;
                
                var chartItem = new BarChartDataItem
                {
                    StepLabel = $"Step {step}",
                    ComponentName = component.Name,
                    ComponentId = component.Id,
                    PassedCount = componentResults.Sum(r => r.AssertPassedCount),
                    FailedCount = componentResults.Sum(r => r.AssertFailedCount + r.ExpectFailedCount)
                };
                
                _barChartData.Add(chartItem);
                step++;
            }
            
            BarChartItemsControl.ItemsSource = null;
            BarChartItemsControl.ItemsSource = _barChartData;
        }

        private void ApplyFilterAndUpdateGrid()
        {
            List<AssertionDisplayItem> filteredAssertions;
            
            if (_currentFilter == AssertionFilterType.FailedOnly)
            {
                filteredAssertions = _allAssertions.Where(a => !a.Passed).ToList();
            }
            else
            {
                filteredAssertions = _allAssertions;
            }
            
            // If a component is selected, filter to that component
            if (_selectedComponent != null)
            {
                filteredAssertions = filteredAssertions
                    .Where(a => string.Equals(a.ComponentId, _selectedComponent.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            
            AssertionsDataGrid.ItemsSource = filteredAssertions;
            
            // Update assertion list title
            var filterLabel = _currentFilter == AssertionFilterType.FailedOnly ? " (Failed Only)" : "";
            var componentLabel = _selectedComponent != null ? $" - {_selectedComponent.Name}" : "";
            AssertionListTitle.Text = $"Assertions ({filteredAssertions.Count}){filterLabel}{componentLabel}";
        }

        private void UpdateSummaryText()
        {
            var totalAssertions = _allAssertions.Count;
            var passedAssertions = _allAssertions.Count(a => a.Passed);
            var failedAssertions = _allAssertions.Count(a => !a.Passed);
            
            SummaryTextBlock.Text = $"Total: {totalAssertions} | Passed: {passedAssertions} | Failed: {failedAssertions}";
        }

        #endregion

        #region Event Handlers

        private void PreviewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_executionContext == null) return;
            RefreshDisplay();
        }

        private void FilterRadio_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowAllRadio == null || FailedOnlyRadio == null) return;
            
            _currentFilter = ShowAllRadio.IsChecked == true 
                ? AssertionFilterType.ShowAll 
                : AssertionFilterType.FailedOnly;
            
            ApplyFilterAndUpdateGrid();
        }

        private void ComponentsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComponentsListBox.SelectedItem is ComponentStepSummary selected)
            {
                _selectedComponent = _componentsWithAssertions
                    .FirstOrDefault(c => string.Equals(c.Id, selected.ComponentId, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                _selectedComponent = null;
            }
            
            ApplyFilterAndUpdateGrid();
            
            // Update bar chart highlighting (optional enhancement)
            UpdateBarChartHighlight();
        }

        private void UpdateBarChartHighlight()
        {
            // Could add visual highlighting to the selected step in the bar chart
            // For now, we'll keep it simple
        }

        #endregion
    }
}
