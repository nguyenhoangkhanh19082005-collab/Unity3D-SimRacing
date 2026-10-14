Shader "SimRacing/World Surface"
{
    Properties {
        _BaseColor("Surface color",Color)=(.1,.11,.12,1)
        _Kind("0 asphalt 1 water 2 sand 3 ground",Float)=0
        _Smoothness("Smoothness",Range(0,1))=.18
        _Cull("Cull",Float)=2
    }
    SubShader {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
        Cull [_Cull]
        Pass {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;float _Kind;half _Smoothness;
            CBUFFER_END
            float _SimWetness;
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half fog:TEXCOORD2;float3 localPosition:TEXCOORD3;half3 localNormal:TEXCOORD4;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            Varyings vert(Attributes v) {
                Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.localPosition=v.positionOS.xyz;o.localNormal=v.normalOS;o.color=v.color;
                if(_Kind>.5 && _Kind<1.5)o.positionWS.y+=sin(o.positionWS.x*.11+_Time.y*.8)*.055;
                if(_Kind>4.5){float phase=o.positionWS.x*.35+o.positionWS.z*.27;float sway=sin(_Time.y*1.1+phase)*.025+sin(_Time.y*2.4+phase*1.7)*.008;o.positionWS.xz+=float2(sway,sway*.45);}
                o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            float noise(float2 p){p=frac(p*float2(123.34,456.21));p+=dot(p,p+45.32);return frac(p.x*p.y);}
            float smoothNoise(float2 p){float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(noise(cell),noise(cell+float2(1,0)),f.x),lerp(noise(cell+float2(0,1)),noise(cell+1),f.x),f.y);}
            half4 frag(Varyings v,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target {
                UNITY_SETUP_INSTANCE_ID(v);float2 p=v.positionWS.xz;half3 color=_BaseColor.rgb;half smooth=_Smoothness;half3 normal=normalize(v.normalWS);
                if(_Kind<.5) {
                    float grain=noise(floor(p*90));float visibility=saturate(1-length(fwidth(p*90))*.35);
                    color*=1+(grain-.5)*.26*visibility;
                    float patches=smoothNoise(p*.75);color*=.985+patches*.03;
                    float wet=saturate(_SimWetness*(.55+smoothNoise(p*.12)*.45));color*=lerp(1,.64,wet);smooth=lerp(.18,.8,wet);
                } else if(_Kind<1.5) {
                    float t=_Time.y;float a=sin(p.x*.53+p.y*.31+t*1.5),b=cos(p.x*.24-p.y*.68-t*.9);
                    normal=normalize(half3(a*.09,1,b*.085));float fresnel=pow(1-saturate(dot(normal,GetWorldSpaceNormalizeViewDir(v.positionWS))),4);
                    color=lerp(half3(.015,.15,.19),half3(.11,.33,.38),fresnel);color+=pow(saturate(a*b),14)*.06;smooth=.93;
                } else if(_Kind<2.5) {
                    float ripple=sin(p.x*8+sin(p.y*1.3)*1.8);color*=.96+ripple*.025+(noise(floor(p*55))-.5)*.08;
                } else if(_Kind<3.5){color*=.88+noise(floor(p*.25))*.24;}
                else if(_Kind<4.5) {
                    float horizontal=abs(v.localNormal.x)>abs(v.localNormal.z)?v.localPosition.z:v.localPosition.x;
                    float2 cell=frac(float2(horizontal/2.5,v.localPosition.y/2.8));
                    float panel=step(.18,cell.x)*step(cell.x,.82)*step(.33,cell.y)*step(cell.y,.86)*(1-step(.6,abs(v.localNormal.y)));
                    float glass=step(.22,cell.x)*step(cell.x,.78)*step(.37,cell.y)*step(cell.y,.82)*panel*smoothstep(.01,.025,abs(cell.x-.5));
                    color=lerp(color*(.96+.04*sin(v.localPosition.y*91)*sin(horizontal*79)),half3(.12,.115,.095),panel);color=lerp(color,half3(.065,.135,.17),glass);smooth=lerp(.1,.8,glass);
                } else {color*=v.color.rgb;normal*=IS_FRONT_VFACE(face,1,-1);}
                InputData input=(InputData)0;input.positionWS=v.positionWS;input.normalWS=normal;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(v.positionWS);input.bakedGI=SampleSH(normal);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);input.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;surface.albedo=color;surface.metallic=_Kind>.5&&_Kind<1.5?.12:.015;surface.smoothness=smooth;surface.normalTS=half3(0,0,1);surface.occlusion=1;surface.alpha=1;
                half4 result=UniversalFragmentPBR(input,surface);result.rgb=MixFog(result.rgb,v.fog);return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
