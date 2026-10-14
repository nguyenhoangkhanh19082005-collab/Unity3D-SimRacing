#pragma once
// Pin map from schematic + existing W5500 sample. Verify module SILK labels.
constexpr int PEDAL_CLUTCH=1, PEDAL_BRAKE=2, PEDAL_THROTTLE=3;
constexpr int ENCODER_A=15, ENCODER_B=16;
constexpr int RPWM=6, LPWM=7, REN=41, LEN=42;
constexpr int JOY_X=4, JOY_Y=5, JOY_BUTTON=21;
constexpr int I2C_SDA=8, I2C_SCL=18;
constexpr int ETH_MOSI=11, ETH_MISO=13, ETH_SCK=12, ETH_CS=10, ETH_RST=14;
constexpr int DASH_TX=39, DASH_RX=40; // Adjacent GPIOs on S3; GPIO17 is already in use.
// Debug uses UART0 GPIO43/44 (CH343) at 115200, independent of USB CDC.
// Dashboard UART uses 39/40; leave UART0 pins 43/44 for the USB-UART bridge.
// Do not attach an external GPIO JTAG probe or another peripheral to 39/40.
constexpr bool ENABLE_FFB=false; // Set true ONLY after electrical/direction checks.
constexpr int MOTOR_SIGN=1;     // Test at low force: must oppose wheel displacement.
constexpr int ENCODER_SIGN=1;   // Godot convention: positive ticks = wheel LEFT.
constexpr uint8_t PWM_LIMIT=40; // 40/255 max duty, independent of game's strength slider.
constexpr uint32_t FEEDBACK_TIMEOUT=200, SCREEN_TIMEOUT=500;
constexpr int32_t STEERING_HARD_LIMIT=3300; // Example: 600 PPR x4, direct drive, +/-495deg.
// Raw released/pressed ADC endpoints. Change after checking dashboard diagnostics.
// Output is normalized 0..4095; leave game pedal inversion OFF after calibrating here.
constexpr int THROTTLE_RELEASED=0, THROTTLE_PRESSED=4095;
constexpr int BRAKE_RELEASED=0, BRAKE_PRESSED=4095;
constexpr int CLUTCH_RELEASED=0, CLUTCH_PRESSED=4095;
static_assert(THROTTLE_RELEASED!=THROTTLE_PRESSED && BRAKE_RELEASED!=BRAKE_PRESSED && CLUTCH_RELEASED!=CLUTCH_PRESSED,"Invalid pedal endpoints");
static_assert(PWM_LIMIT<=64,"Commission at <=25 percent PWM; review mechanics before increasing");
