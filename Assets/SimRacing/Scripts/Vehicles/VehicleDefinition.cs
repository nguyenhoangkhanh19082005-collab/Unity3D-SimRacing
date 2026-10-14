using UnityEngine;
namespace SimRacing
{
    public sealed class VehicleDefinition : MonoBehaviour
    {
        public string id;
        public float mass, torque, idleRPM, redlineRPM, finalDrive, tireRadius;
        public float[] ratios;
        public WheelCollider[] wheels;
        public Transform[] wheelVisuals;
        public bool[] drivenWheels;
        public Transform cockpit, exterior, steeringWheel, speedNeedle, rpmNeedle;
        public TextMesh digitalSpeed, digitalGear;
        public Renderer mirror;
    }
}
