using UnityEngine;
namespace SimRacing
{
    public sealed class VehicleSound : MonoBehaviour
    {
        VehicleController car;AudioSource engine,wind,horn;AudioClip engineClip,noiseClip,hornClip;
        void Start()
        {
            car=GetComponent<VehicleController>();
            engineClip=Tone("Combustion",110,true);hornClip=Tone("Horn",330,false);
            var noise=new float[22050];var random=new System.Random(17);float low=0;
            for(int i=0;i<noise.Length;i++){low=Mathf.Lerp(low,(float)random.NextDouble()*2-1,.14f);noise[i]=low*.25f;}
            noiseClip=AudioClip.Create("Wind",noise.Length,1,22050,false);noiseClip.SetData(noise,0);
            engine=Source(engineClip);wind=Source(noiseClip);horn=Source(hornClip);
        }
        AudioClip Tone(string label,float hz,bool harmonics)
        {
            var samples=new float[22050];for(int i=0;i<samples.Length;i++) {float p=i/22050f*hz*Mathf.PI*2;samples[i]=Mathf.Sin(p)*.15f+(harmonics?Mathf.Sin(p*2)*.07f+Mathf.Sin(p*4)*.025f:Mathf.Sin(p*1.5f)*.1f);}
            var clip=AudioClip.Create(label,samples.Length,1,22050,false);clip.SetData(samples,0);return clip;
        }
        AudioSource Source(AudioClip clip) {var source=gameObject.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.volume=0;source.Play();return source;}
        void Update() {if(!engine)return;engine.pitch=Mathf.Clamp(car.RPM/1800,.3f,4);engine.volume=car.EngineRunning?.12f+car.Controls.throttle*.18f:0;wind.volume=Mathf.Clamp01(car.Speed/240)*.28f;horn.volume=car.Controls.horn?.35f:0;}
        void OnDestroy(){Destroy(engineClip);Destroy(noiseClip);Destroy(hornClip);}
    }
}
