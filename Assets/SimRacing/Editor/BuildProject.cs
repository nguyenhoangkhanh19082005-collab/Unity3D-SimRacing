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
            foreach(string shaderName in new[]{"SimRacing/Atmosphere","SimRacing/Wheel Smoke","Universal Render Pipeline/Unlit","Universal Render Pipeline/Particles/Unlit"}) {
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
                var world=UnityEngine.Object.Instantiate(prefab);var track=world.GetComponent<TrackDefinition>();
                if(track.sceneryPrefabs!=null && track.sceneryPrefabs.Length>0) {
                    // Same layout as TrackModelUpgrade at runtime: legacy children hidden, authored prefabs + generated route
                    foreach(Transform child in world.transform)child.gameObject.SetActive(false);
                    foreach(var c in world.GetComponents<Collider>())c.enabled=false;
                    foreach(var scenery in track.sceneryPrefabs){if(!scenery)throw new Exception("Missing scenery prefab "+id);UnityEngine.Object.Instantiate(scenery,world.transform,false);}
                    track.route=(Vector3[])TrackRoutes.CartoonOval.Clone();Physics.SyncTransforms();TrackModelUpgrade.AlignRouteToRoad(track);
                }
                Physics.SyncTransforms();int checkedPoints=0;
                for(int i=0;i<track.route.Length;i+=2) {
                    Vector3 forward=(track.Point(i+1)-track.Point(i)).normalized;
                    foreach(float lane in new[]{-2.8f,0,2.8f}) {
                        Vector3 point=track.Point(i)+Vector3.Cross(Vector3.up,forward)*lane;bool road=false;
                        foreach(var hit in Physics.RaycastAll(point+Vector3.up*2.5f,Vector3.down,6))
                            if(hit.normal.y>.4f && Mathf.Abs(hit.point.y-point.y)<.55f+Mathf.Abs(lane)*.3f)road=true;
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
            Prepare();
            // Build straight into the committed game folder so a fresh clone runs without Unity (no second copy on disk).
            string exe=Path.Combine(GameFolder,"SimRacing.exe");
            if(Directory.Exists(GameFolder) && Directory.EnumerateFileSystemEntries(GameFolder).GetEnumerator().MoveNext() && !File.Exists(exe))
                throw new Exception("Refusing to build into "+GameFolder+": it is not a SimRacing build");
            Directory.CreateDirectory(GameFolder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/SimRacing.unity"},locationPathName=exe,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            CopyRuntimeData(Path.Combine(GameFolder,"SimRacing_Data"));
            // Unity's debug-symbol folders must not ship
            foreach(string folder in Directory.GetDirectories(GameFolder))
                if(folder.EndsWith("_DoNotShip")||folder.EndsWith("_ButDontShipItWithYourGame"))Directory.Delete(folder,true);
            Debug.Log("[SIMRACING GAME FOLDER] "+Path.GetFullPath(GameFolder));
            Debug.Log("[SIMRACING BUILD PASS] bytes="+report.summary.totalSize);
        }

        // Ready-to-run player committed to the repository (via Git LFS) so a fresh clone runs without Unity.
        public const string GameFolder="SimRacing-Windows";

        // Data the game reads from disk at runtime rather than from Unity assets: mountain pass meshes and track
        // textures (TrackModelUpgrade), high-poly car OBJ models (VehicleModelUpgrade) and engine sounds (VehicleSound).
        static void CopyRuntimeData(string data)
        {
            foreach(string stale in new[]{"Tracks","Imported","Cars","Audio","Car and transportation sounds collection"})
                if(Directory.Exists(Path.Combine(data,stale)))Directory.Delete(Path.Combine(data,stale),true);
            foreach(string part in new[]{"SnowMountain","Textures"})CopyTree(Path.Combine("Assets/Imported/Tracks",part),Path.Combine(data,"Tracks",part),null);
            // Car models go to short folders (Cars/<car>) so a clone in a deep directory stays under the 260-char path limit
            CopyTree("Assets/Imported/ferrari-f40/source/f40",Path.Combine(data,"Cars","ferrari"),null);
            CopyTree("Assets/Imported/mercedes-benz-e-class-w210-sedan/source/Mercedes-Benz_E-class_W210_sedan_1996",Path.Combine(data,"Cars","mercedes"),null);
            CopyTree("Assets/Imported/bugatti-centodieci/source/centi",Path.Combine(data,"Cars","bugatti"),null);
            Directory.CreateDirectory(Path.Combine(data,"Imported"));
            File.Copy("Assets/Imported/3d-model.obj",Path.Combine(data,"Imported","3d-model.obj"),true);
            CopyTree("Assets/Resources/Audio",Path.Combine(data,"Audio"),null);
        }

        static void CopyTree(string source,string target,Func<string,bool> skipDirectory)
        {
            Directory.CreateDirectory(target);
            foreach(string file in Directory.GetFiles(source))
                if(!file.EndsWith(".meta"))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
            foreach(string directory in Directory.GetDirectories(source)) {
                string name=Path.GetFileName(directory);
                if(skipDirectory!=null && skipDirectory(name))continue;
                CopyTree(directory,Path.Combine(target,name),skipDirectory);
            }
        }
        public static void ReimportAndBuild(){SourceImporter.BuildAssets();Build();}
    }
}
