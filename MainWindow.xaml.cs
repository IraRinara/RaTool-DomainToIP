using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace RaTool_DomainToIP;

public partial class MainWindow : Window
{
    private string _inputFilePath = string.Empty;
    private string _outputFilePath = string.Empty;
    private bool _isRunning = false;
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        AddLog("RaTool Domain to IP v1.0");
        AddLog("Select input and output files to start.");
    }

    private int GetThreadCount()
    {
        if (ThreadComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
        {
            return int.TryParse(tagStr, out int count) ? count : 20;
        }
        return 20;
    }

    private void BrowseInputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Select Domain List"
        };

        if (dialog.ShowDialog() == true)
        {
            _inputFilePath = dialog.FileName;
            InputFileTextBox.Text = Path.GetFileName(_inputFilePath);
            AddLog($"Input: {Path.GetFileName(_inputFilePath)}");
            UpdateStartButton();
        }
    }

    private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Save Output",
            FileName = "output.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            _outputFilePath = dialog.FileName;
            OutputFileTextBox.Text = Path.GetFileName(_outputFilePath);
            AddLog($"Output: {Path.GetFileName(_outputFilePath)}");
            UpdateStartButton();
        }
    }

    private void UpdateStartButton()
    {
        StartButton.IsEnabled = !string.IsNullOrEmpty(_inputFilePath) &&
                                !string.IsNullOrEmpty(_outputFilePath) &&
                                !_isRunning;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _cts?.Cancel();
            return;
        }

        _isRunning = true;
        _cts = new CancellationTokenSource();
        StartButton.Content = "STOP";
        StartButton.IsEnabled = true;
        BrowseInputButton.IsEnabled = false;
        BrowseOutputButton.IsEnabled = false;
        ThreadComboBox.IsEnabled = false;

        try
        {
            await ResolveDomains(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            AddLog("Cancelled.");
        }
        catch (Exception ex)
        {
            AddLog($"Error: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
            StartButton.Content = "START";
            BrowseInputButton.IsEnabled = true;
            BrowseOutputButton.IsEnabled = true;
            ThreadComboBox.IsEnabled = true;
            UpdateStartButton();
        }
    }

    private async Task ResolveDomains(CancellationToken ct)
    {
        if (!File.Exists(_inputFilePath))
        {
            AddLog("Error: Input file not found.");
            return;
        }

        var domains = await File.ReadAllLinesAsync(_inputFilePath, ct);
        domains = domains.Where(d => !string.IsNullOrWhiteSpace(d)).ToArray();

        int total = domains.Length;
        int processed = 0;
        int success = 0;
        int failed = 0;
        int threadCount = GetThreadCount();

        AddLog($"Resolving {total} domains ({threadCount} threads)...");
        StatusTextBlock.Text = "Processing...";
        ProgressTextBlock.Text = $"0/{total}";

        var results = new List<string>();
        var semaphore = new SemaphoreSlim(threadCount);

        var tasks = domains.Select(async domain =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                var cleanDomain = domain.Trim();
                if (string.IsNullOrEmpty(cleanDomain)) return null;

                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(cleanDomain, ct);
                    var ipList = string.Join(",", addresses.Select(a => a.ToString()));
                    Interlocked.Increment(ref success);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        processed++;
                        ProgressTextBlock.Text = $"{processed}/{total}";
                        AddLog($"{cleanDomain} -> {ipList}");
                    });

                    return $"{cleanDomain}|{ipList}";
                }
                catch
                {
                    Interlocked.Increment(ref failed);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        processed++;
                        ProgressTextBlock.Text = $"{processed}/{total}";
                        AddLog($"{cleanDomain} -> FAILED");
                    });
                    return $"{cleanDomain}|FAILED";
                }
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var resultsArray = await Task.WhenAll(tasks);
        results = resultsArray.Where(r => r != null).Cast<string>().ToList();

        await File.WriteAllLinesAsync(_outputFilePath, results, ct);

        AddLog($"Done! {success} success, {failed} failed");
        StatusTextBlock.Text = "Done";
        ProgressTextBlock.Text = $"{success}/{total}";
    }

    private void AddLog(string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        var line = $"[{time}] {message}\n";

        if (LogTextBlock.Text == "Ready.")
            LogTextBlock.Text = line;
        else
            LogTextBlock.Text += line;

        LogScrollViewer.ScrollToEnd();
    }
}
