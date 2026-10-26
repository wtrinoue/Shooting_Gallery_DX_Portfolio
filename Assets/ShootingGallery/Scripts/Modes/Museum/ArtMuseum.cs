using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 美術館モード (絵筆の的を撃つと始まる)。
    ///   行き: 目の前にイーゼルに載った真っ白なキャンバスが現れ、宙に浮いた筆がひとりでに美術館の中を描き出す。
    ///         描き上がると白い閃光に包まれ (Arrived: GameManager が屋台をしまう)、光が晴れると美術館の中にいる。
    ///   中: キャンバスや彫刻にあふれた円い展示室。的は未完成の作品 (真っ白なキャンバス / 灰色の直方体)。
    ///       武器はイマジネーション。当たるとキャンバスでは筆、彫刻ではトンカチになって、作品が1段ずつ出来上がっていく (Artwork)。
    ///       未完成の作品は、空いている所に次々に出てくる (完成した作品はそのまま飾られる。全部埋まったら古い完成品から白紙に戻す)。
    ///   帰り: 白い閃光に包まれ (Left: GameManager が屋台を元の場所に並べておく)、視界が晴れると、もう屋台の前にいる (Closed: 的を出す)。
    /// プレイヤーは中心から動かない。屋台の出し入れ・点数の判断は GameManager が行い、ここは見た目と作品の出し方だけを受け持つ。
    /// </summary>
    public class ArtMuseum : MonoBehaviour
    {
        [Header("行き: 目の前で美術館を描くキャンバス")]
        [Tooltip("イーゼルとキャンバス (出てくる時にふくらむ)")]
        public Transform easel;
        [Tooltip("ひとりでに描く筆 (筆先がこの物の原点)")]
        public Transform introBrush;
        [Tooltip("キャンバスに描く美術館の中の絵 (切り抜きの材質。筆の通ったあとから見えていく。PaintingMask)")]
        public Renderer introPainting;
        [Tooltip("キャンバスをプレイヤーの目の前のどれだけ先に出すか (m)")]
        public float easelDistance = 2.2f;
        [Tooltip("描きはじめるまで / 描く / 描き終えてから光るまで の秒数")]
        public float easelAppear = 0.5f;
        public float paintSeconds = 3.2f;
        public float paintHold = 0.4f;

        [Header("美術館")]
        [Tooltip("美術館の建物と飾りの作品 (中にいる間だけ出す)")]
        public GameObject hall;
        [Tooltip("始める前に、美術館の動かない部品 (壁・柱・廊下・飾り) を材質ごとに1つにまとめる (描く回数を減らして軽くする。作品などスクリプトの付いた物はまとめない)")]
        public bool mergeHall = true;
        [Tooltip("未完成の作品 (的) を置く所。出したりしまったりする")]
        public Artwork[] artworks;
        [Tooltip("同時に出しておく未完成の作品の数 / 次の作品を出すまでの秒数")]
        public int maxBlank = 7;
        public float spawnEvery = 0.7f;

        [Header("白い閃光")]
        public Color flashColor = new Color(1f, 0.99f, 0.95f);
        [Tooltip("光に包まれるまで / 何も見えないまま入れ替える / 晴れていく 秒数")]
        public float flashIn = 0.35f;
        public float flashHold = 0.35f;
        public float flashOut = 1.6f;

        [Header("BGM")]
        [Tooltip("美術館の中で流す曲 (Assets/Source/museum_stage/cks_russianoodori.mp3)。空なら流さない")]
        public AudioClip bgmClip;
        [Range(0f, 1f)] public float bgmVolume = 0.5f;
        [Tooltip("曲がふわっと大きくなる / 消えていく秒数")]
        public float bgmFade = 1.2f;

        [Header("時間")]
        [Tooltip("美術館モードの長さ (秒。キャンバスが出てから)")]
        public float duration = 45f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>作品を進めた回数 / 完成させた数</summary>
        public int Hits { get; private set; }
        public int Completed { get; private set; }
        /// <summary>時間切れから、光が晴れて屋台が見えるまでの秒数</summary>
        public float CloseTime => flashIn + flashHold + flashOut * 0.5f + 0.1f;

        /// <summary>行き: 光で何も見えない間に美術館へ来た (GameManager が屋台をしまい、明るさを変える)</summary>
        public event System.Action Arrived;
        /// <summary>帰り: 光で何も見えない間に美術館を消した (GameManager が屋台を並べる)。true ならラウンドの終わりなどで、すぐに戻した</summary>
        public event System.Action<bool> Left;
        /// <summary>帰り: 光が晴れた (屋台の的を出してよい)</summary>
        public event System.Action Closed;

        float startTime, nextSpawn;
        bool inside;
        Coroutine anim;
        Vector3 easelScale;
        float introDelay;
        MaterialPropertyBlock block;
        readonly List<Artwork> pick = new List<Artwork>(); // 使い回す
        AudioSource bgm;
        float bgmTarget; // 曲の音量の行き先 (0 になったら止める)

        void Awake()
        {
            if (easel != null) { easelScale = easel.localScale; easel.gameObject.SetActive(false); }
            if (introBrush != null) introBrush.gameObject.SetActive(false);
            block = new MaterialPropertyBlock();
            bgm = gameObject.AddComponent<AudioSource>();
            bgm.playOnAwake = false;
            bgm.loop = true;
            bgm.spatialBlend = 0f; // どこにいても同じ大きさ
            bgm.volume = 0f;
            if (hall != null)
            {
                if (mergeHall) MeshMerger.Merge(hall.transform, null);
                hall.SetActive(false);
            }
        }

        /// <summary>美術館モードを始める。yaw = キャンバスを出す向き (度。撃った的の方)。delay 秒たってから (屋台とハッチが消えてから) キャンバスが現れる</summary>
        public void Begin(float yaw, float delay = 0f)
        {
            introDelay = delay;
            if (bgmClip != null && bgmClip.loadState == AudioDataLoadState.Unloaded) bgmClip.LoadAudioData(); // 中に入るまでに読み込んでおく
            if (IsActive) return;
            IsActive = true;
            startTime = Time.time;
            Hits = Completed = 0;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Play(Enter());
        }

        /// <summary>時間切れ: 白い閃光で元の屋台の前へ戻る。immediate なら、その場ですぐに全部しまう (ラウンドの終わり・リセット)</summary>
        public void End(bool immediate = false)
        {
            if (!IsActive && !immediate) return;
            IsActive = false;
            if (immediate)
            {
                if (anim != null) StopCoroutine(anim);
                anim = null;
                ScreenFader.FadeTo(0f, 0.05f);
                HideEasel();
                SetInside(false);
                if (bgm != null) { bgm.Stop(); bgm.volume = 0f; } // すぐに戻す時は、曲もすぐ止める
                Left?.Invoke(true);
                Closed?.Invoke();
                return;
            }
            Play(Leave());
        }

        /// <summary>弾が作品に当たった (GameManager から): 作品を1段進めて、入る点数を返す (完成していたら 0)</summary>
        public int OnArtworkHit(Target target, Vector3 point)
        {
            if (!IsActive || !inside) return 0;
            var work = target.GetComponentInParent<Artwork>();
            if (work == null || !work.IsBlank) return 0;
            int points = work.Advance(point);
            if (points > 0) Hits++;
            if (work.IsComplete) Completed++;
            return points;
        }

        void Update()
        {
            // BGM の音量をなめらかに行き先へ (消えきったら止める)
            if (bgm != null && bgm.isPlaying)
            {
                bgm.volume = Mathf.MoveTowards(bgm.volume, bgmTarget, Time.deltaTime * bgmVolume / Mathf.Max(0.05f, bgmFade));
                if (bgmTarget <= 0f && bgm.volume <= 0f) bgm.Stop();
            }
            if (!IsActive || !inside || Time.time < nextSpawn) return;
            nextSpawn = Time.time + spawnEvery;
            int blank = 0;
            foreach (var w in artworks) if (w != null && w.IsBlank) blank++;
            if (blank < maxBlank) SpawnBlank();
        }

        // 空いている所に未完成の作品を1つ出す。空きがなければ、いちばん古い完成品を白紙に戻す
        void SpawnBlank()
        {
            pick.Clear();
            foreach (var w in artworks) if (w != null && !w.IsShown) pick.Add(w);
            if (pick.Count > 0)
            {
                pick[Random.Range(0, pick.Count)].Show();
                return;
            }
            Artwork oldest = null;
            foreach (var w in artworks)
                if (w != null && w.IsComplete && (oldest == null || w.CompletedAt < oldest.CompletedAt)) oldest = w;
            if (oldest != null && Time.time - oldest.CompletedAt > 4f) oldest.Show(); // 完成してすぐは、しばらく飾っておく
        }

        // 行き: キャンバスが現れて、筆が美術館を描き、白い閃光で中へ
        IEnumerator Enter()
        {
            // 0. 屋台とハッチが消えるのを待つ
            if (introDelay > 0f) yield return new WaitForSeconds(introDelay);
            // 1. イーゼルとキャンバスが、プレイヤーの目の前にまっすぐこちらを向いてふくらんで現れる (まだ何も描かれていない)
            if (easel != null)
            {
                PlaceEasel();
                easel.gameObject.SetActive(true);
                PaintingMask.SetProgress(introPainting, block, 0f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, easelAppear))
                {
                    easel.localScale = easelScale * EaseOutBack(t);
                    yield return null;
                }
                easel.localScale = easelScale;
            }
            // 2. 筆がひとりでに、キャンバスの上を左右に行き来しながら美術館の中を描いていく (筆の通ったあとから絵が見えていく)
            if (introBrush != null) introBrush.gameObject.SetActive(true);
            float nextSwish = 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, paintSeconds))
            {
                PaintingMask.SetProgress(introPainting, block, t);
                if (introBrush != null && introPainting != null)
                {
                    var layer = introPainting.transform;
                    var uv = PaintingMask.BrushAt(t, PaintingMask.IntroRows);
                    int row = Mathf.Min(PaintingMask.IntroRows - 1, (int)(t * PaintingMask.IntroRows));
                    introBrush.position = layer.TransformPoint(new Vector3(uv.x - 0.5f, uv.y - 0.5f, 0f)) - layer.forward * 0.04f;
                    introBrush.rotation = layer.rotation * Quaternion.Euler(-25f, 0f, (row % 2 == 0 ? -28f : 28f) + 8f * Mathf.Sin(t * 60f));
                }
                if (Time.time >= nextSwish) { nextSwish = Time.time + 0.9f; Sfx.Play("paint", 0.7f); } // サッ、サッ (筆で塗る音)
                yield return null;
            }
            PaintingMask.SetProgress(introPainting, block, 1f);
            if (introBrush != null) introBrush.gameObject.SetActive(false);
            yield return new WaitForSeconds(paintHold);
            // 3. 白い閃光に包まれ、その間に美術館の中へ。光が晴れると中にいる (光るまでに高まって、光った瞬間にパァーンと鳴る)
            Sfx.Play("flash");
            ScreenFader.FadeTo(1f, flashIn, flashColor);
            yield return new WaitForSeconds(flashIn + flashHold * 0.5f);
            HideEasel();
            SetInside(true);
            Arrived?.Invoke();
            for (int i = 0; i < maxBlank; i++) SpawnBlank(); // 光が晴れた時には、もう未完成の作品が並んでいる
            nextSpawn = Time.time + spawnEvery;
            yield return new WaitForSeconds(flashHold * 0.5f);
            ScreenFader.FadeTo(0f, flashOut);
            anim = null;
        }

        // 帰り: 白い閃光に包まれ、その間に美術館を消して屋台を並べる。視界が晴れると屋台の前
        IEnumerator Leave()
        {
            Sfx.Play("flash");
            ScreenFader.FadeTo(1f, flashIn, flashColor);
            yield return new WaitForSeconds(flashIn + flashHold * 0.5f);
            HideEasel();
            SetInside(false);
            Left?.Invoke(false);
            yield return new WaitForSeconds(flashHold * 0.5f);
            ScreenFader.FadeTo(0f, flashOut);
            yield return new WaitForSeconds(flashOut * 0.5f);
            Closed?.Invoke();
            anim = null;
        }

        // キャンバスを、プレイヤーの頭の easelDistance 先 (撃った的の向き) に置き、表をまっすぐ頭の方へ向ける (部屋の中で立つ場所がずれていても正面を向く)
        void PlaceEasel()
        {
            var cam = Camera.main;
            var head = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.6f;
            var fwd = transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
            var pos = new Vector3(head.x, transform.position.y, head.z) + fwd * easelDistance;
            easel.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd)); // 表 (-Z) が頭の方
        }

        void HideEasel()
        {
            if (easel != null) easel.gameObject.SetActive(false);
            if (introBrush != null) introBrush.gameObject.SetActive(false);
        }

        void SetInside(bool on)
        {
            inside = on;
            SetBgm(on);
            if (hall != null) hall.SetActive(on);
            if (!on) foreach (var w in artworks) if (w != null) w.HideNow();
        }

        // 美術館の中に入った: 曲を頭からふわっと流す / 出た: ふわっと消す (白い閃光の間)
        void SetBgm(bool on)
        {
            if (bgm == null || bgmClip == null) return;
            if (on)
            {
                bgm.clip = bgmClip;
                bgm.volume = 0f;
                bgm.time = 0f;
                bgm.Play();
                bgmTarget = Mathf.Min(1f, bgmVolume * Sfx.BgmBoost);
            }
            else bgmTarget = 0f;
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.6f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (c + 1f) * t * t * t + c * t * t;
        }
    }
}
