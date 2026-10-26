using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 屋台の足元の長方形のハッチ。屋台がすっぽり入る大きさの穴を、2枚の扉がふさぐ。
    /// 扉は長い方の向きに、両はしの床の下へすべり込むように開き、真ん中で合わさるように閉じる。
    /// 屋台がせり上がる (Stall.Rise) / しまわれる (Stall.Sink) 時だけ床に出てくる。ほかの動きの時は小さくなって消える
    /// </summary>
    public class StallHatch : MonoBehaviour
    {
        /// <summary>GameManager がビルダーの作った材質を渡す: 0 = 枠, 1 = 穴のマスク (HatchMask), 2 = 扉, 3 = 扉の合わせ目の線, 4 = 穴の中の壁</summary>
        public static Material[] materials;

        public static bool Available => materials != null && materials.Length >= 5 && materials[0] != null && materials[1] != null;

        const float GrowTime = 0.15f, ShrinkTime = 0.2f, Frame = 0.08f;
        const float Margin = 0.06f; // 屋台のいちばん外側から穴のふちまで (ぎりぎり囲む)
        /// <summary>屋台のいちばん外側から、ハッチの枠の外側まで (屋台を並べる時に、隣のハッチと重ならないように空ける幅)</summary>
        public const float Outset = Margin + Frame;

        Transform body;    // 穴の真ん中に置いた、ハッチ全部の親 (ここを中心にふくらむ / 縮む)
        Transform doorA, doorB, seamA, seamB;
        Vector3 axis;      // 扉が動く向き (長い方)
        float length, span; // 長い方の長さ / 短い方の長さ
        float open;        // 0 = 閉じている, 1 = 開ききった
        Coroutine routine;
        GameObject platform; // 屋台の足元の床板 (屋台の子。屋台といっしょに上下し、上がりきると穴をふさいで床と同じ高さになる)

        public bool Shown => gameObject.activeSelf;

        /// <summary>
        /// stall の足元に作る (stall と同じ親の下。置き場所は Place で合わせる)。exclude (屋根の上に隠したパラシュート) は大きさに数えない。
        /// depth = 穴の深さ (しまった屋台が入りきる深さ)
        /// </summary>
        public static StallHatch Create(Stall stall, Vector3 restScale, Transform exclude, float minDepth, out float top, out float depth)
        {
            // 屋台のメッシュ全部を、屋台の中の座標で囲む (屋根やひさしのはみ出しも含めて、すっぽり入るように)
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = -min;
            foreach (var mf in stall.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (exclude != null && mf.transform.IsChildOf(exclude)) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = stall.transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            if (min.x > max.x) { min = new Vector3(-1.8f, 0f, -1.5f); max = new Vector3(1.8f, 3f, 1f); }
            min = Vector3.Scale(min, restScale);
            max = Vector3.Scale(max, restScale);
            top = max.y;
            depth = Mathf.Max(minDepth, top + 0.3f); // 屋台のてっぺんまで入りきる深さ (浅いと、閉じた扉に屋台がはさまる)

            var go = new GameObject($"{stall.name}_Hatch");
            go.transform.SetParent(stall.transform.parent, false);
            var hatch = go.AddComponent<StallHatch>();
            hatch.Build(new Vector3((min.x + max.x) / 2f, 0f, (min.z + max.z) / 2f), max.x - min.x + Margin * 2f, max.z - min.z + Margin * 2f, depth);
            hatch.platform = Platform(stall, restScale, new Vector3((min.x + max.x) / 2f, 0f, (min.z + max.z) / 2f), max.x - min.x + Margin * 2f - 0.02f, max.z - min.z + Margin * 2f - 0.02f);
            go.SetActive(false);
            return hatch;
        }

        // 屋台の子として、穴と同じ大きさの床板を作る (center / w / d は屋台の向きのまま、親の大きさでの値)。ハッチを出している間だけ見せる
        static GameObject Platform(Stall stall, Vector3 restScale, Vector3 center, float w, float d)
        {
            const float Thick = 0.03f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "HatchPlatform";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(stall.transform, false);
            go.transform.localPosition = new Vector3(center.x / restScale.x, -Thick / 2f / restScale.y, center.z / restScale.z);
            go.transform.localScale = new Vector3(w / restScale.x, Thick / restScale.y, d / restScale.z);
            go.GetComponent<MeshRenderer>().sharedMaterial = materials[2];
            go.SetActive(false);
            return go;
        }

        void SetHidden()
        {
            gameObject.SetActive(false);
            if (platform != null) platform.SetActive(false);
        }

        void Build(Vector3 center, float w, float d, float depth)
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localPosition = center;
            var c = Vector3.zero;
            bool alongX = w >= d;
            axis = alongX ? Vector3.right : Vector3.forward;
            length = alongX ? w : d;
            span = alongX ? d : w;
            var side = alongX ? Vector3.forward : Vector3.right;

            // 穴: 床の高さに色を出さない板 (ここだけ地面が描かれない) と、その下のしまう所 (灰色の壁と底)
            Box("HoleMask", c + Vector3.up * 0.001f, Size(length, 0.002f, span), materials[1]);
            const float Wall = 0.04f;
            foreach (float s in new[] { -1f, 1f })
            {
                Box("PitWallLong", c + side * s * (span + Wall) / 2f + Vector3.down * depth / 2f, Size(length + Wall * 2f, depth, Wall), materials[4]);
                Box("PitWallShort", c + axis * s * (length + Wall) / 2f + Vector3.down * depth / 2f, Size(Wall, depth, span), materials[4]);
            }
            Box("PitFloor", c + Vector3.down * (depth + Wall / 2f), Size(length + Wall * 2f, Wall, span + Wall * 2f), materials[4]);
            // 枠: 穴のまわりの金属の縁
            foreach (float s in new[] { -1f, 1f })
            {
                Box("FrameLong", c + side * s * (span + Frame) / 2f + Vector3.up * 0.01f, Size(length + Frame * 2f, 0.02f, Frame), materials[0]);
                Box("FrameShort", c + axis * s * (length + Frame) / 2f + Vector3.up * 0.01f, Size(Frame, 0.02f, span), materials[0]);
            }
            doorA = Box("Door", c, Vector3.one, materials[2]);
            doorB = Box("Door", c, Vector3.one, materials[2]);
            seamA = Box("DoorSeam", c, Vector3.one, materials[3]);
            seamB = Box("DoorSeam", c, Vector3.one, materials[3]);
            SetOpen(0f);
        }

        // 長い方 (axis) の長さ l、高さ h、短い方の長さ s を、ローカルの大きさにする
        Vector3 Size(float l, float h, float s) => axis == Vector3.right ? new Vector3(l, h, s) : new Vector3(s, h, l);

        Transform Box(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(body, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        // 2枚の扉: それぞれ両はしにくっついたまま、見えている長さが短くなる (はしの床の下へすべり込んで見える)
        void SetOpen(float k)
        {
            open = Mathf.Clamp01(k);
            float half = length / 2f, len = Mathf.Max(0.001f, half * (1f - open));
            const float SeamW = 0.05f;
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                var end = axis * s * half;
                var door = i == 0 ? doorA : doorB;
                var seam = i == 0 ? seamA : seamB;
                door.localPosition = end - axis * s * len / 2f + Vector3.up * 0.012f;
                door.localScale = Size(len, 0.016f, span);
                door.gameObject.SetActive(open < 0.999f);
                // 扉の先 (合わせ目) の黄色い線
                seam.localPosition = end - axis * s * Mathf.Max(0f, len - SeamW / 2f) + Vector3.up * 0.0215f;
                seam.localScale = Size(Mathf.Min(SeamW, len), 0.003f, span);
                seam.gameObject.SetActive(open < 0.999f);
            }
        }

        /// <summary>止まる場所 (親のローカル座標) に合わせる</summary>
        public void Place(Vector3 localPos, Quaternion localRot)
        {
            transform.localPosition = localPos;
            transform.localRotation = localRot;
        }

        /// <summary>閉じた扉を出して (消えていたらふくらんで出る)、time 秒で開く。開ききるまでの秒数を返す</summary>
        public float Open(float time)
        {
            if (!Shown) SetOpen(0f);
            float remain = time * (1f - open) + (Shown && body.localScale.x > 0.999f ? 0f : GrowTime);
            Run(OpenRoutine(time));
            return remain;
        }

        IEnumerator OpenRoutine(float time)
        {
            if (body.localScale.x < 0.999f) yield return Grow(true);
            yield return Slide(1f, time);
            routine = null;
        }

        /// <summary>
        /// 開いた穴を出しておき (消えていたらふくらんで出る)、delay 秒たったら time 秒で閉じ、少しおいて小さくなって消える
        /// (屋台が床の下に入りきってから閉じる)
        /// </summary>
        /// <returns>ハッチが消えきるまでの秒数</returns>
        public float CloseAfter(float delay, float time)
        {
            float grow = Shown && body.localScale.x > 0.999f ? 0f : GrowTime;
            if (!Shown) SetOpen(1f);
            Run(CloseRoutine(delay, time));
            return grow + delay + time + 0.25f + ShrinkTime;
        }

        IEnumerator CloseRoutine(float delay, float time)
        {
            if (body.localScale.x < 0.999f) yield return Grow(true);
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return Slide(0f, time);
            yield return new WaitForSeconds(0.25f);
            yield return Grow(false);
            SetHidden();
            routine = null;
        }

        /// <summary>地面に立っている屋台の足元に、開いたハッチを出しておく (もう出ていれば何もしない)。instant ならその場で出す</summary>
        public void ShowOpen(bool instant = false)
        {
            if (Shown && routine == null && open > 0.999f && body.localScale.x > 0.999f) return;
            if (!Shown) SetOpen(1f); // 屋台が立っているので、扉は開いた形で出す (閉じた扉から開くと屋台にかぶる)
            if (instant)
            {
                if (routine != null) StopCoroutine(routine);
                routine = null;
                gameObject.SetActive(true);
                if (platform != null) platform.SetActive(true);
                body.localScale = Vector3.one;
                SetOpen(1f);
                return;
            }
            Run(OpenRoutine(0.2f));
        }

        /// <summary>しまう (せり上がり / しまう以外の動きの時)。instant ならその場で消す</summary>
        public void Hide(bool instant = false)
        {
            if (!Shown) return;
            if (instant)
            {
                if (routine != null) StopCoroutine(routine);
                routine = null;
                body.localScale = Vector3.one;
                SetHidden();
                return;
            }
            Run(HideRoutine());
        }

        IEnumerator HideRoutine()
        {
            yield return Grow(false);
            SetHidden();
            routine = null;
        }

        void Run(IEnumerator r)
        {
            if (routine != null) StopCoroutine(routine);
            if (!Shown)
            {
                body.localScale = Vector3.one * 0.001f;
                if (platform != null) platform.SetActive(true);
                gameObject.SetActive(true);
            }
            routine = StartCoroutine(r);
        }

        // 穴の真ん中を中心に、ふくらんで出る / 小さくなって消える
        IEnumerator Grow(bool show)
        {
            float from = body.localScale.x, to = show ? 1f : 0.001f, time = show ? GrowTime : ShrinkTime;
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                float e = show ? 1f - (1f - t) * (1f - t) : t * t;
                body.localScale = Vector3.one * Mathf.Lerp(from, to, e);
                yield return null;
            }
            body.localScale = Vector3.one * to;
        }

        IEnumerator Slide(float to, float time)
        {
            float from = open;
            if (Mathf.Approximately(from, to) || time <= 0f) { SetOpen(to); yield break; }
            for (float t = 0f; t < 1f; t += Time.deltaTime / (time * Mathf.Abs(to - from)))
            {
                SetOpen(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            SetOpen(to);
        }
    }
}
