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
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
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

            bool keyboard = app.Control == ControlMode.Keyboard;
            string weather = SimulationApp.WeatherName(app.ActiveWeather) + (app.Weather == "random" ? " (ngẫu nhiên)" : "");
            Panel(22, 18, 660, 68);
            Label(38, 28, 620, 27, session.Objective, body);
            Label(38, 57, 620, 22, $"{weather.ToUpperInvariant()}   ·   {session.Elapsed:0.0} s   ·   {session.Distance / 1000:0.00} km", small);
            Label(925, 28, 335, 30, keyboard ? $"BÀN PHÍM   ·   {fps:0} FPS" : $"BUỒNG LÁI {app.Rig.PacketsPerSecond} Hz   ·   {fps:0} FPS", small);

            if (!string.IsNullOrEmpty(app.Rig.Status))
            {
                Panel(225, 96, 830, 62);
                Label(245, 111, 790, 46, app.Rig.Status, body);
            }

            if (!session.Active && !session.Finished && (keyboard || !app.Rig.Held))
            {
                Label(600, 210, 180, 90, Mathf.CeilToInt(Mathf.Max(.01f, app.Countdown)).ToString(), big);
            }
            else if (app.Race && session.Active && session.Elapsed < 1.5f)
            {
                Label(520, 210, 400, 90, "XUẤT PHÁT!", big);
            }

            if (app.Race) Standings(app.Race);

            if (app.CameraRig.Mode != 0)
            {
                Panel(1000, 526, 258, 148);
                Label(1020, 536, 175, 65, car.Speed.ToString("0"), big);
                Label(1155, 553, 90, 35, "km/h", small);
                Label(1020, 604, 220, 40, $"{(car.Gear == 0 ? "N" : car.Gear == 6 ? "R" : car.Gear.ToString())}    {car.RPM:0} rpm", heading);
            }

            Panel(22, 584, 320, 91);
            Label(38, 594, 290, 25, $"XĂNG {car.Fuel:0} L   ·   XE {car.Health:0}%", body);
            Label(38, 627, 288, 38, car.Refueling ? "ĐANG TIẾP NHIÊN LIỆU" : car.EngineRunning ? "ĐỘNG CƠ ĐANG CHẠY" : keyboard ? "CHẾ ĐỘ BÀN PHÍM" : "BẬT KHÓA VÀ ĐỀ MÁY TỪ BUỒNG LÁI", small);
            Label(250, 678, 1020, 30, keyboard
                ? "W/S: Ga · Phanh/Lùi    A/D: Lái    Space: Phanh    H: Đèn    V: Góc nhìn    F10: Sang buồng lái    Esc: Tạm dừng"
                : "Lái bằng buồng lái thật    F9: Mở khóa    V: Góc nhìn    Backspace: Cứu hộ    F10: Sang bàn phím    Esc: Tạm dừng", small);

            if (session.Notice != "")
            {
                Panel(300, 172, 680, 48);
                Label(319, 184, 644, 38, session.Notice, body);
            }

            if (car.Refueling || (Vector3.Distance(car.transform.position, app.Track.fuelStation) < 15))
            {
                string fuelMsg = car.Refueling 
                    ? "Đang nạp lại xăng… (Nhấn R để dừng)" 
                    : car.Speed < 0.5f 
                        ? "Dừng xe và nhấn R để nạp lại xăng" 
                        : "Dừng xe về 0 km/h và nhấn R để nạp lại xăng";
                Label(385, 620, 550, 45, fuelMsg, body);
            }

            MiniMap();
        }

        void MiniMap()
        {
            if (!app.Track || !app.Car) return;
            Panel(1085, 92, 173, 173);
            var center = new Vector2(1171, 178);
            var route = app.Track.route;
            if (route == null || route.Length == 0) return;
            if (mapRoute != route)
            {
                // Fit the whole track into the panel once per route
                mapRoute = route;
                Vector2 min = new Vector2(route[0].x, route[0].z), max = min;
                foreach (var p in route) { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
                mapCenter = (min + max) * .5f;
                mapScale = 150f / Mathf.Max(1f, Mathf.Max(max.x - min.x, max.y - min.y));
            }
            Color old = GUI.color;
            GUI.color = Muted;
            int step = route.Length > 300 ? 3 : route.Length > 100 ? 2 : 1;
            for (int i = 0; i < route.Length; i += step)
            {
                Vector2 m = MapPoint(route[i], center);
                GUI.DrawTexture(new Rect(m.x - 1.5f, m.y - 1.5f, 3, 3), Texture2D.whiteTexture);
            }
            if (app.Race)
                foreach (var entry in app.Race.Entries)
                {
                    if (!entry.Ai) continue;
                    GUI.color = entry.Paint;
                    Vector2 ai = MapPoint(entry.Ai.transform.position, center);
                    GUI.DrawTexture(new Rect(ai.x - 3f, ai.y - 3f, 6, 6), Texture2D.whiteTexture);
                }
            GUI.color = Accent;
            Vector2 car = MapPoint(app.Car.transform.position, center);
            GUI.DrawTexture(new Rect(car.x - 3.5f, car.y - 3.5f, 7, 7), Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Standings(RaceController race)
        {
            var standings = race.Standings;
            float y = 236, h = 34 + standings.Count * 24;
            Panel(22, y, 330, h);
            Label(36, y + 6, 310, 22, $"BẢNG XẾP HẠNG  ·  {race.Laps} VÒNG  ·  AI {RaceController.DifficultyNames[race.Difficulty].ToUpperInvariant()}", small);
            for (int i = 0; i < standings.Count; i++)
            {
                var e = standings[i];
                string right = e.Finished ? RaceController.FormatTime(e.FinishTime)
                    : e.IsPlayer ? $"vòng {race.LapOf(e)}"
                    : $"{(race.Gap(race.PlayerEntry, e) >= 0 ? "+" : "−")}{Mathf.Abs(race.Gap(race.PlayerEntry, e)):0} m";
                Color old = GUI.color;
                if (e.IsPlayer) { GUI.color = new Color(1, 1, 1, .12f); GUI.DrawTexture(new Rect(26, y + 30 + i * 24, 322, 23), Texture2D.whiteTexture); }
                GUI.color = e.Paint;
                GUI.DrawTexture(new Rect(36, y + 37 + i * 24, 8, 10), Texture2D.whiteTexture);
                GUI.color = old;
                Label(52, y + 30 + i * 24, 190, 24, $"{i + 1}.  {(e.IsPlayer ? "BẠN" : e.Name)}", e.IsPlayer ? heading14 : small);
                Label(240, y + 30 + i * 24, 105, 24, right, small);
            }
        }

        GUIStyle heading14Style;
        GUIStyle heading14 => heading14Style ??= Text(15, Ink, FontStyle.Bold);

        Vector3[] mapRoute;
        Vector2 mapCenter;
        float mapScale;

        Vector2 MapPoint(Vector3 world, Vector2 center)
        {
            float x = Mathf.Clamp((world.x - mapCenter.x) * mapScale, -80, 80), y = Mathf.Clamp(-(world.z - mapCenter.y) * mapScale, -80, 80);
            return center + new Vector2(x, y);
        }

        void OnDestroy()
        {
            if (panel) Destroy(panel);
        }
    }
}
