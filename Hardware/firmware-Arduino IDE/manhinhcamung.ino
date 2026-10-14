#include <SPI.h>
#include <TFT_eSPI.h>
#include <XPT2046_Touchscreen.h>
#include <lvgl.h>

// --- CẤU HÌNH CHÂN CẢM ỨNG (Mạch CYD) ---
#define XPT2046_IRQ 36
#define XPT2046_MOSI 32
#define XPT2046_MISO 39
#define XPT2046_CLK 25
#define XPT2046_CS 33

SPIClass touchSPI = SPIClass(VSPI);
XPT2046_Touchscreen ts(XPT2046_CS, XPT2046_IRQ);
TFT_eSPI tft = TFT_eSPI();

#define SCREEN_WIDTH 320
#define SCREEN_HEIGHT 240

// Khai báo bộ đệm vẽ cho LVGL v9
#define DRAW_BUF_SIZE (SCREEN_WIDTH * SCREEN_HEIGHT / 10 * (LV_COLOR_DEPTH / 8))
uint8_t draw_buf[DRAW_BUF_SIZE];

// --- KHAI BÁO CÁC THÀNH PHẦN GIAO DIỆN ---
lv_obj_t * arc_rpm;
lv_obj_t * label_speed;
lv_obj_t * btn_start;

/* 1. Hàm đẩy dữ liệu hình ảnh ra màn hình TFT (Chuẩn LVGL v9) */
void my_disp_flush(lv_display_t * display, const lv_area_t * area, uint8_t * px_map) {
    uint32_t w = area->x2 - area->x1 + 1;
    uint32_t h = area->y2 - area->y1 + 1;
    tft.startWrite();
    tft.setAddrWindow(area->x1, area->y1, w, h);
    tft.pushColors((uint16_t *)px_map, w * h, true);
    tft.endWrite();
    lv_display_flush_ready(display);
}

/* 2. Hàm đọc tọa độ cảm ứng (Chuẩn LVGL v9) */
void my_touchpad_read(lv_indev_t * indev, lv_indev_data_t * data) {
    if(ts.touched()){
        TS_Point p = ts.getPoint();
        data->point.x = map(p.x, 200, 3700, 0, SCREEN_WIDTH);
        data->point.y = map(p.y, 240, 3800, 0, SCREEN_HEIGHT);
        data->state = LV_INDEV_STATE_PRESSED;
    } else {
        data->state = LV_INDEV_STATE_RELEASED;
    }
}

/* Sự kiện khi bấm nút Engine Start */
static void btn_event_cb(lv_event_t * e) {
    lv_event_code_t code = lv_event_get_code(e);
    if(code == LV_EVENT_CLICKED) {
        Serial.println("ENGINE START!!!");
        // Ở đây bạn có thể thêm lệnh gửi UART về lại ESP32-S3
    }
}

/* Hàm vẽ giao diện chính */
void build_dashboard() {
    // Đổi màu nền thành xám đen (Dark Mode)
    lv_obj_set_style_bg_color(lv_screen_active(), lv_color_hex(0x1e1e1e), LV_PART_MAIN);

    // 1. Vẽ thanh Arc (Vòng tua máy)
    arc_rpm = lv_arc_create(lv_screen_active());
    lv_obj_set_size(arc_rpm, 200, 200);
    lv_arc_set_rotation(arc_rpm, 135);
    lv_arc_set_bg_angles(arc_rpm, 0, 270);
    lv_arc_set_range(arc_rpm, 0, 8000); // Max 8000 RPM
    lv_arc_set_value(arc_rpm, 0);       // Giá trị ban đầu
    lv_obj_align(arc_rpm, LV_ALIGN_CENTER, 0, -10);

    // Xóa cái núm tròn trên thanh Arc cho ra dáng đồng hồ xe
    lv_obj_remove_style(arc_rpm, NULL, LV_PART_KNOB);
    lv_obj_set_style_arc_color(arc_rpm, lv_color_hex(0xff0000), LV_PART_INDICATOR); // Chữ thập đỏ báo tốc

    // 2. Vẽ chữ hiển thị tốc độ km/h
    label_speed = lv_label_create(lv_screen_active());
    lv_label_set_text(label_speed, "0 km/h");
    lv_obj_set_style_text_color(label_speed, lv_color_hex(0xffffff), LV_PART_MAIN);
    // Dùng font mặc định (LV_FONT_MONTSERRAT_14). Nếu bạn bật Font lớn hơn trong lv_conf.h thì khai báo ở đây
    lv_obj_align(label_speed, LV_ALIGN_CENTER, 0, -10);

    // 3. Vẽ nút bấm Engine Start
    btn_start = lv_button_create(lv_screen_active());
    lv_obj_set_size(btn_start, 120, 40);
    lv_obj_align(btn_start, LV_ALIGN_BOTTOM_MID, 0, -5);
    lv_obj_add_event_cb(btn_start, btn_event_cb, LV_EVENT_ALL, NULL);

    lv_obj_t * label_btn = lv_label_create(btn_start);
    lv_label_set_text(label_btn, "ENGINE START");
    lv_obj_center(label_btn);
}

void setup() {
    Serial.begin(115200);
    pinMode(21, OUTPUT);
    digitalWrite(21, HIGH);
    // Khởi tạo phần cứng TFT
    tft.begin();
    tft.setRotation(1);

    // Khởi tạo phần cứng Cảm ứng
    touchSPI.begin(XPT2046_CLK, XPT2046_MISO, XPT2046_MOSI, XPT2046_CS);
    ts.begin(touchSPI);
    ts.setRotation(1);

    // Khởi tạo LVGL v9
    lv_init();
    lv_tick_set_cb((lv_tick_get_cb_t)millis); // Đồng bộ thời gian thực cho LVGL

    // Đăng ký Màn hình với LVGL v9
    lv_display_t * disp = lv_display_create(SCREEN_WIDTH, SCREEN_HEIGHT);
    lv_display_set_buffers(disp, draw_buf, NULL, DRAW_BUF_SIZE, LV_DISPLAY_RENDER_MODE_PARTIAL);
    lv_display_set_flush_cb(disp, my_disp_flush);

    // Đăng ký Cảm ứng với LVGL v9
    lv_indev_t * indev = lv_indev_create();
    lv_indev_set_type(indev, LV_INDEV_TYPE_POINTER);
    lv_indev_set_read_cb(indev, my_touchpad_read);

    // Gọi hàm vẽ giao diện
    build_dashboard();
}

void loop() {
    lv_timer_handler(); // Xử lý đồ họa
    delay(5);

    /*
     * [KHU VỰC DÀNH CHO BẠN]
     * Hãy thêm code đọc UART từ Mainboard ở đây.
     * Khi nhận được tốc độ mới (ví dụ biến mySpeed), bạn gọi:
     *
     * String speedText = String(mySpeed) + " km/h";
     * lv_label_set_text(label_speed, speedText.c_str());
     * lv_arc_set_value(arc_rpm, myRPM);
     */
}
