using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// マシンガンチャレンジの「横長の屋台とカップ」の出し入れ。
    /// 進行 (屋台を引っ込める・マシンガンを渡す・終了の判定) は GameManager が行い、ここは屋台の出し入れと時間・当てた数だけを受け持つ。
    /// 引っ込めた屋台 (撃った屋台と両隣) が沈むのと同時に、同じ方向に横長の屋台がせり上がり、
    /// 棚に並んだカップが左から順に現れる。カップは撃たれると棚の後ろへ弾き飛ばされて落ちる (Target.knockOffOnHit)。
    /// </summary>
    public class MachineGunChallenge : MonoBehaviour
    {
        [Tooltip("横長の屋台。カップはこの子にあり、Stall.targets には入れない (沈む時にカップを消さず、乗せたまま沈めるため)")]
        public Stall stall;
        [Tooltip("棚に並んだカップ (kind = MachineGunChallengeTarget, manualControl, knockOffOnHit)")]
        public Target[] targets;

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

        public bool IsActive { get; private set; }
        /// <summary>屋台がせり上がり終わり、カップが出始めたか (ここでマシンガンを渡す)</summary>
        public bool IsReady { get; private set; }
        /// <summary>マシンガンを渡す秒数 (カップが現れる時間 + 撃てる時間)</summary>
        public float ShootDuration => appearSpread + timeLimit;
        /// <summary>チャレンジ全体の最長の秒数 (屋台の入れ替え + 撃つ時間)</summary>
        public float Duration => riseDuration + ShootDuration;
        /// <summary>撃てる残り時間。屋台がせり上がる前は減らない</summary>
        public float TimeLeft => !IsActive ? 0f : IsReady ? Mathf.Max(0f, ShootDuration - (Time.time - readyTime)) : ShootDuration;
        public int Total => targets != null ? targets.Length : 0;
        public int Hits { get; private set; }
        public bool AllCleared => Total > 0 && Hits >= Total;
        /// <summary>Hide してから横長の屋台が沈み始めるまでの秒数 (元の屋台はこの時に同時にせり上げる)</summary>
        public float SwapBackDelay => HideWait;
        const float HideWait = 0.3f; // 最後のカップが落ちるのを少し見せる

        float readyTime;
        Coroutine anim;
        readonly List<Target> bombs = new List<Target>(); // 今回爆弾に変わっているカップ

        /// <summary>このカップは今回爆弾か</summary>
        public bool IsBomb(Target t) => bombs.Contains(t);

        void Awake()
        {
            foreach (var t in targets) if (t != null) t.SetAvailable(false);
            foreach (var look in bombLooks) if (look != null) look.SetActive(false);
        }

        /// <summary>
        /// 横長の屋台を出す。center = 輪の中心 (プレイヤーの足元, ワールド座標), direction = 屋台を出す向き (水平),
        /// radius = 中心から屋台までの距離。
        /// </summary>
        public void Show(Vector3 center, Vector3 direction, float radius)
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
            foreach (var t in targets) if (t != null) t.SetAvailable(true); // manualControl なので隠れたまま Raise を待つ
            PlaceBombs();
            Play(ShowRoutine());
        }

        IEnumerator ShowRoutine()
        {
            stall.Rise(riseDuration, false);
            yield return new WaitForSeconds(riseDuration);

            IsReady = true;
            readyTime = Time.time;
            // 左から順に現れる (屋台のローカル X の小さい順)
            var order = new List<Target>(targets);
            order.RemoveAll(t => t == null);
            order.Sort((a, b) => a.transform.localPosition.x.CompareTo(b.transform.localPosition.x));
            float minX = order.Count > 0 ? order[0].transform.localPosition.x : 0f;
            float maxX = order.Count > 0 ? order[order.Count - 1].transform.localPosition.x : 0f;
            foreach (var t in order)
            {
                float at = readyTime + appearSpread * Mathf.InverseLerp(minX, maxX, t.transform.localPosition.x);
                while (Time.time < at) yield return null;
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
            stall.Sink(riseDuration);
            yield return new WaitForSeconds(riseDuration + 0.1f);
            foreach (var t in targets) if (t != null) t.SetAvailable(false);
            ClearBombs();
            anim = null;
        }

        /// <summary>ランダムなカップを爆弾の数だけ選び、カップの見た目を爆弾に付け替える</summary>
        void PlaceBombs()
        {
            ClearBombs();
            var pool = new List<Target>();
            foreach (var t in targets) if (t != null && t.face != null) pool.Add(t);
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
