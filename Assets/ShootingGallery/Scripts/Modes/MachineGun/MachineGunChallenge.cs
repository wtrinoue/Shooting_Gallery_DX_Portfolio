using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// マシンガンチャレンジの「横長の屋台とカップ」の出し入れ。
    /// 進行 (屋台を引っ込める・マシンガンを渡す・終了の判定) は GameManager が行い、ここは屋台の出し入れと時間・当てた数だけを受け持つ。
    /// 引っ込めた屋台 (撃った屋台と両隣) が沈むのと同時に、同じ方向に横長の屋台がせり上がり、
    /// カップが左から順に現れる。カップは撃たれると後ろへ弾き飛ばされて落ちる (Target.knockOffOnHit)。
    /// カップの並べ方 (棚 / ピラミッド / 回転台 / 動く棚) は毎回 layouts からランダムに選ぶ。
    /// ピラミッドのように積んだカップは、下のカップが落ちると上のカップも崩れ落ちる (links)。
    /// </summary>
    public class MachineGunChallenge : MonoBehaviour
    {
        /// <summary>積んだカップの「下」と「その上に乗っているカップ」の組</summary>
        [System.Serializable]
        public struct CupLink
        {
            public Target lower;
            public Target upper;
        }

        /// <summary>カップの並べ方 1つ分</summary>
        [System.Serializable]
        public class Layout
        {
            public string name;
            [Tooltip("この並べ方の台とカップをまとめた親。選ばれた時だけ表示する")]
            public GameObject root;
            [Tooltip("カップ (kind = MachineGunChallengeTarget, manualControl, knockOffOnHit)")]
            public Target[] targets;
            [Tooltip("lower が落ちると upper も崩れ落ちる")]
            public CupLink[] links;
        }

        [Tooltip("横長の屋台。カップはこの子にあり、Stall.targets には入れない (沈む時にカップを消さず、乗せたまま沈めるため)")]
        public Stall stall;
        [Tooltip("カップの並べ方。チャレンジのたびにランダムで1つ選ぶ (前回と同じものは続けて出さない)")]
        public Layout[] layouts;

        [Header("Bombs (並ぶたびに、ランダムなカップが爆弾に変わる)")]
        [Tooltip("爆弾の見た目 (使い回す)。この数だけ爆弾が混ざる。カップの「顔」(Target.face) と付け替える")]
        public GameObject[] bombLooks;
        [Tooltip("爆弾が爆発した時に、周りのカップを落とす半径 (m)")]
        public float bombRadius = 0.6f;

        [Header("Timing")]
        [Tooltip("横長の屋台がせり上がる / 沈む秒数")]
        public float riseDuration = 1.2f;
        [Tooltip("左端から右端までカップが現れ終わるまでの秒数")]
        public float appearSpread = 0.6f;
        [Tooltip("マシンガンを撃てる秒数 (カップが並び終わってから)")]
        public float timeLimit = 8f;
        [Tooltip("下のカップが落ちてから、上のカップが崩れ落ちるまでの秒数")]
        public float collapseDelay = 0.08f;

        public bool IsActive { get; private set; }
        /// <summary>屋台がせり上がり終わり、カップが出始めたか (ここでマシンガンを渡す)</summary>
        public bool IsReady { get; private set; }
        /// <summary>マシンガンを渡す秒数 (カップが現れる時間 + 撃てる時間)</summary>
        public float ShootDuration => appearSpread + timeLimit;
        /// <summary>チャレンジ全体の最長の秒数 (屋台の入れ替え + 撃つ時間)</summary>
        public float Duration => riseDuration + ShootDuration + 1.5f; // (+ 引っ込める屋台のハッチが消えるのを待つ分)
        /// <summary>撃てる残り時間。屋台がせり上がる前は減らない</summary>
        public float TimeLeft => !IsActive ? 0f : IsReady ? Mathf.Max(0f, ShootDuration - (Time.time - readyTime)) : ShootDuration;
        /// <summary>今回の並べ方の名前</summary>
        public string LayoutName => current != null ? current.name : "";
        public int Total => current != null && current.targets != null ? current.targets.Length : 0;
        public int Hits { get; private set; }
        public bool AllCleared => Total > 0 && Hits >= Total;
        /// <summary>Hide してから横長の屋台が沈み始めるまでの秒数 (元の屋台はこの時に同時にせり上げる)</summary>
        public float SwapBackDelay => HideWait;
        const float HideWait = 0.3f; // 最後のカップが落ちるのを少し見せる
        /// <summary>しまう時に沈む秒数 (出てくる時より速く。元の屋台がハッチの消えた所からすぐ戻れるように)</summary>
        public const float SinkTime = 0.7f;

        float readyTime;
        Coroutine anim;
        Layout current;
        int lastLayout = -1;
        readonly List<Target> bombs = new List<Target>(); // 今回爆弾に変わっているカップ

        /// <summary>このカップは今回爆弾か</summary>
        public bool IsBomb(Target t) => bombs.Contains(t);

        void Awake()
        {
            foreach (var layout in layouts)
            {
                foreach (var t in layout.targets) if (t != null) t.SetAvailable(false);
                if (layout.root != null) layout.root.SetActive(false);
            }
            foreach (var look in bombLooks) if (look != null) look.SetActive(false);
        }

        /// <summary>
        /// 横長の屋台を出す。center = 輪の中心 (プレイヤーの足元, ワールド座標), direction = 屋台を出す向き (水平),
        /// radius = 中心から屋台までの距離。 delay = せり上がり始めるまでの秒数。
        /// </summary>
        public void Show(Vector3 center, Vector3 direction, float radius, float delay = 0f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            IsActive = true;
            IsReady = false;
            Hits = 0;

            // 屋台の置き場所は親 (ShootingGallery) のローカル座標で渡す
            var parent = stall.transform.parent;
            Vector3 worldPos = center + direction * radius;
            var worldRot = Quaternion.LookRotation(direction); // 屋台の表 (-Z) がプレイヤーを向く
            stall.PlaceAt(parent != null ? parent.InverseTransformPoint(worldPos) : worldPos,
                          parent != null ? Quaternion.Inverse(parent.rotation) * worldRot : worldRot);
            ChooseLayout();
            foreach (var t in current.targets) if (t != null) t.SetAvailable(true); // manualControl なので隠れたまま Raise を待つ
            PlaceBombs();
            Play(ShowRoutine(delay));
        }

        /// <summary>並べ方をランダムに選び、その台とカップだけを表示する</summary>
        void ChooseLayout()
        {
            // 前回の片付けが途中で止まっていても、前のカップは全部しまっておく
            foreach (var layout in layouts)
                foreach (var t in layout.targets) if (t != null) t.SetAvailable(false);

            int n = layouts.Length;
            int i = Random.Range(0, n);
            if (n > 1 && i == lastLayout) i = (i + Random.Range(1, n)) % n;
            lastLayout = i;
            current = layouts[i];
            for (int k = 0; k < n; k++)
                if (layouts[k].root != null) layouts[k].root.SetActive(k == i);
        }

        IEnumerator ShowRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay); // 引っ込める屋台の足元のハッチが閉じて消えきるのを待つ (ハッチが重ならないように)
            stall.Rise(riseDuration, false);
            yield return new WaitForSeconds(riseDuration);

            IsReady = true;
            readyTime = Time.time;
            // 左から順に現れる (屋台から見た横位置の小さい順。台に乗って動いているカップは今の位置で決める)
            var order = new List<Target>(current.targets);
            order.RemoveAll(t => t == null);
            var appearAt = new Dictionary<Target, float>();
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var t in order)
            {
                float x = stall.transform.InverseTransformPoint(t.transform.position).x;
                appearAt[t] = x;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }
            foreach (var t in order) appearAt[t] = readyTime + appearSpread * Mathf.InverseLerp(minX, maxX, appearAt[t]);
            order.Sort((a, b) => appearAt[a].CompareTo(appearAt[b]));
            foreach (var t in order)
            {
                while (Time.time < appearAt[t]) yield return null;
                if (IsActive) t.Raise(ShootDuration + 60f); // 片付ける時は屋台ごと沈める
            }
            anim = null;
        }

        /// <summary>カップに当たった。残りの数を返す</summary>
        public int RegisterHit()
        {
            if (!IsActive) return Total - Hits;
            Hits++;
            return Mathf.Max(0, Total - Hits);
        }

        /// <summary>カップが落ちた: その上に積んであったカップを少し後に崩れ落とす (崩れたカップも当てた数と点数に入る)</summary>
        public void OnCupHit(Target cup)
        {
            if (!IsActive || current == null || current.links == null) return;
            foreach (var link in current.links)
                if (link.lower == cup && link.upper != null) StartCoroutine(Collapse(link.upper));
        }

        IEnumerator Collapse(Target upper)
        {
            yield return new WaitForSeconds(collapseDelay);
            if (IsActive && upper.IsUp) upper.Hit(upper.visual.position + Vector3.up * 0.09f);
        }

        /// <summary>残ったカップを乗せたまま屋台を沈めて片付ける</summary>
        public void Hide()
        {
            if (!IsActive) return;
            IsActive = false;
            IsReady = false;
            Play(HideRoutine());
        }

        IEnumerator HideRoutine()
        {
            yield return new WaitForSeconds(HideWait);
            stall.Sink(SinkTime); // (しまう時は、テンポよく一気に沈む)
            yield return new WaitForSeconds(SinkTime + 0.1f);
            foreach (var t in current.targets) if (t != null) t.SetAvailable(false);
            ClearBombs();
            anim = null;
        }

        /// <summary>ランダムなカップを爆弾の数だけ選び、カップの見た目を爆弾に付け替える</summary>
        void PlaceBombs()
        {
            ClearBombs();
            var pool = new List<Target>();
            foreach (var t in current.targets) if (t != null && t.face != null) pool.Add(t);
            foreach (var look in bombLooks)
            {
                if (look == null || pool.Count == 0) continue;
                int i = Random.Range(0, pool.Count);
                var t = pool[i];
                pool.RemoveAt(i);
                t.face.SetActive(false);
                look.transform.SetParent(t.visual, false);
                look.transform.localPosition = Vector3.zero;
                look.transform.localRotation = Quaternion.identity;
                look.SetActive(true);
                t.knockOffOnHit = false; // 爆弾は飛ばずに、その場で爆発して消える
                bombs.Add(t);
            }
        }

        /// <summary>爆弾をカップに戻す</summary>
        void ClearBombs()
        {
            foreach (var t in bombs)
            {
                t.face.SetActive(true);
                t.knockOffOnHit = true;
            }
            bombs.Clear();
            foreach (var look in bombLooks)
            {
                if (look == null) continue;
                look.SetActive(false);
                look.transform.SetParent(transform, false);
            }
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
