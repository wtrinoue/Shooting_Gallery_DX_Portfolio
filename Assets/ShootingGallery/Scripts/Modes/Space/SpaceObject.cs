using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 宇宙モードの的: 星の点数パネル (流星群のように光の尾を引いて流れる) / 惑星 (ふわっと現れて、ゆっくり漂いながら回る) /
    /// 隕石 (ごろごろ回りながら炎の尾を引いて横切る) / ロケット (煙を吐きながら飛んでいく)。
    /// どれもミサイルが当たると爆発して、きらきらした破片が飛び散る。点数は大きく書いてあり、いつもプレイヤーの方を向く。
    /// 位置は親 (SpaceStage。プレイヤーの足元が原点) のローカル座標で決める。反撃はしない。
    /// </summary>
    public class SpaceObject : MonoBehaviour
    {
        public enum Kind { StarPanel, Planet, Meteor, Rocket }

        public Kind kind = Kind.StarPanel;
        public string displayName = "STAR";
        [Tooltip("回る・向きを変える見た目の部分")]
        public Transform body;
        [Tooltip("当たり判定 (kind = SpaceObject。visual は空のオブジェクト)")]
        public Target hitTarget;
        [Tooltip("大きく書いた点数 (Launch のたびに書き直す)")]
        public TextMesh[] pointLabels;
        [Tooltip("点数の札 (いつもプレイヤーの方を向く。星のパネルは body ごと向く)")]
        public Transform labelRoot;
        public int[] pointChoices = { 100 };
        [Tooltip("回る軸と速さ (度/秒)。隕石は Launch のたびに軸を変える")]
        public Vector3 spinAxis = Vector3.up;
        public float spinSpeed = 20f;
        [Tooltip("後ろに引く光の尾 (なければ null)")]
        public TrailRenderer trail;
        [Tooltip("ふわっと現れる / 消える秒数")]
        public float appearTime = 0.5f;
        [Tooltip("爆発の色と、飛び散る破片の色")]
        public Material explosionMaterial;
        public Material debrisMaterial;
        [Tooltip("爆発の大きさ (m)")]
        public float blastSize = 1f;

        const float SinkGuard = 0.4f; // 出てすぐ (起き上がりの途中) は当たったと数えない

        public bool IsFlying { get; private set; }
        public bool IsBusy => IsFlying || fading;
        public int Points => hitTarget != null ? hitTarget.points : 0;

        System.Action<SpaceObject> onDestroyed;
        Vector3 from, to;
        float travelTime, t, fadeT;
        bool fading, armed;
        Vector3 fullScale;
        Quaternion faceOffset;

        void Awake()
        {
            hitTarget.SetAvailable(false);
            fullScale = body.localScale;
        }

        /// <summary>from から to へ speed (m/秒) で動く。惑星のように止まっているものは from と to を近くにして、lifetime で長さを決める</summary>
        public void Launch(Vector3 fromPos, Vector3 toPos, float speed, System.Action<SpaceObject> destroyedCallback, float lifetime = 0f)
        {
            gameObject.SetActive(true);
            from = fromPos;
            to = toPos;
            float len = Vector3.Distance(from, to);
            travelTime = lifetime > 0f ? lifetime : len / Mathf.Max(0.1f, speed);
            t = 0f;
            fading = armed = false;
            onDestroyed = destroyedCallback;
            if (kind == Kind.Meteor) spinAxis = Random.onUnitSphere;
            body.localScale = kind == Kind.Planet ? Vector3.one * 0.001f : fullScale;
            Place();
            if (trail != null) { trail.Clear(); trail.emitting = true; }
            IsFlying = true;

            hitTarget.points = pointChoices.Length > 0 ? pointChoices[Random.Range(0, pointChoices.Length)] : 100;
            foreach (var label in pointLabels) if (label != null) label.text = hitTarget.points.ToString();
            hitTarget.SetAvailable(true);
            hitTarget.Raise(9999f);
        }

        /// <summary>撃たれずに去る (宇宙モードの終わり)。小さくなって消える</summary>
        public void Retreat()
        {
            if (!IsFlying) return;
            BeginFade();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (fading)
            {
                fadeT += dt / Mathf.Max(0.05f, appearTime);
                body.localScale = fullScale * Mathf.Max(0.001f, 1f - fadeT);
                if (labelRoot != null && labelRoot != body) labelRoot.localScale = Vector3.one * Mathf.Max(0.001f, 1f - fadeT);
                if (fadeT >= 1f) Vanish();
                return;
            }
            if (!IsFlying) return;

            t += dt;
            Place();
            if (kind == Kind.Planet) // ふわっと大きくなって現れる (少し行き過ぎて戻る)
            {
                float k = Mathf.Clamp01(t / appearTime) - 1f;
                float s = Mathf.Max(0.001f, 1f + 2.4f * k * k * k + 1.4f * k * k);
                body.localScale = fullScale * s;
                if (labelRoot != null) labelRoot.localScale = Vector3.one * Mathf.Min(1f, s);
            }
            body.Rotate(spinAxis, spinSpeed * dt, Space.Self);

            if (hitTarget.IsUp) armed = true;
            if (armed && !hitTarget.IsUp && t > SinkGuard) { Burst(); return; } // ミサイルが当たった
            if (t >= travelTime) BeginFade();
        }

        void LateUpdate()
        {
            if (!IsFlying || Camera.main == null) return;
            // 星のパネルは表 (-Z) を、点数の札は文字を、いつもプレイヤーの方へ向ける
            Vector3 eye = Camera.main.transform.position;
            if (kind == Kind.StarPanel)
            {
                var face = Quaternion.LookRotation(body.position - eye);
                body.rotation = face * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.2f) * 12f); // 少しゆらゆら
            }
            if (labelRoot != null && labelRoot != body) labelRoot.rotation = Quaternion.LookRotation(labelRoot.position - eye);
        }

        // 進む向きに機首を向ける (ロケット・隕石の尾が後ろへ流れるように)
        void Place()
        {
            float k = travelTime > 0f ? Mathf.Clamp01(t / travelTime) : 0f;
            transform.localPosition = Vector3.Lerp(from, to, k);
            if (kind == Kind.Rocket && (to - from).sqrMagnitude > 0.01f)
                transform.localRotation = Quaternion.LookRotation(to - from); // +Z が機首
        }

        // ミサイルが当たった: 大きな爆発と、きらきらした破片が四方に飛び散る
        void Burst()
        {
            IsFlying = false;
            var p = body.position;
            Explosion.Spawn(p, blastSize, explosionMaterial);
            for (int i = 0; i < 6; i++)
                Explosion.Spawn(p + Random.onUnitSphere * blastSize * 0.7f, blastSize * Random.Range(0.25f, 0.45f), i % 2 == 0 ? debrisMaterial : explosionMaterial);
            SpaceDebris.Spawn(p, blastSize, debrisMaterial, kind == Kind.Planet ? 22 : 12);
            Sfx.Play("explosion");
            onDestroyed?.Invoke(this);
            Vanish();
        }

        void BeginFade()
        {
            IsFlying = false;
            fading = true;
            fadeT = 0f;
            hitTarget.SetAvailable(false);
            if (trail != null) trail.emitting = false;
        }

        void Vanish()
        {
            IsFlying = fading = false;
            hitTarget.SetAvailable(false);
            body.localScale = fullScale;
            if (labelRoot != null && labelRoot != body) labelRoot.localScale = Vector3.one;
            if (trail != null) { trail.emitting = false; trail.Clear(); }
            gameObject.SetActive(false);
        }
    }
}
