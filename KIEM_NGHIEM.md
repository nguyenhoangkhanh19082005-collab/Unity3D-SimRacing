# Kiểm nghiệm bản cập nhật: Đua tranh hạng · Thời tiết ngẫu nhiên · Hai chế độ điều khiển

Bản chạy: `SimRacing-Windows/SimRacing.exe` (hoặc `Chay_SimRacing_Unity.bat`). Mỗi mục bên dưới có thao tác và kết quả mong đợi.

## 1. Kiểm tra tự động

```bat
SimRacing-Windows\SimRacing.exe --verify
```

Game tự chạy khoảng 5 phút rồi tự thoát. Mở `SimRacing-Windows/Verification/test-log.txt`: mọi dòng phải là `[PASS]`, và `runtime-report.txt` phải ghi `Errors: 0`. Các bài liên quan bản cập nhật này:

| Bài kiểm tra | Ý nghĩa |
| --- | --- |
| `random weather covers every preset` | 400 lần bốc thăm ra đủ Nắng / Mưa / Sương mù / Đêm |
| `keyboard mode ignores rig pedals` | Ở chế độ bàn phím, đạp ga trên buồng lái không làm xe chạy |
| `switching to rig mode waits for F9 takeover` | Đổi sang buồng lái thì xe bị khóa cho tới khi nhận xe (F9 hoặc nút ĐỀ) |
| `settings screen reads the live cockpit` | Màn hình THIẾT LẬP đọc trực tiếp dữ liệu buồng lái (ảnh `01c-settings-cockpit.png`) |
| `shifting without the clutch is refused` / `clutch pressed engages 1st gear` | Côn thực tế: không đạp côn thì không vào số |
| `releasing the clutch against the brake stalls` / `engine restarts after a stall` | Chết máy khi phanh dừng mà còn số, đề lại được |
| `START button on the cockpit takes over` | Phanh + côn + nút ĐỀ trên buồng lái là nhận xe khi cần số đang giữ số 1 (firmware fix4), không cần bàn phím |
| `latched 1st gear engages with the clutch after takeover` | Nhận xe xong, số 1 đang giữ vào ngay vì côn đang đạp |
| `key N selects neutral on a latched shifter` / `pressing the clutch again leaves neutral` | Phím N về N dù firmware vẫn báo số 1; nhả rồi đạp côn lại thì số 1 vào lại |

Các bài kiểm tra giao thức chạy khi build (`ProtocolVerification`) còn kiểm: chiều encoder (tick dương = trái), hiệu chỉnh bàn đạp đấu ngược hoặc hành trình ngắn, bảng cần số, hai công tắc số cùng chạm thì thành N, đang có số mà đạp hết côn thì nhận xe được (nhả côn thì không).
| `grid race spawns player and 5 AI racers` | Lưới xuất phát có 6 xe |
| `AI racers drive away from the grid` / `stay upright` | Xe AI rời lưới, đi đúng mặt đường, không lật |
| `standings rank the stationary player last` | Bảng xếp hạng đúng khi bạn đứng yên |
| `AI racer completes the race distance` | AI chạy hết số vòng và được ghi giờ về đích |
| `race results rank the player among the field` | Màn hình kết quả có hạng và bảng chung cuộc |
| `AI racers drive the mountain pass` | Cuộc đua chạy được trên cả đường đèo tuyết |

Ảnh minh chứng: `06h-race-start.png`, `06i-race-pack.png`, `06j-race-results.png`, `06k-race-mountain.png`.

## 2. Hai chế độ điều khiển (kiểm bằng tay)

1. Màn hình chính → hàng **ĐIỀU KHIỂN** → chọn **Bàn phím** → Lái tự do → Bắt đầu.
   - Góc trên phải HUD ghi `BÀN PHÍM`. Dòng gợi ý dưới cùng liệt kê W/S, A/D, H, F10.
   - W chạy, S phanh rồi lùi, A/D đánh lái. H bật đèn (tắt → cốt → pha). Xe không có gạt mưa.
2. Đang lái, nhấn **F10**.
   - Hiện thông báo *Đã chuyển sang BUỒNG LÁI*. Xe bị khóa phanh, HUD báo *CHƯA KẾT NỐI BUỒNG LÁI* (nếu ESP32 chưa gửi) hoặc *BUỒNG LÁI TẠM KHÓA* (nếu đã kết nối). Phím W không còn tác dụng.
3. Với buồng lái thật đã kết nối (xem [HUONG_DAN_BUONG_LAI.md](HUONG_DAN_BUONG_LAI.md)) và firmware fix4: bật khóa điện, nhả ga, giữ phanh và côn, nhấn **nút ĐỀ** (hoặc F9), gạt số 1 rồi buông cần, nhả côn từ từ và chạy.
   - Buông cần số sau khi gạt: HUD vẫn ghi số 1. Phím **N** đưa về N; đạp côn lại thì số 1 vào lại.
   - Vô lăng, bàn đạp, cần số điều khiển xe. Dòng dưới HUD hiện số, % ga/phanh/côn và % lái trực tiếp.
   - Gạt số khi chưa đạp côn: HUD báo *KHÔNG VÀO ĐƯỢC SỐ*. Phanh dừng xe khi còn số mà không đạp côn: *CHẾT MÁY*.
4. Nhấn **Esc** → nút *Điều khiển: … ⇄ Đổi (F10)* → đổi lại **Bàn phím** → Tiếp tục.
   - Xe lái được ngay bằng bàn phím, vô lăng thật hết lực phản hồi.
5. Thoát game rồi mở lại: lựa chọn điều khiển lần cuối vẫn được giữ.

## 3. Thời tiết ngẫu nhiên

1. Hàng thời tiết để **Ngẫu nhiên** (mặc định), bắt đầu vài lượt liên tiếp.
   - HUD ghi ví dụ `MƯA (NGẪU NHIÊN)`, mỗi lượt có thể khác nhau. Trời tối thì đèn tự bật (chế độ bàn phím).
2. Chọn cố định *Đêm* → lượt chơi luôn là ban đêm, HUD không ghi "(ngẫu nhiên)".

## 4. Đua tranh hạng

1. Chế độ **Đua tranh hạng** → chọn **ĐỐI THỦ AI** (Dễ / Vừa / Khó) và **SỐ VÒNG** (1 / 3 / 5) → chọn xe, cung đường → Bắt đầu.
2. Bạn xuất phát ở vị trí thứ 4 trên lưới, sau đếm ngược 3-2-1 là `XUẤT PHÁT!`.
   - Bảng xếp hạng bên trái cập nhật liên tục, kèm khoảng cách tới từng xe (+ là xe ở trước bạn).
   - Dòng trên cùng ghi `ĐUA TRANH HẠNG · VÒNG x / N · HẠNG p / 6`.
   - Minimap hiện chấm màu của từng xe AI. Tên tay đua hiện phía trên xe AI.
3. Xe AI giảm tốc trước khúc cua, vượt sang làn trống, bám đuôi khi không vượt được. Va chạm với xe AI ít gây hỏng xe.
4. Chạy đủ số vòng qua vạch đích → màn hình **VỀ ĐÍCH HẠNG p / 6** với bảng chung cuộc và giờ về đích. Thắng được 3 sao, podium 2 sao, về đích 1 sao.
5. Nếu xe hỏng hoàn toàn: **KHÔNG HOÀN THÀNH CUỘC ĐUA**.

## 5. Bản chơi trên GitHub

Thư mục `SimRacing-Windows/` là bản chơi đã build sẵn, nằm trong repo (qua Git LFS). Thành viên chỉ cần `git clone` rồi chạy `Chay_SimRacing_Unity.bat`, không cần cài Unity; xem [README.md](README.md). Sau mỗi lần sửa code: chọn **SimRacing → Build Windows** (lệnh này tự cập nhật `SimRacing-Windows/`), rồi commit và push cả thư mục đó.
