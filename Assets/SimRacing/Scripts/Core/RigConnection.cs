using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

namespace SimRacing
{
    public sealed class RigConnection : MonoBehaviour
    {
        // v2 file: older rig-settings.json files used an 8000-tick wheel and had no pedal/shifter calibration.
        public const string SettingsFile = "rig-settings-v2.json";
        public RigCalibration calibration = new RigCalibration();
        public RigSample Raw { get; private set; }
        public RigSample Controls => Held || !Connected ? RigSample.Held : Raw;
        public bool Held { get; private set; } = true;
        public bool Connected => remote != null && Clock - lastPacket < .25;
        public bool Paused { get; private set; }
        public bool Focused { get; private set; } = true;
        public int PacketsPerSecond { get; private set; }
        public string Error { get; private set; } = "";
        public int Port { get; private set; } = 9999;
        /// <summary>IP address of the control box that is sending (empty until the first packet).</summary>
        public string RemoteAddress => remote == null ? "" : remote.Address.ToString();
        /// <summary>IPv4 addresses of this PC's active network adapters.</summary>
        public string LocalAddresses { get; private set; } = "";
        /// <summary>True when one adapter of this PC is on the control box network 192.168.1.x.</summary>
        public bool OnRigSubnet { get; private set; } = true;
        /// <summary>True when the player chose the physical cockpit as the control mode.</summary>
        public bool InUse = true;
        UdpClient socket;
        IPEndPoint remote;
        double lastPacket = double.NegativeInfinity, counterStart, addressCheck = double.NegativeInfinity;
        uint lastStamp;
        int count;
        ushort sequence;
        bool wasConnected, previousStarter;
        public Func<float> VehicleSpeed = () => 0;
        static double Clock => Time.realtimeSinceStartupAsDouble;
        static bool VerifyRun => Array.Exists(Environment.GetCommandLineArgs(), a => a == "--verify");
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            calibration = LocalStore.Load(SettingsFile, new RigCalibration(), c => c.Valid);
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("--rig-test-port=") && int.TryParse(arg.Substring(16), out int port)) Port = port;
            if (Port == 9999 && VerifyRun) Port = 18888;
            try {
                socket = new UdpClient(new IPEndPoint(Port == 9999 ? IPAddress.Any : IPAddress.Loopback, Port));
                socket.EnableBroadcast = true;
                socket.Client.Blocking = false;
            } catch (SocketException e) {
                Error = e.SocketErrorCode == SocketError.AddressAlreadyInUse
                    ? $"Cổng UDP {Port} đang bị chương trình khác chiếm (game mở 2 lần?)"
                    : "Không mở được cổng rig: " + e.SocketErrorCode;
            }
        }
        public bool Accept(byte[] packet, IPEndPoint sender, double now)
        {
            if (!RigProtocol.TryDecode(packet, calibration, out RigSample sample)) return false;
            bool lease = remote != null && now - lastPacket < .25;
            if (lease && (!sender.Equals(remote) || !RigProtocol.Newer(sample.stamp, lastStamp))) return false;
            if (!lease) Held = true;
            ApplyNeutral(ref sample);
            SmoothPedals(ref sample, lease ? now - lastPacket : -1);
            lastStamp = sample.stamp; Raw = sample; remote = sender; lastPacket = now; count++; Error = "";
            if (PedalCalibrating) TrackPedals(sample);
            return true;
        }
        void Update()
        {
            if (socket != null) {
                try {
                    // Bound work per frame so malformed packet floods cannot starve rendering.
                    for (int i = 0; i < 256 && socket.Available > 0; i++) {
                        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                        Accept(socket.Receive(ref sender), sender, Clock);
                    }
                } catch (SocketException e) { if (e.SocketErrorCode != SocketError.WouldBlock && e.SocketErrorCode != SocketError.ConnectionReset) Error = e.SocketErrorCode.ToString(); }
            }
            if (VerifyRun) Focused = true;
            bool connected = Connected;
            if (!connected || Paused || !Focused || Calibrating) Hold();
            if (wasConnected && !connected) { SendFeedback(0, 0, 0, 0, 0, false, false, 0); remote = null; Raw = RigSample.Held; }
            wasConnected = connected;
            if (!connected && Clock - addressCheck > 3) RefreshAddresses();
            // No key is needed to take the car: as soon as the cockpit is in the safe state (brake held, throttle
            // released) it drives. The control box's START signal does not reach the game on the current rig.
            if (Held && InUse && connected && Raw.SafeToTakeOver) TakeOver(true);
            if (InUse && connected && Input.GetKeyDown(KeyCode.N)) ToggleNeutral();
            // Taking the car from the cockpit itself: brake held, neutral, throttle released, then press START.
            bool starter = connected && Raw.starter;
            if (starter && !previousStarter && InUse) TakeOver(true);
            previousStarter = starter;
            if (GearLearning) StepGearLearn();
            if (Held) SendFeedback(0, 0, 0, 0, 0, false, false, 0);
            if (Clock - counterStart >= 1) { PacketsPerSecond = count; count = 0; counterStart = Clock; }
        }
        void RefreshAddresses()
        {
            addressCheck = Clock;
            try {
                var list = new List<string>();
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var address in adapter.GetIPProperties().UnicastAddresses)
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork) list.Add(address.Address.ToString());
                }
                LocalAddresses = string.Join(", ", list);
                OnRigSubnet = list.Exists(a => a.StartsWith("192.168.1."));
            } catch (Exception) { OnRigSubnet = true; }
        }
        // ---------- Neutral on a latching shifter ----------
        // The firmware keeps reporting the last gear switch that was pressed, and the H-shifter has no switch for
        // N. Key N puts the game in neutral; it lasts until the lever selects another gear, or until the clutch is
        // pressed again after being released (the driver is about to engage the latched gear again).
        int neutralSwitch;
        bool neutralClutchReleased;
        /// <summary>True while key N overrides the gear the firmware still reports.</summary>
        public bool NeutralSelected => neutralSwitch != 0;
        /// <summary>Key N: neutral on, or back into the gear the lever holds (the only way out of N in automatic,
        /// where the clutch is not used, apart from moving the lever).</summary>
        public void ToggleNeutral()
        {
            if (!NeutralSelected) { SelectNeutral(); return; }
            neutralSwitch = 0;
            var r = Raw; if (!r.gearFault) r.gear = calibration.Gear(r.switchGear); Raw = r;
        }
        public void SelectNeutral()
        {
            if (Raw.switchGear == 0 || Raw.gearFault) return;
            neutralSwitch = Raw.switchGear;
            neutralClutchReleased = Raw.clutch < .3f;
            var r = Raw; ApplyNeutral(ref r); Raw = r;
        }
        void ApplyNeutral(ref RigSample s)
        {
            if (neutralSwitch == 0) return;
            if (s.gearFault) return; // Lever crossing between switches: already reported as N.
            if (s.switchGear != neutralSwitch) { neutralSwitch = 0; return; }
            if (s.clutch < .3f) neutralClutchReleased = true;
            else if (s.clutch >= RigSample.ClutchIn && neutralClutchReleased) { neutralSwitch = 0; return; }
            s.gear = 0;
        }
        public void Hold() { Held = true; StartRequested = false; }
        /// <summary>Automatic gearbox: taking the car over (brake held, or START), also cranks the
        /// engine, so the driver only has to put the lever in gear. Consumed by the car once the countdown ends.</summary>
        public bool StartRequested { get; private set; }
        public bool ConsumeStart() { bool s = StartRequested; StartRequested = false; return s; }

        // ---------- Pedal smoothing ----------
        // The throttle potentiometer only moves ~50 ADC counts on the current rig, so one count is ~2% and the
        // reading flickers even after the firmware filter. A 40 ms low-pass plus a 3% rest band at each end keeps
        // a released pedal at exactly 0% (so takeover is not refused) without making the pedals feel slow.
        float smoothThrottle, smoothBrake, smoothClutch;
        const float PedalTau = .04f, PedalRest = .03f;
        void SmoothPedals(ref RigSample s, double dt)
        {
            float a = dt < 0 ? 1 : 1 - Mathf.Exp(-(float)dt / PedalTau);
            smoothThrottle += (s.throttle - smoothThrottle) * a;
            smoothBrake += (s.brake - smoothBrake) * a;
            smoothClutch += (s.clutch - smoothClutch) * a;
            s.throttle = Rest(smoothThrottle); s.brake = Rest(smoothBrake); s.clutch = Rest(smoothClutch);
        }
        static float Rest(float x) => x < PedalRest ? 0 : x > 1 - PedalRest ? 1 : (x - PedalRest) / (1 - 2 * PedalRest);
        /// <summary>Hand the car to the cockpit when it is in a safe state (used by tests and the takeover logic).</summary>
        public bool Resume() => TakeOver(false);
        bool TakeOver(bool fromStarter)
        {
            if (!Held || Paused || !Focused || !Connected || Calibrating || VehicleSpeed() > 1) return false;
            if (fromStarter ? !Raw.SafeToTakeOver : !Raw.Ready) return false;
            Held = false; StartRequested = !calibration.useClutchPedal; return true;
        }
        /// <summary>Why the cockpit is still held, in words the driver can act on.</summary>
        public string Blocker
        {
            get {
                if (!Connected) return "chưa nhận tín hiệu từ ESP32";
                if (Calibrating) return "đang hiệu chỉnh trong THIẾT LẬP";
                if (Paused) return "đang tạm dừng";
                if (!Focused) return "bấm chuột vào cửa sổ game";
                var r = Raw; var need = new List<string>();
                if (r.gearFault) need.Add("cần số chạm 2 công tắc cùng lúc");
                else if (!r.automatic && r.gear != 0 && r.clutch < RigSample.ClutchIn) need.Add($"đạp hết côn (đang ở số {RigProtocol.GearName(r.gear)}; hoặc phím N về số N)");
                if (r.throttle > .05f) need.Add($"nhả ga (đang {r.throttle * 100:0}%)");
                if (r.brake < .5f) need.Add($"đạp phanh quá nửa (đang {r.brake * 100:0}%)");
                if (VehicleSpeed() > 1) need.Add("dừng hẳn xe");
                if (need.Count == 0) return "Sẵn sàng · đang nhận xe…";
                return string.Join(" · ", need) + " → xe tự nhận khi đủ điều kiện.  % sai thì vào THIẾT LẬP › Hiệu chỉnh bàn đạp";
            }
        }
        public void SetPaused(bool paused) { Paused = paused; Hold(); SendFeedback(0, 0, 0, 0, 0, false, false, 0); }
        void OnApplicationFocus(bool focus)
        {
            if (VerifyRun) { Focused = true; return; }
            Focused = focus;
            if (!focus) { Hold(); SendFeedback(0, 0, 0, 0, 0, false, false, 0); }
        }
        public void SaveCalibration() { if (calibration.Valid) LocalStore.Save(SettingsFile, calibration); Hold(); }

        // ---------- Calibration (THIẾT LẬP screen) ----------
        public bool Calibrating => PedalCalibrating || GearLearning;
        /// <summary>Leaving the settings screen abandons any unfinished wizard (saved values are kept).</summary>
        public void CancelCalibration() { PedalCalibrating = false; if (GearLearning) CancelGearLearn(); }

        public string CenterSteering()
        {
            if (!Connected) return "Chưa nhận dữ liệu buồng lái.";
            calibration.steeringCenterTicks = Raw.encoder; SaveCalibration();
            return $"Đã lưu vị trí giữa (encoder {Raw.encoder}). Giờ quay vô lăng HẾT sang PHẢI rồi bấm bước 2.";
        }
        /// <summary>With the wheel turned fully right: stores the steering range and its direction.</summary>
        public string RecordSteeringRight()
        {
            if (!Connected) return "Chưa nhận dữ liệu buồng lái.";
            float d = Raw.encoder - calibration.steeringCenterTicks;
            if (Mathf.Abs(d) < 300) return $"Vô lăng mới lệch {Mathf.Abs(d):0} ticks so với giữa. Bấm bước 1 khi vô lăng thẳng, quay HẾT sang phải rồi bấm lại.";
            calibration.steeringHalfRangeTicks = Mathf.Clamp(Mathf.Abs(d), 300, 20000);
            // Positive ticks normally mean "left"; if turning right produced positive ticks, flip the direction.
            calibration.invertSteering = d > 0;
            SaveCalibration();
            return $"Đã lưu: hết lái = {Mathf.Abs(d):0} ticks (≈ {Mathf.Abs(d) / 2400f * 360:0}° mỗi bên){(d > 0 ? ", đã đảo chiều encoder" : "")}.";
        }

        public bool PedalCalibrating { get; private set; }
        readonly int[] pedalMin = new int[3], pedalMax = new int[3];
        static readonly string[] PedalNames = { "Ga", "Phanh", "Côn" };
        public void BeginPedalCalibration()
        {
            for (int i = 0; i < 3; i++) { pedalMin[i] = 4095; pedalMax[i] = 0; }
            PedalCalibrating = true; Hold();
            if (Connected) TrackPedals(Raw);
        }
        public void CancelPedalCalibration() => PedalCalibrating = false;
        void TrackPedals(RigSample s)
        {
            int[] now = { s.throttleADC, s.brakeADC, s.clutchADC };
            for (int i = 0; i < 3; i++) { pedalMin[i] = Mathf.Min(pedalMin[i], now[i]); pedalMax[i] = Mathf.Max(pedalMax[i], now[i]); }
        }
        public string PedalTravel => PedalCalibrating
            ? $"Hành trình đã ghi:  GA {Mathf.Max(0, pedalMax[0] - pedalMin[0])}   PHANH {Mathf.Max(0, pedalMax[1] - pedalMin[1])}{(calibration.useClutchPedal ? $"   CÔN {Mathf.Max(0, pedalMax[2] - pedalMin[2])}" : "")}   (cần ≥ {RigCalibration.MinimumPedalTravel})"
            : "";
        /// <summary>All pedals released now: the current value is "released", the far end seen during
        /// calibration is "pressed". Works whichever way round each potentiometer is wired.</summary>
        public bool FinishPedalCalibration(out string message)
        {
            if (!Connected) { message = "Chưa nhận dữ liệu buồng lái."; return false; }
            int[] now = { Raw.throttleADC, Raw.brakeADC, Raw.clutchADC };
            int[] released = new int[3], pressed = new int[3];
            var shallow = new List<string>();
            // Automatic mode: the clutch pedal is not used (glued on the current rig), so it is neither required nor changed.
            int count = calibration.useClutchPedal ? 3 : 2;
            for (int i = 0; i < count; i++) {
                int far = Mathf.Abs(pedalMax[i] - now[i]) >= Mathf.Abs(pedalMin[i] - now[i]) ? pedalMax[i] : pedalMin[i];
                if (Mathf.Abs(far - now[i]) < RigCalibration.MinimumPedalTravel) shallow.Add(PedalNames[i]);
                // 4% margin at each end: a resting pedal reads 0% and a firm press reliably reaches 100%.
                int margin = (far - now[i]) / 25;
                released[i] = Mathf.Clamp(now[i] + margin, 0, 4095); pressed[i] = Mathf.Clamp(far - margin, 0, 4095);
            }
            if (shallow.Count > 0) {
                message = "Chưa đạp đủ sâu: " + string.Join(", ", shallow) + ". Đạp HẾT từng bàn đạp vài lần, NHẢ HẾT rồi bấm Xong.";
                return false;
            }
            calibration.throttleReleased = released[0]; calibration.throttlePressed = pressed[0];
            calibration.brakeReleased = released[1]; calibration.brakePressed = pressed[1];
            if (count == 3) { calibration.clutchReleased = released[2]; calibration.clutchPressed = pressed[2]; calibration.invertClutch = false; }
            calibration.invertThrottle = calibration.invertBrake = false;
            PedalCalibrating = false; SaveCalibration();
            message = $"Đã lưu bàn đạp:  GA {released[0]}→{pressed[0]}   PHANH {released[1]}→{pressed[1]}" + (count == 3 ? $"   CÔN {released[2]}→{pressed[2]}" : "   (côn không dùng: số tự động)");
            return true;
        }

        static readonly int[] LearnOrder = { 1, 2, 3, 4, 5, 6 };
        public int GearLearnIndex { get; private set; } = -1;
        public bool GearLearning => GearLearnIndex >= 0;
        public string GearMessage { get; private set; } = "";
        int[] learnMap;
        int learnCandidate;
        double learnSince;
        public void BeginGearLearn()
        {
            learnMap = new int[7]; GearLearnIndex = 0; learnCandidate = 0; Hold();
            GearMessage = "Gạt cần vào số 1 và giữ yên…";
        }
        public void CancelGearLearn() { GearLearnIndex = -1; GearMessage = "Đã hủy học cần số."; }
        public void ResetGearMap() { calibration.gearMap = new[] { 0, 1, 2, 3, 4, 5, 6 }; SaveCalibration(); GearMessage = "Cần số về mặc định: công tắc U1…U5 = số 1…5, U6 = R."; }
        void StepGearLearn()
        {
            int target = LearnOrder[GearLearnIndex];
            if (!Connected) { GearMessage = "Chưa nhận dữ liệu buồng lái."; return; }
            GearMessage = $"Bước {GearLearnIndex + 1}/6 · Gạt cần vào số {RigProtocol.GearName(target)} và giữ yên…";
            int sw = Raw.gearFault ? 0 : Raw.switchGear;
            if (sw == 0 || learnMap[sw] != 0) { learnCandidate = 0; return; }
            if (sw != learnCandidate) { learnCandidate = sw; learnSince = Clock; return; }
            if (Clock - learnSince < .5) return;
            learnMap[sw] = target; GearLearnIndex++; learnCandidate = 0;
            if (GearLearnIndex < LearnOrder.Length) return;
            calibration.gearMap = learnMap; GearLearnIndex = -1; SaveCalibration();
            var parts = new List<string>();
            for (int s = 1; s <= 6; s++) parts.Add($"U{s}→{RigProtocol.GearName(learnMap[s])}");
            GearMessage = "Đã lưu cần số: " + string.Join("  ", parts);
        }

        public void SendFeedback(float torque, float speed, float rpm, int gear, float fuel, bool running, bool blink, float rumble)
        {
            if (socket == null || remote == null) return;
            if (!Connected || Held || Paused || !Focused) { torque = 0; rumble = 0; }
            byte[] b = RigProtocol.Feedback(calibration.Feedback(torque), speed, rpm, gear, fuel, running, Raw, blink, rumble, ++sequence);
            try { socket.Send(b, b.Length, remote); } catch (SocketException) { Held = true; }
        }
        public string Status => !InUse ? ""
            : !string.IsNullOrEmpty(Error) ? "BUỒNG LÁI • " + Error
            : !Connected ? (OnRigSubnet
                ? $"CHƯA KẾT NỐI BUỒNG LÁI • Không nhận được dữ liệu ESP32 (UDP {Port}). Kiểm tra nguồn control box, cáp LAN và tường lửa (chạy Thiet_Lap_Mang_Buong_Lai.bat). F10: lái bằng bàn phím"
                : "CHƯA KẾT NỐI BUỒNG LÁI • Cổng LAN của máy tính chưa có IP 192.168.1.100. Chạy Thiet_Lap_Mang_Buong_Lai.bat (quyền Admin). F10: lái bằng bàn phím")
            : Held ? "BUỒNG LÁI ĐANG KHÓA • " + Blocker : "";
        void OnDestroy() { Hold(); SendFeedback(0, 0, 0, 0, 0, false, false, 0); socket?.Close(); }
    }
}
