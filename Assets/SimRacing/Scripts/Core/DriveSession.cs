using System;
using UnityEngine;
namespace SimRacing
{
    public sealed class DriveSession : MonoBehaviour
    {
        public VehicleController car;public TrackDefinition track;public Challenge challenge;
        public bool FreeDrive,RoadRules,Active,Completed,Finished;
        public int TargetLaps=3,Laps,Violations,HarshBrakes;
        public float Elapsed,LapTime,BestLap=9999,Distance,RouteProgress,Overspeed;
        public bool ManualLaunch;public string Notice="";
        public Action<Result> OnFinished;
        public float Limit => challenge?.speed_limit??80;
        public float Remaining => Mathf.Max(0,(challenge?.distance_m??0)-RouteProgress);
        int nextGate=2,segment;float segmentDistance,startProgress,stopTime,brakeDuration,brakeCooldown,previousSpeed,speedingTime,noticeTimer;
        bool clutchUsed,speedReported;Vector3 previous,destination;
        public void Begin()
        {
            previous=car.transform.position;startProgress=Vector3.Dot(previous-track.Point(0),(track.Point(1)-track.Point(0)).normalized);
            car.OnRecovery+=()=>{nextGate=2;previous=car.transform.position;};
            if(challenge!=null && challenge.distance_m>0) {destination=PointAt(challenge.distance_m+startProgress);CreateDestination();}
            Active=true;
        }
        public Vector3 PointAt(float distance)
        {
            for(int i=0;i<track.route.Length;i++) {var a=track.Point(i);var b=track.Point(i+1);float length=Vector3.Distance(a,b);if(distance<=length)return Vector3.Lerp(a,b,distance/length)+Vector3.Cross(Vector3.up,(b-a).normalized)*2.8f;distance-=length;}
            return track.Point(0);
        }
        void CreateDestination()
        {
            var go=new GameObject("Destination stop ring");go.transform.SetParent(transform);go.transform.position=destination+Vector3.up*.1f;
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.widthMultiplier=.12f;line.positionCount=64;
            line.material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));line.material.color=new Color(.2f,.9f,.65f);
            for(int i=0;i<64;i++){float a=i/64f*Mathf.PI*2;line.SetPosition(i,new Vector3(Mathf.Cos(a)*3,0,Mathf.Sin(a)*3));}
        }
        public static bool Crosses(Vector3 before,Vector3 after,Vector3 center,Vector3 normal,float width)
        {
            float a=Vector3.Dot(before-center,normal),b=Vector3.Dot(after-center,normal);
            if(a>0 || b<=0 || b-a<.00001f)return false;
            var delta=Vector3.Lerp(before,after,-a/(b-a))-center;
            return Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(normal,Vector3.up).normalized))<=width && Mathf.Abs(delta.y)<=4;
        }
        void FixedUpdate()
        {
            if(!Active || Finished || !car)return;
            float dt=Time.fixedDeltaTime;Elapsed+=dt;LapTime+=dt;
            var pos=car.transform.position;float step=Vector3.Distance(previous,pos);
            if(step<Mathf.Max(15,car.Speed/3.6f*dt*4))Distance+=step;
            else {nextGate=2;previous=pos;}
            while(nextGate<track.route.Length && Crosses(previous,pos,track.Point(nextGate),(track.Point(nextGate)-track.Point(nextGate-1)).normalized,track.roadHalfWidth))nextGate++;
            if(nextGate>=track.route.Length && Crosses(previous,pos,track.Point(0),(track.Point(1)-track.Point(0)).normalized,track.roadHalfWidth) && LapTime>8) {
                BestLap=Mathf.Min(BestLap,LapTime);LapTime=0;Laps++;nextGate=2;
                if(!FreeDrive && (challenge==null || challenge.distance_m<=0) && Laps>=TargetLaps){Completed=true;Finish();}
            }
            var a=track.Point(segment);var b=track.Point(segment+1);var direction=(b-a).normalized;float length=Vector3.Distance(a,b);
            float along=Mathf.Clamp(Vector3.Dot(pos-a,direction),0,length);
            if(Vector3.Distance(pos,a+direction*along)<8) {
                RouteProgress=Mathf.Max(RouteProgress,segmentDistance+along-startProgress);
                if(along>=length-2){segmentDistance+=length;segment=(segment+1)%track.route.Length;}
            }
            clutchUsed |= car.Controls.clutch>.7f;ManualLaunch |= clutchUsed && car.Gear==1 && car.Speed>5 && car.rig.Connected;
            if(car.Speed>Limit+5) {Overspeed+=dt;speedingTime+=dt;if(RoadRules && speedingTime>2.2f && !speedReported){Violation("Quá tốc độ • Giảm về "+Limit+" km/h");speedReported=true;}}
            else {speedingTime=Mathf.Max(0,speedingTime-dt);if(car.Speed<=Limit)speedReported=false;}
            float speed=car.Speed/3.6f;float deceleration=(previousSpeed-speed)/dt;previousSpeed=speed;
            brakeDuration=deceleration>4.5f && speed>2?brakeDuration+dt:0;brakeCooldown=Mathf.Max(0,brakeCooldown-dt);
            if(brakeDuration>.25f && brakeCooldown==0){HarshBrakes++;brakeCooldown=3;}
            if(challenge!=null && challenge.distance_m>0) {
                var delta=pos-destination;bool at=new Vector2(delta.x,delta.z).magnitude<3 && Mathf.Abs(delta.y)<2 && Remaining<6;
                stopTime=at&&car.Speed<1.5f?stopTime+dt:0;
                if(stopTime>=2){Completed=true;Finish();}
            }
            if(car.Wrecked && !FreeDrive)Finish();
            previous=pos;noticeTimer-=dt;if(noticeTimer<=0)Notice="";
        }
        public void Violation(string text){if(!RoadRules)return;Violations++;Notice=text;noticeTimer=5;}
        public void Finish(){if(Finished)return;Finished=true;Active=false;car.Locked=true;car.rig.Hold();OnFinished?.Invoke(DriverProgress.Evaluate(this));}
        public string Objective => challenge!=null && challenge.distance_m>0 ? (Remaining<6?"Dừng trong vòng xanh và giữ yên 2 giây":$"{challenge.name} · Còn {Remaining:0} m · {Limit:0} km/h") : FreeDrive?"LÁI TỰ DO · Giữ đúng làn và tốc độ":$"VÒNG {Mathf.Min(Laps+1,TargetLaps)} / {TargetLaps} · {LapTime:0.0} s";
    }
}
