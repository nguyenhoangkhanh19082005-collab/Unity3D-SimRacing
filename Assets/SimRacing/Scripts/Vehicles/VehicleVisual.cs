using UnityEngine;
namespace SimRacing
{
    public static class VehicleVisual
    {
        public static GameObject Create(VehicleDefinition definition,Transform parent)
        {
            var root=new GameObject("Vehicle appearance");root.transform.SetParent(parent,false);
            var body=Object.Instantiate(definition.exterior.gameObject,root.transform);body.SetActive(true);
            body.transform.localPosition=definition.exterior.localPosition;body.transform.localRotation=definition.exterior.localRotation;
            for(int i=0;i<definition.wheelVisuals.Length;i++) {
                var wheel=Object.Instantiate(definition.wheelVisuals[i].gameObject,root.transform);wheel.SetActive(true);
                wheel.transform.localPosition=definition.wheels[i].transform.localPosition;wheel.transform.localRotation=definition.wheelVisuals[i].localRotation;
            }
            return root;
        }
    }
}
