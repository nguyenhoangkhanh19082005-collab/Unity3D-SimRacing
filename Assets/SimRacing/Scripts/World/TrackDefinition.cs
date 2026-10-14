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
        void Awake() { Length = 0; for (int i=0;i<route.Length;i++) Length += Vector3.Distance(route[i],route[(i+1)%route.Length]); }
        public Vector3 Point(int index) => route[(index % route.Length + route.Length) % route.Length];
        public int Closest(Vector3 position)
        {
            int result=0; float distance=float.MaxValue;
            for(int i=0;i<route.Length;i++) { float next=(position-route[i]).sqrMagnitude; if(next<distance){distance=next;result=i;} }
            return result;
        }
    }
}
