using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 潜水艦モードの海の的 (クラゲ・フグ・アンコウ・タコ・ウミガメ・サメ・ほかの潜水艦・神殿の壺など)。
    ///   Circle: プレイヤーのまわりをぐるっと泳ぐ (前を向いて、上下にゆったり揺れる)
    ///   Drift : 決まった場所のあたりでふわふわ漂う (クラゲ・タコ)
    ///   Still : その場から動かない (壺)
    /// ひれ・しっぽ・足をゆらゆら動かし (wiggle)、プロペラを回し (spin)、体をふくらませたり縮めたりする (pulse)。
    /// 当てると縮んで消え、泡 (とタコは墨) がぶわっと出る。少しすると、またふくらんで現れる (泳ぐ的は別の所から)。
    /// 点数は Target が持っていて、GameManager が入れる。
    /// </summary>
    public class SeaCreature : MonoBehaviour
    {
        public enum Motion { Circle, Drift, Still }

        public string displayName = "FISH";
        [Tooltip("当たり判定 (kind = SubmarineObject, manualControl, shrinkOnHit)。visual = 見た目と当たり判定")]
        public Target hitTarget;
        public int points = 100;
        public float respawnDelay = 3f;

        [Header("動き (位置は親のローカル。海の真ん中 = プレイヤーの真下が原点)")]
        public Motion motion = Motion.Circle;
        [Tooltip("Circle: まわる中心 / Drift・Still: いる場所")]
        public Vector3 home;
        public float radius = 8f;
        [Tooltip("Circle: 前後の半径の割合 (1 = 円、小さいほど横長の楕円)")]
        public float squash = 1f;
        [Tooltip("Circle: まわる速さ (度/秒。マイナスで逆回り)")]
        public float angularSpeed = 12f;
        [Tooltip("上下に揺れる幅 (m) と速さ")]
        public float bob = 0.4f, bobSpeed = 0.7f;
        [Tooltip("Drift: 漂う範囲 (m)")]
        public float driftRange = 0.8f;

        [Header("体の動き")]
        public Transform[] wiggle;
        [Tooltip("wiggle の部品が揺れる軸 (部品のローカル) / 角度 / 速さ / 部品ごとにずらす位相")]
        public Vector3 wiggleAxis = Vector3.up;
        public float wiggleAmp = 20f, wiggleSpeed = 6f, wigglePhaseStep = 0.6f;
        public Transform[] spin;
        public Vector3 spinAxis = Vector3.forward;
        public float spinSpeed = 600f;
        [Tooltip("ふくらんだり縮んだりする部品 (クラゲのかさ・フグの体)")]
        public Transform pulse;
        public Vector3 pulseAmount = new Vector3(0.12f, -0.12f, 0.12f);
        public float pulseSpeed = 3f;
        [Tooltip("ON なら、ときどき大きくふくらむ (フグ)")]
        public bool puffer;

        [Header("当たった時")]
        public Material bubbleMaterial;
        [Tooltip("飛び散る破片 (壺のかけら・潜水艦の部品など。なければ出さない)")]
        public Material debrisMaterial;
        [Tooltip("ぶわっと広がる墨 (タコ。なければ出さない)")]
        public Material inkMaterial;
        [Tooltip("泳ぎながら後ろから泡を出す所 (潜水艦のプロペラなど。なければ出さない)")]
        public Transform trailVent;

        public bool IsShown { get; private set; }
        /// <summary>当たった時 (点数はもう入っている)</summary>
        public System.Action<SeaCreature> Downed;

        float angle, phase, respawnAt, nextTrail;
        bool armed, waiting;
        Quaternion[] wiggleBase;
        Vector3 pulseBase;

        void Awake()
        {
            phase = Random.value * 20f;
            angle = Random.Range(0f, 360f);
            wiggleBase = new Quaternion[wiggle != null ? wiggle.Length : 0];
            for (int i = 0; i < wiggleBase.Length; i++) wiggleBase[i] = wiggle[i].localRotation;
            if (pulse != null) pulseBase = pulse.localScale;
            if (hitTarget != null) hitTarget.SetAvailable(false);
        }

        /// <summary>現れる (撃てるようになる)</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            waiting = false;
            hitTarget.points = points;
            hitTarget.SetAvailable(true);
            hitTarget.Raise(9999f); // 縮んだ状態からふくらんで出る (shrinkOnHit)
            IsShown = true;
            armed = false;
            Move(0f);
        }

        public void Hide()
        {
            IsShown = false;
            waiting = false;
            hitTarget.SetAvailable(false);
            gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Move(dt);
            Animate();

            if (waiting)
            {
                if (Time.time >= respawnAt)
                {
                    if (motion == Motion.Circle) angle += Random.Range(90f, 200f); // 泳ぐ的は別の所から現れる
                    Show();
                }
                return;
            }
            if (!IsShown) return;
            if (trailVent != null && Time.time >= nextTrail)
            {
                nextTrail = Time.time + 0.12f;
                Bubble.Spawn(trailVent.position + Random.insideUnitSphere * 0.08f, Random.Range(0.05f, 0.11f), bubbleMaterial, Random.insideUnitSphere * 0.3f, 1.8f);
            }
            if (hitTarget.IsUp) { armed = true; return; }
            if (!armed) return;
            // 当たった: 泡がぶわっと出る (壺はかけら、タコは墨も)。少しして、また現れる
            var p = hitTarget.transform.position;
            Bubble.Burst(p, 0.6f, bubbleMaterial, 16);
            if (debrisMaterial != null) SpaceDebris.Spawn(p, 0.7f, debrisMaterial, 10);
            if (inkMaterial != null) Explosion.Spawn(p, 1.4f, inkMaterial);
            Sfx.Play("splash");
            IsShown = false;
            Downed?.Invoke(this);
            waiting = true;
            respawnAt = Time.time + respawnDelay * Random.Range(0.8f, 1.3f);
        }

        void Move(float dt)
        {
            float t = Time.time + phase;
            switch (motion)
            {
                case Motion.Circle:
                {
                    angle += angularSpeed * dt;
                    float a = angle * Mathf.Deg2Rad;
                    var p = home + new Vector3(Mathf.Sin(a) * radius, Mathf.Sin(t * bobSpeed) * bob, Mathf.Cos(a) * radius * squash);
                    // 進む向き (円の接線) を向く。少しだけ上下にも傾ける
                    float s = Mathf.Sign(angularSpeed);
                    var tangent = new Vector3(Mathf.Cos(a) * s, Mathf.Cos(t * bobSpeed) * bob * bobSpeed * 0.15f, -Mathf.Sin(a) * squash * s);
                    transform.localPosition = p;
                    if (tangent.sqrMagnitude > 0.0001f) transform.localRotation = Quaternion.LookRotation(tangent.normalized);
                    break;
                }
                case Motion.Drift:
                    transform.localPosition = home + new Vector3(Mathf.Sin(t * 0.21f) * driftRange, Mathf.Sin(t * bobSpeed) * bob, Mathf.Cos(t * 0.17f) * driftRange);
                    break;
                case Motion.Still:
                    transform.localPosition = home;
                    break;
            }
        }

        void Animate()
        {
            float t = Time.time + phase;
            for (int i = 0; i < wiggleBase.Length; i++)
            {
                if (wiggle[i] == null) continue;
                float w = Mathf.Sin(t * wiggleSpeed - i * wigglePhaseStep) * wiggleAmp;
                wiggle[i].localRotation = wiggleBase[i] * Quaternion.AngleAxis(w, wiggleAxis);
            }
            if (spin != null)
                foreach (var s in spin) if (s != null) s.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);
            if (pulse != null)
            {
                float k;
                if (puffer)
                {
                    // フグ: ふだんは小さく、ときどきぷくーっと大きくふくらんで、しぼむ
                    float w = Mathf.Sin(t * pulseSpeed * 0.35f);
                    k = Mathf.Pow(Mathf.Max(0f, w), 3f);
                    pulse.localScale = pulseBase * (1f + 0.45f * k);
                    return;
                }
                k = Mathf.Sin(t * pulseSpeed);
                pulse.localScale = pulseBase + Vector3.Scale(pulseBase, pulseAmount) * k;
            }
        }
    }
}
