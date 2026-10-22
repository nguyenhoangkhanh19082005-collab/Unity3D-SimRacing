# SimRacing Unity3D

Game giả lập đua xe chơi bằng buồng lái thật (ESP32-S3, vô lăng, bàn đạp, cần số, phản hồi lực) hoặc bằng bàn phím. Có ba xe, hai cung đường (đèo tuyết và trường đua oval), các chế độ Lái tự do, Đua tính giờ, **Đua tranh hạng với 5 xe AI**, Career, cùng thời tiết ngẫu nhiên. Unity 6000.6.5f1 / URP, Windows x64.

## Chạy game ngay (không cần cài Unity)

Bản chơi đã build sẵn nằm trong thư mục `SimRacing-Windows/` và luôn được cập nhật cùng mã nguồn mới nhất trên nhánh `main`.

1. Cài [Git for Windows](https://git-scm.com/download/win). Bộ cài này đã kèm Git LFS. Nếu dùng bản Git khác, cài thêm [Git LFS](https://git-lfs.com/).
2. Clone repo:

   ```bash
   git clone https://github.com/nguyenhoangkhanh19082005-collab/Unity3D-SimRacing.git
   ```

3. Mở thư mục vừa clone và chạy **`Chay_SimRacing_Unity.bat`**, hoặc chạy trực tiếp `SimRacing-Windows\SimRacing.exe`.

Để cập nhật lên bản mới nhất sau này, chạy `git pull` trong thư mục repo rồi mở lại game.

> Không dùng nút **Code → Download ZIP** của GitHub: file ZIP đó chỉ chứa "con trỏ" Git LFS chứ không chứa game. Nếu `Chay_SimRacing_Unity.bat` báo *chưa được tải đầy đủ*, chạy `git lfs install` rồi `git lfs pull` trong thư mục repo.

Cách chơi, phím điều khiển và cách kết nối buồng lái: xem [HUONG_DAN.md](HUONG_DAN.md). Danh sách kiểm nghiệm: [KIEM_NGHIEM.md](KIEM_NGHIEM.md). Firmware buồng lái: `Hardware/firmware-Arduino IDE`.

## Dành cho người phát triển

- Mở dự án bằng Unity **6000.6.5f1** (Unity Hub → Add project from disk). Lần đầu Unity cần mạng để tải package và tạo lại `Library`.
- Mã game nằm ở `Assets/SimRacing/Scripts`, công cụ build ở `Assets/SimRacing/Editor/BuildProject.cs`.
- Build bằng menu **SimRacing → Build Windows**. Lệnh này build thẳng vào `SimRacing-Windows/`, kèm dữ liệu map, model xe và âm thanh mà game đọc từ đĩa. **Sau mỗi lần sửa code, hãy build rồi commit cả thư mục `SimRacing-Windows/`** để người clone luôn chạy đúng bản mới.
- Build không cần mở Editor:

  ```bash
  "C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe" -quit -batchmode -projectPath . -executeMethod SimRacing.Editor.BuildProject.Build -logFile build.log
  ```

- Kiểm tra tự động: `SimRacing-Windows\SimRacing.exe --verify`, kết quả ở `SimRacing-Windows/Verification` (không đưa lên Git).

## Git LFS

Repo dùng Git LFS cho các file lớn: texture psd/png của trường đua, FBX, tga, wav, bin, mesh đã nhập và toàn bộ `SimRacing-Windows/`. Danh sách luật nằm trong `.gitattributes`. Trước khi commit, `git lfs ls-files` phải liệt kê các file này. Tài khoản GitHub cần đủ hạn mức dung lượng và băng thông LFS.

Tài liệu GitHub về file lớn: https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github
