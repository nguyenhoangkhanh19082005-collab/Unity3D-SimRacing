using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace SimRacing
{
    public sealed class DriverCamera : MonoBehaviour
    {
        public VehicleController car;public int Mode;public float Motion=.65f;
        public Camera view,mirror;public RenderTexture MirrorTexture {get;private set;}
        Vector3 chaseVelocity,lastCarPosition;Vector2 look;int previousMode=-1;bool previousButton;float mirrorClock;
        void Awake()
        {
            view=GetComponent<Camera>();view.nearClipPlane=.045f;view.farClipPlane=1800;
            var data=view.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rear=new GameObject("Rear view camera");rear.transform.SetParent(transform.parent);mirror=rear.AddComponent<Camera>();
            mirror.nearClipPlane=.2f;mirror.farClipPlane=170;mirror.fieldOfView=65;mirror.cullingMask=~(1<<8);mirror.enabled=false;
            mirror.allowHDR=false;mirror.allowMSAA=false;mirror.GetUniversalAdditionalCameraData().renderShadows=false;
            MirrorTexture=new RenderTexture(384,108,16,RenderTextureFormat.ARGB32);MirrorTexture.name="Rearview 30Hz";MirrorTexture.Create();mirror.targetTexture=MirrorTexture;
        }
        void LateUpdate()
        {
            if(!car)return;
            if(car.definition.mirror && car.definition.mirror.sharedMaterial.mainTexture!=MirrorTexture) {
                var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.mainTexture=MirrorTexture;material.mainTextureScale=new Vector2(-1,1);material.mainTextureOffset=new Vector2(1,0);car.definition.mirror.material=material;
            }
            var input=car.rig?car.rig.Raw:default;
            if(Input.GetKeyDown(KeyCode.V)||(input.cameraButton&&!previousButton))Mode=(Mode+1)%4;
            previousButton=input.cameraButton;
            if(Input.GetKeyDown(KeyCode.F1))Mode=0;if(Input.GetKeyDown(KeyCode.F2))Mode=1;if(Input.GetKeyDown(KeyCode.F3))Mode=2;if(Input.GetKeyDown(KeyCode.F4))Mode=3;
            float dt=Mathf.Min(Time.deltaTime,.1f);var t=car.transform;
            bool snap=previousMode!=Mode || Vector3.Distance(lastCarPosition,t.position)>6;
            if(snap){chaseVelocity=Vector3.zero;look=Vector2.zero;}previousMode=Mode;lastCarPosition=t.position;
            car.SetCockpit(Mode==0);
            if(Mode==0) {
                var acceleration=car.Acceleration*Motion;
                var head=Vector3.ClampMagnitude(new Vector3(-acceleration.x*.002f,-Mathf.Abs(acceleration.y)*.0004f,-acceleration.z*.0018f),.04f);
                transform.position=t.TransformPoint(new Vector3(.35f,1.12f,-.32f)+head);
                look=Vector2.Lerp(look,new Vector2(input.lookX*75,input.lookY*30),1-Mathf.Exp(-14*dt));
                transform.rotation=t.rotation*Quaternion.Euler(2.92f+look.y+Mathf.Clamp(-acceleration.z*.057f,-.85f,.85f),-look.x,Mathf.Clamp(-acceleration.x*.103f,-1.4f,1.4f));view.fieldOfView=76;
            } else if(Mode==1) {
                float factor=Mathf.Clamp01(car.Speed/220);var desired=t.position-t.forward*(6.2f+factor*1.2f)+Vector3.up*(2.15f+factor*.25f);
                var position=snap?desired:Vector3.SmoothDamp(transform.position,desired,ref chaseVelocity,.16f,200,dt);
                var pivot=t.position+Vector3.up;var delta=position-pivot;
                foreach(var hit in Physics.SphereCastAll(pivot,.18f,delta.normalized,delta.magnitude,~(1<<8),QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(t) && hit.distance<delta.magnitude) {position=pivot+delta.normalized*Mathf.Max(.4f,hit.distance-.2f);delta=position-pivot;}
                transform.position=position;transform.LookAt(t.position+t.forward*3.5f+Vector3.up*.95f);view.fieldOfView=Mathf.Lerp(view.fieldOfView,65+factor*8,1-Mathf.Exp(-5*dt));
            } else {transform.position=t.TransformPoint(Mode==2?new Vector3(0,.82f,1.4f):new Vector3(0,.38f,2.15f));transform.rotation=t.rotation;view.fieldOfView=Mode==2?70:65;}
            mirror.transform.SetPositionAndRotation(t.position+Vector3.up*1.2f-t.forward*2.7f,Quaternion.LookRotation(-t.forward,t.up));
            mirrorClock+=dt;if(mirrorClock>=1f/30){mirrorClock=0;mirror.Render();}
        }
        void OnDestroy(){if(MirrorTexture){MirrorTexture.Release();Destroy(MirrorTexture);}if(mirror)Destroy(mirror.gameObject);}
    }
}
