Shader "SolidColor/PointCloud"
{

    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Texture", 2D) = "white" {}
    }

        SubShader
    {

    // Tags { "RenderType" = "Opaque" }
        Tags {"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
            ZWrite Off
    Blend SrcAlpha OneMinusSrcAlpha


    CGINCLUDE

    #include "UnityCG.cginc"

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        UNITY_FOG_COORDS(1)
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    sampler2D _MainTex;
    float4 _MainTex_ST;

    UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
    UNITY_INSTANCING_BUFFER_END(Props)

    v2f vert(appdata v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        UNITY_TRANSFER_INSTANCE_ID(v, o);
        UNITY_TRANSFER_FOG(o,o.vertex);
        return o;
    }

    // UNITY_INSTANCING_BUFFER_START(Props)
    // UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
    // UNITY_INSTANCING_BUFFER_END(Props)

    float4 _Color;

    fixed4 frag(v2f i) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(i);
        fixed4 c = tex2D(_MainTex, i.uv) * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

        // fixed4 col = tex2D(_MainTex, i.uv) * _Color;
        // col *= UNITY_ACCESS_INSTANCED_PROP(Props, _Color)
        UNITY_APPLY_FOG(i.fogCoord, col);
        return c;
    }

    ENDCG

    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #pragma multi_compile_instancing // ’Ç‰Á
        ENDCG
    }

    }
}
