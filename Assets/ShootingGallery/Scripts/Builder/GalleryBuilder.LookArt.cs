using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、別の世界へ飛ぶモードの的の絵 (ステッカー)。
    /// LookArt が Painter で描いた絵 (透明な背景・黒いふち) を、的より大きな板 (StickerSize 四方) に切り抜きで貼り、
    /// リボンの上に名前の文字を重ねる。当たり判定は真ん中の見えない箱 (StickerHitSize 四方)。はみ出した絵には当たり判定を付けない
    /// </summary>
    public partial class GalleryBuilder
    {
        const float StickerSize = 0.56f;    // 絵の板の大きさ (m。もとの的の板は 0.3m 四方)
        const float StickerHitSize = 0.34f; // 当たり判定の箱の大きさ (m)

        /// <summary>design (LookArt.Designs の名前) の絵の的。label = リボンに書く名前</summary>
        GameObject StickerLook(Transform look, string design, string label, Color labelColor)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            var hit = Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(StickerHitSize, StickerHitSize, 0.04f), null);
            HitOnly(hit);
            Prim(PrimitiveType.Quad, "Art", look, c + Vector3.back * 0.03f, new Vector3(StickerSize, StickerSize, 1f), LookArtMat(design), false);
            float ribbonY = (LookArt.RibbonY / LookArt.Size - 0.5f) * StickerSize;
            MakeText(look, "Label", label, c + new Vector3(0f, ribbonY, -0.036f), 0.042f, labelColor);
            return look.gameObject;
        }

        // 絵の材質 (Unlit の切り抜き)。テクスチャは再生のたびに LookArt で描く (256×256 なので一瞬)
        Material LookArtMat(string design)
        {
            string name = $"LookArt_{design}";
            if (mats.TryGetValue(name, out var cached)) return cached;
            var p = LookArt.Draw(design);
            var tex = new Texture2D(LookArt.Size, LookArt.Size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
            };
            tex.SetPixels(p.px);
            tex.Apply(true);
            var mat = new Material(unlitShader) { name = name };
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mats[name] = mat;
            return mat;
        }
    }
}
