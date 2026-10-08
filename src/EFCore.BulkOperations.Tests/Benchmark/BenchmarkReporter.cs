using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace EFCore.BulkOperations.Tests.Benchmark;

public sealed class BenchmarkReporter
{
    private static readonly object __fileLock = new();

    public static void RecordResult(BenchmarkResult result)
    {
        lock (__fileLock)
        {
            string? repoRoot = FindRepositoryRoot();

            if (repoRoot is null)
            {
                return;
            }

            UpdateReportFile(repoRoot, result);
            UpdateReadmeFile(repoRoot, result);
        }
    }

    private static string? FindRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;
        var dirInfo = new DirectoryInfo(current);

        while (dirInfo is not null)
        {
            string markerPath = Path.Combine(dirInfo.FullName, "src");

            if (Directory.Exists(markerPath))
            {
                string path = dirInfo.FullName;

                return path;
            }

            dirInfo = dirInfo.Parent;
        }

        return null;
    }

    private static void UpdateReportFile(string repoRoot, BenchmarkResult result)
    {
        string reportFileName = "BENCHMARK_REPORT.md";
        string reportFilePath = Path.Combine(repoRoot, reportFileName);
        var builder = new StringBuilder();

        if (!File.Exists(reportFilePath))
        {
            builder.AppendLine("# EFCore.BulkOperations Benchmark Protocol");
            builder.AppendLine();
            builder.AppendLine("Automated benchmark results comparing Classic EF Core against EFCore.BulkOperations (1,000,000 synthetic records).");
            builder.AppendLine();
            builder.AppendLine("| Date (UTC) | Provider | Mode | Records | Classic Insert | Bulk Insert | Insert Speedup | Classic Update | Bulk Update | Update Speedup | Classic Delete | Bulk Delete | Delete Speedup | Classic Total | Bulk Total | Total Speedup |");
            builder.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |");
        }

        string mode = result.IsAsync ? "Async" : "Sync";
        string dateStr = result.ExecutedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string recordsStr = result.TotalRecords.ToString("N0", CultureInfo.InvariantCulture);
        string classicInsertStr = result.ClassicMetrics.InsertTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s (" + result.ClassicMetrics.InsertSpeedRecordsPerSec.ToString("N0", CultureInfo.InvariantCulture) + " rec/s)";
        string bulkInsertStr = result.BulkMetrics.InsertTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s (" + result.BulkMetrics.InsertSpeedRecordsPerSec.ToString("N0", CultureInfo.InvariantCulture) + " rec/s)";
        string insertSpeedupStr = result.InsertSpeedupFactor.ToString("F1", CultureInfo.InvariantCulture) + "x";
        string classicUpdateStr = result.ClassicMetrics.UpdateTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s (" + result.ClassicMetrics.UpdateSpeedRecordsPerSec.ToString("N0", CultureInfo.InvariantCulture) + " rec/s)";
        string bulkUpdateStr = result.BulkMetrics.UpdateTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s (" + result.BulkMetrics.UpdateSpeedRecordsPerSec.ToString("N0", CultureInfo.InvariantCulture) + " rec/s)";
        string updateSpeedupStr = result.UpdateSpeedupFactor.ToString("F1", CultureInfo.InvariantCulture) + "x";
        string classicDeleteStr = result.ClassicMetrics.DeleteTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s (" + result.ClassicMetrics.DeleteSpeedRecordsPerSec.ToString("N0", CultureInfo.InvariantCulture) + " rec/s)";
        string bulkDeleteStr = result.BulkMetrics.DeleteTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s (" + result.BulkMetrics.DeleteSpeedRecordsPerSec.ToString("N0", CultureInfo.InvariantCulture) + " rec/s)";
        string deleteSpeedupStr = result.DeleteSpeedupFactor.ToString("F1", CultureInfo.InvariantCulture) + "x";
        string classicTotalStr = result.ClassicMetrics.TotalTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s";
        string bulkTotalStr = result.BulkMetrics.TotalTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s";
        string totalSpeedupStr = result.TotalSpeedupFactor.ToString("F1", CultureInfo.InvariantCulture) + "x";

        string row = $"| {dateStr} | {result.DatabaseProvider} | {mode} | {recordsStr} | {classicInsertStr} | {bulkInsertStr} | {insertSpeedupStr} | {classicUpdateStr} | {bulkUpdateStr} | {updateSpeedupStr} | {classicDeleteStr} | {bulkDeleteStr} | {deleteSpeedupStr} | {classicTotalStr} | {bulkTotalStr} | {totalSpeedupStr} |";
        builder.AppendLine(row);

        string contentToAppend = builder.ToString();
        var utf8Encoding = Encoding.UTF8;
        File.AppendAllText(reportFilePath, contentToAppend, utf8Encoding);
    }

    private static void UpdateReadmeFile(string repoRoot, BenchmarkResult result)
    {
        string readmeFileName = "README.md";
        string readmeFilePath = Path.Combine(repoRoot, readmeFileName);

        if (!File.Exists(readmeFilePath))
        {
            return;
        }

        string content = File.ReadAllText(readmeFilePath, Encoding.UTF8);
        string sectionHeader = "## Performances";

        string mode = result.IsAsync ? "Async" : "Sync";
        string dateStr = result.ExecutedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string recordsStr = result.TotalRecords.ToString("N0", CultureInfo.InvariantCulture);
        string insertSpeedup = result.InsertSpeedupFactor.ToString("F1", CultureInfo.InvariantCulture) + "x";
        string updateSpeedup = result.UpdateSpeedupFactor.ToString("F1", CultureInfo.InvariantCulture) + "x";
        string deleteSpeedup = result.DeleteSpeedupFactor.ToString("F1", CultureInfo.InvariantCulture) + "x";
        string totalSpeedup = result.TotalSpeedupFactor.ToString("F1", CultureInfo.InvariantCulture) + "x";
        string classicInsertSec = result.ClassicMetrics.InsertTime.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        string bulkInsertSec = result.BulkMetrics.InsertTime.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        string classicUpdateSec = result.ClassicMetrics.UpdateTime.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        string bulkUpdateSec = result.BulkMetrics.UpdateTime.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        string classicDeleteSec = result.ClassicMetrics.DeleteTime.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        string bulkDeleteSec = result.BulkMetrics.DeleteTime.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";

        string summaryRow = $"| **{result.DatabaseProvider}** | {mode} | {recordsStr} | │ | {classicInsertSec} | {bulkInsertSec} | **{insertSpeedup}** | │ | {classicUpdateSec} | {bulkUpdateSec} | **{updateSpeedup}** | │ | {classicDeleteSec} | {bulkDeleteSec} | **{deleteSpeedup}** | │ | **{totalSpeedup}** |";

        if (!content.Contains(sectionHeader, StringComparison.Ordinal))
        {
            var sectionBuilder = new StringBuilder();
            sectionBuilder.AppendLine();
            sectionBuilder.AppendLine(sectionHeader);
            sectionBuilder.AppendLine($"Measured against **{recordsStr} synthetic records** dataset. Full per-second and throughput metrics logged to [BENCHMARK_REPORT.md](BENCHMARK_REPORT.md).");
            sectionBuilder.AppendLine();
            sectionBuilder.AppendLine("| Database | Mode | Records | │ | Classic Insert | Bulk Insert | Speedup | │ | Classic Update | Bulk Update | Speedup | │ | Classic Delete | Bulk Delete | Speedup | │ | Overall Speedup |");
            sectionBuilder.AppendLine("| :--- | :--- | :--- | :-: | :--- | :--- | :--- | :-: | :--- | :--- | :--- | :-: | :--- | :--- | :--- | :-: | :--- |");
            sectionBuilder.AppendLine(summaryRow);

            string appended = content + sectionBuilder.ToString();
            var utf8Encoding = Encoding.UTF8;
            File.WriteAllText(readmeFilePath, appended, utf8Encoding);
        }
        else
        {
            string providerPrefixPlain = $"| {result.DatabaseProvider} | {mode} |";
            string providerPrefixBold = $"| **{result.DatabaseProvider}** | {mode} |";
            string[] separator = new[] { "\r\n", "\r", "\n" };
            var splitOptions = StringSplitOptions.None;
            string[] lines = content.Split(separator, splitOptions);
            var updatedLines = new StringBuilder();
            bool replaced = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.StartsWith(providerPrefixPlain, StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(providerPrefixBold, StringComparison.OrdinalIgnoreCase))
                {
                    updatedLines.AppendLine(summaryRow);
                    replaced = true;
                }
                else
                {
                    updatedLines.AppendLine(line);
                }
            }

            if (!replaced)
            {
                int tableHeaderIndex = content.IndexOf("| Database | Mode | Records |", StringComparison.Ordinal);

                if (tableHeaderIndex >= 0)
                {
                    int endOfHeaderLine = content.IndexOf('\n', tableHeaderIndex);
                    int endOfDividerLine = content.IndexOf('\n', endOfHeaderLine + 1);

                    if (endOfDividerLine >= 0)
                    {
                        string before = content.Substring(0, endOfDividerLine + 1);
                        string after = content.Substring(endOfDividerLine + 1);
                        string inserted = before + summaryRow + "\n" + after;
                        var utf8Encoding = Encoding.UTF8;
                        File.WriteAllText(readmeFilePath, inserted, utf8Encoding);

                        return;
                    }
                }
            }

            string resultString = updatedLines.ToString();
            var finalEncoding = Encoding.UTF8;
            File.WriteAllText(readmeFilePath, resultString, finalEncoding);
        }
    }
}
