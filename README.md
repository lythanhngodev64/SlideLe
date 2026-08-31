# SlideLe

Ứng dụng Windows Forms để lấy danh sách các tệp PowerPoint (`.pptx`) từ một thư mục công khai trên GitHub và tải từng tệp về máy.

## Chạy ứng dụng

Yêu cầu .NET 8 Desktop Runtime hoặc SDK .NET 8.

```powershell
dotnet build SlideLe.sln
dotnet run --project src\SlideLe\SlideLe.csproj
```

## Cách dùng

1. Mở ứng dụng. URL mặc định là thư mục `Slide` của kho này.
2. Theo mặc định, ứng dụng chỉ quét các tệp `.pptx` ngay trong thư mục của URL. Chọn **Slide khác** nếu muốn quét thêm các thư mục con.
3. Nhấn **Quét tài liệu**.
4. Gõ vào ô **Tìm kiếm** để lọc theo tên, đường dẫn hoặc dung lượng của slide.
5. Chọn **Tải về** để chọn nơi lưu trên máy, hoặc **Mở ngay** để tải tạm và mở bằng ứng dụng PowerPoint mặc định của Windows.

URL phải là liên kết thư mục GitHub công khai theo dạng:

```text
https://github.com/chutai/kho/tree/nhanh/thu-muc
```

Tên nhánh có dấu `/` cần được mã hóa thành `%2F` trong URL.

Danh sách sử dụng GitHub REST API, do đó không phụ thuộc vào bố cục trang web GitHub. GitHub có thể giới hạn số lần gọi API ẩn danh trong một khoảng thời gian; khi đó ứng dụng sẽ hiện thông báo để thử lại sau.
