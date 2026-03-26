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
        private readonly bool _isProjectView;
        
        private List<PlanNode> _componentsWithAssertions = new();
        private List<ExecutionResult> _allResults = new();
        private List<AssertionDisplayItem> _allAssertions = new();
        private List<TestPlanGroupItem> _testPlanGroups = new();
        
        private AssertionFilterType _currentFilter = AssertionFilterType.ShowAll;
        private PlanNode? _selectedComponent = null;
        private string? _selectedTestPlanId = null;

        public AssertionViewerWindow(PlanNode parentNode, Models.ExecutionContext executionContext, bool isFullHistoryMode = false)
        {
            InitializeComponent();
            
            _parentNode = parentNode;
            _executionContext = executionContext;
            _isFullHistoryMode = isFullHistoryMode;
            _isProjectView = parentNode.Type == "Project";
            
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

        public class TestPlanGroupItem
        {
            public string TestPlanId { get; set; } = string.Empty;
            public string TestPlanName { get; set; } = string.Empty;
            public int PassedCount { get; set; }
            public int FailedCount { get; set; }
            public int TotalCount => PassedCount + FailedCount;
            public string StatusIcon => FailedCount > 0 ? "✗" : "✓";
            public Brush StatusIconBrush => FailedCount > 0 
                ? new SolidColorBrush(Color.FromRgb(0xB0, 0x00, 0x20)) 
                : new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34));
            public ObservableCollection<ComponentStepItem> Steps { get; set; } = new();
        }

        public class ComponentStepItem
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
            public string TestPlanId { get; set; } = string.Empty;
            public string TestPlanName { get; set; } = string.Empty;
        }

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
            public string TestPlanName { get; set; } = string.Empty;
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
            public string TestPlanId { get; set; } = string.Empty;
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
            public string TestPlanId { get; set; } = string.Empty;
            public string TestPlanName { get; set; } = string.Empty;
            public bool IsTestPlanHeader { get; set; } = false;
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
            
            // 3. Show appropriate control based on view type
            if (_isProjectView)
            {
                ComponentsTreeView.Visibility = Visibility.Visible;
                ComponentsListBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                ComponentsTreeView.Visibility = Visibility.Collapsed;
                ComponentsListBox.Visibility = Visibility.Visible;
            }
            
            // 4. Build and display data
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
            
            // Build component summaries or test plan groups
            if (_isProjectView)
            {
                BuildTestPlanGroups(filteredResults);
                BuildTreeView();
            }
            else
            {
                BuildComponentSummaries(filteredResults);
            }
            
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

        private void BuildTestPlanGroups(List<ExecutionResult> results)
        {
            _testPlanGroups.Clear();
            
            // Find all TestPlan nodes in the project
            var testPlanNodes = _parentNode.Children.Where(c => c.Type == "TestPlan").ToList();
            
            foreach (var testPlan in testPlanNodes)
            {
                // Get all components under this TestPlan that have assertions
                var testPlanComponents = GetDescendantsWithAssertions(testPlan).ToList();
                
                if (testPlanComponents.Count == 0) continue;
                
                var group = new TestPlanGroupItem
                {
                    TestPlanId = testPlan.Id,
                    TestPlanName = testPlan.Name,
                    PassedCount = 0,
                    FailedCount = 0
                };
                
                foreach (var component in testPlanComponents)
                {
                    var componentResults = results
                        .Where(r => string.Equals(r.ComponentId, component.Id, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    if (componentResults.Count == 0) continue;
                    
                    var passed = componentResults.Sum(r => r.AssertPassedCount);
                    var failed = componentResults.Sum(r => r.AssertFailedCount + r.ExpectFailedCount);
                    
                    group.PassedCount += passed;
                    group.FailedCount += failed;
                    
                    group.Steps.Add(new ComponentStepItem
                    {
                        ComponentId = component.Id,
                        ComponentName = component.Name,
                        ComponentType = component.Type,
                        PassedCount = passed,
                        FailedCount = failed,
                        TestPlanId = testPlan.Id,
                        TestPlanName = testPlan.Name
                    });
                }
                
                if (group.TotalCount > 0)
                {
                    _testPlanGroups.Add(group);
                }
            }
        }

        private void BuildTreeView()
        {
            ComponentsTreeView.Items.Clear();
            
            foreach (var group in _testPlanGroups)
            {
                var testPlanItem = new TreeViewItem
                {
                    Header = CreateTestPlanHeader(group),
                    IsExpanded = true,
                    Tag = group.TestPlanId
                };
                
                foreach (var step in group.Steps)
                {
                    var stepItem = new TreeViewItem
                    {
                        Header = CreateStepHeader(step),
                        Tag = step.ComponentId
                    };
                    testPlanItem.Items.Add(stepItem);
                }
                
                ComponentsTreeView.Items.Add(testPlanItem);
            }
        }

        private StackPanel CreateTestPlanHeader(TestPlanGroupItem group)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var icon = new TextBlock
            {
                Text = group.StatusIcon,
                Foreground = group.StatusIconBrush,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 6, 0)
            };
            panel.Children.Add(icon);
            
            var name = new TextBlock
            {
                Text = group.TestPlanName,
                FontWeight = FontWeights.SemiBold
            };
            panel.Children.Add(name);
            
            var counts = new TextBlock
            {
                Text = $"  ({group.PassedCount} passed, {group.FailedCount} failed)",
                Foreground = (Brush)FindResource("TextMutedBrush"),
                FontSize = 11,
                Margin = new Thickness(6, 0, 0, 0)
            };
            panel.Children.Add(counts);
            
            return panel;
        }

        private StackPanel CreateStepHeader(ComponentStepItem step)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var icon = new TextBlock
            {
                Text = step.StatusIcon,
                Foreground = step.StatusIconBrush,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 6, 0)
            };
            panel.Children.Add(icon);
            
            var name = new TextBlock
            {
                Text = step.ComponentName,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            panel.Children.Add(name);
            
            var counts = new TextBlock
            {
                Text = $"  ({step.PassedCount}P, {step.FailedCount}F)",
                Foreground = (Brush)FindResource("TextMutedBrush"),
                FontSize = 10,
                Margin = new Thickness(6, 0, 0, 0)
            };
            panel.Children.Add(counts);
            
            return panel;
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
            
            // Build a map of component ID to TestPlan name for Project view
            var componentToTestPlan = new Dictionary<string, string>();
            var componentToTestPlanId = new Dictionary<string, string>();
            
            if (_isProjectView)
            {
                foreach (var testPlan in _parentNode.Children.Where(c => c.Type == "TestPlan"))
                {
                    var descendants = GetDescendantsWithAssertions(testPlan).ToList();
                    foreach (var desc in descendants)
                    {
                        componentToTestPlan[desc.Id] = testPlan.Name;
                        componentToTestPlanId[desc.Id] = testPlan.Id;
                    }
                }
            }
            
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
                        TestPlanName = componentToTestPlan.GetValueOrDefault(result.ComponentId, ""),
                        TestPlanId = componentToTestPlanId.GetValueOrDefault(result.ComponentId, ""),
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
            var chartData = new List<BarChartDataItem>();
            
            if (_isProjectView)
            {
                // Build hierarchical bar chart for Project view
                var testPlanNodes = _parentNode.Children.Where(c => c.Type == "TestPlan").ToList();
                
                foreach (var testPlan in testPlanNodes)
                {
                    var testPlanComponents = GetDescendantsWithAssertions(testPlan).ToList();
                    if (testPlanComponents.Count == 0) continue;
                    
                    int testPlanPassed = 0;
                    int testPlanFailed = 0;
                    
                    // First, collect totals for this test plan
                    foreach (var component in testPlanComponents)
                    {
                        var componentResults = _allResults
                            .Where(r => string.Equals(r.ComponentId, component.Id, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        
                        testPlanPassed += componentResults.Sum(r => r.AssertPassedCount);
                        testPlanFailed += componentResults.Sum(r => r.AssertFailedCount + r.ExpectFailedCount);
                    }
                    
                    // Add TestPlan header
                    chartData.Add(new BarChartDataItem
                    {
                        StepLabel = testPlan.Name,
                        ComponentName = testPlan.Name,
                        TestPlanId = testPlan.Id,
                        TestPlanName = testPlan.Name,
                        PassedCount = testPlanPassed,
                        FailedCount = testPlanFailed,
                        IsTestPlanHeader = true
                    });
                    
                    // Add steps under this test plan
                    int step = 1;
                    foreach (var component in testPlanComponents)
                    {
                        var componentResults = _allResults
                            .Where(r => string.Equals(r.ComponentId, component.Id, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        
                        if (componentResults.Count == 0) continue;
                        
                        chartData.Add(new BarChartDataItem
                        {
                            StepLabel = $"{step}. {component.Name}",
                            ComponentName = component.Name,
                            ComponentId = component.Id,
                            TestPlanId = testPlan.Id,
                            TestPlanName = testPlan.Name,
                            PassedCount = componentResults.Sum(r => r.AssertPassedCount),
                            FailedCount = componentResults.Sum(r => r.AssertFailedCount + r.ExpectFailedCount),
                            IsTestPlanHeader = false
                        });
                        step++;
                    }
                }
            }
            else
            {
                // Simple bar chart for non-Project views
                int step = 1;
                foreach (var component in _componentsWithAssertions)
                {
                    var componentResults = _allResults
                        .Where(r => string.Equals(r.ComponentId, component.Id, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    if (componentResults.Count == 0) continue;
                    
                    chartData.Add(new BarChartDataItem
                    {
                        StepLabel = $"Step {step}",
                        ComponentName = component.Name,
                        ComponentId = component.Id,
                        PassedCount = componentResults.Sum(r => r.AssertPassedCount),
                        FailedCount = componentResults.Sum(r => r.AssertFailedCount + r.ExpectFailedCount),
                        IsTestPlanHeader = false
                    });
                    step++;
                }
            }
            
            BarChartItemsControl.ItemsSource = null;
            BarChartItemsControl.ItemsSource = chartData;
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
            
            // If a test plan is selected (Project view), filter to that test plan
            if (_selectedTestPlanId != null)
            {
                filteredAssertions = filteredAssertions
                    .Where(a => string.Equals(a.TestPlanId, _selectedTestPlanId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            // If a component is selected (non-Project view), filter to that component
            else if (_selectedComponent != null)
            {
                filteredAssertions = filteredAssertions
                    .Where(a => string.Equals(a.ComponentId, _selectedComponent.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            
            AssertionsDataGrid.ItemsSource = filteredAssertions;
            
            // Update assertion list title
            var filterLabel = _currentFilter == AssertionFilterType.FailedOnly ? " (Failed Only)" : "";
            var selectionLabel = "";
            if (_selectedTestPlanId != null)
            {
                var selectedGroup = _testPlanGroups.FirstOrDefault(g => g.TestPlanId == _selectedTestPlanId);
                selectionLabel = selectedGroup != null ? $" - {selectedGroup.TestPlanName}" : "";
            }
            else if (_selectedComponent != null)
            {
                selectionLabel = $" - {_selectedComponent.Name}";
            }
            AssertionListTitle.Text = $"Assertions ({filteredAssertions.Count}){filterLabel}{selectionLabel}";
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
            _selectedComponent = null;
            _selectedTestPlanId = null;
            
            if (ComponentsListBox.SelectedItem is ComponentStepSummary selected)
            {
                _selectedComponent = _componentsWithAssertions
                    .FirstOrDefault(c => string.Equals(c.Id, selected.ComponentId, StringComparison.OrdinalIgnoreCase));
            }
            
            ApplyFilterAndUpdateGrid();
        }

        private void ComponentsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedComponent = null;
            _selectedTestPlanId = null;
            
            if (ComponentsTreeView.SelectedItem is TreeViewItem selectedItem)
            {
                if (selectedItem.Tag is string tagId)
                {
                    // Check if it's a TestPlan or a component
                    var testPlan = _testPlanGroups.FirstOrDefault(g => g.TestPlanId == tagId);
                    if (testPlan != null)
                    {
                        _selectedTestPlanId = tagId;
                    }
                    else
                    {
                        // It's a component
                        _selectedComponent = _componentsWithAssertions
                            .FirstOrDefault(c => string.Equals(c.Id, tagId, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
            
            ApplyFilterAndUpdateGrid();
        }

        #endregion
    }
}
