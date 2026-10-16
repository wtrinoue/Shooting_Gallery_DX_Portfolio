// Unknown ステージの「空中のひび割れガラス」用のシェーダー (UnknownStage)。
// プレイヤーを包む透明な球を割った破片に使う。ほとんど透明なガラス (_GlassColor の a) に、ひびの線 (_Crack) を描く。
// 後ろに見えるのは本物の立体の世界 (灰色の部品) なので、写真は使わない (奥行き・両目の立体感がそのまま)。
// ひびの線: 頂点の色の a = 破片のふちで 1、真ん中で 0。_Crack が大きいほど太い線。_CrackGlowAmount で光らせる。
// _Fade で全体を消していく。後処理は使わない (VR・WebGL で軽く)。両面を描く。
Shader "ShootingGallery/Unknown Shard"
{
    Properties
    {
        _GlassColor ("Glass Color (a = 透け具合)", Color) = (0.85, 0.9, 1, 0.06)
        _Crack ("Crack", Range(0, 1)) = 0
        _CrackColor ("Crack Color", Color) = (0.92, 0.95, 1, 1)
        _CrackGlow ("Crack Glow", Color) = (1, 0.97, 0.85, 1)
        _CrackGlowAmount ("Crack Glow Amount", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Lighting Off
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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

            float _Crack, _CrackGlowAmount, _Fade;
            fixed4 _GlassColor, _CrackColor, _CrackGlow;

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
                float width = 0.05 * _Crack; // (細めの線。広すぎると白っぽく見えるので)
                float crackLine = _Crack > 0.001 ? smoothstep(1 - width, 1 - width * 0.4, i.edge) : 0;
                float3 crack = lerp(_CrackColor.rgb, _CrackGlow.rgb * 1.6, _CrackGlowAmount);
                float3 col = lerp(_GlassColor.rgb, crack, crackLine);
                float alpha = lerp(_GlassColor.a, 1, crackLine) * _Fade;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
