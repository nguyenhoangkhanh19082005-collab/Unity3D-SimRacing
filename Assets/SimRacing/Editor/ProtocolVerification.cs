using System;
using UnityEngine;
namespace SimRacing.Editor
{
    public static class ProtocolVerification
    {
        static int checks;
        static void Check(bool yes,string label){if(!yes)throw new Exception("Protocol check failed: "+label);checks++;}
        public static void Run()
        {
            checks=0;var c=new RigCalibration();var b=RuntimeVerification.Packet(100,0,0,4095,4095,0,0);
            Check(RigProtocol.TryDecode(b,c,out var sample),"valid V2");Check(sample.Ready,"neutral hold ready");
            b[0]=0;Check(!RigProtocol.TryDecode(b,c,out _),"bad magic");b[0]=0x49;
            b[2]=3;Check(!RigProtocol.TryDecode(b,c,out _),"unknown version");b[2]=2;
            b[18]=7;Check(!RigProtocol.TryDecode(b,c,out _),"gear range");b[18]=0;
            b[13]=255;Check(!RigProtocol.TryDecode(b,c,out _),"ADC range");b[13]=0;
            Check(!RigProtocol.TryDecode(new byte[26],c,out _),"truncated packet");
            Check(!RigProtocol.TryDecode(new byte[29],c,out _),"oversized packet");
            Check(RigProtocol.Newer(2,uint.MaxValue-2),"timestamp wrap");Check(!RigProtocol.Newer(10,10),"duplicate timestamp");Check(!RigProtocol.Newer(9,10),"old timestamp");
            // Firmware counts positive encoder ticks when the wheel turns LEFT, so full right is negative.
            b=RuntimeVerification.Packet(105,-8000,4095,0,0,6,0x3ff);Check(RigProtocol.TryDecode(b,c,out sample),"all accessories");
            Check(sample.steer==1 && sample.throttle==1 && sample.brake==0 && sample.clutch==0 && sample.gear==6,"controls");
            Check(sample.ignition && sample.starter && sample.hazard && sample.headlights==2 && sample.horn && sample.left && sample.right,"accessories");
            b[26]=1;RigProtocol.TryDecode(b,c,out sample);Check(sample.gearFault && !sample.Ready,"gear fault");
            Check(Mathf.Approximately(c.SteeringFromTicks(-1500),c.Steering(.5f)) && c.SteeringFromTicks(1500)<0,"positive ticks steer left, default +/-3000 ticks");
            // Pedal endpoints from the in-game calibration: a pot wired backwards that only uses part of its travel
            var cal=new RigCalibration{throttleReleased=3000,throttlePressed=1000,brakeReleased=500,brakePressed=2500};
            Check(cal.Valid && cal.Throttle(3000)==0 && cal.Throttle(1000)==1 && Mathf.Abs(cal.Throttle(2000)-.5f)<.02f,"reversed partial-travel throttle");
            Check(cal.Brake(400)==0 && cal.Brake(2600)==1,"pedal endpoints clamp");
            Check(!new RigCalibration{clutchPressed=100}.Valid,"too-short pedal travel rejected");
            Check(!new RigCalibration{version=1}.Valid,"old settings version rejected");
            // Shifter switches wired in another order: switch U2 is 3rd gear
            cal.gearMap=new[]{0,1,3,2,5,4,6};b=RuntimeVerification.Packet(106,0,0,4095,4095,2,0);
            Check(RigProtocol.TryDecode(b,cal,out sample) && sample.switchGear==2 && sample.gear==3,"shifter map");
            Check(!RigCalibration.GearMapValid(new[]{1,1,2,3,4,5,6}) && !RigCalibration.GearMapValid(new[]{0,1,2}),"invalid shifter map rejected");
            b[26]=1;RigProtocol.TryDecode(b,cal,out sample);Check(sample.gear==0 && !sample.SafeToTakeOver,"two shifter switches read as neutral");
            // START button takeover: the safe state ignores the starter bit, F9's Ready does not
            b=RuntimeVerification.Packet(107,0,0,4095,4095,0,3);RigProtocol.TryDecode(b,c,out sample);Check(sample.SafeToTakeOver && !sample.Ready,"start button takeover state");
            // Latched shifter (firmware fix4): in gear, the clutch pressed replaces neutral for the takeover
            b=RuntimeVerification.Packet(108,0,0,4095,4095,1,1);RigProtocol.TryDecode(b,c,out sample);Check(sample.gear==1 && sample.Ready,"in gear with clutch pressed is ready");
            b=RuntimeVerification.Packet(109,0,0,4095,0,1,1);RigProtocol.TryDecode(b,c,out sample);Check(!sample.SafeToTakeOver,"in gear with clutch released is not ready");
            var fb=RigProtocol.Feedback(-500,80,3000,3,45,true,sample,true,256,123);
            Check(fb.Length==16 && RigProtocol.U16(fb,0)==0x4642,"feedback framing");Check(unchecked((short)RigProtocol.U16(fb,2))==-500,"signed feedback");Check(RigProtocol.U16(fb,4)==800 && RigProtocol.U16(fb,6)==3000 && fb[13]==255,"telemetry bounds");
            Check(Mathf.Approximately(c.Feedback(4000),800),"FFB clamp");
            Check(!DriveSession.Crosses(new Vector3(0,0,1),new Vector3(0,0,-1),Vector3.zero,Vector3.forward,7),"reverse cannot finish");
            Check(!DriveSession.Crosses(new Vector3(20,0,-1),new Vector3(20,0,1),Vector3.zero,Vector3.forward,7),"shortcut cannot finish");
            Check(DriveSession.Crosses(new Vector3(2,0,-1),new Vector3(2,0,1),Vector3.zero,Vector3.forward,7),"correct gate crossing");
            Debug.Log("[PROTOCOL PASS] checks="+checks);
        }
    }
}
