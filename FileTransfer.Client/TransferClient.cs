using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using FileTransfer.Shared;

namespace FileTransfer.Client;

public record RemoteFile(string Name, long Size)
{
    public string SizeText => Size switch
    {
        < 1024 => $"{Size} B",
        < 1024 * 1024 => $"{Size / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{Size / 1048576.0:F1} MB",
        _ => $"{Size / 1073741824.0:F2} GB"
    };
}

public record TransferResult(bool Ok, string Message, double SpeedMBps);

public record struct TransferProgress(int Percent, long Bytes);

public class TransferClient(string host, int port)
{
    private async Task<TcpClient> ConnectAsync()
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(5));
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<List<RemoteFile>> ListAsync()
    {
        using TcpClient client = await ConnectAsync();
        await using NetworkStream stream = client.GetStream();

        await stream.WriteAsync(new[] { (byte)Command.List });
        int count = await NetHelper.ReadInt32Async(stream);

        var files = new List<RemoteFile>(count);
        for (int i = 0; i < count; i++)
        {
            string name = await NetHelper.ReadStringAsync(stream);
            long size = await NetHelper.ReadInt64Async(stream);
            files.Add(new RemoteFile(name, size));
        }
        return files;
    }

    public async Task<TransferResult> UploadAsync(string path, IProgress<TransferProgress> progress)
    {
        var info = new FileInfo(path);
        byte[] hash = await FileHasher.ComputeAsync(path);

        using TcpClient client = await ConnectAsync();
        await using NetworkStream stream = client.GetStream();

        await stream.WriteAsync(new[] { (byte)Command.Upload });
        await new FileHeader(info.Name, info.Length, hash).WriteToAsync(stream);

        var sw = Stopwatch.StartNew();
        await NetHelper.SendFileDataAsync(stream, path, new PercentAdapter(info.Length, progress));

        byte[] status = new byte[1];
        await stream.ReadExactlyAsync(status);

        return (Status)status[0] switch
        {
            Status.Ok => new(true, "SHA-256 khớp", Speed(info.Length, sw)),
            Status.HashMismatch => new(false, "SHA-256 không khớp", 0),
            _ => new(false, "Server báo lỗi", 0)
        };
    }

    public async Task<TransferResult> DownloadAsync(string fileName, string saveDir, IProgress<TransferProgress> progress)
    {
        using TcpClient client = await ConnectAsync();
        await using NetworkStream stream = client.GetStream();

        await stream.WriteAsync(new[] { (byte)Command.Download });
        await NetHelper.WriteStringAsync(stream, fileName);

        byte[] status = new byte[1];
        await stream.ReadExactlyAsync(status);
        if ((Status)status[0] != Status.Ok)
            return new(false, "Server không có file này", 0);

        FileHeader header = await FileHeader.ReadFromAsync(stream);
        string savePath = Path.Combine(saveDir, Path.GetFileName(header.FileName));

        var sw = Stopwatch.StartNew();
        byte[] hash = await NetHelper.ReceiveFileDataAsync(
            stream, savePath, header.FileSize, new PercentAdapter(header.FileSize, progress));

        if (!hash.SequenceEqual(header.Sha256))
        {
            File.Delete(savePath);
            return new(false, "SHA-256 không khớp", 0);
        }
        return new(true, "SHA-256 khớp", Speed(header.FileSize, sw));
    }

    private static double Speed(long bytes, Stopwatch sw) =>
        bytes / 1048576.0 / Math.Max(sw.Elapsed.TotalSeconds, 0.001);

    // Doi so byte thanh phan tram, chi bao khi phan tram thay doi de giao dien khong bi qua tai
    private sealed class PercentAdapter(long total, IProgress<TransferProgress> target) : IProgress<long>
    {
        private int _last = -1;

        public void Report(long bytes)
        {
            int percent = total == 0 ? 100 : (int)(bytes * 100 / total);
            if (percent == _last) return;
            _last = percent;
            target.Report(new TransferProgress(percent, bytes));
        }
    }
}