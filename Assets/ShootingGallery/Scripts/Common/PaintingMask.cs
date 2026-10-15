using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 美術館の絵が「筆で塗られていく」順番 (絵の画像のアルファに入れた塗る順番と、筆の動きを合わせる)。
    /// 絵は上から下へ横長の帯 (rows 本) を、左右に行ったり来たりしながら塗る。絵の材質はアルファで切り抜き (Alpha Clip)、
    /// _Cutoff を 1 → 0 に下げると、塗る順番の早い所から見えていく (ビルダーで画像を作る時と、実行時の筆の動きで同じ並びを使う)
    /// </summary>
    public static class PaintingMask
    {
        /// <summary>段ごとの帯の数 (1段目: 大まかな下塗り / 2段目: 形 / 3段目: 細かい仕上げ)</summary>
        public static readonly int[] StageRows = { 4, 6, 8 };
        /// <summary>行きのキャンバス (美術館の中の絵) の帯の数</summary>
        public const int IntroRows = 7;

        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

        /// <summary>塗り進み k (0〜1) の時の、筆先の位置 (絵の中の割合。u = 左 0 〜 右 1、v = 下 0 〜 上 1)</summary>
        public static Vector2 BrushAt(float k, int rows)
        {
            float f = Mathf.Clamp01(k) * rows;
            int r = Mathf.Min(rows - 1, (int)f);
            float p = f - r;
            float u = r % 2 == 0 ? p : 1f - p;
            return new Vector2(u, 1f - (r + 0.5f) / rows);
        }

        /// <summary>絵を k (0〜1) まで塗った見た目にする (MaterialPropertyBlock で、その絵だけ)。k = 1 なら元の材質に戻す (まとめて描ける)</summary>
        public static void SetProgress(Renderer r, MaterialPropertyBlock block, float k)
        {
            if (r == null) return;
            if (k >= 1f)
            {
                r.SetPropertyBlock(null);
                return;
            }
            block.Clear();
            block.SetFloat(CutoffId, 1f - Mathf.Clamp01(k));
            r.SetPropertyBlock(block);
        }
    }
}
