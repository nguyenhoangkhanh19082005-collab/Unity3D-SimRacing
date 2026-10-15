using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimRacing
{
    public static class VehicleModelUpgrade
    {
        private static readonly Dictionary<string, GameObject> CachedModels = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, GameObject> CachedDashboards = new Dictionary<string, GameObject>();
        private static Shader litShader;
        private static Shader unlitShader;

        public static Shader LitShader
        {
            get
            {
                if (!litShader)
                {
                    litShader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                             ?? Shader.Find("Standard");
                    if (!litShader)
                    {
                        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        {
                            if (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.Contains("Lit"))
                            {
                                litShader = r.sharedMaterial.shader;
                                break;
                            }
                        }
                    }
                }
                return litShader;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (!unlitShader)
                {
                    unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                               ?? Shader.Find("Unlit/Color");
                }
                return unlitShader;
            }
        }

        public static bool HasImportedModel(string carId)
        {
            string path = GetModelPath(carId);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public static string GetModelPath(string carId)
        {
            string rel = "";
            if (carId.Contains("ferrari"))
                rel = "Imported/ferrari-f40/source/f40/f40.obj";
            else if (carId.Contains("mercedes"))
                rel = "Imported/mercedes-benz-e-class-w210-sedan/source/Mercedes-Benz_E-class_W210_sedan_1996/Mercedes-Benz_E-class_W210_sedan_1996.obj";
            else if (carId.Contains("bugatti"))
                rel = "Imported/bugatti-centodieci/source/centi/centi.obj";
            else return null;

            string[] candidates = new[]
            {
                Path.Combine(Application.dataPath, rel),
                Path.Combine(Application.dataPath, "..", "Assets", rel),
                Path.Combine(Application.dataPath, "..", "..", "Assets", rel),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Assets", rel)),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets", rel)),
                Path.GetFullPath(Path.Combine("E:/Unity3D-SimRacing/Assets", rel))
            };

            foreach (var p in candidates)
            {
                try
                {
                    if (File.Exists(p)) return Path.GetFullPath(p);
                }
                catch { }
            }
            return null;
        }

        public static GameObject LoadVehicleModel(string carId, Transform parent = null)
        {
            string key = carId.Contains("ferrari") ? "ferrari" : carId.Contains("bugatti") ? "bugatti" : "mercedes";

            if (CachedModels.TryGetValue(key, out GameObject cached) && cached != null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(cached, parent);
                clone.SetActive(true);
                return clone;
            }

            string objPath = GetModelPath(key);
            if (string.IsNullOrEmpty(objPath) || !File.Exists(objPath))
            {
                Debug.LogWarning("[VehicleModelUpgrade] Model file not found for: " + carId);
                return null;
            }

            GameObject loaded = null;
            try
            {
                if (key == "ferrari")
                    loaded = LoadFerrariF40(objPath);
                else if (key == "mercedes")
                    loaded = LoadMercedesW210(objPath);
                else if (key == "bugatti")
                    loaded = LoadBugattiCentodieci(objPath);

                if (loaded != null)
                {
                    loaded.name = key + "_HighPoly";
                    loaded.SetActive(false);
                    CachedModels[key] = loaded;

                    GameObject instance = UnityEngine.Object.Instantiate(loaded, parent);
                    instance.SetActive(true);
                    return instance;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VehicleModelUpgrade] Error loading {key}: {ex.Message}\n{ex.StackTrace}");
            }

            return null;
        }

        private static GameObject LoadFerrariF40(string path)
        {
            string mtlPath = Path.Combine(Path.GetDirectoryName(path), "f40.mtl");
            var materials = LoadMtl(mtlPath);

            foreach (var kvp in materials)
            {
                string name = kvp.Key.ToLowerInvariant();
                Material m = kvp.Value;
                if (name == "f40") // Rosso Corsa
                {
                    m.SetColor("_BaseColor", new Color(0.85f, 0.05f, 0.03f, 1f));
                    m.SetFloat("_Metallic", 0.65f);
                    m.SetFloat("_Smoothness", 0.92f);
                }
                else if (name.Contains("cockpit"))
                {
                    m.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.13f, 1f));
                    m.SetFloat("_Metallic", 0.05f);
                    m.SetFloat("_Smoothness", 0.35f);
                }
                else if (name.Contains("chrome"))
                {
                    m.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.97f, 1f));
                    m.SetFloat("_Metallic", 0.95f);
                    m.SetFloat("_Smoothness", 0.96f);
                }
                else if (name.Contains("glass") || name.Contains("window"))
                {
                    SetTransparent(m, new Color(0.1f, 0.15f, 0.2f, 0.35f), 0.98f);
                }
                else if (name.Contains("brake"))
                {
                    m.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.58f, 1f));
                    m.SetFloat("_Metallic", 0.85f);
                    m.SetFloat("_Smoothness", 0.75f);
                }
                else if (name.Contains("brakelight"))
                {
                    m.SetColor("_BaseColor", new Color(0.9f, 0.05f, 0.02f, 1f));
                    m.SetColor("_EmissionColor", new Color(1.2f, 0.1f, 0.05f, 1f));
                    m.EnableKeyword("_EMISSION");
                }
            }

            return ParseObj(path, materials, 1.0f, Quaternion.identity, Vector3.zero);
        }

        private static GameObject LoadMercedesW210(string path)
        {
            string mtlPath = Path.Combine(Path.GetDirectoryName(path), "Mercedes-Benz_E-class_W210_sedan_1996.mtl");
            var materials = LoadMtl(mtlPath);

            foreach (var kvp in materials)
            {
                string name = kvp.Key.ToLowerInvariant();
                Material m = kvp.Value;
                if (name == "carpaint")
                {
                    SetOpaque(m, new Color(0.78f, 0.82f, 0.85f, 1f), 0.72f, 0.91f, twoSided: true);
                }
                else if (name == "chrome")
                {
                    SetOpaque(m, new Color(0.95f, 0.96f, 0.98f, 1f), 0.98f, 0.97f, twoSided: true);
                }
                else if (name == "redglass")
                {
                    SetTransparent(m, new Color(0.85f, 0.08f, 0.05f, 0.75f), 0.95f);
                    m.SetFloat("_Cull", 0f);
                    m.doubleSidedGI = true;
                }
                else if (name.Contains("glass"))
                {
                    SetTransparent(m, new Color(0.15f, 0.22f, 0.26f, 0.38f), 0.95f);
                    m.SetFloat("_Cull", 0f);
                    m.doubleSidedGI = true;
                }
                else if (name == "tire")
                {
                    SetOpaque(m, new Color(0.12f, 0.12f, 0.13f, 1f), 0.02f, 0.22f, twoSided: true);
                }
                else if (name == "rim")
                {
                    SetOpaque(m, new Color(0.88f, 0.9f, 0.92f, 1f), 0.88f, 0.86f, twoSided: true);
                }
                else if (name == "interior")
                {
                    SetOpaque(m, new Color(0.18f, 0.16f, 0.14f, 1f), 0.05f, 0.38f, twoSided: true);
                }
                else if (name == "black")
                {
                    SetOpaque(m, new Color(0.10f, 0.10f, 0.10f, 1f), 0.10f, 0.40f, twoSided: true);
                }
                else if (name == "white")
                {
                    SetOpaque(m, new Color(0.92f, 0.92f, 0.92f, 1f), 0.05f, 0.50f, twoSided: true);
                }
                else if (name == "brakedisk")
                {
                    SetOpaque(m, new Color(0.55f, 0.55f, 0.58f, 1f), 0.85f, 0.75f, twoSided: true);
                }
                else
                {
                    m.SetFloat("_Cull", 0f);
                    m.doubleSidedGI = true;
                }
            }

            Quaternion rot = Quaternion.Euler(-90f, 0f, 0f);
            var root = ParseObj(path, materials, 0.01f, rot, new Vector3(0, -0.1f, 0));
            if (root)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var mat in r.materials)
                    {
                        if (mat)
                        {
                            mat.SetFloat("_Cull", 0f); // Two-Sided (Cách A)
                            mat.doubleSidedGI = true;
                        }
                    }
                }
            }
            return root;
        }

        private static GameObject LoadBugattiCentodieci(string path)
        {
            var materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            string dir = Path.GetDirectoryName(path);

            Material bodyMat = new Material(LitShader) { name = "Bugatti_Body" };
            bodyMat.SetColor("_BaseColor", new Color(0.08f, 0.12f, 0.22f, 1f)); // Midnight French Racing Blue
            bodyMat.SetFloat("_Metallic", 0.88f);
            bodyMat.SetFloat("_Smoothness", 0.96f);
            materials["Meshpart4Mtl"] = bodyMat;
            materials["Meshpart7Mtl"] = bodyMat;
            materials["Meshpart12Mtl"] = bodyMat;
            materials["11Mtl"] = bodyMat;
            materials["F1Mtl"] = bodyMat;
            materials["Meshpart6Mtl"] = bodyMat;
            materials["Meshpart11Mtl"] = bodyMat;
            materials["Meshpart13Mtl"] = bodyMat;
            materials["Meshpart29Mtl"] = bodyMat;

            Material bugattiTaillight = new Material(LitShader) { name = "Bugatti_TailLight" };
            bugattiTaillight.SetColor("_BaseColor", new Color(0.85f, 0.08f, 0.05f, 1f));
            bugattiTaillight.SetFloat("_Metallic", 0.2f);
            bugattiTaillight.SetFloat("_Smoothness", 0.95f);
            materials["Meshpart30Mtl"] = bugattiTaillight;

            Material carbonMat = new Material(LitShader) { name = "Bugatti_Carbon" };
            carbonMat.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.09f, 1f));
            carbonMat.SetFloat("_Metallic", 0.4f);
            carbonMat.SetFloat("_Smoothness", 0.72f);
            materials["Meshpart1Mtl"] = carbonMat;
            materials["Meshpart2Mtl"] = carbonMat;
            materials["Meshpart3Mtl"] = carbonMat;
            materials["Meshpart14Mtl"] = carbonMat;
            materials["Meshpart28Mtl"] = carbonMat;

            Material glassMat = new Material(LitShader) { name = "Bugatti_Glass" };
            SetTransparent(glassMat, new Color(0.85f, 0.92f, 0.96f, 0.18f), 0.98f);
            glassMat.SetFloat("_Cull", 0f);
            glassMat.doubleSidedGI = true;
            materials["Window1Mtl"] = glassMat;
            materials["Mirrors1Mtl"] = glassMat;

            Material chromeMat = new Material(LitShader) { name = "Bugatti_Chrome" };
            chromeMat.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.98f, 1f));
            chromeMat.SetFloat("_Metallic", 0.95f);
            chromeMat.SetFloat("_Smoothness", 0.97f);
            materials["Badges1Mtl"] = chromeMat;

            Material rimMat = new Material(LitShader) { name = "Bugatti_Rim" };
            rimMat.SetColor("_BaseColor", new Color(0.85f, 0.88f, 0.90f, 1f));
            rimMat.SetFloat("_Metallic", 0.92f);
            rimMat.SetFloat("_Smoothness", 0.90f);
            materials["Wheel1Mtl"] = rimMat;

            Material tireMat = new Material(LitShader) { name = "Bugatti_Tire" };
            tireMat.SetColor("_BaseColor", new Color(0.11f, 0.11f, 0.12f, 1f));
            tireMat.SetFloat("_Metallic", 0.02f);
            tireMat.SetFloat("_Smoothness", 0.25f);
            materials["Tire1Mtl"] = tireMat;

            string texPath = Path.Combine(dir, "Meshpart21_diff.png");
            if (File.Exists(texPath))
            {
                byte[] bytes = File.ReadAllBytes(texPath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    Material texMat = new Material(LitShader) { name = "Bugatti_Diff" };
                    texMat.SetTexture("_BaseMap", tex);
                    texMat.SetFloat("_Smoothness", 0.85f);
                    materials["Meshpart21Mtl"] = texMat;
                }
            }

            // Real Bugatti specs: wheelbase 2.71m / 12.74 units in centi.obj = 0.2127 scale
            // Rotated 180 degrees around Y so nose points towards +Z (forward) and spoiler points towards -Z (rear)
            float scale = 0.2127f;
            Quaternion rot = Quaternion.Euler(0f, 180f, 0f);
            Vector3 centerOffset = -(rot * new Vector3(-79.00f, 0.05f, -166.85f) * scale);
            var root = ParseObj(path, materials, scale, rot, centerOffset, mirrorX: false);
            if (root)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var mat in r.materials)
                    {
                        if (mat)
                        {
                            mat.SetFloat("_Cull", 0f);
                            mat.doubleSidedGI = true;
                        }
                    }
                }
            }
            return root;
        }

        private static void SetTransparent(Material m, Color color, float smoothness)
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_Smoothness", smoothness);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void SetOpaque(Material m, Color color, float metallic, float smoothness, bool twoSided = true)
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Surface", 0f); // Opaque
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_Cull", twoSided ? 0f : 2f); // 0 = Off (Two-Sided, Cách A)
            m.doubleSidedGI = twoSided;
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Geometry; // 2000
        }

        private static Dictionary<string, Material> LoadMtl(string mtlPath)
        {
            var dict = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(mtlPath)) return dict;

            Material currentMat = null;
            string currentName = "";

            foreach (string line in File.ReadLines(mtlPath))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                string[] parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                if (parts[0] == "newmtl")
                {
                    currentName = parts[1];
                    currentMat = new Material(LitShader) { name = currentName };
                    currentMat.SetColor("_BaseColor", Color.white);
                    currentMat.SetFloat("_Smoothness", 0.5f);
                    currentMat.SetFloat("_Metallic", 0.0f);
                    dict[currentName] = currentMat;
                }
                else if (currentMat != null)
                {
                    if (parts[0] == "Kd" && parts.Length >= 4)
                    {
                        float r = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float g = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float b = float.Parse(parts[3], CultureInfo.InvariantCulture);
                        Color c = currentMat.GetColor("_BaseColor");
                        currentMat.SetColor("_BaseColor", new Color(r, g, b, c.a));
                    }
                    else if (parts[0] == "Ks" && parts.Length >= 4)
                    {
                        float r = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        currentMat.SetFloat("_Metallic", Mathf.Clamp01(r));
                    }
                    else if (parts[0] == "Ns")
                    {
                        float ns = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        currentMat.SetFloat("_Smoothness", Mathf.Clamp01(ns / 1000f));
                    }
                    else if (parts[0] == "d")
                    {
                        float alpha = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        Color c = currentMat.GetColor("_BaseColor");
                        currentMat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
                        if (alpha < 0.95f)
                        {
                            SetTransparent(currentMat, new Color(c.r, c.g, c.b, alpha), currentMat.GetFloat("_Smoothness"));
                        }
                    }
                    else if (parts[0] == "Ke" && parts.Length >= 4)
                    {
                        float r = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float g = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float b = float.Parse(parts[3], CultureInfo.InvariantCulture);
                        Color ke = new Color(r, g, b);
                        if (ke.maxColorComponent > 0.05f)
                        {
                            currentMat.SetColor("_EmissionColor", ke);
                            currentMat.EnableKeyword("_EMISSION");
                        }
                    }
                }
            }

            return dict;
        }

        private static GameObject ParseObj(string objPath, Dictionary<string, Material> materials, float scale, Quaternion rotation, Vector3 offset, bool mirrorX = false)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(objPath));
            var rawPositions = new List<Vector3>(250000);

            // Group faces by both object/group name and material name
            var groupFaces = new Dictionary<string, List<int>>();
            string currentGroup = "Default";
            string currentMtl = "default";
            bool isBugatti = scale > 0.20f && scale < 0.23f;

            foreach (string line in File.ReadLines(objPath))
            {
                if (string.IsNullOrEmpty(line) || line.Length < 2) continue;

                char c0 = line[0];
                char c1 = line[1];

                if (c0 == 'v' && c1 == ' ')
                {
                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float z = float.Parse(parts[3], CultureInfo.InvariantCulture);

                        // Eliminate the 10 artifact origin vertices in Bugatti model
                        if (isBugatti && x > -50f)
                        {
                            x = -79.00f; y = 0.05f; z = -166.85f;
                        }

                        Vector3 v = rotation * new Vector3(x, y, z) * scale + offset;
                        rawPositions.Add(v);
                    }
                }
                else if (c0 == 'o' && c1 == ' ')
                {
                    currentGroup = line.Substring(2).Trim();
                }
                else if (c0 == 'g' && c1 == ' ')
                {
                    currentGroup = line.Substring(2).Trim();
                }
                else if (line.StartsWith("usemtl "))
                {
                    currentMtl = line.Substring(7).Trim();
                }
                else if (c0 == 'f' && c1 == ' ')
                {
                    string key = currentGroup + "|" + currentMtl;
                    if (!groupFaces.TryGetValue(key, out var faceList))
                    {
                        faceList = new List<int>();
                        groupFaces[key] = faceList;
                    }

                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int faceVertCount = parts.Length - 1;
                    if (faceVertCount >= 3)
                    {
                        int v0 = ParseObjVertexIndex(parts[1], rawPositions.Count);
                        for (int i = 2; i < faceVertCount; i++)
                        {
                            int v1 = ParseObjVertexIndex(parts[i], rawPositions.Count);
                            int v2 = ParseObjVertexIndex(parts[i + 1], rawPositions.Count);
                            faceList.Add(v0);
                            faceList.Add(v1);
                            faceList.Add(v2);
                        }
                    }
                }
            }

            // Create Submeshes
            foreach (var kvp in groupFaces)
            {
                string key = kvp.Key;
                List<int> faceIndices = kvp.Value;
                if (faceIndices.Count < 3) continue;

                string[] spl = key.Split('|');
                string grpName = spl[0];
                string mtlName = spl.Length > 1 ? spl[1] : "default";

                Mesh mesh = CreateSubmesh(rawPositions, faceIndices, mirrorX);
                if (mesh == null || mesh.vertexCount == 0) continue;

                var subGo = new GameObject(grpName);
                subGo.transform.SetParent(root.transform, false);

                var filter = subGo.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;

                var renderer = subGo.AddComponent<MeshRenderer>();
                if (materials.TryGetValue(mtlName, out Material mat))
                {
                    renderer.sharedMaterial = mat;
                }
                else
                {
                    var fallback = new Material(LitShader) { name = mtlName };
                    fallback.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f));
                    fallback.SetFloat("_Smoothness", 0.6f);
                    fallback.SetFloat("_Metallic", 0.3f);
                    renderer.sharedMaterial = fallback;
                }
            }

            return root;
        }

        private static int ParseObjVertexIndex(string part, int totalVerts)
        {
            int slash = part.IndexOf('/');
            string vStr = slash >= 0 ? part.Substring(0, slash) : part;
            int idx = int.Parse(vStr);
            return idx > 0 ? idx - 1 : totalVerts + idx;
        }

        private static Mesh CreateSubmesh(List<Vector3> allPositions, List<int> faceIndices, bool mirrorX)
        {
            var uniqueVertMap = new Dictionary<int, int>();
            var meshVerts = new List<Vector3>();
            var meshTris = new List<int>();

            for (int i = 0; i < faceIndices.Count; i++)
            {
                int origIdx = faceIndices[i];
                if (origIdx < 0 || origIdx >= allPositions.Count) continue;

                if (!uniqueVertMap.TryGetValue(origIdx, out int newIdx))
                {
                    newIdx = meshVerts.Count;
                    meshVerts.Add(allPositions[origIdx]);
                    uniqueVertMap[origIdx] = newIdx;
                }
                meshTris.Add(newIdx);
            }

            if (meshVerts.Count == 0 || meshTris.Count < 3) return null;

            if (mirrorX)
            {
                int baseCount = meshVerts.Count;
                int triCount = meshTris.Count;

                for (int i = 0; i < baseCount; i++)
                {
                    Vector3 v = meshVerts[i];
                    meshVerts.Add(new Vector3(-v.x, v.y, v.z));
                }

                for (int i = 0; i < triCount; i += 3)
                {
                    meshTris.Add(baseCount + meshTris[i]);
                    meshTris.Add(baseCount + meshTris[i + 2]);
                    meshTris.Add(baseCount + meshTris[i + 1]);
                }
            }

            Mesh mesh = new Mesh
            {
                indexFormat = meshVerts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(meshVerts);
            mesh.SetTriangles(meshTris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static string GetDashboardPath()
        {
            string rel = "Imported/3d-model.obj";
            string[] candidates = new[]
            {
                Path.Combine(Application.dataPath, rel),
                Path.Combine(Application.dataPath, "..", "Assets", rel),
                Path.Combine(Application.dataPath, "..", "..", "Assets", rel),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Assets", rel)),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets", rel)),
                Path.GetFullPath(Path.Combine("E:/Unity3D-SimRacing/Assets", rel)),
                Path.GetFullPath(Path.Combine("E:/Unity3D-SimRacing/Builds/Windows/SimRacing_Data", rel))
            };

            foreach (var p in candidates)
            {
                try
                {
                    if (File.Exists(p)) return Path.GetFullPath(p);
                }
                catch { }
            }
            return null;
        }

        public static GameObject LoadUniversalDashboard(string carId, Transform parent = null)
        {
            string key = carId.Contains("ferrari") ? "ferrari" : carId.Contains("bugatti") ? "bugatti" : "mercedes";

            if (CachedDashboards.TryGetValue(key, out GameObject cached) && cached != null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(cached, parent);
                clone.SetActive(true);
                return clone;
            }

            string dashPath = GetDashboardPath();
            if (string.IsNullOrEmpty(dashPath) || !File.Exists(dashPath))
            {
                Debug.LogWarning("[VehicleModelUpgrade] Dashboard model not found at: " + dashPath);
                return null;
            }

            var materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            Color trimColor, seatColor, accentColor;
            float trimMetallic, trimSmoothness;

            if (key == "ferrari")
            {
                seatColor = new Color(0.72f, 0.06f, 0.04f, 1f);
                trimColor = new Color(0.12f, 0.12f, 0.12f, 1f);
                trimMetallic = 0.30f;
                trimSmoothness = 0.50f;
                accentColor = new Color(0.92f, 0.08f, 0.05f, 1f);
            }
            else if (key == "bugatti")
            {
                seatColor = new Color(0.10f, 0.12f, 0.18f, 1f);
                trimColor = new Color(0.32f, 0.36f, 0.40f, 1f);
                trimMetallic = 0.75f;
                trimSmoothness = 0.70f;
                accentColor = new Color(0.08f, 0.45f, 0.95f, 1f);
            }
            else
            {
                seatColor = new Color(0.13f, 0.13f, 0.14f, 1f);
                trimColor = new Color(0.28f, 0.16f, 0.08f, 1f);
                trimMetallic = 0.05f;
                trimSmoothness = 0.75f;
                accentColor = new Color(0.20f, 0.88f, 0.74f, 1f);
            }

            Action<string, Color, float, float, Color> addMat = (mName, baseCol, metallic, smoothness, emission) =>
            {
                var mat = new Material(LitShader) { name = mName };
                mat.SetColor("_BaseColor", baseCol);
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Cull", 0f);
                mat.doubleSidedGI = true;
                if (emission.maxColorComponent > 0.02f)
                {
                    mat.SetColor("_EmissionColor", emission);
                    mat.EnableKeyword("_EMISSION");
                }
                materials[mName] = mat;
            };

            Color charcoalCol = new Color(0.12f, 0.12f, 0.14f, 1f);
            addMat("_Charcoal_", charcoalCol, 0.04f, 0.35f, Color.black);
            addMat("0136_Charcoal", charcoalCol, 0.04f, 0.35f, Color.black);
            addMat("0137_Black", new Color(0.08f, 0.08f, 0.09f, 1f), 0.05f, 0.30f, Color.black);
            addMat("preto__spec_1", new Color(0.09f, 0.09f, 0.10f, 1f), 0.04f, 0.32f, Color.black);
            addMat("0135_DarkGray", new Color(0.18f, 0.18f, 0.20f, 1f), 0.05f, 0.38f, Color.black);
            addMat("96_96_96", new Color(0.25f, 0.25f, 0.27f, 1f), 0.05f, 0.40f, Color.black);
            addMat("Material1", charcoalCol, 0.04f, 0.35f, Color.black);
            addMat("Material8", charcoalCol, 0.04f, 0.35f, Color.black);
            addMat("skp_front_default", charcoalCol, 0.04f, 0.35f, Color.black);

            Color chromeCol = new Color(0.92f, 0.93f, 0.95f, 1f);
            addMat("0131_Silver", chromeCol, 0.92f, 0.88f, Color.black);
            addMat("Plastic_Silver", chromeCol, 0.88f, 0.85f, Color.black);

            addMat("0128_White", new Color(0.95f, 0.95f, 0.98f, 1f), 0.0f, 0.80f, new Color(0.85f, 0.88f, 0.92f));
            addMat("Light_White_Reflective", new Color(0.92f, 0.95f, 0.98f, 1f), 0.1f, 0.95f, new Color(0.55f, 0.65f, 0.75f));

            addMat("0020_Red", new Color(0.95f, 0.10f, 0.05f, 1f), 0.0f, 0.75f, new Color(0.85f, 0.08f, 0.05f));
            addMat("0076_Green", new Color(0.12f, 0.92f, 0.22f, 1f), 0.0f, 0.75f, new Color(0.10f, 0.85f, 0.18f));
            addMat("0038_Orange", new Color(1.0f, 0.55f, 0.05f, 1f), 0.0f, 0.75f, new Color(0.90f, 0.48f, 0.05f));

            addMat("Rubber_Plastic1", new Color(0.09f, 0.09f, 0.10f, 1f), 0.02f, 0.22f, Color.black);

            addMat("Matte__FFA08B5F1", trimColor, trimMetallic, trimSmoothness, Color.black);
            addMat("Matte__FF1", trimColor, trimMetallic, trimSmoothness, Color.black);
            addMat("Matte__FF5", trimColor, trimMetallic, trimSmoothness, Color.black);
            addMat("Matte__FF5B5B5B1", new Color(0.18f, 0.18f, 0.19f, 1f), 0.05f, 0.35f, Color.black);
            addMat("Matte__FFFF1", chromeCol, 0.85f, 0.85f, Color.black);

            addMat("0022_Maroon", seatColor, 0.03f, 0.32f, Color.black);
            addMat("banco_001", seatColor, 0.03f, 0.32f, Color.black);
            addMat("banco_002", seatColor, 0.03f, 0.32f, Color.black);
            addMat("001__spec_1", seatColor, 0.03f, 0.32f, Color.black);
            addMat("jean_blue", new Color(0.14f, 0.15f, 0.18f, 1f), 0.02f, 0.25f, Color.black);
            addMat("brakes", new Color(0.10f, 0.10f, 0.12f, 1f), 0.05f, 0.30f, Color.black);
            addMat("drop_result", accentColor, 0.10f, 0.60f, Color.black);

            GameObject dashRoot = ParseObj(dashPath, materials, 0.020f, Quaternion.identity, Vector3.zero);
            if (dashRoot == null) return null;

            dashRoot.name = "UniversalDashboard_" + key;

            var steerPivot = new GameObject("UniversalSteeringWheel");
            steerPivot.transform.SetParent(dashRoot.transform, false);

            Vector3 wheelCenter = new Vector3(17.035f, 27.02f, 21.35f) * 0.020f;
            steerPivot.transform.localPosition = wheelCenter;

            var steerChildren = new List<Transform>();
            foreach (Transform child in dashRoot.transform)
            {
                if (child == steerPivot.transform) continue;
                string n = child.name;
                bool isWheelPart = n.Contains("Group_014") || n.Contains("Group_015") ||
                                   n.Contains("Group_016") || n.Contains("Group_017") ||
                                   n.Contains("Group_018") || n.Contains("Group_019") ||
                                   n.Contains("Group_020") || n.Contains("Group_021");
                if (isWheelPart) steerChildren.Add(child);
            }

            foreach (var stChild in steerChildren)
            {
                stChild.SetParent(steerPivot.transform, true);
            }

            // Unmirror instrument cluster digital display (Group_012) so text reads forwards
            var g12 = dashRoot.transform.Find("Group_012");
            if (g12)
            {
                var mf = g12.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh)
                {
                    var m = UnityEngine.Object.Instantiate(mf.sharedMesh);
                    var v = m.vertices;
                    float scale = 0.020f;
                    float cx = 16.118f * scale;
                    for (int i = 0; i < v.Length; i++)
                    {
                        v[i].x = 2f * cx - v[i].x;
                    }
                    var t = m.triangles;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        int tmp = t[i + 1];
                        t[i + 1] = t[i + 2];
                        t[i + 2] = tmp;
                    }
                    m.vertices = v;
                    m.triangles = t;
                    m.RecalculateNormals();
                    m.RecalculateBounds();
                    mf.sharedMesh = m;
                }
            }

            foreach (var r in dashRoot.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.materials)
                {
                    if (mat) mat.SetFloat("_Cull", 0f);
                }
            }

            dashRoot.SetActive(false);
            CachedDashboards[key] = dashRoot;

            GameObject inst = UnityEngine.Object.Instantiate(dashRoot, parent);
            inst.SetActive(true);
            return inst;
        }

        /// <summary>
        /// Enhances an instantiated car in the game scene with the high-poly imported model
        /// </summary>
        public static void EnhanceVehicleInstance(VehicleController car)
        {
            if (!car || !car.definition) return;

            string id = car.definition.id;
            if (!HasImportedModel(id)) return;

            try
            {
                GameObject highPoly = LoadVehicleModel(id, car.transform);
                if (!highPoly) return;

                highPoly.transform.localPosition = Vector3.zero;
                highPoly.transform.localRotation = Quaternion.identity;
                highPoly.transform.localScale = Vector3.one;

                // Permanently destroy all legacy placeholder meshes and prototype light boxes from base prefab
                var legacyRenderers = new List<MeshRenderer>();
                foreach (var mr in car.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // Preserve meshes belonging to our new highPoly model
                    if (mr.transform.IsChildOf(highPoly.transform)) continue;
                    // Preserve meshes belonging to cockpit (universal dashboard)
                    if (car.definition.cockpit && mr.transform.IsChildOf(car.definition.cockpit)) continue;

                    // Preserve wheels in wheelVisuals
                    bool isWheel = false;
                    if (car.definition.wheelVisuals != null)
                    {
                        foreach (var wv in car.definition.wheelVisuals)
                        {
                            if (wv && (mr.transform == wv || mr.transform.IsChildOf(wv)))
                            {
                                isWheel = true;
                                break;
                            }
                        }
                    }
                    if (isWheel) continue;

                    legacyRenderers.Add(mr);
                }
                foreach (var mr in legacyRenderers)
                {
                    if (mr && mr.gameObject != car.gameObject)
                    {
                        mr.enabled = false;
                        mr.gameObject.SetActive(false);
                        if (mr.transform.parent != null && mr.transform.parent.gameObject != car.gameObject && !mr.transform.parent.IsChildOf(highPoly.transform))
                        {
                            mr.transform.parent.gameObject.SetActive(false);
                        }
                        UnityEngine.Object.Destroy(mr.gameObject);
                    }
                }

                // Identify interior vs exterior nodes
                var interiorNodes = new List<Transform>();
                var exteriorNodes = new List<Transform>();

                foreach (Transform child in highPoly.transform)
                {
                    string n = child.name.ToLowerInvariant();
                    var mr = child.GetComponent<MeshRenderer>();
                    string matName = mr && mr.sharedMaterial ? mr.sharedMaterial.name.ToLowerInvariant() : "";
                    bool isCockpit = n.Contains("dash") || n.Contains("steer") || n.Contains("seat") ||
                                     n.Contains("needle") || n.Contains("pedal") || n.Contains("shifter") ||
                                     n.Contains("intmirror") || n.Contains("interior") || n.Contains("cockpit") ||
                                     matName.Contains("interior") ||
                                     n.StartsWith("plane133") || n.StartsWith("plane032") || n.StartsWith("plane035") ||
                                     n.StartsWith("plane046") || n.StartsWith("doorinetrioir") || n.StartsWith("cylinder11") ||
                                     n.StartsWith("cylinder10") || n.StartsWith("box04") || n.StartsWith("box03") ||
                                     n.StartsWith("torus") || n.StartsWith("mirror_dark") || n.StartsWith("plane058") ||
                                     n.StartsWith("cylinder090") ||
                                     n.StartsWith("meshpart2") || n.StartsWith("meshpart31");

                    if (isCockpit)
                    {
                        interiorNodes.Add(child);
                        if (n.Contains("steer")) car.definition.steeringWheel = child;
                        if (n.Contains("needle_speedo")) car.definition.speedNeedle = child;
                        if (n.Contains("needle_tacho")) car.definition.rpmNeedle = child;
                        if (n.Contains("intmirror")) car.definition.mirror = child.GetComponent<Renderer>();
                    }
                    else
                    {
                        exteriorNodes.Add(child);
                    }
                }

                // Attach exterior nodes to definition.exterior (roof, windshield, doors, hood, pillars)
                if (car.definition.exterior)
                {
                    foreach (var r in car.definition.exterior.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        r.enabled = false;
                    }
                    foreach (var ext in exteriorNodes)
                    {
                        ext.SetParent(car.definition.exterior, true);
                        ext.gameObject.SetActive(true);
                        var mr = ext.GetComponent<MeshRenderer>();
                        if (mr) mr.enabled = true;
                    }
                    car.definition.exterior.gameObject.SetActive(true);
                }

                // Attach interior nodes to definition.cockpit
                if (car.definition.cockpit)
                {
                    // Deactivate existing low-poly cockpit children and 3D labels
                    foreach (Transform child in car.definition.cockpit)
                    {
                        child.gameObject.SetActive(false);
                    }

                    if (car.definition.digitalSpeed)
                    {
                        car.definition.digitalSpeed.gameObject.SetActive(false);
                        car.definition.digitalSpeed = null;
                    }
                    if (car.definition.digitalGear)
                    {
                        car.definition.digitalGear.gameObject.SetActive(false);
                        car.definition.digitalGear = null;
                    }
                    if (car.definition.speedNeedle)
                    {
                        car.definition.speedNeedle.gameObject.SetActive(false);
                        car.definition.speedNeedle = null;
                    }
                    if (car.definition.rpmNeedle)
                    {
                        car.definition.rpmNeedle.gameObject.SetActive(false);
                        car.definition.rpmNeedle = null;
                    }

                    // CRITICAL: Deactivate all interior nodes from the imported car model so they never occlude cockpit camera!
                    foreach (var inNode in interiorNodes)
                    {
                        inNode.gameObject.SetActive(false);
                        var mr = inNode.GetComponent<MeshRenderer>();
                        if (mr) mr.enabled = false;
                    }

                    // Attach the high-detail Universal Dashboard (3d-model.obj)
                    GameObject universalDash = LoadUniversalDashboard(id, car.definition.cockpit);
                    if (universalDash != null)
                    {
                        Vector3 dashOffset;
                        if (id.Contains("ferrari"))
                            dashOffset = new Vector3(-0.02f, 0.31f, 0.12f);
                        else if (id.Contains("bugatti"))
                            dashOffset = new Vector3(0.00f, 0.30f, 0.05f);
                        else
                            dashOffset = new Vector3(0.00f, 0.38f, -0.20f);

                        universalDash.transform.localPosition = dashOffset;
                        universalDash.transform.localRotation = Quaternion.identity;
                        universalDash.transform.localScale = Vector3.one;

                        var sw = universalDash.transform.Find("UniversalSteeringWheel");
                        if (sw) car.definition.steeringWheel = sw;
                    }
                    else
                    {
                        foreach (var inNode in interiorNodes)
                        {
                            inNode.gameObject.SetActive(true);
                            inNode.SetParent(car.definition.cockpit, true);
                        }
                    }
                }

                // Refresh light detectors to glow high-poly lights
                car.GetComponent<VehicleEffects>()?.RefreshLights();

                // Initial cockpit state
                car.SetCockpit(false);

                Debug.Log($"[VehicleModelUpgrade] Successfully enhanced {id} with high-detail model & universal cockpit!");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VehicleModelUpgrade] Error enhancing {id}: {ex.Message}");
            }
        }
    }
}
