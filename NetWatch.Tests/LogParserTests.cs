using System.Linq;
using NetWatch.Models;
using Xunit;

namespace NetWatch.Tests;

public class LogParserLevelDetectionTests
{
    [Fact]
    public void Parse_EmptyInput_ReturnsZeroParsed()
    {
        var result = LogParser.Parse("", "en");
        Assert.Empty(result.Parsed);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Parse_SingleError_DetectedAsError()
    {
        var result = LogParser.Parse("ERROR: connection failed", "en");
        Assert.Equal(1, result.Stats.Error);
        Assert.Equal(1, result.Stats.Total);
        var entry = result.Parsed[0];
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public void Parse_SingleCritical_Detected()
    {
        var result = LogParser.Parse("CRITICAL: system failure", "en");
        Assert.Equal(1, result.Stats.Critical);
        var entry = result.Parsed[0];
        Assert.Equal(LogLevel.Critical, entry.Level);
    }

    [Fact]
    public void Parse_SingleWarning_Detected()
    {
        var result = LogParser.Parse("WARNING: disk space low", "en");
        Assert.Equal(1, result.Stats.Warning);
        var entry = result.Parsed[0];
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public void Parse_SingleInfo_Detected()
    {
        var result = LogParser.Parse("INFO: service started", "en");
        Assert.Equal(1, result.Stats.Info);
        var entry = result.Parsed[0];
        Assert.Equal(LogLevel.Info, entry.Level);
    }

    [Fact]
    public void Parse_MixedLevels_CountsCorrect()
    {
        var log = "CRITICAL: crash\nERROR: failed\nWARNING: slow\nINFO: ok\nno level here";
        var result = LogParser.Parse(log, "en");
        Assert.Equal(1, result.Stats.Critical);
        Assert.Equal(1, result.Stats.Error);
        Assert.Equal(1, result.Stats.Warning);
        Assert.Equal(1, result.Stats.Info);
        Assert.Equal(5, result.Stats.Total);
    }

    [Fact]
    public void Parse_RussianKeywords_Detected()
    {
        var result = LogParser.Parse("ОШИБКА: сбой подключения", "ru");
        Assert.Equal(1, result.Stats.Error);
    }

    [Fact]
    public void Parse_RussianCritical_Detected()
    {
        var result = LogParser.Parse("КРИТИЧЕСКАЯ ошибка системы", "ru");
        Assert.Equal(1, result.Stats.Critical);
    }

    [Fact]
    public void Parse_RussianWarning_Detected()
    {
        var result = LogParser.Parse("ПРЕДУПРЕЖДЕНИЕ: мало памяти", "ru");
        Assert.Equal(1, result.Stats.Warning);
    }

    [Fact]
    public void Parse_RussianInfo_Detected()
    {
        var result = LogParser.Parse("ИНФОРМАЦИЯ: сервис запущен", "ru");
        Assert.Equal(1, result.Stats.Info);
    }

    [Fact]
    public void Parse_HttpStatusCodes_Detected()
    {
        var result = LogParser.Parse("500 INTERNAL SERVER ERROR\n502 BAD GATEWAY\n503 SERVICE UNAVAILABLE", "en");
        Assert.Equal(3, result.Stats.Error);
    }

    [Fact]
    public void Parse_NodeErrorCodes_Detected()
    {
        var result = LogParser.Parse("ECONNREFUSED: connection refused\nETIMEDOUT: timeout\nENOTFOUND: dns error", "en");
        Assert.Equal(3, result.Stats.Error);
    }

    [Fact]
    public void Parse_NullLine_Skipped()
    {
        var result = LogParser.Parse("\n\n  \nERROR: real error", "en");
        Assert.Single(result.Parsed);
        Assert.Equal(1, result.Stats.Error);
    }
}

public class LogParserExplanationTests
{
    [Fact]
    public void Parse_ErrorLine_HasExplanation()
    {
        var result = LogParser.Parse("ERROR: database connection failed", "en");
        var entry = result.Parsed[0];
        Assert.NotNull(entry.Explanation);
        Assert.Contains("Component error", entry.Explanation);
    }

    [Fact]
    public void Parse_Timeout_HasFixes()
    {
        var result = LogParser.Parse("TIMEOUT: server not responding", "en");
        var entry = result.Parsed[0];
        Assert.NotNull(entry.Fixes);
        Assert.True(entry.Fixes.Length > 0);
    }

    [Fact]
    public void Parse_RussianError_HasEnglishExplanationViaKeyword()
    {
        var result = LogParser.Parse("ERROR: ОШИБКА connection failed", "en");
        var entry = result.Parsed[0];
        Assert.NotNull(entry.Explanation);
    }

    [Fact]
    public void Parse_InfoLine_HasExplanation()
    {
        var result = LogParser.Parse("INFO: application started", "en");
        var entry = result.Parsed[0];
        Assert.NotNull(entry.Explanation);
    }
}

public class LogParserIssueGroupingTests
{
    [Fact]
    public void Parse_ConsecutiveErrors_GroupedAsIssue()
    {
        var log = "ERROR: first\nERROR: second\nERROR: third";
        var result = LogParser.Parse(log, "en");
        Assert.Equal(3, result.Stats.Error);
        Assert.Single(result.Issues);
        Assert.Equal(3, result.Issues[0].Lines.Count);
    }

    [Fact]
    public void Parse_ErrorsWithNonLevelLine_SeparateIssues()
    {
        var log = "ERROR: first\nsome context line\nERROR: second";
        var result = LogParser.Parse(log, "en");
        Assert.Equal(2, result.Issues.Count);
    }

    [Fact]
    public void Parse_Issues_SortedBySeverity()
    {
        var log = "WARNING: warn\nERROR: err\nCRITICAL: crit";
        var result = LogParser.Parse(log, "en");
        Assert.Equal(LogLevel.Critical, result.Issues[0].Level);
        Assert.Equal(LogLevel.Error, result.Issues[1].Level);
        Assert.Equal(LogLevel.Warning, result.Issues[2].Level);
    }

    [Fact]
    public void Parse_StackTraceAttachedToIssue()
    {
        var log = "ERROR: exception occurred\n   at MyApp.Program.Main() in app.cs:line 42";
        var result = LogParser.Parse(log, "en");
        Assert.Single(result.Issues);
        Assert.True(result.Issues[0].Lines.Count >= 2);
    }

    [Fact]
    public void Parse_Issues_NoLimitInParser()
    {
        var log = string.Join("\n", Enumerable.Range(0, 60).Select(i => $"ERROR: error {i}\ndata"));
        var result = LogParser.Parse(log, "en");
        Assert.Equal(60, result.Issues.Count);
    }
}

public class LogParserSyslogTests
{
    [Fact]
    public void Parse_SyslogFormat_Preprocessed()
    {
        var log = "<34>Jan 01 12:00:00 myhost sshd[1234]: Failed password";
        var result = LogParser.Parse(log, "en");
        Assert.True(result.Stats.Total > 0);
    }

    [Fact]
    public void Parse_Syslog_SeverityDetected()
    {
        var log = "<134>Jan 01 12:00:00 myhost app[100]: INFO starting";
        var result = LogParser.Parse(log, "en");
        Assert.True(result.Stats.Info > 0 || result.Parsed.Count > 0);
    }
}

public class LogParserNginxTests
{
    [Fact]
    public void Parse_NginxAccessFormat_Preprocessed()
    {
        var log = "192.168.1.1 - - [01/Jan/2024:00:00:00 +0000] \"GET /api HTTP/1.1\" 200 1234";
        var result = LogParser.Parse(log, "en");
        Assert.True(result.Parsed.Count > 0);
    }

    [Fact]
    public void Parse_Nginx500_ErrorLevel()
    {
        var log = "10.0.0.1 - - [01/Jan/2024:00:00:00 +0000] \"POST /api HTTP/1.1\" 500 0";
        var result = LogParser.Parse(log, "en");
        Assert.True(result.Stats.Error > 0);
    }

    [Fact]
    public void Parse_Nginx200_InfoLevel()
    {
        var log = "10.0.0.1 - - [01/Jan/2024:00:00:00 +0000] \"GET / HTTP/1.1\" 200 512";
        var result = LogParser.Parse(log, "en");
        Assert.True(result.Stats.Info > 0);
    }
}

public class LogParserWinEventTests
{
    [Fact]
    public void Parse_WinEventXml_Preprocessed()
    {
        var log = "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'>" +
                  "<System><Provider Name='TestProvider'/>" +
                  "<EventID>1000</EventID>" +
                  "<Level>2</Level>" +
                  "<TimeCreated SystemTime='2024-01-01T00:00:00.000Z'/>" +
                  "</System>" +
                  "<EventData><Data>Application error</Data></EventData>" +
                  "</Event>";
        var result = LogParser.Parse(log, "en");
        Assert.True(result.Parsed.Count > 0);
    }
}

public class LogParserStackTraceTests
{
    [Fact]
    public void Parse_DotNetStackTrace_Detected()
    {
        var log = "ERROR: something broke\n   at MyApp.Program.Main(String[] args) in app.cs:line 10";
        var result = LogParser.Parse(log, "en");
        var stackEntry = result.Parsed.FirstOrDefault(e => e.IsStack);
        Assert.NotNull(stackEntry);
    }

    [Fact]
    public void Parse_PythonTraceback_Detected()
    {
        var log = "ERROR: crash\nTraceback (most recent call last):";
        var result = LogParser.Parse(log, "en");
        var stackEntry = result.Parsed.FirstOrDefault(e => e.IsStack);
        Assert.NotNull(stackEntry);
    }

    [Fact]
    public void Parse_CausedBy_Detected()
    {
        var log = "ERROR: failure\nCaused by: NullPointerException";
        var result = LogParser.Parse(log, "en");
        Assert.Contains(result.Parsed, e => e.IsStack);
    }
}

public class LogParserGetLevelNameTests
{
    [Fact]
    public void GetLevelName_English_Correct()
    {
        Assert.Equal("Critical", LogParser.GetLevelName(LogLevel.Critical, "en"));
        Assert.Equal("Error", LogParser.GetLevelName(LogLevel.Error, "en"));
        Assert.Equal("Warning", LogParser.GetLevelName(LogLevel.Warning, "en"));
        Assert.Equal("Info", LogParser.GetLevelName(LogLevel.Info, "en"));
    }

    [Fact]
    public void GetLevelName_Russian_Correct()
    {
        Assert.Equal("Критический", LogParser.GetLevelName(LogLevel.Critical, "ru"));
        Assert.Equal("Ошибка", LogParser.GetLevelName(LogLevel.Error, "ru"));
        Assert.Equal("Предупреждение", LogParser.GetLevelName(LogLevel.Warning, "ru"));
        Assert.Equal("Информация", LogParser.GetLevelName(LogLevel.Info, "ru"));
    }

    [Fact]
    public void GetLevelName_UnknownLang_FallsBackToEnglish()
    {
        Assert.Equal("Critical", LogParser.GetLevelName(LogLevel.Critical, "fr"));
    }
}

public class LogParserEdgeCaseTests
{
    [Fact]
    public void Parse_VeryLongLine_TruncatedInIssueMessage()
    {
        var longLine = new string('x', 300);
        var log = $"ERROR: {longLine}";
        var result = LogParser.Parse(log, "en");
        Assert.Single(result.Issues);
        Assert.True(result.Issues[0].Message.Length <= 200);
    }

    [Fact]
    public void Parse_OnlyWhitespace_NoParsedEntries()
    {
        var result = LogParser.Parse("   \n  \n\t", "en");
        Assert.Empty(result.Parsed);
    }

    [Fact]
    public void Parse_MultipleKeywordsOnSameLine_FirstMatchWins()
    {
        var result = LogParser.Parse("CRITICAL ERROR: fatal crash", "en");
        Assert.Equal(LogLevel.Critical, result.Parsed[0].Level);
    }

    [Fact]
    public void Parse_CaseInsensitiveLevel()
    {
        var result = LogParser.Parse("error: lowercase\nError: mixed case\nERROR: uppercase", "en");
        Assert.Equal(3, result.Stats.Error);
    }
}
