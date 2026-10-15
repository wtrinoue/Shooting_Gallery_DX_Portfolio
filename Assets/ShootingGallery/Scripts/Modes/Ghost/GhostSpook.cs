using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 幽霊モードの的 (どれも点数が大きく見えるパネル)。
    ///   お化け: プレイヤーのまわりを、ふわふわ上下しながらぐるぐる回る。
    ///   魔女: ほうきに乗って、高い所を速めにぐるっと飛んでいく (進む向きに顔を向け、少し傾く)。
    ///   ゾンビ / 骸骨: お墓の前の土からせり上がって「こんにちは」し、ゆらゆら揺れてから土へ戻る。
    /// 当てるとその場で縮んで、紫の煙がぽんと出て消える。位置は親 (GhostManor。プレイヤーの足元が原点) のローカル座標で決める。
    /// 表 (-Z) はいつもプレイヤーの方を向く (FaceCenter)。反撃はしない。
    /// </summary>
    public class GhostSpook : MonoBehaviour
    {
        public enum Kind { Ghost, Witch, Zombie, Skeleton }

        public Kind kind = Kind.Ghost;
        public string displayName = "GHOST";
        [Tooltip("ゆれる・傾く見た目の部分 (パネルと点数の札)")]
        public Transform body;
        [Tooltip("進む向きで左右を反転する絵の部分 (点数の札は入れない。文字が裏返らないように)")]
        public Transform flip;
        [Tooltip("当たり判定 (kind = GhostSpook, manualControl, shrinkOnHit)")]
        public Target hitTarget;
        [Tooltip("大きく書いた点数 (出るたびに書き直す)")]
        public TextMesh[] pointLabels;
        public int[] pointChoices = { 100, 200 };

        [Header("まわりを飛ぶ (お化け・魔女)")]
        [Tooltip("プレイヤーからの距離 (m)")]
        public Vector2 orbitRadius = new Vector2(5f, 8f);
        [Tooltip("高さ (m。プレイヤーの足元が 0)")]
        public Vector2 orbitHeight = new Vector2(1.2f, 3f);
        [Tooltip("回る速さ (度/秒)")]
        public Vector2 orbitSpeed = new Vector2(14f, 26f);
        [Tooltip("出ている秒数 (撃たれなければ消える)")]
        public Vector2 orbitLife = new Vector2(7f, 10f);
        [Tooltip("ふわふわ上下する幅 (m)")]
        public float bobHeight = 0.35f;

        [Header("お墓からせり上がる (ゾンビ・骸骨)")]
        [Tooltip("土の下のどこから出てくるか (m)")]
        public float riseDepth = 2.2f;
        public float riseTime = 1.1f;
        [Tooltip("出ている秒数 (撃たれなければ土へ戻る)")]
        public Vector2 stayTime = new Vector2(3.5f, 5f);

        [Header("当たった時")]
        [Tooltip("ぽんと出る煙の材質と大きさ")]
        public Material puffMaterial;
        public float puffSize = 0.9f;

        const float AppearTime = 0.45f;
        const float VanishTime = 0.4f;
        const float HitHold = 0.3f; // 縮むのを見せてから消える

        enum State { Idle, Orbit, Rise, Stay, Sink, Fade, Hit }
        State state = State.Idle;

        public bool IsBusy => state != State.Idle;
        public bool IsOut => state == State.Orbit || state == State.Rise || state == State.Stay;
        public int Points => hitTarget != null ? hitTarget.points : 0;
        /// <summary>今せり上がっているお墓 (お墓から出る的だけ。-1 = なし)</summary>
        public int Grave { get; private set; } = -1;

        System.Action<GhostSpook> onDown;
        float t, life, angle, angSpeed, radius, height, phase;
        Vector3 home;
        bool armed;
        Vector3 fullScale = Vector3.one;

        void Awake()
        {
            hitTarget.SetAvailable(false);
            fullScale = transform.localScale;
        }

        /// <summary>まわりを飛び始める (お化け・魔女)。startDeg = 出てくる方向 (度)</summary>
        public void LaunchOrbit(float startDeg, System.Action<GhostSpook> downCallback)
        {
            Begin(downCallback);
            Grave = -1;
            angle = startDeg;
            angSpeed = Random.Range(orbitSpeed.x, orbitSpeed.y) * (Random.value < 0.5f ? 1f : -1f);
            radius = Random.Range(orbitRadius.x, orbitRadius.y);
            height = Random.Range(orbitHeight.x, orbitHeight.y);
            life = Random.Range(orbitLife.x, orbitLife.y);
            state = State.Orbit;
            PlaceOrbit();
            transform.localScale = fullScale * 0.001f;
        }

        /// <summary>お墓の前の土からせり上がる (ゾンビ・骸骨)。spot = 出てくる地面の場所, grave = お墓の番号</summary>
        public void LaunchRise(Vector3 spot, int grave, System.Action<GhostSpook> downCallback)
        {
            Begin(downCallback);
            Grave = grave;
            home = spot;
            life = Random.Range(stayTime.x, stayTime.y);
            state = State.Rise;
            transform.localScale = fullScale;
            transform.localPosition = home + Vector3.down * riseDepth;
            if (puffMaterial != null) Explosion.Spawn(transform.parent.TransformPoint(home), 0.5f, puffMaterial); // 土がもこっ
        }

        void Begin(System.Action<GhostSpook> downCallback)
        {
            gameObject.SetActive(true);
            onDown = downCallback;
            t = 0f;
            armed = false;
            phase = Random.value * 10f;
            body.localRotation = Quaternion.identity;
            body.localPosition = Vector3.zero;
            hitTarget.points = pointChoices.Length > 0 ? pointChoices[Random.Range(0, pointChoices.Length)] : 100;
            foreach (var label in pointLabels) if (label != null) label.text = hitTarget.points.ToString();
            hitTarget.SetAvailable(true);
            hitTarget.Raise(9999f);
        }

        /// <summary>撃たれずに去る (幽霊モードの終わり)</summary>
        public void Retreat()
        {
            if (!IsOut) return;
            hitTarget.SetAvailable(false);
            if (state == State.Orbit) { state = State.Fade; t = 0f; }
            else { state = State.Sink; t = 0f; }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            switch (state)
            {
                case State.Orbit:
                    angle += angSpeed * dt;
                    PlaceOrbit();
                    float k = Mathf.Clamp01(t / AppearTime);
                    transform.localScale = fullScale * Mathf.Max(0.001f, 1f - (1f - k) * (1f - k));
                    if (CheckHit()) return;
                    if (t >= life) { hitTarget.SetAvailable(false); state = State.Fade; t = 0f; }
                    break;

                case State.Rise:
                {
                    float r = Mathf.Clamp01(t / riseTime);
                    float e = 1f - (1f - r) * (1f - r) * (1f - r) + Mathf.Sin(r * Mathf.PI) * 0.08f; // ぐぐっと出て、少し行き過ぎて戻る
                    transform.localPosition = home + Vector3.down * riseDepth * (1f - e);
                    Sway();
                    if (CheckHit()) return;
                    if (r >= 1f) { state = State.Stay; t = 0f; }
                    break;
                }

                case State.Stay:
                    transform.localPosition = home;
                    Sway();
                    if (CheckHit()) return;
                    if (t >= life) { hitTarget.SetAvailable(false); state = State.Sink; t = 0f; }
                    break;

                case State.Sink:
                    transform.localPosition += Vector3.down * (riseDepth / riseTime) * dt;
                    if (t >= riseTime) Vanish();
                    break;

                case State.Fade:
                    transform.localScale = fullScale * Mathf.Max(0.001f, 1f - t / VanishTime);
                    if (kind == Kind.Ghost || kind == Kind.Witch) { angle += angSpeed * dt; PlaceOrbit(); }
                    if (t >= VanishTime) Vanish();
                    break;

                case State.Hit:
                    if (t >= HitHold) Vanish();
                    break;
            }
        }

        // 当たった (的が倒れた = 縮み始めた): 紫の煙がぽんと出て、少し後に消える
        bool CheckHit()
        {
            if (hitTarget.IsUp) armed = true;
            if (!armed || hitTarget.IsUp) return false;
            state = State.Hit;
            t = 0f;
            if (puffMaterial != null)
            {
                var p = body.position + Vector3.up * 0.4f;
                Explosion.Spawn(p, puffSize, puffMaterial);
                for (int i = 0; i < 3; i++) Explosion.Spawn(p + Random.insideUnitSphere * puffSize * 0.6f, puffSize * 0.45f, puffMaterial);
            }
            Sfx.Play("ghostpop");
            onDown?.Invoke(this);
            return true;
        }

        // ぐるぐる回る位置。ふわふわ上下し、魔女は進む向きに顔を向けて少し傾く (パネルの表はいつもプレイヤー向き)
        void PlaceOrbit()
        {
            float a = angle * Mathf.Deg2Rad;
            float bob = Mathf.Sin((Time.time + phase) * (kind == Kind.Witch ? 2.2f : 1.6f)) * bobHeight;
            transform.localPosition = new Vector3(Mathf.Sin(a) * radius, height + bob, Mathf.Cos(a) * radius);
            // angSpeed > 0 の時はプレイヤーから見て右 (+X) へ進む。絵は左 (-X) へ進む向きに作ってあるので、右へ進む時は左右を反転する
            float face = angSpeed > 0f ? -1f : 1f;
            if (flip != null)
            {
                var s = flip.localScale;
                if (Mathf.Sign(s.x) != face) flip.localScale = new Vector3(Mathf.Abs(s.x) * face, s.y, s.z);
            }
            float tilt = kind == Kind.Witch ? 10f : 6f;
            body.localRotation = Quaternion.Euler(0f, 0f, face * tilt + Mathf.Sin((Time.time + phase) * 1.3f) * 4f);
        }

        // ゾンビ・骸骨: ゆらゆら左右に揺れる
        void Sway() => body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((Time.time + phase) * 2.4f) * 7f);

        void Vanish()
        {
            state = State.Idle;
            Grave = -1;
            hitTarget.SetAvailable(false);
            transform.localScale = fullScale;
            gameObject.SetActive(false);
        }
    }
}
