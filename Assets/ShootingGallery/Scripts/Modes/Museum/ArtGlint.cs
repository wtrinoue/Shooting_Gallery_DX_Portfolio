using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 美術館モードの、まだ当てられる (完成していない) 作品の上を流れる光の帯。ガラスの照り返しのように、
    /// 斜め (袈裟懸け) の白い帯が左上から右下へすっと流れ、少し間をあけてくり返す。作品が完成すると出なくなる。
    /// 帯は作品の大きさの四角形 (size) の中だけに描く (毎フレーム、帯を四角形で切り取ったメッシュを作り直す)。
    /// キャンバスは絵の面に沿って、彫刻と吊るした作品はプレイヤーの方を向いた面に流す (faceCamera)
    /// </summary>
    public class ArtGlint : MonoBehaviour
    {
        public Artwork art;
        [Tooltip("帯を流す面の基準 (この物のローカル XY 面。表 = -Z)")]
        public Transform anchor;
        [Tooltip("面の真ん中 (anchor のローカル) / 面の大きさ (anchor のローカルの幅・高さ)")]
        public Vector3 center;
        public Vector2 size = Vector2.one;
        [Tooltip("いつもプレイヤーの方を向ける (彫刻・吊るした作品)。その時は、作品の手前へ depth (anchor のローカル) だけ出す")]
        public bool faceCamera;
        public float depth;
        public Material material;
        [Tooltip("帯が端から端まで流れる秒数 / 次に流れるまでの間 (秒) / 帯の太さ (面の短い辺に対する割合)")]
        public float sweepTime = 0.75f;
        public float pause = 1.2f;
        public float bandWidth = 0.28f;

        Transform plane;
        MeshRenderer mr;
        Mesh mesh;
        float phase;
        readonly System.Collections.Generic.List<Vector3> verts = new System.Collections.Generic.List<Vector3>();
        readonly System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>();
        static readonly Vector2 Dir = new Vector2(1f, -1f).normalized; // 流れる向き (左上 → 右下)

        void Start()
        {
            if (anchor == null) anchor = transform;
            var go = new GameObject("Glint");
            plane = go.transform;
            plane.SetParent(anchor, false);
            plane.localPosition = center;
            mesh = new Mesh { name = "ArtGlint" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;
            phase = Random.value * 5f; // (作品ごとに、流れる時をずらす)
        }

        [Tooltip("吊るした作品: 平らな板に描かず、この物の中の形 (メッシュ) の表面ぜんたいを、ぐるりと包むように光の帯を流す。" +
                 "形の少し外側に、同じ形の薄い殻を作って、頂点の色 (透明さ) で帯を描く (wrapMaterial は頂点の色を使う材質)")]
        public Transform wrapSource;
        public Material wrapMaterial;
        [Tooltip("殻を形から外へふくらませる量 (m)")]
        public float wrapInflate = 0.015f;

        class Shell
        {
            public Transform tr;
            public MeshRenderer mr;
            public Mesh mesh;
            public Vector3[] local, normals;
            public Color32[] colors;
        }
        System.Collections.Generic.List<Shell> shells;

        // 包む殻を作る (wrapSource の中のメッシュごとに、ふくらませた写し)
        void BuildShells()
        {
            shells = new System.Collections.Generic.List<Shell>();
            foreach (var mf in wrapSource.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var src = mf.sharedMesh;
                var v = src.vertices;
                var nrm = src.normals;
                if (nrm == null || nrm.Length != v.Length) continue;
                var scale = mf.transform.lossyScale;
                var inflated = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    // (大きさの違う向きでも、同じ厚みだけ外へ)
                    var off = new Vector3(nrm[i].x / Mathf.Max(0.001f, scale.x), nrm[i].y / Mathf.Max(0.001f, scale.y), nrm[i].z / Mathf.Max(0.001f, scale.z));
                    inflated[i] = v[i] + off * wrapInflate;
                }
                var mesh = new Mesh { name = "ArtGlintShell" };
                mesh.MarkDynamic();
                mesh.vertices = inflated;
                mesh.normals = nrm;
                mesh.triangles = src.triangles;
                var colors = new Color32[v.Length];
                mesh.colors32 = colors;
                mesh.RecalculateBounds();
                var go = new GameObject("GlintShell");
                go.transform.SetParent(mf.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = wrapMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                shells.Add(new Shell { tr = go.transform, mr = r, mesh = mesh, local = inflated, normals = nrm, colors = colors });
            }
        }

        // 包む帯: プレイヤーから見た形の上を、左上から右下へ流れる。プレイヤーの方を向いた面ほど明るい (照り返し)
        void UpdateWrap(bool on, float t)
        {
            if (shells == null) BuildShells();
            var cam = Camera.main;
            if (!on || t > 1f || cam == null)
            {
                foreach (var s in shells) s.mr.enabled = false;
                return;
            }
            Vector3 c = anchor.TransformPoint(center);
            Vector3 toCam = cam.transform.position - c;
            if (toCam.sqrMagnitude < 0.0001f) toCam = -anchor.forward;
            toCam.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, toCam).normalized; // (プレイヤーから見て右)
            if (right.sqrMagnitude < 0.01f) right = cam.transform.right;
            right = -right;
            Vector3 up = Vector3.Cross(toCam, right).normalized;
            if (Vector3.Dot(up, Vector3.up) < 0f) up = -up;
            float sx = size.x * anchor.lossyScale.x, sy = size.y * anchor.lossyScale.y;
            float bw = bandWidth * Mathf.Min(sx, sy);
            float half = (sx * Mathf.Abs(Dir.x) + sy * Mathf.Abs(Dir.y)) * 0.5f;
            float pos = Mathf.Lerp(-half - bw, half + bw, t * t * (3f - 2f * t));
            foreach (var s in shells)
            {
                var m = s.tr.localToWorldMatrix;
                for (int i = 0; i < s.local.Length; i++)
                {
                    Vector3 w = m.MultiplyPoint3x4(s.local[i]) - c;
                    float d = Vector2.Dot(new Vector2(Vector3.Dot(w, right), Vector3.Dot(w, up)), Dir);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - pos) / (bw * 0.5f));             // 太い帯
                    a = Mathf.Max(a, 0.6f * Mathf.Clamp01(1f - Mathf.Abs(d - (pos - bw * 1.3f)) / (bw * 0.15f))); // 後ろを追う細い帯
                    float facing = Vector3.Dot(m.MultiplyVector(s.normals[i]).normalized, toCam);
                    a *= Mathf.Clamp01(0.25f + facing * 0.9f);
                    s.colors[i] = new Color32(255, 255, 255, (byte)(a * 0.85f * 255f));
                }
                s.mesh.colors32 = s.colors;
                s.mr.enabled = true;
            }
        }

        void LateUpdate()
        {
            if (wrapSource != null && wrapMaterial != null)
            {
                bool wrapOn = art != null && art.IsBlank;
                float cyc = sweepTime + pause;
                UpdateWrap(wrapOn, Mathf.Repeat(Time.time + phase, cyc) / sweepTime);
                return;
            }
            if (mr == null) return;
            bool on = art != null && art.IsBlank && material != null;
            float cycle = sweepTime + pause;
            float t = Mathf.Repeat(Time.time + phase, cycle) / sweepTime;
            if (!on || t > 1f) { mr.enabled = false; return; }

            if (faceCamera && Camera.main != null)
            {
                Vector3 c = anchor.TransformPoint(center);
                Vector3 toCam = Camera.main.transform.position - c;
                toCam.y = 0f;
                if (toCam.sqrMagnitude < 0.0001f) toCam = -anchor.forward;
                toCam.Normalize();
                plane.SetPositionAndRotation(c + toCam * (depth * anchor.lossyScale.x), Quaternion.LookRotation(-toCam)); // 表 (-Z) をプレイヤーへ
            }
            else
            {
                plane.localPosition = center;
                plane.localRotation = Quaternion.identity;
            }

            // 帯の真ん中の位置 (流れる向きにそって、四角形の外から外まで)
            float bw = bandWidth * Mathf.Min(size.x, size.y);
            float half = (size.x * Mathf.Abs(Dir.x) + size.y * Mathf.Abs(Dir.y)) * 0.5f;
            float s = Mathf.Lerp(-half - bw, half + bw, t * t * (3f - 2f * t));
            verts.Clear();
            tris.Clear();
            AddBand(s, bw);                     // 太い帯
            AddBand(s - bw * 1.3f, bw * 0.3f);  // 後ろを追う細い帯
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mr.enabled = tris.Count > 0;
        }

        // 流れる向きにそって s を真ん中に、太さ w の帯を、四角形で切り取って足す
        void AddBand(float s, float w)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f;
            var poly = new System.Collections.Generic.List<Vector2> { new Vector2(-hx, hy), new Vector2(hx, hy), new Vector2(hx, -hy), new Vector2(-hx, -hy) };
            poly = Clip(poly, Dir, s - w * 0.5f);   // dot(p, Dir) >= s - w/2
            poly = Clip(poly, -Dir, -(s + w * 0.5f)); // dot(p, Dir) <= s + w/2
            if (poly.Count < 3) return;
            int first = verts.Count;
            foreach (var p in poly) verts.Add(new Vector3(p.x, p.y, 0f));
            // 表 (-Z) から見えるように並べる
            for (int i = 1; i < poly.Count - 1; i++)
            {
                Vector3 a = verts[first], b = verts[first + i], c = verts[first + i + 1];
                if (Vector3.Cross(b - a, c - a).z > 0f) { tris.Add(first); tris.Add(first + i + 1); tris.Add(first + i); }
                else { tris.Add(first); tris.Add(first + i); tris.Add(first + i + 1); }
            }
        }

        // 多角形を、dot(p, n) >= d の側だけ残して切り取る
        static System.Collections.Generic.List<Vector2> Clip(System.Collections.Generic.List<Vector2> poly, Vector2 n, float d)
        {
            var result = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                float da = Vector2.Dot(a, n) - d, db = Vector2.Dot(b, n) - d;
                if (da >= 0f) result.Add(a);
                if ((da >= 0f) != (db >= 0f)) result.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            return result;
        }
    }
}
