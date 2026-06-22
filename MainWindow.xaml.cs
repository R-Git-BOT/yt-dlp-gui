using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using YtDlpGui.ViewModels;

namespace YtDlpGui
{
    public partial class MainWindow : Window
    {
        private static readonly string OutputTemplateDebugLogPath =
            Path.Combine(Path.GetTempPath(), "yt-dlp-gui-output-template-debug.log");
        private bool _isClosingAfterAutoSave;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            Closing += MainWindow_Closing;
            LogOutputTemplateDebug("MainWindow initialized");
        }

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isClosingAfterAutoSave || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            e.Cancel = true;
            try
            {
                await viewModel.SaveAutoSettingsAsync();
            }
            finally
            {
                _isClosingAfterAutoSave = true;
                Close();
            }
        }

        private void OutputTemplateFieldButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string token } || string.IsNullOrWhiteSpace(token))
            {
                LogOutputTemplateDebug("field button click ignored: missing token");
                return;
            }

            var textBox = FindVisualDescendant<TextBox>(this, "OutputTemplateTextBox");
            if (textBox is null)
            {
                LogOutputTemplateDebug($"field button click ignored: text box not found token={token}");
                return;
            }

            LogOutputTemplateDebug(
                $"field button click start token={token} focus={DescribeFocus()} len={textBox.Text.Length} caret={textBox.CaretIndex} selectionStart={textBox.SelectionStart} selectionLength={textBox.SelectionLength} canUndo={textBox.CanUndo}");

            textBox.Focus();
            Keyboard.Focus(textBox);
            try
            {
                LogOutputTemplateDebug($"before selectedText insert focus={DescribeFocus()} canUndo={textBox.CanUndo}");
                var selectionStart = textBox.SelectionStart;
                textBox.SelectedText = token;
                textBox.CaretIndex = selectionStart + token.Length;
                LogOutputTemplateDebug(
                    $"after selectedText insert len={textBox.Text.Length} caret={textBox.CaretIndex} selectionStart={textBox.SelectionStart} selectionLength={textBox.SelectionLength} canUndo={textBox.CanUndo}");
            }
            catch (Exception ex)
            {
                LogOutputTemplateDebug($"selectedText insert failed: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.IsOutputTemplateBuilderEnabled = true;
                LogOutputTemplateDebug("output template builder enabled");
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.Focus();
                Keyboard.Focus(textBox);
                LogOutputTemplateDebug(
                    $"after dispatcher focus focus={DescribeFocus()} len={textBox.Text.Length} caret={textBox.CaretIndex} canUndo={textBox.CanUndo}");
            }), DispatcherPriority.Input);
        }

        private void OutputTemplateTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                LogOutputTemplateDebug(
                    $"preview ctrl+z before handled={e.Handled} focus={DescribeFocus()} len={textBox.Text.Length} caret={textBox.CaretIndex} selectionStart={textBox.SelectionStart} selectionLength={textBox.SelectionLength} canUndo={textBox.CanUndo}");

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogOutputTemplateDebug(
                        $"preview ctrl+z after-dispatch len={textBox.Text.Length} caret={textBox.CaretIndex} selectionStart={textBox.SelectionStart} selectionLength={textBox.SelectionLength} canUndo={textBox.CanUndo}");
                }), DispatcherPriority.Background);
            }
        }

        private void OutputTemplateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            LogOutputTemplateDebug(
                $"text changed len={textBox.Text.Length} caret={textBox.CaretIndex} changes={e.Changes.Count} canUndo={textBox.CanUndo}");
        }

        private static T? FindVisualDescendant<T>(DependencyObject parent, string name)
            where T : FrameworkElement
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name && element.IsVisible)
                {
                    return element;
                }

                var found = FindVisualDescendant<T>(child, name);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string DescribeFocus()
        {
            var focused = Keyboard.FocusedElement;
            return focused switch
            {
                FrameworkElement element when !string.IsNullOrWhiteSpace(element.Name) => $"{element.GetType().Name}:{element.Name}",
                FrameworkElement element => element.GetType().Name,
                null => "null",
                _ => focused.GetType().Name
            };
        }

        private static void LogOutputTemplateDebug(string message)
        {
            try
            {
                File.AppendAllText(
                    OutputTemplateDebugLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Debug logging must never break the UI.
            }
        }
    }
}
