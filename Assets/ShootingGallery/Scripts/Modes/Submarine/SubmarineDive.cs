using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 潜水艦モードの舞台。演出の流れ:
    ///   1. プレイヤーの足元から砂の床がざあっと広がり、床下のハッチも屋台の足元も砂になる (Covered)。
    ///      屋台はぐらっと斜めに傾いて、砂に埋もれるように沈んでいく (GameManager が Stall.Bury)。
    ///      同時に真ん中から海の水が一気に広がって、あたり一面が海になる。
    ///   2. それを横目に、丸い潜水艦 (SubmarinePod) がプレイヤーをぱっくん包みこみ、レンズの窓が閉まる。
    ///   3. 水面の下へゆっくり沈んでいき、光の差しこむ広い海の底へ
    ///      (サンゴ礁・海藻の森・沈んだ船・神殿。クラゲ・フグ・アンコウ・タコ・ウミガメ・サメ・ほかの潜水艦が泳ぎ、宝箱が開いたり閉じたりしている)。魚雷で撃つ。
    ///   4. 時間切れで、ゆっくり浮かび上がって水面から顔を出す。潜水艦が開いて消え、海の水が真ん中へ引いていく。
    ///      屋台が砂の中から斜めにせり上がって立ち直り (Drained → GameManager が Stall.Unbury)、砂の床が足元へ縮んで消えてハッチが戻る (Closed)。
    /// 屋台の出し入れ・点数・明るさの判断は GameManager が行い、ここは見た目と、プレイヤーの上げ下げ・的の出し方を受け持つ。
    /// </summary>
    public class SubmarineDive : MonoBehaviour
    {
        [Header("砂の床と海の水")]
        [Tooltip("足元から広がって、ハッチと屋台の足元を覆う砂の床")]
        public GameObject cover;
        [Tooltip("真ん中から一気に広がる海の水面")]
        public Transform water;
        [Tooltip("水面に広がる波紋の輪")]
        public Transform[] ripples;
        [Tooltip("屋台が埋もれる時に舞う砂けむり (GameManager が Stall.Bury に渡す)")]
        public Material sandPuffMaterial;
        public float coverGrowTime = 0.8f;
        [Tooltip("海の水が広がりきるまでの秒数 / 引いていく秒数")]
        public float floodTime = 2.2f, drainTime = 1.6f;
        [Tooltip("屋台が砂に埋もれる秒数 / 砂からせり上がる秒数")]
        public float stallBuryTime = 3.2f, stallRiseTime = 2.4f;
        [Tooltip("砂の床の真ん中の、潜水艦が通るハッチの絞り羽根 (ドレミのハッチと同じ。付け根の縦の軸のまわりに回って開き、砂の下に隠れる)")]
        public Transform[] irisBlades;
        public float irisOpenAngle = 160f, irisTime = 0.9f;

        [Tooltip("海の的の点数の札 (もぐっている間だけ出す。文字は岩などを透けて見えるので、地上からは見えないように)")]
        public GameObject[] pointSigns;

        [Header("潜水艦と海")]
        public SubmarinePod pod;
        [Tooltip("潜水艦モードの間だけ表示する物 (海)")]
        public GameObject[] showDuringStage;
        [Tooltip("海の底でプレイヤーの足元が止まる深さ (m)")]
        public float depth = 20f;
        public float descendTime = 15f, ascendTime = 12f;

        [Header("的")]
        public SeaCreature[] creatures;
        public TreasureChest[] chests;

        [Header("海の雰囲気")]
        [Tooltip("ぶくぶく泡が立ちのぼる海底の割れ目")]
        public Transform[] bubbleVents;
        public Material bubbleMaterial;
        [Tooltip("ゆっくり漂う、マリンスノー (白い小さな粒)")]
        public Transform[] motes;
        [Tooltip("ゆらゆら揺れる海藻 (根元のまわりに揺れる)")]
        public Transform[] kelp;
        [Tooltip("ゆっくり泳ぐ小魚の群れ (真ん中のまわりを回る。偶数番目は左回り、奇数番目は右回り)")]
        public Transform[] schools;

        [Header("Timing")]
        [Tooltip("始まってから浮かび始めるまでの秒数 (砂・水が広がる・潜る時間も含む)")]
        public float duration = 45f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回撃った的の数</summary>
        public int Hits { get; private set; }
        /// <summary>浮かび上がって、屋台が戻り、砂の床が消えるまでの秒数</summary>
        public float CloseTime => ascendTime + 2.2f + drainTime + stallRiseTime + coverGrowTime;

        /// <summary>砂の床が広がりきった時 (GameManager がその下でハッチを消し、屋台を砂に埋もれさせる)</summary>
        public event System.Action Covered;
        /// <summary>頭が水にもぐった時 / 浮かんで頭が水から出た時 (GameManager が水の中の色にする / 元に戻す)</summary>
        public event System.Action Underwater, Surfaced;
        /// <summary>海の水が引いた時 (GameManager が屋台を砂からせり上がらせる。true = ラウンドの終わりなどで、その場ですぐ戻す)</summary>
        public event System.Action<bool> Drained;
        /// <summary>砂の床が消えた時 (GameManager がハッチと屋台の的を戻す)</summary>
        public event System.Action Closed;

        Transform player, head;
        float startTime, playerStartY, nextVentBubble;
        Coroutine anim, flood;
        Vector3 waterScale;
        bool stageVisible, underwater;
        Quaternion[] kelpBase;
        Vector3[] moteHome;
        float[] schoolSpeed;

        void Awake()
        {
            waterScale = water != null ? water.localScale : Vector3.one;
            kelpBase = new Quaternion[kelp.Length];
            for (int i = 0; i < kelp.Length; i++) kelpBase[i] = kelp[i].localRotation;
            moteHome = new Vector3[motes.Length];
            for (int i = 0; i < motes.Length; i++) moteHome[i] = motes[i].localPosition;
            schoolSpeed = new float[schools.Length];
            for (int i = 0; i < schools.Length; i++) schoolSpeed[i] = Random.Range(8f, 16f) * (i % 2 == 0 ? 1f : -1f);
            irisClosed = new Quaternion[irisBlades != null ? irisBlades.Length : 0];
            for (int i = 0; i < irisClosed.Length; i++) irisClosed[i] = irisBlades[i].localRotation;
            SetIris(0f);
            SetSigns(false);
            SetStageVisible(false);
            SetWater(0f);
            if (cover != null) cover.SetActive(false);
            if (pod != null) pod.Hide();
        }

        Quaternion[] irisClosed;
        float irisOpen;
        int irisToken;
        bool signsShown = true;

        // ハッチの開き具合 (0 = 閉じている 〜 1 = 開いている)
        void SetIris(float open)
        {
            irisOpen = open;
            for (int i = 0; i < irisClosed.Length; i++)
                irisBlades[i].localRotation = irisClosed[i] * Quaternion.Euler(0f, irisOpenAngle * open, 0f);
        }

        /// <summary>ハッチを開く / 閉じる (今の開き具合から。あとから呼んだ方が勝つ)</summary>
        IEnumerator Iris(bool open)
        {
            int token = ++irisToken;
            float from = irisOpen, to = open ? 1f : 0f;
            Sfx.Play("door", 0.6f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / irisTime)
            {
                if (token != irisToken) yield break;
                SetIris(Mathf.Lerp(from, to, t * t * (3f - 2f * t)));
                yield return null;
            }
            if (token == irisToken) SetIris(to);
        }

        // 点数の札を出す / しまう
        void SetSigns(bool show)
        {
            if (show == signsShown || pointSigns == null) return;
            signsShown = show;
            foreach (var s in pointSigns) if (s != null) s.SetActive(show);
        }

        /// <summary>潜水艦モードを始める。playerRoot = 上げ下げするプレイヤー (足元が原点) / headTransform = カメラ</summary>
        public void Begin(Transform playerRoot, Transform headTransform)
        {
            if (IsActive) return;
            IsActive = true;
            player = playerRoot;
            head = headTransform;
            playerStartY = player.localPosition.y;
            startTime = Time.time;
            Hits = 0;
            underwater = false;
            Play(Enter());
        }

        /// <summary>時間切れ: 浮かんで水面へ戻る。immediate なら、その場ですぐ地上へ戻して全部隠す (ラウンドの終わり・リセット)</summary>
        public void End(bool immediate = false)
        {
            if (!IsActive && !immediate) return;
            IsActive = false;
            if (immediate)
            {
                if (anim != null) StopCoroutine(anim);
                if (flood != null) StopCoroutine(flood);
                anim = flood = null;
                StopTargets();
                SetPlayerY(playerStartY);
                if (pod != null) pod.Hide();
                irisToken++;
                SetIris(0f);
                SetSigns(false);
                SetWater(0f);
                if (cover != null) cover.SetActive(false);
                SetCover(1f);
                SetStageVisible(false);
                if (underwater) { underwater = false; Surfaced?.Invoke(); }
                Drained?.Invoke(true);
                Closed?.Invoke();
                return;
            }
            Play(Leave());
        }

        /// <summary>海の的に当たった (GameManager から。数えるだけ)</summary>
        public void RegisterHit() => Hits++;

        IEnumerator Enter()
        {
            // 1. 足元から砂の床がざあっと広がる (屋台の足元まで)
            SetWater(0f);
            SetCover(0f);
            if (cover != null) cover.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.deltaTime / coverGrowTime)
            {
                SetCover(EaseOutBack(t));
                yield return null;
            }
            SetCover(1f);
            Covered?.Invoke(); // ハッチが砂の下で消え、屋台が斜めに傾いて砂に埋もれていく (GameManager)
            SetStageVisible(true);
            StartTargets();
            // 真ん中から海の水が一気に広がる (屋台が埋もれていくのと同時)
            flood = StartCoroutine(Flood());
            yield return new WaitForSeconds(floodTime * 0.45f);
            // 2. それを横目に、丸い潜水艦がプレイヤーをぱっくん包み、窓が閉まる
            if (pod != null) yield return pod.Close(head);
            // 3. 足元のハッチ (ドレミのハッチと同じ絞り羽根) が渦を巻いて開き、ゆっくり真下へ沈む。通り抜けたらハッチが閉じる
            yield return Iris(true);
            Sfx.Play("bubbles");
            bool shut = false;
            for (float t = 0f; t < 1f; t += Time.deltaTime / descendTime)
            {
                float e = t * t * (3f - 2f * t);
                SetPlayerY(playerStartY - depth * e);
                if (!shut && depth * e > 6f) { shut = true; StartCoroutine(Iris(false)); }
                yield return null;
            }
            SetPlayerY(playerStartY - depth);
            anim = null;
        }

        IEnumerator Flood()
        {
            Sfx.Play("bubbles");
            Sfx.Play("splash");
            for (float t = 0f; t < 1f; t += Time.deltaTime / floodTime)
            {
                SetWater(1f - (1f - t) * (1f - t) * (1f - t)); // 最初に一気に広がって、遠くはゆっくり
                yield return null;
            }
            SetWater(1f);
            flood = null;
        }

        IEnumerator Leave()
        {
            Sfx.Play("bubbles");
            // 4. ゆっくり浮かぶ (水面の近くまで来たら、海の的を片付ける)
            float from = player != null ? playerStartY - player.localPosition.y : 0f;
            bool stopped = false, opened = false;
            for (float t = 0f; t < 1f; t += Time.deltaTime / ascendTime)
            {
                float e = t * t * (3f - 2f * t);
                float d = Mathf.Lerp(from, 0f, e);
                SetPlayerY(playerStartY - d);
                if (!stopped && d < 3f) { stopped = true; StopTargets(); }
                if (!opened && d < 8f) { opened = true; StartCoroutine(Iris(true)); } // 足元のハッチが開いて、そこから顔を出す
                yield return null;
            }
            SetPlayerY(playerStartY);
            if (!stopped) StopTargets();
            if (!opened) yield return Iris(true);
            StartCoroutine(Iris(false)); // 通り抜けたらハッチが閉じる
            // 水面から顔を出したら、潜水艦が開いて消える
            if (pod != null && pod.IsShown) yield return pod.Open();
            // 海の水が真ん中へ引いていく
            Sfx.Play("bubbles");
            for (float t = 0f; t < 1f; t += Time.deltaTime / drainTime)
            {
                SetWater(1f - t * t);
                yield return null;
            }
            SetWater(0f);
            SetStageVisible(false);
            // 屋台が砂の中から斜めにせり上がって、立ち直る (GameManager)
            Drained?.Invoke(false);
            yield return new WaitForSeconds(stallRiseTime + 0.2f);
            // 砂の床が足元へ縮んで消え、ハッチが戻る
            Closed?.Invoke();
            for (float t = 0f; t < 1f; t += Time.deltaTime / coverGrowTime)
            {
                SetCover(1f - t * t * (3f - 2f * t));
                yield return null;
            }
            if (cover != null) cover.SetActive(false);
            SetCover(1f);
            anim = null;
        }

        void StartTargets()
        {
            foreach (var c in creatures) if (c != null) c.Show();
            foreach (var c in chests) if (c != null) c.Show();
        }

        void StopTargets()
        {
            foreach (var c in creatures) if (c != null) c.Hide();
            foreach (var c in chests) if (c != null) c.Hide();
        }

        // 砂の床の広がり (0 = 足元の点 → 1 = 屋台の足元まで覆う大きさ)
        void SetCover(float k)
        {
            if (cover == null) return;
            float s = Mathf.Max(0.001f, k);
            cover.transform.localScale = new Vector3(s, 1f, s);
        }

        // 海の水面の広がり (0 = なし → 1 = あたり一面)
        void SetWater(float k)
        {
            if (water == null) return;
            water.gameObject.SetActive(k > 0.001f);
            water.localScale = new Vector3(waterScale.x * Mathf.Max(0.001f, k), waterScale.y, waterScale.z * Mathf.Max(0.001f, k));
        }

        void SetPlayerY(float y)
        {
            if (player == null) return;
            var p = player.localPosition;
            player.localPosition = new Vector3(p.x, y, p.z);
            if (pod != null) pod.SetDepth((playerStartY - y) / Mathf.Max(0.1f, depth));
        }

        void SetStageVisible(bool visible)
        {
            stageVisible = visible;
            if (showDuringStage != null)
                foreach (var go in showDuringStage) if (go != null) go.SetActive(visible);
        }

        void Update()
        {
            if (!stageVisible) return;
            // 頭が水にもぐった / 水から出た
            if (head != null && water != null)
            {
                bool under = head.position.y < water.position.y - 0.05f;
                if (under != underwater)
                {
                    underwater = under;
                    if (under)
                    {
                        Sfx.Play("splash");
                        Bubble.Burst(head.position + Vector3.up * 0.6f, 1.2f, bubbleMaterial, 24);
                        Underwater?.Invoke();
                    }
                    else Surfaced?.Invoke();
                }
            }
            SetSigns(underwater); // 点数の札はもぐっている間だけ
            UpdateAtmosphere();
        }

        // 海の雰囲気: 波紋が広がり、海藻がゆらゆら揺れ、マリンスノーが漂い、小魚の群れが回り、海底の割れ目から泡が立ちのぼる
        void UpdateAtmosphere()
        {
            float time = Time.time;
            if (ripples != null)
                for (int i = 0; i < ripples.Length; i++)
                {
                    float k = Mathf.Repeat(time * 0.3f + i / (float)ripples.Length, 1f);
                    ripples[i].localScale = new Vector3(0.05f + k * 0.95f, 1f, 0.05f + k * 0.95f);
                }
            if (!underwater) return; // 海の中の物は、もぐっている間だけ動かす
            for (int i = 0; i < kelp.Length; i++)
            {
                float w = Mathf.Sin(time * 0.9f + i * 0.73f) * 9f + Mathf.Sin(time * 1.7f + i) * 3f;
                kelp[i].localRotation = kelpBase[i] * Quaternion.Euler(w, 0f, w * 0.6f);
            }
            for (int i = 0; i < motes.Length; i++)
            {
                float ph = i * 1.37f;
                motes[i].localPosition = moteHome[i] + new Vector3(Mathf.Sin(time * 0.11f + ph) * 0.8f, Mathf.Repeat(time * 0.08f + ph, 1f) * -1.2f, Mathf.Cos(time * 0.09f + ph) * 0.8f);
            }
            for (int i = 0; i < schools.Length; i++)
                schools[i].Rotate(0f, schoolSpeed[i] * Time.deltaTime, 0f, Space.Self);
            if (bubbleVents != null && bubbleVents.Length > 0 && time >= nextVentBubble)
            {
                nextVentBubble = time + 0.07f;
                var v = bubbleVents[Random.Range(0, bubbleVents.Length)];
                Bubble.Spawn(v.position + Random.insideUnitSphere * 0.15f, Random.Range(0.05f, 0.14f), bubbleMaterial, Vector3.up * 0.8f, Random.Range(3.5f, 5f));
            }
        }

        static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.7f;
            float x = t - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
