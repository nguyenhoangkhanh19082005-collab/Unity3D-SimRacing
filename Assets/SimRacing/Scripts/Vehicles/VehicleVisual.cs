using UnityEngine;

namespace SimRacing
{
    public static class VehicleVisual
    {
        public static GameObject Create(VehicleDefinition definition, Transform parent)
        {
            if (definition != null && VehicleModelUpgrade.HasImportedModel(definition.id))
            {
                var visual = VehicleModelUpgrade.LoadVehicleModel(definition.id, parent);
                if (visual != null)
                {
                    visual.name = "Vehicle appearance (" + definition.id + ")";
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one;

                    // Disable interior/cockpit submeshes to optimize traffic rendering while preserving full exterior (body, glass, wheels, rims, lights)
                    foreach (Transform child in visual.transform)
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
                                         n == "meshpart2" || n == "meshpart31";

                        if (isCockpit)
                        {
                            child.gameObject.SetActive(false);
                            if (mr) mr.enabled = false;
                        }
                        else
                        {
                            child.gameObject.SetActive(true);
                            if (mr) mr.enabled = true;
                        }
                    }

                    // Ensure all exterior renderers (body, glass, wheels, rims, lights) are fully enabled
                    foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.gameObject.activeSelf) r.enabled = true;
                    }

                    return visual;
                }
            }

            var root = new GameObject("Vehicle appearance");
            root.transform.SetParent(parent, false);
            if (definition != null)
            {
                if (definition.exterior != null)
                {
                    var body = Object.Instantiate(definition.exterior.gameObject, root.transform);
                    body.SetActive(true);
                    body.transform.localPosition = definition.exterior.localPosition;
                    body.transform.localRotation = definition.exterior.localRotation;
                    body.transform.localScale = definition.exterior.localScale;
                }
                if (definition.wheelVisuals != null && definition.wheels != null)
                {
                    for (int i = 0; i < definition.wheelVisuals.Length && i < definition.wheels.Length; i++)
                    {
                        if (definition.wheelVisuals[i] == null) continue;
                        var wheel = Object.Instantiate(definition.wheelVisuals[i].gameObject, root.transform);
                        wheel.SetActive(true);
                        wheel.transform.localPosition = definition.wheels[i].transform.localPosition;
                        wheel.transform.localRotation = definition.wheelVisuals[i].localRotation;
                        wheel.transform.localScale = definition.wheelVisuals[i].localScale;
                    }
                }
            }
            return root;
        }
    }
}
