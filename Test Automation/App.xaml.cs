using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using Test_Automation.Services;

namespace Test_Automation
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // WPF emits frequent transient Binding Error:4 messages for container lifecycle
            // (e.g., TreeViewItem ancestor lookups). They are non-fatal and clutter Output.
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Critical;
            base.OnStartup(e);
        }

        public static void ChangeTheme(string themeName)
        {
            var app = (App)Current;
            app.Resources.MergedDictionaries.Clear();
            try
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Themes/{themeName}.xaml") });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/Styles.xaml") });
            }
            catch (Exception ex)
            {
                // Fallback or ignore if resource not found
                Logger.Log($"Error changing theme: {ex.Message}", LogLevel.Error);
            }
        }
    }

}
