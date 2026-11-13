# Vận hành buồng lái thật (ESP32-S3)

Tài liệu này dành cho người lái bằng buồng lái in 3D. Nó đi theo đúng đường tín hiệu, từ phần cứng đến game:

```
Bàn đạp (3 biến trở) ─┐
Encoder vô lăng ──────┤
Cần số H (6 công tắc) ─ MCP23017 ─┤  ESP32-S3 ── W5500 ── cáp LAN ── máy tính (game, UDP 9999)
Khóa điện / Đề / Đèn / Còi / Xi nhan ─┘
```

Có hai phần phải nạp hoặc cài: **firmware** `Hardware/firmware-Arduino IDE/ControlBoxS3` cho ESP32-S3, và **game** trong `SimRacing-Windows`.

## 1. Bàn đạp và cần số dùng để làm gì

### Ba bàn đạp: côn · phanh · ga (xe số sàn)

| Bàn đạp | Biến trở | Chân ESP32-S3 | Trong game |
|---|---|---|---|
| Côn (trái) | PR1 | GPIO1 | Cắt truyền động giữa máy và bánh. Đạp hết = máy tách khỏi bánh; nhả từ từ = xe bắt đầu chạy |
| Phanh (giữa) | PR2 | GPIO2 | Phanh 4 bánh |
| Ga (phải) | PR3 | GPIO3 | Tăng vòng tua và lực kéo |

### Cần số: 6 công tắc = 6 vị trí số của hộp số sàn chữ H

Cần số trong bản vẽ 3D (chi tiết `HVitesV4`, tiếng Thổ Nhĩ Kỳ nghĩa là "cần số chữ H") là **cần số sàn kiểu H**, giống xe số tay thật. Mỗi vị trí số đẩy cần vào đúng một công tắc hành trình. Ba công tắc ở hàng trước là số 1 · 3 · 5, ba công tắc ở hàng sau là số 2 · 4 · R:

```
   1   3   5        ← cần đẩy về phía trước: chạm U của số 1 / 3 / 5
   |   |   |
   +---+---+        ← ở giữa, không chạm công tắc nào = số N (mo)
   |   |   |
   2   4   R        ← cần kéo về phía sau: chạm U của số 2 / 4 / lùi
```

- **Không chạm công tắc nào = N.** Đây là vị trí bắt buộc khi nổ máy và khi nhận xe.
- Công tắc U1…U6 nối vào GPA0…GPA5 của MCP23017. Firmware gửi "đang chạm công tắc số mấy" (1…6). Game dịch số đó thành số xe qua **bảng cần số**. Nhờ vậy, đấu dây theo thứ tự nào cũng được: chỉ cần chạy **Học cần số** một lần (mục 4.3).
- Nếu hai công tắc cùng chạm (cần nằm lệch giữa hai rãnh, hoặc công tắc bị dội), game coi là N và báo *lỗi 2 công tắc*.

Khi bật tùy chọn **Côn thực tế** (mặc định), game xử lý như xe số sàn thật:

- Máy đang nổ mà gạt vào số **không đạp côn**: số không vào, xe vẫn ở N và HUD báo *KHÔNG VÀO ĐƯỢC SỐ*.
- Vào số lùi khi xe còn đang chạy tới: không vào.
- Nhả côn ở số 1 hoặc R, không ga, không phanh: xe bò chậm như xe thật ở vòng tua không tải.
- Đang có số mà phanh dừng hẳn xe nhưng không đạp côn: **chết máy**. Muốn đề lại thì đạp côn hoặc về N rồi nhấn nút ĐỀ.
- Đề máy khi đang có số thì phải đạp côn.

Tắt **Côn thực tế** trong THIẾT LẬP nếu muốn lái dễ hơn (khi đó vào số không cần côn và không chết máy).

## 2. Chuẩn bị máy tính (làm một lần)

1. Nối **cáp LAN** từ module W5500 của control box vào cổng LAN của máy tính. Cắm thẳng hoặc qua switch đều được.
2. Cấp nguồn 12 V cho control box. Đèn cổng LAN phải sáng hoặc nháy.
3. Trong thư mục game, chuột phải **`Thiet_Lap_Mang_Buong_Lai.bat`** → chạy và bấm *Yes* khi Windows hỏi quyền Admin. Chọn **[1] Thiết lập đầy đủ**:
   - Script mở cổng **UDP 9999** trong tường lửa cho mọi loại mạng. Cáp nối thẳng tới control box là "mạng không xác định" (Public), loại mạng Windows chặn mặc định.
   - Script xóa luật **chặn** SimRacing.exe mà Windows tự tạo nếu lần đầu chạy game bạn đã bấm *Cancel* ở hộp thoại tường lửa.
   - Script đặt IP tĩnh **192.168.1.100/24** cho card LAN bạn chọn, rồi ping thử control box `192.168.1.150`.
4. Khi không lái nữa và cần card LAN vào Internet qua router, chạy lại script và chọn **[3]** để trả card về IP tự động.

> Firmware mới (`[RIG fix2]`) gửi dữ liệu dạng broadcast cho tới khi game trả lời, nên game nhận được dù IP máy tính không chính xác là `.100`. Tường lửa thì vẫn bắt buộc phải mở. Firmware cũ chỉ gửi tới đúng `192.168.1.100`.

## 3. Nạp firmware ESP32-S3

1. Mở `Hardware/firmware-Arduino IDE/ControlBoxS3/ControlBoxS3.ino` bằng Arduino IDE (core **esp32 3.3.x**). Cài thư viện **Ethernet** (Arduino).
2. Board **ESP32S3 Dev Module**, chọn đúng cổng COM, rồi Upload.
3. Mở **Serial Monitor 115200** (cổng USB-UART CH343). Sau khi khởi động sẽ thấy `[RIG fix2] ...` và mỗi giây một dòng trạng thái:

```
[RIG] ms=… chip=1 ver=04 i2c=01 link=1 udp=1 mcp=1 tx=… fail=0 dst=game game=1 enc=… adc=…/…/… out=…/…/… gear=0 sw=0040 btn=001
```

| Trường | Ý nghĩa | Bình thường |
|---|---|---|
| `ver=04`, `chip=1` | W5500 trả lời qua SPI | `04` / `1` |
| `link=1` | Có tín hiệu cáp LAN | `1` |
| `mcp=1`, `i2c=01` | MCP23017 (cần số, nút) trả lời ở địa chỉ 0x20 | `1` |
| `dst=bcast` / `dst=game` | Đang gửi broadcast / đã thấy game và gửi thẳng tới game | `game` khi game đang mở |
| `game=1` | Đang nhận phản hồi từ game | `1` khi game đang mở |
| `adc=a/b/c` | Giá trị thô ga/phanh/côn (0…4095) | Thay đổi khi đạp |
| `gear` | Công tắc cần số đang chạm (0 = không chạm) | 1…6 khi vào số |
| `sw` | Bit thô của 16 chân MCP23017 | |

Firmware mới tự dò lại MCP23017 mỗi giây nếu mất kết nối. Nó cũng thử đảo SDA/SCL và in cảnh báo nếu dây I2C bị cắm ngược. Một lần đọc I2C lỗi không còn làm mất cần số nữa.

## 4. Hiệu chỉnh trong game (THIẾT LẬP)

Mở game → **THIẾT LẬP**. Cột phải là khu **BUỒNG LÁI**. Dòng đầu phải ghi *ĐÃ KẾT NỐI · … gói/giây từ 192.168.1.150*. Nếu chưa kết nối, xem mục 6. Kết quả hiệu chỉnh tự lưu trên từng máy.

### 4.1 Vô lăng
1. Để vô lăng **thẳng** → bấm **B1: Vô lăng thẳng → Đặt giữa**.
2. Quay vô lăng **hết cỡ sang PHẢI** → bấm **B2: Quay hết PHẢI → Ghi**. Game tự ghi biên độ và tự đảo chiều nếu encoder đếm ngược.
3. Quay thử: dòng *Encoder … → lái …%* phải ra **+** khi quay phải và **−** khi quay trái.

Mặc định là ±3000 ticks (±450°, tức 900° từ hết trái tới hết phải), ứng với encoder 600 xung × 4. Thanh trượt dùng để chỉnh tay nếu muốn.

### 4.2 Bàn đạp
1. Bấm **Bắt đầu hiệu chỉnh**.
2. Đạp **hết** từng bàn đạp 2–3 lần (ga, phanh, côn). Dòng *Hành trình đã ghi* phải vượt 300 cho cả ba.
3. **Nhả hết** cả ba bàn đạp → bấm **Xong (đã nhả hết)**.

Sau đó dòng *GA / PHANH / CÔN* phải về **0%** khi nhả và lên **100%** khi đạp hết. Biến trở đấu ngược chiều hoặc chỉ xoay được một phần hành trình vẫn hiệu chỉnh được.

### 4.3 Cần số
1. Bấm **Học cần số**.
2. Làm theo chữ trên màn hình: gạt cần lần lượt vào **1, 2, 3, 4, 5, R**, giữ yên mỗi số khoảng nửa giây.
3. Game lưu bảng, ví dụ `U1→1  U2→2 … U6→R`. Dòng *Cần số: công tắc U… → số …* cho thấy kết quả ngay.

**Mặc định** trả về U1…U5 = số 1…5, U6 = R.

## 5. Lái

1. Màn hình chính → **Điều khiển: Buồng lái thật (ESP32)** (hoặc F10 khi đang lái) → chọn xe, đường, chế độ → BẮT ĐẦU.
2. HUD ghi **BUỒNG LÁI ĐANG KHÓA** cùng việc còn thiếu, ví dụ *về số N · đạp phanh quá nửa (đang 12%)*.
3. Nhận xe: **bật khóa điện** → **đạp phanh** → **cần số về N** → **nhả ga** → nhấn **nút ĐỀ** trên buồng lái (hoặc F9 trên bàn phím). Xe mở khóa, đếm ngược và nổ máy.
4. Khởi hành: đạp hết **côn** → vào **số 1** → nhả côn từ từ đồng thời thêm **ga**.
5. Lên hoặc xuống số: nhả ga → đạp côn → gạt số → nhả côn.
6. Dừng: đạp côn + phanh → về **N**.
7. Lùi: dừng hẳn → đạp côn → vào **R** → nhả côn.

Dưới HUD có dòng trực tiếp: *CẦN SỐ · GA · PHANH · CÔN · LÁI · KHÓA ĐIỆN*.

Game tự khóa lại (phải nhận xe lại ở bước 3) khi: mất tín hiệu quá 0,25 giây, cửa sổ game mất tiêu điểm (bấm chuột vào game), tạm dừng, cứu hộ, đổi chế độ điều khiển.

Các công tắc khác: **Khóa điện** (GPA6) tắt thì máy tắt. **Đèn** (GPB1/GPB2) có 3 vị trí tắt / cốt / pha. **Còi** (GPB5). **Xi nhan** (GPB6/GPB7). **Hazard** (GPB0). **Joystick** để nhìn quanh, nhấn joystick để đổi góc nhìn.

## 6. Không điều khiển được? Kiểm tra theo thứ tự

| Hiện tượng | Nguyên nhân / cách sửa |
|---|---|
| HUD: *Cổng LAN của máy tính chưa có IP 192.168.1.100* | Chạy `Thiet_Lap_Mang_Buong_Lai.bat` → [1] |
| HUD: *Không nhận được dữ liệu ESP32 (UDP 9999)* | Serial Monitor: `link=0` thì kiểm tra cáp hoặc nguồn. `ver` khác `04` thì kiểm tra dây SPI của W5500. `fail` tăng thì sai IP. Nếu `tx` tăng mà game vẫn không nhận: tường lửa chưa mở, chạy script [2] |
| *Cổng UDP 9999 đang bị chương trình khác chiếm* | Game mở 2 lần, hoặc còn chạy phần mềm cũ (Node/Godot) dùng cổng 9999. Tắt chúng đi |
| Đã kết nối nhưng **ĐANG KHÓA** mãi | Đọc dòng lý do trên HUD. Thường là phanh chưa tới 50% hoặc ga không về 0% vì bàn đạp chưa hiệu chỉnh: làm mục 4.2 |
| Quay vô lăng phải mà xe rẽ trái, hoặc lái quá nhẹ | Làm mục 4.1 |
| Gạt số mà game ra số khác | Làm mục 4.3 |
| Gạt số mà game luôn N | Chưa đạp côn (Côn thực tế), hoặc `mcp=0` trên Serial Monitor (MCP23017 chưa trả lời: kiểm tra 3V3, GND, SDA=GPIO18, SCL=GPIO8, RESET# kéo lên 3V3) |
| Nhấn ĐỀ mà máy không nổ | Khóa điện đang TẮT, hoặc đang có số mà chưa đạp côn, hoặc hết xăng |
| `mcp=0` mà vẫn chạy được | Đó là **chế độ dự phòng**: khóa điện luôn bật, đẩy joystick lên/xuống để đổi số, nhấn joystick để đề. Sửa dây I2C để dùng cần số thật |
