# SimRacing — Unity

Mở `Chay_SimRacing_Unity.bat` để chơi, hoặc `Mo_Du_An_Unity.bat` để sửa dự án bằng Unity (phiên bản ghi trong `ProjectSettings/ProjectVersion.txt`, hiện là 6000.6.5f1). Đây là dự án C# / URP riêng; bản chạy không cần Godot.

Firmware buồng lái (ESP32-S3 và màn hình cảm ứng) nằm trong `Hardware/firmware-Arduino IDE`; game không thay đổi giao thức UDP của firmware.

## Hai chế độ điều khiển

Chọn ở hàng **ĐIỀU KHIỂN** trên màn hình chính. Lựa chọn được lưu lại cho lần chơi sau và đổi được ngay khi đang lái bằng **F10** hoặc nút *Điều khiển* trong màn hình tạm dừng (Esc).

| | Bàn phím | Buồng lái thật (ESP32) |
| --- | --- | --- |
| Lái | A / D (hoặc ← →) | Vô lăng encoder |
| Ga / phanh | W / S (S giữ khi đứng yên = lùi), Space = phanh dần | Bàn đạp ga, phanh (côn không dùng, trừ khi bật *Dùng chân côn*) |
| Số | Tự động 1–5, lùi tự động | Cần số 1–5 = D (tự động lên xuống số), R = lùi, phím N = số N |
| Đèn | H (tắt → cốt → pha) | Công tắc trên buồng lái |
| Đề máy | Tự nổ máy | Khóa điện + nút đề |
| Phản hồi lực | Không | Có (gửi về ESP32) |

Ở chế độ bàn phím, game bỏ qua hoàn toàn tín hiệu từ buồng lái và không gửi lực phản hồi. Ở chế độ buồng lái, bàn phím chỉ dùng cho camera, tạm dừng, cứu hộ và F9/F10.

### Khởi hành với buồng lái thật

**Hướng dẫn đầy đủ (mạng, nạp firmware, hiệu chỉnh vô lăng/bàn đạp/cần số, xử lý lỗi): [HUONG_DAN_BUONG_LAI.md](HUONG_DAN_BUONG_LAI.md).**

1. Lần đầu trên mỗi máy: chạy `Thiet_Lap_Mang_Buong_Lai.bat` (quyền Admin) để mở tường lửa UDP 9999 và đặt IP 192.168.1.100 cho cổng LAN, rồi vào **THIẾT LẬP** hiệu chỉnh vô lăng, bàn đạp và học cần số.
2. Chọn **Buồng lái thật (ESP32)**, chọn xe, cung đường, thời tiết rồi bắt đầu. HUD báo *CHƯA KẾT NỐI BUỒNG LÁI* nếu chưa nhận được dữ liệu, kèm nguyên nhân.
3. Bật khóa điện, giữ phanh, nhả ga, rồi nhấn **nút ĐỀ** trên buồng lái (hoặc **F9**) để nhận xe và nổ máy. Nếu chưa được, HUD ghi rõ còn thiếu gì.
4. Gạt số 1 (firmware fix5 tự giữ số khi buông cần), nhả phanh, đạp ga: xe chạy như **số tự động**, không cần côn. Phím **N** bật/tắt số N khi đứng chờ. Muốn lái số sàn có côn: THIẾT LẬP → bật *Dùng chân côn*. Khi bật *Côn thực tế*, sang số phải đạp côn và xe có thể chết máy như xe số sàn thật.

Mất gói tin quá 250 ms, mất tiêu điểm cửa sổ, tạm dừng hoặc đổi chế độ điều khiển đều khóa điều khiển và ngắt phản hồi lực. Khi kết nối lại, làm lại bước 3. Game không tự tiếp tục ga cũ. Cần số chạm hai công tắc cùng lúc được coi là N.

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
| N | Buồng lái: bật/tắt số N (cần số tự giữ số không có vị trí N) |
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

Chạy bản Windows với `--verify` (tùy chọn `--rig-test-port=19999`) để game tự kiểm tra bằng buồng lái giả lập trên loopback: chế độ điều khiển, thời tiết ngẫu nhiên, lưới xuất phát và xe AI, kết quả cuộc đua, career, nhiên liệu và va chạm. Chế độ này không lưu tiến trình người chơi và từ chối cổng thật 9999. Báo cáo (`test-log.txt`, `runtime-report.txt`) và ảnh xuất vào thư mục `Verification` cạnh `SimRacing.exe`. Xem `KIEM_NGHIEM.md` để kiểm nghiệm bằng tay.
