# SimRacing — Unity

Mở `Chay_SimRacing_Unity.bat` để chơi, hoặc `Mo_Du_An_Unity.bat` để sửa dự án bằng Unity (phiên bản ghi trong `ProjectSettings/ProjectVersion.txt`, hiện là 6000.6.5f1). Đây là dự án C# / URP riêng; bản chạy không cần Godot.

Firmware buồng lái (ESP32-S3 và màn hình cảm ứng) nằm trong `Hardware/firmware-Arduino IDE`; game không thay đổi giao thức UDP của firmware.

## Hai chế độ điều khiển

Chọn ở hàng **ĐIỀU KHIỂN** trên màn hình chính. Lựa chọn được lưu lại cho lần chơi sau và đổi được ngay khi đang lái bằng **F10** hoặc nút *Điều khiển* trong màn hình tạm dừng (Esc).

| | Bàn phím | Buồng lái thật (ESP32) |
| --- | --- | --- |
| Lái | A / D (hoặc ← →) | Vô lăng encoder |
| Ga / phanh | W / S (S giữ khi đứng yên = lùi), Space = phanh dần | Bàn đạp ga, phanh, côn |
| Số | Tự động 1–5, lùi tự động | Cần số 1–5, R, N (số tay, có côn) |
| Đèn | H (tắt → cốt → pha) | Công tắc trên buồng lái |
| Đề máy | Tự nổ máy | Khóa điện + nút đề |
| Phản hồi lực | Không | Có (gửi về ESP32) |

Ở chế độ bàn phím, game bỏ qua hoàn toàn tín hiệu từ buồng lái và không gửi lực phản hồi. Ở chế độ buồng lái, bàn phím chỉ dùng cho camera, tạm dừng, cứu hộ và F9/F10.

### Khởi hành với buồng lái thật

1. Chọn **Buồng lái thật (ESP32)**, chọn xe, cung đường, thời tiết rồi bắt đầu.
2. Buồng lái gửi UDP đến cổng **9999** của máy. HUD báo *CHƯA KẾT NỐI BUỒNG LÁI* nếu chưa nhận được dữ liệu.
3. Giữ phanh, về N, nhả ga và thả nút đề, rồi nhấn **F9** để tiếp quản. Đợi đếm ngược.
4. Bật khóa, đề máy. Đạp côn, vào số 1, thêm ga và nhả côn từ từ.

Mất gói tin quá 250 ms, mất tiêu điểm cửa sổ, tạm dừng, đổi chế độ điều khiển hoặc lỗi cần số đều khóa điều khiển và ngắt phản hồi lực. Khi kết nối lại, làm lại bước 3. Game không tự tiếp tục ga cũ.

## Các chế độ chơi

- **Lái tự do:** giao thông hai chiều, giới hạn tốc độ, nhiên liệu và cứu hộ.
- **Đua tính giờ:** ba vòng một mình trên tuyến đóng, đồng hồ vòng và ghost thành tích tốt nhất.
- **Đua tranh hạng:** xuất phát từ lưới cùng 5 tay đua AI (bạn ở vị trí thứ 4). Chọn độ khó AI (Dễ / Vừa / Khó) và số vòng (1 / 3 / 5). Về đích trước để thắng. Xe AI dùng cùng mẫu xe và cùng thông số động cơ, hộp số với xe của bạn (mỗi xe một màu sơn), giảm tốc theo độ cong khúc cua, vượt và bám đuôi. HUD hiển thị bảng xếp hạng trực tiếp, khoảng cách tới từng đối thủ và vị trí các xe trên minimap. Kết quả: thắng 3 sao, podium 2 sao, về đích 1 sao.
- **Career:** năm bài từ khởi hành số tay đến time trial. Đạt bảy sao để mở Bugatti.

## Thời tiết

Mặc định **Ngẫu nhiên**: mỗi lượt chơi bốc thăm Nắng (45%), Mưa (22%), Đêm (20%) hoặc Sương mù (13%). HUD ghi thời tiết đang dùng kèm "(ngẫu nhiên)". Vẫn có thể chọn cố định một loại. Mưa và sương mù giảm độ bám cho cả xe của bạn lẫn xe AI. Khi lái bằng bàn phím, đèn tự bật nếu trời tối. Game không mô phỏng gạt mưa; nếu firmware buồng lái vẫn gửi trạng thái công tắc gạt mưa thì game bỏ qua.

## Phím tiện ích

| Phím | Thao tác |
| --- | --- |
| F1 / F2 / V | Buồng lái / bám đuôi / đổi góc nhìn |
| F9 | Mở khóa buồng lái khi thỏa điều kiện tiếp quản |
| F10 | Đổi giữa bàn phím và buồng lái thật |
| Esc | Tạm dừng / tiếp tục |
| Backspace | Cứu hộ khi đã dừng xe |
| R | Bật / tắt tiếp nhiên liệu khi dừng tại trạm xăng |
| Enter / W | Bỏ qua đếm ngược (chế độ bàn phím) |

Cứu hộ sửa xe và cấp lượng xăng tối thiểu, đồng thời làm mất điều kiện hoàn thành sạch của lượt Career hiện tại.

## Hình ảnh và dữ liệu

Mặc định **Cân bằng**, mục tiêu 60 FPS, vật lý 120 Hz. Có mức Nhẹ / Cao và thanh điều chỉnh chuyển động đầu.

Hồ sơ, cấu hình buồng lái, chế độ điều khiển và ghost nằm trong thư mục `UnitySaveV1` dưới vùng dữ liệu ứng dụng SimRacing Unity của Windows. Tệp lưu có bản dự phòng.

Mã vận hành nằm trong `Assets/SimRacing/Scripts`, công cụ build trong `Assets/SimRacing/Editor/BuildProject.cs`. Trường đua oval dùng các prefab của `Assets/CartoonTracksPack1` (được tham chiếu từ `Assets/Resources/Worlds/race_circuit.prefab`); chế độ đua tranh hạng nằm trong `Assets/SimRacing/Scripts/World/RaceController.cs`.

## Kiểm tra

Chạy bản Windows với `--verify` (tùy chọn `--rig-test-port=19999`) để game tự kiểm tra bằng buồng lái giả lập trên loopback: chế độ điều khiển, thời tiết ngẫu nhiên, lưới xuất phát và xe AI, kết quả cuộc đua, career, nhiên liệu và va chạm. Chế độ này không lưu tiến trình người chơi và từ chối cổng thật 9999. Báo cáo (`test-log.txt`, `runtime-report.txt`) và ảnh xuất vào `Builds/Windows/Verification`. Xem `KIEM_NGHIEM.md` để kiểm nghiệm bằng tay.
