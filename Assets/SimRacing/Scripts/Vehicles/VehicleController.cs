using System;
using System.Linq;
using UnityEngine;

namespace SimRacing
{
    [RequireComponent(typeof(VehicleDefinition),typeof(Rigidbody))]
    public sealed class VehicleController : MonoBehaviour
    {
        public VehicleDefinition definition;
        public Rigidbody Body { get; private set; }
        public RigConnection rig;
        public TrackDefinition track;
        public float Speed { get; private set; }
        public float RPM { get; private set; }
        public float Fuel = 60, Health = 100, Grip = 1, TopSpeed;
        public int Gear { get; private set; }
        public bool EngineRunning { get; private set; }
        public bool Locked, Refueling, Offroad, Grounded;
        public bool Wrecked => Health <= 0;
        public Vector3 Acceleration { get; private set; }
        public float Steering { get; private set; }
        public int Recoveries { get; private set; }
        public Action OnRecovery;
        Vector3 previousVelocity;
        Quaternion steeringRest;
        Quaternion[] wheelRest;
        float age, feedbackClock, recoveryClock, blinkClock, smoothKeyboardSteer, spaceBrakeTime;
        bool refuelRequested;
        Light[] headlamps;
        Transform[] wipers;
        Quaternion[] wiperRest;
        TextMesh[] statusLabels;
        public bool Blink => blinkClock % 1 < .5f;
        public RigSample Controls => rig ? rig.Controls : RigSample.Held;
        public void ResetSteeringRest() { if (definition.steeringWheel) steeringRest = definition.steeringWheel.localRotation; }
        void Awake()
        {
            definition=GetComponent<VehicleDefinition>(); Body=GetComponent<Rigidbody>();
            Body.mass=definition.mass;Body.centerOfMass=new Vector3(0,-.12f,0);
            Body.interpolation=RigidbodyInterpolation.Interpolate;Body.maxAngularVelocity=4;
            Body.linearDamping=.025f;Body.angularDamping=.5f;
            wheelRest=definition.wheelVisuals.Select(t=>t.localRotation).ToArray();
            if(definition.steeringWheel)steeringRest=definition.steeringWheel.localRotation;
            headlamps=GetComponentsInChildren<Light>(true).Where(x=>x.type==LightType.Spot && x.transform.localPosition.z>0).ToArray();
            wipers=GetComponentsInChildren<Transform>(true).Where(x=>x.name=="WiperLeft"||x.name=="WiperRight").ToArray();
            wiperRest=wipers.Select(x=>x.localRotation).ToArray();statusLabels=GetComponentsInChildren<TextMesh>(true);
            foreach(var wheel in definition.wheels) {
                // The imported node is a wheel centre; Unity starts its suspension ray here.
                wheel.center=new Vector3(0,.11f,0);
                var f=wheel.forwardFriction;f.extremumSlip=.35f;f.extremumValue=1.15f;f.asymptoteSlip=.85f;f.asymptoteValue=.8f;wheel.forwardFriction=f;
                f=wheel.sidewaysFriction;f.extremumSlip=.22f;f.extremumValue=1.15f;f.asymptoteSlip=.7f;f.asymptoteValue=.85f;wheel.sidewaysFriction=f;
            }
            gameObject.AddComponent<VehicleSound>();
            gameObject.AddComponent<VehicleEffects>();
        }
        void FixedUpdate()
        {
            age+=Time.fixedDeltaTime;blinkClock+=Time.fixedDeltaTime;
            var input=Controls;float dt=Time.fixedDeltaTime;
            float forwardSpeed=Vector3.Dot(Body.linearVelocity,transform.forward);
            Speed=Mathf.Abs(forwardSpeed)*3.6f;TopSpeed=Mathf.Max(TopSpeed,Speed);
            Vector3 rawAcceleration=transform.InverseTransformDirection((Body.linearVelocity-previousVelocity)/dt);
            Acceleration=Vector3.Lerp(Acceleration,Vector3.ClampMagnitude(rawAcceleration,18),1-Mathf.Exp(-8*dt));
            previousVelocity=Body.linearVelocity;
            bool isKeyboardMode = (!rig || !rig.Connected);
            bool holding = Locked || (!isKeyboardMode && (Wrecked || rig.Held || !rig.Connected));
            if (isKeyboardMode)
            {
                if (Locked)
                {
                    input = RigSample.Held;
                }
                else
                {
                    bool wantForward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
                    bool wantReverse = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
                    bool wantSpace = Input.GetKey(KeyCode.Space);

                    float gas = 0f;
                    float brk = 0f;
                    int g = 1;

                    if (wantSpace)
                    {
                        spaceBrakeTime += dt;
                        // Tapping Space: initial moderate brake (~0.45f) to reduce speed slightly
                        // Holding Space: progressively ramps up to 1.0f full brake over ~1.2 seconds
                        float spaceBrake = Mathf.Clamp(0.45f + spaceBrakeTime * 0.75f, 0f, 1f);
                        brk = spaceBrake;
                        gas = 0f;

                        // When speed drops below 0.8 km/h and Space is held, bring vehicle to a clean full stop at 0 km/h
                        if (Speed < 0.8f && spaceBrakeTime > 0.4f)
                        {
                            Body.linearVelocity = Vector3.zero;
                            Body.angularVelocity = Vector3.zero;
                            brk = 1f;
                        }
                        g = (forwardSpeed < -0.5f) ? 6 : (Speed < 45f ? 1 : 2);
                    }
                    else
                    {
                        spaceBrakeTime = 0f;

                        if (wantForward && !wantReverse)
                        {
                            if (forwardSpeed < -0.8f)
                            {
                                // Car is rolling backward: apply brakes to stop backward motion first!
                                brk = 1f;
                                gas = 0f;
                                g = 1;
                            }
                            else
                            {
                                gas = 1f;
                                brk = 0f;
                                // Automatic gear selection (1 to 5)
                                if (Speed < 45f) g = 1;
                                else if (Speed < 85f) g = 2;
                                else if (Speed < 135f) g = 3;
                                else if (Speed < 190f) g = 4;
                                else g = 5;
                                if (definition.ratios != null && g >= definition.ratios.Length)
                                    g = Mathf.Max(1, definition.ratios.Length - 1);
                            }
                        }
                        else if (wantReverse && !wantForward)
                        {
                            if (forwardSpeed > 0.8f)
                            {
                                // Car is moving forward: apply brakes to stop forward motion first!
                                brk = 1f;
                                gas = 0f;
                                g = (Speed < 45f ? 1 : 2);
                            }
                            else
                            {
                                // Car stopped or reversing: engage Reverse gear
                                g = 6;
                                gas = 0.85f;
                                brk = 0f;
                            }
                        }
                        else if (wantForward && wantReverse)
                        {
                            brk = 1f;
                            gas = 0f;
                            g = 1;
                        }
                        else
                        {
                            // Coasting
                            gas = 0f;
                            brk = 0f;
                            g = (forwardSpeed < -0.5f) ? 6 : (Speed < 45f ? 1 : 2);
                        }
                    }

                    // Smooth speed-sensitive keyboard steering input
                    float targetStr = 0f;
                    if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) targetStr -= 1f;
                    if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) targetStr += 1f;

                    float steerRate = (targetStr != 0f) ? (Speed > 100f ? 2.8f : 3.8f) : 6.5f;
                    smoothKeyboardSteer = Mathf.MoveTowards(smoothKeyboardSteer, targetStr, steerRate * dt);

                    input = new RigSample
                    {
                        ignition = true,
                        starter = true,
                        throttle = gas,
                        brake = brk,
                        steer = smoothKeyboardSteer,
                        clutch = 0f,
                        gear = g
                    };
                    EngineRunning = true;
                }
            }
            else if (holding) input = RigSample.Held;
            Gear = input.gear;
            if (!isKeyboardMode && (!input.ignition || Fuel <= 0 || Wrecked || holding)) EngineRunning = false;
            else if (input.starter) EngineRunning = true;
            float ratio=definition.ratios[Mathf.Clamp(Gear,0,definition.ratios.Length-1)];
            float engagement=1-input.clutch;
            float shaft=Mathf.Abs(forwardSpeed)/(2*Mathf.PI*definition.tireRadius)*60*Mathf.Abs(ratio)*definition.finalDrive;
            float targetRPM=Gear==0 || engagement<.15f ? Mathf.Lerp(definition.idleRPM,definition.redlineRPM,input.throttle) : Mathf.Max(definition.idleRPM,shaft);
            RPM=Mathf.MoveTowards(RPM,EngineRunning?targetRPM:0,(EngineRunning?6500:1500)*dt);
            RPM=Mathf.Min(RPM,definition.redlineRPM);
            if(EngineRunning && !Refueling)Fuel=Mathf.Max(0,Fuel-(.015f+RPM/definition.redlineRPM*.045f*(.3f+input.throttle*.7f))*dt);
            // Braking and a speed-sensitive steering rack are independent of the render frame rate.
            Steering=Mathf.MoveTowards(Steering,input.steer*Mathf.Lerp(32f,13f,Mathf.Clamp01(Speed/200f)),(Speed>70?110:180)*dt);
            int driven=Mathf.Max(1,definition.drivenWheels.Count(x=>x));Grounded=false;
            if(track) { int nearest=track.Closest(transform.position);Offroad=Vector3.Distance(transform.position,track.Point(nearest))>track.roadHalfWidth+1; }
            float grip=Grip*(Offroad?.65f:1);
            float curve=.6f+.4f*Mathf.Sin(Mathf.Clamp((RPM-1000)/7000*Mathf.PI,0,Mathf.PI));
            float motor=EngineRunning&&!holding ? definition.torque*curve*ratio*definition.finalDrive*engagement*input.throttle*Mathf.Clamp(Health/100+.3f,.3f,1)/driven : 0;
            // Prevent selecting a direction against appreciable existing motion.
            if(Mathf.Abs(forwardSpeed)>2 && Mathf.Sign(motor)!=Mathf.Sign(forwardSpeed))motor=0;
            for(int i=0;i<definition.wheels.Length;i++) {
                var wheel=definition.wheels[i];
                wheel.steerAngle=wheel.transform.localPosition.z>0?Steering:0;
                wheel.motorTorque=definition.drivenWheels[i]?motor:0;
                wheel.brakeTorque=(holding?1:input.brake)*definition.mass*1.65f;
                if(EngineRunning && Gear!=0 && input.throttle<.02f)wheel.brakeTorque+=engagement*40;
                var f=wheel.forwardFriction;f.stiffness=grip;wheel.forwardFriction=f;
                f=wheel.sidewaysFriction;f.stiffness=grip;wheel.sidewaysFriction=f;
                Grounded |= wheel.isGrounded;
            }
            if(Grounded && transform.up.y>.5f)Body.AddForce(-transform.up*forwardSpeed*forwardSpeed*1.65f);
            Body.AddForce(-Body.linearVelocity*Body.linearVelocity.magnitude*.38f);
            if (isKeyboardMode && Grounded)
            {
                float latVel = Vector3.Dot(Body.linearVelocity, transform.right);
                // Counter-lateral sliding force on asphalt: gives planted grip when cornering
                Body.AddForce(-transform.right * latVel * Body.mass * 0.90f * (Offroad ? 0.35f : 1.0f));

                // Stabilizing yaw damping: prevents sudden snap oversteer / spin-out when holding A/D through curves
                float yaw = Body.angularVelocity.y;
                float intendedYaw = (Steering * Mathf.Deg2Rad) * (forwardSpeed / 2.8f);
                float yawDelta = yaw - intendedYaw;
                Body.AddTorque(transform.up * (-yawDelta * Body.mass * 1.25f));
            }
            if(holding && Speed<.35f && Grounded) {Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}
            if(transform.position.y < -30)Recover(true);
            feedbackClock+=dt;
            if(rig && feedbackClock>=1f/50) {
                feedbackClock=0;
                float lateral=-Vector3.Dot(Body.linearVelocity,transform.right);
                float rumble=Offroad && Speed>10 ? Mathf.Sin(age*40)*160 : 0;
                rig.SendFeedback(Mathf.Clamp(lateral*45-input.steer*200+rumble,-1000,1000),Speed,RPM,Gear,Fuel/60*100,EngineRunning,Blink,Mathf.Abs(rumble));
            }
        }
        void Update()
        {
            var input=Controls;
            for(int i=0;i<definition.wheels.Length;i++) {
                definition.wheels[i].GetWorldPose(out Vector3 p,out Quaternion q);
                definition.wheelVisuals[i].SetPositionAndRotation(p,q*wheelRest[i]);
            }
            if(definition.steeringWheel)definition.steeringWheel.localRotation=steeringRest*Quaternion.Euler(0,0,-Steering*4.2f);
            if(definition.speedNeedle)definition.speedNeedle.localRotation=Quaternion.Euler(0,0,-130+Mathf.Clamp01(Speed/260)*260);
            if(definition.rpmNeedle)definition.rpmNeedle.localRotation=Quaternion.Euler(0,0,-130+Mathf.Clamp01(RPM/8000)*260);
            if(definition.digitalSpeed)definition.digitalSpeed.text=Mathf.FloorToInt(Speed).ToString();
            if(definition.digitalGear)definition.digitalGear.text=Gear==0?"N":Gear==6?"R":Gear.ToString();
            foreach(var lamp in headlamps) {lamp.gameObject.SetActive(input.headlights>0);lamp.enabled=input.headlights>0;lamp.intensity=input.headlights==2?500:250;lamp.range=input.headlights==2?100:65;lamp.spotAngle=input.headlights==2?38:55;}
            for(int i=0;i<wipers.Length;i++)wipers[i].localRotation=wiperRest[i]*Quaternion.Euler(0,0,input.wipers>0?Mathf.Sin(age*(input.wipers==2?7:4))*50:0);
            foreach(var label in statusLabels) {
                if(label.name=="FuelDisplay")label.text=$"FUEL  {Fuel:0} L";
                if(label.name=="EngineStatus")label.text=EngineRunning?"ENGINE RUNNING":"IGNITION OFF";
            }
            if(Input.GetKeyDown(KeyCode.Backspace))Recover(false);
            bool nearStation = track && Vector3.Distance(transform.position, track.fuelStation) < 12f;
            bool stopped = Speed < 0.5f;
            if (nearStation && stopped)
            {
                if (Input.GetKeyDown(KeyCode.R)) refuelRequested = !refuelRequested;
                bool testMode = FindFirstObjectByType<SimulationApp>()?.TestMode ?? false;
                if (Input.GetKey(KeyCode.R) || refuelRequested || testMode)
                {
                    Refueling = true;
                }
                else
                {
                    Refueling = false;
                }
            }
            else
            {
                Refueling = false;
                refuelRequested = false;
            }
            if(Refueling)Fuel=Mathf.Min(60,Fuel+8*Time.deltaTime);
        }
        public bool Recover(bool emergency)
        {
            if(!track || Time.unscaledTime-recoveryClock<2 || (!emergency && Speed>1))return false;
            int i=track.Closest(transform.position);Vector3 direction=(track.Point(i+1)-track.Point(i)).normalized;
            Body.position=track.Point(i)+Vector3.Cross(Vector3.up,direction)*2.8f+Vector3.up*.45f;
            Body.rotation=Quaternion.LookRotation(direction);Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;
            previousVelocity=Vector3.zero;Acceleration=Vector3.zero;Health=100;Fuel=Mathf.Max(Fuel,10);EngineRunning=false;
            Recoveries++;recoveryClock=Time.unscaledTime;age=0;rig?.Hold();OnRecovery?.Invoke();return true;
        }
        void OnCollisionEnter(Collision contact)
        {
            float damage=0;
            bool isTraffic = contact.gameObject.GetComponentInParent<TrafficAgent>() != null;
            foreach(var point in contact.contacts) {
                bool road=point.normal.y>.6f; if(road && age<3)continue;
                float closing=Mathf.Abs(Vector3.Dot(contact.relativeVelocity,point.normal));
                float threshold=road?(transform.up.y>.5f?6:3.5f):1.5f;
                float amount=Mathf.Pow(Mathf.Max(0,closing-threshold),2)*(road?.65f:.18f);
                if(contact.rigidbody && contact.rigidbody.mass<Body.mass)amount*=Mathf.Clamp(contact.rigidbody.mass/(Body.mass+contact.rigidbody.mass),.03f,1);
                if(isTraffic)amount*=0.10f;
                damage=Mathf.Max(damage,amount);
            }
            damage=Mathf.Min(damage,25f);
            Health=Mathf.Max(0,Health-damage);
        }
        public void SetCockpit(bool active)
        {
            if(definition.cockpit)definition.cockpit.gameObject.SetActive(active);
            if(definition.exterior)definition.exterior.gameObject.SetActive(true);
            foreach(var visual in definition.wheelVisuals)visual.gameObject.SetActive(!active);
        }
    }
}
