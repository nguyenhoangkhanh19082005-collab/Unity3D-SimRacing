using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimRacing
{
    public sealed class ShowcaseStage : MonoBehaviour
    {
        public static ShowcaseStage Instance;

        private GameObject stageRoot;
        private Transform turntable;
        private GameObject currentCarInstance;
        private string displayedCarId = "";

        private Light keyLight;
        private Light rimLight;
        private Light fillLight;
        private Light underglow;

        private float rotationSpeed = 16f;
        private bool isDragging;
        private Vector3 lastMousePos;
        private float manualRotation;

        public void Initialize(Camera menuCamera)
        {
            if (stageRoot) return;
            Instance = this;

            stageRoot = new GameObject("Showcase Studio");
            stageRoot.transform.SetPositionAndRotation(new Vector3(0, -50f, 0), Quaternion.identity);

            // Studio Turntable Platform (centered slightly right to balance UI dock)
            Vector3 centerOffset = new Vector3(0.6f, 0, 0);

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Showcase_Pedestal";
            pedestal.transform.SetParent(stageRoot.transform, false);
            pedestal.transform.localPosition = centerOffset + new Vector3(0, -0.15f, 0);
            pedestal.transform.localScale = new Vector3(8.5f, 0.15f, 8.5f);

            var pedMat = new Material(VehicleModelUpgrade.LitShader);
            pedMat.SetColor("_BaseColor", new Color(0.06f, 0.08f, 0.11f, 1f));
            pedMat.SetFloat("_Metallic", 0.85f);
            pedMat.SetFloat("_Smoothness", 0.88f);
            pedestal.GetComponent<Renderer>().sharedMaterial = pedMat;
            UnityEngine.Object.Destroy(pedestal.GetComponent<Collider>());

            // Outer Neon Ring
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Showcase_NeonRing";
            ring.transform.SetParent(stageRoot.transform, false);
            ring.transform.localPosition = centerOffset + new Vector3(0, -0.16f, 0);
            ring.transform.localScale = new Vector3(9.2f, 0.02f, 9.2f);
            var ringMat = new Material(VehicleModelUpgrade.UnlitShader);
            ringMat.SetColor("_BaseColor", new Color(0.1f, 0.85f, 0.95f, 1f));
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            UnityEngine.Object.Destroy(ring.GetComponent<Collider>());

            // Turntable Pivot
            var pivotGo = new GameObject("Showcase_Turntable");
            pivotGo.transform.SetParent(stageRoot.transform, false);
            pivotGo.transform.localPosition = centerOffset;
            turntable = pivotGo.transform;

            // Studio Lights
            var keyGo = new GameObject("Key Light");
            keyGo.transform.SetParent(stageRoot.transform, false);
            keyGo.transform.localPosition = new Vector3(4.5f, 5.5f, 4.5f);
            keyLight = keyGo.AddComponent<Light>();
            keyLight.type = LightType.Spot;
            keyLight.range = 22f;
            keyLight.spotAngle = 65f;
            keyLight.intensity = 4.5f;
            keyLight.color = new Color(1f, 0.96f, 0.92f);
            keyLight.shadows = LightShadows.Soft;
            keyGo.transform.LookAt(stageRoot.transform.position + centerOffset + Vector3.up * 0.6f);

            var rimGo = new GameObject("Rim Light");
            rimGo.transform.SetParent(stageRoot.transform, false);
            rimGo.transform.localPosition = new Vector3(-3.5f, 3.2f, -4.5f);
            rimLight = rimGo.AddComponent<Light>();
            rimLight.type = LightType.Spot;
            rimLight.range = 20f;
            rimLight.spotAngle = 75f;
            rimLight.intensity = 5.5f;
            rimLight.color = new Color(0.2f, 0.85f, 1f); // Electric Cyan Rim
            rimGo.transform.LookAt(stageRoot.transform.position + centerOffset + Vector3.up * 0.5f);

            var fillGo = new GameObject("Warm Fill Light");
            fillGo.transform.SetParent(stageRoot.transform, false);
            fillGo.transform.localPosition = new Vector3(5.5f, 2.5f, -2.5f);
            fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 16f;
            fillLight.intensity = 2.0f;
            fillLight.color = new Color(1f, 0.45f, 0.2f); // Warm Amber Fill

            var underGo = new GameObject("Floor Underglow");
            underGo.transform.SetParent(stageRoot.transform, false);
            underGo.transform.localPosition = centerOffset + new Vector3(0, 0.2f, 0);
            underglow = underGo.AddComponent<Light>();
            underglow.type = LightType.Point;
            underglow.range = 7f;
            underglow.intensity = 2.2f;
            underglow.color = new Color(0.1f, 0.9f, 0.75f);

            if (menuCamera)
            {
                menuCamera.transform.position = stageRoot.transform.position + new Vector3(0.6f, 1.45f, -5.0f);
                menuCamera.transform.rotation = Quaternion.Euler(11f, 0f, 0f);
                menuCamera.clearFlags = CameraClearFlags.SolidColor;
                menuCamera.backgroundColor = new Color(0.015f, 0.025f, 0.04f, 1f);
            }
        }

        public void SetVisible(bool visible)
        {
            if (stageRoot) stageRoot.SetActive(visible);
        }

        public void ShowCar(string carId)
        {
            if (displayedCarId == carId && currentCarInstance != null) return;
            displayedCarId = carId;

            if (currentCarInstance != null)
            {
                UnityEngine.Object.Destroy(currentCarInstance);
                currentCarInstance = null;
            }

            if (!turntable) return;

            // Load high-poly vehicle model or prefab fallback
            currentCarInstance = VehicleModelUpgrade.LoadVehicleModel(carId, turntable);
            if (currentCarInstance == null)
            {
                string carName = carId.Contains("mercedes") ? "mercedes_w210" : carId.Contains("ferrari") ? "ferrari_f40" : "bugatti_centodieci";
                var req = Resources.Load<GameObject>("Vehicles/" + carName);
                if (req)
                {
                    currentCarInstance = UnityEngine.Object.Instantiate(req, turntable);
                    foreach (var rb in currentCarInstance.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.Destroy(rb);
                    foreach (var c in currentCarInstance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
                    foreach (var vc in currentCarInstance.GetComponentsInChildren<VehicleController>(true)) UnityEngine.Object.Destroy(vc);
                }
            }

            if (currentCarInstance != null)
            {
                currentCarInstance.transform.localPosition = Vector3.zero;
                currentCarInstance.transform.localRotation = Quaternion.Euler(0, 140f, 0);
                currentCarInstance.transform.localScale = Vector3.one;

                // Ensure two-sided rendering (_Cull = 0) on all showcase materials (Cách A)
                foreach (var r in currentCarInstance.GetComponentsInChildren<Renderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    Debug.Log($"[SHOWCASE_CHILD] {r.gameObject.name}: enabled={r.enabled} active={r.gameObject.activeInHierarchy} verts={mf?.sharedMesh?.vertexCount} bounds={mf?.sharedMesh?.bounds}");
                    foreach (var mat in r.materials)
                    {
                        if (mat)
                        {
                            mat.SetFloat("_Cull", 0f); // Two-Sided (Cách A)
                            mat.doubleSidedGI = true;
                            string mname = mat.name.ToLowerInvariant();
                            if (!mname.Contains("glass") && !mname.Contains("window"))
                            {
                                mat.SetFloat("_Surface", 0f); // Opaque
                                mat.SetFloat("_ZWrite", 1f);  // Depth write
                                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                            }
                        }
                    }
                }

                // Adjust rim light and underglow to match car aesthetic
                if (carId.Contains("ferrari"))
                {
                    if (rimLight) rimLight.color = new Color(1f, 0.2f, 0.15f);
                    if (underglow) underglow.color = new Color(1f, 0.15f, 0.1f);
                }
                else if (carId.Contains("bugatti"))
                {
                    if (rimLight) rimLight.color = new Color(0.15f, 0.75f, 1f);
                    if (underglow) underglow.color = new Color(0.1f, 0.8f, 1f);
                }
                else
                {
                    if (rimLight) rimLight.color = new Color(0.2f, 0.9f, 0.75f);
                    if (underglow) underglow.color = new Color(0.15f, 0.85f, 0.7f);
                }
            }
        }

        private void Update()
        {
            if (!stageRoot || !stageRoot.activeInHierarchy || !turntable) return;

            // Mouse drag interaction
            if (Input.GetMouseButtonDown(0))
            {
                if (Input.mousePosition.x > Screen.width * 0.25f && Input.mousePosition.y > Screen.height * 0.28f)
                {
                    isDragging = true;
                    lastMousePos = Input.mousePosition;
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            if (isDragging)
            {
                float deltaX = Input.mousePosition.x - lastMousePos.x;
                manualRotation -= deltaX * 0.45f;
                lastMousePos = Input.mousePosition;
                turntable.localRotation = Quaternion.Euler(0, manualRotation, 0);
            }
            else
            {
                manualRotation += rotationSpeed * Time.unscaledDeltaTime;
                turntable.localRotation = Quaternion.Euler(0, manualRotation, 0);
            }

            if (underglow)
            {
                float pulse = 0.85f + 0.35f * Mathf.Sin(Time.unscaledTime * 2.8f);
                underglow.intensity = 2.2f * pulse;
            }
        }

        private void OnDestroy()
        {
            if (stageRoot) UnityEngine.Object.Destroy(stageRoot);
        }
    }
}
