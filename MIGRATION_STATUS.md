# SimRacing — chuyển sang Unity 6.3 LTS

Nguồn đối chứng: D:\doancodientu. Dự án Unity độc lập: E:\doancodientuunity3D.
Engine: Unity 6000.3.26f1, URP 17.3.0. Không dùng Godot để chạy bản Unity.

## Trạng thái thực tế

Bản Windows độc lập đã xuất thành công và đạt 33/33 kiểm tra chạy thực tế,
22 kiểm tra giao thức và 840 điểm kiểm tra mặt đường. Lỗi đổi phiên UDP đã sửa.
Xem VERIFICATION.md để biết phạm vi kiểm tra và bằng chứng.

Ngày 14/10/2026, lần xuất bổ sung dừng với mã 198 vì Unity không còn nhận
giấy phép Editor. Người dùng chọn giữ bản Windows đã kiểm tra. Mã nguồn
có thêm sửa hiển thị dấu chọn cho bốn tùy chọn đảo chiều điều khiển; sửa nhỏ
này chưa có trong bản Windows và chưa được biên dịch/kiểm tra trực quan.
Sau khi Hub nhận giấy phép, mở dự án và dùng SimRacing > Build Windows
để đưa sửa này vào bản chạy. Không cần giấy phép Editor để chạy bản đã xuất.

Buồng lái Godot đã có 11 ảnh kiểm chứng cuối, không lỗi. Dấu vân tay SHA-256
của 111 tệp dự án gốc nằm trong reports/cockpit_unity_2026_10_13 trên ổ D.
Phần chuyển engine được làm trong dự án Unity riêng, không sửa tiếp mã Godot.

## Phạm vi bàn giao

- Buồng lái Godot được cải thiện và kiểm tra; sau đó ghi dấu vân tay source để
  chứng minh phần chuyển engine không thay đổi bản gốc.
- Ba xe, hai bản đồ, vật liệu, thời tiết, camera, bánh, đèn, gạt mưa và âm thanh.
- Lái bằng rig: gói V1/V2, calibration, số tay/côn/đề máy và phản hồi lực.
- Giữ phanh khi mất rig; chống gói cũ, timeout, pause/focus-loss và F9 nhận lại rig.
- Lái tự do, đua nhanh, career, traffic, quy tắc đường bộ, va chạm, nhiên liệu, cứu hộ,
  tiến trình và ghost với dữ liệu lưu riêng của bản Unity.
- Giữ đúng cấu hình hai bản đồ gốc: không có giao lộ, đèn đỏ hay cảnh sát
  truy đuổi đang hoạt động; không tự thêm chúng vào bản chuyển đổi.
- Menu, chọn xe/bản đồ/thời tiết, HUD, hướng dẫn và thiết lập rig.
- Kiểm tra Unity Editor, build Windows, chạy bản build, ảnh đối chứng và đo
  hiệu năng thực; không dùng kiểm tra văn bản thay cho chạy game.

Bản bàn giao giữ bản Windows đã kiểm tra theo lựa chọn của người dùng.
Kiểm tra bằng gói giả không thay thế nghiệm thu rig thật.
