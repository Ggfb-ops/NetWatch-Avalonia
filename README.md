# NetWatch

**Log analyzer with auto-detection of errors and fix suggestions**

Drag & drop a `.log` / `.txt` / `.json` file or paste from clipboard (Ctrl+V) — NetWatch will detect error levels, group issues, explain causes, and suggest fixes.

<!-- Screenshots will be added here -->

## Features

- **Auto-detection** — Critical / Error / Warning / Info levels via 80+ regex patterns (EN + RU keywords)
- **Log formats** — plain text, syslog, nginx access log, Windows Event XML
- **Issue grouping** — consecutive errors + stack traces grouped into actionable issues
- **Explanations & fixes** — every detected issue gets a human-readable explanation and fix suggestions
- **Search & filter** — full-text search + level filters
- **HTML report export** — styled dark-theme report with statistics, issues, and full log
- **Drag & drop** — drop file onto the drop zone or use file picker
- **Clipboard paste** — Ctrl+V to paste log text directly
- **Bilingual UI** — Russian / English toggle
- **Cross-platform** — Windows, macOS, Linux

## Tech Stack

- Avalonia UI 12
- .NET 9
- CommunityToolkit.Mvvm 8.4.1

## Build

```bash
dotnet build NetWatch.csproj -c Release
```

## Run

```bash
dotnet run --project NetWatch.csproj -c Release
```

## Tests

38 xUnit tests covering level detection, explanations, issue grouping, syslog/nginx/WinEvent preprocessing, stack traces, and edge cases.

```bash
dotnet test NetWatch.Tests/
```

## License

MIT
