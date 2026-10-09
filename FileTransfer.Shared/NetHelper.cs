using System;
using System.Collections.Generic;
using System.Text;

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace FileTransfer.Shared;

public static class NetHelper
{
    public static async Task WriteInt32Async(Stream s, int value)
    {
        byte[] b = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(b, value);
        await s.WriteAsync(b);
    }

    public static async Task<int> ReadInt32Async(Stream s)
    {
        byte[] b = new byte[4];
        await s.ReadExactlyAsync(b);
        return BinaryPrimitives.ReadInt32BigEndian(b);
    }

    public static async Task WriteInt64Async(Stream s, long value)
    {
        byte[] b = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(b, value);
        await s.WriteAsync(b);
    }

    public static async Task<long> ReadInt64Async(Stream s)
    {
        byte[] b = new byte[8];
        await s.ReadExactlyAsync(b);
        return BinaryPrimitives.ReadInt64BigEndian(b);
    }

    public static async Task WriteStringAsync(Stream s, string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);
        await WriteInt32Async(s, data.Length);
        await s.WriteAsync(data);
    }

    public static async Task<string> ReadStringAsync(Stream s)
    {
        int len = await ReadInt32Async(s);
        if (len < 0 || len > 1024) throw new InvalidDataException("Chuoi qua dai");
        byte[] data = new byte[len];
        await s.ReadExactlyAsync(data);
        return Encoding.UTF8.GetString(data);
    }

    // Gui noi dung file theo tung khoi 64 KB
    public static async Task SendFileDataAsync(Stream dst, string path, IProgress<long>? progress = null)
    {
        byte[] buffer = new byte[Protocol.BufferSize];
        long sent = 0;
        await using FileStream fs = File.OpenRead(path);
        int n;
        while ((n = await fs.ReadAsync(buffer)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n));
            sent += n;
            progress?.Report(sent);
        }
    }

    // Nhan dung 'size' byte, ghi ra file, tinh SHA-256 trong luc nhan
    public static async Task<byte[]> ReceiveFileDataAsync(Stream src, string savePath, long size, IProgress<long>? progress = null)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[Protocol.BufferSize];
        long received = 0;

        await using (FileStream fs = File.Create(savePath))
        {
            while (received < size)
            {
                int toRead = (int)Math.Min(buffer.Length, size - received);
                int n = await src.ReadAsync(buffer.AsMemory(0, toRead));
                if (n == 0) throw new IOException("Mat ket noi giua chung");

                await fs.WriteAsync(buffer.AsMemory(0, n));
                sha.AppendData(buffer, 0, n);
                received += n;
                progress?.Report(received);
            }
        }
        return sha.GetHashAndReset();
    }
}