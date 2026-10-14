# SimRacing Unity3D

Dự án Unity 6000.3.26f1 / URP 17.3.0, Windows x64. Ba xe, hai cung đường, cockpit 3D, thời tiết, rig UDP và phản hồi lực. Bản Unity chạy độc lập với Godot.

## Mở dự án sau khi giải nén

Trong Unity Hub, chọn Add > Add project from disk và chọn thư mục chứa Assets, Packages, ProjectSettings. Cài đúng Unity 6000.3.26f1, kích hoạt giấy phép và mở dự án. Lần đầu Unity cần mạng để tải package và tạo lại Library. Mở Assets/Scenes/SimRacing.unity nếu cần; xem đường dẫn scene thực tế trong ProjectSettings/EditorBuildSettings.asset. Xuất game bằng SimRacing > Build Windows.

Không kèm Library, Temp, Logs, UserSettings, bộ cài Unity hoặc lịch sử Git. Các tệp .meta và GUID được giữ nguyên. Dữ liệu gốc để import lại có trong Assets/SourceData.

## Đưa mã nguồn lên GitHub

Dùng Git và Git LFS; không kéo toàn bộ dự án vào trang upload trên trình duyệt vì có tệp vượt giới hạn 100 MiB của Git thường. .gitattributes đã liệt kê những tệp lớn hơn 25 MiB. ZIP chứa dữ liệu thật, không phải các pointer LFS.

1. Giải nén ZIP nguồn. Mở terminal tại thư mục doancodientuunity3D, nơi có .gitignore và .gitattributes.
2. Cài Git và Git LFS từ nguồn chính thức nếu chưa có.
3. Tạo repository GitHub trống, không thêm README/license/gitignore tự động.
4. Chạy các lệnh sau; thay YOUR_ACCOUNT/YOUR_REPOSITORY bằng repository của bạn:

```powershell
git lfs install
git init
git branch -M main
git add .
git lfs ls-files
git status
git commit -m "Initial SimRacing Unity project"
git remote add origin https://github.com/YOUR_ACCOUNT/YOUR_REPOSITORY.git
git push -u origin main
```

Trước commit, git lfs ls-files phải liệt kê các tệp trong .gitattributes. Tài khoản/repository cần dung lượng Git LFS phù hợp. Khi clone ở máy khác, cài Git LFS và chạy git lfs pull trước khi mở Unity.

Không commit ZIP bản chơi vào repository. Tạo GitHub Release và đính kèm SimRacing-Unity-Windows.zip ở mục Assets; người nhận giải nén toàn bộ rồi chạy SimRacing.exe. Khi muốn đặt bản chơi cạnh mã nguồn, giải nén nội dung bản chơi vào Builds/Windows.

## Trạng thái bản bàn giao

Bản Windows kèm riêng đã đạt 33/33 kiểm tra runtime, 22 kiểm tra giao thức, 840 điểm kiểm tra mặt đường. Mã nguồn có thêm sửa kiểu checkbox ở bốn tùy chọn đảo chiều; sửa này chưa được build/kiểm tra vì lần xuất cuối thiếu giấy phép Unity. Người dùng đã chọn giữ bản Windows hiện có. Đợt đóng gói này không build lại hay thay đổi logic game.

Chưa nghiệm thu rig vật lý. Xem HUONG_DAN.md, MIGRATION_STATUS.md và VERIFICATION.md. Các đường dẫn ổ D/E trong báo cáo là đường dẫn lịch sử của máy phát triển, không cần tồn tại để mở dự án. Log/ảnh kiểm tra lịch sử không nằm trong ZIP nguồn.

Tài liệu GitHub: https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github
Git LFS: https://git-lfs.com/
