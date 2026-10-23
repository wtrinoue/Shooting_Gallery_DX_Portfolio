using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、2 MIN の終わりのフィナーレ (FinaleCrackers)。
    /// 屋台のカウンターの外 (柱のそばの小さな棚の上) の左右に、プレイヤーの頭の上へ口を向けたクラッカー (縞模様の円すいに金紙のふた。小さな台に挿してある) を、隠した状態で置く
    /// </summary>
    public partial class GalleryBuilder
    {
        public const string FinaleApplausePath = "Assets/Source/normal/clap_in_hall.mp3"; // 2 MIN の終わりの拍手

        /// <summary>
        /// 屋台 (mainShell。どの屋台もカウンターと柱の寸法は同じ) に1組のクラッカーを置く。
        /// shelfMat / holderMat = 棚と台の材質 (豪華屋台は黒と金)
        /// </summary>
        FinaleCrackers BuildFinaleCrackers(Transform mainShell, string name, Material shelfMat, Material holderMat)
        {
            var root = Group(world, name); // 紙吹雪はこの下に出す (世界の原点。y = 0 が床)
            var finale = root.gameObject.AddComponent<FinaleCrackers>();

            var stripes = new[]
            {
                Mat("Finale_Red", new Color(0.9f, 0.12f, 0.15f)), Mat("Finale_Yellow", new Color(1f, 0.82f, 0.15f)),
                Mat("Finale_Blue", new Color(0.15f, 0.4f, 0.95f)), Mat("Finale_Green", new Color(0.15f, 0.75f, 0.35f))
            };
            var foil = Mat("Finale_Foil", new Color(1f, 0.8f, 0.3f), new Color(0.35f, 0.25f, 0.05f), false, 0.8f);

            // 円すい (先が下、口が上): 長さ 0.28m を 4色の縞に分ける
            const float Length = 0.28f;
            var radii = new[] { 0.01f, 0.03f, 0.05f, 0.07f, 0.09f };
            float seg = Length / (radii.Length - 1);
            const float CounterTop = 1.045f, CounterZ = -1.2f;
            const float CrackerX = 1.52f; // (柱は x ±1.7・z -0.9、カウンターの天板は x ±1.25 まで)

            var crackers = new Transform[2];
            var mouths = new Transform[2];
            var caps = new GameObject[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var cracker = Group(mainShell, $"FinaleCracker_{i + 1}"); // (屋台の子: 屋台が出ている時だけ見え、屋台といっしょに動く)
                // 的 (いちばん外側は x ±1.2) と重ならないよう、カウンターの外の柱のそばに置く
                cracker.localPosition = new Vector3(side * CrackerX, CounterTop, CounterZ);
                // 柱から張り出した小さな棚 (カウンターの天板と同じ高さ) と、棚を支える斜めの板
                Prim(PrimitiveType.Cube, "Shelf", cracker, new Vector3(side * 0.03f, -0.02f, 0.1f), new Vector3(0.24f, 0.04f, 0.5f), shelfMat, false);
                var brace = Prim(PrimitiveType.Cube, "ShelfBrace", cracker, new Vector3(side * 0.1f, -0.14f, 0.27f), new Vector3(0.03f, 0.24f, 0.03f), shelfMat, false);
                brace.transform.localRotation = Quaternion.Euler(0f, 0f, side * 30f);
                // 小さな台 (先を挿しておく)
                Prim(PrimitiveType.Cylinder, "Holder", cracker, new Vector3(0f, 0.03f, 0f), new Vector3(0.07f, 0.03f, 0.07f), holderMat, false);
                // 口はプレイヤーの頭の上へ (上へ、手前へ、少し真ん中へ)
                var body = Group(cracker, "Body");
                body.localPosition = new Vector3(0f, 0.04f, 0f);
                body.localRotation = Quaternion.LookRotation(new Vector3(-side * 0.45f, 1f, -0.85f).normalized, Vector3.back);
                for (int s = 0; s < radii.Length - 1; s++)
                {
                    var part = MeshPart(body, $"Stripe_{s + 1}", ConeMesh(radii[s], radii[s + 1], seg), stripes[(s + i) % stripes.Length]);
                    part.localPosition = new Vector3(0f, 0f, s * seg);
                }
                var mouth = Group(body, "Mouth");
                mouth.localPosition = new Vector3(0f, 0f, Length);
                var cap = Prim(PrimitiveType.Cylinder, "Cap", body, new Vector3(0f, 0f, Length), new Vector3(radii[radii.Length - 1] * 2.1f, 0.004f, radii[radii.Length - 1] * 2.1f), foil, false);
                cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                cracker.gameObject.SetActive(false); // 残り時間が少なくなるまで隠しておく
                crackers[i] = cracker;
                mouths[i] = mouth;
                caps[i] = cap;
            }

            finale.stall = mainShell;
            finale.crackers = crackers;
            finale.mouths = mouths;
            finale.caps = caps;
            finale.confettiMaterials = new[]
            {
                Mat("Finale_Confetti_Red", new Color(1f, 0.2f, 0.25f), null, true), Mat("Finale_Confetti_Yellow", new Color(1f, 0.88f, 0.2f), null, true),
                Mat("Finale_Confetti_Blue", new Color(0.25f, 0.55f, 1f), null, true), Mat("Finale_Confetti_Green", new Color(0.3f, 0.9f, 0.45f), null, true),
                Mat("Finale_Confetti_Pink", new Color(1f, 0.5f, 0.8f), null, true), Mat("Finale_Confetti_White", new Color(1f, 0.98f, 0.92f), null, true),
                Mat("Finale_Confetti_Gold", new Color(1f, 0.78f, 0.3f), null, true)
            };
            return finale;
        }
    }
}
