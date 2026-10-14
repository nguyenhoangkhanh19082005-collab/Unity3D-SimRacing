using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SimRacing.Editor
{
    public static class BuildProject
    {
        public static void Prepare()
        {
            SourceImporter.BuildTraffic();
            ConfigureWorldMaterials();
            PlayerSettings.companyName="SimRacing";PlayerSettings.productName="SimRacing Unity";
            PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
            PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue=0;settings.ApplyModifiedPropertiesWithoutUndo();
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if(!pipeline)throw new Exception("URP template asset missing");
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            pipeline.renderScale=1;pipeline.shadowDistance=95;pipeline.msaaSampleCount=1;
            pipeline.supportsHDR=true;pipeline.useSRPBatcher=true;
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach(string shaderName in new[]{"SimRacing/Atmosphere","Universal Render Pipeline/Unlit","Universal Render Pipeline/Particles/Unlit"}) {
                var shader=Shader.Find(shaderName);if(!shader)continue;bool found=false;for(int i=0;i<shaders.arraySize;i++)found|=shaders.GetArrayElementAtIndex(i).objectReferenceValue==shader;
                if(!found){shaders.InsertArrayElementAtIndex(shaders.arraySize);shaders.GetArrayElementAtIndex(shaders.arraySize-1).objectReferenceValue=shader;}
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory("Assets/Scenes");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/SimRacing.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/SimRacing.unity",true)};
            AssetDatabase.SaveAssets();
            VerifyData();Debug.Log("[SIMRACING PROJECT READY]");
        }
        static void ConfigureWorldMaterials()
        {
            var shader=Shader.Find("SimRacing/World Surface");
            if(!shader)throw new Exception("World surface shader missing");
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/SimRacing/Imported/Materials"})) {
                var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                int kind=mat.name.StartsWith("asphalt")?0:mat.name.StartsWith("ocean")?1:mat.name.StartsWith("sand")?2:mat.name.StartsWith("ground")?3:mat.name.StartsWith("facade")?4:mat.name.StartsWith("foliage_wind")?5:-1;
                if(kind<0)continue;Color color=mat.GetColor("_BaseColor");mat.shader=shader;mat.SetFloat("_Kind",kind);mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",kind==1?.93f:.18f);EditorUtility.SetDirty(mat);
                if(kind==0)mat.SetColor("_BaseColor",new Color(.33f,.345f,.36f));
                if(kind==5)mat.SetFloat("_Cull",0);
            }
        }
        public static void VerifyData()
        {
            foreach(string id in new[]{"mercedes_w210","ferrari_f40","bugatti_centodieci"}) {
                var prefab=Resources.Load<GameObject>("Vehicles/"+id);if(!prefab)throw new Exception("Missing car "+id);
                var car=prefab.GetComponent<VehicleDefinition>();if(car.wheels.Length!=4 || !car.cockpit || !car.mirror || !car.digitalSpeed || !car.steeringWheel)throw new Exception("Incomplete vehicle "+id);
                foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))foreach(var mat in renderer.sharedMaterials)if(!mat || !mat.shader || mat.shader.name.Contains("Error"))throw new Exception("Bad material "+id);
                Debug.Log("[ASSET PASS] "+id+" wheels=4 cockpit+mirror+instruments");
            }
            foreach(string id in new[]{"coastal_track","race_circuit"}) {
                var prefab=Resources.Load<GameObject>("Worlds/"+id);if(!prefab || prefab.GetComponent<TrackDefinition>().route.Length<200)throw new Exception("Missing route "+id);
                Debug.Log("[ASSET PASS] "+id);
                var world=UnityEngine.Object.Instantiate(prefab);var track=world.GetComponent<TrackDefinition>();Physics.SyncTransforms();int checkedPoints=0;
                for(int i=0;i<track.route.Length;i+=2) {
                    Vector3 forward=(track.Point(i+1)-track.Point(i)).normalized;
                    foreach(float lane in new[]{-2.8f,0,2.8f}) {
                        Vector3 point=track.Point(i)+Vector3.Cross(Vector3.up,forward)*lane;bool road=false;
                        foreach(var hit in Physics.RaycastAll(point+Vector3.up*2.5f,Vector3.down,6))
                            if(hit.normal.y>.4f && Mathf.Abs(hit.point.y-point.y)<.55f)road=true;
                        if(!road)throw new Exception("Road collision missing "+id+" point "+i+" lane "+lane);checkedPoints++;
                    }
                }
                UnityEngine.Object.DestroyImmediate(world);Debug.Log("[ROAD PASS] "+id+" raycasts="+checkedPoints);
            }
            ProtocolVerification.Run();
        }
        [MenuItem("SimRacing/Build Windows")]
        public static void Build()
        {
            Prepare();Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/SimRacing.unity"},locationPathName="Builds/Windows/SimRacing.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("[SIMRACING BUILD PASS] bytes="+report.summary.totalSize);
        }
        public static void ReimportAndBuild(){SourceImporter.BuildAssets();Build();}
    }
}
