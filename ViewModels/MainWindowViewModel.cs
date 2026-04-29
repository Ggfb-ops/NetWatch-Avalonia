using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetWatch.Models;

namespace NetWatch.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public static readonly LevelToBrushConverter LevelToBrushConverter = new();
    public static readonly NotNullConverter NotNullConverter = new();

    private const int MaxFileSize = 100 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = [".log", ".txt", ".json", ".csv", ".xml", ".yaml", ".yml", ".md"];

    [ObservableProperty] private string _currentLang = "ru";
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private bool _hasIssues;
    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _infoCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _currentFilter = "";
    [ObservableProperty] private string _dropZoneText = "";
    [ObservableProperty] private string _dropZoneHint = "";
    [ObservableProperty] private string _browseText = "";
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _issuesText = "";
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private string _exportText = "";
    [ObservableProperty] private string _clearText = "";
    [ObservableProperty] private string _searchPlaceholder = "";
    [ObservableProperty] private string _linesShown = "";
    [ObservableProperty] private LogLineItem? _selectedLogLine;

    public string LangDisplay => CurrentLang == "ru" ? "RU" : "EN";
    public string SubtitleDisplay => CurrentLang == "ru" ? "Анализатор логов" : "Log Analyzer";
    public string DetailLineLabel => CurrentLang == "ru" ? "Строка " : "Line ";
    public string DetailHowToFixLabel => T("howToFix");

    public ObservableCollection<FilterItem> Filters { get; } = [];
    public ObservableCollection<IssueItem> Issues { get; } = [];
    public ObservableCollection<LogLineItem> LogLines { get; } = [];

    private AnalysisResult? _analysisResult;

    private static readonly Dictionary<string, Dictionary<string, string>> I18N = new()
    {
        ["ru"] = new()
        {
            ["dropText"] = "Перетащи .log / .txt / .json файл сюда",
            ["dropHint"] = "или нажми Ctrl+V для вставки из буфера",
            ["browse"] = "Выбрать файл",
            ["summary"] = "Сводка",
            ["labelCritical"] = "Критические",
            ["labelError"] = "Ошибки",
            ["labelWarning"] = "Предупреждения",
            ["labelInfo"] = "Информация",
            ["labelTotal"] = "Всего строк",
            ["filterAll"] = "Все",
            ["filterCritical"] = "Критические",
            ["filterError"] = "Ошибки",
            ["filterWarning"] = "Предупреждения",
            ["filterInfo"] = "Информация",
            ["issues"] = "Проблемные места",
            ["logTitle"] = "Лог",
            ["searchPlaceholder"] = "Поиск по логу...",
            ["clear"] = "Очистить",
            ["exportReport"] = "Экспорт отчёта",
            ["howToFix"] = "Как исправить",
            ["line"] = "Строка",
            ["lines"] = "Строки",
            ["reportSaved"] = "Отчёт сохранён",
            ["reportTitle"] = "Отчёт NetWatch",
            ["reportDate"] = "Дата анализа",
            ["reportStats"] = "Статистика",
            ["reportIssues"] = "Проблемные места",
            ["reportLog"] = "Лог",
            ["reportGenerated"] = "Сгенерировано NetWatch",
            ["summaryLabel"] = "СТАТИСТИКА",
            ["filePickerTitle"] = "Открыть файл логов",
            ["savePickerTitle"] = "Сохранить отчёт",
        },
        ["en"] = new()
        {
            ["dropText"] = "Drag & drop .log / .txt / .json file here",
            ["dropHint"] = "or press Ctrl+V to paste from clipboard",
            ["browse"] = "Browse file",
            ["summary"] = "Summary",
            ["labelCritical"] = "Critical",
            ["labelError"] = "Errors",
            ["labelWarning"] = "Warnings",
            ["labelInfo"] = "Info",
            ["labelTotal"] = "Total lines",
            ["filterAll"] = "All",
            ["filterCritical"] = "Critical",
            ["filterError"] = "Errors",
            ["filterWarning"] = "Warnings",
            ["filterInfo"] = "Info",
            ["issues"] = "Problem areas",
            ["logTitle"] = "Log",
            ["searchPlaceholder"] = "Search log...",
            ["clear"] = "Clear",
            ["exportReport"] = "Export report",
            ["howToFix"] = "How to fix",
            ["line"] = "Line",
            ["lines"] = "Lines",
            ["reportSaved"] = "Report saved",
            ["reportTitle"] = "NetWatch Report",
            ["reportDate"] = "Analysis date",
            ["reportStats"] = "Statistics",
            ["reportIssues"] = "Problem areas",
            ["reportLog"] = "Log",
            ["reportGenerated"] = "Generated by NetWatch",
            ["summaryLabel"] = "STATISTICS",
            ["filePickerTitle"] = "Open log file",
            ["savePickerTitle"] = "Save report",
        }
    };

    internal string T(string key)
    {
        var dict = I18N.GetValueOrDefault(CurrentLang, I18N["en"]);
        return dict.GetValueOrDefault(key, key);
    }

    public MainWindowViewModel()
    {
        ApplyLang();
    }

    partial void OnCurrentLangChanged(string value)
    {
        OnPropertyChanged(nameof(LangDisplay));
        OnPropertyChanged(nameof(SubtitleDisplay));
    }

    [RelayCommand]
    private void ToggleLang()
    {
        CurrentLang = CurrentLang == "ru" ? "en" : "ru";
        ApplyLang();
        if (_analysisResult != null)
        {
            ApplyFilter();
            RenderIssues();
        }
    }

    private void ApplyLang()
    {
        DropZoneText = T("dropText");
        DropZoneHint = T("dropHint");
        BrowseText = T("browse");
        SummaryText = T("summaryLabel");
        IssuesText = T("issues");
        LogText = T("logTitle");
        ExportText = T("exportReport");
        ClearText = T("clear");
        SearchPlaceholder = T("searchPlaceholder");

        Filters.Clear();
        Filters.Add(new("all", T("filterAll"), CurrentFilter == "" || CurrentFilter == "all"));
        Filters.Add(new("critical", T("filterCritical"), CurrentFilter == "critical"));
        Filters.Add(new("error", T("filterError"), CurrentFilter == "error"));
        Filters.Add(new("warning", T("filterWarning"), CurrentFilter == "warning"));
        Filters.Add(new("info", T("filterInfo"), CurrentFilter == "info"));

        if (_analysisResult != null) RenderIssues();
    }

    [RelayCommand]
    private void SetFilter(string level)
    {
        CurrentFilter = CurrentFilter == level ? "" : level;
        foreach (var f in Filters) f.IsActive = f.Level == (string.IsNullOrEmpty(CurrentFilter) ? "all" : CurrentFilter);
        if (_analysisResult != null) ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        if (_analysisResult != null) ApplyFilter();
    }

    public async Task LoadFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) return;

        // Проверка path traversal
        var fullPath = Path.GetFullPath(filePath);
        if (filePath.Contains('\0')) return;

        var info = new FileInfo(fullPath);
        if (info.Length > MaxFileSize) return;

        var content = await File.ReadAllTextAsync(fullPath);
        ProcessContent(content);
    }

    public void ProcessContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return;
        if (content.Length > 50 * 1024 * 1024)
            content = content[..(50 * 1024 * 1024)];

        _analysisResult = LogParser.Parse(content, CurrentLang);
        CriticalCount = _analysisResult.Stats.Critical;
        ErrorCount = _analysisResult.Stats.Error;
        WarningCount = _analysisResult.Stats.Warning;
        InfoCount = _analysisResult.Stats.Info;
        TotalCount = _analysisResult.Stats.Total;
        HasResult = true;
        HasIssues = _analysisResult.Issues.Count > 0;
        CurrentFilter = "";
        SearchText = "";
        foreach (var f in Filters) f.IsActive = f.Level == "all";
        ApplyFilter();
        RenderIssues();
    }

    private void ApplyFilter()
    {
        if (_analysisResult == null) return;
        var term = SearchText?.ToLowerInvariant() ?? "";
        var filtered = _analysisResult.Parsed.Where(e =>
        {
            if (!string.IsNullOrEmpty(CurrentFilter) && CurrentFilter != "all")
            {
                var filterLevel = CurrentFilter switch
                {
                    "critical" => LogLevel.Critical,
                    "error" => LogLevel.Error,
                    "warning" => LogLevel.Warning,
                    "info" => LogLevel.Info,
                    _ => (LogLevel?)null
                };
                if (e.Level != filterLevel) return false;
            }
            if (!string.IsNullOrEmpty(term) && !e.Trimmed.ToLowerInvariant().Contains(term))
                return false;
            return true;
        }).ToList();

        LogLines.Clear();
        foreach (var entry in filtered)
        {
            var isMatch = !string.IsNullOrEmpty(term) && entry.Trimmed.ToLowerInvariant().Contains(term);
            var fixesTooltip = entry.Fixes is { Length: > 0 }
                ? T("howToFix") + ":\n" + string.Join("\n", entry.Fixes.Select(f => "• " + f))
                : entry.Explanation ?? "";
            LogLines.Add(new(entry.LineNum, entry.Trimmed, entry.Level, isMatch, fixesTooltip, entry.Explanation, entry.Fixes));
        }
        LinesShown = $"({filtered.Count} {(CurrentLang == "ru" ? "строк" : "lines")})";
    }

    private void RenderIssues()
    {
        if (_analysisResult == null) return;
        Issues.Clear();
        foreach (var issue in _analysisResult.Issues.Take(50))
        {
            var levelName = LogParser.GetLevelName(issue.Level, CurrentLang);
            var lineLabel = issue.Lines.Count > 1 ? T("lines") : T("line");
            var lineList = string.Join(", ", issue.Lines.Take(5)) + (issue.Lines.Count > 5 ? "..." : "");
            Issues.Add(new(issue.Level, levelName, lineLabel, lineList,
                issue.Message, issue.Explanation ?? "", issue.Fixes, T("howToFix"), issue.Lines));
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _analysisResult = null;
        HasResult = false;
        HasIssues = false;
        CriticalCount = ErrorCount = WarningCount = InfoCount = TotalCount = 0;
        LogLines.Clear();
        Issues.Clear();
        SelectedLogLine = null;
        SearchText = "";
        CurrentFilter = "";
        LinesShown = "";
        foreach (var f in Filters) f.IsActive = f.Level == "all";
    }

    public async Task ExportReportAsync(string? filePath)
    {
        if (_analysisResult == null || string.IsNullOrEmpty(filePath)) return;
        var html = GenerateReport(_analysisResult);
        await File.WriteAllTextAsync(filePath, html);
    }

    private static string EscapeHtml(string s) => System.Security.SecurityElement.Escape(s) ?? s;

    private string GenerateReport(AnalysisResult result)
    {
        var sb = new StringBuilder(8192);
        var date = DateTime.Now.ToString(CurrentLang == "ru" ? "dd.MM.yyyy HH:mm:ss" : "MM/dd/yyyy hh:mm:ss tt");
        var s = result.Stats;

        sb.Append($@"<!DOCTYPE html>
<html lang=""{CurrentLang}"">
<head><meta charset=""UTF-8""><title>{EscapeHtml(T("reportTitle"))}</title>
<style>
*{{margin:0;padding:0;box-sizing:border-box}}
body{{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#0d1117;color:#e6edf3;padding:24px;max-width:1000px;margin:0 auto}}
h1{{font-size:24px;margin-bottom:8px;color:#7c3aed}}
h2{{font-size:18px;margin:20px 0 10px;border-bottom:1px solid #30363d;padding-bottom:6px}}
.meta{{color:#8b949e;font-size:13px;margin-bottom:20px}}
.stats{{display:flex;gap:10px;margin-bottom:16px;flex-wrap:wrap}}
.stats div{{flex:1;min-width:80px;padding:12px;background:#161b22;border:1px solid #30363d;border-radius:6px;text-align:center}}
.stats .val{{font-size:24px;font-weight:700}}
.stats .lbl{{font-size:11px;color:#8b949e;text-transform:uppercase}}
.critical .val{{color:#f85149}}.error .val{{color:#da3633}}.warning .val{{color:#d29922}}.info .val{{color:#58a6ff}}.total .val{{color:#e6edf3}}
table{{width:100%;border-collapse:collapse;font-size:13px;margin-bottom:20px}}
th{{background:#161b22;padding:8px 12px;text-align:left;border:1px solid #30363d;color:#8b949e;font-size:11px;text-transform:uppercase}}
td{{padding:6px 12px;border:1px solid #30363d;word-break:break-word}}
tr.Critical td{{background:rgba(248,81,73,0.1)}}tr.Error td{{background:rgba(218,54,51,0.08)}}tr.Warning td{{background:rgba(210,153,34,0.06)}}
.tag{{font-weight:600}}.tag.Critical{{color:#f85149}}.tag.Error{{color:#da3633}}.tag.Warning{{color:#d29922}}.tag.Info{{color:#58a6ff}}
em{{color:#58a6ff;font-size:12px}}
ul{{margin:4px 0 0 16px;font-size:12px}}
li{{margin:2px 0;color:#3fb950}}
.footer{{text-align:center;color:#8b949e;font-size:11px;margin-top:30px}}
@media print{{body{{background:#fff;color:#000}}table,.stats div{{border-color:#ccc}}tr.Critical td,tr.Error td,tr.Warning td{{background:#f5f5f5}}}}
</style></head>
<body>
<h1>{EscapeHtml(T("reportTitle"))}</h1>
<div class=""meta"">{EscapeHtml(T("reportDate"))}: {date}</div>
<h2>{EscapeHtml(T("reportStats"))}</h2>
<div class=""stats"">
<div class=""critical""><div class=""val"">{s.Critical}</div><div class=""lbl"">{EscapeHtml(T("labelCritical"))}</div></div>
<div class=""error""><div class=""val"">{s.Error}</div><div class=""lbl"">{EscapeHtml(T("labelError"))}</div></div>
<div class=""warning""><div class=""val"">{s.Warning}</div><div class=""lbl"">{EscapeHtml(T("labelWarning"))}</div></div>
<div class=""info""><div class=""val"">{s.Info}</div><div class=""lbl"">{EscapeHtml(T("labelInfo"))}</div></div>
<div class=""total""><div class=""val"">{s.Total}</div><div class=""lbl"">{EscapeHtml(T("labelTotal"))}</div></div>
</div>
");

        if (result.Issues.Count > 0)
        {
            sb.Append($@"<h2>{EscapeHtml(T("reportIssues"))}</h2>
<table><tr><th>Level</th><th>Lines</th><th>Message</th><th>{EscapeHtml(T("howToFix"))}</th></tr>
");
            foreach (var issue in result.Issues.Take(50))
            {
                var fixes = issue.Fixes is { Length: > 0 }
                    ? $"<ul>{string.Join("", issue.Fixes.Select(f => $"<li>{EscapeHtml(f)}</li>"))}</ul>" : "";
                sb.Append($@"<tr class=""{issue.Level}"">
<td>{EscapeHtml(LogParser.GetLevelName(issue.Level, CurrentLang))}</td>
<td>{string.Join(", ", issue.Lines.Take(5))}</td>
<td>{EscapeHtml(issue.Message)}{(issue.Explanation != null ? $"<br><em>{EscapeHtml(issue.Explanation)}</em>" : "")}</td>
<td>{fixes}</td></tr>
");
            }
            sb.Append("</table>");
        }

        sb.Append($@"<h2>{EscapeHtml(T("reportLog"))}</h2><table><tr><th>#</th><th>Content</th></tr>
");
        foreach (var entry in result.Parsed)
        {
            var cls = entry.Level.HasValue ? $" class=\"{entry.Level.Value}\"" : "";
            var tag = entry.Level.HasValue
                ? $"<span class=\"tag {entry.Level.Value}\">[{entry.Level.Value.ToString().ToUpper()}]</span> "
                : "";
            sb.Append($"<tr{cls}><td>{entry.LineNum}</td><td>{tag}{EscapeHtml(entry.Trimmed)}</td></tr>\n");
        }
        sb.Append($"</table>\n<div class=\"footer\">{EscapeHtml(T("reportGenerated"))}</div>\n</body></html>");

        return sb.ToString();
    }

    public void ScrollToLine(int lineNum)
    {
        var item = LogLines.FirstOrDefault(l => l.LineNum == lineNum);
        if (item != null)
        {
            SelectedLogLine = item;
            ScrollRequested?.Invoke(item);
        }
    }

    public event Action<LogLineItem>? ScrollRequested;
}

public partial class FilterItem : ObservableObject
{
    [ObservableProperty] private bool _isActive;
    public string Level { get; }
    public string Label { get; }

    public FilterItem(string level, string label, bool isActive)
    {
        Level = level;
        Label = label;
        IsActive = isActive;
    }
}

public class IssueItem
{
    public LogLevel Level { get; }
    public string LevelName { get; }
    public string LineLabel { get; }
    public string LineList { get; }
    public string Message { get; }
    public string Explanation { get; }
    public string[]? Fixes { get; }
    public string HowToFixText { get; }
    public List<int> Lines { get; }
    public string LevelClass => Level.ToString().ToLower();
    public bool HasExplanation => !string.IsNullOrEmpty(Explanation);
    public bool HasFixes => Fixes is { Length: > 0 };

    public IssueItem(LogLevel level, string levelName, string lineLabel, string lineList,
        string message, string explanation, string[]? fixes, string howToFixText, List<int> lines)
    {
        Level = level;
        LevelName = levelName;
        LineLabel = lineLabel;
        LineList = lineList;
        Message = message;
        Explanation = explanation;
        Fixes = fixes;
        HowToFixText = howToFixText;
        Lines = lines;
    }
}

public class LogLineItem
{
    public int LineNum { get; }
    public string Content { get; }
    public LogLevel? Level { get; }
    public bool IsSearchMatch { get; }
    public string Tooltip { get; }
    public string? Explanation { get; }
    public string[]? Fixes { get; }
    public string LevelClass => Level?.ToString().ToLower() ?? "";
    public bool HasTooltip => !string.IsNullOrEmpty(Tooltip);
    public bool HasExplanation => !string.IsNullOrEmpty(Explanation);
    public bool HasFixes => Fixes is { Length: > 0 };

    public LogLineItem(int lineNum, string content, LogLevel? level, bool isSearchMatch, string tooltip, string? explanation, string[]? fixes)
    {
        LineNum = lineNum;
        Content = content;
        Level = level;
        IsSearchMatch = isSearchMatch;
        Tooltip = tooltip;
        Explanation = explanation;
        Fixes = fixes;
    }
}
