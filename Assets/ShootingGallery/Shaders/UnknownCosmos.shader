// Unknown ステージの「雲海の上の星空」(大きなワープホールの向こうの世界) 用のシェーダー (UnknownStage)。
// 星空は本物の遠い場所に置いたまま、大きなホールの輪を通して見える所だけを描く (輪がこちらへ来て、くぐると背景が入れ替わる)。
// 目からその点への線が輪の円の中を通るかで決める。ステンシルは使わない。
//   _CosmosPortal (グローバル)    : xyz = 輪の真ん中 (ワールド), w = 輪の半径
//   _CosmosPortalDir (グローバル) : xyz = 輪の向き (くぐる前のプレイヤーから見て奥), w = 0 描かない / 1 輪の中だけ / 2 輪の中以外 / 3 全部
// 手前の物 (床・飾り) は普通に前に描かれる。霧はかけない。_Shade で上から光が当たったような明暗を少しつける (雲・岩)。
// _UsePortal = 0 なら、輪で切らずにいつも描く (ホールの輪の枠。背景の一部として、床・飾りより先に奥行きを書かずに描くので、物に食い込んで見えない)。
Shader "ShootingGallery/Unknown Cosmos"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Shade ("Shade", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
        _UsePortal ("Use Portal", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" }
        Lighting Off
        Cull [_Cull]
        ZWrite [_ZWrite]
        Blend [_SrcBlend] [_DstBlend]

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
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 cam : TEXCOORD1;
                float light : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _CosmosPortal, _CosmosPortalDir;
            float4 _Color;
            float _Shade, _UsePortal;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.cam = _WorldSpaceCameraPos; // (両目で別々の目の位置)
                float up = UnityObjectToWorldNormal(v.normal).y;
                o.light = lerp(1, 0.6 + 0.4 * saturate(up * 0.5 + 0.5), _Shade);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float mode = _CosmosPortalDir.w;
                if (_UsePortal > 0.5 && mode < 2.5)
                {
                    float3 ray = i.world - i.cam;
                    float denom = dot(_CosmosPortalDir.xyz, ray);
                    float t = abs(denom) > 1e-5 ? dot(_CosmosPortalDir.xyz, _CosmosPortal.xyz - i.cam) / denom : -1;
                    float3 hit = i.cam + ray * t;
                    float through = (t > 0 && t < 1 && distance(hit, _CosmosPortal.xyz) < _CosmosPortal.w) ? 1 : 0;
                    float show = mode < 0.5 ? 0 : (mode < 1.5 ? through : 1 - through);
                    clip(show - 0.5);
                }
                return float4(_Color.rgb * i.light, _Color.a);
            }
            ENDCG
        }
    }
}
