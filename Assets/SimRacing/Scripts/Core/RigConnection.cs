using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace SimRacing
{
    public sealed class RigConnection : MonoBehaviour
    {
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
        /// <summary>True when the player chose the physical cockpit as the control mode.</summary>
        public bool InUse = true;
        UdpClient socket;
        IPEndPoint remote;
        double lastPacket = double.NegativeInfinity, counterStart;
        uint lastStamp;
        int count;
        ushort sequence;
        bool wasConnected;
        public Func<float> VehicleSpeed = () => 0;
        static double Clock => Time.realtimeSinceStartupAsDouble;
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            calibration = LocalStore.Load("rig-settings.json", new RigCalibration(), c => c.Valid);
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("--rig-test-port=") && int.TryParse(arg.Substring(16), out int port)) Port = port;
            if(Port==9999 && Array.Exists(Environment.GetCommandLineArgs(),arg=>arg=="--verify")) Port = 18888;
            try {
                socket = new UdpClient(new IPEndPoint(Port == 9999 ? IPAddress.Any : IPAddress.Loopback, Port));
                socket.Client.Blocking = false;
            } catch (SocketException e) { Error = "Không mở được cổng rig: " + e.SocketErrorCode; }
        }
        public bool Accept(byte[] packet, IPEndPoint sender, double now)
        {
            if (!RigProtocol.TryDecode(packet, calibration, out RigSample sample)) return false;
            bool lease = remote != null && now - lastPacket < .25;
            if (lease && (!sender.Equals(remote) || !RigProtocol.Newer(sample.stamp, lastStamp))) return false;
            if (!lease) Held = true;
            lastStamp = sample.stamp; Raw = sample; remote = sender; lastPacket = now; count++;Error="";
            if (sample.gearFault) Held = true;
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
                } catch (SocketException e) { if (e.SocketErrorCode != SocketError.WouldBlock) Error = e.SocketErrorCode.ToString(); }
            }
            bool isVerify = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--verify");
            if (isVerify) Focused = true;
            bool connected = Connected;
            if (!connected || Paused || !Focused) Hold();
            if (wasConnected && !connected) {SendFeedback(0,0,0,0,0,false,false,0);remote=null;Raw = RigSample.Held;}
            wasConnected = connected;
            if (Input.GetKeyDown(KeyCode.F9)) Resume();
            if (Held) SendFeedback(0, 0, 0, 0, 0, false, false, 0);
            if (Clock - counterStart >= 1) { PacketsPerSecond = count; count = 0; counterStart = Clock; }
        }
        public void Hold() { Held = true; }
        public bool Resume()
        {
            if (!Held || Paused || !Focused || !Connected || !Raw.Ready || VehicleSpeed() > 1) return false;
            Held = false; return true;
        }
        public void SetPaused(bool paused) { Paused = paused; Hold(); SendFeedback(0,0,0,0,0,false,false,0); }
        void OnApplicationFocus(bool focus)
        {
            if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "--verify"))
            {
                Focused = true;
                return;
            }
            Focused = focus;
            if (!focus) { Hold(); SendFeedback(0,0,0,0,0,false,false,0); }
        }
        public void SaveCalibration() { if (calibration.Valid) LocalStore.Save("rig-settings.json", calibration); Hold(); }
        public void SendFeedback(float torque, float speed, float rpm, int gear, float fuel, bool running, bool blink, float rumble)
        {
            if (socket == null || remote == null) return;
            if (!Connected || Held || Paused || !Focused) { torque = 0; rumble = 0; }
            byte[] b = RigProtocol.Feedback(calibration.Feedback(torque), speed, rpm, gear, fuel, running, Raw, blink, rumble, ++sequence);
            try { socket.Send(b, b.Length, remote); } catch (SocketException) { Held = true; }
        }
        public string Status => !InUse ? ""
            : !string.IsNullOrEmpty(Error) ? "BUỒNG LÁI • " + Error
            : !Connected ? $"CHƯA KẾT NỐI BUỒNG LÁI • Chưa nhận dữ liệu ESP32 qua UDP {Port}. Kiểm tra nguồn/Wi-Fi, hoặc F10 để lái bằng bàn phím"
            : Raw.gearFault ? "LỖI CẦN SỐ • Về N, nhả ga, giữ phanh rồi F9"
            : Held ? "BUỒNG LÁI TẠM KHÓA • Về N, nhả ga, giữ phanh rồi nhấn F9" : "";
        void OnDestroy() { Hold(); SendFeedback(0,0,0,0,0,false,false,0); socket?.Close(); }
    }
}
