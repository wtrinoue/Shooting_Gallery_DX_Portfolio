using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>屋台1つ分。的の一括制御と、フィーバー時のせり上がり / 輪が広がるときの移動演出。</summary>
    public class Stall : MonoBehaviour
    {
        public Target[] targets;
        public float hiddenDepth = 4f;

        Vector3 restPos;
        Quaternion restRot;
        bool restPosCaptured;
        Coroutine moveRoutine;
        bool enableTargetsOnArrive; // せり上がり途中に Slide されても、着いたら的を出す

        /// <summary>表示中で、せり上がり / 沈み込みの途中でない</summary>
        public bool IsSettled => gameObject.activeInHierarchy && moveRoutine == null && squashRoutine == null && !worldScaling && !driven;
        /// <summary>止まる場所 (地上に出ている時の位置と向き。親のローカル座標)。動いている途中でも行き先を返す</summary>
        public Vector3 RestPosition { get { CaptureRestPos(); return restPos; } }
        public Quaternion RestRotation { get { CaptureRestPos(); return restRot; } }

        Vector3 restScale; // 元の大きさ (入れ替えの演出で横に縮める前)
        bool worldScaling; // 怪獣モードで床といっしょに小さく / 大きくなっている途中

        void Awake()
        {
            CaptureRestPos();
            CaptureScale();
        }

        void CaptureScale()
        {
            if (restScale == Vector3.zero) restScale = transform.localScale;
        }

        void CaptureRestPos()
        {
            if (restPosCaptured) return;
            restPos = transform.localPosition;
            restRot = transform.localRotation;
            restPosCaptured = true;
        }

        /// <summary>屋台の的を出す直前 (GameManager が、モード専用の武器をここでハンドガンに戻す)</summary>
        public static event System.Action<Stall> TargetsShowing;

        public void SetTargetsAvailable(bool value)
        {
            if (value) TargetsShowing?.Invoke(this);
            // しまう時は、せり上がり途中の「着いたら的を出す」も取り消す (ゲーム終了後に的が出てこないように)
            if (!value) enableTargetsOnArrive = false;
            foreach (var t in targets)
                if (t != null) t.SetAvailable(value);
        }

        /// <summary>
        /// 止まる場所に瞬時に出す (動きを見せない)。enableTargets なら的もすぐ出す。
        /// 西部劇モードの帰り: 砂嵐で何も見えない間に屋台と的を並べておき、晴れた時にはもう遊べるようにする
        /// </summary>
        public void ShowImmediate(bool enableTargets)
        {
            CaptureRestPos();
            CaptureScale();
            StopMove();
            HideChute();
            HideHatch(true);
            moveRoutine = null;
            worldScaling = false;
            enableTargetsOnArrive = false;
            gameObject.SetActive(true);
            transform.localPosition = restPos;
            transform.localRotation = restRot;
            transform.localScale = restScale;
            SetTargetsAvailable(enableTargets);
            ReadyHatch(true);
        }

        /// <summary>地面の下へ瞬時に隠す (起動時用 / 発掘モードで、プレイヤーから見えなくなった屋台を隠す時)</summary>
        public void HideImmediate()
        {
            driven = false;
            CaptureRestPos();
            CaptureScale();
            SetTargetsAvailable(false);
            HideChute();
            HideHatch(true);
            transform.localScale = restScale;
            transform.localPosition = restPos + Vector3.down * hiddenDepth;
            gameObject.SetActive(false);
        }

        /// <summary>隠れている屋台の置き場所を変える (フィーバーのたびにランダムな場所へ並べ替える用)</summary>
        public void PlaceAt(Vector3 localPos, Quaternion localRot)
        {
            StopMove();
            HideChute();
            HideHatch(true);
            moveRoutine = null;
            CaptureScale();
            transform.localScale = restScale;
            restPos = localPos;
            restRot = localRot;
            restPosCaptured = true;
            transform.localRotation = localRot;
            transform.localPosition = restPos + Vector3.down * hiddenDepth;
        }

        // ---- 足元の長方形のハッチ (せり上がる / しまわれる時だけ床に出る。ほかの動きの時は小さくなって消える) ----
        const float HatchOpenTime = 0.35f, HatchCloseTime = 0.35f;
        StallHatch hatch;
        float hatchGoneAt; // しまう時、閉じたハッチが消えきる時刻
        /// <summary>しまっている途中の足元のハッチが、閉じて消えきるまでの秒数 (同じ場所に別の屋台を出す前に待つ)</summary>
        public float HatchClearIn => hatch != null && hatch.Shown ? Mathf.Max(0f, hatchGoneAt - Time.time) : 0f;
        float hatchTop; // 屋台のいちばん高い所 (足元から)。しまう時、これが床の下に入りきってからハッチを閉じる

        StallHatch Hatch
        {
            get
            {
                if (hatch == null && StallHatch.Available && transform.parent != null)
                {
                    CaptureRestPos();
                    CaptureScale();
                    hatch = StallHatch.Create(this, restScale, parachute, hiddenDepth + 0.05f, out hatchTop, out float pitDepth);
                    hiddenDepth = Mathf.Max(hiddenDepth, pitDepth - 0.05f); // しまった屋台のてっぺんが、閉じた扉より下になる深さまで沈める
                }
                return hatch;
            }
        }

        /// <summary>足元のハッチをしまう (屋台は動かさない)。instant ならその場で消す</summary>
        public void HideHatch(bool instant = false)
        {
            if (hatch != null) hatch.Hide(instant);
        }

        /// <summary>
        /// 屋台が地面に着いて止まった: いつでも下へしまえるように、足元に開いたハッチ (と床板) を用意しておく。
        /// instant なら、ふくらむ様子も見せずにその場で出す
        /// </summary>
        void ReadyHatch(bool instant = false)
        {
            if (!gameObject.activeInHierarchy || worldScaling || swappingOut || Hatch == null) return;
            hatch.Place(restPos, restRot);
            hatch.ShowOpen(instant);
        }

        void Start()
        {
            // 始めから地面に立っている屋台 (タイトルのメイン屋台など) にも、足元のハッチを用意する
            if (moveRoutine == null && (transform.localPosition - restPos).sqrMagnitude < 0.0001f) ReadyHatch(true);
        }

        /// <summary>
        /// 地下からせり上がる。enableTargets = false なら的は出さない (ゲーム終了時に屋台だけ戻す時など)。
        /// 地下から出る時は、先に足元のハッチが開いてから上がる (全体の秒数は duration のまま)
        /// </summary>
        public void Rise(float duration, bool enableTargets = true)
        {
            CaptureRestPos();
            bool hidden = !gameObject.activeSelf;
            bool below = hidden || transform.localPosition.y < restPos.y - 0.05f;
            enableTargetsOnArrive = enableTargets;
            float wait = 0f;
            if (below && Hatch != null)
            {
                // 隠れていた屋台は、穴の底 (てっぺんまで閉じた扉より下) から出す。ハッチを作る前に隠した屋台は浅い所にいることがある
                if (hidden) transform.localPosition = restPos + Vector3.down * hiddenDepth;
                hatch.Place(restPos, restRot);
                wait = Mathf.Min(hatch.Open(HatchOpenTime), duration * 0.4f);
            }
            gameObject.SetActive(true);
            Move(restPos, transform.localRotation, duration - wait, () => ReadyHatch(), wait);
        }

        // ---- 外から1コマずつ動かす (100,000 POINTS のフィーバーで、列になって輪を回る時。GameManager.FeverEntry) ----
        bool driven; // 外から動かしている途中 (ほかの動きを始めると取り消される)

        void StopMove()
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            driven = false;
            swappingOut = false;
            if (squashRoutine != null) { StopCoroutine(squashRoutine); squashRoutine = null; } // (入れ替えの途中でほかの動きを始めた)
        }

        /// <summary>
        /// 外から動かし始める: 地下の (localPos, localRot) の所に出す。ハッチは出さない (地面から生えてくる)。的はしまう。
        /// 動かす間は DriveTo で毎フレーム置き場所を渡し、着いたら Release で止める
        /// </summary>
        public void BeginDrive(Vector3 localPos, Quaternion localRot)
        {
            StopMove();
            moveRoutine = null;
            HideChute();
            HideHatch(true);
            CaptureScale();
            worldScaling = false;
            enableTargetsOnArrive = false;
            SetTargetsAvailable(false);
            transform.localScale = restScale;
            gameObject.SetActive(true);
            driven = true;
            DriveTo(localPos + Vector3.down * hiddenDepth, localRot);
        }

        /// <summary>外から動かしている途中なら、その場所へ置いて true。ほかの動き (沈む・入れ替えなど) に取り消されていたら false</summary>
        public bool DriveTo(Vector3 localPos, Quaternion localRot)
        {
            if (!driven || !gameObject.activeInHierarchy) return false;
            restPos = localPos;
            restRot = localRot;
            restPosCaptured = true;
            transform.localPosition = localPos;
            transform.localRotation = localRot;
            return true;
        }

        /// <summary>外から動かすのを終える: 今の場所を止まる場所にする。enableTargets なら的を出す</summary>
        public void Release(bool enableTargets)
        {
            if (!driven) return;
            driven = false;
            if (enableTargets) SetTargetsAvailable(true);
        }

        /// <summary>
        /// 外から動かしている途中 (列で回っている・まだ出ていない) の屋台を、その場でまっすぐ沈め (ハッチなし)、
        /// 止まる場所を (localPos, localRot) に変えてその地下に置いておく。次に Rise するとそこからせり上がる
        /// </summary>
        public void SinkAway(float duration, Vector3 localPos, Quaternion localRot)
        {
            if (!gameObject.activeInHierarchy)
            {
                PlaceAt(localPos, localRot);
                return;
            }
            enableTargetsOnArrive = false;
            SetTargetsAvailable(false);
            HideHatch();
            var down = transform.localPosition + Vector3.down * hiddenDepth;
            restPos = localPos; // (チャレンジの間も、自分の輪の場所として扱われるように先に変える)
            restRot = localRot;
            restPosCaptured = true;
            Move(down, transform.localRotation, duration, () =>
            {
                gameObject.SetActive(false);
                transform.localRotation = restRot;
                transform.localPosition = restPos + Vector3.down * hiddenDepth;
            });
        }

        /// <summary>足元のハッチを出す (Release した後、全部の屋台が並び終わってから)</summary>
        public void ShowHatch()
        {
            if (!driven && moveRoutine == null) ReadyHatch();
        }

        const float BuryDepth = 6f; // 砂に埋もれる時に、傾いた向きにそって沈む深さ

        /// <summary>
        /// 潜水艦モード: ぐらっと斜めに傾き、傾いた向きにそって砂に埋もれるように沈む (だんだん速く。根元に砂けむり)。
        /// delay 秒待ってから始める (屋台ごとに少しずらす)。沈みきったら隠す (隠した後はいつもの隠れた姿勢に戻しておく)
        /// </summary>
        public void Bury(float duration, float delay, Material sandPuff)
        {
            if (!gameObject.activeInHierarchy) return;
            CaptureRestPos();
            CaptureScale();
            enableTargetsOnArrive = false;
            SetTargetsAvailable(false);
            StopMove();
            HideChute();
            HideHatch();
            worldScaling = false;
            transform.localScale = restScale;
            buryTilt = RandomTilt();
            moveRoutine = StartCoroutine(BuryRoutine(duration, delay, sandPuff, true));
        }

        /// <summary>潜水艦モードの帰り: 斜めに傾いたまま砂の中からせり上がり、上がりきる前にまっすぐ立ち直る。enableTargets なら着いたら的を出す</summary>
        public void Unbury(float duration, bool enableTargets, Material sandPuff)
        {
            CaptureRestPos();
            CaptureScale();
            StopMove();
            HideChute();
            HideHatch();
            worldScaling = false;
            transform.localScale = restScale;
            buryTilt = RandomTilt();
            gameObject.SetActive(true);
            enableTargetsOnArrive = enableTargets;
            moveRoutine = StartCoroutine(BuryRoutine(duration, 0f, sandPuff, false));
        }

        Quaternion buryTilt = Quaternion.identity;

        static Quaternion RandomTilt() =>
            Quaternion.Euler(Random.Range(20f, 32f) * (Random.value < 0.5f ? 1f : -1f), 0f, Random.Range(-20f, 20f));

        IEnumerator BuryRoutine(float duration, float delay, Material sandPuff, bool down)
        {
            Quaternion tilted = restRot * buryTilt;
            Vector3 along = tilted * Vector3.down; // 傾いた屋台の下向き (この向きにそって斜めに沈む)
            if (!down)
            {
                transform.localPosition = restPos + along * BuryDepth;
                transform.localRotation = tilted;
            }
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float nextPuff = 0f, seed = Random.value * 10f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                // 沈む: 先にぐらっと傾いてから、だんだん速く沈む / 上がる: 傾いたまま出てきて、だんだん遅くなり、最後にまっすぐ立ち直る
                float sink = down ? t * t : (1f - t) * (1f - t);
                float tilt = down ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2.2f)) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.55f) * 2.2f));
                float shake = Mathf.Sin(Time.time * 23f + seed) * 1.2f * (1f - Mathf.Abs(t - 0.5f) * 2f); // 砂に飲まれる間、小刻みに揺れる
                transform.localPosition = restPos + along * BuryDepth * sink;
                transform.localRotation = Quaternion.Slerp(restRot, tilted, tilt) * Quaternion.Euler(shake, 0f, shake * 0.6f);
                // 根元のまわりに砂けむりが舞う (地面の近くにいる間)
                if (sandPuff != null && sink < 0.7f && Time.time >= nextPuff && transform.parent != null)
                {
                    nextPuff = Time.time + 0.07f;
                    var r = Random.insideUnitCircle * 1.4f;
                    var basePos = transform.parent.TransformPoint(restPos) + new Vector3(r.x, 0.1f, r.y);
                    Explosion.Spawn(basePos, Random.Range(0.45f, 0.9f), sandPuff);
                }
                yield return null;
            }
            moveRoutine = null;
            if (down)
            {
                // 隠す。いつもの隠れた姿勢 (まっすぐ・地下) にしておく (ほかのモードの Rise などが傾いたまま出さないように)
                transform.localRotation = restRot;
                transform.localPosition = restPos + Vector3.down * hiddenDepth;
                gameObject.SetActive(false);
                yield break;
            }
            transform.localPosition = restPos;
            transform.localRotation = restRot;
            ReadyHatch();
            if (enableTargetsOnArrive)
            {
                enableTargetsOnArrive = false;
                SetTargetsAvailable(true);
            }
        }

        // ---- 海戦モード: 船の外の海へはね飛ばされ、海に流されて、船の後ろで沈んでいく ----

        /// <summary>
        /// 海戦モード: 少し跳ね上がって、船 (ship。原点が船の真ん中、+Z が船首) の横の外側の海へ落ち (水しぶき)、
        /// ぷかぷか揺れながら後ろへ流されていき、船の後ろの見やすい所で、傾きながら沈んでいく (水しぶき)。
        /// delay 秒待ってから始める (屋台ごとにずらす)。沈みきったら隠す (いつもの隠れた姿勢に戻しておく)
        /// </summary>
        public void CastAdrift(Transform ship, float shipHalfWidth, float sternZ, float seaY, float delay, Material splash)
        {
            if (!gameObject.activeInHierarchy || ship == null) return;
            CaptureRestPos();
            CaptureScale();
            enableTargetsOnArrive = false;
            SetTargetsAvailable(false);
            StopMove();
            HideChute();
            HideHatch();
            worldScaling = false;
            transform.localScale = restScale;
            moveRoutine = StartCoroutine(AdriftRoutine(ship, shipHalfWidth, sternZ, seaY, delay, splash));
        }

        IEnumerator AdriftRoutine(Transform ship, float halfWidth, float sternZ, float seaY, float delay, Material splash)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Vector3 start = transform.position;
            Quaternion startRot = transform.rotation;
            var onShip = ship.InverseTransformPoint(start);
            float side = Mathf.Abs(onShip.x) > 0.4f ? Mathf.Sign(onShip.x) : (Random.value < 0.5f ? -1f : 1f); // 近い方の船べりの外へ
            float floatY = seaY - 0.35f; // 屋台の根元が少し水につかった高さ
            // 落ちる所: 船べりより外 (屋台の幅の分も空ける)。前後は今の位置のあたり
            var land = ship.TransformPoint(new Vector3(side * Random.Range(halfWidth + 2.4f, halfWidth + 3.2f), 0f,
                                                       Mathf.Clamp(onShip.z, sternZ + 1f, 4f) + Random.Range(-0.6f, 0.6f)));
            land.y = floatY;
            // 沈む所: 船の後ろの、斜め後ろを振り返ると見える所 (屋台ごとに少しずつ違う)
            var sinkAt = ship.TransformPoint(new Vector3(side * Random.Range(halfWidth + 2.6f, halfWidth + 6f), 0f, sternZ - Random.Range(6f, 11f)));
            sinkAt.y = floatY;
            var lean = Quaternion.AngleAxis(Random.Range(-12f, 12f), ship.forward) * Quaternion.AngleAxis(Random.Range(-8f, 8f), ship.right);
            float seed = Random.value * 10f;

            // 1. 少し跳ね上がって、船の外の海へ (回りながら)
            const float HopTime = 0.85f;
            float hopH = Random.Range(0.9f, 1.5f), hopSpin = Random.Range(-40f, 40f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / HopTime)
            {
                transform.position = Vector3.Lerp(start, land, t) + Vector3.up * (4f * hopH * t * (1f - t));
                transform.rotation = Quaternion.AngleAxis(hopSpin * t, Vector3.up) * Quaternion.Slerp(startRot, lean * startRot, t);
                yield return null;
            }
            var floatRot = Quaternion.AngleAxis(hopSpin, Vector3.up) * lean * startRot;
            SplashAround(land, splash, 6, 1.6f, 0.6f, 1.1f); // ザブン

            // 2. ぷかぷか揺れながら、後ろへ流されていく (はじめは落ちた勢いで速く、だんだんゆっくり)
            float driftTime = Random.Range(3.2f, 4.2f), driftSpin = Random.Range(-30f, 30f), nextSplash = 0f;
            Vector3 bobPos = land;
            for (float t = 0f; t < 1f; t += Time.deltaTime / driftTime)
            {
                float e = 1f - (1f - t) * (1f - t);
                float time = Time.time + seed;
                bobPos = Vector3.Lerp(land, sinkAt, e) + Vector3.up * (Mathf.Sin(time * 2.1f) * 0.08f);
                transform.position = bobPos;
                transform.rotation = Quaternion.AngleAxis(driftSpin * e, Vector3.up)
                                   * Quaternion.AngleAxis(Mathf.Sin(time * 1.6f) * 4f, ship.forward) * Quaternion.AngleAxis(Mathf.Sin(time * 1.3f + 1f) * 3f, ship.right)
                                   * floatRot;
                if (splash != null && Time.time >= nextSplash) // 流れていく根元の小さな波しぶき
                {
                    nextSplash = Time.time + 0.35f;
                    SplashAround(new Vector3(bobPos.x, seaY, bobPos.z), splash, 1, 1.4f, 0.3f, 0.55f);
                }
                yield return null;
            }

            // 3. 傾きながら、だんだん速く沈んでいく (ブクブクと水しぶき)
            const float SinkTime = 2f, SinkDepth = 5.5f;
            var before = transform.rotation;
            var tipped = Quaternion.AngleAxis(Random.Range(28f, 42f) * (Random.value < 0.5f ? -1f : 1f), ship.forward) * before;
            nextSplash = 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / SinkTime)
            {
                transform.position = bobPos + Vector3.down * (SinkDepth * t * t);
                transform.rotation = Quaternion.Slerp(before, tipped, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.8f)));
                if (splash != null && t < 0.6f && Time.time >= nextSplash)
                {
                    nextSplash = Time.time + 0.09f;
                    SplashAround(new Vector3(bobPos.x, seaY, bobPos.z), splash, 1, 1.3f, 0.4f, 0.8f);
                }
                yield return null;
            }
            moveRoutine = null;
            transform.localRotation = restRot;
            transform.localPosition = restPos + Vector3.down * hiddenDepth;
            gameObject.SetActive(false);
        }

        // center のまわり (半径 spread) に、水しぶきを count 個
        static void SplashAround(Vector3 center, Material splash, int count, float spread, float minSize, float maxSize)
        {
            if (splash == null) return;
            for (int i = 0; i < count; i++)
            {
                var r = Random.insideUnitCircle * spread;
                Explosion.Spawn(center + new Vector3(r.x, 0.1f, r.y), Random.Range(minSize, maxSize), splash);
            }
        }

        // ---- 空中ステージ: 床が消えた空中から、ばらばらに落ちていく / 帰りはパラシュートで真上から降りてくる ----

        /// <summary>
        /// 空中ステージ: 床がなくなった空中から、ばらばらの向きに回りながら落ちていく (少し跳ね上がってから、プレイヤーから離れる向きへ流れて落ちる)。
        /// delay 秒待ってから始める (屋台ごとにずらす)。落ちきったら隠す (いつもの隠れた姿勢に戻しておく)
        /// </summary>
        public void FallAway(float duration, float delay)
        {
            if (!gameObject.activeInHierarchy) return;
            CaptureRestPos();
            CaptureScale();
            enableTargetsOnArrive = false;
            SetTargetsAvailable(false);
            StopMove();
            HideChute();
            HideHatch();
            worldScaling = false;
            transform.localScale = restScale;
            moveRoutine = StartCoroutine(FallRoutine(duration, delay));
        }

        IEnumerator FallRoutine(float duration, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Vector3 start = transform.localPosition;
            Quaternion startRot = transform.localRotation;
            // プレイヤー (親の原点) から離れる向きへ、横にもずれながら。最初は少し跳ね上がる
            Vector3 away = new Vector3(start.x, 0f, start.z);
            away = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere;
            away.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, away);
            Vector3 vel = away * Random.Range(1.5f, 3.5f) + side * Random.Range(-1.5f, 1.5f) + Vector3.up * Random.Range(0.8f, 2.2f);
            Vector3 spinAxis = (Random.onUnitSphere + away * 0.5f).normalized;
            float spin = Random.Range(45f, 120f) * (Random.value < 0.5f ? -1f : 1f);
            const float Gravity = 7f; // 本当より少しゆっくり (落ちていく様子が見えるように)
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.localPosition = start + vel * t + Vector3.down * (0.5f * Gravity * t * t);
                transform.localRotation = Quaternion.AngleAxis(spin * t, spinAxis) * startRot;
                yield return null;
            }
            moveRoutine = null;
            transform.localRotation = restRot;
            transform.localPosition = restPos + Vector3.down * hiddenDepth;
            gameObject.SetActive(false);
        }

        Transform parachute;   // パラシュート (GameManager が見本を渡して、始める前に作っておく)
        Vector3 chuteScale;

        // 降りてくる途中でほかの動きに切り替わった時は、パラシュートをしまう
        void HideChute()
        {
            if (parachute == null || !parachute.gameObject.activeSelf) return;
            parachute.gameObject.SetActive(false);
            parachute.localScale = chuteScale;
            parachute.localRotation = Quaternion.identity;
        }

        /// <summary>パラシュートを用意しておく (見本を複製して、屋台の屋根の上に隠しておく)。遊んでいる最中に作らないように、始める前に呼ぶ</summary>
        public void PrepareParachute(GameObject template)
        {
            if (parachute != null || template == null) return;
            CaptureRestPos();
            // 屋台の屋根の高さと幅 (見た目の大きさ) を、部品のメッシュから測って (隠れていても測れるように)、屋根の上に載せる
            float top = 3f, width = 3f;
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            var toLocal = transform.worldToLocalMatrix;
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var m = toLocal * mf.transform.localToWorldMatrix;
                var bb = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var p = m.MultiplyPoint3x4(bb.center + Vector3.Scale(bb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (!any) { min = max = p; any = true; }
                    else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                }
            }
            if (any)
            {
                top = Mathf.Max(1f, max.y);
                width = Mathf.Clamp(Mathf.Max(max.x - min.x, max.z - min.z), 1.5f, 6f);
            }
            var go = Instantiate(template, transform);
            go.name = "Parachute";
            parachute = go.transform;
            parachute.localPosition = Vector3.up * top;
            parachute.localRotation = Quaternion.identity;
            chuteScale = template.transform.localScale * (width / 3f);
            parachute.localScale = chuteScale;
            go.SetActive(false);
        }

        /// <summary>
        /// 空中ステージの帰り: 止まる場所の真上 height (m) から、パラシュートでまっすぐ降りてくる (ゆらゆら揺れながら、着く前にゆっくりになる)。
        /// 着いたらパラシュートがしぼんで消え、enableTargets なら的を出す。delay 秒待ってから始める
        /// </summary>
        public void ParachuteIn(float duration, float delay, float height, bool enableTargets)
        {
            CaptureRestPos();
            CaptureScale();
            StopMove();
            HideChute();
            HideHatch();
            worldScaling = false;
            transform.localScale = restScale;
            gameObject.SetActive(true);
            SetTargetsAvailable(false);
            enableTargetsOnArrive = enableTargets;
            transform.localPosition = restPos + Vector3.up * height;
            transform.localRotation = restRot;
            moveRoutine = StartCoroutine(ParachuteRoutine(duration, delay, height));
        }

        IEnumerator ParachuteRoutine(float duration, float delay, float height)
        {
            if (parachute != null)
            {
                parachute.gameObject.SetActive(true);
                parachute.localScale = chuteScale;
                parachute.localRotation = Quaternion.identity;
            }
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float seed = Random.value * 10f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float e = 1f - (1f - t) * (1f - t); // 着く前にゆっくりになる
                float sway = (1f - t) * 5f;          // ゆらゆら (着くころには止まる)
                float w = Time.time * 1.6f + seed;
                transform.localPosition = restPos + Vector3.up * (height * (1f - e));
                transform.localRotation = restRot * Quaternion.Euler(Mathf.Sin(w) * sway, 0f, Mathf.Sin(w * 0.8f + 1f) * sway);
                if (parachute != null) parachute.localRotation = Quaternion.Euler(Mathf.Sin(w + 0.6f) * sway * 0.6f, 0f, Mathf.Sin(w * 0.8f + 1.6f) * sway * 0.6f);
                yield return null;
            }
            transform.localPosition = restPos;
            transform.localRotation = restRot;
            if (enableTargetsOnArrive)
            {
                enableTargetsOnArrive = false;
                SetTargetsAvailable(true);
            }
            moveRoutine = null;
            ReadyHatch();
            // パラシュートがしぼんで横へ倒れ、消える
            if (parachute != null)
            {
                float lean = Random.value < 0.5f ? -1f : 1f;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
                {
                    if (parachute == null) yield break;
                    parachute.localScale = new Vector3(chuteScale.x * (1f + 0.15f * t), chuteScale.y * Mathf.Max(0.05f, 1f - t), chuteScale.z * (1f + 0.15f * t));
                    parachute.localRotation = Quaternion.Euler(0f, 0f, lean * 70f * t * t);
                    yield return null;
                }
                parachute.gameObject.SetActive(false);
                parachute.localScale = chuteScale;
                parachute.localRotation = Quaternion.identity;
            }
        }

        public void Sink(float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            enableTargetsOnArrive = false;
            SetTargetsAvailable(false);
            Move(restPos + Vector3.down * hiddenDepth, transform.localRotation, duration, () => gameObject.SetActive(false));
            if (Hatch == null) return;
            // 開いたハッチの中へ沈み、屋台のてっぺんが床の下に入りきったら閉じる (MoveTo の ease out: e = 1 - (1 - t)^3 を t について解く)
            hatch.Place(restPos, restRot);
            float e = hiddenDepth > 0f ? Mathf.Clamp01((hatchTop + 0.15f) / hiddenDepth) : 1f;
            hatchGoneAt = Time.time + hatch.CloseAfter(duration * (1f - Mathf.Pow(1f - e, 1f / 3f)), HatchCloseTime);
        }

        /// <summary>
        /// 出ている屋台を地上のまま別の場所へ滑らせる (輪が広がるときに後ろへ下がる / 元の位置へ戻る用)。
        /// 的は出したまま動く。隠れている屋台なら置き場所だけ変える。
        /// </summary>
        public void Slide(Vector3 localPos, Quaternion localRot, float duration)
        {
            CaptureRestPos();
            restPos = localPos;
            restRot = localRot;
            if (!gameObject.activeInHierarchy)
            {
                transform.localRotation = localRot;
                transform.localPosition = restPos + Vector3.down * hiddenDepth;
                return;
            }
            HideHatch(); // 屋台が足元のハッチから離れていく
            Move(restPos, localRot, duration, () => ReadyHatch()); // 着いた所でまた足元にハッチを用意する
        }

        // ---- 豪華屋台への入れ替え (変身) ----
        // 縮む・広がる (横幅) は、動き (せり上がる・滑る・列で回る) とは別に進める。動いている途中の屋台は、動きながら変身し、
        // 新しい屋台が同じ動きの続きを引き継ぐ (位置の移動には関係なく、屋台そのものが変わる)
        Coroutine squashRoutine;
        bool swappingOut;            // 豪華版に入れ替わって縮んでいる途中 (動きの続きで着いても、足元にハッチを出さない)
        Vector3 moveTarget;          // 今の動き (Move) の行き先
        Quaternion moveTargetRot;
        float moveEndsAt;            // 今の動きが終わる時刻
        System.Action moveDone;      // 今の動きが終わった時にすること

        /// <summary>
        /// 豪華屋台への入れ替え用: その場でカードを裏返すように横へ縮んで消える (沈まない)。的はすぐしまう。
        /// 動いている途中なら、動きは止めずに縮む (外から動かしている途中も、そのまま動かしてもらう)。
        /// 消えた後は地下の置き場所に戻しておく (次に Rise / SwapIn で出せるように)。
        /// </summary>
        public void SwapOut(float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            CaptureRestPos();
            CaptureScale();
            enableTargetsOnArrive = false;
            SetTargetsAvailable(false);
            HideChute();
            HideHatch();
            if (squashRoutine != null) StopCoroutine(squashRoutine);
            swappingOut = true;
            squashRoutine = StartCoroutine(Squash(1f, 0f, duration, 0f, () =>
            {
                StopMove();
                moveRoutine = null;
                transform.localScale = restScale;
                transform.localPosition = restPos + Vector3.down * hiddenDepth;
                gameObject.SetActive(false);
                HideHatch(true); // (念のため) 消えた屋台の足元に、開いたハッチを残さない
            }));
        }

        /// <summary>
        /// 豪華屋台への入れ替え用: 細い線から横へぱっと広がって現れる (少し行き過ぎて戻る)。delay 秒は細い線のまま待つ。
        /// takeOver (入れ替わる前の屋台) を渡すと、その屋台の今の場所と動きを引き継ぐ: せり上がり・滑る途中ならその続きを動き、
        /// 外から動かしている途中 (列で回る) なら、そのまま外から動かしてもらう。止まっていればその場所で現れる。
        /// 的は、止まる場所に着いてから出す (外から動かしている時は Release で)
        /// </summary>
        public void SwapIn(float duration, bool enableTargets = true, Stall takeOver = null, float delay = 0f)
        {
            CaptureRestPos();
            CaptureScale();
            StopMove();
            moveRoutine = null;
            HideChute();
            HideHatch();
            worldScaling = false;
            if (takeOver == null)
            {
                transform.localPosition = restPos;
                transform.localRotation = restRot;
            }
            else if (!TakeOverMotion(takeOver)) return; // 沈む途中の屋台は引き継がない (隠したまま)
            gameObject.SetActive(true);
            SetTargetsAvailable(false);
            enableTargetsOnArrive = enableTargets && !driven;
            transform.localScale = new Vector3(restScale.x * 0.001f, restScale.y, restScale.z);
            squashRoutine = StartCoroutine(Squash(0f, 1f, duration, delay, null));
        }

        // 入れ替わる前の屋台 old の、今の場所と動きを引き継ぐ。old が沈む途中なら false (引き継がずに地下へ置く)
        bool TakeOverMotion(Stall old)
        {
            old.CaptureRestPos();
            restPos = old.restPos;
            restRot = old.restRot;
            restPosCaptured = true;
            bool moving = old.moveRoutine != null && old.moveTarget.y >= old.restPos.y - 0.05f; // せり上がる・滑る途中 (沈む途中は除く)
            if (old.moveRoutine != null && !moving)
            {
                PlaceAt(restPos, restRot);
                return false;
            }
            transform.localPosition = old.transform.localPosition;
            transform.localRotation = old.transform.localRotation;
            if (old.driven)
            {
                driven = true; // 列で回っている途中: 外から動かすのを引き継ぐ
                return true;
            }
            if (moving)
            {
                bool rising = old.transform.localPosition.y < old.restPos.y - 0.05f;
                gameObject.SetActive(true); // (動かすコルーチンを始めるため)
                Move(old.moveTarget, old.moveTargetRot, Mathf.Max(0.05f, old.moveEndsAt - Time.time), old.moveDone != null ? () => ReadyHatch() : (System.Action)null);
                if (rising && Hatch != null)
                {   // せり上がる途中: 開いたハッチも、その場で引き継ぐ (前の屋台のハッチはすぐ消す)
                    old.HideHatch(true);
                    hatch.Place(restPos, restRot);
                    hatch.ShowOpen(true);
                }
            }
            return true;
        }

        // 横幅を from → to に変える (縮む時はだんだん速く、広がる時は少し行き過ぎて戻る)。途中で少しだけ縦に伸びる
        IEnumerator Squash(float from, float to, float duration, float delay, System.Action onDone)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            bool grow = to > from;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float k = grow ? EaseOutBack(t) : t * t;
                float w = Mathf.Max(0.001f, Mathf.LerpUnclamped(from, to, k));
                transform.localScale = new Vector3(restScale.x * w, restScale.y * (1f + 0.06f * Mathf.Sin(t * Mathf.PI)), restScale.z);
                yield return null;
            }
            transform.localScale = grow ? restScale : new Vector3(restScale.x * 0.001f, restScale.y, restScale.z);
            squashRoutine = null;
            bool stopped = moveRoutine == null && !driven; // 動きの途中なら、ハッチと的は着いてから (MoveTo / Release)
            if (grow && stopped) ReadyHatch();
            if (enableTargetsOnArrive && stopped)
            {
                enableTargetsOnArrive = false;
                SetTargetsAvailable(true);
            }
            onDone?.Invoke();
        }

        // ---- 怪獣モード: 床 (地面・穴のふち・ボーナスステージのハッチ) と屋台をまとめて、プレイヤーの足元を中心に小さくする ----
        // 大きさは GameManager が全部の部品に同じ倍率で毎フレーム渡す (屋台ごとにばらばらに縮まず、世界全体が1つになって小さくなる)

        /// <summary>まとめて小さくする / 大きくする準備。growing なら隠れていた屋台を出し、着いたら的を出す (enableTargets)。縮める時は的をすぐしまう</summary>
        public bool BeginWorldScale(bool growing, bool enableTargets = true)
        {
            if (!growing && !gameObject.activeInHierarchy) return false;
            CaptureRestPos();
            CaptureScale();
            StopMove();
            HideChute();
            HideHatch();
            moveRoutine = null;
            worldScaling = true;
            if (growing)
            {
                gameObject.SetActive(true);
                transform.localRotation = restRot;
                enableTargetsOnArrive = enableTargets;
            }
            else
            {
                enableTargetsOnArrive = false;
                SetTargetsAvailable(false);
            }
            return true;
        }

        /// <summary>親の原点 (プレイヤーの足元) を中心に、位置と大きさを元の s 倍にする</summary>
        public void SetWorldScale(float s)
        {
            transform.localPosition = restPos * s;
            transform.localScale = restScale * s;
        }

        /// <summary>小さくなりきった: 元の大きさに戻して地下の置き場所に隠す</summary>
        public void FinishWorldShrink()
        {
            worldScaling = false;
            transform.localScale = restScale;
            transform.localPosition = restPos + Vector3.down * hiddenDepth;
            gameObject.SetActive(false);
        }

        /// <summary>元の大きさに戻りきった: 的を出す</summary>
        public void FinishWorldGrow()
        {
            worldScaling = false;
            SetWorldScale(1f);
            ReadyHatch();
            if (!enableTargetsOnArrive) return;
            enableTargetsOnArrive = false;
            SetTargetsAvailable(true);
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.6f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (c + 1f) * t * t * t + c * t * t;
        }

        void Move(Vector3 toPos, Quaternion toRot, float duration, System.Action onDone, float delay = 0f)
        {
            StopMove();
            HideChute();
            CaptureScale();
            transform.localScale = restScale; // 入れ替えの途中で止められても元の大きさに戻す
            worldScaling = false;
            moveTarget = toPos; // (豪華版に入れ替わった時、新しい屋台がこの動きの続きを引き継ぐ)
            moveTargetRot = toRot;
            moveEndsAt = Time.time + delay + duration;
            moveDone = onDone;
            moveRoutine = StartCoroutine(MoveTo(toPos, toRot, duration, onDone, delay));
        }

        IEnumerator MoveTo(Vector3 toPos, Quaternion toRot, float duration, System.Action onDone, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay); // 足元のハッチが開くのを待つ
            Vector3 fromPos = transform.localPosition;
            Quaternion fromRot = transform.localRotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // ease out
                transform.localPosition = Vector3.LerpUnclamped(fromPos, toPos, e);
                transform.localRotation = Quaternion.SlerpUnclamped(fromRot, toRot, e);
                yield return null;
            }
            transform.localPosition = toPos;
            transform.localRotation = toRot;
            moveRoutine = null;
            if (enableTargetsOnArrive)
            {
                enableTargetsOnArrive = false;
                SetTargetsAvailable(true);
            }
            onDone?.Invoke();
        }
    }
}
