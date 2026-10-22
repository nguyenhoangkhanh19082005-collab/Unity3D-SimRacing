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

        Vector3 GetGroundPosition(Vector3 queryPos, out Vector3 groundNormal)
        {
            groundNormal = Vector3.up;
            RaycastHit[] hits = Physics.RaycastAll(queryPos + Vector3.up * 4.5f, Vector3.down, 10.0f);
            float bestY = -9999f;
            Vector3 bestPoint = queryPos;
            Vector3 bestNormal = Vector3.up;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.rigidbody != null) continue;
                if (hit.normal.y < 0.6f) continue;

                string cName = hit.collider.gameObject.name;
                if (cName.Contains("Abyss") || cName.Contains("Safety")) continue;

                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    bestPoint = hit.point;
                    bestNormal = hit.normal;
                }
            }

            if (bestY > -9000f)
            {
                groundNormal = bestNormal;
                return bestPoint;
            }
            return queryPos;
        }

        Vector3 Lane(int i)
        {
            Vector3 tangent = (track.Point(i + direction) - track.Point(i - direction)).normalized;
            Vector3 raw = track.Point(i) + Vector3.Cross(Vector3.up, tangent).normalized * 2.8f;
            return GetGroundPosition(raw, out _);
        }

        public void Initialize()
        {
            body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.mass = 1400; body.interpolation = RigidbodyInterpolation.Interpolate;
            Vector3 startPos = Lane(index);
            Vector3 groundPt = GetGroundPosition(startPos, out Vector3 groundNormal);
            Quaternion targetRot = Heading(Lane(index + direction) - startPos, groundNormal);
            transform.SetPositionAndRotation(groundPt, targetRot); speed = maxSpeed * .7f;
            foreach (var light in GetComponentsInChildren<Light>()) light.enabled = false;
        }

        void FixedUpdate()
        {
            if (!body || wrecked) return;
            crashCooldown = Mathf.Max(0, crashCooldown - Time.fixedDeltaTime);
            Vector3 target = Lane(index + direction);
            float remaining = Vector3.Distance(body.position, target);
            if (remaining < Mathf.Max(1.5f, speed * Time.fixedDeltaTime * 1.5f))
            {
                index = (index + direction + track.route.Length) % track.route.Length;
                target = Lane(index + direction);
            }

            // Steer on the horizontal plane only: a height difference between the body and the next lane point
            // (banking, kerbs) must never pitch the car nose-up.
            Vector3 desired = target - body.position;
            desired.y = 0;
            desired.Normalize();
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
                    var newPos = Lane(index);
                    Vector3 groundPt = GetGroundPosition(newPos, out Vector3 groundNormal);
                    Quaternion targetRot = Heading(Lane(index + direction) - newPos, groundNormal);
                    transform.SetPositionAndRotation(groundPt, targetRot);
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

            Vector3 nextPos = body.position + desired * speed * Time.fixedDeltaTime;
            Vector3 groundPos = GetGroundPosition(nextPos, out Vector3 groundNorm);
            nextPos.y = groundPos.y;

            body.MovePosition(nextPos);
            if (desired.sqrMagnitude > .01f)
            {
                Quaternion targetRot = Heading(desired, groundNorm);
                body.MoveRotation(Quaternion.Slerp(body.rotation, targetRot, 1 - Mathf.Exp(-8 * Time.fixedDeltaTime)));
            }
        }

        static Quaternion Heading(Vector3 direction, Vector3 groundNormal)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < 0.0001f) return Quaternion.identity;
            if (groundNormal.y < 0.6f) groundNormal = Vector3.up;
            return Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, groundNormal).normalized, groundNormal);
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
