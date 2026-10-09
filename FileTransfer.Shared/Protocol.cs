using System;
///CÁC HẰNG SỐ VÀ MÃ LỆNH 
using System.Collections.Generic;
using System.Text;

namespace FileTransfer.Shared;

public enum Command : byte { Upload = 1, Download = 2, List = 3 }

public enum Status : byte { Ok = 0, HashMismatch = 1, Error = 2 }

public static class Protocol
{
    public const int Port = 9000;
    public const int BufferSize = 64 * 1024; // 64 KB mỗi lần gửi
}
