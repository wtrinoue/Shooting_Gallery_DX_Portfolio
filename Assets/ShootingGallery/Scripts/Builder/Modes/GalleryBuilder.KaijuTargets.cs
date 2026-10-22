using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、怪獣モードの的の建物 (色とりどりで、上に大きく点数)。
    /// オフィスビル・丸いタワー・電波塔・段々のビル・ツインタワーの5種類。どれも、炎の玉で割れて落ちるかけら (pieces) を積み上げて作る
    /// (かけらには当たり判定を付けず、建物全体を1つの箱の当たり判定で覆う)。
    /// </summary>
    public partial class GalleryBuilder
    {
        static readonly Color[] KaijuTargetColors =
        {
            new Color(0.95f, 0.2f, 0.2f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.15f), new Color(0.25f, 0.8f, 0.3f),
            new Color(0.15f, 0.8f, 0.9f), new Color(0.25f, 0.45f, 1f), new Color(0.65f, 0.3f, 0.95f), new Color(1f, 0.4f, 0.75f),
        };

        /// <summary>
        /// 的の建物。遠いほど高い点数 (一番最後の1棟は金色で 1000 点)。点数の看板はいつもプレイヤーの方を向く。
        /// 当たると割れて落ち、しばらくすると建て直される (KaijuBuilding)
        /// </summary>
        KaijuBuilding KaijuTargetBuilding(Transform parent, List<Transform> lots, Vector3 center, float dist, int index, bool gold, Material explosion, Material dust)
        {
            int points = gold ? 1000 : dist < 12f ? 100 : dist < 22f ? 200 : dist < 34f ? 300 : 500;
            Color c = gold ? new Color(1f, 0.78f, 0.25f) : KaijuTargetColors[index % KaijuTargetColors.Length];
            var body = Mat(gold ? "KaijuTarget_Gold" : $"KaijuTarget_{index % KaijuTargetColors.Length}", c, c * 0.3f, false, gold ? 0.8f : 0.45f);
            var accent = Mat("KaijuTarget_White", new Color(0.95f, 0.95f, 0.97f), new Color(0.2f, 0.2f, 0.22f), false, 0.4f);
            var glow = Mat("KaijuTarget_Window", new Color(1f, 0.95f, 0.8f), new Color(1.4f, 1.25f, 0.85f));

            var lot = CityLot(parent, lots, center);
            var root = Group(lot, $"TargetBuilding_{points}");
            var bodyRoot = Group(root, "Body");
            var pieces = new List<Transform>();
            float h = (Mathf.Lerp(3.6f, 6.5f, Mathf.Clamp01(dist / 45f)) + (gold ? 2f : 0f)) * CityHeight;
            int type = gold ? 0 : index % 5;
            Vector2 extent; // 当たり判定の幅・奥行き
            float top;
            switch (type)
            {
                case 1: top = KaijuRoundTower(bodyRoot, pieces, h * 1.15f, body, glow); extent = new Vector2(2.5f, 2.5f); break;
                case 2: top = KaijuRadioTower(bodyRoot, pieces, h * 1.6f, body, accent, glow); extent = new Vector2(2.6f, 2.6f); break;
                case 3: top = KaijuSteppedTower(bodyRoot, pieces, h * 1.1f, body, glow); extent = new Vector2(2.8f, 2.8f); break;
                case 4: top = KaijuTwinTowers(bodyRoot, pieces, h * 1.1f, body, glow); extent = new Vector2(2.9f, 1.7f); break;
                default: top = KaijuOfficeTower(bodyRoot, pieces, h, body, glow); extent = new Vector2(2.6f, 2.6f); break;
            }

            // 上の看板 (いつもプレイヤーの方を向く)。遠いほど大きな文字
            var sign = Group(bodyRoot, "Sign");
            sign.localPosition = new Vector3(0f, top + 1.1f, 0f);
            sign.gameObject.AddComponent<FaceCenter>();
            float size = 1f + dist * 0.03f;
            Prim(PrimitiveType.Cube, "SignBoard", sign, Vector3.zero, new Vector3(size * 2.2f, size * 0.9f, 0.05f), Mat("KaijuSign", new Color(0.08f, 0.08f, 0.1f), new Color(0.04f, 0.04f, 0.05f)), false);
            Prim(PrimitiveType.Cube, "SignFrame", sign, new Vector3(0f, 0f, 0.01f), new Vector3(size * 2.3f, size * 1f, 0.04f), body, false);
            MakeText(sign, "Points", points.ToString(), new Vector3(0f, 0f, -0.04f), size * 0.7f, gold ? c : Color.Lerp(c, Color.white, 0.25f));

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, top / 2f, 0f);
            box.size = new Vector3(extent.x, top, extent.y);

            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.KaijuBuilding;
            target.points = points;
            target.manualControl = true;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);
            var building = root.gameObject.AddComponent<KaijuBuilding>();
            building.target = target;
            building.body = bodyRoot;
            building.pieces = pieces.ToArray();
            building.sign = sign.gameObject;
            building.hitBox = box;
            building.size = new Vector3(extent.x, top, extent.y);
            building.explosionMaterial = explosion;
            building.dustMaterial = dust;
            return building;
        }

        // かけら1つ (真ん中が原点のグループ)
        static Transform KaijuPiece(Transform body, List<Transform> pieces, Vector3 center)
        {
            var p = Group(body, "Piece");
            p.localPosition = center;
            pieces.Add(p);
            return p;
        }

        // 階の数 (1かけらがだいたい 0.85m の高さになるように)
        static int Floors(float h, float each = 0.85f) => Mathf.Max(2, Mathf.RoundToInt(h / each));

        /// <summary>オフィスビル: 四角いビルを階ごと・左右に割ったかけらで積む。階ごとに光る窓の帯。屋上に機械室と給水塔</summary>
        float KaijuOfficeTower(Transform body, List<Transform> pieces, float h, Material wall, Material glow)
        {
            const float W = 2.6f, D = 2.6f;
            int n = Floors(h);
            float sh = h / n;
            for (int j = 0; j < n; j++)
                foreach (float s in new[] { -1f, 1f })
                {
                    var p = KaijuPiece(body, pieces, new Vector3(s * W / 4f, sh * (j + 0.5f), 0f));
                    Prim(PrimitiveType.Cube, "Wall", p, Vector3.zero, new Vector3(W / 2f - 0.02f, sh - 0.02f, D), wall, false);
                    Prim(PrimitiveType.Cube, "Windows", p, new Vector3(0f, 0.04f, 0f), new Vector3(W / 2f + 0.01f, sh * 0.32f, D + 0.03f), glow, false);
                }
            var roof = KaijuPiece(body, pieces, new Vector3(0f, h + 0.2f, 0f));
            Prim(PrimitiveType.Cube, "RoofRoom", roof, Vector3.zero, new Vector3(W * 0.5f, 0.4f, D * 0.5f), wall, false);
            Prim(PrimitiveType.Cylinder, "WaterTank", roof, new Vector3(W * 0.12f, 0.4f, 0f), new Vector3(0.4f, 0.2f, 0.4f), glow, false);
            return h + 0.6f;
        }

        /// <summary>丸いタワー: 円柱を階ごとのかけらで積む。光る窓の輪と、てっぺんの丸屋根とアンテナ</summary>
        float KaijuRoundTower(Transform body, List<Transform> pieces, float h, Material wall, Material glow)
        {
            const float R = 1.2f;
            int n = Floors(h, 0.75f);
            float sh = h / n;
            for (int j = 0; j < n; j++)
            {
                var p = KaijuPiece(body, pieces, new Vector3(0f, sh * (j + 0.5f), 0f));
                Prim(PrimitiveType.Cylinder, "Wall", p, Vector3.zero, new Vector3(2f * R, (sh - 0.02f) / 2f, 2f * R), wall, false);
                Prim(PrimitiveType.Cylinder, "Windows", p, new Vector3(0f, 0.04f, 0f), new Vector3(2f * R + 0.04f, sh * 0.16f, 2f * R + 0.04f), glow, false);
            }
            var dome = KaijuPiece(body, pieces, new Vector3(0f, h, 0f));
            Prim(PrimitiveType.Sphere, "Dome", dome, Vector3.zero, new Vector3(2f * R * 0.95f, R * 0.9f, 2f * R * 0.95f), wall, false);
            Prim(PrimitiveType.Cylinder, "Antenna", dome, new Vector3(0f, R * 0.75f, 0f), new Vector3(0.06f, 0.45f, 0.06f), glow, false);
            return h + R * 0.45f + 0.9f;
        }

        /// <summary>電波塔: 下が太く上へ細くなる段 (色と白が交互) と、途中の展望台 (光る窓の輪)、てっぺんの細いアンテナ</summary>
        float KaijuRadioTower(Transform body, List<Transform> pieces, float h, Material color, Material white, Material glow)
        {
            const int Tiers = 7;
            float deckAt = h * 0.62f;
            float y = 0f;
            for (int j = 0; j < Tiers; j++)
            {
                float k = j / (float)(Tiers - 1);
                float th = h * 0.86f / Tiers;
                float r = Mathf.Lerp(1.1f, 0.22f, k);
                var p = KaijuPiece(body, pieces, new Vector3(0f, y + th / 2f, 0f));
                Prim(PrimitiveType.Cylinder, "Tier", p, Vector3.zero, new Vector3(2f * r, th / 2f - 0.01f, 2f * r), j % 2 == 0 ? color : white, false);
                if (j == 0) // 足元の4本の脚
                    for (int l = 0; l < 4; l++)
                    {
                        var leg = Prim(PrimitiveType.Cube, "Leg", p, Quaternion.Euler(0f, 45f + l * 90f, 0f) * new Vector3(0f, -th * 0.1f, 1.15f), new Vector3(0.22f, th * 0.8f, 0.22f), color, false);
                        leg.transform.localRotation = Quaternion.Euler(0f, 45f + l * 90f, 0f) * Quaternion.Euler(-15f, 0f, 0f);
                    }
                y += th;
            }
            var deck = KaijuPiece(body, pieces, new Vector3(0f, deckAt, 0f));
            Prim(PrimitiveType.Cylinder, "Deck", deck, Vector3.zero, new Vector3(2.5f, 0.22f, 2.5f), white, false);
            Prim(PrimitiveType.Cylinder, "DeckWindows", deck, new Vector3(0f, 0.02f, 0f), new Vector3(2.54f, 0.1f, 2.54f), glow, false);
            var antenna = KaijuPiece(body, pieces, new Vector3(0f, y + 0.5f, 0f));
            Prim(PrimitiveType.Cylinder, "Antenna", antenna, Vector3.zero, new Vector3(0.08f, 0.5f, 0.08f), white, false);
            Prim(PrimitiveType.Sphere, "Beacon", antenna, new Vector3(0f, 0.52f, 0f), Vector3.one * 0.18f, glow, false);
            return y + 1.1f;
        }

        /// <summary>段々のビル: 上ほど細い四角い段を重ね、てっぺんに尖塔。大きい段は左右に割ったかけら</summary>
        float KaijuSteppedTower(Transform body, List<Transform> pieces, float h, Material wall, Material glow)
        {
            float[] widths = { 2.8f, 2.2f, 1.6f, 1.0f };
            float[] shares = { 0.34f, 0.28f, 0.22f, 0.16f };
            float y = 0f;
            for (int t = 0; t < widths.Length; t++)
            {
                float w = widths[t], th = h * shares[t];
                int n = Floors(th, 0.9f);
                float sh = th / n;
                for (int j = 0; j < n; j++)
                {
                    bool split = t < 2;
                    foreach (float s in split ? new[] { -1f, 1f } : new[] { 0f })
                    {
                        float pw = split ? w / 2f : w;
                        var p = KaijuPiece(body, pieces, new Vector3(s * w / 4f, y + sh * (j + 0.5f), 0f));
                        Prim(PrimitiveType.Cube, "Wall", p, Vector3.zero, new Vector3(pw - 0.02f, sh - 0.02f, w), wall, false);
                        Prim(PrimitiveType.Cube, "Windows", p, new Vector3(0f, 0.03f, 0f), new Vector3(pw + 0.01f, sh * 0.28f, w + 0.03f), glow, false);
                    }
                }
                y += th;
            }
            var spire = KaijuPiece(body, pieces, new Vector3(0f, y + 0.45f, 0f));
            Prim(PrimitiveType.Cylinder, "Spire", spire, Vector3.zero, new Vector3(0.14f, 0.45f, 0.14f), glow, false);
            Prim(PrimitiveType.Sphere, "SpireTip", spire, new Vector3(0f, 0.48f, 0f), Vector3.one * 0.2f, wall, false);
            return y + 1f;
        }

        /// <summary>ツインタワー: 背の高い細いビルが2棟並び、途中を空中の渡り廊下がつなぐ</summary>
        float KaijuTwinTowers(Transform body, List<Transform> pieces, float h, Material wall, Material glow)
        {
            const float W = 1.1f, D = 1.6f, Gap = 0.85f;
            float[] heights = { h, h * 0.88f };
            for (int t = 0; t < 2; t++)
            {
                float x = (t == 0 ? -1f : 1f) * Gap;
                int n = Floors(heights[t], 0.8f);
                float sh = heights[t] / n;
                for (int j = 0; j < n; j++)
                {
                    var p = KaijuPiece(body, pieces, new Vector3(x, sh * (j + 0.5f), 0f));
                    Prim(PrimitiveType.Cube, "Wall", p, Vector3.zero, new Vector3(W, sh - 0.02f, D), wall, false);
                    Prim(PrimitiveType.Cube, "Windows", p, new Vector3(0f, 0.03f, 0f), new Vector3(W + 0.02f, sh * 0.3f, D + 0.02f), glow, false);
                }
                var cap = KaijuPiece(body, pieces, new Vector3(x, heights[t] + 0.15f, 0f));
                Prim(PrimitiveType.Cube, "Cap", cap, Vector3.zero, new Vector3(W * 0.7f, 0.3f, D * 0.7f), glow, false);
            }
            var bridge = KaijuPiece(body, pieces, new Vector3(0f, h * 0.55f, 0f));
            Prim(PrimitiveType.Cube, "Bridge", bridge, Vector3.zero, new Vector3(Gap * 2f - W, 0.3f, 0.6f), wall, false);
            Prim(PrimitiveType.Cube, "BridgeWindows", bridge, Vector3.zero, new Vector3(Gap * 2f - W + 0.01f, 0.12f, 0.62f), glow, false);
            return h + 0.3f;
        }
    }
}
