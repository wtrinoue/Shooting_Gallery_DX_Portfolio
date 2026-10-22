using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 忍者モードの舞台 (忍者屋敷の一室)。屋台が沈んだあと、的を撃った方を正面にして、寝ていた4枚の壁がプレイヤーを囲むように
    /// 起き上がり (正面 → 左右 → 後ろの順)、上から天井が降りてきてふたをする。左右と後ろはただの壁で、正面の壁は一面のふすま。
    /// 完全に囲まれるとふすまが左右へ開き、奥行きのある奥座敷が現れる。畳を跳ね上げて床下から・左右のふすまを開けて壁の奥から・天井板を開けて天井裏から忍者のパネルが次々に出てきて (仕掛けは NinjaTrap)、
    /// プレイヤーは手裏剣を投げて倒す (奥ほど高い点数)。時間切れでふすまが閉じ、天井が上がり、壁が倒れて元に戻る。
    /// 屋台の出し入れ・点数・終わる時間の判断は GameManager が行い、ここは見た目と忍者の出し方だけを受け持つ。
    /// プレイヤーは動かない (壁と天井とふすまが動くだけ)。
    /// </summary>
    public class NinjaDojo : MonoBehaviour
    {
        [Tooltip("4枚の壁の根元 (正面・右・後ろ・左の順)。ローカル +Z が外向き。起き上がった向きで置いておく")]
        public Transform[] walls;
        [Tooltip("天井。上から降りてきて壁の上にふたをする")]
        public Transform ceiling;
        public float ceilingDrop = 5f;
        [Tooltip("忍者モードの間だけ表示する物 (畳など)")]
        public GameObject[] showDuringStage;

        [Header("正面のふすまと奥座敷")]
        [Tooltip("正面の壁のふすま (左へ開く組 / 右へ開く組)。左右の壁の向こうへ滑り込んで見えなくなる")]
        public Transform leftDoors;
        public Transform rightDoors;
        [Tooltip("ふすまが開く時に横へ動く距離 (m)")]
        public float doorSlide = 3.4f;
        public float doorTime = 0.9f;
        [Tooltip("ふすまの奥の部屋 (囲まれてから表示する。閉じている間はふすまで見えない)")]
        public GameObject innerRoom;

        [Header("忍者")]
        public Target[] panels;
        [Tooltip("次の忍者が出てくるまでの秒数 (この範囲でランダム)")]
        public Vector2 popInterval = new Vector2(0.35f, 0.8f);
        [Tooltip("忍者が出ている秒数 (この範囲でランダム)")]
        public Vector2 panelStay = new Vector2(1.4f, 2.2f);
        [Tooltip("同時に出ている忍者の最大数")]
        public int maxUp = 4;
        [Tooltip("終わりのこの秒数前からは新しい忍者を出さない")]
        public float stopPopBefore = 1f;

        [Header("Timing")]
        [Tooltip("始まってから終わるまでの秒数 (屋台が沈む・壁が起き上がる・ふすまが開く時間も含む)")]
        public float duration = 20f;
        [Tooltip("始まってから壁が起き上がり始めるまでの秒数 (屋台が沈み終わるのを待つ)")]
        public float dropDelay = 1.2f;
        public float wallTime = 0.8f;
        [Tooltip("正面 → 左右 → 後ろ と、壁が起き上がり始める時間のずれ (秒)")]
        public float wallStagger = 0.25f;
        public float ceilingTime = 0.8f;

        public bool IsActive { get; private set; }
        /// <summary>ふすまが開いて忍者が出てくる状態</summary>
        public bool IsOpen { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回倒した忍者の数</summary>
        public int Hits { get; private set; }
        /// <summary>ふすまが開いた瞬間 (GameManager がメッセージを出す)</summary>
        /// <summary>正面のふすまが開き始めた (GameManager が忍者の BGM を流し始める)</summary>
        public event System.Action DoorsOpening;
        public event System.Action Opened;
        /// <summary>片付け (ふすまが閉じて天井が上がり、壁が倒れる) にかかる秒数</summary>
        public float CloseTime => doorTime * 0.6f + ceilingTime * 0.75f + wallTime;

        float startTime;
        float nextPopTime;
        Coroutine anim;
        Quaternion[] wallUp;
        Vector3 ceilingRest, leftClosed, rightClosed;
        float doorK;
        float[] cooldown; // 忍者ごとの、次に出てよくなる時刻
        float wallK0, wallK1, wallK2, ceilingK;

        void Awake()
        {
            wallUp = new Quaternion[walls.Length];
            for (int i = 0; i < walls.Length; i++) wallUp[i] = walls[i].localRotation;
            ceilingRest = ceiling.localPosition;
            leftClosed = leftDoors.localPosition;
            rightClosed = rightDoors.localPosition;
            cooldown = new float[panels.Length];
            foreach (var p in panels) p.SetAvailable(false);
            SetWalls(0f, 0f, 0f);
            SetCeiling(0f);
            SetDoors(0f);
            SetStageVisible(false);
        }

        /// <summary>忍者モードを始める。yawDeg = 正面にする向き (プレイヤーから見た水平の角度。的を撃った方)</summary>
        public void Begin(float yawDeg)
        {
            if (IsActive) return;
            IsActive = true;
            IsOpen = false;
            startTime = Time.time;
            Hits = 0;
            transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            Play(Intro());
        }

        /// <summary>忍者モードを終える。ふすまが閉じ、天井が上がって壁が倒れる</summary>
        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            IsOpen = false;
            Play(Outro());
        }

        /// <summary>忍者を倒した (GameManager から)</summary>
        public void RegisterHit() => Hits++;

        void Update()
        {
            if (!IsActive || !IsOpen) return;
            if (Time.time < nextPopTime || TimeLeft < stopPopBefore) return;
            nextPopTime = Time.time + Random.Range(popInterval.x, popInterval.y);

            int up = 0;
            var free = new System.Collections.Generic.List<int>();
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i].IsVisible)
                {
                    up++;
                    cooldown[i] = Time.time + Random.Range(0.5f, 1.2f); // 倒れてから少しの間は同じ所から出ない
                }
                else if (Time.time >= cooldown[i]) free.Add(i);
            }
            if (up >= maxUp || free.Count == 0) return;
            panels[free[Random.Range(0, free.Count)]].Raise(Random.Range(panelStay.x, panelStay.y));
        }

        IEnumerator Intro()
        {
            yield return new WaitForSeconds(dropDelay);
            SetStageVisible(true);
            ceiling.gameObject.SetActive(false);
            innerRoom.SetActive(false);
            Sfx.Play("door");
            // 壁が起き上がる (正面 → 左右 → 後ろ)
            float total = wallTime + wallStagger * 2f;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                SetWalls(Phase(t, 0), Phase(t, 1), Phase(t, 2));
                yield return null;
            }
            SetWalls(1f, 1f, 1f);
            // 天井が降りてきてふたをする
            ceiling.gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.deltaTime / ceilingTime)
            {
                SetCeiling(t * t * (3f - 2f * t));
                yield return null;
            }
            SetCeiling(1f);
            Sfx.Play("thunk");
            // 囲まれた: 少し間をおいて、正面のふすまが左右へ開き、奥座敷が現れる
            innerRoom.SetActive(true);
            foreach (var p in panels) p.SetAvailable(true);
            for (int i = 0; i < cooldown.Length; i++) cooldown[i] = 0f;
            yield return new WaitForSeconds(0.4f);
            DoorsOpening?.Invoke();
            Sfx.Play("fusuma");
            for (float t = 0f; t < 1f; t += Time.deltaTime / doorTime)
            {
                SetDoors(t);
                yield return null;
            }
            SetDoors(1f);
            IsOpen = true;
            nextPopTime = Time.time + 0.2f;
            anim = null;
            Opened?.Invoke();
        }

        IEnumerator Outro()
        {
            // 忍者をしまって、ふすまを閉じる
            foreach (var p in panels) p.SetAvailable(false);
            if (doorK > 0f)
            {
                Sfx.Play("fusuma");
                float from = doorK;
                for (float t = 0f; t < 1f; t += Time.deltaTime / (doorTime * 0.6f))
                {
                    SetDoors(Mathf.Lerp(from, 0f, t));
                    yield return null;
                }
            }
            SetDoors(0f);
            innerRoom.SetActive(false);
            // 天井が上がる
            if (ceiling.gameObject.activeSelf)
            {
                float from = ceilingK;
                for (float t = 0f; t < 1f; t += Time.deltaTime / (ceilingTime * 0.75f))
                {
                    SetCeiling(Mathf.Lerp(from, 0f, t * t));
                    yield return null;
                }
            }
            SetCeiling(0f);
            ceiling.gameObject.SetActive(false);
            // 壁が外へ倒れる (全部いっしょに)
            float wallFrom = Mathf.Max(wallK0, Mathf.Max(wallK1, wallK2));
            for (float t = 0f; t < 1f; t += Time.deltaTime / wallTime)
            {
                float k = Mathf.Lerp(wallFrom, 0f, t * t);
                SetWalls(k, k, k);
                yield return null;
            }
            SetWalls(0f, 0f, 0f);
            SetStageVisible(false);
            anim = null;
        }

        // order = 0 (正面), 1 (左右), 2 (後ろ) の壁の、起き上がり具合 (0〜1)
        float Phase(float t, int order)
        {
            float x = Mathf.Clamp01((t - order * wallStagger) / wallTime);
            // 最後に少しだけ行き過ぎて戻る (ドンと立つ感じ)
            const float c = 1.3f;
            float u = x - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        // 起き上がり具合 (0 = 外へ倒れて寝ている, 1 = 立っている)
        void SetWalls(float front, float sides, float back)
        {
            wallK0 = front; wallK1 = sides; wallK2 = back;
            for (int i = 0; i < walls.Length; i++)
            {
                float k = i == 0 ? front : i == 2 ? back : sides;
                walls[i].localRotation = wallUp[i] * Quaternion.Euler(90f * (1f - k), 0f, 0f); // 上が外 (+Z) へ倒れる
            }
        }

        void SetCeiling(float k)
        {
            ceilingK = k;
            ceiling.localPosition = ceilingRest + Vector3.up * (ceilingDrop * (1f - k));
        }

        // 0 = 閉じている, 1 = 開いている (左右の壁の向こうへ滑り込む)
        void SetDoors(float k)
        {
            doorK = k;
            float e = k * k * (3f - 2f * k);
            leftDoors.localPosition = leftClosed + Vector3.left * (doorSlide * e);
            rightDoors.localPosition = rightClosed + Vector3.right * (doorSlide * e);
        }

        void SetStageVisible(bool visible)
        {
            foreach (var w in walls) w.gameObject.SetActive(visible);
            ceiling.gameObject.SetActive(visible);
            innerRoom.SetActive(visible && IsOpen);
            if (showDuringStage != null)
                foreach (var go in showDuringStage) if (go != null) go.SetActive(visible);
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
