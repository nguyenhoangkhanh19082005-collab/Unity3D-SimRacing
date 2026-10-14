# Kết quả kiểm chứng — 21/09/2026

## Firmware Arduino

Build bằng Arduino CLI 1.2.0, ESP32 core 3.3.11, từ chính hai sketch được bàn giao:

| Sketch | FQBN | Flash chương trình | RAM tĩnh | Kết quả |
|---|---|---:|---:|---|
| ControlBoxS3 | esp32:esp32:esp32s3:CDCOnBoot=cdc | 369.921 byte | 25.544 byte | PASS |
| TouchscreenWroom | esp32:esp32:esp32 | 306.444 byte | 23.532 byte | PASS |

RAM màn hình ở bảng chưa bao gồm sprite cấp phát lúc chạy: 320×160×2 = 102.400 byte. Firmware kiểm tra cấp phát; nếu thất bại không phát heartbeat để ARM. `ENABLE_FFB=false` trong bản bàn giao.

Các log cuối nằm tại `build/firmware/control_compile.log` và `touch_compile.log`. Thư viện Ethernet 2.0.2, TFT_eSPI 2.5.43 và XPT2046_Touchscreen 1.4. Đã giữ nguyên TFT User_Setup của máy và lưu bản tham khảo trong config.

## Giao thức và logic

- C++ thực thi bằng MinGW: kiểm byte offset/28-byte UDP, encoder đủ một vòng quadrature = 4 tick và đảo chiều, chuyển trạng thái sai, cần số chồng tiếp điểm, debounce và millis wrap, CRC16 vector chuẩn, mọi bit bị đảo trong khung thử, khung thiếu byte, nối liên tiếp, đồng bộ lại và sequence wrap. PASS.
- Godot 4.3 `test_rig_protocol.gd`: UDP thật qua loopback, input/feedback đúng kích thước và đơn vị, bitmask, số lùi=6, reject length/magic/version/range, peer khác, timestamp lặp/cũ, timeout, reboot, rollover, nhiên liệu thật, cờ đèn, lực bằng 0 khi mất liên lạc. **errors=0**.
- `test_rig_accessories.gd`: đèn OFF/LOW/HIGH, còi nhấn/nhả, hai cần gạt, tốc độ nhanh gấp đôi chậm, về vị trí nghỉ khi OFF/mất rig. **errors=0**.
- `test_rig_engine.gd`: xe Ferrari thật của game; ga không tự đề khi kết nối rig, ignition+START đề máy, nhả START vẫn nổ, ignition OFF tắt, thêm xăng không tự đề, bàn phím giữ hành vi cũ. **errors=0**.
- Suite vật lý hiện có `test_physics_regressions.gd`: **COMPLETE errors=0**.

Giới hạn của test: Godot renderer dummy khi chạy headless phát thông báo `mesh_get_surface_count: Parameter m is null` lúc giải phóng mesh; test xe còn có cảnh báo tài nguyên khi thoát nhanh. Không có GDScript lỗi runtime hoặc assertion thất bại trong các lượt kiểm tra cuối. Đã chạy lại test phụ kiện bằng **OpenGL 3.3 / NVIDIA RTX A2000** trong tiến trình ẩn: **errors=0, stderr trống** (`accessories_opengl.log`, `accessories_opengl_errors.log`). Đây là kiểm tra hành vi/render API, chưa phải đánh giá bằng mắt bố cục cockpit trên mọi xe.

## Bản game phát hành

- `build/SimRacingRig/SimRacingRig.exe` và `.pck` đã xuất thành công; chạy bằng `Chay_Game_Voi_Phan_Cung.bat`.
- Gói PCK đã qua test UDP: `--main-pack build/SimRacingRig/SimRacingRig.pck --script <đường dẫn tuyệt đối tới test_rig_protocol.gd>`, **errors=0**. Test dùng file bên ngoài vì preset phát hành loại các test ra khỏi PCK.
- Log: `build/firmware/game_export.log`, `packed_game_test.log`.
- Quy trình tái lập: `tools/verify_rig.ps1` và `tools/build_rig_game.ps1`.

## Chưa kiểm chứng bằng phần cứng

Chưa nạp board, chưa đo mức điện áp/tín hiệu, chưa xác minh nhãn W5500/U9 hoặc chân UART trên PCB, chưa đo độ trễ thực, lực/dòng/nhiệt motor, chưa kiểm tra cảm ứng thực với firmware mới. ADC endpoints, chiều encoder, tỷ số truyền, hard limit và chiều motor phải hiệu chỉnh theo bộ cơ khí. Các timeout/khóa FFB mới được kiểm tra bằng mã và cấu trúc giao thức, chưa có phép thử rút dây vật lý hay rig HIL.

Phải đối chiếu các điểm cần sửa ở README (RESET MCP23017, tín hiệu encoder, nguồn/driver, khả năng xung đột UART43/44), thử đầu vào khi motor không cấp nguồn rồi mới cho phép FFB.

## Cập nhật 01/10/2026 — USB CDC tùy chọn

Đã bỏ lỗi biên dịch bắt buộc bật USB CDC. `Serial.begin` và log debug chỉ được biên dịch khi `ARDUINO_USB_CDC_ON_BOOT=1`; khi Disabled không mở UART0 debug, giữ GPIO43/44 cho UART màn hình. Không thay đổi giao thức, chân nối hoặc giới hạn motor.

Đã compile lại ESP32 core 3.3.11 với cả hai cấu hình:

| USB CDC On Boot | FQBN option | Flash | RAM tĩnh | Kết quả |
|---|---|---:|---:|---|
| Disabled | CDCOnBoot=default | 365.305 byte | 25.352 byte | PASS |
| Enabled | CDCOnBoot=cdc | 369.921 byte | 25.544 byte | PASS |

Log `build/firmware/control_cdc_off_compile.log` và `control_compile.log`. `tools/verify_rig.ps1` đã bổ sung kiểm tra CDC Disabled. Chưa upload thử trên board. Nếu USB–UART của board nối với 43/44, vẫn cần xử lý xung đột đường dây theo README; sửa firmware không tháo được kết nối điện đó.

## Cập nhật tiếp 01/10/2026 — chuyển UART màn hình sang GPIO17/40

Theo xác nhận board có chip USB–UART, cấu hình hiện tại là `DASH_TX=17`, `DASH_RX=40`. Màn hình giữ `UART_RX=27`, `UART_TX=22`. Nối S3 TX17 → màn hình RX27; màn hình TX22 → S3 RX40; GND chung. Các ghi chú 43/44 dành cho màn hình trong lịch sử kiểm chứng bên trên chỉ áp dụng bản cũ; hiện cặp 43/44 dành cho USB–UART.

Đã compile cấu hình mới với `esp32:esp32:esp32s3:CDCOnBoot=default` (USB CDC Disabled), core 3.3.11: PASS, flash 365.305 byte, RAM tĩnh 25.352 byte. Không thay đổi giao thức hoặc chức năng màn hình/game. Chỉ cần nạp lại S3 và chuyển dây; không cần nạp lại màn hình nếu đang chạy đúng RX27/TX22. Chưa thử trên board thật.

## Cập nhật 02/10/2026 — chọn cặp UART liền nhau GPIO39/40

Bạn xác nhận GPIO17 đã sử dụng. Đã bỏ phương án 17/40; cấu hình hiện tại là **S3 TX=GPIO39, RX=GPIO40**, nối tới màn hình RX27/TX22. Hai chân S3 nằm cạnh nhau theo sơ đồ và pinout DevKitC-1, chưa thấy nối ngoại vi khác trong sơ đồ/cấu hình Control Box. Điều kiện: không có dây ngoại vi bổ sung hoặc đầu dò JTAG ngoài trên cặp 39/40. GPIO39 của ESP32-S3 là I/O dùng được cho TX; không nhầm với GPIO39 input-only của ESP32-WROOM32 trên màn hình.

Compile cấu hình mới với core ESP32 3.3.11, `esp32:esp32:esp32s3:CDCOnBoot=default`: **PASS**, flash 365.305 byte, RAM tĩnh 25.352 byte. Đã đồng bộ bảng đấu dây và ghi chú ở hai sketch. Chưa xác nhận bằng nạp/đo trên board thật; lịch sử 17/40 ở các mục cũ đã được thay thế bằng cấu hình này.
