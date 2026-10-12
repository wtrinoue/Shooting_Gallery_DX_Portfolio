using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GalleryBuilder のうち、スナイパーチャレンジの生成。</summary>
    public partial class GalleryBuilder
    {
        /// <summary>
        /// スナイパーチャレンジの遠くの大きな的。宙に浮かぶ赤と白の縞の的 (真ん中ほど高い点数) + 光る輪。
        /// 編集中はメイン屋台の奥の遠くに置いておく (再生時は隠れていて、チャレンジのたびに SniperChallenge が動かす)。
        /// </summary>
        SniperChallenge BuildSniperChallenge()
        {
            var go = new GameObject("SniperChallenge");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            // 輪の半径と点数は SniperChallenge の設定を使う (点数の判定と見た目をそろえる)
            var challenge = go.AddComponent<SniperChallenge>();
            float[] radii = challenge.ringRadii;
            int[] ringPoints = challenge.tiers[0].ringPoints; // 数字は距離が決まるたびに SniperChallenge が書き換える
            var labels = new TextMesh[radii.Length];
            float Diameter = radii[radii.Length - 1] * 2f;

            var root = Group(go.transform, "FarTarget");
            root.localPosition = new Vector3(0f, 3.5f, 22f);

            // 的本体 (根元が下端。撃たれると後ろに倒れる)
            var targetRoot = Group(root, "BigTarget");
            targetRoot.localPosition = new Vector3(0f, -Diameter / 2f, 0f);
            var target = targetRoot.gameObject.AddComponent<Target>();
            target.kind = TargetKind.SniperBig;
            target.points = ringPoints[0]; // 実際の点数は当たった所で GameManager が決める
            target.manualControl = true;
            var pivot = Group(targetRoot, "Pivot");
            target.visual = pivot;
            var gold = Mat("Sniper_Gold", new Color(1f, 0.75f, 0.15f), new Color(0.9f, 0.55f, 0.05f));
            var red = Mat("Sniper_BigRed", new Color(1f, 0.12f, 0.1f), new Color(1.1f, 0.08f, 0.05f));
            var white = Mat("Target_White", Color.white);

            // 外側から赤・白・赤…の縞。真ん中だけ金。内側の円盤ほど手前に重ねる
            float cy = Diameter / 2f;
            for (int i = radii.Length - 1; i >= 0; i--)
            {
                int fromOuter = radii.Length - 1 - i;
                var mat = i == 0 ? gold : fromOuter % 2 == 0 ? red : white;
                var disc = Disc(pivot, $"Ring_{ringPoints[i]}", cy, radii[i] * 2f, -0.006f * fromOuter, mat);
                if (i == radii.Length - 1) disc.AddComponent<BoxCollider>();

                // 点数の数字: 真ん中は中央、外の輪は帯の上側に置く
                float labelY = i == 0 ? 0f : (radii[i] + radii[i - 1]) / 2f;
                float bandWidth = i == 0 ? radii[0] : radii[i] - radii[i - 1];
                var textColor = mat == red ? Color.white : new Color(0.15f, 0.05f, 0.05f);
                labels[i] = MakeText(pivot, $"Label_{i}", ringPoints[i].ToString(), new Vector3(0f, cy + labelY, -0.03f),
                         Mathf.Min(0.24f, bandWidth * 0.7f), textColor);
            }

            // 飾り: 的のまわりの光る輪と、照準の目盛り (当たり判定なし)
            var glow = Mat("Sniper_Glow", new Color(1f, 0.3f, 0.3f), new Color(2f, 0.3f, 0.25f), true);
            var halo = Prim(PrimitiveType.Cylinder, "Halo", root, new Vector3(0f, 0f, 0.05f), new Vector3(Diameter + 0.35f, 0.005f, Diameter + 0.35f), glow, false);
            halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (var d in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right })
            {
                Prim(PrimitiveType.Cube, "Tick", root, d * (Diameter / 2f + 0.35f), new Vector3(0.06f, 0.06f, 0.02f) + new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), 0f) * 0.3f, glow, false);
            }
            MakeText(root, "Title", "SNIPER CHALLENGE", new Vector3(0f, Diameter / 2f + 0.75f, 0f), 0.3f, new Color(1f, 0.85f, 0.3f));

            challenge.root = root;
            challenge.bigTarget = target;
            challenge.ringLabels = labels;
            root.gameObject.SetActive(false); // チャレンジの時だけ出る
            go.SetActive(true);
            return challenge;
        }
    }
}
