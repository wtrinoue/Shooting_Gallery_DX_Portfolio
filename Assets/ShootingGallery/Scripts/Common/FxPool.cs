using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 使い捨ての見た目 (爆発・点数の文字・弾の軌跡・砂粒・紙吹雪・破片・弾など) を使い回す入れ物。
    /// 毎回作って消すと、作る手間と消した後のごみ集め (GC) でときどきカクッとなるので、使い終わった物はしまっておき、次に同じ種類が要る時に出し直す。
    /// 種類は名前 (key) で分ける。しまう時は非表示にしてこのオブジェクトの子にする。実行中に自動で作られる (シーンに置く必要はない)。
    /// </summary>
    public class FxPool : MonoBehaviour
    {
        const int MaxPerKind = 300; // 1種類あたりしまっておく最大の数 (これを超えた分は消す)

        static FxPool instance;
        readonly Dictionary<string, Stack<GameObject>> free = new Dictionary<string, Stack<GameObject>>();
        readonly List<(string key, GameObject go, float at)> timed = new List<(string, GameObject, float)>();

        static FxPool Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("FxPool").AddComponent<FxPool>();
                return instance;
            }
        }

        /// <summary>
        /// key の種類を1つ出す。しまってある物があればそれを、なければ create で作る。parent の子にして表示した状態で返す
        /// (位置・大きさなどは呼んだ側で毎回設定すること)
        /// </summary>
        public static GameObject Get(string key, System.Func<GameObject> create, Transform parent = null)
        {
            var pool = Instance;
            if (pool.free.TryGetValue(key, out var stack))
            {
                while (stack.Count > 0)
                {
                    var go = stack.Pop();
                    if (go == null) continue; // シーンの切り替えなどで消えていた
                    go.transform.SetParent(parent, false);
                    go.SetActive(true);
                    return go;
                }
            }
            var created = create();
            created.name = key;
            created.transform.SetParent(parent, false);
            return created;
        }

        /// <summary>使い終わった物をしまう (非表示にして、次に Get された時に使い回す)</summary>
        public static void Release(string key, GameObject go)
        {
            if (go == null) return;
            if (instance == null) { Destroy(go); return; }
            if (go.transform.parent == instance.transform && !go.activeSelf) return; // もうしまってある
            if (!instance.free.TryGetValue(key, out var stack)) instance.free[key] = stack = new Stack<GameObject>();
            if (stack.Count >= MaxPerKind) { Destroy(go); return; }
            go.SetActive(false);
            go.transform.SetParent(instance.transform, false);
            stack.Push(go);
        }

        /// <summary>seconds 秒後にしまう (弾の軌跡・刺さった手裏剣など。親が非表示になっていても時間どおりにしまう)</summary>
        public static void ReleaseAfter(string key, GameObject go, float seconds)
        {
            if (go == null) return;
            Instance.timed.Add((key, go, Time.time + seconds));
        }

        void Update()
        {
            float now = Time.time;
            for (int i = timed.Count - 1; i >= 0; i--)
            {
                var (key, go, at) = timed[i];
                if (now < at) continue;
                timed.RemoveAt(i);
                Release(key, go);
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>見た目だけのプリミティブを作る (当たり判定なし・影なし)。プールの create に使う</summary>
        public static GameObject Primitive(PrimitiveType type, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            DestroyImmediate(go.GetComponent<Collider>()); // すぐ消す (同じフレームの当たり判定に入らないように)
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }
}
