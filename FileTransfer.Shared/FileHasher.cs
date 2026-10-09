using System;
// tính mã SHA - 256 của file
using System.Collections.Generic;
using System.Text;

using System.Security.Cryptography;

namespace FileTransfer.Shared;

public static class FileHasher
{
    public static async Task<byte[]> ComputeAsync(string path)
    {
        await using var fs = File.OpenRead(path);
        return await SHA256.HashDataAsync(fs);
    }

    public static string ToHex(byte[] hash) => Convert.ToHexString(hash).ToLower();
}
