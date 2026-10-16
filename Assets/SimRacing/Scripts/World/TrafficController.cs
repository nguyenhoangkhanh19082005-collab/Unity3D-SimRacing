using System.Collections.Generic;
using UnityEngine;
namespace SimRacing
{
    public sealed class TrafficController : MonoBehaviour
    {
        public readonly List<TrafficAgent> Cars=new List<TrafficAgent>();
        public void Setup(TrackDefinition track,VehicleController player,DriveSession session,int count)
        {
            var prefab=Resources.Load<GameObject>("Vehicles/traffic_vehicle");
            if(!prefab){Debug.LogError("Traffic prefab missing");return;}

            var merc=Resources.Load<GameObject>("Vehicles/mercedes_w210")?.GetComponent<VehicleDefinition>();
            var ferr=Resources.Load<GameObject>("Vehicles/ferrari_f40")?.GetComponent<VehicleDefinition>();
            var buga=Resources.Load<GameObject>("Vehicles/bugatti_centodieci")?.GetComponent<VehicleDefinition>();
            var carDefs=new[]{merc, ferr, buga};

            for(int i=0;i<count;i++) {
                var go=Instantiate(prefab,transform);go.name="Traffic "+i;
                // Disable incomplete wheel-less proxy renderers
                foreach(var r in go.GetComponentsInChildren<Renderer>())r.enabled=false;

                // Create full vehicle visual appearance with body and all 4 wheels
                var def = carDefs[i % carDefs.Length] ?? carDefs[0];
                if (def != null)
                {
                    var detail = VehicleVisual.Create(def, go.transform);
                    foreach(var r in detail.GetComponentsInChildren<Renderer>())r.enabled=true;
                }

                var agent=go.AddComponent<TrafficAgent>();agent.track=track;agent.player=player;agent.session=session;
                agent.index=(10+i*Mathf.Max(1,(track.route.Length-20)/Mathf.Max(1,count)))%track.route.Length;agent.direction=i%2==0?1:-1;agent.maxSpeed=Mathf.Min(60,session.Limit)/3.6f;
                agent.Initialize();Cars.Add(agent);
            }
        }
    }
    public sealed class TrafficAgent : MonoBehaviour
    {
        public TrackDefinition track; public VehicleController player; public DriveSession session;
        public int index, direction = 1; public float maxSpeed = 16, speed; public bool wrecked;
        Rigidbody body; float crashCooldown; float stuckTimer;
        static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        Vector3 Lane(int i)
        {
            Vector3 tangent = (track.Point(i + direction) - track.Point(i - direction)).normalized;
            return track.Point(i) + Vector3.Cross(Vector3.up, tangent).normalized * 2.8f;
        }

        public void Initialize()
        {
            body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.mass = 1400; body.interpolation = RigidbodyInterpolation.Interpolate;
            var tangent = (Lane(index + direction) - Lane(index)).normalized;
            transform.SetPositionAndRotation(Lane(index) + Vector3.up * .15f, Quaternion.LookRotation(tangent)); speed = maxSpeed * .7f;
            foreach (var light in GetComponentsInChildren<Light>()) light.enabled = false;
        }

        void FixedUpdate()
        {
            if (!body || wrecked) return;
            crashCooldown = Mathf.Max(0, crashCooldown - Time.fixedDeltaTime);
            Vector3 target = Lane(index + direction) + Vector3.up * .15f;
            float remaining = Vector3.Distance(body.position, target);
            if (remaining < Mathf.Max(1.5f, speed * Time.fixedDeltaTime * 1.5f))
            {
                index = (index + direction + track.route.Length) % track.route.Length;
                target = Lane(index + direction) + Vector3.up * .15f;
            }

            Vector3 desired = (target - body.position).normalized;
            float allowed = maxSpeed;
            float sense = Mathf.Clamp(6f + speed * 1.8f, 6f, 30f);

            int hitCount = Physics.BoxCastNonAlloc(
                body.position + Vector3.up * .65f,
                new Vector3(.65f, .45f, .4f),
                transform.forward,
                hitBuffer,
                body.rotation,
                sense,
                ~(1 << 8),
                QueryTriggerInteraction.Ignore
            );

            for (int h = 0; h < hitCount; h++)
            {
                var hit = hitBuffer[h];
                if (hit.transform == null || hit.transform.IsChildOf(transform)) continue;
                if (!hit.rigidbody) continue;

                // Check if hit object is another traffic agent
                if (hit.rigidbody.TryGetComponent<TrafficAgent>(out var otherAgent))
                {
                    // Never stop for oncoming traffic on the other lane!
                    if (otherAgent.direction != direction) continue;
                    // If car is driving in the opposite heading, ignore it
                    if (Vector3.Dot(transform.forward, otherAgent.transform.forward) < -0.2f) continue;
                }
                else if (hit.rigidbody.TryGetComponent<VehicleController>(out var playerCar))
                {
                    // Never stop for player driving in the opposite heading / oncoming lane
                    if (Vector3.Dot(transform.forward, playerCar.transform.forward) < -0.2f) continue;
                }

                // Check forward and lateral distance along car's local axes
                Vector3 toHit = hit.collider != null ? hit.collider.bounds.center - body.position : hit.transform.position - body.position;
                float fwdDist = Vector3.Dot(toHit, transform.forward);
                float latDist = Mathf.Abs(Vector3.Dot(toHit, transform.right));

                if (fwdDist <= 0.5f) continue; // Behind or right beside us
                if (latDist > 1.8f) continue;  // In adjacent lane or roadside

                // Safe bumper-to-bumper following distance (approx 5.2m center-to-center)
                float gap = Mathf.Max(0f, fwdDist - 5.2f);
                allowed = Mathf.Min(allowed, gap * 1.5f);
            }

            // Anti-deadlock / Unstuck logic:
            if (speed < 0.6f && crashCooldown <= 0)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > 2.5f && stuckTimer <= 5.0f)
                {
                    // If briefly delayed, gently nudge allowed speed forward
                    allowed = Mathf.Max(allowed, maxSpeed * 0.35f);
                }
                else if (stuckTimer > 5.0f)
                {
                    // Deadlock detected: advance past congestion to clear route point
                    index = (index + direction * 4 + track.route.Length) % track.route.Length;
                    var newTangent = (Lane(index + direction) - Lane(index)).normalized;
                    transform.SetPositionAndRotation(Lane(index) + Vector3.up * .15f, Quaternion.LookRotation(newTangent));
                    speed = maxSpeed * 0.7f;
                    stuckTimer = 0;
                }
            }
            else
            {
                stuckTimer = 0;
            }

            if (crashCooldown > 0 && crashCooldown < 1.8f && allowed > 3f && speed < 3f)
            {
                speed = Mathf.MoveTowards(speed, maxSpeed * 0.45f, 3.5f * Time.fixedDeltaTime);
            }
            else
            {
                speed = Mathf.MoveTowards(speed, allowed, (speed > allowed ? 5.5f : 2.0f) * Time.fixedDeltaTime);
            }

            body.MovePosition(body.position + desired * speed * Time.fixedDeltaTime);
            if (desired.sqrMagnitude > .01f)
                body.MoveRotation(Quaternion.Slerp(body.rotation, Quaternion.LookRotation(desired), 1 - Mathf.Exp(-8 * Time.fixedDeltaTime)));
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.rigidbody != player.Body || crashCooldown > 0) return;
            crashCooldown = 2.5f;
            if (collision.relativeVelocity.magnitude > 3)
            {
                session.Violation("Va chạm giao thông • Giữ khoảng cách an toàn");
                speed = 0;
            }
            if (collision.relativeVelocity.magnitude > 22) wrecked = true;
        }
    }
}
