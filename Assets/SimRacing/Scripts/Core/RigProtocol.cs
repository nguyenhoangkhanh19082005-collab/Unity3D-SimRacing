using System;
using UnityEngine;

namespace SimRacing
{
    [Serializable]
    public sealed class RigCalibration
    {
        public float steeringCenterTicks, steeringHalfRangeTicks = 8000;
        public float steeringDeadzone = .015f, pedalDeadzone = .015f;
        public float ffbStrength = .55f, ffbLimit = .8f;
        public bool invertSteering, invertThrottle, invertBrake, invertClutch;
        public bool Valid => Finite(steeringCenterTicks) && Between(steeringHalfRangeTicks, 1000, 20000)
            && Between(steeringDeadzone, 0, .25f) && Between(pedalDeadzone, 0, .25f)
            && Between(ffbStrength, 0, 1) && Between(ffbLimit, .1f, 1);
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static bool Between(float x, float lo, float hi) => Finite(x) && x >= lo && x <= hi;
        public float Steering(float x)
        {
            x = Mathf.Clamp(x, -1, 1);
            x = Mathf.Sign(x) * Mathf.Max(0, Mathf.Abs(x) - steeringDeadzone) / (1 - steeringDeadzone);
            return invertSteering ? -x : x;
        }
        public float Pedal(int raw, bool invert) => Mathf.Clamp01(((invert ? 1 - raw / 4095f : raw / 4095f)
            - pedalDeadzone) / Mathf.Max(.1f, 1 - pedalDeadzone * 2));
        public float Feedback(float torque) => Mathf.Clamp(torque * Mathf.Clamp01(ffbStrength),
            -Mathf.Clamp(ffbLimit, .1f, 1) * 1000, Mathf.Clamp(ffbLimit, .1f, 1) * 1000);
    }

    public struct RigSample
    {
        public uint stamp;
        public int encoder, throttleADC, brakeADC, clutchADC, gear, headlights, wipers;
        public float steer, throttle, brake, clutch, lookX, lookY;
        public bool ignition, starter, hazard, horn, left, right, cameraButton, gearFault;
        public bool Ready => !gearFault && throttle <= .03f && brake >= .5f && gear == 0 && !starter;
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
            s.steer = c.Steering((s.encoder - c.steeringCenterTicks) / c.steeringHalfRangeTicks);
            s.throttle = c.Pedal(s.throttleADC, c.invertThrottle);
            s.brake = c.Pedal(s.brakeADC, c.invertBrake);
            s.clutch = c.Pedal(s.clutchADC, c.invertClutch);
            s.gear = b[18]; int mask = U16(b, 19);
            s.ignition = (mask & 1) != 0; s.starter = (mask & 2) != 0; s.hazard = (mask & 4) != 0;
            s.headlights = (mask & 16) != 0 ? 2 : (mask & 8) != 0 ? 1 : 0;
            s.wipers = (mask & 64) != 0 ? 2 : (mask & 32) != 0 ? 1 : 0;
            s.horn = (mask & 128) != 0; s.left = (mask & 256) != 0; s.right = (mask & 512) != 0;
            s.lookX = Mathf.Clamp((U16(b, 21) - 2048) / 2048f, -1, 1);
            s.lookY = Mathf.Clamp((U16(b, 23) - 2048) / 2048f, -1, 1);
            s.cameraButton = b[25] != 0; s.gearFault = (b[26] & 1) != 0;
            return true;
        }
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
