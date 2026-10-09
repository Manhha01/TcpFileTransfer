using System;
// ĐÓNG GÓI VÀ ĐỌC THÔNG TIN FILE 
using System.Collections.Generic;
using System.Text;

using System.Buffers.Binary;

namespace FileTransfer.Shared;

public record FileHeader(string FileName, long FileSize, byte[] Sha256)
{
    public async Task WriteToAsync(Stream stream)
    {
        byte[] name = Encoding.UTF8.GetBytes(FileName);
        byte[] buf = new byte[4 + name.Length + 8 + 32];

        BinaryPrimitives.WriteInt32BigEndian(buf, name.Length);
        name.CopyTo(buf, 4);
        BinaryPrimitives.WriteInt64BigEndian(buf.AsSpan(4 + name.Length), FileSize);
        Sha256.CopyTo(buf, 12 + name.Length);

        await stream.WriteAsync(buf);
    }

    public static async Task<FileHeader> ReadFromAsync(Stream stream)
    {
        byte[] lenBuf = new byte[4];
        await stream.ReadExactlyAsync(lenBuf);
        int nameLen = BinaryPrimitives.ReadInt32BigEndian(lenBuf);
        if (nameLen <= 0 || nameLen > 1024)
            throw new InvalidDataException("Độ dài tên file không hợp lệ");

        byte[] nameBuf = new byte[nameLen];
        await stream.ReadExactlyAsync(nameBuf);

        byte[] sizeBuf = new byte[8];
        await stream.ReadExactlyAsync(sizeBuf);

        byte[] hash = new byte[32];
        await stream.ReadExactlyAsync(hash);

        return new FileHeader(
            Encoding.UTF8.GetString(nameBuf),
            BinaryPrimitives.ReadInt64BigEndian(sizeBuf),
            hash);
    }
}
