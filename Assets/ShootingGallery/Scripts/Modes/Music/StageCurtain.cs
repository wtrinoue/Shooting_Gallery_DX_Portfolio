using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 音楽モードの、プレイヤーを囲む丸いカーテン。
    /// 頭の少し上に濃い灰色の円い天井と、そのふちに丸いレール (金色) と上飾りの布がある。上から降りてきて、終わると上へ去っていく。
    /// 赤いビロードのカーテンは左右の2枚。開いている時はどちらもプレイヤーの真後ろにたぐり寄せてあり (ひだが深い)、
    /// 閉まる時は2枚が左右に分かれてレールをぐるりと回り、正面 (的を撃った時に見ていた方) で合わさる (SetCover 0 → 1)。開く時は逆に真後ろへ戻る。
    /// 布のメッシュは実行中に作り、動いている間だけ毎フレーム作り直す (頂点は 600 ほど)
    /// </summary>
    public class StageCurtain : MonoBehaviour
    {
        [Tooltip("天井・レール・上飾り・カーテン (現れる時はふくらみ、消える時は縮む)")]
        public Transform frame;
        [Tooltip("引かれるカーテンの布 / 上飾りの布 (メッシュは実行中に作る)")]
        public MeshFilter fabric, valance;
        [Tooltip("閉まっている間、中を照らす明かり")]
        public Light lamp;
        public float radius = 2f;
        [Tooltip("レールの高さ (カーテンの上端)")]
        public float height = 2.35f;
        [Tooltip("ひだの数 (2枚の合計) / ひだの深さ (m)")]
        public int pleats = 64;
        public float pleatDepth = 0.06f;
        [Tooltip("上飾りの高さ (m)")]
        public float valanceHeight = 0.28f;
        [Tooltip("天井とカーテンが降りてくる高さ (m。定位置からこれだけ上から)")]
        public float dropHeight = 6f;

        public float Cover { get; private set; }

        Mesh mesh;
        Vector3[] verts;
        int columns;
        float lampIntensity;

        void Awake() => Init();

        /// <summary>布のメッシュを作る (何度呼んでもよい)。ConcertHall が最初に呼んで、隠しておく</summary>
        public void Init()
        {
            if (mesh != null) return;
            columns = Mathf.Max(2, pleats) + 1; // 1枚あたりの列の数
            verts = new Vector3[columns * 4 * 2];
            mesh = new Mesh { name = "CurtainFabric" };
            mesh.MarkDynamic();
            var tris = new int[(columns - 1) * 12 * 2];
            int k = 0;
            for (int p = 0; p < 2; p++)
            {
                int baseV = p * columns * 4;
                for (int j = 0; j < columns - 1; j++)
                {
                    // 表 (内側から見える面) と裏 (外側から見える面) を別の頂点で作る (法線がそれぞれの向きになるように)。
                    // 2枚目は列が逆回りに並ぶので、面の向きを入れ替える
                    int a = baseV + j * 2, b = a + 1, c = a + 2, d = a + 3;
                    int o = columns * 2;
                    bool flip = p == 1;
                    if (!flip) { tris[k++] = a; tris[k++] = c; tris[k++] = b; tris[k++] = b; tris[k++] = c; tris[k++] = d; }
                    else { tris[k++] = a; tris[k++] = b; tris[k++] = c; tris[k++] = b; tris[k++] = d; tris[k++] = c; }
                    if (!flip) { tris[k++] = o + a; tris[k++] = o + b; tris[k++] = o + c; tris[k++] = o + b; tris[k++] = o + d; tris[k++] = o + c; }
                    else { tris[k++] = o + a; tris[k++] = o + c; tris[k++] = o + b; tris[k++] = o + b; tris[k++] = o + c; tris[k++] = o + d; }
                }
            }
            Build(0.001f, 0f);
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (fabric != null) fabric.sharedMesh = mesh;
            if (valance != null) valance.sharedMesh = ValanceMesh();
            if (lamp != null) lampIntensity = lamp.intensity;
        }

        /// <summary>カーテンの閉まり具合 (0 = 2枚とも真後ろにたぐり寄せてある 〜 1 = 2枚が正面で合わさって、ぐるりと閉まっている)</summary>
        public void SetCover(float k)
        {
            Init();
            Cover = Mathf.Clamp01(k);
            Build(Cover, Time.time);
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // 布の頂点: 2枚それぞれ、列 j ごとに上と下の2つ。角度は真後ろ (180 度) から、1枚目は左回り・2枚目は右回りに sweep まで
        // (閉まりきると正面で少し重なる)。ひだは列ごとに外・内へ交互に
        void Build(float k, float time)
        {
            float sweep = Mathf.Lerp(2f, 182f, k) * Mathf.Deg2Rad;
            float depth = pleatDepth * (1f + 2.5f * (1f - k)); // たぐり寄せてあるほど、ひだが深い
            int half = columns * 2;
            for (int p = 0; p < 2; p++)
            {
                int baseV = p * columns * 4;
                float sign = p == 0 ? 1f : -1f; // 1枚目: 真後ろから左を回って正面へ / 2枚目: 右を回って正面へ
                for (int j = 0; j < columns; j++)
                {
                    float u = j / (float)(columns - 1);
                    float a = Mathf.PI + sign * u * sweep;
                    float r = radius + depth * (j % 2 == 0 ? 1f : -1f) + (p == 0 ? 0.01f : 0f); // 正面で重なる所がちらつかないように、1枚目を少し外に
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    // 裾が少し揺れる (動いている先の方ほど大きく)
                    float sway = 0.03f * Mathf.Sin(time * 5f + u * 12f + p * 2f) * u;
                    var top = dir * r + Vector3.up * height;
                    var bottom = dir * (r + sway) + Vector3.up * 0.02f;
                    verts[baseV + j * 2] = top;
                    verts[baseV + j * 2 + 1] = bottom;
                    verts[baseV + half + j * 2] = top;
                    verts[baseV + half + j * 2 + 1] = bottom;
                }
            }
        }

        // 上飾り: レールの外側をぐるりと一周する、ひだのある短い布 (表裏)
        Mesh ValanceMesh()
        {
            int cols = 121;
            var v = new Vector3[cols * 4];
            var tris = new int[(cols - 1) * 12];
            int k = 0;
            for (int j = 0; j < cols; j++)
            {
                float a = j / (float)(cols - 1) * Mathf.PI * 2f;
                float r = radius + 0.06f + 0.025f * (j % 2 == 0 ? 1f : -1f);
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                v[j * 2] = v[cols * 2 + j * 2] = dir * r + Vector3.up * (height + 0.06f);
                v[j * 2 + 1] = v[cols * 2 + j * 2 + 1] = dir * r + Vector3.up * (height + 0.06f - valanceHeight);
            }
            for (int j = 0; j < cols - 1; j++)
            {
                int a = j * 2, b = a + 1, c = a + 2, d = a + 3, o = cols * 2;
                tris[k++] = a; tris[k++] = c; tris[k++] = b;
                tris[k++] = b; tris[k++] = c; tris[k++] = d;
                tris[k++] = o + a; tris[k++] = o + b; tris[k++] = o + c;
                tris[k++] = o + b; tris[k++] = o + d; tris[k++] = o + c;
            }
            var m = new Mesh { name = "CurtainValance", vertices = v, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>天井・レール・たぐり寄せたカーテンが、ふくらんで現れる</summary>
        public IEnumerator Appear(float duration)
        {
            Init();
            gameObject.SetActive(true);
            SetCover(0f);
            if (lamp != null) lamp.enabled = true;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                SetFrameDrop((1f - t) * (1f - t) * (1f - t)); // 上から降りてきて、ゆっくり止まる (下へ行きすぎない)
                if (lamp != null) lamp.intensity = lampIntensity * t;
                yield return null;
            }
            SetFrameDrop(0f);
            if (lamp != null) lamp.intensity = lampIntensity;
        }

        /// <summary>カーテンを from から to まで引く (閉める: 0 → 1 / 開ける: 1 → 0)</summary>
        public IEnumerator Draw(float from, float to, float duration)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float e = t * t * (3f - 2f * t);
                SetCover(Mathf.Lerp(from, to, e));
                yield return null;
            }
            SetCover(to);
        }

        /// <summary>天井・レール・カーテンが上へ去っていく</summary>
        public IEnumerator Vanish(float duration)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                SetFrameDrop(t * t);
                if (lamp != null) lamp.intensity = lampIntensity * (1f - t);
                yield return null;
            }
            HideInstant();
        }

        public void HideInstant()
        {
            Init();
            SetFrameDrop(1f);
            if (lamp != null) lamp.enabled = false;
            gameObject.SetActive(false);
        }

        // 天井・レール・カーテンを上へずらす (0 = 定位置 〜 1 = dropHeight だけ上。プレイヤーを下から押しつぶさないように、上から降りて上へ去る)
        void SetFrameDrop(float k)
        {
            if (frame == null) return;
            frame.localScale = Vector3.one;
            frame.localPosition = Vector3.up * dropHeight * k;
        }

        void Update()
        {
            // 閉まっている間も、裾が少し揺れる
            if (Cover > 0.01f && Time.frameCount % 2 == 0) SetCover(Cover);
        }

    }
}
