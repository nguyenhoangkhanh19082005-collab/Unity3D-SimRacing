using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimRacing.Editor
{
    public static class SourceImporter
    {
        static string Source = "Assets/SourceData/";
        static string Imported = "Assets/SimRacing/Imported/";
        static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        static Material[] materials;
        static Color ColorOf(JToken a, Color fallback)
        {
            if (!(a is JArray values) || values.Count < 3) return fallback;
            return new Color((float)values[0],(float)values[1],(float)values[2],values.Count>3?(float)values[3]:1);
        }
        static Vector3 V(JToken a) => a == null ? Vector3.zero : new Vector3(-(float)a[0], (float)a[1], (float)a[2]);
        static float F(JToken a, float fallback = 0) => a == null ? fallback : (float)a;
        static bool B(JToken a) => a != null && (bool)a;
        static void SetTransform(Transform t, JToken a)
        {
            // Reflect X to convert handedness while retaining +Z as the vehicle's forward.
            Vector3 x = new Vector3((float)a[0],-(float)a[1],-(float)a[2]);
            Vector3 y = new Vector3(-(float)a[3],(float)a[4],(float)a[5]);
            Vector3 z = new Vector3(-(float)a[6],(float)a[7],(float)a[8]);
            t.localPosition = new Vector3(-(float)a[9],(float)a[10],(float)a[11]);
            Vector3 scale = new Vector3(x.magnitude,y.magnitude,z.magnitude);
            if (Vector3.Dot(Vector3.Cross(x,y),z)<0) scale.x *= -1;
            t.localRotation = z.sqrMagnitude>.000001f && y.sqrMagnitude>.000001f ? Quaternion.LookRotation(z,y) : Quaternion.identity;
            t.localScale = scale;
        }
        [MenuItem("SimRacing/Import source geometry")]
        public static void BuildAssets()
        {
            Source="Assets/SourceData/";Imported="Assets/SimRacing/Imported/";
            Directory.CreateDirectory(Imported+"Meshes");
            Directory.CreateDirectory(Imported+"Materials");
            Directory.CreateDirectory("Assets/Resources/Worlds");
            Directory.CreateDirectory("Assets/Resources/Vehicles");
            AssetDatabase.Refresh();
            Meshes.Clear();
            ImportMaterials();
            foreach(string id in new[]{"coastal_track","race_circuit","mercedes_w210","ferrari_f40","bugatti_centodieci"})
                ImportScene(id);
            AssetDatabase.SaveAssets();
            Debug.Log("[SIMRACING IMPORT COMPLETE] Native Unity prefabs and meshes saved.");
        }
        public static void BuildTraffic()
        {
            Source="Assets/TrafficSourceData/";Imported="Assets/SimRacing/ImportedTraffic/";
            Directory.CreateDirectory(Imported+"Meshes");Directory.CreateDirectory(Imported+"Materials");AssetDatabase.Refresh();
            Meshes.Clear();ImportMaterials();ImportScene("traffic_vehicle");AssetDatabase.SaveAssets();
        }
        static void ImportMaterials()
        {
            var rows = (JArray)JObject.Parse(File.ReadAllText(Source+"materials.json"))["materials"];
            materials = new Material[rows.Count];
            for(int i=0;i<rows.Count;i++) {
                JToken row=rows[i]; string shader=(string)row["shader"]??"";
                string assetPath=Imported+"Materials/material_"+i.ToString("D5")+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                bool fresh=mat==null;
                if(fresh) mat=new Material(Shader.Find(B(row["unshaded"]) ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
                mat.name=shader+"_"+i.ToString("D5"); mat.enableInstancing=true;
                Color albedo=ColorOf(row["color"],Color.gray);
                float rough=F(row["roughness"],.8f), metal=F(row["metallic"]);
                if(shader=="asphalt") { albedo=new Color(.105f,.113f,.124f); rough=.82f; }
                if(shader=="ground") albedo=new Color(.31f,.38f,.17f);
                if(shader=="sand") albedo=new Color(.7f,.64f,.42f);
                if(shader=="ocean") {albedo=new Color(.025f,.22f,.3f);rough=.17f;metal=.12f;}
                if(shader=="foliage_wind") albedo=ColorOf(row["parameters"]?["leaf_color"],new Color(.12f,.3f,.1f));
                if(shader=="facade") albedo=ColorOf(row["parameters"]?["plaster"],new Color(.7f,.66f,.57f));
                if(shader=="cockpit_mirror") {mat.shader=Shader.Find("Universal Render Pipeline/Unlit");albedo=Color.black;}
                mat.SetColor("_BaseColor",albedo);
                mat.SetFloat("_Smoothness",1-rough); mat.SetFloat("_Metallic",metal);
                string texture=(string)row["texture"];
                if(!string.IsNullOrEmpty(texture)) mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+texture));
                string normal=(string)row["normal"];
                if(!string.IsNullOrEmpty(normal)) {
                    var ti=(TextureImporter)AssetImporter.GetAtPath(Source+normal);
                    if(ti.textureType!=TextureImporterType.NormalMap){ti.textureType=TextureImporterType.NormalMap;ti.SaveAndReimport();}
                    mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+normal));mat.EnableKeyword("_NORMALMAP");
                }
                Color emission=ColorOf(row["emission"],Color.black);
                if(emission.maxColorComponent>0){mat.SetColor("_EmissionColor",emission);mat.EnableKeyword("_EMISSION");}
                if(B(row["transparent"])) {
                    // Alpha clipping preserves opaque gauge faces and clean round silhouettes.
                    mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.15f);mat.EnableKeyword("_ALPHATEST_ON");mat.renderQueue=2450;
                }
                if(B(row["doubleSided"])) mat.SetFloat("_Cull",0);
                if(fresh) AssetDatabase.CreateAsset(mat,assetPath);else EditorUtility.SetDirty(mat);
                materials[i]=mat;
            }
        }
        static Mesh ReadMesh(string relative)
        {
            if(Meshes.TryGetValue(relative,out Mesh found)) return found;
            string assetPath=Imported+"Meshes/"+Path.GetFileNameWithoutExtension(relative)+".asset";
            found=AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if(found){Meshes[relative]=found;return found;}
            using(var file=new BinaryReader(File.OpenRead(Source+relative))) {
                int nv=file.ReadInt32(),nn=file.ReadInt32(),nu=file.ReadInt32(),nc=file.ReadInt32(),ni=file.ReadInt32();
                Vector3[] vertices=new Vector3[nv],normals=new Vector3[nn];
                Vector2[] uv=new Vector2[nu];Color[] colors=new Color[nc];
                for(int i=0;i<nv;i++)vertices[i]=new Vector3(-file.ReadSingle(),file.ReadSingle(),file.ReadSingle());
                for(int i=0;i<nn;i++)normals[i]=new Vector3(-file.ReadSingle(),file.ReadSingle(),file.ReadSingle());
                for(int i=0;i<nu;i++)uv[i]=new Vector2(file.ReadSingle(),1-file.ReadSingle());
                for(int i=0;i<nc;i++)colors[i]=new Color(file.ReadSingle(),file.ReadSingle(),file.ReadSingle(),file.ReadSingle());
                int[] triangles=new int[ni>0?ni:nv];
                for(int i=0;i<triangles.Length;i++)triangles[i]=ni>0?file.ReadInt32():i;
                for(int i=0;i+2<triangles.Length;i+=3) {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    if(nn==nv && Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normals[a])<0)
                        {triangles[i+1]=c;triangles[i+2]=b;}
                }
                found=new Mesh{name=Path.GetFileNameWithoutExtension(relative),indexFormat=IndexFormat.UInt32};
                found.vertices=vertices;found.triangles=triangles;
                if(nn==nv)found.normals=normals;else found.RecalculateNormals();
                if(nu==nv)found.uv=uv;if(nc==nv)found.colors=colors;
                if(nu==nv)found.RecalculateTangents();found.RecalculateBounds();
                AssetDatabase.CreateAsset(found,assetPath);Meshes[relative]=found;return found;
            }
        }
        static Mesh CollisionMesh(JToken shape)
        {
            string relative=(string)shape["file"], assetPath=Imported+"Meshes/"+Path.GetFileNameWithoutExtension(relative)+".asset";
            Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            using(var file=new BinaryReader(File.OpenRead(Source+relative))) {
                int count=file.ReadInt32();var vertices=new Vector3[count];
                for(int i=0;i<count;i++)vertices[i]=new Vector3(-file.ReadSingle(),file.ReadSingle(),file.ReadSingle());
                var indices=new List<int>();
                if(B(shape["convex"])) {
                    if(ConvexHull.Build(vertices,out Vector3[] hull,out int[] hullIndices)){vertices=hull;indices.AddRange(hullIndices);}
                    else { // Flat source shapes need a small physical thickness.
                        var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var point in vertices)bounds.Encapsulate(point);
                        Vector3 e=Vector3.Max(bounds.extents,Vector3.one*.01f),c=bounds.center;
                        vertices=new[]{c+new Vector3(-e.x,-e.y,-e.z),c+new Vector3(e.x,-e.y,-e.z),c+new Vector3(e.x,e.y,-e.z),c+new Vector3(-e.x,e.y,-e.z),c+new Vector3(-e.x,-e.y,e.z),c+new Vector3(e.x,-e.y,e.z),c+new Vector3(e.x,e.y,e.z),c+new Vector3(-e.x,e.y,e.z)};
                        indices.AddRange(new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,2,3,7,2,7,6,1,2,6,1,6,5,3,0,4,3,4,7});
                    }
                }
                else for(int i=0;i+2<count;i+=3)
                    if(Vector3.Cross(vertices[i+1]-vertices[i],vertices[i+2]-vertices[i]).sqrMagnitude>.00000001f)indices.AddRange(new[]{i,i+1,i+2});
                bool fresh=mesh==null;if(fresh)mesh=new Mesh{name=relative,indexFormat=IndexFormat.UInt32};else mesh.Clear();
                mesh.vertices=vertices;mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                if(fresh)AssetDatabase.CreateAsset(mesh,assetPath);else EditorUtility.SetDirty(mesh);return mesh;
            }
        }
        static void AddShape(GameObject go, JToken shape)
        {
            switch((string)shape["kind"]) {
                case "box": var box=go.AddComponent<BoxCollider>();box.size=V(shape["size"]);box.size=new Vector3(Mathf.Abs(box.size.x),box.size.y,box.size.z);break;
                case "sphere": go.AddComponent<SphereCollider>().radius=F(shape["radius"]);break;
                case "capsule": var capsule=go.AddComponent<CapsuleCollider>();capsule.radius=F(shape["radius"]);capsule.height=F(shape["height"]);break;
                case "mesh": var mc=go.AddComponent<MeshCollider>();mc.sharedMesh=CollisionMesh(shape);mc.convex=B(shape["convex"]);break;
                case "unsupported":
                    if((string)shape["type"]!="WorldBoundaryShape3D")throw new InvalidDataException("Unsupported source collider: "+shape["type"]);
                    var floor=go.AddComponent<BoxCollider>();floor.size=new Vector3(10000,2,10000);floor.center=new Vector3(0,-1,0);break;
            }
        }
        static void AddSurfaces(GameObject go,JToken row)
        {
            foreach(JToken part in row["surfaces"]) {
                var surface=new GameObject("Surface");surface.transform.SetParent(go.transform,false);surface.layer=go.layer;
                surface.AddComponent<MeshFilter>().sharedMesh=ReadMesh((string)part["mesh"]);
                var renderer=surface.AddComponent<MeshRenderer>();int mat=(int)part["material"];
                renderer.sharedMaterial=mat>=0?materials[mat]:materials[0];
            }
        }
        static void ImportScene(string id)
        {
            JObject data=JObject.Parse(File.ReadAllText(Source+id+".json"));
            var rows=(JArray)data["nodes"];var objects=new GameObject[rows.Count];bool vehicle=data["ratios"]!=null,traffic=B(data["traffic"]);
            for(int i=0;i<rows.Count;i++) {
                JToken row=rows[i];var go=new GameObject((string)row["name"]);objects[i]=go;
                int parent=(int)row["parent"];if(parent>=0)go.transform.SetParent(objects[parent].transform,false);
                SetTransform(go.transform,row["transform"]);
                go.layer=(int)row["layer"]==2?8:0;
                if(row["instances"] is JArray instances) foreach(var transform in instances) {
                    var instance=new GameObject("Instance");instance.transform.SetParent(go.transform,false);SetTransform(instance.transform,transform);AddSurfaces(instance,row);
                } else AddSurfaces(go,row);
                if(!string.IsNullOrEmpty((string)row["text"]) || (string)row["kind"]=="Label3D") {
                    var text=go.AddComponent<TextMesh>();text.text=(string)row["text"];text.fontSize=64;text.characterSize=F(row["textHeight"],.02f)/6.4f;
                    text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=ColorOf(row["textColor"],Color.white);
                    text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    go.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
                    go.transform.localRotation*=Quaternion.Euler(0,180,0);
                }
                if(row["shape"]?["kind"]!=null)AddShape(go,row["shape"]);
                string kind=(string)row["kind"];
                if(kind=="OmniLight3D"||kind=="SpotLight3D"||kind=="DirectionalLight3D") {
                    var light=go.AddComponent<Light>();light.type=kind=="OmniLight3D"?LightType.Point:kind=="SpotLight3D"?LightType.Spot:LightType.Directional;
                    light.color=ColorOf(row["lightColor"],Color.white);light.intensity=F(row["lightEnergy"],1);
                    light.range=F(row["range"],20);light.spotAngle=F(row["angle"],45);
                    go.transform.localRotation*=Quaternion.Euler(0,180,0);
                    light.shadows=light.type==LightType.Directional?LightShadows.Soft:LightShadows.None;
                    if(vehicle && light.type==LightType.Point)light.cullingMask=1<<8;
                }
                if(row["mass"]!=null) {
                    var body=go.AddComponent<Rigidbody>();body.mass=F(row["mass"],1000);
                    body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                    body.centerOfMass=new Vector3(0,-.2f,0);
                }
                go.SetActive(B(row["visible"]));
            }
            GameObject root=objects[0];root.name=id;root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            if(vehicle)ConfigureCar(root,data,rows,objects,id);
            else if(!traffic) {
                var track=root.AddComponent<TrackDefinition>();track.id=id;
                track.route=((JArray)data["route"]).Select(V).ToArray();track.roadHalfWidth=F(data["roadHalfWidth"],7);
                track.fuelStation=V(data["fuelStation"]);track.speedTrap=V(data["speedTrap"]);
                var pose=new GameObject("Spawn");pose.transform.SetParent(root.transform,false);SetTransform(pose.transform,data["spawn"]);
                track.spawnPosition=pose.transform.localPosition;track.spawnRotation=pose.transform.localRotation;
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic);
            }
            string destination="Assets/Resources/"+(vehicle||traffic?"Vehicles/":"Worlds/")+id+".prefab";
            PrefabUtility.SaveAsPrefabAsset(root,destination);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("[IMPORTED] "+id+" nodes="+rows.Count);
        }
        static void ConfigureCar(GameObject root,JObject data,JArray rows,GameObject[] objects,string id)
        {
            var car=root.AddComponent<VehicleDefinition>();car.id=id;
            car.mass=F(data["mass"]);car.torque=F(data["torque"]);car.idleRPM=F(data["idleRPM"]);car.redlineRPM=F(data["redlineRPM"]);
            car.finalDrive=F(data["finalDrive"]);car.tireRadius=F(data["tireRadius"]);car.ratios=((JArray)data["ratios"]).Select(x=>(float)x).ToArray();
            var wheels=new List<WheelCollider>();var visuals=new List<Transform>();var driven=new List<bool>();
            for(int i=0;i<rows.Count;i++) {
                JToken row=rows[i];GameObject go=objects[i];
                if(row["wheel"]!=null) {
                    var visual=new GameObject("WheelVisual");visual.transform.SetParent(go.transform,false);
                    foreach(Transform child in go.transform.Cast<Transform>().ToArray())if(child!=visual.transform)child.SetParent(visual.transform,false);
                    var wc=go.AddComponent<WheelCollider>();wc.radius=F(row["wheel"]["radius"],.33f);wc.suspensionDistance=.22f;
                    wc.suspensionSpring=new JointSpring{spring=F(row["wheel"]["stiffness"],45)*1000,damper=4500,targetPosition=.5f};
                    wc.mass=24;wc.wheelDampingRate=.8f;wc.forceAppPointDistance=.15f;
                    wc.ConfigureVehicleSubsteps(8,8,12);
                    wheels.Add(wc);visuals.Add(visual.transform);driven.Add(B(row["wheel"]["traction"]));
                }
                switch(go.name) {
                    case "CockpitInterior":car.cockpit=go.transform;break;
                    case "ModelInstance":car.exterior=go.transform;break;
                    case "SteeringWheel":car.steeringWheel=go.transform;break;
                    case "SpeedNeedle":car.speedNeedle=go.transform;break;
                    case "RPMNeedle":car.rpmNeedle=go.transform;break;
                    case "DigitalSpeed":car.digitalSpeed=go.GetComponent<TextMesh>();break;
                    case "DigitalGear":car.digitalGear=go.GetComponent<TextMesh>();break;
                    case "RearviewSurface":car.mirror=go.GetComponentInChildren<Renderer>();break;
                }
            }
            car.wheels=wheels.ToArray();car.wheelVisuals=visuals.ToArray();car.drivenWheels=driven.ToArray();
        }
    }
}
