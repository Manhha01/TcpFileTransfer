using System.Diagnostics;
using System.Net.Sockets;
using FileTransfer.Shared;

string downloadDir = Path.Combine(AppContext.BaseDirectory, "Downloads");
Directory.CreateDirectory(downloadDir);

Console.Write("IP server (Enter = 127.0.0.1): ");
string host = Console.ReadLine() ?? "";
if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

while (true)
{
    Console.WriteLine("\n1. Gui file   2. Xem file tren server   3. Tai file ve   0. Thoat");
    Console.Write("Chon: ");
    string choice = (Console.ReadLine() ?? "").Trim();
    if (choice == "0") break;

    try
    {
        switch (choice)
        {
            case "1":
                Console.Write("Duong dan file: ");
                string path = (Console.ReadLine() ?? "").Trim().Trim('"');
                if (!File.Exists(path)) Console.WriteLine("Khong tim thay file.");
                else await UploadAsync(path);
                break;
            case "2":
                await ListAsync();
                break;
            case "3":
                Console.Write("Ten file can tai: ");
                await DownloadAsync((Console.ReadLine() ?? "").Trim());
                break;
            default:
                Console.WriteLine("Lua chon khong hop le.");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nLoi: {ex.Message}");
    }
}

async Task UploadAsync(string path)
{
    var info = new FileInfo(path);
    Console.WriteLine("Dang tinh SHA-256...");
    byte[] hash = await FileHasher.ComputeAsync(path);

    using var client = new TcpClient();
    await client.ConnectAsync(host, Protocol.Port);
    await using NetworkStream stream = client.GetStream();

    await stream.WriteAsync(new[] { (byte)Command.Upload });
    await new FileHeader(info.Name, info.Length, hash).WriteToAsync(stream);

    var sw = Stopwatch.StartNew();
    await NetHelper.SendFileDataAsync(stream, path, new ConsoleProgress(info.Length));

    byte[] status = new byte[1];
    await stream.ReadExactlyAsync(status);
    Console.WriteLine($"\nKet qua: {(Status)status[0]} | {Speed(info.Length, sw)}");
}

async Task ListAsync()
{
    using var client = new TcpClient();
    await client.ConnectAsync(host, Protocol.Port);
    await using NetworkStream stream = client.GetStream();

    await stream.WriteAsync(new[] { (byte)Command.List });

    int count = await NetHelper.ReadInt32Async(stream);
    Console.WriteLine($"Server co {count} file:");
    for (int i = 0; i < count; i++)
    {
        string name = await NetHelper.ReadStringAsync(stream);
        long size = await NetHelper.ReadInt64Async(stream);
        Console.WriteLine($"  - {name} ({size:N0} byte)");
    }
}

async Task DownloadAsync(string fileName)
{
    using var client = new TcpClient();
    await client.ConnectAsync(host, Protocol.Port);
    await using NetworkStream stream = client.GetStream();

    await stream.WriteAsync(new[] { (byte)Command.Download });
    await NetHelper.WriteStringAsync(stream, fileName);

    byte[] status = new byte[1];
    await stream.ReadExactlyAsync(status);
    if ((Status)status[0] != Status.Ok)
    {
        Console.WriteLine("Server khong co file nay.");
        return;
    }

    FileHeader header = await FileHeader.ReadFromAsync(stream);
    string savePath = Path.Combine(downloadDir, Path.GetFileName(header.FileName));

    var sw = Stopwatch.StartNew();
    byte[] hash = await NetHelper.ReceiveFileDataAsync(
        stream, savePath, header.FileSize, new ConsoleProgress(header.FileSize));

    bool ok = hash.SequenceEqual(header.Sha256);
    if (!ok) File.Delete(savePath);
    Console.WriteLine(ok
        ? $"\nTai xong, SHA-256 khop | {Speed(header.FileSize, sw)}\nLuu tai: {savePath}"
        : "\nSHA-256 KHONG khop, da xoa file");
}

static string Speed(long bytes, Stopwatch sw) =>
    $"{bytes / 1024.0 / 1024.0 / Math.Max(sw.Elapsed.TotalSeconds, 0.001):F1} MB/s";

class ConsoleProgress(long total) : IProgress<long>
{
    public void Report(long value) => Console.Write($"\r{value * 100 / Math.Max(total, 1)}%");
}