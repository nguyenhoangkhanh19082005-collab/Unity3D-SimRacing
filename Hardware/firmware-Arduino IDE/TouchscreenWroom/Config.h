#pragma once
constexpr int UART_RX=27, UART_TX=22; // S3 TX39 -> RX27; S3 RX40 <- TX22; common GND.
constexpr int TOUCH_IRQ=36, TOUCH_MOSI=32, TOUCH_MISO=39, TOUCH_SCK=25, TOUCH_CS=33;
constexpr int BACKLIGHT=21;
constexpr int TOUCH_X_MIN=200, TOUCH_X_MAX=3700, TOUCH_Y_MIN=240, TOUCH_Y_MAX=3800;
constexpr bool TOUCH_SWAP_XY=false, TOUCH_INVERT_X=false, TOUCH_INVERT_Y=false;
