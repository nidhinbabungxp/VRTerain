Shader "CarInterior/StudioSky"
{
    Properties
    {
        _Exposure("Reflection brightness",Range(0,3))=1
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Exposure;
            struct appdata {float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;
            }
            half4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.direction);
                float sky=smoothstep(-.30,.85,d.y);
                float3 c=lerp(float3(.024,.027,.035),float3(.22,.27,.35),sky);
                // Broad luminous studio windows make readable, soft metallic reflections.
                float window=smoothstep(.78,.86,abs(d.x))*smoothstep(-.02,.16,d.y)*(1-smoothstep(.60,.76,d.y));
                c+=float3(1.8,2.0,2.3)*window;
                float ceiling=smoothstep(.93,.985,d.y);
                c+=float3(1.1,1.2,1.35)*ceiling;
                return half4(c*_Exposure,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
