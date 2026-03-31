using System.Diagnostics;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;

namespace ScreenshotWebpSaver;

public sealed class ScreenshotMonitorForm : Form
{
    private const string OutputFolder = @"D:\1A-blog-webp-jietu\April";
    private const string ThonnyPythonPath = @"C:\Users\JinRC\AppData\Local\Programs\Thonny\python.exe";
    private const string HelperScriptName = "convert_to_webp.py";

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _openOutputItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly string _tempFolder;
    private readonly string _logPath;

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

        Directory.CreateDirectory(_tempFolder);
        Directory.CreateDirectory(OutputFolder);

        _statusItem = new ToolStripMenuItem("等待截图...", null, (_, _) => { })
        {
            Enabled = false
        };
        _openOutputItem = new ToolStripMenuItem("打开输出文件夹", null, (_, _) => OpenOutputFolder());
        _exitItem = new ToolStripMenuItem("退出", null, (_, _) => ExitApplication());

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
        UpdateStatus("正在后台监听 Win+Shift+S 截图");
        ShowBalloon("截图转 WebP 已启动", $"新的截图会自动保存到 {OutputFolder}");
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
            var finalOutputPath = Path.Combine(OutputFolder, $"clip_{stamp}.webp");

            await File.WriteAllBytesAsync(tempInputPath, pngBytes);
            UpdateStatus($"正在转换 {Path.GetFileName(finalOutputPath)}");

            var result = await RunPythonConversionAsync(tempInputPath, finalOutputPath);
            if (!result.Success)
            {
                UpdateStatus("转换失败，见日志");
                ShowBalloon("截图转 WebP 失败", result.Message);
                Log(result.Message);
                return;
            }

            TryDeleteFile(tempInputPath);
            UpdateStatus($"已保存 {Path.GetFileName(finalOutputPath)}");
            Log($"Saved: {finalOutputPath}");
        }
        catch (Exception ex)
        {
            UpdateStatus("发生异常，见日志");
            ShowBalloon("截图转 WebP 出错", ex.Message);
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
        if (!File.Exists(ThonnyPythonPath))
        {
            return (false, $"找不到 Thonny Python：{ThonnyPythonPath}");
        }

        if (!File.Exists(helperScriptPath))
        {
            return (false, $"找不到转换脚本：{helperScriptPath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ThonnyPythonPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
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

        if (!File.Exists(ThonnyPythonPath))
        {
            ShowBalloon("缺少 Python", $"没有找到 {ThonnyPythonPath}");
            UpdateStatus("缺少 Thonny Python");
            return;
        }

        if (!File.Exists(helperScriptPath))
        {
            ShowBalloon("缺少脚本", $"没有找到 {helperScriptPath}");
            UpdateStatus("缺少转换脚本");
        }
    }

    private void OpenOutputFolder()
    {
        Directory.CreateDirectory(OutputFolder);
        Process.Start(new ProcessStartInfo
        {
            FileName = OutputFolder,
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
