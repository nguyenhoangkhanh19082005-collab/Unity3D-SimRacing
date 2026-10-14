Shader "SimRacing/Atmosphere"
{
    Properties { _Night("Night",Range(0,1))=0 _Cloudiness("Cloudiness",Range(0,1))=.35 }
    SubShader {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"}
        Cull Off ZWrite Off
        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Night,_Cloudiness;
            struct V {float4 position:SV_POSITION;float3 direction:TEXCOORD0;};
            V vert(float4 vertex:POSITION){V o;o.position=TransformObjectToHClip(vertex.xyz);o.direction=TransformObjectToWorldDir(vertex.xyz);return o;}
            float hash(float2 p){p=frac(p*float2(123.34,456.21));p+=dot(p,p+45.32);return frac(p.x*p.y);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            half4 frag(V v):SV_Target {
                float3 d=normalize(v.direction);float elevation=saturate(d.y);float3 top=lerp(float3(.17,.39,.69),float3(.003,.008,.024),_Night),horizon=lerp(float3(.7,.8,.87),float3(.023,.04,.075),_Night);
                float3 sky=lerp(horizon,top,pow(elevation,.43));
                float2 p=d.xz/max(.18,d.y+.16)*2.2+_Time.y*float2(.004,.002);float n=noise(p)*.55+noise(p*2.1)*.27+noise(p*4.3)*.13+noise(p*8.2)*.05;
                float clouds=smoothstep(.6-_Cloudiness*.24,.78-_Cloudiness*.18,n)*saturate(d.y*6);
                sky=lerp(sky,lerp(float3(.92,.94,.97),float3(.045,.06,.085),_Night),clouds);
                float sun=pow(saturate(dot(d,normalize(float3(.45,.58,-.68)))),1300);sky+=sun*lerp(float3(6,4.8,3),float3(.35,.45,.7),_Night);
                float stars=step(.9988,hash(floor(d.xz/(abs(d.y)+.1)*850)))*saturate(d.y*4)*_Night*(1-clouds);sky+=stars*.3;
                sky=lerp(lerp(float3(.32,.34,.31),float3(.012,.018,.026),_Night),sky,smoothstep(-.08,.015,d.y));
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
