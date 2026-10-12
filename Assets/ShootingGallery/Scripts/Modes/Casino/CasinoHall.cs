using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// カジノモードの舞台 (円柱形の大広間)。演出の流れ (どれもゆっくり):
    ///   1. 屋台が沈んだあと、地味な灰色の円柱の壁が、的を撃った方から左右へ順に床下から伸びてプレイヤーを囲み、天井がふたをする。
    ///   2. 「バツン」と一気に真っ暗になり (Darken)、目の前の黒い板 (ScreenFader) で視界を完全にふさぐ。しばらく静かな暗闇が続く。
    ///   3. 視界をふさいだまま、ドラムロールの中でスポットライトがガシャン、ガシャンと1つずつ点く音がする (ルーレットとスロットはその間に現れる)。
    ///      ジャーン! と視界が一気に開け、全部の明かりがまぶしく点く。壁は赤いビロードと金の飾りの豪華な姿に変わり、ネオンがチカチカと灯り、シャンデリアが輝き、
    ///      紙吹雪が舞って歓声が上がる (LightUp)。ここからゲーム開始。
    ///   4. 時間切れでまた視界をふさいで真っ暗になり、その間に地味な壁・天井・床だけに戻す (Restore)。しばらくして視界が開けたら、天井が外れ、壁が床下へゆっくりしまわれていく。
    /// 広さは屋台6軒の輪が入るくらい、天井は普通の3倍ほど。武器はダーツ。
    /// 屋台の出し入れ・点数・終わる時間・明るさの判断は GameManager が行い、ここは見た目の順番だけを受け持つ。プレイヤーは動かない。
    /// </summary>
    public class CasinoHall : MonoBehaviour
    {
        [Header("地味な外側 (伸びてくる壁と天井)")]
        [Tooltip("円柱の壁の板 (床下からせり上がる)。正面 (ローカル +Z) からの角度で、せり上がる順番が決まる")]
        public Transform[] wallPanels;
        public float panelHiddenDepth = 10f;
        [Tooltip("天井 (上から降りてきてふたをする)")]
        public Transform ceiling;
        public float ceilingDrop = 8f;
        [Tooltip("囲まれている間だけ表示する物 (床)")]
        public GameObject[] showDuringStage;

        [Header("豪華な内側 (明かりが点いた時に現れる)")]
        [Tooltip("暗闇の中で1つずつ現れる物 (スポットライトとルーレットの組など。この順に、ぽんと大きくなって現れる)")]
        public GameObject[] revealSteps;
        [Tooltip("最後に現れる主役 (壇とスロット)")]
        public GameObject revealFinale;
        [Tooltip("全部の明かりが点いた時に現れる物 (壁の豪華な飾り・ネオン・シャンデリアと明かり)")]
        public GameObject luxury;
        public SlotMachine slot;
        public RouletteWheel[] wheels;

        [Header("明かりが点く瞬間の演出")]
        [Tooltip("チカチカと点くネオン (名前が同じものは同じ調子で点く)")]
        public Renderer[] neon;
        [Tooltip("広間の明かり (シャンデリアなど)。一度ふっと強く点いてから落ち着く")]
        public Light[] hallLights;
        [Tooltip("天井から舞い落ちる紙吹雪の色")]
        public Material[] confettiMaterials;
        public int confettiCount = 90;

        [Header("Timing")]
        [Tooltip("明かりが点いてから終わるまでの、ゲームの秒数")]
        public float playTime = 16f;
        [Tooltip("始まってから壁がせり上がり始めるまでの秒数 (屋台が沈み終わるのを待つ)")]
        public float dropDelay = 1.2f;
        [Tooltip("壁の板1枚がせり上がる秒数")]
        public float panelTime = 1.0f;
        [Tooltip("正面の板から真後ろの板まで、せり上がり始める時間のずれ (秒)")]
        public float spreadTime = 1.4f;
        public float ceilingTime = 1.4f;
        [Tooltip("ふたをしてから真っ暗になるまで / 真っ暗なまま静かに待つ / スポットライトが1つずつ点く間隔 / スロットが照らされてから全部の明かりが点くまで (秒)。" +
                 "スポットライトの間はドラムロールが鳴る (長さ 3.6 秒 = revealInterval × (ルーレットの数 + 1) + finaleHold に合わせてある)")]
        public float darkDelay = 0.5f;
        public float darkHold = 1.6f;
        public float revealInterval = 0.4f;
        public float finaleHold = 0.8f;
        [Tooltip("目の前の黒い板で視界をふさぐ秒数 (バツンと一気に) / 全部の明かりが点く時に視界が開ける秒数 (一気に)")]
        public float blackoutTime = 0.12f;
        public float revealOpenTime = 0.15f;
        [Tooltip("終わり: 真っ暗なまま待つ秒数 (この間に飾りを消し、地味な壁・天井・床だけにする) / 視界が開ける秒数 / 開けてから天井が外れ始めるまでの秒数")]
        public float outroDarkHold = 1.6f;
        public float outroOpenTime = 0.6f;
        public float outroLookTime = 0.7f;

        public bool IsActive { get; private set; }
        /// <summary>明かりが点いて、スロットとルーレットが使える状態</summary>
        public bool IsOpen { get; private set; }
        /// <summary>ゲームの残り秒数 (明かりが点くまでは playTime のまま)</summary>
        public float TimeLeft => !IsActive ? 0f : IsOpen ? Mathf.Max(0f, playTime - (Time.time - openTime)) : playTime;
        /// <summary>明かりが点くまでの秒数 (始めてから)</summary>
        public float IntroTime => dropDelay + panelTime + spreadTime + ceilingTime + darkDelay + darkHold + revealInterval * ((revealSteps?.Length ?? 0) + 1) + finaleHold;
        /// <summary>片付けにかかる秒数</summary>
        public float CloseTime => blackoutTime + outroDarkHold + outroOpenTime + outroLookTime + ceilingTime + panelTime + spreadTime * 0.5f;

        /// <summary>真っ暗にする時 (GameManager が環境光を落とす)</summary>
        public event System.Action Darken;
        /// <summary>全部の明かりが点いた時 = ゲーム開始 (GameManager がカジノの明るさにし、ダーツを渡す)</summary>
        public event System.Action LightUp;
        /// <summary>片付けで天井が上がり始める時 (GameManager がいつもの夜の明るさへ戻す)</summary>
        public event System.Action Restore;

        float openTime;
        Coroutine anim;
        Vector3[] panelRest;
        float[] panelDelay; // 正面から離れているほど遅い (0〜1)
        Vector3 ceilingRest;
        float ceilingK;
        float[] lightBase;
        readonly List<Confetti> confetti = new List<Confetti>();

        void Awake()
        {
            panelRest = new Vector3[wallPanels.Length];
            panelDelay = new float[wallPanels.Length];
            for (int i = 0; i < wallPanels.Length; i++)
            {
                panelRest[i] = wallPanels[i].localPosition;
                var p = panelRest[i];
                panelDelay[i] = Mathf.Abs(Mathf.DeltaAngle(0f, Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg)) / 180f;
            }
            ceilingRest = ceiling.localPosition;
            if (hallLights == null) hallLights = new Light[0];
            lightBase = new float[hallLights.Length];
            for (int i = 0; i < hallLights.Length; i++) lightBase[i] = hallLights[i] != null ? hallLights[i].intensity : 0f;
            SetPanels(0f, false);
            SetCeiling(0f);
            SetLuxury(false);
            SetStageVisible(false);
        }

        /// <summary>カジノモードを始める。yawDeg = 正面にする向き (的を撃った方)。スロットはこの向きに来る</summary>
        public void Begin(float yawDeg)
        {
            if (IsActive) return;
            IsActive = true;
            IsOpen = false;
            transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            Play(Intro());
        }

        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            IsOpen = false;
            Play(Outro());
        }

        IEnumerator Intro()
        {
            yield return new WaitForSeconds(dropDelay);
            // 1. 地味な壁が床下から伸びてきて囲み、天井がふたをする
            SetStageVisible(true);
            ceiling.gameObject.SetActive(false);
            Sfx.Play("door");
            float total = panelTime + spreadTime;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                SetPanels(t, false);
                yield return null;
            }
            SetPanels(total, false);
            ceiling.gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.deltaTime / ceilingTime)
            {
                SetCeiling(t * t * (3f - 2f * t));
                yield return null;
            }
            SetCeiling(1f);
            Sfx.Play("thunk");

            // 2. 「バツン」と一気に真っ暗に。目の前の黒い板で視界をふさぎ、何も見えないまま静かに待つ
            yield return new WaitForSeconds(darkDelay);
            Darken?.Invoke();
            Sfx.Play("blackout");
            ScreenFader.FadeTo(1f, blackoutTime);
            yield return new WaitForSeconds(darkHold);

            // 3. ドラムロールの中、スポットライトがガシャン、ガシャンと1つずつ点いてルーレットが現れる → 最後にスロットが照らされる
            Sfx.Play("drumroll");
            if (revealSteps != null)
                foreach (var step in revealSteps)
                {
                    if (step == null) continue;
                    StartCoroutine(Pop(step));
                    Sfx.Play("lever");
                    yield return new WaitForSeconds(revealInterval);
                }
            yield return new WaitForSeconds(revealInterval);
            if (revealFinale != null) StartCoroutine(Pop(revealFinale));
            Sfx.Play("lever");
            yield return new WaitForSeconds(finaleHold);

            // 4. ジャーン! 視界が一気に開け、全部の明かりが一度にまぶしく点き、ネオンがチカチカと灯り、紙吹雪が舞って歓声が上がる
            //    (ここまでのスポットライトとスロットは、視界をふさいだまま音だけで聞かせる)
            ScreenFader.FadeTo(0f, revealOpenTime);
            Sfx.Play("crash");
            Sfx.Play("deluxe");
            Sfx.Play("cheer");
            if (luxury != null) luxury.SetActive(true);
            StartCoroutine(LightShow());
            SpawnConfetti();
            slot.Activate();
            foreach (var w in wheels) w.Activate();
            IsOpen = true;
            openTime = Time.time;
            anim = null;
            LightUp?.Invoke();
        }

        IEnumerator Outro()
        {
            // 4. また視界をふさいで真っ暗に。何も見えない間に豪華な飾りを消し、地味な壁・天井・床だけにして、明るさも夜に戻す
            //    → しばらくして視界が開ける (地味な部屋の中) → 天井が外れ、壁がゆっくり地面へしまわれていく
            slot.Deactivate();
            foreach (var w in wheels) w.Deactivate();
            Darken?.Invoke();
            Sfx.Play("blackout");
            ScreenFader.FadeTo(1f, blackoutTime);
            yield return new WaitForSeconds(blackoutTime);
            ClearConfetti();
            SetLuxury(false);
            Restore?.Invoke();
            yield return new WaitForSeconds(outroDarkHold);
            ScreenFader.FadeTo(0f, outroOpenTime);
            yield return new WaitForSeconds(outroOpenTime + outroLookTime);
            if (ceiling.gameObject.activeSelf)
            {
                float from = ceilingK;
                for (float t = 0f; t < 1f; t += Time.deltaTime / ceilingTime)
                {
                    SetCeiling(Mathf.Lerp(from, 0f, t * t * (3f - 2f * t)));
                    yield return null;
                }
            }
            SetCeiling(0f);
            ceiling.gameObject.SetActive(false);
            Sfx.Play("door");
            float total = panelTime + spreadTime * 0.5f;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                SetPanels(t, true);
                yield return null;
            }
            SetStageVisible(false);
            anim = null;
        }

        // ぽんと現れる: 0 から少し行き過ぎて元の大きさへ。スポットライト (名前が Spot で始まる物) は大きくならず、その場でパッと点く
        IEnumerator Pop(GameObject go)
        {
            var parts = new List<Transform>();
            foreach (Transform child in go.transform)
                if (!child.name.StartsWith("Spot")) parts.Add(child);
            var full = new Vector3[parts.Count];
            for (int i = 0; i < parts.Count; i++) full[i] = parts[i].localScale;
            go.SetActive(true);
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.35f)
            {
                const float c = 1.7f;
                float u = Mathf.Clamp01(k) - 1f;
                float s = Mathf.Max(0.001f, 1f + (c + 1f) * u * u * u + c * u * u);
                for (int i = 0; i < parts.Count; i++) parts[i].localScale = full[i] * s;
                yield return null;
            }
            for (int i = 0; i < parts.Count; i++) parts[i].localScale = full[i];
        }

        // 明かりが点く瞬間: 広間の明かりは一瞬チカッとしてから、まぶしく強く点いてゆっくり落ち着く。
        // ネオンは種類ごとに少しずつずれて、蛍光灯のようにチカ、チカッと瞬いてから灯る
        IEnumerator LightShow()
        {
            const float Length = 1.6f;
            var offsets = new Dictionary<string, float>();
            var neonOffset = new float[neon?.Length ?? 0];
            for (int i = 0; i < neonOffset.Length; i++)
            {
                if (neon[i] == null) continue;
                if (!offsets.TryGetValue(neon[i].name, out float o)) offsets[neon[i].name] = o = Random.Range(0.05f, 0.7f);
                neonOffset[i] = o;
            }
            for (float t = 0f; t < Length; t += Time.deltaTime)
            {
                float k = t < 0.08f ? 1.2f : t < 0.16f ? 0.1f : Mathf.Lerp(2.2f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.16f) / (Length - 0.16f)));
                for (int i = 0; i < hallLights.Length; i++)
                    if (hallLights[i] != null) hallLights[i].intensity = lightBase[i] * k;
                for (int i = 0; i < neonOffset.Length; i++)
                    if (neon[i] != null) neon[i].enabled = NeonFlicker(t - neonOffset[i]);
                yield return null;
            }
            for (int i = 0; i < hallLights.Length; i++)
                if (hallLights[i] != null) hallLights[i].intensity = lightBase[i];
            foreach (var n in neon) if (n != null) n.enabled = true;
        }

        // 点き始めてからの秒数 t のネオンが光っているか (チカ、チカッ…パッ)
        static bool NeonFlicker(float t) =>
            t >= 0f && (t < 0.05f || (t >= 0.11f && t < 0.15f) || (t >= 0.2f && t < 0.23f) || t >= 0.32f);

        // 天井近くから紙吹雪がぱっと広がって、ひらひら舞い落ちる
        void SpawnConfetti()
        {
            if (confettiMaterials == null || confettiMaterials.Length == 0) return;
            for (int i = 0; i < confettiCount; i++)
            {
                var mat = confettiMaterials[i % confettiMaterials.Length];
                var piece = FxPool.Get("Confetti", () => FxPool.Primitive(PrimitiveType.Quad, mat), transform); // 作って消さずに使い回す
                piece.GetComponent<Renderer>().sharedMaterial = mat;
                var t = piece.transform;
                Vector2 p = Random.insideUnitCircle * 5.5f;
                t.localPosition = new Vector3(p.x, ceilingRest.y - Random.Range(0.4f, 1.6f), p.y);
                t.localRotation = Random.rotation;
                t.localScale = new Vector3(0.07f, 0.045f, 1f);
                var v = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 2.5f), Random.Range(-1.5f, 1.5f));
                confetti.Add(new Confetti
                {
                    t = t, velocity = v, spinAxis = Random.onUnitSphere, spin = Random.Range(200f, 600f),
                    sway = Random.Range(0.3f, 0.7f), phase = Random.value * 10f
                });
            }
        }

        void ClearConfetti()
        {
            foreach (var c in confetti) if (c.t != null) FxPool.Release("Confetti", c.t.gameObject);
            confetti.Clear();
        }

        void Update()
        {
            // 紙吹雪: はじめの勢いはすぐ弱まり、あとはゆらゆら揺れながらゆっくり落ちる。床に着いたら消える
            float dt = Time.deltaTime;
            for (int i = confetti.Count - 1; i >= 0; i--)
            {
                var c = confetti[i];
                if (c.t == null) { confetti.RemoveAt(i); continue; }
                c.velocity = Vector3.Lerp(c.velocity, new Vector3(0f, -0.55f, 0f), dt * 2.2f);
                float w = (Time.time + c.phase) * 3f;
                c.t.localPosition += (c.velocity + new Vector3(Mathf.Sin(w), 0f, Mathf.Cos(w * 0.8f)) * c.sway) * dt;
                c.t.Rotate(c.spinAxis, c.spin * dt, Space.Self);
                if (c.t.localPosition.y < 0.06f)
                {
                    FxPool.Release("Confetti", c.t.gameObject);
                    confetti.RemoveAt(i);
                    continue;
                }
                confetti[i] = c;
            }
        }

        struct Confetti
        {
            public Transform t;
            public Vector3 velocity, spinAxis;
            public float spin, sway, phase;
        }

        void SetLuxury(bool on)
        {
            if (revealSteps != null) foreach (var s in revealSteps) if (s != null) s.SetActive(on);
            if (revealFinale != null) revealFinale.SetActive(on);
            if (luxury != null) luxury.SetActive(on);
        }

        // t = 始まってからの秒数。sinking なら逆向き (後ろの板から先に沈む)
        void SetPanels(float t, bool sinking)
        {
            for (int i = 0; i < wallPanels.Length; i++)
            {
                float k = sinking
                    ? 1f - Mathf.Clamp01((t - (1f - panelDelay[i]) * spreadTime * 0.5f) / panelTime)
                    : Mathf.Clamp01((t - panelDelay[i] * spreadTime) / panelTime);
                float e = k * k * (3f - 2f * k);
                wallPanels[i].localPosition = panelRest[i] + Vector3.down * (panelHiddenDepth * (1f - e));
            }
        }

        void SetCeiling(float k)
        {
            ceilingK = k;
            ceiling.localPosition = ceilingRest + Vector3.up * (ceilingDrop * (1f - k));
        }

        void SetStageVisible(bool visible)
        {
            foreach (var p in wallPanels) p.gameObject.SetActive(visible);
            ceiling.gameObject.SetActive(visible);
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
