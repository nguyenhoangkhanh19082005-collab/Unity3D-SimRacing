using System.Collections.Generic;
using UnityEngine;
namespace SimRacing.Editor
{
    // A bounded support-point hull keeps scenery within PhysX's 255-face limit.
    // It preserves extreme points instead of treating a triangle fan as a closed hull.
    static class ConvexHull
    {
        struct Face {public int a,b,c;public Face(int a,int b,int c){this.a=a;this.b=b;this.c=c;}}
        struct Edge {public int a,b;public Edge(int a,int b){this.a=a;this.b=b;}}
        public static bool Build(Vector3[] source,out Vector3[] vertices,out int[] triangles)
        {
            var points=new List<Vector3>();
            for(int i=0;i<80;i++) {
                float y=1-2*(i+.5f)/80,r=Mathf.Sqrt(1-y*y),phi=i*2.399963f;var direction=new Vector3(Mathf.Cos(phi)*r,y,Mathf.Sin(phi)*r);
                int best=0;float projection=float.MinValue;
                for(int j=0;j<source.Length;j++){float support=Vector3.Dot(source[j],direction);if(support>projection){projection=support;best=j;}}
                Vector3 candidate=source[best];if(!points.Exists(p=>(p-candidate).sqrMagnitude<1e-9f))points.Add(candidate);
            }
            vertices=points.ToArray();triangles=System.Array.Empty<int>();if(points.Count<4)return false;
            int a=0,b=1,c=2,d=3;float longest=0;
            for(int i=1;i<points.Count;i++){float length=(points[i]-points[a]).sqrMagnitude;if(length>longest){longest=length;b=i;}}
            float area=0;for(int i=0;i<points.Count;i++){float next=Vector3.Cross(points[b]-points[a],points[i]-points[a]).sqrMagnitude;if(next>area){area=next;c=i;}}
            Vector3 normal=Vector3.Cross(points[b]-points[a],points[c]-points[a]).normalized;float height=0;
            for(int i=0;i<points.Count;i++){float next=Mathf.Abs(Vector3.Dot(normal,points[i]-points[a]));if(next>height){height=next;d=i;}}
            if(height<.0001f)return false;
            Vector3 inside=(points[a]+points[b]+points[c]+points[d])/4;
            var faces=new List<Face>();Add(faces,points,inside,a,b,c);Add(faces,points,inside,a,d,b);Add(faces,points,inside,a,c,d);Add(faces,points,inside,b,d,c);
            for(int i=0;i<points.Count;i++) {
                if(i==a||i==b||i==c||i==d)continue;
                var edges=new Dictionary<long,Edge>();var counts=new Dictionary<long,int>();
                for(int f=faces.Count-1;f>=0;f--) {
                    var face=faces[f];var n=Vector3.Cross(points[face.b]-points[face.a],points[face.c]-points[face.a]).normalized;
                    if(Vector3.Dot(n,points[i]-points[face.a])<=.00001f)continue;
                    foreach(var e in new[]{new Edge(face.a,face.b),new Edge(face.b,face.c),new Edge(face.c,face.a)}) {long key=((long)Mathf.Min(e.a,e.b)<<32)|(uint)Mathf.Max(e.a,e.b);edges[key]=e;counts[key]=counts.ContainsKey(key)?counts[key]+1:1;}
                    faces.RemoveAt(f);
                }
                foreach(var pair in edges)if(counts[pair.Key]==1)Add(faces,points,inside,pair.Value.a,pair.Value.b,i);
            }
            var indices=new List<int>();foreach(var f in faces)indices.AddRange(new[]{f.a,f.b,f.c});triangles=indices.ToArray();return triangles.Length>=12;
        }
        static void Add(List<Face> faces,List<Vector3> p,Vector3 inside,int a,int b,int c)
        {
            if(Vector3.Dot(Vector3.Cross(p[b]-p[a],p[c]-p[a]),inside-p[a])>0){int temp=b;b=c;c=temp;}faces.Add(new Face(a,b,c));
        }
    }
}
