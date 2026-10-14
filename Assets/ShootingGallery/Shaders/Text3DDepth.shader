// 3D の文字 (TextMesh) 用のシェーダー。Unity 標準の文字のシェーダー (GUI/Text Shader) は手前に物があっても透けて描かれるので、
// 奥行きを判定して (ZTest LEqual)、手前の物に隠れるようにしたもの。描く手間は標準と同じ。
// 文字は 3cm (_DepthBias) だけ手前にあるものとして判定する (板の表面に埋まって消えないように)。
// 霧 (夜・水の中) もかかる。VR (シングルパス / インスタンシング) でも両目に描かれる。
// HUD・点数のポップアップなど、いつも見えてほしい文字は標準のシェーダーのまま (GalleryBuilder.MakeText の overlay)。
Shader "ShootingGallery/Text 3D (Depth Tested)"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
        _DepthBias ("Depth Bias (m)", Float) = 0.03
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Lighting Off
        Cull Off
        ZTest LEqual
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _DepthBias;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // 文字は板や的の表面のすぐ前 (数 mm) に置いてあり、板の厚みや重なった部品に埋まって隠れることがあるので、
                // 視線に沿って _DepthBias (m) だけカメラ寄りに描く (画面上の位置と大きさは変わらない)
                float3 viewPos = UnityObjectToViewPos(v.vertex);
                float dist = length(viewPos);
                viewPos *= max(dist - _DepthBias, dist * 0.5) / max(dist, 1e-4);
                o.vertex = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));
                o.color = v.color * _Color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = i.color;
                col.a *= tex2D(_MainTex, i.texcoord).a; // 文字の形はフォントの画像のアルファ
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
