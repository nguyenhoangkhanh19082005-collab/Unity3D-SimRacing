using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SimRacing
{
    /// <summary>
    /// Grid race against AI drivers ("Đua tranh hạng"). Builds the arc-length parametrisation of the track route,
    /// places the starting grid, runs the AI field and keeps live standings for the HUD and the results screen.
    /// </summary>
    public sealed class RaceController : MonoBehaviour
    {
        public sealed class Entry
        {
            public string Name;
            public Color Paint;
            public bool IsPlayer;
            public RaceOpponent Ai;
            public float Progress, Lateral, Speed, FinishTime = -1;
            public bool Finished => FinishTime >= 0;
        }

        public static readonly string[] DriverNames = { "Minh Anh", "Quốc Bảo", "Thu Hà", "Đức Huy", "Lan Chi", "Gia Khang", "Hải Nam" };
        public static readonly string[] DifficultyNames = { "Dễ", "Vừa", "Khó" };
        static readonly Color[] Paints =
        {
            new Color(.96f, .76f, .08f), new Color(.08f, .52f, .95f), new Color(.12f, .72f, .34f),
            new Color(.93f, .94f, .96f), new Color(.58f, .2f, .86f), new Color(.98f, .42f, .08f), new Color(.85f, .1f, .35f)
        };
        static readonly float[] DifficultyScale = { .84f, .93f, 1.02f };

        public readonly List<Entry> Entries = new List<Entry>();
        public TrackDefinition Track { get; private set; }
        public VehicleController Player { get; private set; }
        public DriveSession Session { get; private set; }
        public Entry PlayerEntry { get; private set; }
        public int Laps { get; private set; }
        public int Difficulty { get; private set; }
        public float LapLength { get; private set; }
        public float MaxLateral { get; private set; }
        public int PlayerPosition { get; private set; } = 1;
        public float Countdown;

        float[] cumulative, radius;
        float playerS;
        readonly List<Entry> ranked = new List<Entry>();

        public void Setup(TrackDefinition track, VehicleController player, DriveSession session, int laps, int opponents, int difficulty)
        {
            Track = track; Player = player; Session = session; Laps = Mathf.Max(1, laps);
            Difficulty = Mathf.Clamp(difficulty, 0, DifficultyScale.Length - 1);
            BuildRouteTables();
            // The oval tarmac is 15 m wide; the mountain pass is a two-lane road, so the field stays tighter there.
            MaxLateral = track.id == "mountain" ? 5.2f : 2.6f;
            float gridLateral = Mathf.Min(2.6f, MaxLateral);

            int count = Mathf.Clamp(opponents, 1, DriverNames.Length) + 1;
            int playerSlot = Mathf.Min(3, count - 1);
            var skills = new List<float>();
            for (int i = 0; i < count - 1; i++) skills.Add((1f - i * .025f) * DifficultyScale[Difficulty]);
            for (int i = skills.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (skills[i], skills[j]) = (skills[j], skills[i]); }

            int ai = 0;
            for (int slot = 0; slot < count; slot++)
            {
                int row = slot / 2, column = slot % 2;
                float s = -5f - row * 9f - column * 3.5f;
                float lateral = column == 0 ? -gridLateral : gridLateral;
                Pose(s, lateral, out Vector3 position, out Quaternion rotation, out _);
                if (slot == playerSlot)
                {
                    player.transform.SetPositionAndRotation(position + Vector3.up * .35f, rotation);
                    if (player.Body) { player.Body.position = player.transform.position; player.Body.rotation = rotation; }
                    PlayerEntry = new Entry { Name = "Bạn", IsPlayer = true, Progress = s, Lateral = lateral, Paint = new Color(.29f, .88f, .72f) };
                    Entries.Add(PlayerEntry);
                    playerS = Mathf.Repeat(s, LapLength);
                    continue;
                }
                var entry = new Entry { Name = DriverNames[ai], Paint = Paints[ai % Paints.Length], Progress = s, Lateral = lateral };
                var go = new GameObject("Racer " + entry.Name);
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(position, rotation);
                entry.Ai = go.AddComponent<RaceOpponent>();
                entry.Ai.Initialize(this, entry, skills[ai], player.definition);
                Entries.Add(entry);
                ai++;
            }
            UpdateStandings();
        }

        void BuildRouteTables()
        {
            var route = Track.route;
            int n = route.Length;
            cumulative = new float[n + 1];
            radius = new float[n];
            for (int i = 0; i < n; i++) cumulative[i + 1] = cumulative[i] + Vector3.Distance(route[i], route[(i + 1) % n]);
            LapLength = cumulative[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = Flat(Track.Point(i - 1)), b = Flat(Track.Point(i)), c = Flat(Track.Point(i + 1));
                float ab = Vector2.Distance(a, b), bc = Vector2.Distance(b, c), ca = Vector2.Distance(c, a);
                float area2 = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
                radius[i] = area2 < 1e-4f ? 2000f : Mathf.Min(2000f, ab * bc * ca / (2f * area2));
            }
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>Corner radius (m) of the racing line around route index i, smoothed over neighbours.</summary>
        public float RadiusAt(int i)
        {
            int n = radius.Length;
            float r = radius[(i % n + n) % n];
            r = Mathf.Min(r, radius[((i - 1) % n + n) % n] * 1.15f);
            r = Mathf.Min(r, radius[((i + 1) % n + n) % n] * 1.15f);
            return r;
        }

        public int RoutePointCount => radius.Length;
        public float SegmentStart(int i) => cumulative[(i % radius.Length + radius.Length) % radius.Length];

        /// <summary>Route index whose segment contains arc length s (wrapped to the lap).</summary>
        public int SegmentAt(float s, out float t)
        {
            s = Mathf.Repeat(s, LapLength);
            int lo = 0, hi = radius.Length - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (cumulative[mid] <= s) lo = mid; else hi = mid - 1; }
            float length = cumulative[lo + 1] - cumulative[lo];
            t = length > 1e-4f ? (s - cumulative[lo]) / length : 0;
            return lo;
        }

        public void Sample(float s, out Vector3 center, out Vector3 tangent)
        {
            int i = SegmentAt(s, out float t);
            Vector3 a = Track.Point(i), b = Track.Point(i + 1);
            center = Vector3.Lerp(a, b, t);
            // Blend tangents across the vertex so headings do not snap at each waypoint
            Vector3 current = b - a, previous = a - Track.Point(i - 1), next = Track.Point(i + 2) - b;
            current.y = previous.y = next.y = 0;
            tangent = (t < .5f ? Vector3.Lerp(previous.normalized, current.normalized, t + .5f) : Vector3.Lerp(current.normalized, next.normalized, t - .5f)).normalized;
            if (tangent.sqrMagnitude < 1e-4f) tangent = current.normalized;
        }

        public void Pose(float s, float lateral, out Vector3 position, out Quaternion rotation, out Vector3 groundNormal)
        {
            Sample(s, out Vector3 center, out Vector3 tangent);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            position = Ground(center + right * lateral, center.y, out groundNormal);
            rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(tangent, groundNormal).normalized, groundNormal);
        }

        static readonly RaycastHit[] groundHits = new RaycastHit[16];

        public static Vector3 Ground(Vector3 point, float fallbackY, out Vector3 normal)
        {
            normal = Vector3.up;
            int count = Physics.RaycastNonAlloc(new Vector3(point.x, Mathf.Max(point.y, fallbackY) + 3f, point.z), Vector3.down, groundHits, 12f, ~(1 << 8), QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.rigidbody != null || hit.normal.y < .6f) continue;
                string name = hit.collider.name;
                if (name.Contains("Abyss") || name.Contains("Safety")) continue;
                if (hit.point.y > best) { best = hit.point.y; normal = hit.normal; }
            }
            return new Vector3(point.x, best > float.NegativeInfinity ? best : fallbackY, point.z);
        }

        /// <summary>Arc length and signed lateral offset of a world position relative to the route.</summary>
        public void Project(Vector3 position, out float s, out float lateral)
        {
            int nearest = Track.Closest(position);
            float bestDistance = float.MaxValue; s = 0; lateral = 0;
            for (int offset = -1; offset <= 0; offset++)
            {
                int i = ((nearest + offset) % radius.Length + radius.Length) % radius.Length;
                Vector3 a = Track.Point(i), b = Track.Point(i + 1), ab = b - a;
                ab.y = 0;
                Vector3 ap = position - a; ap.y = 0;
                float length = ab.magnitude;
                if (length < 1e-4f) continue;
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / (length * length));
                Vector3 closest = a + ab * t; closest.y = position.y;
                float distance = (position - closest).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    s = cumulative[i] + (cumulative[i + 1] - cumulative[i]) * t;
                    lateral = Vector3.Dot(position - closest, Vector3.Cross(Vector3.up, ab / length));
                }
            }
        }

        /// <summary>Distance from a to b along the track, wrapped into half a lap either way (positive = b ahead).</summary>
        public float Gap(Entry a, Entry b)
        {
            float d = Mathf.Repeat(b.Progress - a.Progress + LapLength * .5f, LapLength) - LapLength * .5f;
            return d;
        }

        public int LapOf(Entry e) => Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, e.Progress) / LapLength) + 1, 1, Laps);

        void FixedUpdate()
        {
            if (!Player || PlayerEntry == null) return;
            if (!PlayerEntry.Finished)
            {
                Project(Player.transform.position, out float s, out float lateral);
                float delta = Mathf.Repeat(s - playerS + LapLength * .5f, LapLength) - LapLength * .5f;
                PlayerEntry.Progress += delta;
                playerS = s;
                PlayerEntry.Lateral = lateral;
            }
            PlayerEntry.Speed = Player.Speed / 3.6f;
            UpdateStandings();
        }

        void UpdateStandings()
        {
            ranked.Clear();
            ranked.AddRange(Entries);
            ranked.Sort((a, b) =>
            {
                if (a.Finished != b.Finished) return a.Finished ? -1 : 1;
                if (a.Finished) return a.FinishTime.CompareTo(b.FinishTime);
                return b.Progress.CompareTo(a.Progress);
            });
            PlayerPosition = ranked.IndexOf(PlayerEntry) + 1;
        }

        public IReadOnlyList<Entry> Standings => ranked;

        /// <summary>Called once when the player's session ends; freezes the player's result into the standings.</summary>
        public void MarkPlayerFinished(bool completed, float elapsed)
        {
            if (completed && !PlayerEntry.Finished) PlayerEntry.FinishTime = elapsed;
            UpdateStandings();
        }

        public string StandingsReport()
        {
            var text = new StringBuilder();
            for (int i = 0; i < ranked.Count; i++)
            {
                var e = ranked[i];
                string status = e.Finished ? FormatTime(e.FinishTime) : $"chưa về đích · vòng {LapOf(e)}/{Laps}";
                text.Append($"{i + 1}.  {(e.IsPlayer ? "BẠN" : e.Name)}   ·   {status}");
                if (i < ranked.Count - 1) text.Append('\n');
            }
            return text.ToString();
        }

        public static string FormatTime(float seconds) => $"{Mathf.FloorToInt(seconds / 60)}:{seconds % 60:00.0}";
    }

    /// <summary>
    /// One AI racer. Drives along the route by arc length with a speed profile limited by corner radius,
    /// braking distance and the same engine/gearbox numbers as the player's car, and moves laterally to overtake.
    /// </summary>
    public sealed class RaceOpponent : MonoBehaviour
    {
        public RaceController.Entry Entry { get; private set; }
        public float Skill { get; private set; }
        RaceController race;
        VehicleDefinition car;
        Rigidbody body;
        TextMesh nameTag;
        float[] profile;
        float preferredLateral, targetLateral, overtakeClock, gearTopSpeed;

        public void Initialize(RaceController owner, RaceController.Entry entry, float skill, VehicleDefinition definition)
        {
            race = owner; Entry = entry; Skill = skill; car = definition;
            preferredLateral = targetLateral = entry.Lateral;

            body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, .62f, 0);
            box.size = new Vector3(1.9f, 1.15f, 4.4f);

            var visual = VehicleVisual.Create(definition, transform);
            Paint(visual, entry.Paint);
            CreateTag();

            float wheel = 2 * Mathf.PI * Mathf.Max(.2f, car.tireRadius);
            float topRatio = Mathf.Abs(car.ratios[Mathf.Min(5, car.ratios.Length - 1)]) * car.finalDrive;
            gearTopSpeed = car.redlineRPM / 60f / Mathf.Max(.1f, topRatio) * wheel;
            BuildProfile();
        }

        static void Paint(GameObject visual, Color color)
        {
            if (!visual) return;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                bool paints = false;
                foreach (var m in shared) paints |= IsPaint(m);
                if (!paints) continue;
                var materials = renderer.materials;
                foreach (var m in materials) if (IsPaint(m)) m.SetColor("_BaseColor", color);
            }
        }

        static bool IsPaint(Material m)
        {
            if (!m) return false;
            string n = m.name.ToLowerInvariant();
            return n.StartsWith("carpaint") || n.StartsWith("f40") && !n.StartsWith("f40_") || n.StartsWith("bugatti_body");
        }

        void CreateTag()
        {
            var go = new GameObject("Name tag");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 2.2f, 0);
            nameTag = go.AddComponent<TextMesh>();
            nameTag.text = Entry.Name;
            nameTag.fontSize = 48;
            nameTag.characterSize = .09f;
            nameTag.anchor = TextAnchor.MiddleCenter;
            nameTag.alignment = TextAlignment.Center;
            nameTag.color = Entry.Paint;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font) { nameTag.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
        }

        float Grip => race.Player ? race.Player.Grip : 1f;

        void BuildProfile()
        {
            int n = race.RoutePointCount;
            profile = new float[n];
            float lateralGrip = 9.2f * Skill * Grip, braking = 7.5f * Grip;
            float top = gearTopSpeed * .94f * Skill;
            for (int i = 0; i < n; i++) profile[i] = Mathf.Min(top, Mathf.Sqrt(lateralGrip * race.RadiusAt(i)));
            // Two backward passes around the closed loop so every corner is preceded by a braking zone
            for (int pass = 0; pass < 2; pass++)
                for (int i = n - 1; i >= 0; i--)
                {
                    int next = (i + 1) % n;
                    float length = race.SegmentStart(i + 1) - race.SegmentStart(i);
                    if (length < 0) length += race.LapLength;
                    profile[i] = Mathf.Min(profile[i], Mathf.Sqrt(profile[next] * profile[next] + 2 * braking * length));
                }
        }

        float ProfileAt(float s)
        {
            int i = race.SegmentAt(s, out float t);
            return Mathf.Lerp(profile[i], profile[(i + 1) % profile.Length], t);
        }

        /// <summary>Forward acceleration available at speed v, using the player's car torque curve and gearing.</summary>
        float Acceleration(float v)
        {
            float wheel = 2 * Mathf.PI * Mathf.Max(.2f, car.tireRadius);
            float force = 0;
            for (int gear = 1; gear <= Mathf.Min(5, car.ratios.Length - 1); gear++)
            {
                float ratio = Mathf.Abs(car.ratios[gear]);
                float rpm = Mathf.Max(car.idleRPM, v / wheel * 60 * ratio * car.finalDrive);
                if (rpm > car.redlineRPM) continue;
                float curve = Mathf.Max(.75f, .6f + .4f * Mathf.Sin(Mathf.Clamp((rpm - 1000) / 7000 * Mathf.PI, 0, Mathf.PI)));
                force = car.torque * curve * ratio * car.finalDrive / Mathf.Max(.2f, car.tireRadius);
                break;
            }
            float accel = (force * Skill - .38f * v * v) / Mathf.Max(500, car.mass) - .15f;
            return Mathf.Min(accel, 9.81f * .95f * Grip);
        }

        void FixedUpdate()
        {
            if (race == null || race.Session == null) return;
            float dt = Time.fixedDeltaTime;
            bool racing = race.Session.Active || race.Session.Finished;
            if (racing)
            {
                float target = ProfileAt(Entry.Progress + Mathf.Max(4, Entry.Speed * .6f));
                if (Entry.Finished) target *= .55f; // cool-down lap
                else if (race.PlayerEntry != null)
                {
                    // Mild catch-up so the field stays together: slower when far ahead of the player, faster when far behind
                    float gap = race.Gap(race.PlayerEntry, Entry);
                    target *= 1f - Mathf.Clamp(gap / 400f, -1f, 1f) * .07f;
                }
                target = Traffic(target, dt);
                float rate = target > Entry.Speed ? Mathf.Max(.5f, Acceleration(Entry.Speed)) : 8f * Grip;
                Entry.Speed = Mathf.MoveTowards(Entry.Speed, target, rate * dt);
                Entry.Progress += Entry.Speed * dt;
                if (!Entry.Finished && Entry.Progress >= race.Laps * race.LapLength) Entry.FinishTime = race.Session.Elapsed;
            }
            float lateralRate = 2.6f * Mathf.Max(.4f, Mathf.Clamp01(Entry.Speed / 6f));
            float previousLateral = Entry.Lateral;
            Entry.Lateral = Mathf.MoveTowards(Entry.Lateral, Mathf.Clamp(targetLateral, -race.MaxLateral, race.MaxLateral), lateralRate * dt);

            race.Pose(Entry.Progress, Entry.Lateral, out Vector3 position, out Quaternion rotation, out Vector3 normal);
            float lateralVelocity = (Entry.Lateral - previousLateral) / dt;
            if (Entry.Speed > 1f)
                rotation = Quaternion.AngleAxis(Mathf.Atan2(lateralVelocity, Entry.Speed) * Mathf.Rad2Deg, normal) * rotation;
            body.MovePosition(position);
            body.MoveRotation(rotation);
        }

        /// <summary>Overtake or follow the nearest car ahead in the same lane; returns the adjusted target speed.</summary>
        float Traffic(float target, float dt)
        {
            overtakeClock = Mathf.Max(0, overtakeClock - dt);
            RaceController.Entry blocker = null;
            float blockerGap = float.MaxValue;
            foreach (var other in race.Entries)
            {
                if (other == Entry) continue;
                float gap = race.Gap(Entry, other);
                float side = other.Lateral - Entry.Lateral;
                // Side-by-side: give room laterally
                if (Mathf.Abs(gap) < 5f && Mathf.Abs(side) < 2f)
                    targetLateral = Entry.Lateral - Mathf.Sign(side == 0 ? 1 : side) * 2.2f;
                if (gap > 0 && gap < 28f && Mathf.Abs(side) < 2.2f && gap < blockerGap) { blocker = other; blockerGap = gap; }
            }

            if (blocker != null)
            {
                if (blocker.Speed < Entry.Speed + .5f)
                {
                    float best = float.NaN;
                    foreach (float candidate in new[] { blocker.Lateral - 3f, blocker.Lateral + 3f })
                    {
                        if (Mathf.Abs(candidate) > race.MaxLateral) continue;
                        if (!LaneFree(candidate)) continue;
                        if (float.IsNaN(best) || Mathf.Abs(candidate - preferredLateral) < Mathf.Abs(best - preferredLateral)) best = candidate;
                    }
                    if (!float.IsNaN(best)) { targetLateral = best; overtakeClock = 3f; }
                    else target = Mathf.Min(target, blocker.Speed + Mathf.Max(0, blockerGap - 9f) * .6f);
                }
                // Close behind: match the car ahead, but keep creeping while pulling out so a stopped car never traps us
                bool pullingOut = Mathf.Abs(targetLateral - blocker.Lateral) >= 2.2f;
                if (blockerGap < 7f) target = Mathf.Min(target, pullingOut && blockerGap > 4.5f ? Mathf.Max(blocker.Speed, 2.5f) : blocker.Speed * .85f);
            }
            else if (overtakeClock <= 0 && LaneFree(preferredLateral))
            {
                targetLateral = preferredLateral;
            }
            return Mathf.Max(0, target);
        }

        bool LaneFree(float lateral)
        {
            foreach (var other in race.Entries)
            {
                if (other == Entry) continue;
                float gap = race.Gap(Entry, other);
                if (gap > -7f && gap < 20f && Mathf.Abs(other.Lateral - lateral) < 2.1f) return false;
            }
            return true;
        }

        void LateUpdate()
        {
            if (!nameTag) return;
            var rig = SimulationApp.Instance ? SimulationApp.Instance.CameraRig : null;
            var view = rig ? rig.view : Camera.main;
            if (!view) return;
            float distance = Vector3.Distance(view.transform.position, transform.position);
            bool racing = SimulationApp.Instance && SimulationApp.Instance.ScreenState == "drive";
            nameTag.gameObject.SetActive(racing && distance < 90f && distance > 9f);
            nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - view.transform.position);
        }
    }
}
