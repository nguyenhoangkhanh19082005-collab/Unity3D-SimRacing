using UnityEngine;

namespace SimRacing
{
    public sealed class SimulationUI : MonoBehaviour
    {
        SimulationApp app;
        GUIStyle heading, body, small, big;
        Texture2D panel;
        float fps;
        static readonly Color Ink = new Color(.90f, .94f, .96f), Muted = new Color(.73f, .8f, .85f), Accent = new Color(.29f, .88f, .72f);

        void Awake()
        {
            app = GetComponent<SimulationApp>();
        }

        void Start()
        {
            if (!GetComponent<SimulationMenuCanvas>())
            {
                gameObject.AddComponent<SimulationMenuCanvas>().Initialize(app);
            }
        }

        void Update()
        {
            fps = Mathf.Lerp(fps, 1 / Mathf.Max(.001f, Time.unscaledDeltaTime), .04f);
            SimulationMenuCanvas.Instance?.UpdateVisibility();
        }

        Texture2D Solid(Color color)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, color);
            t.Apply();
            return t;
        }

        GUIStyle Text(int size, Color color, FontStyle weight = FontStyle.Normal) =>
            new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight, normal = { textColor = color }, wordWrap = true };

        void Styles()
        {
            if (heading != null) return;
            panel = Solid(new Color(.025f, .045f, .065f, .93f));
            heading = Text(25, Ink, FontStyle.Bold);
            body = Text(18, Ink);
            small = Text(14, Muted);
            big = Text(52, Ink, FontStyle.Bold);
        }

        void Label(float x, float y, float w, float h, string text, GUIStyle style = null) =>
            GUI.Label(new Rect(x, y, w, h), text, style ?? body);

        void Panel(float x, float y, float w, float h) =>
            GUI.DrawTexture(new Rect(x, y, w, h), panel);

        /// <summary>
        /// OnGUI is used EXCLUSIVELY for the in-game HUD during driving sessions.
        /// Main Menu, Settings, Pause, Results, and Loading are all handled by Canvas UI (SimulationMenuCanvas).
        /// </summary>
        void OnGUI()
        {
            if (app != null && app.ScreenState == "drive")
            {
                Styles();
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
                Hud();
            }
        }

        void Hud()
        {
            if (!app.Car || !app.Session) return;
            var car = app.Car;
            var session = app.Session;

            Panel(22, 18, 660, 68);
            Label(38, 28, 620, 27, session.Objective, body);
            Label(38, 57, 620, 22, $"{app.Weather.ToUpperInvariant()}   ·   {session.Elapsed:0.0} s   ·   {session.Distance / 1000:0.00} km", small);
            Label(1095, 28, 165, 30, $"{fps:0} FPS  ·  {app.Rig.PacketsPerSecond} Hz", small);

            if (!string.IsNullOrEmpty(app.Rig.Status))
            {
                Panel(225, 96, 830, 62);
                Label(245, 111, 790, 46, app.Rig.Status, body);
            }

            if (!session.Active && !session.Finished && !app.Rig.Held)
            {
                Label(600, 210, 180, 90, Mathf.CeilToInt(app.Countdown).ToString(), big);
            }

            if (app.CameraRig.Mode != 0)
            {
                Panel(1000, 526, 258, 148);
                Label(1020, 536, 175, 65, car.Speed.ToString("0"), big);
                Label(1155, 553, 90, 35, "km/h", small);
                Label(1020, 604, 220, 40, $"{(car.Gear == 0 ? "N" : car.Gear == 6 ? "R" : car.Gear.ToString())}    {car.RPM:0} rpm", heading);
            }

            Panel(22, 584, 320, 91);
            Label(38, 594, 290, 25, $"XĂNG {car.Fuel:0} L   ·   XE {car.Health:0}%", body);
            Label(38, 627, 288, 38, car.Refueling ? "ĐANG TIẾP NHIÊN LIỆU" : car.EngineRunning ? "ĐỘNG CƠ ĐANG CHẠY" : "ĐỀ MÁY TỪ RIG", small);
            Label(375, 678, 870, 30, "F1–F2 / V: góc nhìn     F9: mở khóa rig     Backspace: cứu hộ khi dừng     Esc: tạm dừng", small);

            if (session.Notice != "")
            {
                Panel(300, 172, 680, 48);
                Label(319, 184, 644, 38, session.Notice, body);
            }

            if (car.Refueling || (Vector3.Distance(car.transform.position, app.Track.fuelStation) < 15 && !car.Refueling))
            {
                Label(385, 620, 550, 45, car.Refueling ? "Đang đổ xăng…" : "Dừng tại trạm, tắt máy và giữ phanh để đổ xăng", body);
            }

            MiniMap();
        }

        void MiniMap()
        {
            if (!app.Track || !app.Car) return;
            Panel(1085, 92, 173, 173);
            var center = new Vector2(1171, 178);
            float scale = .065f;
            Color old = GUI.color;
            GUI.color = Muted;
            foreach (var point in app.Track.route)
            {
                Vector3 d = point - app.Car.transform.position;
                float x = Mathf.Clamp(d.x * scale, -76, 76), y = Mathf.Clamp(-d.z * scale, -76, 76);
                GUI.DrawTexture(new Rect(center.x + x, center.y + y, 2, 2), Texture2D.whiteTexture);
            }
            GUI.color = Accent;
            GUI.DrawTexture(new Rect(center.x - 3, center.y - 3, 6, 6), Texture2D.whiteTexture);
            GUI.color = old;
        }

        void OnDestroy()
        {
            if (panel) Destroy(panel);
        }
    }
}
