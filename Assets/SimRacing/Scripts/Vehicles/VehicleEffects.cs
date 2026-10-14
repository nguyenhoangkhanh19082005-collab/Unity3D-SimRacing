using System.Collections.Generic;
using UnityEngine;
namespace SimRacing
{
    public sealed class VehicleEffects : MonoBehaviour
    {
        VehicleController car;VehicleDefinition definition;
        readonly List<Material> owned=new List<Material>();readonly List<Renderer> rear=new List<Renderer>(),left=new List<Renderer>(),right=new List<Renderer>(),reverse=new List<Renderer>();
        ParticleSystem[] spray;TrailRenderer[] skid;Material particleMaterial,skidMaterial;Texture2D smokeTexture;TextMesh[] labels;
        void Start()
        {
            car=GetComponent<VehicleController>();definition=car.definition;
            foreach(var node in GetComponentsInChildren<Transform>(true)) {
                if(node.parent!=transform)continue;var p=node.localPosition;var renderer=node.GetComponentInChildren<MeshRenderer>(true);if(!renderer)continue;
                if(Mathf.Abs(p.z+2.05f)<.012f && Mathf.Abs(p.y-.55f)<.012f) {
                    if(Mathf.Abs(Mathf.Abs(p.x)-.55f)<.012f)rear.Add(renderer);
                    if(Mathf.Abs(Mathf.Abs(p.x)-.25f)<.012f)reverse.Add(renderer);
                }
                if(Mathf.Abs(Mathf.Abs(p.x)-.82f)<.012f && (Mathf.Abs(p.z+2.05f)<.012f||Mathf.Abs(p.z-1.9f)<.012f)) {if(p.x>0)left.Add(renderer);else right.Add(renderer);}
            }
            foreach(var group in new[]{rear,left,right,reverse})foreach(var renderer in group){var material=renderer.material;material.EnableKeyword("_EMISSION");owned.Add(material);renderer.gameObject.SetActive(true);renderer.transform.parent.gameObject.SetActive(true);}
            labels=GetComponentsInChildren<TextMesh>(true);
            foreach(var label in labels) {
                if(label.text.StartsWith("NHIÊN LIỆU"))label.name="FuelDisplay";
                if(label.text=="MÁY TẮT" || label.text=="READY")label.name="EngineStatus";
                if(label.text=="◀")label.name="LeftIndicator";if(label.text=="▶")label.name="RightIndicator";
            }
            smokeTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);var colors=new Color[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float radius=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/16;colors[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Max(0,1-radius),2));}smokeTexture.SetPixels(colors);smokeTexture.Apply();
            particleMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));particleMaterial.mainTexture=smokeTexture;particleMaterial.SetFloat("_Surface",1);particleMaterial.SetFloat("_SrcBlend",5);particleMaterial.SetFloat("_DstBlend",10);particleMaterial.SetFloat("_ZWrite",0);particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");particleMaterial.renderQueue=3000;
            skidMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));skidMaterial.color=new Color(.03f,.035f,.04f);
            spray=new ParticleSystem[4];skid=new TrailRenderer[4];
            for(int i=0;i<4;i++) {
                var go=new GameObject("Wheel contact effects");go.transform.SetParent(transform);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.startLifetime=.8f;main.startSpeed=.2f;main.startSize=new ParticleSystem.MinMaxCurve(.1f,.5f);main.startColor=new Color(.72f,.76f,.8f,.22f);main.maxParticles=64;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.06f;var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.y=.5f;
                var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.2f,1,1));
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particleMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;ps.Play();spray[i]=ps;
                var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=skidMaterial;trail.time=12;trail.startWidth=.19f;trail.endWidth=.18f;trail.minVertexDistance=.2f;trail.alignment=LineAlignment.TransformZ;trail.emitting=false;trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;skid[i]=trail;
            }
        }
        void Glow(List<Renderer> group,Color color,float energy){foreach(var renderer in group){renderer.material.SetColor("_BaseColor",color*Mathf.Max(.05f,energy*.2f));renderer.material.SetColor("_EmissionColor",color*energy);}}
        void LateUpdate()
        {
            if(!car)return;var input=car.Controls;
            Glow(rear,Color.red,input.brake>.08f?2.5f:input.headlights>0?.3f:.04f);Glow(reverse,Color.white,car.Gear==6?1.7f:0);
            Glow(left,new Color(1,.32f,.015f),car.Blink&&(input.hazard||input.left)?2:0);Glow(right,new Color(1,.32f,.015f),car.Blink&&(input.hazard||input.right)?2:0);
            foreach(var label in labels) {
                if(label.name=="LeftIndicator")label.gameObject.SetActive(car.Blink&&(input.left||input.hazard));
                if(label.name=="RightIndicator")label.gameObject.SetActive(car.Blink&&(input.right||input.hazard));
            }
            for(int i=0;i<4;i++) {
                bool contact=definition.wheels[i].GetGroundHit(out var hit);bool slipping=contact && car.Speed>12 && (Mathf.Abs(hit.sidewaysSlip)>.23f||Mathf.Abs(hit.forwardSlip)>.45f);
                var emission=spray[i].emission;emission.rateOverTime=contact&&car.Speed>12?(car.Grip<.8f?24:slipping?13:car.Offroad?14:0):0;
                if(contact){spray[i].transform.position=hit.point+hit.normal*.025f;spray[i].transform.rotation=Quaternion.LookRotation(hit.normal);}
                skid[i].emitting=slipping && car.Grip>.8f && !car.Offroad;
            }
        }
        void OnDestroy(){foreach(var mat in owned)Destroy(mat);Destroy(particleMaterial);Destroy(skidMaterial);Destroy(smokeTexture);}
    }
}
