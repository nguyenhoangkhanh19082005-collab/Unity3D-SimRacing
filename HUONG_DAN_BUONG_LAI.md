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

### Ba bàn đạp: côn · phanh · ga

> **Mặc định game lái kiểu SỐ TỰ ĐỘNG**: chân côn bị bỏ qua hoàn toàn. Trên buồng lái hiện tại chân côn đã gắn chết bằng keo AB, nên không cần làm gì với nó, kể cả khi biến trở côn đang báo một giá trị cố định. Chỉ khi bật **Dùng chân côn** trong THIẾT LẬP thì côn mới có tác dụng (lái số sàn).

| Bàn đạp | Biến trở | Chân ESP32-S3 | Trong game |
|---|---|---|---|
| Côn (trái) | PR1 | GPIO1 | Số tự động: **không dùng**. Số sàn: cắt truyền động giữa máy và bánh |
| Phanh (giữa) | PR2 | GPIO2 | Phanh 4 bánh |
| Ga (phải) | PR3 | GPIO3 | Tăng vòng tua và lực kéo |

### Cần số: 6 công tắc = 6 vị trí số của hộp số sàn chữ H

Cần số trong bản vẽ 3D (chi tiết `HVitesV4`, tiếng Thổ Nhĩ Kỳ nghĩa là "cần số chữ H") là **cần số sàn kiểu H**, giống xe số tay thật. Mỗi vị trí số đẩy cần vào đúng một công tắc hành trình. Ba công tắc ở hàng trước là số 1 · 3 · 5, ba công tắc ở hàng sau là số 2 · 4 · R:

```
   1   3   5        ← cần đẩy về phía trước: chạm U của số 1 / 3 / 5
   |   |   |
   +---+---+        ← ở giữa: không chạm công tắc nào
   |   |   |
   2   4   R        ← cần kéo về phía sau: chạm U của số 2 / 4 / lùi
```

- **Công tắc hành trình chỉ bị nhấn lúc cần đang đè vào rãnh số**; buông tay là công tắc nhả ra. Vì vậy firmware **fix4 tự giữ số**: số vừa gạt được giữ nguyên tới khi gạt sang số khác, kể cả khi cần nhả khỏi công tắc. (Firmware cũ báo N ngay khi nhả công tắc, nên vào số 1 rồi buông tay là game về N và xe không đi.)
- Vì cần số không có công tắc cho N, sau lần gạt đầu tiên firmware không bao giờ báo N nữa (chỉ khi vừa bật nguồn). Muốn về N: nhấn **phím N** trên bàn phím (nhấn lại để ra khỏi N).
- Công tắc U1…U6 nối vào GPA0…GPA5 của MCP23017. Bảng `GEAR_PIN` trong `Config.h` ghi số nào chạm chân GPA nào. Trên buồng lái hiện tại đã đo được: số 1 = GPA0, số 2 = GPA3, số 3 = GPA1, số 4 = GPA4, số 5 = GPA2, R = GPA5. Firmware gửi thẳng số 1…5 / 6 = R cho game.
- Game còn có **bảng cần số** riêng (mục 4.3). Nếu đã sửa `GEAR_PIN` trong firmware thì để bảng của game ở **Mặc định**, đừng ánh xạ hai lần.
- Nếu hai công tắc cùng chạm (cần nằm lệch giữa hai rãnh, hoặc công tắc bị dội), game coi là N và báo *lỗi 2 công tắc*; firmware vẫn giữ số cũ.

### Số tự động (mặc định)

- Cần số ở **1, 2, 3, 4 hoặc 5** đều là **D**: hộp số tự lên xuống số theo tốc độ. HUD ghi `D1`, `D2`…
- **R** = lùi, chỉ vào khi xe đã dừng hẳn. Đang lùi muốn đi tới thì dừng hẳn rồi gạt về số tiến.
- **N**: lúc vừa bật nguồn (chưa gạt số nào), hoặc nhấn **phím N** (nhấn lần nữa hoặc gạt số khác để ra khỏi N).
- Không chết máy, không cần côn khi đề máy, vào số hay nhận xe.
- Nhả phanh ở D hoặc R mà không đạp ga: xe bò chậm như xe số tự động thật.

### Số sàn (bật **Dùng chân côn** trong THIẾT LẬP)

Chỉ dùng khi chân côn hoạt động được. Game xử lý như xe số sàn thật:

- Máy đang nổ mà gạt vào số **không đạp côn**: số không vào, xe vẫn ở N và HUD báo *KHÔNG VÀO ĐƯỢC SỐ*.
- Vào số lùi khi xe còn đang chạy tới: không vào.
- Nhả côn ở số 1 hoặc R, không ga, không phanh: xe bò chậm như xe thật ở vòng tua không tải.
- Đang có số mà phanh dừng hẳn xe nhưng không đạp côn: **chết máy**. Muốn đề lại thì đạp hết côn (hoặc phím N) rồi nhấn nút ĐỀ.
- Đề máy khi đang có số thì phải đạp côn.

Ở số sàn, game nhận xe khi đạp hết côn (không bắt buộc N), và phím N cũng hết tác dụng khi bạn nhả rồi đạp côn lại.

## 2. Chuẩn bị máy tính (làm một lần)

1. Nối **cáp LAN** từ module W5500 của control box vào cổng LAN của máy tính. Cắm thẳng hoặc qua switch đều được.
2. Cấp nguồn 12 V cho control box. Đèn cổng LAN phải sáng hoặc nháy.
3. Trong thư mục game, chuột phải **`Thiet_Lap_Mang_Buong_Lai.bat`** → chạy và bấm *Yes* khi Windows hỏi quyền Admin. Chọn **[1] Thiết lập đầy đủ**:
   - Script mở cổng **UDP 9999** trong tường lửa cho mọi loại mạng. Cáp nối thẳng tới control box là "mạng không xác định" (Public), loại mạng Windows chặn mặc định.
   - Script xóa luật **chặn** SimRacing.exe mà Windows tự tạo nếu lần đầu chạy game bạn đã bấm *Cancel* ở hộp thoại tường lửa.
   - Script đặt IP tĩnh **192.168.1.100/24** cho card LAN bạn chọn, rồi ping thử control box `192.168.1.150`.
4. Khi không lái nữa và cần card LAN vào Internet qua router, chạy lại script và chọn **[3]** để trả card về IP tự động.

> Firmware mới (`[RIG fix2]` trở lên) gửi dữ liệu dạng broadcast cho tới khi game trả lời, nên game nhận được dù IP máy tính không chính xác là `.100`. Tường lửa thì vẫn bắt buộc phải mở. Firmware cũ chỉ gửi tới đúng `192.168.1.100`.

## 3. Nạp firmware ESP32-S3

1. Mở `Hardware/firmware-Arduino IDE/ControlBoxS3/ControlBoxS3.ino` bằng Arduino IDE (core **esp32 3.3.x**). Cài thư viện **Ethernet** (Arduino).
2. Board **ESP32S3 Dev Module**, chọn đúng cổng COM, rồi Upload.
3. Mở **Serial Monitor 115200** (cổng USB-UART CH343). Sau khi khởi động sẽ thấy `[RIG fix5] ...` và mỗi giây một dòng trạng thái:

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
| `gear` | Số đang giữ (fix4: giữ cả khi đã nhả công tắc; 0 = chưa gạt số nào từ lúc bật nguồn) | 1…6 sau khi gạt số |
| `sw` | Bit thô của 16 chân MCP23017 | |

Firmware mới tự dò lại MCP23017 mỗi giây nếu mất kết nối. Nó cũng thử đảo SDA/SCL và in cảnh báo nếu dây I2C bị cắm ngược. Lỗi đọc I2C dưới 5 lần liên tiếp không làm mất cần số, và số đang giữ không bị xóa khi MCP23017 khởi động lại. Fix5 lọc bàn đạp và joystick theo cách của nhóm: mỗi lần quét đọc 16 mẫu, bỏ mẫu lớn nhất và nhỏ nhất, làm mượt 1/8, và chỉ đổi giá trị khi lệch quá 2 đơn vị ADC (bàn đạp) hoặc 6 (joystick). Bàn đạp nhả hẳn ra đúng 0, đạp hết ra đúng 4095.

> **Không dùng bản firmware `ControlBoxS3_fixed` cũ của nhóm.** Ở bản đó, chỉ một lần đọc I2C lỗi là MCP23017 bị coi là mất vĩnh viễn và không bao giờ thử lại. Firmware chuyển sang chế độ dự phòng: cần số bị bỏ qua, số luôn là N, xe không chạy dù đã gạt số. Bản đó còn chỉ gửi tới đúng IP `192.168.1.100`. Fix5 trong repo đã gộp bộ lọc ADC của bản này nhưng giữ phần tự kết nối lại.

## 4. Hiệu chỉnh trong game (THIẾT LẬP)

Mở game → **THIẾT LẬP**. Cột phải là khu **BUỒNG LÁI**. Dòng đầu phải ghi *ĐÃ KẾT NỐI · … gói/giây từ 192.168.1.150*. Nếu chưa kết nối, xem mục 6. Kết quả hiệu chỉnh tự lưu trên từng máy.

### 4.1 Vô lăng
1. Để vô lăng **thẳng** → bấm **B1: Vô lăng thẳng → Đặt giữa**.
2. Quay vô lăng **hết cỡ sang PHẢI** → bấm **B2: Quay hết PHẢI → Ghi**. Game tự ghi biên độ và tự đảo chiều nếu encoder đếm ngược.
3. Quay thử: dòng *Encoder … → lái …%* phải ra **+** khi quay phải và **−** khi quay trái.

Mặc định là ±3000 ticks (±450°, tức 900° từ hết trái tới hết phải), ứng với encoder 600 xung × 4. Thanh trượt dùng để chỉnh tay nếu muốn.

### 4.2 Bàn đạp
Firmware fix3 đã quy đổi bàn đạp theo dải đo trong `Config.h` (`*_RELEASED` / `*_PRESSED`), nên game nhận 0…4095 cho cả ba. Trong game, chỉ cần hiệu chỉnh lại nếu % chưa về 0 khi nhả hoặc chưa lên 100% khi đạp hết. **Nếu trước đó đã hiệu chỉnh trong game bằng firmware cũ (giá trị thô), hãy làm lại bước này sau khi nạp fix3.**

1. Bấm **Bắt đầu hiệu chỉnh**.
2. Đạp **hết** ga và phanh 2–3 lần (thêm côn nếu bật *Dùng chân côn*). Dòng *Hành trình đã ghi* phải vượt 300.
3. **Nhả hết** các bàn đạp → bấm **Xong (đã nhả hết)**. Ở số tự động, côn không cần đạp và giữ nguyên hiệu chỉnh cũ.

Sau đó dòng *GA / PHANH / CÔN* phải về **0%** khi nhả và lên **100%** khi đạp hết. Biến trở đấu ngược chiều hoặc chỉ xoay được một phần hành trình vẫn hiệu chỉnh được.

### 4.3 Cần số
Với firmware fix3 (`GEAR_PIN` đã đúng) chỉ cần bấm **Mặc định**, rồi gạt thử: dòng *Cần số: công tắc U… → số …* phải ra đúng số đang gạt. Chỉ dùng **Học cần số** khi không muốn sửa firmware:

1. Bấm **Học cần số**.
2. Làm theo chữ trên màn hình: gạt cần lần lượt vào **1, 2, 3, 4, 5, R**, giữ yên mỗi số khoảng nửa giây.
3. Game lưu bảng, ví dụ `U1→1  U2→2 … U6→R`. Dòng *Cần số: công tắc U… → số …* cho thấy kết quả ngay.

**Mặc định** trả về U1…U5 = số 1…5, U6 = R.

## 5. Lái

1. Màn hình chính → **Điều khiển: Buồng lái thật (ESP32)** (hoặc F10 khi đang lái) → chọn xe, đường, chế độ → BẮT ĐẦU.
2. HUD ghi **BUỒNG LÁI ĐANG KHÓA** cùng việc còn thiếu, ví dụ *đạp phanh quá nửa (đang 12%)*.

**Số tự động (mặc định):**

3. Nhận xe: **bật khóa điện** → **nhả ga** → **đạp phanh** quá nửa. Game **tự nhận xe**, đếm ngược 3 giây rồi **tự nổ máy**. Không cần nút ĐỀ (tín hiệu ĐỀ từ control box hiện không lên được game) và không có phím F9. Cần số đang ở số nào cũng được. Nếu máy tắt khi đang lái, đạp phanh là máy nổ lại.
   - Máy chỉ chạy khi **khóa điện BẬT**. Dòng *KHÓA ĐIỆN* dưới HUD phải ghi BẬT. Nếu ghi TẮT dù đã bật công tắc, kiểm tra dây công tắc khóa điện (GPA6 của MCP23017).
   - Dòng chữ giữa HUD luôn ghi lý do xe chưa chạy được: *KHÓA ĐIỆN đang TẮT*, *MÁY CHƯA NỔ*, *Đang ở số N*…
4. Đi: gạt cần vào **số 1** (hoặc 2…5, đều là D; buông cần được) → nhả phanh → đạp **ga**. Hộp số tự lên số.
5. Dừng: nhả ga, đạp phanh. Không chết máy.
6. Lùi: dừng hẳn → gạt **R** → nhả phanh, đạp ga. Đi tới lại: dừng hẳn → gạt số 1.
7. Đứng chờ lâu: nhấn **phím N** (HUD ghi *CẦN SỐ N (phím N)*), nhấn lại là vào D.

**Số sàn (bật Dùng chân côn):** nhận xe bằng phanh + đạp hết côn + nút ĐỀ. Khởi hành: giữ côn, gạt số 1, nhả côn từ từ và thêm ga. Sang số: nhả ga, đạp côn, gạt số, nhả côn. Dừng: côn + phanh (hoặc phím N).

Dưới HUD có dòng trực tiếp: *CẦN SỐ · GA · PHANH · SỐ TỰ ĐỘNG (hoặc CÔN %) · LÁI · KHÓA ĐIỆN*.

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
| Vào số rồi buông tay là game về N, xe không đi | Firmware cũ không giữ số: nạp firmware **fix5** (Serial Monitor in `[RIG fix5]`) |
| Đạp phanh, vào số 1 mà xe không chạy | Đọc dòng chữ giữa HUD. *KHÓA ĐIỆN đang TẮT*: bật khóa điện (hoặc sửa dây công tắc GPA6). *MÁY CHƯA NỔ*: đạp phanh quá nửa. Vẫn *BUỒNG LÁI ĐANG KHÓA*: đọc lý do (thường là ga chưa về 0% hoặc phanh chưa quá 50%) |
| % ga / phanh nhảy liên tục | Biến trở ga trên buồng lái chỉ xoay được ~50 đơn vị ADC (Config.h: 4095→4045, lại nằm ở mức bão hòa 4095), nên mỗi đơn vị ≈ 2%. Game đã lọc thêm và coi dưới 3% là 0%, nhưng muốn hết hẳn phải chỉnh cơ khí cho biến trở xoay nhiều hơn (lý tưởng vài trăm đơn vị ADC) rồi đo lại `adc=` trên Serial Monitor và sửa `THROTTLE_RELEASED/PRESSED`. Nạp firmware fix5 (có bộ lọc) |
| Đã nổ máy, vào số mà xe không chạy | Xem dòng *CẦN SỐ* dưới HUD. Ghi N thì đọc dòng dưới. Có số mà vẫn không chạy: THIẾT LẬP phải để **tắt** *Dùng chân côn* (côn gắn chết sẽ luôn cắt truyền động), và kiểm tra *GA* có lên khi đạp |
| Gạt số mà game luôn N | Đang ở N do phím N (nhấn N lần nữa), chưa đạp côn khi bật *Dùng chân côn*, đang ở N do phím N (đạp côn lại hoặc gạt số khác), hoặc `mcp=0` trên Serial Monitor (MCP23017 chưa trả lời: kiểm tra 3V3, GND, SDA=GPIO18, SCL=GPIO8, RESET# kéo lên 3V3) |
| Nhấn ĐỀ mà máy không nổ | Khóa điện đang TẮT, hết xăng, hoặc (chỉ khi bật *Dùng chân côn*) đang có số mà chưa đạp côn |
| `mcp=0` trên Serial Monitor | MCP23017 không trả lời, cần số bị bỏ qua (số luôn N nếu chưa đẩy joystick). Kiểm tra dây I2C. Với firmware fix5 nó tự thử lại mỗi giây. Đây là **chế độ dự phòng**: khóa điện luôn bật, đẩy joystick lên/xuống để đổi số, nhấn joystick để đề. Sửa dây I2C để dùng cần số thật |
