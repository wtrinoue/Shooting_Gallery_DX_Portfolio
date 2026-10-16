// Unknown ステージの「空中のひび割れガラス」が割れた後の見た目 (UnknownStage)。
// 割れた瞬間から、ガラスの破片は透けずに「灰色に止まった世界の空」(_SkyColor) の色になる。ひびの線はそのまま。
// 破片が飛んで抜けた所にだけ、ふしぎな空間が見える (灰色の景色のまま割れて見える)。
// 屋台・地面の立体の破片 (WorldShatter) は、この後に描いて手前に見せる:
//   ここで破片の所にステンシルの印 (128) をつけ、UnknownShardDepth.shader がその所の奥行きを一番奥に戻す。
// 銃・手など、ガラスより手前の物は奥行きのテストで残る (そこには印がつかない)。HUD など半透明の物は後で描かれる。
Shader "ShootingGallery/Unknown Shard Solid"
{
    Properties
    {
        _SkyColor ("Sky Color", Color) = (0.1, 0.1, 0.12, 1)
        _Crack ("Crack", Range(0, 1)) = 1
        _CrackColor ("Crack Color", Color) = (0.92, 0.95, 1, 1)
        _CrackGlow ("Crack Glow", Color) = (1, 0.97, 0.85, 1)
        _CrackGlowAmount ("Crack Glow Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Geometry+450" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Lighting Off
        Cull Off
        ZWrite Off
        ZTest LEqual
        Stencil
        {
            Ref 128
            WriteMask 128
            Comp Always
            Pass Replace
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
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float edge : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _Crack, _CrackGlowAmount;
            fixed4 _SkyColor, _CrackColor, _CrackGlow;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.edge = v.color.a;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float width = 0.05 * _Crack;
                float crackLine = _Crack > 0.001 ? smoothstep(1 - width, 1 - width * 0.4, i.edge) : 0;
                float3 crack = lerp(_CrackColor.rgb, _CrackGlow.rgb * 1.6, _CrackGlowAmount);
                return fixed4(lerp(_SkyColor.rgb, crack, crackLine), 1);
            }
            ENDCG
        }
    }
}
