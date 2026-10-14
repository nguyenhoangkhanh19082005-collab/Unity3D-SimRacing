# Bằng chứng kiểm tra bản bàn giao

Ngày đối chiếu: 14/10/2026. Unity 6000.3.26f1, URP 17.3.0, Windows x64.

## Bản Windows được giữ lại

- Chạy bằng `Chay_SimRacing_Unity.bat`; mã nguồn mở bằng `Mo_Du_An_Unity.bat`.
- `Logs/build-release.log`: build thành công, 22 kiểm tra giao thức; 360 raycast đường coastal_track và 480 raycast race_circuit đều đạt.
- `Logs/runtime-release.log` và `Builds/Windows/Verification/runtime-report.txt`: 33 kiểm tra, 0 lỗi.
- Bộ kiểm tra chạy GPU thật và gói UDP giả trên loopback 19999: ba xe, hai đường, tăng tốc/phanh/lùi, ngắt và nối lại rig, pause, phụ kiện, cứu hộ, nhiên liệu, hư hại, Career hoàn chỉnh, lưu/đọc/backup và dữ liệu ghost.
- Ảnh tại `Builds/Windows/Verification`: menu, cockpit, lái xe, các thời tiết, camera ngoài xe, xe Ferrari/Bugatti và kết quả Career. `02b-cockpit-clean.png` là ảnh cockpit không HUD.
- Trong lượt đo 1280×720 trên NVIDIA GeForce RTX 2050: median 16,67 ms/frame, p95 16,79 ms/frame, giới hạn 60 FPS. Đây là số đo các cảnh kiểm tra, không phải bảo đảm cho mọi máy hay mọi tình huống.

## Bản Godot được bảo toàn

Buồng lái đã cải thiện trước khi chuyển engine: dashboard, đồng hồ, vô lăng, cửa, gương 3D và ánh sáng. Có 11 ảnh cuối cùng trong `D:/doancodientu/reports/cockpit_unity_2026_10_13/final`.

Đối chiếu SHA-256 ngày bàn giao với `frozen-godot-sha256.json`: 111 tệp thuộc dự án Godot, không thay đổi sau mốc chốt. Unity được đặt riêng tại E:/doancodientuunity3D; không phụ thuộc Godot khi chạy.

## Giới hạn và chênh lệch đã biết

- Chưa nghiệm thu trên rig vật lý; thử giao thức bằng UDP loopback không chứng minh lực thực tế của động cơ/vô lăng.
- `Logs/build-final.log`: lần xuất bổ sung dừng trước biên dịch vì thiếu giấy phép (mã 198). Người dùng chọn giữ bản Windows hiện có.
- Mã nguồn mới hơn bản Windows đúng một sửa giao diện: dùng kiểu checkbox cho bốn tùy chọn đảo chiều. Bản Windows cũ vẫn điều khiển được các tùy chọn nhưng thiếu dấu chọn trực quan. Sửa nguồn này chưa được biên dịch hoặc kiểm tra trong game.
- Tiến trình Unity lưu riêng, không tự nhập save Godot. Hai bản đồ giữ cấu hình nguồn: không có giao lộ/đèn đỏ/cảnh sát truy đuổi hoạt động.

Sau khi giấy phép hoạt động, có thể xuất lại từ menu `SimRacing > Build Windows`; chạy bản mới với `--verify --rig-test-port=19999` để kiểm tra lại trong vùng lưu thử riêng.
