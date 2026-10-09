# TCP File Transfer

Hệ thống truyền file Client-Server qua TCP/IP: gửi file lên server, xem danh sách và tải file về, kiểm tra toàn vẹn bằng SHA-256. Viết bằng C# 14 trên .NET 10, giao diện WPF (Material Design).

## 1. Yêu cầu

| Thành phần | Ghi chú |
| --- | --- |
| Windows 10 hoặc 11 | Giao diện WPF chỉ chạy trên Windows |
| Visual Studio 2026 Community | Khi cài, tích workload **.NET desktop development** (đã kèm .NET 10 SDK) |
| Internet ở lần build đầu tiên | Để tải thư viện MaterialDesignThemes từ NuGet |
| Wireshark (không bắt buộc) | Để bắt và phân tích gói tin khi demo |

Kiểm tra .NET: mở Command Prompt, gõ `dotnet --version`, kết quả phải bắt đầu bằng `10.`

## 2. Tải mã nguồn

Cách 1, dùng Git:

```
git clone https://github.com/Manhha01/TcpFileTransfer.git
```

Cách 2, không dùng Git: trên trang GitHub bấm **Code → Download ZIP**, rồi **giải nén** ra thư mục.

Nên đặt project ở thư mục đơn giản như `D:\Projects\TcpFileTransfer`. Tránh để trong Desktop hoặc thư mục đang đồng bộ OneDrive vì dễ gây lỗi khóa file khi build.

## 3. Build và chạy bằng Visual Studio

1. Mở file **TcpFileTransfer.slnx** bằng Visual Studio 2026.
2. Menu **Build → Rebuild Solution**. Lần đầu sẽ mất vài chục giây để tải thư viện. Kết quả phải là 0 lỗi.
3. Chuột phải **Solution 'TcpFileTransfer'** → **Configure Startup Projects** → chọn **Multiple startup projects** → đặt **Start** cho `FileTransfer.Server` và `FileTransfer.Client`, hai project còn lại để **None** → OK.
   (Thiết lập này lưu trên máy từng người, không đi theo GitHub, nên ai tải về cũng phải làm bước này.)
4. Nhấn **F5**. Cửa sổ console của server và cửa sổ giao diện client sẽ mở ra. Nếu Windows Firewall hỏi, chọn **Allow**.
5. Trên giao diện, giữ IP `127.0.0.1`, port `9000`, bấm **Kết nối**, rồi kéo thả file vào vùng nét đứt để gửi.

## 4. Chạy bằng dòng lệnh (không cần mở Visual Studio)

Mở hai cửa sổ Command Prompt tại thư mục gốc của project:

```
dotnet run --project FileTransfer.Server
```

```
dotnet run --project FileTransfer.Client
```

Có thể thay Client bằng `FileTransfer.TestClient` để dùng bản console.

## 5. Chạy trên hai máy khác nhau

Hai máy phải cùng một mạng Wi-Fi hoặc LAN.

Trên **máy server**:

1. Chạy `ipconfig`, ghi lại địa chỉ **IPv4 Address** (ví dụ `192.168.1.10`).
2. Mở port 9000 trên tường lửa (Command Prompt chạy bằng quyền Administrator):
   ```
   netsh advfirewall firewall add rule name="TcpFileTransfer" dir=in action=allow protocol=TCP localport=9000
   ```
3. Chạy server.

Trên **máy client**: chạy ứng dụng, nhập IP của máy server vào ô **IP server**, bấm **Kết nối**.

## 6. File được lưu ở đâu

| Chương trình | Thư mục |
| --- | --- |
| Server | `FileTransfer.Server\bin\Debug\net10.0\ServerFiles` |
| Client (giao diện) | `Downloads\TcpFileTransfer` trong thư mục người dùng |
| TestClient (console) | `FileTransfer.TestClient\bin\Debug\net10.0\Downloads` |

## 7. Cấu trúc project

| Project | Loại | Vai trò |
| --- | --- | --- |
| FileTransfer.Shared | Class Library | Giao thức dùng chung: Protocol, FileHeader, FileHasher, NetHelper |
| FileTransfer.Server | Console App | Máy chủ lắng nghe port 9000, phục vụ nhiều client cùng lúc |
| FileTransfer.Client | WPF App | Giao diện gửi, xem danh sách, tải file |
| FileTransfer.TestClient | Console App | Client dạng menu để kiểm thử nhanh |

## 8. Lỗi thường gặp

| Hiện tượng | Nguyên nhân và cách xử lý |
| --- | --- |
| `The current .NET SDK does not support targeting .NET 10.0` | Chưa có .NET 10 SDK. Cài Visual Studio 2026 với workload .NET desktop development, hoặc tải .NET 10 SDK tại dot.net |
| Không mở được file `.slnx` | Phiên bản Visual Studio quá cũ. Dùng Visual Studio 2026 |
| Giao diện có dấu X đỏ, báo lỗi MaterialDesign | Thư viện chưa tải về. Kiểm tra Internet, chuột phải Solution → **Restore NuGet Packages**, rồi **Rebuild Solution** |
| Nhấn F5 chỉ mở một cửa sổ | Chưa làm bước Configure Startup Projects ở mục 3 |
| `Only one usage of each socket address` khi chạy server | Port 9000 đang bị server cũ chiếm. Tắt cửa sổ server cũ hoặc nhấn Shift + F5 rồi chạy lại |
| Client báo "Không kết nối được tới server" | Server chưa chạy, nhập sai IP, tường lửa chưa mở port 9000, hoặc hai máy không cùng mạng |
| Báo lỗi file đang bị tiến trình khác sử dụng khi build | Tắt hết cửa sổ chương trình đang chạy, hoặc đóng Visual Studio rồi mở lại |
| Wireshark không thấy gói tin khi chạy cùng một máy | Phải chọn card **Adapter for loopback traffic capture**, bộ lọc `tcp.port == 9000` |

## 9. Lưu ý bảo mật

Dữ liệu được truyền **không mã hóa** và **không có đăng nhập**: ai biết IP và port đều gửi, xem và tải file được, ai bắt gói tin cũng đọc được nội dung. Chỉ dùng trong mạng tin cậy cho mục đích học tập.
