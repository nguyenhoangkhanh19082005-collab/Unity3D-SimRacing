using UnityEngine;

namespace SimRacing
{
    public sealed class TrackDefinition : MonoBehaviour
    {
        public string id;
        public Vector3[] route;
        public Vector3 spawnPosition, fuelStation, speedTrap;
        public Quaternion spawnRotation;
        public float roadHalfWidth = 7;
        public float Length { get; private set; }

        void Awake()
        {
            Length = 0;
            for (int i = 0; i < route.Length; i++)
                Length += Vector3.Distance(route[i], route[(i + 1) % route.Length]);
        }

        void Start()
        {
            FixStreetLights();
            FixTerrainGaps();
        }

        public Vector3 Point(int index) => route[(index % route.Length + route.Length) % route.Length];

        public int Closest(Vector3 position)
        {
            int result = 0;
            float distance = float.MaxValue;
            for (int i = 0; i < route.Length; i++)
            {
                float next = (position - route[i]).sqrMagnitude;
                if (next < distance) { distance = next; result = i; }
            }
            return result;
        }

        public Vector3 ClosestPointOnRoad(Vector3 position)
        {
            if (route == null || route.Length == 0) return position;
            int nearestIdx = Closest(position);
            Vector3 bestPoint = route[nearestIdx];
            float bestDistSq = (position - bestPoint).sqrMagnitude;

            for (int offset = -2; offset <= 2; offset++)
            {
                int i = (nearestIdx + offset + route.Length) % route.Length;
                int next = (i + 1) % route.Length;
                Vector3 a = route[i];
                Vector3 b = route[next];
                Vector3 ab = b - a;
                float lenSq = ab.sqrMagnitude;
                if (lenSq < 0.001f) continue;
                float t = Mathf.Clamp01(Vector3.Dot(position - a, ab) / lenSq);
                Vector3 proj = a + t * ab;
                float dSq = (position - proj).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestPoint = proj;
                }
            }
            return bestPoint;
        }

        public void FixStreetLights()
        {
            if (route == null || route.Length == 0) return;

            // Orient all street light arms to point inward toward the road
            var lights = GetComponentsInChildren<Light>(true);
            foreach (var light in lights)
            {
                if (light.type != LightType.Spot) continue;
                Transform pole = light.transform.parent;
                if (!pole) continue;

                Vector3 roadPt = ClosestPointOnRoad(pole.position);
                Vector3 toRoad = roadPt - pole.position;
                toRoad.y = 0;
                if (toRoad.sqrMagnitude > 0.05f)
                {
                    pole.rotation = Quaternion.LookRotation(toRoad.normalized);
                }
            }
        }

        public void FixTerrainGaps()
        {
            // 1. Fix houses & buildings floating above terrain by adding deep foundation plinths
            var foundationMat = new Material(VehicleModelUpgrade.LitShader) { name = "BuildingFoundationMat" };
            foundationMat.SetColor("_BaseColor", new Color(0.22f, 0.21f, 0.20f, 1f));
            foundationMat.SetFloat("_Smoothness", 0.15f);
            foundationMat.SetFloat("_Metallic", 0.05f);

            var colliders = GetComponentsInChildren<BoxCollider>(true);
            foreach (var bc in colliders)
            {
                string pName = bc.transform.parent != null ? bc.transform.parent.name : "";
                bool isBuilding = (bc.size.x >= 6f && bc.size.z >= 6f && bc.size.y >= 3.5f) ||
                                  pName.Contains("VillageHouse") || pName.StartsWith("@StaticBody3D@28") || pName.StartsWith("@StaticBody3D@29");

                if (bc.size.x > 50f || bc.size.z > 50f) isBuilding = false;

                if (isBuilding && bc.transform.Find("BuildingFoundation") == null)
                {
                    var foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    foundation.name = "BuildingFoundation";
                    foundation.transform.SetParent(bc.transform, false);

                    float bottomY = bc.center.y - bc.size.y * 0.5f;
                    float foundationDepth = 5.0f; // 5 meters down into terrain
                    foundation.transform.localPosition = new Vector3(bc.center.x, bottomY - foundationDepth * 0.5f + 0.1f, bc.center.z);
                    foundation.transform.localRotation = Quaternion.identity;
                    foundation.transform.localScale = new Vector3(bc.size.x + 0.8f, foundationDepth, bc.size.z + 0.8f);

                    var c = foundation.GetComponent<Collider>();
                    if (c) Destroy(c);

                    var r = foundation.GetComponent<Renderer>();
                    if (r) r.sharedMaterial = foundationMat;
                }
            }

            // 2. Fix bridge pillars floating above water/seabed/ground
            Material bridgeMat = null;
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                string n = r.name;
                bool isPillar = n.Contains("1299") || n.Contains("1326") || n.Contains("1357") ||
                                n.Contains("1384") || n.Contains("1415") || n.Contains("1442") ||
                                n.Contains("1473") || n.Contains("1500") || n.Contains("1531") ||
                                n.Contains("1558") || n.Contains("1589") || n.Contains("1616") ||
                                n.Contains("1647");

                if (isPillar && r.transform.Find("PillarExtension") == null)
                {
                    if (bridgeMat == null && r.sharedMaterial) bridgeMat = r.sharedMaterial;

                    var ext = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    ext.name = "PillarExtension";
                    ext.transform.SetParent(r.transform, false);
                    ext.transform.localPosition = new Vector3(0, -3.5f, 0);
                    ext.transform.localRotation = Quaternion.identity;
                    ext.transform.localScale = new Vector3(1.05f, 7.0f, 1.05f);

                    var c = ext.GetComponent<Collider>();
                    if (c) Destroy(c);

                    var er = ext.GetComponent<Renderer>();
                    if (er) er.sharedMaterial = r.sharedMaterial ?? bridgeMat;
                }
            }

            // 3. Fix bridge abutments where elevated road connects to the land
            if (bridgeMat == null)
            {
                bridgeMat = new Material(VehicleModelUpgrade.LitShader) { name = "BridgeAbutmentMat" };
                bridgeMat.SetColor("_BaseColor", new Color(0.75f, 0.77f, 0.80f, 1f));
                bridgeMat.SetFloat("_Metallic", 0.5f);
                bridgeMat.SetFloat("_Smoothness", 0.4f);
            }

            if (route != null && route.Length > 0 && transform.Find("BridgeAbutments") == null)
            {
                var abutmentRoot = new GameObject("BridgeAbutments");
                abutmentRoot.transform.SetParent(transform, false);

                for (int i = 0; i < route.Length; i++)
                {
                    Vector3 p = Point(i);
                    // Ramping sections between 0.35m and 5.0m height
                    if (p.y > 0.35f && p.y < 5.0f)
                    {
                        Vector3 next = Point(i + 1);
                        Vector3 seg = next - p;
                        float segLen = seg.magnitude;
                        if (segLen < 0.1f) continue;

                        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        block.name = "Abutment_" + i;
                        block.transform.SetParent(abutmentRoot.transform, false);

                        float h = p.y + 2.0f; // From road down into ground
                        Vector3 centerPos = (p + next) * 0.5f;
                        centerPos.y = (p.y - 2.0f) * 0.5f; // Centered between road underside and -2.0m deep
                        block.transform.position = centerPos;
                        block.transform.rotation = Quaternion.LookRotation(seg.normalized);
                        block.transform.localScale = new Vector3(roadHalfWidth * 2f + 0.6f, h, segLen + 0.3f);

                        var c = block.GetComponent<Collider>();
                        if (c) Destroy(c);

                        var br = block.GetComponent<Renderer>();
                        if (br) br.sharedMaterial = bridgeMat;
                    }
                }
            }
        }
    }
}
