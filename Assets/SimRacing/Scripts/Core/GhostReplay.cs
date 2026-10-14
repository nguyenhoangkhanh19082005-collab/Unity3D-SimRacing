using System;
using System.Collections.Generic;
using UnityEngine;
namespace SimRacing
{
    [Serializable] public struct GhostFrame {public float t;public Vector3 p;public Quaternion q;}
    [Serializable] public sealed class GhostData
    {
        public int version=1;public float duration;public List<GhostFrame> frames=new List<GhostFrame>();
        public bool Valid {
            get {
                if(version!=1||!Finite(duration)||duration<=0||duration>7200||frames==null||frames.Count<2||frames.Count>72001)return false;
                float previous=-1;foreach(var f in frames){if(float.IsNaN(f.t)||f.t<previous||f.t>duration+.2f||!Finite(f.p.x)||!Finite(f.p.y)||!Finite(f.p.z)||!Finite(f.q.x)||!Finite(f.q.y)||!Finite(f.q.z)||!Finite(f.q.w))return false;previous=f.t;}return true;
            }
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Mathf.Abs(v)<100000;
    }
    public sealed class GhostReplay : MonoBehaviour
    {
        VehicleController car;DriveSession session;GhostData recorded=new GhostData(),best;string file;bool test,invalid;float clock;int cursor;
        GameObject visual;Material material;
        public void Setup(VehicleController vehicle,DriveSession drive,string key,bool testMode)
        {
            car=vehicle;session=drive;test=testMode;file="ghost_"+key+".json";
            if(session.FreeDrive || (session.challenge!=null && session.challenge.distance_m>0))return;
            best=LocalStore.Load(file,(GhostData)null,g=>g.Valid);
            if(best!=null && car.definition.exterior) {
                visual=VehicleVisual.Create(car.definition,transform);visual.name="Personal best ghost";
                foreach(var collider in visual.GetComponentsInChildren<Collider>())Destroy(collider);
                material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=new Color(.15f,.75f,.85f,.26f);
                material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);material.SetFloat("_ZWrite",0);material.renderQueue=3000;
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>()){var mats=renderer.sharedMaterials;for(int i=0;i<mats.Length;i++)mats[i]=material;renderer.sharedMaterials=mats;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
            }
            session.OnFinished+=OnFinish;car.OnRecovery+=()=>invalid=true;
        }
        void Update()
        {
            if(!session || !session.Active || session.FreeDrive || (session.challenge!=null && session.challenge.distance_m>0))return;
            clock+=Time.deltaTime;if(clock>=.1f && recorded.frames.Count<72001){clock=0;recorded.frames.Add(new GhostFrame{t=session.Elapsed,p=car.transform.position,q=car.transform.rotation});}
            if(visual && best!=null){while(cursor<best.frames.Count-2 && best.frames[cursor+1].t<session.Elapsed)cursor++;var a=best.frames[cursor];var b=best.frames[cursor+1];float t=Mathf.InverseLerp(a.t,b.t,session.Elapsed);visual.transform.SetPositionAndRotation(Vector3.Lerp(a.p,b.p,t),Quaternion.Slerp(a.q,b.q,t));visual.SetActive(session.Elapsed<=best.duration);}
        }
        void OnFinish(Result result){recorded.duration=session.Elapsed;if(!test && !invalid && result.passed && recorded.Valid && (best==null || recorded.duration<best.duration))LocalStore.Save(file,recorded);}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
