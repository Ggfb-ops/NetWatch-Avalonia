using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NetWatch.ViewModels;

namespace NetWatch.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel VM => (MainWindowViewModel)DataContext!;

    private readonly Dictionary<string, Border> _statBorders = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        VM.ScrollRequested += OnScrollRequested;

        VM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VM.CurrentLang)) UpdateLabels();
            if (e.PropertyName == nameof(VM.CurrentFilter)) UpdateStatHighlight();
            if (e.PropertyName == nameof(VM.LinesShown)) LinesShownLabel.Text = VM.LinesShown;
            if (e.PropertyName == nameof(VM.HasResult) && VM.HasResult)
                Dispatcher.UIThread.Post(() => UpdateStatHighlight());
        };

        UpdateLabels();
        KeyDown += OnKeyDown;
        Loaded += (_, _) => InitStatBorders();
    }

    private void InitStatBorders()
    {
        if (_statBorders.Count > 0) return;
        _statBorders["critical"] = StatCritical;
        _statBorders["error"] = StatError;
        _statBorders["warning"] = StatWarning;
        _statBorders["info"] = StatInfo;
        _statBorders["all"] = StatTotal;
        UpdateStatHighlight();
    }

    // Обновляем лейблы, которые не биндятся — подпись статистики и деталей
    private void UpdateLabels()
    {
        var ru = VM.CurrentLang == "ru";
        StatCriticalLbl.Text = ru ? "Критические" : "Critical";
        StatErrorLbl.Text = ru ? "Ошибки" : "Errors";
        StatWarningLbl.Text = ru ? "Предупреждения" : "Warnings";
        StatInfoLbl.Text = ru ? "Информация" : "Info";
        StatTotalLbl.Text = ru ? "Всего строк" : "Total lines";

        if (DetailLineLabel != null)
            DetailLineLabel.Text = ru ? "Строка " : "Line ";
        if (DetailHowToFixLabel != null)
            DetailHowToFixLabel.Text = ru ? "Как исправить" : "How to fix";
    }

    private void UpdateStatHighlight()
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var (level, border) in _statBorders)
            {
                var isSelected = !string.IsNullOrEmpty(VM.CurrentFilter) && VM.CurrentFilter == level;
                border.BorderBrush = isSelected
                    ? new SolidColorBrush(Color.Parse("#7c3aed"))
                    : new SolidColorBrush(Color.Parse("#7c3aed40"));
                border.Background = isSelected
                    ? new SolidColorBrush(Color.Parse("#7c3aed18"))
                    : new SolidColorBrush(Color.Parse("#0f0f14"));
            }

            // Подписи
            var labels = new Dictionary<string, TextBlock?>
            {
                ["critical"] = StatCriticalLbl,
                ["error"] = StatErrorLbl,
                ["warning"] = StatWarningLbl,
                ["info"] = StatInfoLbl,
                ["all"] = StatTotalLbl
            };
            foreach (var (level, lbl) in labels)
            {
                if (lbl == null) continue;
                var isSelected = !string.IsNullOrEmpty(VM.CurrentFilter) && VM.CurrentFilter == level;
                lbl.Foreground = isSelected
                    ? new SolidColorBrush(Color.Parse("#e4e4e7"))
                    : new SolidColorBrush(Color.Parse("#71717a"));
            }

            // Полоски
            var strips = new Dictionary<string, Border?>
            {
                ["critical"] = StripCritical,
                ["error"] = StripError,
                ["warning"] = StripWarning,
                ["info"] = StripInfo,
                ["all"] = StripTotal
            };
            foreach (var (level, strip) in strips)
            {
                if (strip == null) continue;
                var isSelected = !string.IsNullOrEmpty(VM.CurrentFilter) && VM.CurrentFilter == level;
                strip.Width = isSelected ? 6 : 4;
            }
        });
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files is null || files.Length == 0) return;
            var path = files[0].TryGetLocalPath();
            if (path != null) await VM.LoadFileAsync(path);
        }
        catch { }
    }

    private void OnDropZoneClick(object? sender, PointerPressedEventArgs e) => OnBrowseClick(sender, e);

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storage = GetTopLevel(this)?.StorageProvider;
            if (storage == null) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = VM.T("filePickerTitle"),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new("Log files") { Patterns = ["*.log", "*.txt", "*.json", "*.csv", "*.xml", "*.yaml", "*.yml", "*.md"] },
                    new("All files") { Patterns = ["*"] }
                ]
            });

            var file = files.FirstOrDefault();
            if (file == null) return;
            var path = file.TryGetLocalPath();
            if (path != null) await VM.LoadFileAsync(path);
        }
        catch { }
    }

    private void OnStatClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string level)
            VM.SetFilterCommand.Execute(level);
    }

    private void OnIssueClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is List<int> lines && lines.Count > 0)
            VM.ScrollToLine(lines[0]);
    }

    private void OnScrollRequested(LogLineItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var container = LogItems.ContainerFromItem(item);
            if (container != null) container.BringIntoView();
        });
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storage = GetTopLevel(this)?.StorageProvider;
            if (storage == null) return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = VM.T("savePickerTitle"),
                SuggestedFileName = $"NetWatch-report-{DateTime.Now:yyyy-MM-dd}.html",
                FileTypeChoices =
                [
                    new("HTML") { Patterns = ["*.html"] },
                    new("All") { Patterns = ["*"] }
                ]
            });

            if (file == null) return;
            var path = file.TryGetLocalPath();
            if (path != null) await VM.ExportReportAsync(path);
        }
        catch { }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard == null) return;
                using var data = await clipboard.TryGetDataAsync();
                if (data == null) return;
                var text = await data.TryGetTextAsync();
                if (!string.IsNullOrEmpty(text) && text.Length > 10 && text.Length <= 5 * 1024 * 1024)
                    VM.ProcessContent(text);
            }
            catch { }
        }
    }
}
