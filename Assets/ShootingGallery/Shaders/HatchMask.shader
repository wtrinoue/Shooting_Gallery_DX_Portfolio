// 屋台の足元のハッチ (StallHatch) の穴にかぶせる、色を出さない板。
// 地面 (Ground。描く順番 Geometry+20) より先に、床の高さの奥行きだけを書く。
// こうすると地面はここで描かれず (奥行きで負ける)、先に描いた穴の中の壁や、地下で待っている屋台が見える
// (地面に本当に穴を空けなくても、穴が空いて見える)。
Shader "ShootingGallery/Hatch Mask"
{
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Lighting Off
        Cull Back
        ZWrite On
        ZTest LEqual
        ColorMask 0

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
