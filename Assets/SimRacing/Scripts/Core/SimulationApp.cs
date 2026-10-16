using System;
using System.Collections;
using UnityEngine;

namespace SimRacing
{
    [Serializable]
    public sealed class DisplayPreferences
    {
        public int quality = 1;
        public float motion = .65f;
        public bool Valid => quality >= 0 && quality <= 2 && !float.IsNaN(motion) && motion >= 0 && motion <= 1;
    }

    public sealed class SimulationApp : MonoBehaviour
    {
        public static SimulationApp Instance;
        public RigConnection Rig { get; private set; }
        public VehicleController Car { get; private set; }
        public TrackDefinition Track { get; private set; }
        public DriverCamera CameraRig { get; private set; }
        public WorldPresentation Presentation { get; private set; }
        public DriveSession Session { get; private set; }
        public DriverProfile Profile { get; private set; }
        public Result LastResult;
        public string ScreenState = "menu", CarId = "mercedes", TrackId = "coastal", Weather = "clear", Mode = "free";
        public int ChallengeIndex, Quality = 1;
        public float Countdown, CameraMotion = .65f;
        public bool TestMode;
        public string Message = "";

        GameObject world;
        Camera menuCamera;
        bool loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() { if (!FindFirstObjectByType<SimulationApp>()) new GameObject("SimRacing").AddComponent<SimulationApp>(); }

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Time.fixedDeltaTime = 1f / 120;
            Time.maximumDeltaTime = .1f;
            TestMode = Array.Exists(Environment.GetCommandLineArgs(), s => s == "--verify");
            Rig = new GameObject("Physical rig").AddComponent<RigConnection>();
            Rig.VehicleSpeed = () => Car ? Car.Speed : 0;
            Profile = TestMode ? new DriverProfile() : LocalStore.Load("profile.json", new DriverProfile(), p => p.Valid);
            var display = LocalStore.Load("display-settings.json", new DisplayPreferences(), p => p.Valid);
            Quality = display.quality;
            CameraMotion = display.motion;

            gameObject.AddComponent<SimulationUI>();

            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.gameObject.SetActive(false);

            menuCamera = new GameObject("Menu camera").AddComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = new Color(.025f, .04f, .065f);
            menuCamera.gameObject.AddComponent<AudioListener>();

            var stage = gameObject.AddComponent<ShowcaseStage>();
            stage.Initialize(menuCamera);
            stage.ShowCar(CarId);

            if (TestMode) gameObject.AddComponent<RuntimeVerification>();
        }

        public void StartDrive()
        {
            if (loading) return;
            if (Mode == "career" && Profile.Stars < DriverProgress.Challenges[ChallengeIndex].requires_stars)
            {
                Message = "Hoàn thành các bài trước để mở thử thách này.";
                return;
            }
            if (Mode != "career" && CarId == "bugatti" && Profile.Stars < 7)
            {
                Message = "Bugatti mở khi bạn đạt 7 sao Career.";
                return;
            }
            StartCoroutine(LoadDrive());
        }

        public IEnumerator LoadDrive()
        {
            loading = true;
            ScreenState = "loading";
            Rig.Hold();
            Time.timeScale = 1;

            if (ShowcaseStage.Instance) ShowcaseStage.Instance.SetVisible(false);

            yield return null;
            if (world) Destroy(world);
            world = new GameObject("Current drive");

            Challenge challenge = Mode == "career" ? DriverProgress.Challenges[ChallengeIndex] : null;
            if (challenge != null) { CarId = challenge.car; TrackId = challenge.track; }

            string trackName = TrackId == "coastal" ? "coastal_track" : "race_circuit";
            var trackRequest = Resources.LoadAsync<GameObject>("Worlds/" + trackName);
            yield return trackRequest;
            if (!trackRequest.asset) { Message = "Thiếu tài nguyên đường đua: " + trackName; ScreenState = "menu"; loading = false; yield break; }
            Track = Instantiate((GameObject)trackRequest.asset, world.transform).GetComponent<TrackDefinition>();

            string carName = CarId == "mercedes" ? "mercedes_w210" : CarId == "ferrari" ? "ferrari_f40" : "bugatti_centodieci";
            var carRequest = Resources.LoadAsync<GameObject>("Vehicles/" + carName);
            yield return carRequest;
            if (!carRequest.asset) { Message = "Thiếu tài nguyên xe: " + carName; ScreenState = "menu"; loading = false; yield break; }

            var vehicle = Instantiate((GameObject)carRequest.asset, Track.spawnPosition + Vector3.up * .15f, Track.spawnRotation, world.transform);
            Car = vehicle.AddComponent<VehicleController>();
            Car.rig = Rig;
            Car.track = Track;
            Car.Locked = true;

            // Enhance vehicle instance with imported high-poly model & materials
            VehicleModelUpgrade.EnhanceVehicleInstance(Car);

            var cameraObject = new GameObject("Driver camera");
            cameraObject.transform.SetParent(world.transform);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            CameraRig = cameraObject.AddComponent<DriverCamera>();
            CameraRig.car = Car;
            CameraRig.Motion = CameraMotion;

            menuCamera.gameObject.SetActive(false);

            Presentation = world.AddComponent<WorldPresentation>();
            Presentation.car = Car;
            Presentation.cameraTarget = CameraRig.view;
            Presentation.SetWeather(Weather);

            Session = world.AddComponent<DriveSession>();
            Session.car = Car;
            Session.track = Track;
            Session.challenge = challenge;
            Session.FreeDrive = Mode == "free";
            Session.RoadRules = Mode == "free" || (challenge != null && challenge.rules != "closed");
            Session.TargetLaps = challenge?.laps ?? 3;
            Session.OnFinished = Finished;

            if (Session.RoadRules)
            {
                var traffic = world.AddComponent<TrafficController>();
                traffic.Setup(Track, Car, Session, challenge?.traffic_count ?? 12);
            }

            var ghost = world.AddComponent<GhostReplay>();
            ghost.Setup(Car, Session, TrackId + "_" + CarId + "_" + Weather, TestMode);

            ApplyQuality();
            Countdown = 3;
            ScreenState = "drive";
            loading = false;
        }

        void Update()
        {
            if (ScreenState == "drive" && Car && !Session.Active && !Session.Finished)
            {
                if (!Rig.Connected)
                {
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                    {
                        Countdown = 0f;
                    }
                    Countdown -= Time.deltaTime;
                }
                else if (!Rig.Held && Rig.Connected)
                {
                    Countdown -= Time.deltaTime;
                }

                if (Countdown <= 0) { Car.Locked = false; Session.Begin(); }
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (ScreenState == "drive") Pause();
                else if (ScreenState == "pause") Resume();
                else if (ScreenState == "settings") ScreenState = "menu";
            }
        }

        public void Pause()
        {
            if (ScreenState != "drive") return;
            ScreenState = "pause";
            Rig.SetPaused(true);
            Time.timeScale = 0;
        }

        public void Resume()
        {
            Time.timeScale = 1;
            Rig.SetPaused(false);
            ScreenState = "drive";
        }

        public void ToMenu()
        {
            Rig.SetPaused(false);
            Rig.Hold();
            Time.timeScale = 1;
            if (world) Destroy(world);
            Car = null;
            Track = null;
            Session = null;
            menuCamera.gameObject.SetActive(true);
            ScreenState = "menu";
            LastResult = null;

            if (ShowcaseStage.Instance)
            {
                ShowcaseStage.Instance.SetVisible(true);
                ShowcaseStage.Instance.ShowCar(CarId);
            }
        }

        void Finished(Result result)
        {
            LastResult = result;
            if (!TestMode) DriverProgress.Record(Profile, Session, result, TrackId + "_" + CarId);
            ScreenState = "results";
        }

        public void ApplyQuality()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipeline)
            {
                pipeline.renderScale = Quality == 0 ? .8f : 1;
                pipeline.shadowDistance = Quality == 2 ? 140 : Quality == 0 ? 55 : 95;
                pipeline.msaaSampleCount = 1;
            }
        }

        public void SavePreferences()
        {
            LocalStore.Save("display-settings.json", new DisplayPreferences { quality = Quality, motion = CameraMotion });
            Rig.SaveCalibration();
        }
    }
}
