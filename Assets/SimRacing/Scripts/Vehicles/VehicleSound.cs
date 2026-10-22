using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SimRacing
{
    public sealed class VehicleSound : MonoBehaviour
    {
        private VehicleController car;

        // 6 dedicated AudioSource channels
        private AudioSource engineSource;
        private AudioSource starterSource;
        private AudioSource shiftSource;
        private AudioSource handbrakeSource;
        private AudioSource windSource;
        private AudioSource hornSource;

        private AudioClip windClip;
        private AudioClip hornClip;
        private AudioClip proceduralFallbackClip;

        // Static memory cache for audio clips across vehicle instances
        private static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private static AudioClip clipIgnition;
        private static AudioClip clipEngineWarming;
        private static AudioClip clipEngineOff;
        private static AudioClip clipFirstGear;
        private static AudioClip clipSecondGear;
        private static AudioClip clipThirdGear;
        private static AudioClip clipHandbrakeOn;
        private static AudioClip clipHandbrakeOff;
        private static AudioClip clipMotorNoStart;
        private static bool clipsLoaded;

        // Audio state telemetry
        private int previousGear;
        private bool wasEngineRunning;
        private bool wasHandbrake;
        private bool ignitionStarted;
        private float currentPitch = 0.80f;
        private float currentVolume = 0f;

        // Public telemetry accessors
        public float CurrentPitch => currentPitch;
        public float CurrentVolume => currentVolume;
        public AudioSource EngineSource => engineSource;
        public AudioSource StarterSource => starterSource;
        public AudioSource ShiftSource => shiftSource;
        public AudioSource HandbrakeSource => handbrakeSource;
        public AudioSource WindSource => windSource;
        public AudioSource HornSource => hornSource;

        public static AudioClip ClipIgnition => clipIgnition;
        public static AudioClip ClipEngineWarming => clipEngineWarming;
        public static AudioClip ClipEngineOff => clipEngineOff;
        public static AudioClip ClipFirstGear => clipFirstGear;
        public static AudioClip ClipSecondGear => clipSecondGear;
        public static AudioClip ClipThirdGear => clipThirdGear;
        public static AudioClip ClipHandbrakeOn => clipHandbrakeOn;
        public static AudioClip ClipHandbrakeOff => clipHandbrakeOff;
        public static AudioClip ClipMotorNoStart => clipMotorNoStart;

        private void Awake()
        {
            car = GetComponent<VehicleController>();
            EnsureClipsLoaded();
            SetupAudioSources();
        }

        private void Start()
        {
            if (!car) return;
            previousGear = car.Gear;
            wasEngineRunning = car.EngineRunning;

            // Trigger ignition sound when spawned in countdown mode
            if (car.Locked)
            {
                TriggerIgnition();
            }
        }

        private void SetupAudioSources()
        {
            AudioClip warmingLoopClip = clipEngineWarming;
            if (warmingLoopClip == null)
            {
                proceduralFallbackClip = GenerateTone("CombustionFallback", 110f, true);
                warmingLoopClip = proceduralFallbackClip;
            }

            engineSource = CreateSource("Engine_Loop", warmingLoopClip, loop: true, blend: 0.25f);
            starterSource = CreateSource("Starter_SFX", null, loop: false, blend: 0.20f);
            shiftSource = CreateSource("Shift_SFX", null, loop: false, blend: 0.20f);
            handbrakeSource = CreateSource("Handbrake_SFX", null, loop: false, blend: 0.15f);

            // Aerodynamic wind noise generator
            var noise = new float[22050];
            var random = new System.Random(17);
            float low = 0f;
            for (int i = 0; i < noise.Length; i++)
            {
                low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.14f);
                noise[i] = low * 0.25f;
            }
            windClip = AudioClip.Create("WindNoise", noise.Length, 1, 22050, false);
            windClip.SetData(noise, 0);
            windSource = CreateSource("Wind_Rush", windClip, loop: true, blend: 0.10f);
            windSource.volume = 0f;
            try { windSource.Play(); } catch { }

            // Procedural electric/pneumatic horn
            hornClip = GenerateTone("HornTone", 330f, false);
            hornSource = CreateSource("Horn_Sound", hornClip, loop: true, blend: 0.25f);
            hornSource.volume = 0f;
            try { hornSource.Play(); } catch { }
        }

        private AudioSource CreateSource(string channelName, AudioClip clip, bool loop, float blend)
        {
            var go = new GameObject(channelName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.spatialBlend = blend;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 2f;
            src.maxDistance = 65f;
            src.dopplerLevel = 0.2f;
            src.playOnAwake = false;
            return src;
        }

        private void Update()
        {
            if (!car) return;
            float dt = Time.deltaTime;

            // 1. Ignition & Combustion Transitions
            HandleIgnitionAndEngineState();

            // 2. Non-linear RPM Pitch and Throttle Load Modulation
            ModulateEngineAudio(dt);

            // 3. Gear Shifting Clunks
            HandleGearShift();

            // 4. Handbrake Ratchet & Release Clicks
            HandleHandbrake();

            // 5. Speed-sensitive Wind Rush and Driver Horn
            if (windSource)
            {
                windSource.volume = Mathf.Clamp01(car.Speed / 240f) * 0.28f;
            }
            if (hornSource)
            {
                hornSource.volume = (car.Controls.horn) ? 0.35f : 0f;
            }
        }

        private void HandleIgnitionAndEngineState()
        {
            if (car.EngineRunning && !wasEngineRunning)
            {
                // Engine caught or engaged: ensure warming idle loop is running
                if (engineSource != null && !engineSource.isPlaying)
                {
                    if (clipEngineWarming != null)
                    {
                        engineSource.clip = clipEngineWarming;
                    }
                    try { engineSource.Play(); } catch { }
                }
            }
            else if (!car.EngineRunning && wasEngineRunning)
            {
                // Engine shut down or stalled
                if (engineSource != null && engineSource.isPlaying)
                {
                    try { engineSource.Stop(); } catch { }
                }
                if (clipEngineOff != null && starterSource != null)
                {
                    try { starterSource.PlayOneShot(clipEngineOff, 0.75f); } catch { }
                }
                ignitionStarted = false;
            }

            wasEngineRunning = car.EngineRunning;
        }

        public void TriggerIgnition()
        {
            if (ignitionStarted) return;
            ignitionStarted = true;
            if (clipIgnition != null && starterSource != null)
            {
                try { starterSource.PlayOneShot(clipIgnition, 0.85f); } catch { }
            }
        }

        public void TriggerMotorNoStart()
        {
            if (clipMotorNoStart != null && starterSource != null)
            {
                try { starterSource.PlayOneShot(clipMotorNoStart, 0.85f); } catch { }
            }
        }

        public void ResetIgnitionState()
        {
            ignitionStarted = false;
        }

        private void ModulateEngineAudio(float dt)
        {
            if (engineSource == null) return;

            if (!car.EngineRunning)
            {
                currentVolume = Mathf.MoveTowards(currentVolume, 0f, 4.0f * dt);
                engineSource.volume = currentVolume;
                return;
            }

            if (!engineSource.isPlaying)
            {
                if (clipEngineWarming != null)
                {
                    engineSource.clip = clipEngineWarming;
                }
                try { engineSource.Play(); } catch { }
            }

            float idleRPM = (car.definition && car.definition.idleRPM > 0) ? car.definition.idleRPM : 800f;
            float redlineRPM = (car.definition && car.definition.redlineRPM > idleRPM) ? car.definition.redlineRPM : 7000f;

            // Pitch modulation with flywheel inertia
            float targetPitch = CalculateTargetPitch(car.RPM, idleRPM, redlineRPM);
            currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, 4.0f * dt);
            engineSource.pitch = currentPitch;

            // Throttle & mechanical load volume modulation
            float targetVolume = CalculateTargetVolume(car.EngineRunning, car.Controls.throttle, car.RPM, idleRPM, redlineRPM);
            currentVolume = Mathf.MoveTowards(currentVolume, targetVolume, 3.5f * dt);
            engineSource.volume = currentVolume;
        }

        public static float CalculateTargetPitch(float rpm, float idleRPM, float redlineRPM)
        {
            if (idleRPM <= 0f) idleRPM = 800f;
            if (redlineRPM <= idleRPM) redlineRPM = 7000f;

            if (rpm < idleRPM)
            {
                return 0.65f + 0.15f * Mathf.Clamp01(rpm / idleRPM);
            }

            float normRPM = Mathf.Clamp01((rpm - idleRPM) / Mathf.Max(100f, redlineRPM - idleRPM));
            return 0.80f + 1.45f * Mathf.Pow(normRPM, 0.82f);
        }

        public static float CalculateTargetVolume(bool engineRunning, float throttle, float rpm, float idleRPM, float redlineRPM)
        {
            if (!engineRunning) return 0f;
            if (idleRPM <= 0f) idleRPM = 800f;
            if (redlineRPM <= idleRPM) redlineRPM = 7000f;

            float normRPM = Mathf.Clamp01((rpm - idleRPM) / Mathf.Max(100f, redlineRPM - idleRPM));
            float t = Mathf.Clamp01(throttle);
            return Mathf.Clamp01(0.30f + 0.35f * t + 0.35f * normRPM);
        }

        private void HandleGearShift()
        {
            int currentGear = car.Gear;
            if (currentGear != previousGear && car.EngineRunning)
            {
                AudioClip shiftClip = null;
                if (currentGear == 1 || currentGear == 6) shiftClip = clipFirstGear;
                else if (currentGear == 2) shiftClip = clipSecondGear;
                else if (currentGear >= 3) shiftClip = clipThirdGear;

                if (shiftClip != null && shiftSource != null)
                {
                    try { shiftSource.PlayOneShot(shiftClip, 0.60f); } catch { }
                }
                previousGear = currentGear;
            }
        }

        private void HandleHandbrake()
        {
            bool isHandbrake = Input.GetKey(KeyCode.Space) ||
                (car.rig && car.rig.Connected && car.Controls.brake > 0.85f && car.Speed < 5f);

            if (isHandbrake && !wasHandbrake)
            {
                if (clipHandbrakeOn != null && handbrakeSource != null)
                {
                    try { handbrakeSource.PlayOneShot(clipHandbrakeOn, 0.70f); } catch { }
                }
            }
            else if (!isHandbrake && wasHandbrake)
            {
                if (clipHandbrakeOff != null && handbrakeSource != null)
                {
                    try { handbrakeSource.PlayOneShot(clipHandbrakeOff, 0.70f); } catch { }
                }
            }
            wasHandbrake = isHandbrake;
        }

        public static void EnsureClipsLoaded()
        {
            if (clipsLoaded) return;
            clipsLoaded = true;

            clipIgnition = GetOrLoadClip("2CV6Ignition");
            clipEngineWarming = GetOrLoadClip("2CV6EngineWarming");
            clipEngineOff = GetOrLoadClip("2CV6EngineOff");
            clipFirstGear = GetOrLoadClip("2CV6FirstGear");
            clipSecondGear = GetOrLoadClip("2CV6SecondGear");
            clipThirdGear = GetOrLoadClip("2CV6ThirdGear");
            clipHandbrakeOn = GetOrLoadClip("2CV6HandbrakeOn");
            clipHandbrakeOff = GetOrLoadClip("2CV6HandbrakeOff");
            clipMotorNoStart = GetOrLoadClip("2CV6MotorNoStart");
        }

        private static AudioClip GetOrLoadClip(string clipName)
        {
            lock (clipCache)
            {
                if (clipCache.TryGetValue(clipName, out var cached) && cached != null)
                {
                    return cached;
                }
                var loaded = LoadClip(clipName);
                if (loaded != null)
                {
                    clipCache[clipName] = loaded;
                }
                return loaded;
            }
        }

        private static AudioClip LoadClip(string clipName)
        {
            // Layer 1: Primary Resources.Load
            try
            {
                var clip = Resources.Load<AudioClip>("Audio/" + clipName) ?? Resources.Load<AudioClip>(clipName);
                if (clip != null) return clip;
            }
            catch { }

            // Layer 2: Direct disk PCM WAV decoder fallback
            string[] searchPaths = new[]
            {
                Path.Combine(Application.dataPath, "Resources", "Audio", clipName + ".wav"),
                Path.Combine(Application.dataPath, "Car and transportation sounds collection", clipName + ".wav"),
                Path.Combine(Application.dataPath, "..", "Assets", "Resources", "Audio", clipName + ".wav"),
                Path.Combine(Application.dataPath, "..", "Assets", "Car and transportation sounds collection", clipName + ".wav"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Car and transportation sounds collection", clipName + ".wav"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SimRacing_Data", "Car and transportation sounds collection", clipName + ".wav"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SimRacing_Data", "Audio", clipName + ".wav"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Resources", "Audio", clipName + ".wav"),
                Path.Combine(Application.streamingAssetsPath, "Audio", clipName + ".wav"),
                Path.Combine("E:/Unity3D-SimRacing/Assets/Resources/Audio", clipName + ".wav"),
                Path.Combine("E:/Unity3D-SimRacing/Assets/Car and transportation sounds collection", clipName + ".wav")
            };

            foreach (var path in searchPaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var decoded = DecodeWav(path, clipName);
                        if (decoded != null) return decoded;
                    }
                }
                catch { }
            }

            return null;
        }

        public static AudioClip DecodeWav(string filePath, string clipName)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                byte[] bytes = File.ReadAllBytes(filePath);
                if (bytes.Length < 44) return null;

                // Validate RIFF WAVE header
                if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F' ||
                    bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
                {
                    return null;
                }

                int pos = 12;
                int channels = 0;
                int sampleRate = 0;
                int bitsPerSample = 0;
                int dataOffset = -1;
                int dataSize = 0;

                while (pos + 8 <= bytes.Length)
                {
                    string chunkId = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                    int chunkSize = BitConverter.ToInt32(bytes, pos + 4);
                    if (chunkSize < 0) break;

                    if (chunkId == "fmt ")
                    {
                        if (pos + 8 + 16 <= bytes.Length)
                        {
                            short audioFormat = BitConverter.ToInt16(bytes, pos + 8);
                            if (audioFormat != 1) return null; // Uncompressed PCM only
                            channels = BitConverter.ToInt16(bytes, pos + 10);
                            sampleRate = BitConverter.ToInt32(bytes, pos + 12);
                            bitsPerSample = BitConverter.ToInt16(bytes, pos + 22);
                        }
                    }
                    else if (chunkId == "data")
                    {
                        dataOffset = pos + 8;
                        dataSize = chunkSize;
                        break;
                    }

                    pos += 8 + chunkSize;
                    if ((chunkSize & 1) != 0) pos++; // Word padding
                }

                if (dataOffset < 0 || channels <= 0 || sampleRate <= 0 || bitsPerSample != 16)
                {
                    return null;
                }

                int availableBytes = bytes.Length - dataOffset;
                if (dataSize > availableBytes || dataSize <= 0)
                {
                    dataSize = availableBytes;
                }

                int bytesPerSample = bitsPerSample / 8;
                int totalSamples = dataSize / (bytesPerSample * channels);
                if (totalSamples <= 0) return null;

                float[] samples = new float[totalSamples * channels];
                for (int i = 0; i < samples.Length; i++)
                {
                    int byteIdx = dataOffset + i * 2;
                    if (byteIdx + 1 >= bytes.Length) break;
                    short val = (short)(bytes[byteIdx] | (bytes[byteIdx + 1] << 8));
                    samples[i] = val / 32768.0f;
                }

                var clip = AudioClip.Create(clipName, totalSamples, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VehicleSound] DecodeWav failed for {clipName}: {ex.Message}");
                return null;
            }
        }

        private AudioClip GenerateTone(string label, float hz, bool harmonics)
        {
            var samples = new float[22050];
            for (int i = 0; i < samples.Length; i++)
            {
                float p = i / 22050f * hz * Mathf.PI * 2f;
                samples[i] = Mathf.Sin(p) * 0.15f + (harmonics ? Mathf.Sin(p * 2f) * 0.07f + Mathf.Sin(p * 4f) * 0.025f : Mathf.Sin(p * 1.5f) * 0.10f);
            }
            var clip = AudioClip.Create(label, samples.Length, 1, 22050, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (windClip) Destroy(windClip);
            if (hornClip) Destroy(hornClip);
            if (proceduralFallbackClip) Destroy(proceduralFallbackClip);
        }
    }
}
