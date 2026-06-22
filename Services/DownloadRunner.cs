using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using YtDlpGui.ViewModels;

namespace YtDlpGui.Services;

public sealed class DownloadRunner
{
    private static readonly Regex ProgressRegex = new(@"\[download\]\s+(?<percent>\d+(?:\.\d+)?)%", RegexOptions.Compiled);
    private static readonly Regex SpeedRegex = new(@"at\s+(?<speed>[^\s]+/s)", RegexOptions.Compiled);

    private Process? _currentProcess;

    public async Task RunQueueAsync(
        string ytDlpPath,
        IReadOnlyList<string> baseArguments,
        IEnumerable<DownloadItemViewModel> queue,
        Action<string> appendLog,
        CancellationToken cancellationToken)
    {
        foreach (var item in queue)
        {
            cancellationToken.ThrowIfCancellationRequested();

            item.Status = "ダウンロード中";
            item.Progress = 0;
            item.Speed = "-";
            appendLog($"[INFO] 開始: {item.Url}");

            using var process = CreateProcess(ytDlpPath, baseArguments, item.Url);
            _currentProcess = process;

            process.OutputDataReceived += (_, args) => HandleLine(args.Data, item, appendLog);
            process.ErrorDataReceived += (_, args) => HandleLine(args.Data, item, appendLog);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                KillCurrentProcess();
                item.Status = "停止";
                appendLog("[WARN] 停止しました");
                throw;
            }

            item.Status = process.ExitCode == 0 ? "完了" : $"失敗 ({process.ExitCode})";
            if (process.ExitCode == 0)
            {
                item.Progress = 100;
            }

            appendLog(process.ExitCode == 0 ? $"[INFO] 完了: {item.Url}" : $"[ERROR] 失敗: {item.Url}");
        }
    }

    public void KillCurrentProcess()
    {
        try
        {
            if (_currentProcess is { HasExited: false })
            {
                _currentProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process may already be gone.
        }
    }

    private static Process CreateProcess(string ytDlpPath, IReadOnlyList<string> baseArguments, string url)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(ytDlpPath) ? "yt-dlp" : ytDlpPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        foreach (var argument in baseArguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.ArgumentList.Add(url);
        return process;
    }

    private static void HandleLine(string? line, DownloadItemViewModel item, Action<string> appendLog)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        appendLog(line);

        var progress = ProgressRegex.Match(line);
        var speed = SpeedRegex.Match(line);
        if (!progress.Success && !speed.Success)
        {
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (progress.Success && double.TryParse(progress.Groups["percent"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                item.Progress = Math.Max(0, Math.Min(100, percent));
            }

            if (speed.Success)
            {
                item.Speed = speed.Groups["speed"].Value;
            }
        });
    }
}
