using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using FileTransfer.Shared;

namespace FileTransfer.Client;

public partial class MainWindow : Window
{
    private readonly string _downloadDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "TcpFileTransfer");
    private readonly Stopwatch _sw = new();
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(_downloadDir);
        Log($"File tải về sẽ lưu tại: {_downloadDir}");
    }

    private TransferClient CreateClient()
    {
        if (!int.TryParse(txtPort.Text, out int port)) port = Protocol.Port;
        return new TransferClient(txtHost.Text.Trim(), port);
    }

    // ---------- Ket noi va danh sach file ----------

    private async void BtnConnect_Click(object sender, RoutedEventArgs e) => await RefreshFilesAsync();

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await RefreshFilesAsync();

    private async Task RefreshFilesAsync()
    {
        try
        {
            List<RemoteFile> files = await CreateClient().ListAsync();
            lstFiles.ItemsSource = files;
            SetConnected(true);
            Log($"Đã lấy danh sách: {files.Count} file trên server");
        }
        catch (Exception ex)
        {
            SetConnected(false);
            Log($"Không kết nối được tới server: {ex.Message}");
        }
    }

    private void SetConnected(bool ok)
    {
        statusIcon.Kind = ok ? PackIconKind.LanConnect : PackIconKind.LanDisconnect;
        txtStatus.Text = ok ? "Đã kết nối" : "Mất kết nối";
        statusBadge.Background = new SolidColorBrush(ok
            ? Color.FromArgb(0x33, 0x4C, 0xAF, 0x50)
            : Color.FromArgb(0x33, 0xF4, 0x43, 0x36));
    }

    // ---------- Gui file ----------

    private async void BtnChoose_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "Chọn file cần gửi" };
        if (dialog.ShowDialog() == true)
            await UploadFilesAsync(dialog.FileNames);
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await UploadFilesAsync(paths.Where(File.Exists).ToArray());
    }

    private async Task UploadFilesAsync(string[] paths)
    {
        if (_busy) { Log("Đang truyền file khác, hãy đợi xong rồi thử lại"); return; }
        _busy = true;
        try
        {
            foreach (string path in paths)
            {
                string name = Path.GetFileName(path);
                BeginTransfer($"Đang gửi: {name}");
                try
                {
                    TransferResult r = await CreateClient().UploadAsync(path, CreateProgress());
                    EndTransfer(r);
                    Log(r.Ok ? $"Gửi xong '{name}' | {r.SpeedMBps:F1} MB/s | {r.Message}"
                             : $"Gửi '{name}' thất bại: {r.Message}");
                }
                catch (Exception ex)
                {
                    EndTransfer(new TransferResult(false, "Lỗi kết nối", 0));
                    Log($"Gửi '{name}' thất bại: {ex.Message}");
                }
            }
            await RefreshFilesAsync();
        }
        finally { _busy = false; }
    }

    // ---------- Tai file ve ----------

    private async void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        if (lstFiles.SelectedItem is not RemoteFile file) { Log("Hãy chọn một file trong danh sách trước"); return; }
        if (_busy) { Log("Đang truyền file khác, hãy đợi xong rồi thử lại"); return; }
        _busy = true;
        try
        {
            BeginTransfer($"Đang tải: {file.Name}");
            TransferResult r = await CreateClient().DownloadAsync(file.Name, _downloadDir, CreateProgress());
            EndTransfer(r);
            Log(r.Ok ? $"Tải xong '{file.Name}' | {r.SpeedMBps:F1} MB/s | {r.Message}"
                     : $"Tải '{file.Name}' thất bại: {r.Message}");
        }
        catch (Exception ex)
        {
            EndTransfer(new TransferResult(false, "Lỗi kết nối", 0));
            Log($"Tải '{file.Name}' thất bại: {ex.Message}");
        }
        finally { _busy = false; }
    }

    // ---------- Hien thi tien do ----------

    private IProgress<TransferProgress> CreateProgress()
    {
        _sw.Restart();
        return new Progress<TransferProgress>(p =>
        {
            progressBar.Value = p.Percent;
            txtPercent.Text = $"{p.Percent}%";
            double mbps = p.Bytes / 1048576.0 / Math.Max(_sw.Elapsed.TotalSeconds, 0.001);
            txtSpeed.Text = $"{mbps:F1} MB/s";
        });
    }

    private void BeginTransfer(string title)
    {
        txtFileName.Text = title;
        progressBar.Value = 0;
        txtPercent.Text = "0%";
        txtSpeed.Text = "";
        hashBadge.Visibility = Visibility.Collapsed;
    }

    private void EndTransfer(TransferResult r)
    {
        hashBadge.Visibility = Visibility.Visible;
        hashIcon.Kind = r.Ok ? PackIconKind.ShieldCheck : PackIconKind.ShieldAlert;
        txtHash.Text = r.Message;
        hashBadge.Background = new SolidColorBrush(r.Ok
            ? Color.FromArgb(0x33, 0x4C, 0xAF, 0x50)
            : Color.FromArgb(0x33, 0xF4, 0x43, 0x36));
    }

    private void Log(string message)
    {
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        txtLog.ScrollToEnd();
    }
}