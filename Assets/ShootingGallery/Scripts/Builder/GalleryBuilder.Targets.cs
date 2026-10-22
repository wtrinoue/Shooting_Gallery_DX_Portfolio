using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GalleryBuilder のうち、的 (点数の的・フィーバーの的・START の的と、的の模様) の生成。</summary>
    public partial class GalleryBuilder
    {
        // ------------------------------------------------------------------
        // 的
        // ------------------------------------------------------------------

        const float TargetDiscY = 0.3f; // 的の根元から円の中心までの高さ

        /// <summary>点数付きの的。点数で色と大きさが変わり、中央に点数が書いてある。</summary>
        /// <summary>点数の的。stand = false なら円盤の下の棒を付けない (回る円盤や板に貼り付いた的・吊るした的など、棒があるとおかしい所)</summary>
        Target PointTarget(Transform parent, int points, Vector3 localPos, float respawnDelay = 3f, bool stand = true)
        {
            var root = Group(parent, $"Target_{points}");
            root.localPosition = localPos;
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Normal;
            target.points = points;
            target.respawnDelay = respawnDelay;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;

            float d = StallTargetDiameter(points);
            if (stand)
                Prim(PrimitiveType.Cube, "Stand", pivot, new Vector3(0f, (TargetDiscY - d / 2f) / 2f, 0f), new Vector3(0.035f, TargetDiscY - d / 2f, 0.035f), Mat("Target_Stand", new Color(0.3f, 0.3f, 0.32f)));

            // 「顔」(円盤と数字)。武器の的に化ける時はここが武器の箱に付け替わる
            var face = Group(pivot, "Face");
            target.face = face.gameObject;
            var color = Mat($"Points_{points}", PointStyle.TargetColor(points), PointStyle.Emission(points));
            Bullseye(face, TargetDiscY, d, color, Mat("Target_White", Color.white), null);
            // 数字 (図形): 豪華屋台の的 (ShapeTarget) の数字と同じ大きさ。円の模様より手前に置く
            float dx = DxSize(points);
            PointDigits(face, points, new Vector3(0f, TargetDiscY, -0.02f), dx * 0.9f, dx * 0.4f, DigitMat);
            return target;
        }

        /// <summary>普通の屋台の点数の的の直径 (PointStyle の大きさを一回り大きくして、遠くからでも狙いやすく・数字を読みやすくする)</summary>
        static float StallTargetDiameter(int points) => PointStyle.Diameter(points) * 1.25f;

        /// <summary>回る円盤や板に貼り付けた点数の的: 棒がなく、当たると後ろに倒れずにその場で縮んで消える (起き上がる時はふくらんで出る)</summary>
        Target WallTarget(Transform parent, int points, Vector3 localPos)
        {
            var target = PointTarget(parent, points, localPos, 3f, stand: false);
            target.shrinkOnHit = true;
            // 縮む中心が円盤の真ん中になるように、見た目の軸を円盤の中心まで上げ、中身はその分下げる
            var pivot = target.visual;
            pivot.localPosition = Vector3.up * TargetDiscY;
            foreach (Transform child in pivot) child.localPosition += Vector3.down * TargetDiscY;
            return target;
        }


        /// <summary>フィーバーの赤い的 (メイン屋台の最上段)。当てると周りに屋台が現れる。</summary>
        Target FeverTarget(Transform parent, Vector3 localPos)
        {
            var root = Group(parent, "Target_Fever");
            root.localPosition = localPos;
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Fever;
            target.points = 300;
            target.respawnDelay = 25f;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;

            Prim(PrimitiveType.Cube, "Stand", pivot, new Vector3(0f, 0.08f, 0f), new Vector3(0.04f, 0.16f, 0.04f), Mat("Target_Stand", new Color(0.3f, 0.3f, 0.32f)));
            Prim(PrimitiveType.Sphere, "RedOrb", pivot, new Vector3(0f, 0.32f, 0f), Vector3.one * 0.26f, Mat("Target_Fever", new Color(1f, 0.1f, 0.1f), new Color(1.5f, 0.05f, 0.05f)));
            var spinner = Group(pivot, "Spinner");
            spinner.localPosition = new Vector3(0f, 0.32f, 0f);
            var ring = Prim(PrimitiveType.Cylinder, "Ring", spinner, Vector3.zero, new Vector3(0.38f, 0.008f, 0.38f), Mat("Target_FeverRing", new Color(1f, 0.8f, 0.2f), new Color(0.6f, 0.4f, 0f)), false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            target.spinner = spinner;
            target.spinSpeed = 150f;
            return target;
        }

        // タイトルの的の横の間隔 (左から 2 MIN / 100,000 POINTS / WORLD GATE。円の直径 0.55m の間を少し空ける)
        const float TitleTargetGap = 0.72f;

        /// <summary>真ん中のタイトルの的「100,000 POINTS」: 撃つと、100,000 点を取るまでの時間を競うラウンドが始まる (赤い的)</summary>
        Target BuildStartTarget(Transform stall)
        {
            var root = Group(stall, "Target_Start");
            root.localPosition = new Vector3(0f, 0f, -0.6f); // 左に 2 MIN、右に WORLD GATE の的
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Start;
            target.points = 0;
            target.respawnDelay = 1f;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;
            const float y = TitleTargetY;
            TitleTargetStand(pivot, y + 0.62f - 0.55f / 2f);
            var red = Mat("Target_StartRed", new Color(0.85f, 0.1f, 0.1f), new Color(0.3f, 0f, 0f));
            Bullseye(pivot, y + 0.62f, 0.55f, red, Mat("Target_White", Color.white), red);
            var ink = new Color(0.1f, 0.1f, 0.1f);
            MakeText(pivot, "Label", "100,000", new Vector3(0f, y + 0.685f, -0.03f), 0.1f, ink);
            MakeText(pivot, "Label2", "POINTS", new Vector3(0f, y + 0.555f, -0.03f), 0.09f, ink);
            return target;
        }

        /// <summary>左のタイトルの的「2 MIN」: 撃つと、2分間でどれだけ点を取れるかのタイムアタックが始まる (緑の的)</summary>
        Target BuildTimeAttackTarget(Transform stall)
        {
            var root = Group(stall, "Target_TimeAttack");
            root.localPosition = new Vector3(-TitleTargetGap, 0f, -0.6f);
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.TimeAttack;
            target.points = 0;
            target.respawnDelay = 1f;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;
            const float y = TitleTargetY;
            TitleTargetStand(pivot, y + 0.62f - 0.55f / 2f);
            var green = Mat("Target_TimeGreen", new Color(0.15f, 0.65f, 0.25f), new Color(0.03f, 0.2f, 0.06f));
            Bullseye(pivot, y + 0.62f, 0.55f, green, Mat("Target_White", Color.white), Mat("Target_TimeYellow", new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.2f, 0f)));
            var ink = new Color(0.1f, 0.1f, 0.1f);
            MakeText(pivot, "Label", "2", new Vector3(0f, y + 0.685f, -0.03f), 0.12f, ink);
            MakeText(pivot, "Label2", "MIN", new Vector3(0f, y + 0.555f, -0.03f), 0.1f, ink);
            return target;
        }

        // タイトルの的 (START / WORLD GATE) の、屋台の床から円の下までの高さの目安 (円の中心は床から 1.72m)
        const float TitleTargetY = 1.1f;

        /// <summary>
        /// タイトルの的の棒: 屋台の床の重しから、円の下 (top) までまっすぐ立てる。
        /// 的は根元 (床) を軸に奥へ倒れるので、棒も重しも見た目 (pivot) ごと倒れて消える
        /// </summary>
        void TitleTargetStand(Transform pivot, float top)
        {
            var stand = Mat("Target_Stand", new Color(0.3f, 0.3f, 0.32f));
            Prim(PrimitiveType.Cube, "Stand", pivot, new Vector3(0f, top / 2f, 0f), new Vector3(0.05f, top + 0.02f, 0.05f), stand);
            Prim(PrimitiveType.Cylinder, "StandBase", pivot, new Vector3(0f, 0.03f, 0f), new Vector3(0.26f, 0.03f, 0.26f), Mat("Target_StandBase", new Color(0.18f, 0.18f, 0.2f)), false);
            Prim(PrimitiveType.Cube, "StandCollar", pivot, new Vector3(0f, 0.1f, 0f), new Vector3(0.09f, 0.08f, 0.09f), stand, false);
        }

        /// <summary>同心円の的。外側の円にだけ BoxCollider を付ける。center が null なら2色。</summary>
        void Bullseye(Transform pivot, float centerY, float diameter, Material outer, Material middle, Material center)
        {
            var o = Disc(pivot, "Outer", centerY, diameter, 0f, outer);
            o.AddComponent<BoxCollider>();
            Disc(pivot, "Middle", centerY, diameter * 0.66f, -0.006f, middle);
            if (center != null)
                Disc(pivot, "Center", centerY, diameter * 0.3f, -0.012f, center);
        }

        // 当たり判定なしの円盤 (的の模様用)
        GameObject Disc(Transform parent, string name, float y, float diameter, float z, Material mat)
        {
            var go = Prim(PrimitiveType.Cylinder, name, parent, new Vector3(0f, y, z), new Vector3(diameter, 0.01f, diameter), mat, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return go;
        }
    }
}
