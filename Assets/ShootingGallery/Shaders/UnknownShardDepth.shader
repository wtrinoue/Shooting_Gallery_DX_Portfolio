// Unknown ステージの割れたガラス (UnknownShardSolid.shader) のすぐ後に描く、色を出さない膜 (UnknownStage)。
// ガラスの破片が描かれた所 (ステンシルの印 128) だけ、奥行きを一番奥に戻す。
// こうすると、後から描く屋台・地面の立体の破片 (WorldShatter。描く順番 Geometry+460) は、
// ガラスより奥にあっても、ガラスの前に見える (ガラスの奥のふしぎな空間には隠されない)。
Shader "ShootingGallery/Unknown Shard Depth"
{
    SubShader
    {
        Tags { "Queue"="Geometry+451" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Lighting Off
        Cull Off
        ZWrite On
        ZTest Always
        ColorMask 0
        Stencil
        {
            Ref 128
            ReadMask 128
            Comp Equal
        }

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
                // 一番奥 (見える距離の果てのすぐ手前) の奥行きにする
                #if defined(UNITY_REVERSED_Z)
                o.vertex.z = o.vertex.w * 1e-6;
                #else
                o.vertex.z = o.vertex.w * (1 - 1e-6);
                #endif
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
