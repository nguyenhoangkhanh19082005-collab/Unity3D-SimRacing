Shader "SimRacing/Wheel Smoke"
{
    // Keyword-free alpha-blended particle shader. Materials created at runtime with the URP particle shader
    // lost their transparent variant in player builds and rendered as opaque white squares.
    Properties { _BaseMap("Texture",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1) }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;half4 _BaseColor;
            CBUFFER_END
            struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;half fog:TEXCOORD1;};
            V vert(A v){V o;o.position=TransformObjectToHClip(v.vertex.xyz);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.color=v.color*_BaseColor;o.fog=ComputeFogFactor(o.position.z);return o;}
            half4 frag(V v):SV_Target {
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv)*v.color;
                c.rgb=MixFog(c.rgb,v.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
