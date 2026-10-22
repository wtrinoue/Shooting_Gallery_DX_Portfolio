using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、幽霊モードの空のドーム (月に近いほど明るい)・朽ちた家・魔女の鍋。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float GhostDomeR = 92f; // 空のドームの半径 (カメラの見える距離 100m より内側。月や雲・星はこの内側)

        /// <summary>
        /// 空のドーム (内側から見る大きな球)。月の方向 (moonDir) に近いほど白っぽく明るく、離れるほど白さが抜けて深い紫の闇になる。
        /// 地平線の近くは、ほんのり明るい紫。色は頂点の色で付け、材質の透明度で現れる / 消える (GhostManor)
        /// </summary>
        Renderer BuildGhostSkyDome(Transform parent, Vector3 moonDir)
        {
            var go = new GameObject("SkyDome");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = SkyDomeMesh(moonDir.normalized);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = SkyDomeMat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        Mesh SkyDomeMesh(Vector3 moonDir)
        {
            const string name = "Ghost_SkyDome";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            const int Lat = 28, Lon = 56;
            Color far = new Color(0.03f, 0.012f, 0.06f), mid = new Color(0.2f, 0.11f, 0.34f), near = new Color(0.82f, 0.76f, 0.98f);
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i <= Lat; i++)
            {
                float lat = Mathf.Lerp(-30f, 90f, (float)i / Lat) * Mathf.Deg2Rad; // (地平線の少し下から真上まで。下は土に隠れる)
                for (int j = 0; j <= Lon; j++)
                {
                    float lon = j * Mathf.PI * 2f / Lon;
                    var d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
                    verts.Add(d * GhostDomeR);
                    float cosA = Vector3.Dot(d, moonDir);
                    float broad = Mathf.Pow(Mathf.Max(0f, (cosA + 1f) * 0.5f), 5f); // 月の側の空が広く明るい
                    float glow = Mathf.Pow(Mathf.Max(0f, cosA), 30f);                // 月のすぐまわりは白っぽく
                    float horizon = Mathf.Pow(1f - Mathf.Abs(d.y), 4f) * 0.5f;      // 地平線のあたりは、ほんのり明るい紫
                    var c = Color.Lerp(far, mid, Mathf.Clamp01(broad + horizon * 0.4f));
                    c = Color.Lerp(c, near, glow);
                    c.a = 1f;
                    colors.Add(c);
                }
            }
            for (int i = 0; i < Lat; i++)
                for (int j = 0; j < Lon; j++)
                {
                    int a = i * (Lon + 1) + j, b = a + 1, c = a + Lon + 1, e = c + 1;
                    // 内側から見える向き
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(e); tris.Add(c);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMeshIfEditing(mesh, name);
            meshes[name] = mesh;
            return mesh;
        }

        // 頂点の色が効く半透明の材質 (パーティクル用の Unlit)。ほかの物より先に描いて (いつもいちばん奥に見える)、透明度で現れる / 消える
        Material SkyDomeMat()
        {
            const string name = "Ghost_SkyDome";
            if (mats.TryGetValue(name, out var cached)) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return TransparentMat(name, new Color(0.2f, 0.11f, 0.34f, 1f));
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f); // (両面。内側からも外側からも見える)
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background + 1; // ほかの物より先に描く (いちばん奥の空)
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        // ------------------------------------------------------------------
        // 朽ちた家
        // ------------------------------------------------------------------

        /// <summary>
        /// 朽ちた家 (表 -Z がプレイヤー向き。根元の真ん中が原点)。少し傾いた木の板の壁 (ところどころ板が抜けている)、
        /// 真っ暗な窓に×に打ち付けた板、半開きの扉、片側に穴のあいたたわんだ屋根、傾いた煙突、地面に落ちた板。
        /// glow なら、窓の1つに不気味な緑の明かりが灯る
        /// </summary>
        void RuinedHouse(Transform parent, Vector3 pos, float scale, bool glow, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var house = Group(parent, "RuinedHouse");
            house.localPosition = pos;
            house.localRotation = Quaternion.LookRotation(new Vector3(pos.x, 0f, pos.z)) * Quaternion.Euler(0f, R(-15f, 15f), 0f);
            house.localScale = Vector3.one * scale;
            var body = Group(house, "Body");
            body.localRotation = Quaternion.Euler(R(-3f, 3f), 0f, R(-6f, 6f)); // 全体が少し傾いている

            var wood = Mat("Ghost_RuinWood", new Color(0.3f, 0.25f, 0.2f), null, false, 0.05f);
            var woodDark = Mat("Ghost_RuinWoodDark", new Color(0.17f, 0.14f, 0.12f), null, false, 0.05f);
            var roofMat = Mat("Ghost_RuinRoof", new Color(0.14f, 0.12f, 0.13f), null, false, 0.05f);
            var stone = Mat("Ghost_GraveStoneDark", new Color(0.3f, 0.3f, 0.34f), null, false, 0.15f);
            var dark = Mat("Ghost_RuinHole", new Color(0.015f, 0.01f, 0.02f));
            var eerie = Mat("Ghost_RuinGlow", new Color(0.5f, 1f, 0.6f), new Color(0.6f, 1.8f, 0.8f));
            const float W = 5f, D = 4f, H = 2.8f;

            // 石の土台
            Prim(PrimitiveType.Cube, "Foundation", body, new Vector3(0f, 0.15f, 0f), new Vector3(W + 0.3f, 0.3f, D + 0.3f), stone, false);
            // 壁: 縦の板を並べる (ところどころ抜けている)。窓と扉の所は暗い穴
            void Wall(Vector3 center, Vector3 right, float width, bool front)
            {
                int n = Mathf.RoundToInt(width / 0.42f);
                float pw = width / n;
                var rot = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
                // 壁の裏の暗い面 (板の抜けた所から、中の暗がりが見える)
                var back = Prim(PrimitiveType.Cube, "WallDark", body, center + Vector3.up * (H / 2f + 0.3f), new Vector3(width, H, 0.02f), dark, false);
                back.transform.localRotation = rot;
                for (int k = 0; k < n; k++)
                {
                    if (rng.NextDouble() < 0.12) continue; // 抜けた板
                    float x = -width / 2f + (k + 0.5f) * pw;
                    float h = H * R(0.82f, 1f);           // 上がぎざぎざに折れている
                    var p = Prim(PrimitiveType.Cube, "Plank", body, center + right * x + Vector3.up * (0.3f + h / 2f), new Vector3(pw * 0.92f, h, 0.06f), k % 3 == 0 ? woodDark : wood, false);
                    p.transform.localRotation = rot * Quaternion.Euler(0f, 0f, R(-3f, 3f));
                    p.transform.localPosition -= rot * Vector3.forward * 0.03f;
                }
                if (!front) return;
                // 正面: 半開きの扉 (奥は真っ暗) と、2つの窓 (×に板を打ち付けた暗い穴。1つは緑に光ることも)
                var fwd = rot * Vector3.back * 0.08f;
                Prim(PrimitiveType.Cube, "Doorway", body, center + Vector3.up * 1.3f + fwd, new Vector3(0.95f, 2f, 0.02f), dark, false).transform.localRotation = rot;
                var door = Group(body, "Door");
                door.localPosition = center - right * 0.47f + Vector3.up * 1.3f + fwd * 2f;
                door.localRotation = rot * Quaternion.Euler(0f, -R(35f, 60f), 0f);
                Prim(PrimitiveType.Cube, "DoorPanel", door, new Vector3(0.47f, 0f, 0f), new Vector3(0.9f, 1.95f, 0.05f), woodDark, false);
                foreach (float s in new[] { -1f, 1f })
                {
                    var wpos = center + right * (s * 1.5f) + Vector3.up * 1.9f + fwd;
                    bool lit = glow && s > 0f;
                    Prim(PrimitiveType.Cube, "Window", body, wpos, new Vector3(0.8f, 0.7f, 0.02f), lit ? eerie : dark, false).transform.localRotation = rot;
                    foreach (float tilt in new[] { 35f, -35f })
                    {
                        var board = Prim(PrimitiveType.Cube, "Board", body, wpos + fwd, new Vector3(1f, 0.1f, 0.04f), wood, false);
                        board.transform.localRotation = rot * Quaternion.Euler(0f, 0f, tilt + R(-6f, 6f));
                    }
                }
            }
            Wall(new Vector3(0f, 0f, -D / 2f), Vector3.right, W, true);       // 正面 (プレイヤーの方)
            Wall(new Vector3(0f, 0f, D / 2f), Vector3.left, W, false);        // 裏
            Wall(new Vector3(W / 2f, 0f, 0f), Vector3.forward, D, false);     // 右
            Wall(new Vector3(-W / 2f, 0f, 0f), Vector3.back, D, false);       // 左

            // たわんだ切妻屋根 (左右の2枚。右の1枚は穴があいて、垂木が見える)
            float roofY = 0.3f + H, rise = 1.3f, half = W / 2f + 0.3f;
            float slope = Mathf.Atan2(rise, half) * Mathf.Rad2Deg, len = Mathf.Sqrt(half * half + rise * rise);
            foreach (float s in new[] { -1f, 1f })
            {
                var side = Group(body, "RoofSide");
                side.localPosition = new Vector3(s * half / 2f, roofY + rise / 2f, 0f);
                side.localRotation = Quaternion.Euler(R(-3f, 3f), 0f, -s * slope + R(-4f, 4f)); // (少したわむ)
                if (s > 0f)
                {
                    // 穴のあいた側: 屋根板を前と後ろに分けて、間に垂木だけ見える
                    Prim(PrimitiveType.Cube, "Roof", side, new Vector3(0f, 0f, -D * 0.33f), new Vector3(len, 0.1f, D * 0.42f), roofMat, false);
                    Prim(PrimitiveType.Cube, "Roof", side, new Vector3(0.3f, 0f, D * 0.36f), new Vector3(len * 0.75f, 0.1f, D * 0.38f), roofMat, false);
                    for (int k = -1; k <= 1; k++)
                        Prim(PrimitiveType.Cube, "Rafter", side, new Vector3(0f, -0.08f, k * 0.45f), new Vector3(len, 0.1f, 0.1f), woodDark, false);
                }
                else Prim(PrimitiveType.Cube, "Roof", side, Vector3.zero, new Vector3(len, 0.1f, D + 0.5f), roofMat, false);
            }
            // 屋根の三角の壁 (正面と裏)
            foreach (float z in new[] { -D / 2f, D / 2f })
                for (int k = 0; k < 5; k++)
                {
                    float y = roofY + (k + 0.5f) * rise / 5f, w = 2f * half * (1f - (k + 0.5f) / 5f);
                    Prim(PrimitiveType.Cube, "Gable", body, new Vector3(0f, y, z), new Vector3(w, rise / 5f, 0.06f), k % 2 == 0 ? wood : woodDark, false);
                }
            // 傾いた石の煙突
            var chimney = Group(body, "Chimney");
            chimney.localPosition = new Vector3(-W * 0.25f, roofY + rise * 0.5f, D * 0.15f);
            chimney.localRotation = Quaternion.Euler(R(-5f, 5f), 0f, R(8f, 14f));
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Cube, "ChimneyStone", chimney, new Vector3(R(-0.03f, 0.03f), k * 0.38f, 0f), new Vector3(0.55f, 0.38f, 0.55f), stone, false);
            // 地面に落ちた板と、崩れた石
            for (int k = 0; k < 5; k++)
            {
                var plank = Prim(PrimitiveType.Cube, "FallenPlank", house, new Vector3(R(-W * 0.7f, W * 0.7f), 0.04f, -D / 2f - R(0.6f, 2f)), new Vector3(R(1f, 2f), 0.06f, 0.35f), k % 2 == 0 ? wood : woodDark, false);
                plank.transform.localRotation = Quaternion.Euler(0f, R(0f, 180f), R(-8f, 8f));
            }
            if (glow && pos.magnitude < 26f) // (奥の家は窓が光るだけ。明かりの数を増やしすぎないように)
            {
                // 緑に光る窓の明かり (家の前だけをぼんやり照らす)
                var lg = new GameObject("RuinGlow");
                lg.transform.SetParent(body, false);
                lg.transform.localPosition = new Vector3(1.5f, 1.9f, -D / 2f - 0.8f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 6f;
                l.intensity = 1.2f;
                l.color = new Color(0.5f, 1f, 0.6f);
                l.shadows = LightShadows.None;
            }
        }

        // ------------------------------------------------------------------
        // 魔女の鍋
        // ------------------------------------------------------------------

        /// <summary>
        /// 魔女の鍋 (根元の真ん中が原点)。3本脚の黒い鉄の鍋に、緑に光る液。下ではまきの火が燃えて、緑の明かりがまわりを照らす。
        /// 泡と火は GhostManor が動かす (組み上げの部品には入れない。大きさを動かすので): bubbles・flames に入れて返す。
        /// surfaceY = 液の面の高さ (泡の親 = bubbleRoot のローカル)
        /// </summary>
        Transform WitchCauldron(Transform parent, Vector3 pos, Transform bubbleRoot, List<Transform> bubbles, List<Transform> flames, out float surfaceY)
        {
            var c = Group(parent, "WitchCauldron");
            c.localPosition = pos;
            var iron = Mat("Ghost_CauldronIron", new Color(0.08f, 0.08f, 0.09f), null, false, 0.5f);
            var brew = Mat("Ghost_Brew", new Color(0.35f, 1f, 0.3f), new Color(0.6f, 2.2f, 0.4f));
            var logMat = Mat("Ghost_Log", new Color(0.28f, 0.17f, 0.09f), null, false, 0.05f);
            const float PotY = 0.75f;
            // 鍋 (少しつぶれた球) と、ふちの厚い輪・取っ手
            Prim(PrimitiveType.Sphere, "Pot", c, new Vector3(0f, PotY, 0f), new Vector3(1.15f, 0.85f, 1.15f), iron, false);
            FlatRing(c, "Rim", 0.5f, PotY + 0.3f, 0.1f, 0.08f, 20, iron);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Handle", c, new Vector3(s * 0.6f, PotY + 0.22f, 0f), new Vector3(0.12f, 0.06f, 0.2f), iron, false);
            // 光る液の面
            Prim(PrimitiveType.Cylinder, "Brew", c, new Vector3(0f, PotY + 0.29f, 0f), new Vector3(0.92f, 0.01f, 0.92f), brew, false);
            // 3本脚
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f;
                var leg = Prim(PrimitiveType.Cube, "Leg", c, new Vector3(Mathf.Sin(a) * 0.42f, 0.2f, Mathf.Cos(a) * 0.42f), new Vector3(0.08f, 0.45f, 0.08f), iron, false);
                leg.transform.localRotation = Quaternion.Euler(Mathf.Cos(a) * -15f, 0f, Mathf.Sin(a) * 15f);
            }
            // 鍋の下のまき (井げたに組んだ丸太)
            for (int k = 0; k < 4; k++)
            {
                var log = Prim(PrimitiveType.Cylinder, "Log", c, new Vector3(0f, 0.08f + (k / 2) * 0.1f, 0f), new Vector3(0.1f, 0.38f, 0.1f), logMat, false);
                log.transform.localRotation = Quaternion.Euler(90f, k * 45f + 20f, 0f);
            }
            // 火 (外は橙・中は黄色。GhostManor がゆらゆら伸び縮みさせる)
            var flame = Group(bubbleRoot, "CauldronFlame");
            flame.localPosition = c.localPosition + new Vector3(0f, 0.14f, 0f);
            Prim(PrimitiveType.Sphere, "FlameOuter", flame, new Vector3(0f, 0.14f, 0f), new Vector3(0.45f, 0.4f, 0.45f), Mat("Mine_FlameOuter", new Color(2.6f, 0.9f, 0.2f), null, true), false);
            Prim(PrimitiveType.Sphere, "FlameInner", flame, new Vector3(0f, 0.1f, 0f), new Vector3(0.25f, 0.24f, 0.25f), Mat("Mine_FlameInner", new Color(3f, 2.4f, 0.9f), null, true), false);
            flames.Add(flame);
            // ぶくぶく上がる泡 (GhostManor が液の面から上へ動かす)
            surfaceY = c.localPosition.y + PotY + 0.3f;
            var bubbleMat = Mat("Ghost_Bubble", new Color(0.55f, 1f, 0.45f), new Color(0.8f, 2.4f, 0.6f), true);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 1.7f, r = 0.12f + (k % 3) * 0.1f;
                var b = Prim(PrimitiveType.Sphere, "Bubble", bubbleRoot, c.localPosition + new Vector3(Mathf.Sin(a) * r, PotY + 0.3f, Mathf.Cos(a) * r), Vector3.one * 0.1f, bubbleMat, false);
                bubbles.Add(b.transform);
            }
            // 緑の明かり (鍋のまわりをぼんやり照らす)
            var lg = new GameObject("BrewLight");
            lg.transform.SetParent(c, false);
            lg.transform.localPosition = new Vector3(0f, PotY + 0.8f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 5f;
            l.intensity = 1.6f;
            l.color = new Color(0.45f, 1f, 0.4f);
            l.shadows = LightShadows.None;
            return c;
        }
    }
}
