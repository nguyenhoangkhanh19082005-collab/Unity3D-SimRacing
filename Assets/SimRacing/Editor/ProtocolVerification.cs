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
            b=RuntimeVerification.Packet(105,8000,4095,0,0,6,0x3ff);Check(RigProtocol.TryDecode(b,c,out sample),"all accessories");
            Check(sample.steer==1 && sample.throttle==1 && sample.brake==0 && sample.clutch==0 && sample.gear==6,"controls");
            Check(sample.ignition && sample.starter && sample.hazard && sample.headlights==2 && sample.wipers==2 && sample.horn && sample.left && sample.right,"accessories");
            b[26]=1;RigProtocol.TryDecode(b,c,out sample);Check(sample.gearFault && !sample.Ready,"gear fault");
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
