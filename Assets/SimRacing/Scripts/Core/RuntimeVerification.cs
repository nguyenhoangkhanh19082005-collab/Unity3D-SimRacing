using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
namespace SimRacing
{
    // Runs only with an explicit --verify argument and an isolated loopback rig port.
    public sealed class RuntimeVerification : MonoBehaviour
    {
        SimulationApp app;UdpClient sender;uint stamp=1;int checks,errors;string directory;bool sending;
        int encoder,throttle,brake=4095,clutch=4095,gear,flags;
        List<float> frameTimes=new List<float>();
        public static byte[] Packet(uint timestamp,int steering,int gas,int brake,int clutch,int gear,int flags)
        {
            var b=new byte[28];Action<int,int> put=(p,v)=>{b[p]=(byte)v;b[p+1]=(byte)(v>>8);};
            put(0,0x5349);put(2,2);put(4,(int)timestamp);put(6,(int)(timestamp>>16));put(8,steering);put(10,steering>>16);
            put(12,gas);put(14,brake);put(16,clutch);b[18]=(byte)gear;put(19,flags);put(21,2048);put(23,2048);return b;
        }
        void Check(bool yes,string label){checks++;string dir=directory??Path.Combine(Application.dataPath,"..","Verification");string line=(yes?"[PASS] ":"[FAIL] ")+label;if(!yes){errors++;Debug.LogError(line);}else{Debug.Log(line);}try{File.AppendAllText(Path.Combine(dir,"test-log.txt"),line+"\n");}catch{}}
        void Update(){if(sending && sender!=null){var b=Packet(++stamp,encoder,throttle,brake,clutch,gear,flags);sender.Send(b,b.Length);}if(app && app.ScreenState=="drive")frameTimes.Add(Time.unscaledDeltaTime*1000);}
        IEnumerator Start()
        {
            app=GetComponent<SimulationApp>();
            if(app.Rig.Port==9999){Debug.LogError("Verification refuses hardware port 9999");Application.Quit(2);yield break;}
            directory=Path.Combine(Application.dataPath,"..","Verification");Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(2);yield return Capture("01-menu.png");
            app.Mode="free";app.CarId="mercedes";app.TrackId="coastal";yield return app.LoadDrive();
            yield return new WaitForSecondsRealtime(3);
            Check(app.Car && app.Track && app.CameraRig,"drive loads");Check(app.Car.Grounded,"wheels settle on road");Check(app.Car.Health>99,"spawn does not damage car");
            yield return Capture("02-cockpit.png");
            app.GetComponent<SimulationUI>().enabled=false;yield return Capture("02b-cockpit-clean.png");app.GetComponent<SimulationUI>().enabled=true;
            sender=new UdpClient();sender.Connect(IPAddress.Loopback,app.Rig.Port);sending=true;
            yield return new WaitForSecondsRealtime(.5f);Check(app.Rig.Connected && app.Rig.Held,"new rig remains held");
            app.Rig.SendMessage("OnApplicationFocus",true);Check(app.Rig.Resume(),"neutral + brake explicitly resumes");
            yield return new WaitForSecondsRealtime(3.5f);Check(app.Session.Active,"countdown starts session");
            flags=3;yield return new WaitForSecondsRealtime(.5f);flags=1;Check(app.Car.EngineRunning,"ignition and starter start engine");
            Vector3 origin=app.Car.transform.position;brake=0;clutch=0;gear=1;throttle=1600;
            yield return new WaitForSecondsRealtime(4);Check(app.Car.Speed>5,"manual first gear accelerates");Check(Vector3.Distance(origin,app.Car.transform.position)>3,"vehicle moves along road");
            yield return Capture("03-driving.png");
            throttle=0;brake=4095;clutch=4095;gear=0;
            yield return new WaitForSecondsRealtime(4);Check(app.Car.Speed<1,"brake stops car");
            brake=0;clutch=0;gear=6;throttle=1000;yield return new WaitForSecondsRealtime(1.3f);
            Check(Vector3.Dot(app.Car.Body.linearVelocity,app.Car.transform.forward)<-.5f,"reverse gear moves backward");
            throttle=0;brake=clutch=4095;gear=0;yield return new WaitForSecondsRealtime(2);
            sending=false;yield return new WaitForSecondsRealtime(.4f);Check(!app.Rig.Connected && app.Rig.Held,"timeout holds rig");
            sending=true;yield return new WaitForSecondsRealtime(.3f);Check(app.Rig.Held,"reconnection does not auto resume");
            app.Rig.SendMessage("OnApplicationFocus",true);Check(app.Rig.Resume(),"explicit resume after timeout");
            app.Pause();yield return new WaitForSecondsRealtime(.1f);Check(app.Rig.Held && Time.timeScale==0,"pause holds rig");app.Resume();
            app.Rig.SendMessage("OnApplicationFocus",true);app.Rig.Resume();flags=1|16|64|4;
            yield return new WaitForSecondsRealtime(.3f);Check(app.Car.Controls.headlights==2 && app.Car.Controls.wipers==2 && app.Car.Controls.hazard,"live lights wipers hazard reach car");
            foreach(string weather in new[]{"rain","fog","night","clear"}) {app.Weather=weather;app.Presentation.SetWeather(weather);yield return new WaitForSecondsRealtime(1.2f);yield return Capture("04-"+weather+".png");}
            for(int mode=2;mode<4;mode++){app.CameraRig.Mode=mode;yield return new WaitForSecondsRealtime(.5f);yield return Capture("04-camera-"+mode+".png");}
            app.CameraRig.Mode=1;yield return new WaitForSecondsRealtime(1);yield return Capture("05-exterior.png");
            Check(app.Car.Health>90,"normal road drive does not cause false crash damage");
            Check(app.Car.Recover(false),"stationary recovery");Check(app.Car.Recoveries==1 && app.Rig.Held,"recovery invalidates attempt and holds rig");
            sending=false;sender.Close();sender=null;
            app.Mode="race";app.CarId="ferrari";app.TrackId="mountain";yield return app.LoadDrive();yield return new WaitForSecondsRealtime(3);
            Check(app.Car.Grounded && app.Car.Health>99,"Ferrari mountain spawn");app.Car.Body.position+=app.Car.transform.forward*15f;app.CameraRig.Mode=1;yield return new WaitForSecondsRealtime(1);yield return Capture("06-mountain-ferrari.png");
            app.CarId="bugatti";app.TrackId="coastal";yield return app.LoadDrive();yield return new WaitForSecondsRealtime(3);Check(app.Car.Grounded && app.Car.Health>99,"Bugatti coastal spawn");
            app.Car.Body.position+=app.Car.transform.forward*15f;
            
            // 1. Third-person chase camera (Mode 1)
            app.CameraRig.Mode=1;yield return new WaitForSecondsRealtime(1);
            yield return Capture("07-bugatti.png");
            yield return Capture("bugatti-01-chase-rear.png");

            // 2. First-person cockpit camera (Mode 0) forward view
            app.CameraRig.Mode=0;yield return new WaitForSecondsRealtime(1);
            yield return Capture("07b-bugatti-cockpit.png");
            yield return Capture("bugatti-02-cockpit-fpv.png");

            // 3. First-person cockpit camera (Mode 0) looking right towards passenger side & door
            var camObj = Camera.main ?? app.CameraRig.GetComponentInChildren<Camera>();
            Quaternion savedCamRot = camObj.transform.localRotation;
            camObj.transform.localRotation *= Quaternion.Euler(0, 32f, 0);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("bugatti-03-cockpit-look-right.png");
            camObj.transform.localRotation = savedCamRot;

            // 4. Third-person front 3/4 angle
            app.CameraRig.enabled = false;
            var carTf = app.Car.transform;
            camObj.transform.position = carTf.position + carTf.forward * 6.5f + carTf.right * 3.8f + Vector3.up * 1.4f;
            camObj.transform.LookAt(carTf.position + Vector3.up * 0.7f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("bugatti-04-exterior-front.png");

            // 5. Third-person side profile
            camObj.transform.position = carTf.position + carTf.right * 6.2f + Vector3.up * 1.1f;
            camObj.transform.LookAt(carTf.position + Vector3.up * 0.6f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("bugatti-05-exterior-side.png");

            // 6. Action drive shot (Third-person chase cam while cruising)
            app.CameraRig.enabled = true;
            app.CameraRig.Mode = 1;
            app.Car.Body.linearVelocity = carTf.forward * 22f; // ~80 km/h
            yield return new WaitForSecondsRealtime(1);
            yield return Capture("bugatti-06-driving-action.png");

            // 7. Action cockpit drive shot (First-person FPV while cruising)
            app.CameraRig.Mode = 0;
            app.Car.Body.linearVelocity = carTf.forward * 24f; // ~86 km/h
            yield return new WaitForSecondsRealtime(1);
            yield return Capture("bugatti-07-driving-cockpit.png");
            app.Car.Body.linearVelocity = Vector3.zero;
            app.CameraRig.Mode = 1;
            yield return CareerAndSaves();
            yield return FuelAndImpact();
            frameTimes.Sort();float median=frameTimes.Count>0?frameTimes[frameTimes.Count/2]:0,p95=frameTimes.Count>0?frameTimes[(int)(frameTimes.Count*.95f)]:0;
            string report=$"Checks: {checks}\nErrors: {errors}\nMedian frame ms: {median:F2}\nP95 frame ms: {p95:F2}\nGraphics: {SystemInfo.graphicsDeviceName}\n";
            File.WriteAllText(Path.Combine(directory,"runtime-report.txt"),report);Debug.Log("[RUNTIME COMPLETE] "+report.Replace('\n',' '));
            yield return new WaitForSecondsRealtime(1);Application.Quit(errors==0?0:1);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name));
            yield return null;
        }
        IEnumerator CareerAndSaves()
        {
            encoder=throttle=gear=flags=0;brake=clutch=4095;
            app.Mode="career";app.ChallengeIndex=0;yield return app.LoadDrive();yield return new WaitForSecondsRealtime(2);
            sender=new UdpClient();sender.Connect(IPAddress.Loopback,app.Rig.Port);sending=true;
            yield return new WaitForSecondsRealtime(.4f);
            float readyDeadline=Time.unscaledTime+3;
            while((!app.Rig.Connected || !app.Rig.Raw.Ready || app.Car.Speed>1) && Time.unscaledTime<readyDeadline)yield return null;
            app.Rig.SendMessage("OnApplicationFocus",true);
            Debug.Log($"[CAREER READY] held={app.Rig.Held} connected={app.Rig.Connected} ready={app.Rig.Raw.Ready} paused={app.Rig.Paused} speed={app.Car.Speed} brake={app.Rig.Raw.brake} throttle={app.Rig.Raw.throttle} gear={app.Rig.Raw.gear} starter={app.Rig.Raw.starter}");
            bool takeover=app.Rig.Resume();Check(takeover,"career safe takeover");if(!takeover){sending=false;yield break;}
            yield return new WaitForSecondsRealtime(3.5f);flags=3;yield return new WaitForSecondsRealtime(.3f);flags=1;gear=1;clutch=0;
            float timeout=Time.unscaledTime+75;
            while(!app.Session.Finished && Time.unscaledTime<timeout) {
                int nearest=app.Track.Closest(app.Car.transform.position);Vector3 next=app.Track.Point(nearest+2),forward=(app.Track.Point(nearest+3)-next).normalized;
                Vector3 target=next+Vector3.Cross(Vector3.up,forward)*2.8f;
                var local=app.Car.transform.InverseTransformPoint(target);
                float angle=Mathf.Atan2(local.x,Mathf.Max(1,local.z));float roadWheel=Mathf.Atan2(2*2.82f*Mathf.Sin(angle),Mathf.Max(4,local.magnitude))*Mathf.Rad2Deg;
                encoder=Mathf.RoundToInt(Mathf.Clamp(roadWheel/Mathf.Lerp(31.5f,7,app.Car.Speed/220),-1,1)*8000);
                float wanted=Mathf.Min(28,app.Session.Remaining*1.8f);if(app.Session.Remaining<1.8f)wanted=0;
                float error=wanted-app.Car.Speed;throttle=Mathf.RoundToInt(Mathf.Clamp01(error*.075f)*4095);brake=Mathf.RoundToInt(Mathf.Clamp01(-error*.16f)*4095);
                if(wanted==0){brake=4095;clutch=4095;gear=0;}
                yield return null;
            }
            Check(app.Session.Completed && app.Session.ManualLaunch,"career route and manual stop complete");
            Check(app.LastResult!=null && app.LastResult.passed && app.LastResult.stars==3,"career evaluates three-star clean drive");
            yield return Capture("08-career-results.png");
            sending=false;sender.Close();sender=null;
            if(app.LastResult!=null && app.LastResult.passed){DriverProgress.Record(app.Profile,app.Session,app.LastResult,"test_coastal_mercedes");var loaded=LocalStore.Load("profile.json",new DriverProfile(),p=>p.Valid);Check(loaded.Stars==3 && loaded.xp>0,"career progress persists");}
            var first=new DriverProfile{xp=100};Check(LocalStore.Save("integrity-test.json",first),"first atomic save");first.xp=200;Check(LocalStore.Save("integrity-test.json",first),"second save keeps backup");
            File.WriteAllText(Path.Combine(LocalStore.DirectoryPath,"integrity-test.json"),"{}");
            // A structurally invalid version must fall back to the prior valid backup.
            File.WriteAllText(Path.Combine(LocalStore.DirectoryPath,"integrity-test.json"),"{\"version\":999}");
            Check(LocalStore.Load("integrity-test.json",new DriverProfile(),p=>p.Valid).xp==100,"invalid primary recovers backup");
            var ghost=new GhostData{duration=1};ghost.frames.Add(new GhostFrame{t=0,p=Vector3.zero,q=Quaternion.identity});ghost.frames.Add(new GhostFrame{t=1,p=Vector3.forward,q=Quaternion.identity});
            Check(LocalStore.Save("ghost-verification.json",ghost) && LocalStore.Load("ghost-verification.json",(GhostData)null,g=>g.Valid)!=null,"ghost serialization roundtrip");ghost.duration=float.NaN;Check(!ghost.Valid,"non-finite ghost rejected");
        }
        IEnumerator FuelAndImpact()
        {
            app.Mode="free";app.CarId="mercedes";app.TrackId="coastal";yield return app.LoadDrive();
            app.Car.Body.position=app.Track.fuelStation+Vector3.up*.7f;app.Car.Body.linearVelocity=Vector3.zero;app.Car.Fuel=20;
            yield return new WaitForSecondsRealtime(2.5f);Check(app.Car.Refueling && app.Car.Fuel>24,"stopped engine-off vehicle refuels at station");
            app.Car.Body.position=app.Track.spawnPosition+Vector3.up*.15f;app.Car.Body.rotation=app.Track.spawnRotation;
            yield return new WaitForSecondsRealtime(1);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Verification impact fixture";wall.transform.position=app.Car.transform.position+app.Car.transform.forward*8+Vector3.up;wall.transform.rotation=app.Car.transform.rotation;wall.transform.localScale=new Vector3(7,3,.5f);
            app.Car.Body.linearVelocity=app.Car.transform.forward*25;
            yield return new WaitForSecondsRealtime(1.2f);Check(app.Car.Health<95,"real wall contact causes damage");Destroy(wall);
            app.Car.Body.linearVelocity=Vector3.zero;app.Car.Body.angularVelocity=Vector3.zero;
            yield return new WaitForSecondsRealtime(.3f);Check(app.Car.Recover(false) && app.Car.Health==100,"recovery repairs collision damage");
        }
        void OnDestroy(){sender?.Close();}
    }
}
