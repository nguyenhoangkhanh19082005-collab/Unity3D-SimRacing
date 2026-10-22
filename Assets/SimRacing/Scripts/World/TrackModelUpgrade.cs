using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimRacing
{
    public static class TrackModelUpgrade
    {
        private static readonly Dictionary<string, Texture2D> CachedTextures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Material> CachedMaterials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, List<SubmeshData>> CachedBinData = new Dictionary<string, List<SubmeshData>>();
        private static string cachedTracksDir;

        public struct SubmeshData
        {
            public string name;
            public string materialName;
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uvs;
            public int[] triangles;
        }

        public static string GetTracksDirectory()
        {
            if (!string.IsNullOrEmpty(cachedTracksDir) && Directory.Exists(cachedTracksDir))
                return cachedTracksDir;

            string[] candidates = new[]
            {
                Path.Combine(Application.dataPath, "Tracks"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SimRacing_Data", "Tracks"),
                Path.Combine(Application.dataPath, "SimRacing_Data", "Tracks"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Imported", "Tracks"),
                Path.Combine(Application.dataPath, "..", "Assets", "Imported", "Tracks"),
                Path.Combine(Application.dataPath, "Imported", "Tracks"),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Imported", "Tracks")),
                "E:/Unity3D-SimRacing/Assets/Imported/Tracks"
            };

            foreach (var path in candidates)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        cachedTracksDir = Path.GetFullPath(path);
                        Debug.Log("[TrackModelUpgrade] Found tracks directory: " + cachedTracksDir);
                        return cachedTracksDir;
                    }
                }
                catch { }
            }

            Debug.LogWarning("[TrackModelUpgrade] Tracks directory not found!");
            return null;
        }

        public static List<SubmeshData> LoadBinMesh(string binPath)
        {
            if (string.IsNullOrEmpty(binPath)) return null;
            if (CachedBinData.TryGetValue(binPath, out var cached) && cached != null)
                return cached;

            if (!File.Exists(binPath))
            {
                Debug.LogWarning("[TrackModelUpgrade] Bin mesh not found: " + binPath);
                return null;
            }

            try
            {
                using (var fs = new FileStream(binPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
                using (var br = new BinaryReader(fs))
                {
                    byte[] magic = br.ReadBytes(4);
                    if (magic.Length < 4 || magic[0] != 'S' || magic[1] != 'I' || magic[2] != 'M' || magic[3] != '1')
                    {
                        Debug.LogError("[TrackModelUpgrade] Invalid magic in: " + binPath);
                        return null;
                    }

                    uint submeshCount = br.ReadUInt32();
                    var list = new List<SubmeshData>((int)submeshCount);

                    for (int s = 0; s < submeshCount; s++)
                    {
                        ushort nlen = br.ReadUInt16();
                        string name = System.Text.Encoding.UTF8.GetString(br.ReadBytes(nlen));

                        ushort mlen = br.ReadUInt16();
                        string matName = System.Text.Encoding.UTF8.GetString(br.ReadBytes(mlen));

                        uint vertCount = br.ReadUInt32();
                        var verts = new Vector3[vertCount];
                        for (int i = 0; i < vertCount; i++)
                            verts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                        var norms = new Vector3[vertCount];
                        for (int i = 0; i < vertCount; i++)
                            norms[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                        var uvs = new Vector2[vertCount];
                        for (int i = 0; i < vertCount; i++)
                            uvs[i] = new Vector2(br.ReadSingle(), br.ReadSingle());

                        uint triCount = br.ReadUInt32();
                        var tris = new int[triCount];
                        for (int i = 0; i < triCount; i++)
                            tris[i] = (int)br.ReadUInt32();

                        list.Add(new SubmeshData
                        {
                            name = name,
                            materialName = matName,
                            vertices = verts,
                            normals = norms,
                            uvs = uvs,
                            triangles = tris
                        });
                    }

                    CachedBinData[binPath] = list;
                    return list;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TrackModelUpgrade] Error reading {binPath}: {ex.Message}");
                return null;
            }
        }

        public static Texture2D GetTexture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (CachedTextures.TryGetValue(name, out var cached) && cached)
                return cached;

            string tracksDir = GetTracksDirectory();
            if (string.IsNullOrEmpty(tracksDir)) return null;

            string path = Path.Combine(tracksDir, "Textures", name + ".png");
            if (!File.Exists(path))
            {
                Debug.LogWarning("[TrackModelUpgrade] Texture not found: " + path);
                return null;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (tex.LoadImage(bytes))
                {
                    tex.name = name;
                    tex.wrapMode = TextureWrapMode.Repeat;
                    tex.filterMode = FilterMode.Trilinear;
                    CachedTextures[name] = tex;
                    return tex;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TrackModelUpgrade] Failed to load texture {name}: {ex.Message}");
            }

            return null;
        }

        public static Material GetMaterial(string matKey)
        {
            if (CachedMaterials.TryGetValue(matKey, out var cached) && cached)
                return cached;

            Shader shader = VehicleModelUpgrade.LitShader;
            var mat = new Material(shader) { name = matKey };

            string lower = matKey.ToLowerInvariant();
            if (lower.Contains("road") || lower.Contains("tarmac") || lower.Contains("asphalt") || lower.Contains("concrete_red") || lower.Contains("kerb"))
            {
                if (lower.Contains("snow"))
                {
                    mat.mainTexture = GetTexture("road2Lane_snow");
                    var norm = GetTexture("road_b");
                    if (norm) { mat.SetTexture("_BumpMap", norm); mat.EnableKeyword("_NORMALMAP"); }
                    mat.mainTextureScale = new Vector2(1f, 0.29f);
                    mat.SetFloat("_Smoothness", 0.2f);
                    mat.SetFloat("_Metallic", 0.05f);
                }
                else
                {
                    mat.mainTexture = GetTexture("road");
                    var norm = GetTexture("road_normal") ?? GetTexture("road_b");
                    if (norm) { mat.SetTexture("_BumpMap", norm); mat.EnableKeyword("_NORMALMAP"); }
                    mat.SetFloat("_Smoothness", 0.35f);
                    mat.SetFloat("_Metallic", 0.1f);
                }
            }
            else if (lower.Contains("barrier") || lower.Contains("concrete"))
            {
                if (lower.Contains("concrete"))
                {
                    mat.mainTexture = GetTexture("ConcreteBarePoleTGA");
                    mat.SetFloat("_Smoothness", 0.15f);
                }
                else
                {
                    mat.mainTexture = GetTexture("barriers");
                    var norm = GetTexture("barriers_normal");
                    if (norm) { mat.SetTexture("_BumpMap", norm); mat.EnableKeyword("_NORMALMAP"); }
                    mat.SetFloat("_Smoothness", 0.45f);
                }
            }
            else if (lower.Contains("metal") || lower.Contains("pole"))
            {
                mat.mainTexture = GetTexture("metal02");
                mat.SetFloat("_Metallic", 0.85f);
                mat.SetFloat("_Smoothness", 0.65f);
            }
            else if (lower.Contains("snowterrain") || lower.Contains("terrain"))
            {
                mat.mainTexture = GetTexture("ice");
                mat.mainTextureScale = new Vector2(32f, 32f);
                mat.SetColor("_BaseColor", new Color(0.96f, 0.98f, 1f, 1f));
                mat.SetFloat("_Smoothness", 0.15f);
                mat.SetFloat("_Metallic", 0.02f);
            }
            else if (lower.Contains("grass_2"))
            {
                mat.mainTexture = GetTexture("grass_2");
                mat.SetFloat("_Smoothness", 0.1f);
            }
            else if (lower.Contains("grass"))
            {
                mat.mainTexture = GetTexture("grass_1");
                mat.SetFloat("_Smoothness", 0.1f);
            }
            else if (lower.Contains("gravel") || lower.Contains("sand"))
            {
                mat.mainTexture = GetTexture("gravel");
                mat.SetFloat("_Smoothness", 0.1f);
            }
            else if (lower.Contains("rock"))
            {
                mat.mainTexture = GetTexture("rock");
                mat.SetColor("_BaseColor", new Color(0.68f, 0.65f, 0.62f, 1f));
                mat.SetFloat("_Smoothness", 0.15f);
            }
            else if (lower.Contains("garage") || lower.Contains("building") || lower.Contains("block") || lower.Contains("bridge") || lower.Contains("pitwall") || lower.Contains("seat") || lower.Contains("startlight") || lower.Contains("tent") || lower.Contains("turnsign"))
            {
                mat.mainTexture = GetTexture("garages");
                mat.SetFloat("_Smoothness", 0.4f);
                mat.SetFloat("_Metallic", 0.1f);
            }
            else if (lower.Contains("fence") || lower.Contains("metal_fence"))
            {
                mat.mainTexture = GetTexture("metal_fence");
                SetCutout(mat, 0.4f);
                mat.SetFloat("_Smoothness", 0.35f);
            }
            else if (lower.Contains("tree"))
            {
                mat.mainTexture = GetTexture("trees");
                SetCutout(mat, 0.45f);
                mat.SetFloat("_Smoothness", 0.1f);
            }
            else if (lower.Contains("tyre"))
            {
                mat.mainTexture = GetTexture("tyres");
                var norm = GetTexture("tyres_normal");
                if (norm) { mat.SetTexture("_BumpMap", norm); mat.EnableKeyword("_NORMALMAP"); }
                mat.SetFloat("_Smoothness", 0.2f);
            }
            else if (lower.Contains("banner"))
            {
                mat.mainTexture = GetTexture("banners");
                mat.SetFloat("_Cull", 0f);
                mat.doubleSidedGI = true;
                mat.SetFloat("_Smoothness", 0.3f);
            }
            else if (lower.Contains("white_line") || lower.Contains("line"))
            {
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.25f);
            }
            else if (lower.Contains("kerb_red"))
            {
                mat.SetColor("_BaseColor", new Color(0.92f, 0.12f, 0.10f, 1f));
                mat.SetFloat("_Smoothness", 0.4f);
            }
            else if (lower.Contains("kerb_white"))
            {
                mat.SetColor("_BaseColor", new Color(0.96f, 0.96f, 0.98f, 1f));
                mat.SetFloat("_Smoothness", 0.4f);
            }
            else if (lower.Contains("mountain_snow") || lower.Contains("snow_cap"))
            {
                mat.SetColor("_BaseColor", new Color(0.98f, 0.99f, 1f, 1f));
                mat.SetFloat("_Smoothness", 0.25f);
            }
            else if (lower.Contains("water"))
            {
                mat.SetColor("_BaseColor", new Color(0.12f, 0.55f, 0.82f, 0.88f));
                mat.SetFloat("_Smoothness", 0.95f);
                mat.SetFloat("_Metallic", 0.15f);
            }
            else if (lower.Contains("emissive") || lower.Contains("lamp_emissive"))
            {
                mat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.85f, 1f));
                mat.SetColor("_EmissionColor", new Color(2.5f, 2.2f, 1.8f, 1f));
                mat.EnableKeyword("_EMISSION");
            }
            else if (lower.Contains("seat_red"))
            {
                mat.SetColor("_BaseColor", new Color(0.88f, 0.14f, 0.12f, 1f));
                mat.SetFloat("_Smoothness", 0.45f);
            }
            else if (lower.Contains("seat_blue"))
            {
                mat.SetColor("_BaseColor", new Color(0.12f, 0.45f, 0.88f, 1f));
                mat.SetFloat("_Smoothness", 0.45f);
            }
            else if (lower.Contains("seat_yellow"))
            {
                mat.SetColor("_BaseColor", new Color(0.95f, 0.80f, 0.10f, 1f));
                mat.SetFloat("_Smoothness", 0.45f);
            }
            else
            {
                mat.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.82f, 1f));
                mat.SetFloat("_Smoothness", 0.2f);
            }

            CachedMaterials[matKey] = mat;
            return mat;
        }

        private static void SetCutout(Material m, float cutoff = 0.5f)
        {
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetFloat("_Cull", 0f);
            m.doubleSidedGI = true;
        }

        public static void EnhanceTrackInstance(TrackDefinition track, string trackId)
        {
            if (!track) return;

            bool coastal = trackId == "coastal" || trackId == "coastal_track";
            string tracksDir = coastal ? GetTracksDirectory() : null;
            if (coastal && string.IsNullOrEmpty(tracksDir))
            {
                Debug.LogWarning("[TrackModelUpgrade] Cannot enhance track; tracks directory missing.");
                return;
            }
            if (!coastal && (track.sceneryPrefabs == null || track.sceneryPrefabs.Length == 0))
            {
                Debug.LogWarning("[TrackModelUpgrade] race_circuit has no scenery prefabs; keeping legacy geometry.");
                return;
            }

            // 1. Remove all legacy renderers, colliders and child GameObjects
            for (int i = track.transform.childCount - 1; i >= 0; i--)
            {
                var child = track.transform.GetChild(i);
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }

            foreach (var c in track.GetComponents<Collider>())
                c.enabled = false;
            foreach (var r in track.GetComponents<Renderer>())
                r.enabled = false;

            // 2. Configure track definition properties & spawn points
            if (coastal)
            {
                track.id = "coastal";
                track.route = (Vector3[])TrackRoutes.SnowMountain.Clone();
                track.spawnPosition = new Vector3(232.46f, 21.05f, 87.45f);
                track.spawnRotation = new Quaternion(0f, 0.6822507f, 0f, 0.7311206f);
                track.fuelStation = new Vector3(233f, 20.95f, 82f);
                track.speedTrap = new Vector3(350f, 20.56f, 70f);
                track.roadHalfWidth = 7.5f;

                BuildSnowMountainTrack(track, tracksDir);
            }
            else // "mountain" or "race_circuit"
            {
                float mainStraightZ = TrackRoutes.OvalCenterZ + TrackRoutes.OvalTurnRadius;
                track.id = "mountain";
                track.route = (Vector3[])TrackRoutes.CartoonOval.Clone();
                track.spawnPosition = new Vector3(TrackRoutes.OvalStartX, 0.25f, mainStraightZ);
                track.spawnRotation = Quaternion.Euler(0f, 90f, 0f);
                // Refuelling bay inside the pit lane (drivable z -15.8..-9), clear of the garages and the footbridge at x = -44
                track.fuelStation = new Vector3(-30f, 0.06f, -10.9f);
                track.speedTrap = new Vector3(0f, 0.15f, TrackRoutes.OvalCenterZ - TrackRoutes.OvalTurnRadius);
                track.roadHalfWidth = 7.5f;

                BuildCartoonOvalTrack(track);
            }

            Physics.SyncTransforms();

            // 3. Align track waypoints to actual road surface collider height (the oval turns are banked up to 3.9 m)
            AlignRouteToRoad(track);
            track.RecalculateLength();

            Debug.Log($"[TrackModelUpgrade] Successfully upgraded track '{trackId}' (Length: {track.Length:F1}m, Waypoints: {track.route.Length})");
        }

        public static void AlignRouteToRoad(TrackDefinition track)
        {
            if (track.route == null) return;
            for (int i = 0; i < track.route.Length; i++)
            {
                Vector3 wp = track.route[i];
                var hits = Physics.RaycastAll(new Vector3(wp.x, wp.y + 8f, wp.z), Vector3.down, 60f, ~(1 << 8), QueryTriggerInteraction.Ignore);
                float bestY = float.NegativeInfinity;
                bool bestIsRoad = false;
                foreach (var hit in hits)
                {
                    string n = hit.collider.name;
                    if (n.Contains("Abyss") || n.Contains("Safety") || hit.normal.y < 0.5f) continue;
                    // Prefer the tarmac surface over any prop or wall collider above it
                    bool isRoad = n.Contains("TARMAC") || n.Contains("road") || n.Contains("Road");
                    if ((isRoad && !bestIsRoad) || (isRoad == bestIsRoad && hit.point.y > bestY))
                    {
                        bestY = hit.point.y;
                        bestIsRoad = isRoad;
                    }
                }
                if (bestY > float.NegativeInfinity)
                    track.route[i].y = bestY;
            }
        }

        private static void BuildSnowMountainTrack(TrackDefinition track, string tracksDir)
        {
            string snowDir = Path.Combine(tracksDir, "SnowMountain");
            var root = new GameObject("SnowMountain_Environment");
            root.transform.SetParent(track.transform, false);

            // 1. Road (Renderer + MeshCollider)
            InstantiateBinMesh(Path.Combine(snowDir, "road.bin"), root.transform, "road_snow", addCollider: true);

            // 2. Barriers (Renderer + MeshCollider)
            InstantiateBinMesh(Path.Combine(snowDir, "concrete.bin"), root.transform, "concrete", addCollider: true);
            InstantiateBinMesh(Path.Combine(snowDir, "concrete2.bin"), root.transform, "concrete", addCollider: true);

            // 3. Poles (Renderer)
            InstantiateBinMesh(Path.Combine(snowDir, "poles.bin"), root.transform, "metal", addCollider: false);

            // 4. Terrain (Renderer + MeshCollider)
            InstantiateBinMesh(Path.Combine(snowDir, "terrain.bin"), root.transform, "SnowTerrain", addCollider: true);

            // 5. Visible 3D Gas Station & Refueling Bay
            CreateGasStation(root.transform, track.fuelStation, track.spawnRotation, "SNOW_ALPINE_PETROL");
        }

        private static void BuildCartoonOvalTrack(TrackDefinition track)
        {
            var root = new GameObject("CartoonOval_Environment");
            root.transform.SetParent(track.transform, false);

            // 1. CartoonTracksPack1 oval: road, kerbs, terrain, walls & colliders, pits, grandstands, trees.
            // The prefabs are authored around the origin exactly as in complete_track_demo.unity.
            foreach (var prefab in track.sceneryPrefabs)
            {
                if (prefab) UnityEngine.Object.Instantiate(prefab, root.transform, false);
            }

            // 2. Start / finish line and grid boxes under the start lights (x = -16)
            BuildStartGrid(root.transform, track.roadHalfWidth);

            // 3. Visible 3D refuelling bay in the pit lane
            CreateGasStation(root.transform, track.fuelStation, Quaternion.Euler(0f, 90f, 0f), "OVAL_PIT_REFUEL");

            // 4. Safety floor far below the terrain (the outer hills have no collider)
            var safetyFloor = new GameObject("Abyss_Safety_Floor");
            safetyFloor.transform.SetParent(root.transform, false);
            safetyFloor.transform.position = new Vector3(0f, -30f, TrackRoutes.OvalCenterZ);
            var floorCollider = safetyFloor.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(2000f, 1f, 2000f);
        }

        private static void BuildStartGrid(Transform parent, float halfWidth)
        {
            var markingsGo = new GameObject("Road_Markings");
            markingsGo.transform.SetParent(parent, false);

            Material lineMat = GetMaterial("white_line");
            float z = TrackRoutes.OvalCenterZ + TrackRoutes.OvalTurnRadius;

            var startLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            startLine.name = "StartFinish_Line";
            startLine.transform.SetParent(markingsGo.transform, false);
            startLine.transform.position = new Vector3(-16f, 0.026f, z);
            startLine.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            startLine.transform.localScale = new Vector3(halfWidth * 2f - 0.2f, 0.01f, 1.4f);
            startLine.GetComponent<Renderer>().sharedMaterial = lineMat;
            var slc = startLine.GetComponent<Collider>(); if (slc) UnityEngine.Object.Destroy(slc);

            // Staggered starting grid boxes behind the spawn point
            for (int g = 0; g < 6; g++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "GridBox_" + g;
                box.transform.SetParent(markingsGo.transform, false);
                box.transform.position = new Vector3(TrackRoutes.OvalStartX - 2.5f - g * 6f, 0.026f, z + (g % 2 == 0 ? -3.5f : 3.5f));
                box.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                box.transform.localScale = new Vector3(2.6f, 0.01f, 0.25f);
                box.GetComponent<Renderer>().sharedMaterial = lineMat;
                var gbc = box.GetComponent<Collider>(); if (gbc) UnityEngine.Object.Destroy(gbc);
            }
        }


        private static void CreateGasStation(Transform parent, Vector3 stationPos, Quaternion stationRot, string brandName)
        {
            var stationGo = new GameObject("GasStation_" + brandName);
            stationGo.transform.SetParent(parent, false);
            stationGo.transform.SetPositionAndRotation(stationPos, stationRot);

            Shader lit = VehicleModelUpgrade.LitShader;

            // Materials
            var padMat = new Material(lit) { name = "GasStation_Pad" };
            padMat.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.20f, 1f));
            padMat.SetFloat("_Smoothness", 0.25f);
            padMat.SetFloat("_Metallic", 0.05f);

            var curbMat = new Material(lit) { name = "GasStation_Curb" };
            curbMat.SetColor("_BaseColor", new Color(0.75f, 0.77f, 0.78f, 1f));
            curbMat.SetFloat("_Smoothness", 0.3f);
            curbMat.SetFloat("_Metallic", 0.1f);

            var yellowMat = new Material(lit) { name = "GasStation_Yellow" };
            yellowMat.SetColor("_BaseColor", new Color(0.98f, 0.82f, 0.08f, 1f));
            yellowMat.SetFloat("_Smoothness", 0.45f);

            var pumpMat = new Material(lit) { name = "GasStation_PumpBody" };
            pumpMat.SetColor("_BaseColor", new Color(0.85f, 0.12f, 0.10f, 1f));
            pumpMat.SetFloat("_Metallic", 0.75f);
            pumpMat.SetFloat("_Smoothness", 0.82f);

            var darkMat = new Material(lit) { name = "GasStation_DarkMetal" };
            darkMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.14f, 1f));
            darkMat.SetFloat("_Metallic", 0.85f);
            darkMat.SetFloat("_Smoothness", 0.7f);

            var screenMat = new Material(lit) { name = "GasStation_Screen" };
            screenMat.SetColor("_BaseColor", new Color(0.05f, 0.95f, 0.45f, 1f));
            screenMat.SetColor("_EmissionColor", new Color(0.1f, 1.8f, 0.8f, 1f));
            screenMat.EnableKeyword("_EMISSION");

            var roofMat = new Material(lit) { name = "GasStation_Canopy" };
            roofMat.SetColor("_BaseColor", new Color(0.92f, 0.93f, 0.95f, 1f));
            roofMat.SetFloat("_Smoothness", 0.4f);
            roofMat.SetFloat("_Metallic", 0.2f);

            var fasciaMat = new Material(lit) { name = "GasStation_Fascia" };
            fasciaMat.SetColor("_BaseColor", new Color(0.95f, 0.18f, 0.12f, 1f));
            fasciaMat.SetColor("_EmissionColor", new Color(1.6f, 0.25f, 0.15f, 1f));
            fasciaMat.EnableKeyword("_EMISSION");

            var price95Mat = new Material(lit) { name = "GasStation_Price95" };
            price95Mat.SetColor("_BaseColor", new Color(0.1f, 1f, 0.4f, 1f));
            price95Mat.SetColor("_EmissionColor", new Color(0.2f, 2.0f, 0.6f, 1f));
            price95Mat.EnableKeyword("_EMISSION");

            var priceDieselMat = new Material(lit) { name = "GasStation_PriceDiesel" };
            priceDieselMat.SetColor("_BaseColor", new Color(1f, 0.8f, 0.1f, 1f));
            priceDieselMat.SetColor("_EmissionColor", new Color(2.0f, 1.4f, 0.2f, 1f));
            priceDieselMat.EnableKeyword("_EMISSION");

            // 1. Ground Pad (Refueling Bay Floor, top at y = 0.0f)
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "GroundPad";
            pad.transform.SetParent(stationGo.transform, false);
            pad.transform.localPosition = new Vector3(1.6f, -0.05f, 0f);
            pad.transform.localScale = new Vector3(6.5f, 0.05f, 13f);
            pad.GetComponent<Renderer>().sharedMaterial = padMat;
            var padCol = pad.GetComponent<Collider>(); if (padCol) UnityEngine.Object.Destroy(padCol);

            // Yellow Lane Markings on pad
            CreateMarker(stationGo.transform, new Vector3(-1.2f, 0.01f, 0f), new Vector3(0.15f, 0.02f, 9.0f), yellowMat);
            CreateMarker(stationGo.transform, new Vector3(1.2f, 0.01f, 0f), new Vector3(0.15f, 0.02f, 9.0f), yellowMat);
            CreateMarker(stationGo.transform, new Vector3(0f, 0.01f, -4.5f), new Vector3(2.4f, 0.02f, 0.2f), yellowMat);
            CreateMarker(stationGo.transform, new Vector3(0f, 0.01f, 4.5f), new Vector3(2.4f, 0.02f, 0.2f), yellowMat);
            CreateMarker(stationGo.transform, new Vector3(0f, 0.01f, 0f), new Vector3(1.8f, 0.02f, 0.35f), yellowMat);

            // 2. Concrete Pump Island Curb (offset to x = 3.2m, keeping center bay x = 0m completely unobstructed)
            var island = GameObject.CreatePrimitive(PrimitiveType.Cube);
            island.name = "PumpIsland";
            island.transform.SetParent(stationGo.transform, false);
            island.transform.localPosition = new Vector3(3.2f, 0.12f, 0f);
            island.transform.localScale = new Vector3(1.4f, 0.24f, 7.5f);
            island.GetComponent<Renderer>().sharedMaterial = curbMat;
            var ic = island.GetComponent<Collider>(); if (ic) UnityEngine.Object.Destroy(ic);

            // Safety bollards on island ends
            CreateBollard(stationGo.transform, new Vector3(3.2f, 0.5f, -3.4f), yellowMat);
            CreateBollard(stationGo.transform, new Vector3(3.2f, 0.5f, 3.4f), yellowMat);

            // 3. Fuel Pump Dispensers (2 units on the island)
            float[] pumpZ = new float[] { -1.8f, 1.8f };
            for (int p = 0; p < pumpZ.Length; p++)
            {
                float pz = pumpZ[p];
                var pumpRoot = new GameObject("PumpDispenser_" + (p + 1));
                pumpRoot.transform.SetParent(stationGo.transform, false);
                pumpRoot.transform.localPosition = new Vector3(3.2f, 0.24f, pz);

                // Main Cabinet
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Cabinet";
                body.transform.SetParent(pumpRoot.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                body.transform.localScale = new Vector3(0.65f, 1.5f, 0.95f);
                body.GetComponent<Renderer>().sharedMaterial = pumpMat;
                var bc = body.GetComponent<Collider>(); if (bc) UnityEngine.Object.Destroy(bc);

                // Base skirt
                var skirt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                skirt.name = "Skirt";
                skirt.transform.SetParent(pumpRoot.transform, false);
                skirt.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                skirt.transform.localScale = new Vector3(0.7f, 0.2f, 1.0f);
                skirt.GetComponent<Renderer>().sharedMaterial = darkMat;
                var sc = skirt.GetComponent<Collider>(); if (sc) UnityEngine.Object.Destroy(sc);

                // Brand Header Lightbox
                var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cap.name = "BrandCap";
                cap.transform.SetParent(pumpRoot.transform, false);
                cap.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                cap.transform.localScale = new Vector3(0.72f, 0.22f, 1.0f);
                cap.GetComponent<Renderer>().sharedMaterial = fasciaMat;
                var cc = cap.GetComponent<Collider>(); if (cc) UnityEngine.Object.Destroy(cc);

                // Digital LCD Displays (both faces along Z)
                var scr1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                scr1.name = "LCD_Front";
                scr1.transform.SetParent(pumpRoot.transform, false);
                scr1.transform.localPosition = new Vector3(0f, 0.95f, 0.49f);
                scr1.transform.localScale = new Vector3(0.48f, 0.35f, 0.04f);
                scr1.GetComponent<Renderer>().sharedMaterial = screenMat;
                var s1c = scr1.GetComponent<Collider>(); if (s1c) UnityEngine.Object.Destroy(s1c);

                var scr2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                scr2.name = "LCD_Back";
                scr2.transform.SetParent(pumpRoot.transform, false);
                scr2.transform.localPosition = new Vector3(0f, 0.95f, -0.49f);
                scr2.transform.localScale = new Vector3(0.48f, 0.35f, 0.04f);
                scr2.GetComponent<Renderer>().sharedMaterial = screenMat;
                var s2c = scr2.GetComponent<Collider>(); if (s2c) UnityEngine.Object.Destroy(s2c);

                // Nozzle holster facing vehicle bay
                var nozzle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                nozzle.name = "Nozzle";
                nozzle.transform.SetParent(pumpRoot.transform, false);
                nozzle.transform.localPosition = new Vector3(-0.35f, 0.8f, 0f);
                nozzle.transform.localScale = new Vector3(0.08f, 0.6f, 0.22f);
                nozzle.GetComponent<Renderer>().sharedMaterial = darkMat;
                var nc = nozzle.GetComponent<Collider>(); if (nc) UnityEngine.Object.Destroy(nc);
            }

            // 4. Canopy Pillars (cantilever columns situated directly on pump island at x = 3.2f)
            float[] pillarZ = new float[] { -3.6f, 3.6f };
            for (int pz = 0; pz < pillarZ.Length; pz++)
            {
                var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                col.name = $"Pillar_{pz}";
                col.transform.SetParent(stationGo.transform, false);
                col.transform.localPosition = new Vector3(3.2f, 2.5f, pillarZ[pz]);
                col.transform.localScale = new Vector3(0.38f, 2.5f, 0.38f);
                col.GetComponent<Renderer>().sharedMaterial = darkMat;
                var cc = col.GetComponent<Collider>(); if (cc) UnityEngine.Object.Destroy(cc);
            }

            // 5. Canopy Roof
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "CanopyRoof";
            roof.transform.SetParent(stationGo.transform, false);
            roof.transform.localPosition = new Vector3(1.6f, 5.15f, 0f);
            roof.transform.localScale = new Vector3(6.8f, 0.6f, 13f);
            roof.GetComponent<Renderer>().sharedMaterial = roofMat;
            var rc = roof.GetComponent<Collider>(); if (rc) UnityEngine.Object.Destroy(rc);

            // Glowing Fascia Ribbon
            var fascia = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fascia.name = "CanopyFascia";
            fascia.transform.SetParent(stationGo.transform, false);
            fascia.transform.localPosition = new Vector3(1.6f, 5.15f, 0f);
            fascia.transform.localScale = new Vector3(7.0f, 0.42f, 13.2f);
            fascia.GetComponent<Renderer>().sharedMaterial = fasciaMat;
            var fc = fascia.GetComponent<Collider>(); if (fc) UnityEngine.Object.Destroy(fc);

            // 6. Ceiling Downlights (bright point lights illuminating refueling bay and pumps)
            float[] lightZ = new float[] { -2.8f, 2.8f };
            for (int lz = 0; lz < lightZ.Length; lz++)
            {
                var lgo = new GameObject($"CeilingLight_Bay_{lz}");
                lgo.transform.SetParent(stationGo.transform, false);
                lgo.transform.localPosition = new Vector3(0f, 4.7f, lightZ[lz]);
                var lt = lgo.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(1f, 0.96f, 0.90f);
                lt.intensity = 45f;
                lt.range = 10f;
            }
            var lgoIsland = new GameObject("CeilingLight_Island");
            lgoIsland.transform.SetParent(stationGo.transform, false);
            lgoIsland.transform.localPosition = new Vector3(3.2f, 4.7f, 0f);
            var ltIsland = lgoIsland.AddComponent<Light>();
            ltIsland.type = LightType.Point;
            ltIsland.color = new Color(1f, 0.96f, 0.90f);
            ltIsland.intensity = 35f;
            ltIsland.range = 9f;

            // 7. Roadside Totem / Price Billboard Pylon (on off-road shoulder side at x = 4.8m)
            var totem = new GameObject("PriceTotem");
            totem.transform.SetParent(stationGo.transform, false);
            totem.transform.localPosition = new Vector3(4.8f, 0f, 6.0f);
            totem.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);

            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Tower";
            tower.transform.SetParent(totem.transform, false);
            tower.transform.localPosition = new Vector3(0f, 3.25f, 0f);
            tower.transform.localScale = new Vector3(1.1f, 6.5f, 0.35f);
            tower.GetComponent<Renderer>().sharedMaterial = darkMat;
            var tc = tower.GetComponent<Collider>(); if (tc) UnityEngine.Object.Destroy(tc);

            var crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crown.name = "Crown";
            crown.transform.SetParent(totem.transform, false);
            crown.transform.localPosition = new Vector3(0f, 6.4f, 0f);
            crown.transform.localScale = new Vector3(1.35f, 0.9f, 0.42f);
            crown.GetComponent<Renderer>().sharedMaterial = fasciaMat;
            var crc = crown.GetComponent<Collider>(); if (crc) UnityEngine.Object.Destroy(crc);

            var p95 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p95.name = "Price_RON95";
            p95.transform.SetParent(totem.transform, false);
            p95.transform.localPosition = new Vector3(0f, 4.9f, 0f);
            p95.transform.localScale = new Vector3(0.95f, 0.55f, 0.38f);
            p95.GetComponent<Renderer>().sharedMaterial = price95Mat;
            var p95c = p95.GetComponent<Collider>(); if (p95c) UnityEngine.Object.Destroy(p95c);

            var pDiesel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pDiesel.name = "Price_Diesel";
            pDiesel.transform.SetParent(totem.transform, false);
            pDiesel.transform.localPosition = new Vector3(0f, 4.0f, 0f);
            pDiesel.transform.localScale = new Vector3(0.95f, 0.55f, 0.38f);
            pDiesel.GetComponent<Renderer>().sharedMaterial = priceDieselMat;
            var pdc = pDiesel.GetComponent<Collider>(); if (pdc) UnityEngine.Object.Destroy(pdc);

            var pAuto = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pAuto.name = "Sign_Refuel";
            pAuto.transform.SetParent(totem.transform, false);
            pAuto.transform.localPosition = new Vector3(0f, 3.1f, 0f);
            pAuto.transform.localScale = new Vector3(0.95f, 0.45f, 0.38f);
            pAuto.GetComponent<Renderer>().sharedMaterial = screenMat;
            var pac = pAuto.GetComponent<Collider>(); if (pac) UnityEngine.Object.Destroy(pac);
        }

        private static void CreateMarker(Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m.name = "Marking";
            m.transform.SetParent(parent, false);
            m.transform.localPosition = localPos;
            m.transform.localScale = localScale;
            m.GetComponent<Renderer>().sharedMaterial = mat;
            var c = m.GetComponent<Collider>(); if (c) UnityEngine.Object.Destroy(c);
        }

        private static void CreateBollard(Transform parent, Vector3 localPos, Material mat)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            b.name = "Bollard";
            b.transform.SetParent(parent, false);
            b.transform.localPosition = localPos;
            b.transform.localScale = new Vector3(0.24f, 0.5f, 0.24f);
            b.GetComponent<Renderer>().sharedMaterial = mat;
            var bc = b.GetComponent<Collider>(); if (bc) UnityEngine.Object.Destroy(bc);
        }

        private static void InstantiateBinMesh(string binPath, Transform parent, string overrideMaterial = null, bool addCollider = false, bool colliderOnly = false, Vector3 localOffset = default)
        {
            var submeshes = LoadBinMesh(binPath);
            if (submeshes == null || submeshes.Count == 0) return;

            string baseName = Path.GetFileNameWithoutExtension(binPath);
            var groupGo = new GameObject(baseName);
            groupGo.transform.SetParent(parent, false);
            if (localOffset != Vector3.zero)
                groupGo.transform.localPosition = localOffset;

            foreach (var sub in submeshes)
            {
                if (sub.vertices == null || sub.vertices.Length == 0 || sub.triangles == null || sub.triangles.Length == 0)
                    continue;

                var mesh = new Mesh { name = sub.name };
                if (sub.vertices.Length > 65000 || sub.triangles.Length > 65000)
                    mesh.indexFormat = IndexFormat.UInt32;

                mesh.vertices = sub.vertices;
                if (sub.normals != null && sub.normals.Length == sub.vertices.Length)
                    mesh.normals = sub.normals;
                else
                    mesh.RecalculateNormals();

                if (sub.uvs != null && sub.uvs.Length == sub.vertices.Length)
                    mesh.uv = sub.uvs;

                mesh.triangles = sub.triangles;
                mesh.RecalculateBounds();

                var go = new GameObject(sub.name);
                go.transform.SetParent(groupGo.transform, false);

                if (!colliderOnly)
                {
                    var mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;

                    var mr = go.AddComponent<MeshRenderer>();
                    string matTag = !string.IsNullOrEmpty(overrideMaterial) ? overrideMaterial : sub.materialName;
                    mr.sharedMaterial = GetMaterial(matTag);
                }

                if (addCollider)
                {
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                }
            }
        }
    }
}
