using System.Diagnostics;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;

namespace ScreenshotWebpSaver;

public sealed class ScreenshotMonitorForm : Form
{
    private static readonly string DefaultOutputFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ScreenshotWebpSaver");

    private const string HelperScriptName = "convert_to_webp.py";

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _openOutputItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly string _tempFolder;
    private readonly string _logPath;
    private readonly string _outputFolder;

    private bool _clipboardListenerRegistered;
    private bool _isProcessing;
    private string? _lastImageHash;
    private DateTime _lastHandledAtUtc;

    public ScreenshotMonitorForm()
    {
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Opacity = 0;
        Size = new Size(0, 0);

        _tempFolder = Path.Combine(Path.GetTempPath(), "ScreenshotWebpSaver");
        _logPath = Path.Combine(AppContext.BaseDirectory, "ScreenshotWebpSaver.log");
        _outputFolder = ResolveOutputFolder();

        Directory.CreateDirectory(_tempFolder);
        Directory.CreateDirectory(_outputFolder);

        _statusItem = new ToolStripMenuItem("Waiting for screenshots...", null, (_, _) => { })
        {
            Enabled = false
        };
        _openOutputItem = new ToolStripMenuItem("Open output folder", null, (_, _) => OpenOutputFolder());
        _exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([_statusItem, _openOutputItem, new ToolStripSeparator(), _exitItem]);

        _trayIcon = new NotifyIcon
        {
            Text = "Screenshot WebP Saver",
            Visible = true,
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => OpenOutputFolder();

        _ = Handle;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Hide();
        UpdateStatus("Listening for clipboard screenshots");
        ShowBalloon("Screenshot WebP Saver is running", $"New screenshots will be saved to {_outputFolder}");
        VerifyDependencies();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (!_clipboardListenerRegistered)
        {
            _clipboardListenerRegistered = NativeMethods.AddClipboardFormatListener(Handle);
            Log(_clipboardListenerRegistered
                ? "Clipboard listener registered."
                : "Failed to register clipboard listener.");
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_clipboardListenerRegistered)
        {
            NativeMethods.RemoveClipboardFormatListener(Handle);
            _clipboardListenerRegistered = false;
        }

        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            _ = HandleClipboardUpdateAsync();
        }

        base.WndProc(ref m);
    }

    private async Task HandleClipboardUpdateAsync()
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;

        try
        {
            await Task.Delay(150);

            if (!Clipboard.ContainsImage())
            {
                return;
            }

            using var image = Clipboard.GetImage();
            if (image is null)
            {
                return;
            }

            using var bitmap = new Bitmap(image);
            using var pngStream = new MemoryStream();
            bitmap.Save(pngStream, ImageFormat.Png);
            var pngBytes = pngStream.ToArray();

            var currentHash = Convert.ToHexStringLower(SHA256.HashData(pngBytes));
            var nowUtc = DateTime.UtcNow;

            if (_lastImageHash == currentHash && nowUtc - _lastHandledAtUtc < TimeSpan.FromSeconds(3))
            {
                return;
            }

            _lastImageHash = currentHash;
            _lastHandledAtUtc = nowUtc;

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var tempInputPath = Path.Combine(_tempFolder, $"clip_{stamp}.png");
            var finalOutputPath = Path.Combine(_outputFolder, $"clip_{stamp}.webp");

            await File.WriteAllBytesAsync(tempInputPath, pngBytes);
            UpdateStatus($"Converting {Path.GetFileName(finalOutputPath)}");

            var result = await RunPythonConversionAsync(tempInputPath, finalOutputPath);
            if (!result.Success)
            {
                UpdateStatus("Conversion failed. See log.");
                ShowBalloon("Screenshot WebP conversion failed", result.Message);
                Log(result.Message);
                return;
            }

            TryDeleteFile(tempInputPath);
            UpdateStatus($"Saved {Path.GetFileName(finalOutputPath)}");
            Log($"Saved: {finalOutputPath}");
        }
        catch (Exception ex)
        {
            UpdateStatus("Unexpected error. See log.");
            ShowBalloon("Screenshot WebP conversion error", ex.Message);
            Log(ex.ToString());
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task<(bool Success, string Message)> RunPythonConversionAsync(string inputPath, string outputPath)
    {
        var helperScriptPath = Path.Combine(AppContext.BaseDirectory, HelperScriptName);
        var pythonCommand = ResolvePythonCommand();

        if (pythonCommand is null)
        {
            return (false, "Python was not found. Configure SCREENSHOT_WEBP_PYTHON or install Python.");
        }

        if (!File.Exists(helperScriptPath))
        {
            return (false, $"Conversion script not found: {helperScriptPath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonCommand,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (string.Equals(pythonCommand, "py", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("-3");
        }

        startInfo.ArgumentList.Add(helperScriptPath);
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add(outputPath);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0 || !File.Exists(outputPath))
        {
            var message = new StringBuilder();
            message.AppendLine($"Python exit code: {process.ExitCode}");

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                message.AppendLine(standardOutput.Trim());
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                message.AppendLine(standardError.Trim());
            }

            return (false, message.ToString().Trim());
        }

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            Log(standardOutput.Trim());
        }

        return (true, outputPath);
    }

    private void VerifyDependencies()
    {
        var helperScriptPath = Path.Combine(AppContext.BaseDirectory, HelperScriptName);
        var pythonCommand = ResolvePythonCommand();

        if (pythonCommand is null)
        {
            ShowBalloon("Missing Python", "Python was not found. Set SCREENSHOT_WEBP_PYTHON or install Python.");
            UpdateStatus("Python not found");
            return;
        }

        if (!File.Exists(helperScriptPath))
        {
            ShowBalloon("Missing script", $"Could not find {helperScriptPath}");
            UpdateStatus("Conversion script missing");
        }
    }

    private void OpenOutputFolder()
    {
        Directory.CreateDirectory(_outputFolder);
        Process.Start(new ProcessStartInfo
        {
            FileName = _outputFolder,
            UseShellExecute = true
        });
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        Application.Exit();
    }

    private void UpdateStatus(string message)
    {
        _statusItem.Text = message;
        _trayIcon.Text = message.Length > 63 ? message[..63] : message;
    }

    private void ShowBalloon(string title, string message)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = message;
        _trayIcon.ShowBalloonTip(2500);
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to delete temp file {path}: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(_logPath, line);
        }
        catch
        {
            // Ignore log failures.
        }
    }

    private static string ResolveOutputFolder()
    {
        var configured = Environment.GetEnvironmentVariable("SCREENSHOT_WEBP_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return DefaultOutputFolder;
    }

    private static string? ResolvePythonCommand()
    {
        var configured = Environment.GetEnvironmentVariable("SCREENSHOT_WEBP_PYTHON");
        if (!string.IsNullOrWhiteSpace(configured) && (File.Exists(configured) || IsCommandAvailable(configured)))
        {
            return configured;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        string[] candidatePaths =
        [
            Path.Combine(localAppData, "Programs", "Thonny", "python.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python313", "python.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python312", "python.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python311", "python.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python310", "python.exe"),
            Path.Combine(programFiles, "Python313", "python.exe"),
            Path.Combine(programFiles, "Python312", "python.exe"),
            Path.Combine(programFiles, "Python311", "python.exe"),
            Path.Combine(programFiles, "Python310", "python.exe")
        ];

        var filePath = candidatePaths.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return filePath;
        }

        if (IsCommandAvailable("py"))
        {
            return "py";
        }

        if (IsCommandAvailable("python"))
        {
            return "python";
        }

        return null;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
            _statusItem.Dispose();
            _openOutputItem.Dispose();
            _exitItem.Dispose();
        }

        base.Dispose(disposing);
    }
}
