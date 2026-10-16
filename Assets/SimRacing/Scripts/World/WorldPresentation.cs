using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimRacing
{
    public sealed class WorldPresentation : MonoBehaviour
    {
        public string Weather="clear";
        public Camera cameraTarget;
        public VehicleController car;
        Light sun;Volume volume;ParticleSystem rain;
        Material sky;float time;ReflectionProbe reflection;
        void Awake()
        {
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional)light.enabled=false;
            sun=new GameObject("Daylight").AddComponent<Light>();sun.transform.SetParent(transform);sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;
            sun.shadowBias=.035f;sun.shadowNormalBias=.25f;sun.transform.rotation=Quaternion.Euler(35,-40,0);
            volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.profile=ScriptableObject.CreateInstance<VolumeProfile>();
            var tone=volume.profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.ACES;
            var bloom=volume.profile.Add<Bloom>(true);bloom.intensity.value=.14f;bloom.threshold.value=1.1f;bloom.scatter.value=.55f;
            var color=volume.profile.Add<ColorAdjustments>(true);color.contrast.value=4;color.saturation.value=-3;color.postExposure.value=.6f;
            var vignette=volume.profile.Add<Vignette>(true);vignette.intensity.value=.15f;vignette.smoothness.value=.65f;
            var skyShader=Shader.Find("SimRacing/Atmosphere");if(skyShader){sky=new Material(skyShader);RenderSettings.skybox=sky;}
            RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
            QualitySettings.shadowDistance=95;QualitySettings.shadowCascades=2;QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
            reflection=new GameObject("Sky reflection").AddComponent<ReflectionProbe>();reflection.transform.SetParent(transform);reflection.mode=ReflectionProbeMode.Realtime;reflection.refreshMode=ReflectionProbeRefreshMode.ViaScripting;reflection.resolution=128;reflection.cullingMask=0;reflection.size=Vector3.one*10000;
            CreateRain();SetWeather(Weather);
        }
        public void SetWeather(string weather)
        {
            Weather=weather;bool night=weather=="night",wet=weather=="rain",fog=weather=="fog";
            sun.intensity=night?.16f:wet?.6f:fog?.8f:1.8f;sun.color=night?new Color(.5f,.65f,1):new Color(1,.93f,.82f);
            var track = FindFirstObjectByType<TrackDefinition>();
            if (track != null)
            {
                foreach (var l in track.GetComponentsInChildren<Light>(true))
                    if (l.type == LightType.Spot) l.enabled = night;
            }
            RenderSettings.ambientSkyColor=night?new Color(.04f,.06f,.105f):wet?new Color(.3f,.36f,.43f):new Color(.48f,.62f,.8f);
            RenderSettings.ambientEquatorColor=night?new Color(.03f,.035f,.055f):new Color(.35f,.38f,.4f);
            RenderSettings.ambientGroundColor=night?new Color(.018f,.022f,.025f):new Color(.16f,.17f,.16f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=fog?.014f:wet?.0045f:night?.003f:.0012f;
            RenderSettings.fogColor=night?new Color(.035f,.055f,.095f):fog?new Color(.59f,.65f,.69f):new Color(.58f,.7f,.8f);
            if(sky){sky.SetFloat("_Night",night?1:0);sky.SetFloat("_Cloudiness",wet?.9f:fog?.7f:.35f);}
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(night?new Color(.035f,.048f,.075f):wet?new Color(.3f,.35f,.42f):new Color(.55f,.64f,.76f));RenderSettings.ambientProbe=ambient;
            if(reflection)reflection.RenderProbe();
            if(car)car.Grip=wet?.68f:fog?.82f:1;
            var emission=rain.emission;emission.rateOverTime=wet?1000:0;
            Shader.SetGlobalFloat("_SimWetness",wet?1:0);
            foreach(var material in Resources.FindObjectsOfTypeAll<Material>().Where(m=>m.name.StartsWith("asphalt")))
                if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",wet?.73f:.18f);
        }
        void CreateRain()
        {
            var go=new GameObject("Rain around driver");go.transform.SetParent(transform);rain=go.AddComponent<ParticleSystem>();
            rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=rain.main;main.loop=true;main.startLifetime=1;main.startSpeed=0;main.startSize=.028f;main.maxParticles=1800;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startColor=new Color(.6f,.7f,.8f,.5f);main.gravityModifier=0;
            var shape=rain.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(28,12,28);
            var velocity=rain.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=-1;velocity.y=-20;
            var renderer=rain.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=9;renderer.velocityScale=.03f;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Universal Render Pipeline/Unlit"));mat.color=new Color(.6f,.7f,.8f,.3f);renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;rain.Play();
        }
        void LateUpdate(){time+=Time.deltaTime;if(cameraTarget)rain.transform.position=cameraTarget.transform.position+Vector3.up*8;}
        void OnDestroy(){if(volume)Destroy(volume.profile);if(sky)Destroy(sky);Shader.SetGlobalFloat("_SimWetness",0);}
    }
}
