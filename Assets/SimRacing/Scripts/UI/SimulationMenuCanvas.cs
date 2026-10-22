using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimRacing
{
    public sealed class SimulationMenuCanvas : MonoBehaviour
    {
        public static SimulationMenuCanvas Instance;
        private SimulationApp app;

        private GameObject canvasObject;
        private Canvas canvas;
        private Font uiFont;
        private Sprite panelSprite;
        private Sprite buttonSprite;

        // Roots for distinct screens
        private GameObject menuScreen;
        private GameObject settingsScreen;
        private GameObject pauseScreen;
        private GameObject resultsScreen;
        private GameObject loadingScreen;

        // Header elements
        private Text profileText;

        // Mode content
        private GameObject freeRaceSection;
        private GameObject careerSection;

        // Dynamic buttons & text
        private readonly List<Button> modeButtons = new List<Button>();
        private readonly List<Button> carButtons = new List<Button>();
        private readonly List<Button> trackButtons = new List<Button>();
        private readonly List<Button> weatherButtons = new List<Button>();
        private readonly List<Button> careerButtons = new List<Button>();
        private readonly List<Button> controlButtons = new List<Button>();
        private readonly List<Button> difficultyButtons = new List<Button>();
        private readonly List<Button> lapButtons = new List<Button>();
        private GameObject raceOptions;
        private Text pauseControlLabel;
        private Text resultsStandings;
        private static readonly int[] LapChoices = { 1, 3, 5 };
        private Text carSpecsText;
        private Text modeDescText;
        private Text rigMessageText;
        private Text careerObjectiveText;
        private Button startButton;

        // Settings dynamic elements
        private readonly List<Button> qualityButtons = new List<Button>();
        private Slider motionSlider;
        private Text motionLabel;
        private Text rigInfoLabel;
        private Slider steerRangeSlider;
        private Text steerRangeLabel;
        private Toggle invertSteerToggle;
        private Toggle invertThrottleToggle;
        private Toggle invertBrakeToggle;
        private Toggle invertClutchToggle;
        private Text livePedalsText;

        // Results dynamic elements
        private Text resultsTitle;
        private Text resultsStars;
        private Text resultsStats;
        private Text resultsFeedback;

        // Colors
        private static readonly Color BgColor = new Color(0.02f, 0.04f, 0.06f, 0.85f);
        private static readonly Color CardBg = new Color(0.06f, 0.09f, 0.13f, 0.88f);
        private static readonly Color CardSelected = new Color(0.09f, 0.28f, 0.29f, 0.98f);
        private static readonly Color AccentColor = new Color(0.22f, 0.88f, 0.74f, 1f);
        private static readonly Color TextInk = new Color(0.92f, 0.95f, 0.98f, 1f);
        private static readonly Color TextMuted = new Color(0.65f, 0.72f, 0.78f, 1f);
        private static readonly Color IgnitionRed = new Color(0.86f, 0.15f, 0.10f, 1f);

        public void Initialize(SimulationApp simulationApp)
        {
            if (canvasObject) return;
            Instance = this;
            app = simulationApp;

            EnsureEventSystem();
            CreateSprites();
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Tahoma" }, 16);

            // Root Canvas
            canvasObject = new GameObject("SimulationMenuCanvas");
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            BuildHeader();
            BuildMainMenu();
            BuildSettingsMenu();
            BuildPauseMenu();
            BuildResultsMenu();
            BuildLoadingScreen();

            UpdateVisibility();
        }

        private void EnsureEventSystem()
        {
            if (!FindFirstObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        private void CreateSprites()
        {
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            panelSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            buttonSprite = panelSprite;
        }

        private void BuildHeader()
        {
            var header = CreatePanel(canvasObject.transform, new Color(0.015f, 0.025f, 0.04f, 0.88f),
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -68), new Vector2(0, 0));

            // Logo & Title
            var title = CreateText(header.transform, "SIM / RACING", 26, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(40, 0), new Vector2(300, 0));

            // Driver Profile info
            profileText = CreateText(header.transform, "", 16, TextMuted, FontStyle.Normal, TextAnchor.MiddleRight);
            SetRect(profileText.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(-40, 0));

            // Cyan neon line
            var line = CreatePanel(canvasObject.transform, new Color(0.2f, 0.85f, 0.75f, 0.6f),
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(0, -68));
        }

        private void BuildMainMenu()
        {
            menuScreen = new GameObject("MainMenuScreen");
            menuScreen.transform.SetParent(canvasObject.transform, false);
            SetStretch(menuScreen.AddComponent<RectTransform>());

            // Left Navigation Sidebar
            var sidebar = CreatePanel(menuScreen.transform, BgColor,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(30, 35), new Vector2(330, -90));

            var sideTitle = CreateText(sidebar.transform, "CHẾ ĐỘ ĐUA", 14, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(sideTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(25, -45), new Vector2(-25, -20));

            string[] modes = { "Lái tự do", "Đua tính giờ", "Đua tranh hạng", "Career" };
            string[] modeIds = { "free", "race", "versus", "career" };
            for (int i = 0; i < modes.Length; i++)
            {
                int idx = i;
                var btn = CreateStyledButton(sidebar.transform, modes[i], 18, () =>
                {
                    app.Mode = modeIds[idx];
                    RefreshMenuContent();
                });
                SetRect(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(20, -60 - (i + 1) * 65), new Vector2(-20, -60 - i * 65 - 10));
                modeButtons.Add(btn);
            }

            // Settings & Exit in sidebar footer
            var settingsBtn = CreateStyledButton(sidebar.transform, "Thiết lập rig & hình ảnh", 16, () =>
            {
                app.ScreenState = "settings";
                UpdateVisibility();
            });
            SetRect(settingsBtn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(20, 80), new Vector2(-20, 135));

            var exitBtn = CreateStyledButton(sidebar.transform, "Thoát", 16, () => Application.Quit());
            SetRect(exitBtn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(20, 20), new Vector2(-20, 70));

            // Main Content Area
            var contentArea = new GameObject("ContentArea");
            contentArea.transform.SetParent(menuScreen.transform, false);
            var contentRt = contentArea.AddComponent<RectTransform>();
            SetRect(contentRt, new Vector2(0, 0), new Vector2(1, 1), new Vector2(360, 35), new Vector2(-40, -90));

            // Dramatic Banner Texts at Top
            var mainTitle = CreateText(contentArea.transform, "CẢM NHẬN TỪNG KHÚC CUA", 38, TextInk, FontStyle.Bold, TextAnchor.UpperLeft);
            SetRect(mainTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -45), new Vector2(0, 0));

            var subtitle = CreateText(contentArea.transform, "Vô lăng thật. Số tay. Một chuyến đi của bạn.", 18, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            SetRect(subtitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -80), new Vector2(0, -50));

            // 360 inspection hint
            var dragHint = CreateText(contentArea.transform, "⟳ Kéo chuột để xoay xe 360°", 15, new Color(0.2f, 0.88f, 1f, 0.85f), FontStyle.Normal, TextAnchor.UpperRight);
            SetRect(dragHint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -80), new Vector2(-15, -50));

            // Free & Race Section
            freeRaceSection = new GameObject("FreeRaceSection");
            freeRaceSection.transform.SetParent(contentArea.transform, false);
            SetStretch(freeRaceSection.AddComponent<RectTransform>());

            // Floating Car Specs Badge (sits right above bottom control dock)
            var specsCard = CreatePanel(freeRaceSection.transform, new Color(0.04f, 0.07f, 0.11f, 0.78f),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(15, 360), new Vector2(-15, 415));

            carSpecsText = CreateText(specsCard.transform, "", 16, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(carSpecsText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 0), new Vector2(-20, 0));

            // Bottom Control Dock (holds car, track, weather selection & launch button)
            var bottomDock = CreatePanel(freeRaceSection.transform, new Color(0.02f, 0.04f, 0.07f, 0.88f),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 345));

            // Car Selection Row
            var carLabel = CreateText(bottomDock.transform, "CHỌN XE", 13, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(carLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -32), new Vector2(0, -10));

            string[] carNames = { "Mercedes W210", "Ferrari F40", "Bugatti Centodieci" };
            string[] carIds = { "mercedes", "ferrari", "bugatti" };
            float carBtnW = 270f;
            for (int i = 0; i < carNames.Length; i++)
            {
                int idx = i;
                var btn = CreateStyledButton(bottomDock.transform, carNames[i], 17, () =>
                {
                    app.CarId = carIds[idx];
                    ShowcaseStage.Instance?.ShowCar(app.CarId);
                    RefreshMenuContent();
                });
                SetRect(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(20 + i * (carBtnW + 15), -92), new Vector2(20 + i * (carBtnW + 15) + carBtnW, -38));
                carButtons.Add(btn);
            }

            // Track Selection Row
            var trackLabel = CreateText(bottomDock.transform, "CUNG ĐƯỜNG", 13, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(trackLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -120), new Vector2(0, -98));

            string[] trackNames = { "Đèo Tuyết Sơn Cước", "Trường Đua Hoạt Hình Oval" };
            string[] trackIds = { "coastal", "mountain" };
            float trackBtnW = 230f;
            for (int i = 0; i < trackNames.Length; i++)
            {
                int idx = i;
                var btn = CreateStyledButton(bottomDock.transform, trackNames[i], 15, () =>
                {
                    app.TrackId = trackIds[idx];
                    RefreshMenuContent();
                });
                SetRect(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(20 + i * (trackBtnW + 12), -178), new Vector2(20 + i * (trackBtnW + 12) + trackBtnW, -128));
                trackButtons.Add(btn);
            }

            // Weather Selection Row
            string[] weatherLabels = { "Ngẫu nhiên", "Nắng", "Mưa", "Sương mù", "Đêm" };
            string[] weatherIds = { "random", "clear", "rain", "fog", "night" };
            float weatherW = 100f;
            for (int i = 0; i < weatherLabels.Length; i++)
            {
                int idx = i;
                var btn = CreateStyledButton(bottomDock.transform, weatherLabels[i], 15, () =>
                {
                    app.Weather = weatherIds[idx];
                    RefreshMenuContent();
                });
                SetRect(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(520 + i * (weatherW + 10), -178), new Vector2(520 + i * (weatherW + 10) + weatherW, -128));
                weatherButtons.Add(btn);
            }

            // Big Pulsing Ignition / Start Button
            // Control Mode Row: physical ESP32 cockpit or keyboard (also switchable in-game with F10 / pause menu)
            var controlLabel = CreateText(bottomDock.transform, "ĐIỀU KHIỂN", 13, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(controlLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -210), new Vector2(0, -188));
            ControlMode[] controlModes = { ControlMode.Keyboard, ControlMode.Rig };
            for (int i = 0; i < controlModes.Length; i++)
            {
                var mode = controlModes[i];
                var btn = CreateStyledButton(bottomDock.transform, SimulationApp.ControlName(mode), 15, () =>
                {
                    app.SetControl(mode);
                    app.SavePreferences();
                    RefreshMenuContent();
                });
                SetRect(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(20 + i * 242, -262), new Vector2(20 + i * 242 + 230, -215));
                controlButtons.Add(btn);
            }

            // Grid race options (only for "Đua tranh hạng")
            raceOptions = new GameObject("RaceOptions");
            raceOptions.transform.SetParent(bottomDock.transform, false);
            SetStretch(raceOptions.AddComponent<RectTransform>());
            var aiLabel = CreateText(raceOptions.transform, "ĐỐI THỦ AI", 13, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(aiLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(520, -210), new Vector2(800, -188));
            for (int i = 0; i < RaceController.DifficultyNames.Length; i++)
            {
                int level = i;
                var btn = CreateStyledButton(raceOptions.transform, RaceController.DifficultyNames[i], 15, () =>
                {
                    app.RaceDifficulty = level;
                    RefreshMenuContent();
                });
                SetRect(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(520 + i * 98, -262), new Vector2(520 + i * 98 + 88, -215));
                difficultyButtons.Add(btn);
            }
            var lapsLabel = CreateText(raceOptions.transform, "SỐ VÒNG", 13, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(lapsLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(830, -210), new Vector2(1100, -188));
            for (int i = 0; i < LapChoices.Length; i++)
            {
                int laps = LapChoices[i];
                var btn = CreateStyledButton(raceOptions.transform, laps + " vòng", 15, () =>
                {
                    app.RaceLaps = laps;
                    RefreshMenuContent();
                });
                SetRect(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(830 + i * 98, -262), new Vector2(830 + i * 98 + 88, -215));
                lapButtons.Add(btn);
            }

            startButton = CreateStyledButton(bottomDock.transform, "BẮT ĐẦU CHUYẾN ĐI", 20, () => app.StartDrive(), IgnitionRed);
            SetRect(startButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-360, -262), new Vector2(-20, -50));

            // Dock Footer info texts
            modeDescText = CreateText(bottomDock.transform, "", 14, TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(modeDescText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 42), new Vector2(-380, 72));

            rigMessageText = CreateText(bottomDock.transform, "", 13, new Color(0.4f, 0.75f, 0.85f), FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(rigMessageText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 14), new Vector2(-380, 42));

            // Career Section
            careerSection = new GameObject("CareerSection");
            careerSection.transform.SetParent(contentArea.transform, false);
            SetStretch(careerSection.AddComponent<RectTransform>());

            var chalHeader = CreateText(careerSection.transform, "THỬ THÁCH SỰ NGHIỆP", 14, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(chalHeader.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -130), new Vector2(0, -105));

            for (int i = 0; i < DriverProgress.Challenges.Length; i++)
            {
                int idx = i;
                var btn = CreateStyledButton(careerSection.transform, "", 16, () =>
                {
                    app.ChallengeIndex = idx;
                    RefreshMenuContent();
                });
                SetRect(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(15, -190 - i * 58), new Vector2(620, -140 - i * 58));
                careerButtons.Add(btn);
            }

            // Career Objective Panel
            var objPanel = CreatePanel(careerSection.transform, CardBg,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(650, -320), new Vector2(1050, -140));

            careerObjectiveText = CreateText(objPanel.transform, "", 18, TextInk, FontStyle.Bold, TextAnchor.UpperLeft);
            SetRect(careerObjectiveText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(25, 25), new Vector2(-25, -25));

            var careerStartBtn = CreateStyledButton(careerSection.transform, "BẮT ĐẦU", 22, () => app.StartDrive(), IgnitionRed);
            SetRect(careerStartBtn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(650, -400), new Vector2(1050, -335));
        }

        private void BuildSettingsMenu()
        {
            settingsScreen = new GameObject("SettingsScreen");
            settingsScreen.transform.SetParent(canvasObject.transform, false);
            SetStretch(settingsScreen.AddComponent<RectTransform>());

            var panel = CreatePanel(settingsScreen.transform, BgColor,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(80, 50), new Vector2(-80, -100));

            var title = CreateText(panel.transform, "THIẾT LẬP", 32, TextInk, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -70), new Vector2(400, -20));

            // Left column: Visuals & Instructions
            var qLabel = CreateText(panel.transform, "Chất lượng hình ảnh", 20, TextInk, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(qLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -120), new Vector2(400, -90));

            string[] qNames = { "Nhẹ", "Cân bằng", "Cao" };
            for (int i = 0; i < 3; i++)
            {
                int q = i;
                var btn = CreateStyledButton(panel.transform, qNames[i], 16, () =>
                {
                    app.Quality = q;
                    app.ApplyQuality();
                    RefreshSettingsUI();
                });
                SetRect(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(40 + i * 150, -180), new Vector2(40 + i * 150 + 135, -135));
                qualityButtons.Add(btn);
            }

            motionLabel = CreateText(panel.transform, "Chuyển động đầu", 18, TextInk, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(motionLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -230), new Vector2(500, -200));

            motionSlider = CreateSlider(panel.transform, val =>
            {
                app.CameraMotion = val;
                RefreshSettingsUI();
            });
            SetRect(motionSlider.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, -270), new Vector2(490, -245));

            var info1 = CreateText(panel.transform, "Chế độ điều khiển (Bàn phím / Buồng lái thật) chọn ở màn hình chính, đổi khi đang lái bằng F10.", 16, TextInk, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(info1.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -340), new Vector2(520, -290));

            var info2 = CreateText(panel.transform, "Khởi hành: giữ phanh, về N, nhả ga → F9 → bật khóa và đề máy → đạp côn, vào số 1 và nhả côn từ từ.", 15, TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(info2.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -440), new Vector2(520, -350));

            // Right column: Rig Calibration
            rigInfoLabel = CreateText(panel.transform, "Rig UDP 9999", 20, TextInk, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(rigInfoLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(560, -120), new Vector2(-40, -90));

            steerRangeLabel = CreateText(panel.transform, "Nửa hành trình vô lăng", 16, TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(steerRangeLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(560, -165), new Vector2(-40, -135));

            steerRangeSlider = CreateSlider(panel.transform, val =>
            {
                if (app?.Rig?.calibration != null)
                {
                    app.Rig.calibration.steeringHalfRangeTicks = val;
                    RefreshSettingsUI();
                }
            }, 1000, 20000);
            SetRect(steerRangeSlider.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(560, -200), new Vector2(-40, -175));

            var setCenterBtn = CreateStyledButton(panel.transform, "Đặt vị trí giữa hiện tại", 16, () =>
            {
                if (app?.Rig != null)
                {
                    app.Rig.calibration.steeringCenterTicks = app.Rig.Raw.encoder;
                    RefreshSettingsUI();
                }
            });
            SetRect(setCenterBtn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(560, -260), new Vector2(850, -215));

            invertSteerToggle = CreateToggle(panel.transform, " Đảo chiều vô lăng", val =>
            {
                if (app?.Rig?.calibration != null) app.Rig.calibration.invertSteering = val;
            });
            SetRect(invertSteerToggle.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(880, -260), new Vector2(1100, -220));

            // Invert pedals toggles
            invertThrottleToggle = CreateToggle(panel.transform, " Đảo chân ga", val =>
            {
                if (app?.Rig?.calibration != null) app.Rig.calibration.invertThrottle = val;
            });
            SetRect(invertThrottleToggle.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(560, -320), new Vector2(740, -280));

            invertBrakeToggle = CreateToggle(panel.transform, " Đảo phanh", val =>
            {
                if (app?.Rig?.calibration != null) app.Rig.calibration.invertBrake = val;
            });
            SetRect(invertBrakeToggle.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(750, -320), new Vector2(930, -280));

            invertClutchToggle = CreateToggle(panel.transform, " Đảo côn", val =>
            {
                if (app?.Rig?.calibration != null) app.Rig.calibration.invertClutch = val;
            });
            SetRect(invertClutchToggle.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(940, -320), new Vector2(1120, -280));

            livePedalsText = CreateText(panel.transform, "GA 0%  PHANH 0%  CÔN 0%", 18, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(livePedalsText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(560, -380), new Vector2(-40, -340));

            // Save & Return Button
            var saveBtn = CreateStyledButton(panel.transform, "LƯU VÀ TRỞ VỀ", 18, () =>
            {
                app.SavePreferences();
                app.ScreenState = "menu";
                UpdateVisibility();
            });
            SetRect(saveBtn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(40, 30), new Vector2(300, 85));
        }

        private void BuildPauseMenu()
        {
            pauseScreen = new GameObject("PauseScreen");
            pauseScreen.transform.SetParent(canvasObject.transform, false);
            SetStretch(pauseScreen.AddComponent<RectTransform>());

            // Modal card
            var card = CreatePanel(pauseScreen.transform, BgColor,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -240), new Vector2(300, 240));

            var title = CreateText(card.transform, "TẠM DỪNG", 28, TextInk, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(0, -20));

            var resumeBtn = CreateStyledButton(card.transform, "Tiếp tục", 18, () => app.Resume());
            SetRect(resumeBtn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(40, -135), new Vector2(-40, -80));

            var controlBtn = CreateStyledButton(card.transform, "", 17, () =>
            {
                app.ToggleControl();
                RefreshPauseUI();
            });
            SetRect(controlBtn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(40, -205), new Vector2(-40, -150));
            pauseControlLabel = controlBtn.GetComponentInChildren<Text>();

            var toMenuBtn = CreateStyledButton(card.transform, "Trở về gara", 18, () => app.ToMenu());
            SetRect(toMenuBtn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(40, -275), new Vector2(-40, -220));

            var hint = CreateText(card.transform, "Buồng lái: sau khi tiếp tục, về N, nhả ga, giữ phanh rồi nhấn F9.\nF10 đổi nhanh giữa bàn phím và buồng lái.", 15, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetRect(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 20), new Vector2(-20, 85));
        }

        private void BuildResultsMenu()
        {
            resultsScreen = new GameObject("ResultsScreen");
            resultsScreen.transform.SetParent(canvasObject.transform, false);
            SetStretch(resultsScreen.AddComponent<RectTransform>());

            var panel = CreatePanel(resultsScreen.transform, BgColor,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(100, 60), new Vector2(-100, -100));

            resultsTitle = CreateText(panel.transform, "KẾT QUẢ CHUYẾN ĐI", 36, TextInk, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(resultsTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -80), new Vector2(0, -20));

            resultsStars = CreateText(panel.transform, "", 24, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(resultsStars.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -150), new Vector2(0, -90));

            resultsStats = CreateText(panel.transform, "", 18, TextInk, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(resultsStats.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -220), new Vector2(0, -160));

            resultsFeedback = CreateText(panel.transform, "", 22, TextInk, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(resultsFeedback.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -320), new Vector2(0, -240));

            resultsStandings = CreateText(panel.transform, "", 19, TextInk, FontStyle.Normal, TextAnchor.UpperLeft);
            SetRect(resultsStandings.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -580), new Vector2(-50, -335));

            var toMenuBtn = CreateStyledButton(panel.transform, "TRỞ VỀ GARA", 20, () => app.ToMenu(), AccentColor);
            SetRect(toMenuBtn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(50, 40), new Vector2(350, 105));
        }

        private void BuildLoadingScreen()
        {
            loadingScreen = new GameObject("LoadingScreen");
            loadingScreen.transform.SetParent(canvasObject.transform, false);
            SetStretch(loadingScreen.AddComponent<RectTransform>());

            var panel = CreatePanel(loadingScreen.transform, new Color(0.02f, 0.03f, 0.05f, 0.95f),
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);

            var title = CreateText(panel.transform, "ĐANG CHUẨN BỊ CHUYẾN ĐI", 36, TextInk, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400, 20), new Vector2(400, 80));

            var sub = CreateText(panel.transform, "Đường đua, xe và điều kiện thời tiết…", 20, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetRect(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400, -40), new Vector2(400, 20));
        }

        public void UpdateVisibility()
        {
            if (!app) return;
            string state = app.ScreenState;

            bool isDriving = state == "drive";
            canvasObject.SetActive(!isDriving);

            if (isDriving) return;

            menuScreen.SetActive(state == "menu");
            settingsScreen.SetActive(state == "settings");
            pauseScreen.SetActive(state == "pause");
            resultsScreen.SetActive(state == "results");
            loadingScreen.SetActive(state == "loading");

            if (state == "menu") RefreshMenuContent();
            else if (state == "settings") RefreshSettingsUI();
            else if (state == "results") RefreshResultsUI();
            else if (state == "pause") RefreshPauseUI();
        }

        private void RefreshMenuContent()
        {
            if (!app) return;

            // Profile in header
            if (profileText != null && app.Profile != null)
            {
                profileText.text = $"CẤP {1 + app.Profile.xp / 500}     {app.Profile.Stars} SAO     {app.Profile.credits:N0} CR";
            }

            // Mode navigation buttons
            string[] modeIds = { "free", "race", "versus", "career" };
            for (int i = 0; i < modeButtons.Count; i++)
            {
                SetButtonActive(modeButtons[i], app.Mode == modeIds[i]);
            }

            bool isCareer = app.Mode == "career";
            freeRaceSection.SetActive(!isCareer);
            careerSection.SetActive(isCareer);

            if (!isCareer)
            {
                // Car buttons
                string[] carIds = { "mercedes", "ferrari", "bugatti" };
                string[] carNames = { "Mercedes W210", "Ferrari F40", "Bugatti Centodieci" };
                for (int i = 0; i < carButtons.Count; i++)
                {
                    bool locked = i == 2 && app.Profile.Stars < 7;
                    var txt = carButtons[i].GetComponentInChildren<Text>();
                    if (txt) txt.text = carNames[i] + (locked ? " · 7 sao" : "");
                    SetButtonActive(carButtons[i], app.CarId == carIds[i]);
                }

                // Car specs badge
                if (carSpecsText)
                {
                    if (app.CarId.Contains("ferrari"))
                        carSpecsText.text = "⚡ Ferrari F40: 577 Nm · 7750 RPM · 1100 kg · Cầu sau (RWD) · Twin-Turbo V8";
                    else if (app.CarId.Contains("bugatti"))
                        carSpecsText.text = "⚡ Bugatti Centodieci: 1600 Nm · 6700 RPM · 1978 kg · Dẫn động 4 bánh (AWD) · W16 Quad-Turbo";
                    else
                        carSpecsText.text = "⚡ Mercedes-Benz W210: 315 Nm · 6500 RPM · 1540 kg · Cầu sau (RWD) · Sedan Đức";
                }

                // Track buttons
                string[] trackIds = { "coastal", "mountain" };
                for (int i = 0; i < trackButtons.Count; i++)
                {
                    SetButtonActive(trackButtons[i], app.TrackId == trackIds[i]);
                }

                // Weather buttons
                string[] weatherIds = { "random", "clear", "rain", "fog", "night" };
                for (int i = 0; i < weatherButtons.Count; i++)
                {
                    SetButtonActive(weatherButtons[i], app.Weather == weatherIds[i]);
                }

                for (int i = 0; i < controlButtons.Count; i++)
                    SetButtonActive(controlButtons[i], (int)app.Control == i);
                bool versus = app.Mode == "versus";
                raceOptions.SetActive(versus);
                for (int i = 0; i < difficultyButtons.Count; i++)
                    SetButtonActive(difficultyButtons[i], app.RaceDifficulty == i);
                for (int i = 0; i < lapButtons.Count; i++)
                    SetButtonActive(lapButtons[i], app.RaceLaps == LapChoices[i]);

                if (modeDescText)
                {
                    modeDescText.text = versus
                        ? $"Xuất phát từ lưới cùng 5 tay đua AI · {app.RaceLaps} vòng · Về đích trước để giành chiến thắng"
                        : app.Mode == "race"
                        ? "3 vòng trên tuyến đóng · Đồng hồ vòng và ghost thành tích tốt nhất"
                        : "Giao thông hai chiều · Tiếp nhiên liệu · Cứu hộ khi dừng xe";
                }

                if (rigMessageText)
                {
                    rigMessageText.text = !string.IsNullOrEmpty(app.Message) ? app.Message
                        : app.Control == ControlMode.Keyboard
                        ? "Bàn phím · W/S ga-phanh, A/D lái, H đèn · F10 trong lúc lái để đổi sang buồng lái"
                        : $"Buồng lái thật · ESP32 gửi UDP {app.Rig.Port} · {(app.Rig.Connected ? "ĐÃ KẾT NỐI " + app.Rig.PacketsPerSecond + " gói/giây" : "chưa nhận tín hiệu")}";
                }
            }
            else
            {
                // Career challenges
                for (int i = 0; i < careerButtons.Count && i < DriverProgress.Challenges.Length; i++)
                {
                    var c = DriverProgress.Challenges[i];
                    bool locked = app.Profile.Stars < c.requires_stars;
                    var txt = careerButtons[i].GetComponentInChildren<Text>();
                    if (txt) txt.text = c.name + (locked ? $"  ·  Cần {c.requires_stars} sao" : "");
                    SetButtonActive(careerButtons[i], i == app.ChallengeIndex);
                }

                if (careerObjectiveText && app.ChallengeIndex < DriverProgress.Challenges.Length)
                {
                    careerObjectiveText.text = DriverProgress.Challenges[app.ChallengeIndex].objective;
                }
            }
        }

        private void RefreshSettingsUI()
        {
            if (!app) return;

            for (int i = 0; i < qualityButtons.Count; i++)
            {
                SetButtonActive(qualityButtons[i], app.Quality == i);
            }

            if (motionSlider) motionSlider.SetValueWithoutNotify(app.CameraMotion);
            if (motionLabel) motionLabel.text = $"Chuyển động đầu: {app.CameraMotion:P0}";

            if (app.Rig != null)
            {
                if (rigInfoLabel)
                    rigInfoLabel.text = $"Rig: {app.Rig.PacketsPerSecond} gói/giây · Encoder {app.Rig.Raw.encoder}";

                var c = app.Rig.calibration;
                if (c != null)
                {
                    if (steerRangeSlider) steerRangeSlider.SetValueWithoutNotify(c.steeringHalfRangeTicks);
                    if (steerRangeLabel) steerRangeLabel.text = $"Nửa hành trình vô lăng: {c.steeringHalfRangeTicks:0} ticks";
                    if (invertSteerToggle) invertSteerToggle.SetIsOnWithoutNotify(c.invertSteering);
                    if (invertThrottleToggle) invertThrottleToggle.SetIsOnWithoutNotify(c.invertThrottle);
                    if (invertBrakeToggle) invertBrakeToggle.SetIsOnWithoutNotify(c.invertBrake);
                    if (invertClutchToggle) invertClutchToggle.SetIsOnWithoutNotify(c.invertClutch);
                }

                if (livePedalsText)
                    livePedalsText.text = $"GA {app.Rig.Raw.throttle:P0}     PHANH {app.Rig.Raw.brake:P0}     CÔN {app.Rig.Raw.clutch:P0}";
            }
        }

        private void RefreshPauseUI()
        {
            if (pauseControlLabel)
                pauseControlLabel.text = $"Điều khiển: {SimulationApp.ControlName(app.Control)}   ⇄   Đổi (F10)";
        }

        private void RefreshResultsUI()
        {
            var r = app.LastResult;
            if (r == null) return;

            if (resultsTitle) resultsTitle.text = r.passed ? "HOÀN THÀNH CHUYẾN ĐI" : "KẾT QUẢ CHUYẾN ĐI";
            if (resultsStars) resultsStars.text = $"{r.stars} SAO    +{r.xp} XP    +{r.credits} CR";
            if (resultsStats) resultsStats.text = $"THỜI GIAN  {r.time:0.0} s       QUÃNG ĐƯỜNG  {r.distance:0.00} km       TÌNH TRẠNG XE  {r.health:0}%";
            if (resultsFeedback) resultsFeedback.text = r.feedback;
            if (resultsStandings) resultsStandings.text = r.racers > 0 ? "KẾT QUẢ CHUNG CUỘC\n" + r.standings : "";
            if (resultsTitle && r.racers > 0) resultsTitle.text = r.passed ? $"VỀ ĐÍCH HẠNG {r.position} / {r.racers}" : "KHÔNG HOÀN THÀNH CUỘC ĐUA";
        }

        private void Update()
        {
            if (!app) return;

            // Subtle pulsing of start button
            if (startButton && startButton.gameObject.activeInHierarchy)
            {
                float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 3.5f);
                var img = startButton.GetComponent<Image>();
                if (img) img.color = IgnitionRed * pulse;
            }

            // Live pedals update when in settings
            if (app.ScreenState == "settings" && livePedalsText && app.Rig != null)
            {
                livePedalsText.text = $"GA {app.Rig.Raw.throttle:P0}     PHANH {app.Rig.Raw.brake:P0}     CÔN {app.Rig.Raw.clutch:P0}";
                if (rigInfoLabel)
                    rigInfoLabel.text = $"Rig: {app.Rig.PacketsPerSecond} gói/giây · Encoder {app.Rig.Raw.encoder}";
            }
        }

        // --- uGUI Helpers ---

        private GameObject CreatePanel(Transform parent, Color color, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = panelSprite;
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            SetRect(rt, aMin, aMax, oMin, oMax);
            return go;
        }

        private Text CreateText(Transform parent, string content, int fontSize, Color color, FontStyle style, TextAnchor align)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.font = uiFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.fontStyle = style;
            txt.alignment = align;
            return txt;
        }

        private Button CreateStyledButton(Transform parent, string label, int fontSize, Action onClick, Color? customBg = null)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.sprite = buttonSprite;
            Color baseColor = customBg ?? CardBg;
            img.color = baseColor;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = baseColor * 1.35f;
            colors.pressedColor = AccentColor * 0.8f;
            colors.selectedColor = CardSelected;
            btn.colors = colors;

            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.font = uiFont;
            txt.text = label;
            txt.fontSize = fontSize;
            txt.color = TextInk;
            txt.alignment = TextAnchor.MiddleCenter;
            SetStretch(txtGo.GetComponent<RectTransform>());

            return btn;
        }

        private Slider CreateSlider(Transform parent, Action<float> onValueChanged, float min = 0, float max = 1)
        {
            var go = new GameObject("Slider");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();

            var bg = CreatePanel(go.transform, new Color(0.12f, 0.16f, 0.22f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            SetRect(fillAreaRt, Vector2.zero, Vector2.one, new Vector2(5, 0), new Vector2(-5, 0));

            var fill = CreatePanel(fillArea.transform, AccentColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRt = handleArea.AddComponent<RectTransform>();
            SetRect(handleAreaRt, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));

            var handle = CreatePanel(handleArea.transform, Color.white, new Vector2(0, 0), new Vector2(0, 1), new Vector2(-10, -5), new Vector2(10, 5));

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.minValue = min;
            slider.maxValue = max;
            if (onValueChanged != null) slider.onValueChanged.AddListener(v => onValueChanged(v));

            return slider;
        }

        private Toggle CreateToggle(Transform parent, string label, Action<bool> onValueChanged)
        {
            var go = new GameObject("Toggle");
            go.transform.SetParent(parent, false);

            var bg = CreatePanel(go.transform, CardBg, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, -12), new Vector2(24, 12));
            var check = CreatePanel(bg.transform, AccentColor, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));

            var txt = CreateText(go.transform, label, 16, TextInk, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(txt.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(32, 0), Vector2.zero);

            var toggle = go.AddComponent<Toggle>();
            toggle.graphic = check.GetComponent<Image>();
            if (onValueChanged != null) toggle.onValueChanged.AddListener(v => onValueChanged(v));

            return toggle;
        }

        private void SetButtonActive(Button btn, bool active)
        {
            if (!btn) return;
            var img = btn.GetComponent<Image>();
            if (img) img.color = active ? CardSelected : CardBg;
            var txt = btn.GetComponentInChildren<Text>();
            if (txt) txt.color = active ? AccentColor : TextInk;
        }

        private static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
        }

        private static void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
