using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、WORLD GATE (タイトルの START の横の的。撃つと、別の世界へ飛ぶ的だけが並んだ6軒の屋台に囲まれる)。
    /// 屋台は1段だけで、的を横一列に並べる。どの屋台にどの世界の的を並べるかは GameManager が決める (GameManager.WorldGate.cs)
    /// </summary>
    public partial class GalleryBuilder
    {
        const int GateStallCount = 6;   // WORLD GATE の輪の屋台の数
        const int GateSlots = 3;        // 1軒の段に並べる的の数 (入りきらない世界の的は次の屋台へ。6軒 × 3 = 18 個まで)
        const float GateTierH = 1.3f;   // 段の高さ (普通の屋台の中段と同じ)
        const float GateTierZ = 0.15f;
        const float GateSlotStep = 0.95f; // 的の間隔

        /// <summary>タイトルの WORLD GATE の的 (START の横)。文字は WORLD が上、GATE が下</summary>
        Target BuildWorldGateTarget(Transform stall)
        {
            var root = Group(stall, "Target_WorldGate");
            root.localPosition = new Vector3(TitleTargetGap, 0f, -0.6f); // 屋台の床から立てる (タイトルの的の右。左から 2 MIN / 100,000 POINTS / WORLD GATE)
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.WorldGate;
            target.points = 0;
            target.respawnDelay = 1f;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;
            const float y = TitleTargetY;
            TitleTargetStand(pivot, y + 0.62f - 0.55f / 2f);
            var blue = Mat("Target_GateBlue", new Color(0.2f, 0.25f, 0.85f), new Color(0.05f, 0.06f, 0.3f));
            Bullseye(pivot, y + 0.62f, 0.55f, blue, Mat("Target_White", Color.white), Mat("Target_GatePurple", new Color(0.6f, 0.2f, 0.85f), new Color(0.2f, 0.05f, 0.3f)));
            var ink = new Color(0.08f, 0.08f, 0.2f);
            TitleLabel(pivot, "LabelWorld", "WORLD", new Vector3(0f, y + 0.685f, -0.03f), 0.1f, ink);
            TitleLabel(pivot, "LabelGate", "GATE", new Vector3(0f, y + 0.555f, -0.03f), 0.1f, ink);
            return target;
        }

        /// <summary>WORLD GATE の BACK の的 (撃つとタイトルに戻る): 紫の板に、左向きの白い矢印と「BACK」の文字</summary>
        GameObject GateBackLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.32f, 0.32f, 0.04f), Mat("Target_GateBack", new Color(0.3f, 0.12f, 0.5f), new Color(0.08f, 0.03f, 0.15f)));
            Prim(PrimitiveType.Cube, "PlateTrim", look, c + Vector3.forward * 0.005f, new Vector3(0.35f, 0.35f, 0.03f), Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f)), false);
            // 左向きの矢印 (軸と、斜めの2本の矢じり)
            var white = Mat("Target_White", Color.white);
            var arrow = c + new Vector3(0f, 0.035f, -0.025f);
            Prim(PrimitiveType.Cube, "ArrowShaft", look, arrow + new Vector3(0.02f, 0f, 0f), new Vector3(0.17f, 0.035f, 0.01f), white, false);
            foreach (float a in new[] { 45f, -45f })
            {
                var head = Prim(PrimitiveType.Cube, "ArrowHead", look, arrow + new Vector3(-0.045f, a > 0f ? 0.03f : -0.03f, 0f), new Vector3(0.1f, 0.035f, 0.01f), white, false);
                head.transform.localRotation = Quaternion.Euler(0f, 0f, a);
            }
            MakeText(look, "Label", "BACK", c + new Vector3(0f, -0.075f, -0.025f), 0.05f, new Color(1f, 0.88f, 0.55f));
            return look.gameObject;
        }

        /// <summary>WORLD GATE の6軒の屋台 (1段だけ・的を横一列に GateSlots 個。隠した状態で作る。並べ方は GameManager がフィーバーの輪と同じように決める)</summary>
        Stall[] BuildWorldGateStalls()
        {
            var cloth = Mat("Cloth_Gate", new Color(0.35f, 0.15f, 0.6f));
            var stalls = new Stall[GateStallCount];
            for (int i = 0; i < GateStallCount; i++)
            {
                float angle = 360f / GateStallCount * i;
                var t = Group(world, $"Stall_Gate{i + 1}");
                t.gameObject.SetActive(false);
                var rot = Quaternion.Euler(0f, angle, 0f);
                t.localPosition = rot * new Vector3(0f, 0f, stallRadius);
                t.localRotation = rot;
                BuildStallFrame(t, cloth, "WORLD GATE");
                Prim(PrimitiveType.Cube, "Tier", t, new Vector3(0f, GateTierH / 2f, GateTierZ), new Vector3(3.0f, GateTierH, 0.25f), cloth);

                // 的は組み立て中は非アクティブにしておき、しまった状態にしてから有効化する (BuildStallContent と同じ)
                var targetsRoot = Group(t, "Targets");
                targetsRoot.gameObject.SetActive(false);
                var targets = new List<Target>();
                for (int s = 0; s < GateSlots; s++)
                {
                    float x = (s - (GateSlots - 1) * 0.5f) * GateSlotStep;
                    targets.Add(PointTarget(targetsRoot, 100, new Vector3(x, GateTierH, GateTierZ)));
                }
                var stall = t.gameObject.AddComponent<Stall>();
                stall.targets = targets.ToArray();
                stall.SetTargetsAvailable(false);
                targetsRoot.gameObject.SetActive(true);
                stalls[i] = stall;
            }
            return stalls;
        }
    }
}
