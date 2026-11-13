#pragma once
// Pin map from schematic + existing W5500 sample. Verify module SILK labels.
constexpr int PEDAL_CLUTCH=1, PEDAL_BRAKE=2, PEDAL_THROTTLE=3;
constexpr int ENCODER_A=15, ENCODER_B=16;
constexpr int RPWM=6, LPWM=7, REN=41, LEN=42;
constexpr int JOY_X=4, JOY_Y=5, JOY_BUTTON=17;
// Actual rig wiring confirmed 2026-10-13 (differs from the original schematic).
constexpr int I2C_SDA=18, I2C_SCL=8;
constexpr int ETH_MOSI=11, ETH_MISO=13, ETH_SCK=12, ETH_CS=10, ETH_RST=14;
constexpr int DASH_TX=39, DASH_RX=40; // Adjacent GPIOs on S3; GPIO17 is already in use.
// Debug uses UART0 GPIO43/44 (CH343) at 115200, independent of USB CDC.
// Dashboard UART uses 39/40; leave UART0 pins 43/44 for the USB-UART bridge.
// Do not attach an external GPIO JTAG probe or another peripheral to 39/40.
constexpr bool ENABLE_FFB=false; // Set true ONLY after electrical/direction checks.
constexpr int MOTOR_SIGN=1;     // Test at low force: must oppose wheel displacement.
constexpr int ENCODER_SIGN=1;   // Godot convention: positive ticks = wheel LEFT.
// MCP23017 input (0..5 = GPA0..GPA5) pressed in gear 1,2,3,4,5,R. Measured on the rig 2026-11.
// Firmware then reports gear 1..6 directly; keep the game's shifter map at "Mac dinh".
constexpr uint8_t GEAR_PIN[6] = {0, 3, 1, 4, 2, 5};
constexpr uint8_t PWM_LIMIT=40; // 40/255 max duty, independent of game's strength slider.
constexpr uint32_t FEEDBACK_TIMEOUT=200, SCREEN_TIMEOUT=500;
constexpr int32_t STEERING_HARD_LIMIT=3300; // Example: 600 PPR x4, direct drive, +/-495deg.
// Raw released/pressed ADC endpoints. Change after checking dashboard diagnostics.
// Output is normalized 0..4095; leave game pedal inversion OFF after calibrating here.
// Measured on the rig 2026-11. Throttle only moves 50 counts and rests at 4095 (ADC saturation):
// widen its travel mechanically if the throttle % is jumpy.
constexpr int THROTTLE_RELEASED=4095, THROTTLE_PRESSED=4045;
constexpr int BRAKE_RELEASED=3350, BRAKE_PRESSED=2850;
constexpr int CLUTCH_RELEASED=3070, CLUTCH_PRESSED=2630;
static_assert(THROTTLE_RELEASED!=THROTTLE_PRESSED && BRAKE_RELEASED!=BRAKE_PRESSED && CLUTCH_RELEASED!=CLUTCH_PRESSED,"Invalid pedal endpoints");
static_assert(GEAR_PIN[0]<6 && GEAR_PIN[1]<6 && GEAR_PIN[2]<6 && GEAR_PIN[3]<6 && GEAR_PIN[4]<6 && GEAR_PIN[5]<6,"GEAR_PIN must name GPA0..GPA5");
static_assert(PWM_LIMIT<=64,"Commission at <=25 percent PWM; review mechanics before increasing");
