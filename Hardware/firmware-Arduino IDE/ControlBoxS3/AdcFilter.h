#pragma once
#include <stdint.h>
// In its own header so the Arduino auto-generated prototypes can see the type.
struct AdcFilter { int32_t ema=-1, held=-1; };