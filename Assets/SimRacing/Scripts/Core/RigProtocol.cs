using System;
using UnityEngine;

namespace SimRacing
{
    [Serializable]
    public sealed class RigCalibration
    {
        // Version 2 adds pedal endpoints, the shifter map and the manual-clutch option. Older files are
        // rejected by Valid so every PC starts from the defaults below instead of a stale 8000-tick wheel.
        public int version = 2;
        // Encoder E38S6G5-600B: 600 PPR read on all 4 edges = 2400 ticks per wheel turn.
        // 3000 ticks = 450 degrees each way (900 degrees lock to lock), inside the firmware's 3300 hard limit.
        public float steeringCenterTicks, steeringHalfRangeTicks = 3000;
        public float steeringDeadzone = .015f, pedalDeadzone = .015f;
        public float ffbStrength = .55f, ffbLimit = .8f;
        public bool invertSteering, invertThrottle, invertBrake, invertClutch;
        // Firmware pedal value (0..4095) with the pedal fully released / fully pressed. Recorded by the
        // in-game pedal calibration, so a potentiometer that only travels part of its range still gives 0..100%.
        public int throttleReleased, throttlePressed = 4095, brakeReleased, brakePressed = 4095, clutchReleased, clutchPressed = 4095;
        // gearMap[n] = game gear for firmware switch n (1..6 = U1..U6). Game gears: 0 = N, 1..5, 6 = R.
        // Recorded by "Học cần số" so the six shifter switches may be wired in any order.
        public int[] gearMap = { 0, 1, 2, 3, 4, 5, 6 };
        // Manual gearbox realism: a gear only engages with the clutch pressed, and the engine stalls when
        // the car is held still in gear with the clutch released.
        public bool manualClutch = true;
        public bool Valid => version == 2 && Finite(steeringCenterTicks) && Between(steeringHalfRangeTicks, 300, 20000)
            && Between(steeringDeadzone, 0, .25f) && Between(pedalDeadzone, 0, .25f)
            && Between(ffbStrength, 0, 1) && Between(ffbLimit, .1f, 1)
            && PedalRange(throttleReleased, throttlePressed) && PedalRange(brakeReleased, brakePressed) && PedalRange(clutchReleased, clutchPressed)
            && GearMapValid(gearMap);
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static bool Between(float x, float lo, float hi) => Finite(x) && x >= lo && x <= hi;
        public const int MinimumPedalTravel = 300;
        static bool PedalRange(int released, int pressed) => released >= 0 && released <= 4095 && pressed >= 0 && pressed <= 4095
            && Mathf.Abs(pressed - released) >= MinimumPedalTravel;
        public static bool GearMapValid(int[] map)
        {
            if (map == null || map.Length != 7 || map[0] != 0) return false;
            foreach (int g in map) if (g < 0 || g > 6) return false;
            return true;
        }
        public int Gear(int switchGear) => switchGear >= 0 && switchGear < 7 && GearMapValid(gearMap) ? gearMap[switchGear] : 0;
        /// <summary>Encoder ticks to steering -1 (left) .. +1 (right). The firmware counts positive ticks
        /// when the wheel turns LEFT, so positive ticks steer left unless invertSteering is set.</summary>
        public float SteeringFromTicks(int ticks) => Steering(-(ticks - steeringCenterTicks) / steeringHalfRangeTicks);
        public float Steering(float x)
        {
            x = Mathf.Clamp(x, -1, 1);
            x = Mathf.Sign(x) * Mathf.Max(0, Mathf.Abs(x) - steeringDeadzone) / (1 - steeringDeadzone);
            return invertSteering ? -x : x;
        }
        public float Pedal(int raw, bool invert) => Pedal(raw, 0, 4095, invert);
        public float Pedal(int raw, int released, int pressed, bool invert)
        {
            float t = pressed == released ? 0 : Mathf.Clamp01((raw - released) / (float)(pressed - released));
            if (invert) t = 1 - t;
            return Mathf.Clamp01((t - pedalDeadzone) / Mathf.Max(.1f, 1 - pedalDeadzone * 2));
        }
        public float Throttle(int raw) => Pedal(raw, throttleReleased, throttlePressed, invertThrottle);
        public float Brake(int raw) => Pedal(raw, brakeReleased, brakePressed, invertBrake);
        public float Clutch(int raw) => Pedal(raw, clutchReleased, clutchPressed, invertClutch);
        public float Feedback(float torque) => Mathf.Clamp(torque * Mathf.Clamp01(ffbStrength),
            -Mathf.Clamp(ffbLimit, .1f, 1) * 1000, Mathf.Clamp(ffbLimit, .1f, 1) * 1000);
    }

    public struct RigSample
    {
        public uint stamp;
        // gear = shifter position after the calibration map (0 = N, 6 = R); switchGear = raw firmware value.
        public int encoder, throttleADC, brakeADC, clutchADC, gear, switchGear, buttons, headlights;
        public float steer, throttle, brake, clutch, lookX, lookY;
        public bool ignition, starter, hazard, horn, left, right, cameraButton, gearFault;
        /// <summary>Safe state for handing the car to the cockpit: throttle released, brake held, and either
        /// neutral or the clutch pressed. Firmware fix4 latches the last gear switch (the shifter's microswitches
        /// are not held down once the lever is released), so after the first shift the lever never reports N again.</summary>
        public bool SafeToTakeOver => !gearFault && throttle <= .03f && brake >= .5f && (gear == 0 || clutch >= ClutchIn);
        /// <summary>Clutch travel that counts as "pressed" for shifting, starting and taking over.</summary>
        public const float ClutchIn = .6f;
        public bool Ready => SafeToTakeOver && !starter;
        public static RigSample Held => new RigSample { brake = 1, clutch = 1 };
    }

    public static class RigProtocol
    {
        public static ushort U16(byte[] b, int p) => (ushort)(b[p] | b[p + 1] << 8);
        public static uint U32(byte[] b, int p) => (uint)(U16(b, p) | U16(b, p + 2) << 16);
        public static bool Newer(uint stamp, uint previous)
        {
            uint advance = unchecked(stamp - previous);
            return advance != 0 && advance < 0x80000000u;
        }
        public static bool TryDecode(byte[] b, RigCalibration c, out RigSample s)
        {
            s = default;
            if (b == null || c == null || !c.Valid) return false;
            if (b.Length == 14)
            {
                if (U16(b, 0) > 4095 || U16(b, 2) > 4095 || U16(b, 4) > 4095) return false;
                for (int i = 6; i <= 9; i++) if (b[i] > 1) return false;
                s.stamp = U32(b, 10); s.encoder = U16(b, 0) - 2048;
                s.throttleADC = U16(b, 4);
                s.steer = c.Steering(s.encoder / 2048f);
                s.throttle = c.Pedal(s.throttleADC, c.invertThrottle);
                s.gear = b[7]; s.cameraButton = b[6] == 1; s.ignition = b[8] == 1; s.hazard = b[9] == 1;
                s.lookX = (U16(b, 0) - 2048) / 2048f; s.lookY = (U16(b, 2) - 2048) / 2048f;
                return true;
            }
            if (b.Length != 27 && b.Length != 28) return false;
            if (U16(b, 0) != 0x5349 || U16(b, 2) != 2 || b[18] > 6 || b[25] > 1 || U16(b, 19) > 0x3ff) return false;
            foreach (int p in new[] { 12, 14, 16, 21, 23 }) if (U16(b, p) > 4095) return false;
            s.stamp = U32(b, 4); s.encoder = unchecked((int)U32(b, 8));
            s.throttleADC = U16(b, 12); s.brakeADC = U16(b, 14); s.clutchADC = U16(b, 16);
            s.steer = c.SteeringFromTicks(s.encoder);
            s.throttle = c.Throttle(s.throttleADC);
            s.brake = c.Brake(s.brakeADC);
            s.clutch = c.Clutch(s.clutchADC);
            s.switchGear = b[18]; s.gear = c.Gear(s.switchGear); int mask = U16(b, 19); s.buttons = mask;
            s.ignition = (mask & 1) != 0; s.starter = (mask & 2) != 0; s.hazard = (mask & 4) != 0;
            s.headlights = (mask & 16) != 0 ? 2 : (mask & 8) != 0 ? 1 : 0;
            // Mask bits 32/64 (wiper switch) stay reserved in the packet format but are ignored: the game has no wipers.
            s.horn = (mask & 128) != 0; s.left = (mask & 256) != 0; s.right = (mask & 512) != 0;
            // A real joystick rests a little off 2048: ignore the first 12% so the view does not drift.
            s.lookX = Look(U16(b, 21)); s.lookY = Look(U16(b, 23));
            s.cameraButton = b[25] != 0; s.gearFault = (b[26] & 1) != 0;
            // Two shifter switches closed at once (between gates, or a bouncing contact): treat it as neutral.
            if (s.gearFault) { s.gear = 0; s.switchGear = 0; }
            return true;
        }
        static float Look(int raw)
        {
            float x = Mathf.Clamp((raw - 2048) / 2048f, -1, 1);
            return Mathf.Sign(x) * Mathf.Max(0, Mathf.Abs(x) - .12f) / .88f;
        }
        public static string GearName(int gear) => gear == 0 ? "N" : gear == 6 ? "R" : gear.ToString();
        static void Put16(byte[] b, int p, int v) { b[p] = (byte)v; b[p + 1] = (byte)(v >> 8); }
        public static byte[] Feedback(float torque, float speed, float rpm, int gear, float fuel,
            bool running, RigSample input, bool blink, float rumble, ushort sequence)
        {
            var b = new byte[16];
            Put16(b, 0, 0x4642); Put16(b, 2, Mathf.Clamp(Mathf.RoundToInt(torque), -1000, 1000));
            Put16(b, 4, Mathf.Clamp(Mathf.RoundToInt(speed * 10), 0, 3500));
            Put16(b, 6, Mathf.Clamp(Mathf.RoundToInt(rpm), 0, 12000));
            b[8] = (byte)Mathf.Clamp(gear, 0, 6); b[9] = 90;
            b[10] = (byte)Mathf.Clamp(Mathf.RoundToInt(fuel), 0, 100);
            b[11] = (byte)((running ? 1 : 0) | (input.hazard ? 2 : 0)
                | (blink && (input.left || input.hazard) ? 4 : 0) | (blink && (input.right || input.hazard) ? 8 : 0)
                | (input.headlights > 0 ? 16 : 0) | (input.headlights == 2 ? 32 : 0));
            b[12] = 45; b[13] = (byte)Mathf.Clamp(Mathf.RoundToInt(rumble), 0, 255); Put16(b, 14, sequence);
            return b;
        }
    }
}
