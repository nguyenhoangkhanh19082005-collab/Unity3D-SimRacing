# SimRacing — Unity

Mở `Chay_SimRacing_Unity.bat` để chơi, hoặc `Mo_Du_An_Unity.bat` để sửa dự án bằng Unity 6000.3.26f1. Đây là dự án C# / URP riêng, sử dụng tài nguyên Unity; bản chạy không cần Godot.

Firmware được giữ nguyên và sao chép trong `Hardware/firmware-Arduino IDE`. Không có thay đổi hoặc nạp lại firmware trong đợt chuyển engine này.

## Khởi hành với rig

1. Chọn xe, cung đường và thời tiết rồi bắt đầu chuyến đi.
2. Rig gửi UDP đến cổng **9999** của máy. Giữ phanh, về N, nhả ga và thả nút đề.
3. Nhấn **F9** để xác nhận tiếp quản, đợi đếm ngược.
4. Bật khóa, đề máy. Đạp côn, vào số 1, thêm ga và nhả côn từ từ.

Vô lăng, ga, phanh, côn, cần số, đề máy, đèn, xi nhan, còi và gạt mưa lấy từ rig. Mất gói tin quá 250 ms, mất tiêu điểm cửa sổ, tạm dừng hoặc báo lỗi cần số sẽ khóa điều khiển và ngắt phản hồi lực. Khi kết nối lại, thực hiện lại bước 2–3. Không tự tiếp tục ga cũ.

## Phím tiện ích

| Phím | Thao tác |
| --- | --- |
| F1 / F2 / F3 / F4 | Buồng lái / bám đuôi / nắp capo / cản trước |
| V hoặc nút camera trên rig | Chuyển góc nhìn |
| F9 | Mở khóa rig khi thỏa điều kiện tiếp quản |
| Esc | Tạm dừng / tiếp tục |
| Backspace | Cứu hộ khi đã dừng xe |
| Space | Cứu hộ xe hỏng hoặc hết xăng khi đã dừng |

Đổ xăng bằng cách dừng tại trạm, tắt máy và giữ phanh. Cứu hộ sửa xe và cấp lượng xăng tối thiểu, đồng thời làm mất điều kiện hoàn thành sạch của lượt Career hiện tại.

## Các chế độ

- **Lái tự do:** giao thông hai chiều, giới hạn tốc độ, nhiên liệu và cứu hộ.
- **Đua nhanh:** ba vòng trên tuyến đóng, cổng kiểm tra theo thứ tự và thành tích ghost riêng cho xe/đường/thời tiết.
- **Career:** năm bài từ khởi hành số tay đến time trial. Đạt bảy sao để mở Bugatti.

Hai tuyến hiện tại không có giao lộ, vì vậy không tự thêm đèn đỏ hay cảnh sát truy đuổi; giữ cách vận hành của bản Godot đã cải thiện.

## Hình ảnh và dữ liệu

Mặc định **Cân bằng**, mục tiêu 60 FPS, vật lý 120 Hz. Có mức Nhẹ/Cao và thanh điều chỉnh chuyển động đầu. Hiệu năng thực tế phụ thuộc cảnh và máy.

Hồ sơ, cấu hình rig và ghost Unity nằm trong thư mục `UnitySaveV1` dưới vùng dữ liệu ứng dụng SimRacing Unity của Windows. Dữ liệu Godot được giữ riêng. Tệp lưu có bản dự phòng.

Mã vận hành nằm trong `Assets/SimRacing/Scripts`; công cụ tạo bản Windows nằm trong `Assets/SimRacing/Editor/BuildProject.cs`. Hình học và vật liệu đã chuyển đổi nằm trong `Assets/SimRacing/Imported`, các prefab nằm trong `Assets/Resources`.

`Assets/SourceData` và `Assets/TrafficSourceData` là dữ liệu hình học trung gian phục vụ nhập lại, không phải mã hoặc bộ chạy Godot. Không cần chúng để chạy bản Windows đã đóng gói.

## Kiểm tra

Chạy bản Windows với `--verify --rig-test-port=19999` để kiểm tra bằng rig giả lập trên loopback. Chế độ này không lưu tiến trình người chơi và từ chối cổng rig thật 9999. Báo cáo và ảnh xuất vào `Builds/Windows/Verification`.
