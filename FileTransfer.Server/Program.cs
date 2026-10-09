using System.Net;
using System.Net.Sockets;
using FileTransfer.Shared;

string storageDir = Path.Combine(AppContext.BaseDirectory, "ServerFiles");
Directory.CreateDirectory(storageDir);

var listener = new TcpListener(IPAddress.Any, Protocol.Port);
listener.Start();
Console.WriteLine($"Server dang lang nghe port {Protocol.Port}");
Console.WriteLine($"File se luu tai: {storageDir}\n");

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    _ = Task.Run(() => HandleClientAsync(client));
}

async Task HandleClientAsync(TcpClient client)
{
    string who = client.Client.RemoteEndPoint?.ToString() ?? "?";
    Console.WriteLine($"[+] {who} da ket noi");
    try
    {
        using TcpClient c = client;
        await using NetworkStream stream = client.GetStream();

        byte[] cmd = new byte[1];
        await stream.ReadExactlyAsync(cmd);

        switch ((Command)cmd[0])
        {
            case Command.Upload: await HandleUploadAsync(stream, who); break;
            case Command.List: await HandleListAsync(stream, who); break;
            case Command.Download: await HandleDownloadAsync(stream, who); break;
            default: await stream.WriteAsync(new[] { (byte)Status.Error }); break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[!] {who} loi: {ex.Message}");
    }
    Console.WriteLine($"[-] {who} ngat ket noi");
}

async Task HandleUploadAsync(NetworkStream stream, string who)
{
    FileHeader header = await FileHeader.ReadFromAsync(stream);
    string safeName = Path.GetFileName(header.FileName); // chan path traversal
    string savePath = Path.Combine(storageDir, safeName);
    Console.WriteLine($"    {who} gui '{safeName}' ({header.FileSize:N0} byte)");

    byte[] hash = await NetHelper.ReceiveFileDataAsync(stream, savePath, header.FileSize);
    bool ok = hash.SequenceEqual(header.Sha256);
    if (!ok) File.Delete(savePath);

    await stream.WriteAsync(new[] { (byte)(ok ? Status.Ok : Status.HashMismatch) });
    Console.WriteLine(ok ? "    SHA-256 khop, da luu file" : "    SHA-256 KHONG khop, da xoa file");
}

async Task HandleListAsync(NetworkStream stream, string who)
{
    FileInfo[] files = new DirectoryInfo(storageDir).GetFiles();
    await NetHelper.WriteInt32Async(stream, files.Length);
    foreach (FileInfo f in files)
    {
        await NetHelper.WriteStringAsync(stream, f.Name);
        await NetHelper.WriteInt64Async(stream, f.Length);
    }
    Console.WriteLine($"    {who} xem danh sach ({files.Length} file)");
}

async Task HandleDownloadAsync(NetworkStream stream, string who)
{
    string name = Path.GetFileName(await NetHelper.ReadStringAsync(stream));
    string path = Path.Combine(storageDir, name);

    if (name == "" || !File.Exists(path))
    {
        await stream.WriteAsync(new[] { (byte)Status.Error });
        Console.WriteLine($"    {who} xin tai '{name}' nhung khong co file nay");
        return;
    }

    var info = new FileInfo(path);
    byte[] hash = await FileHasher.ComputeAsync(path);

    await stream.WriteAsync(new[] { (byte)Status.Ok });
    await new FileHeader(info.Name, info.Length, hash).WriteToAsync(stream);
    await NetHelper.SendFileDataAsync(stream, path);
    Console.WriteLine($"    {who} da tai '{name}' ({info.Length:N0} byte)");
}