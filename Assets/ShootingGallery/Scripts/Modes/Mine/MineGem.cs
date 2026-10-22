using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 発掘モードの宝石 (洞窟の壁や床から生えた、光る結晶のかたまり)。宝石そのものに点数があり、すぐそばに大きく点数が浮かんでいる。
    /// 当てると縮んで、きらきらした破片が飛び散って消える。少しすると、どこか別の空いている宝石がまた光り始める (MineCave)。
    /// 天井から落ちてくる宝石 (Drop) にも使う: 床に当たるまでに撃てば割れて点数が入り、床に当たると点数なしで砕ける。
    /// </summary>
    public class MineGem : MonoBehaviour
    {
        public string displayName = "AMETHYST";
        [Tooltip("当たり判定 (kind = MineObject, manualControl, shrinkOnHit)。visual = 結晶と点数の文字")]
        public Target hitTarget;
        public TextMesh pointLabel;
        public int points = 100;
        [Tooltip("飛び散る破片の色")]
        public Material shardMaterial;
        public Material sparkleMaterial;
        [Tooltip("落ちてくる宝石: 落ちながらくるくる回す部分 (結晶。点数の文字は回さない) / 落ちる後ろに引く光の尾")]
        public Transform spinPart;
        public TrailRenderer fallTrail;
        [Tooltip("落ちてくる宝石: 落ちる速さが増える割合 (m/秒²。本物の重力より小さく、撃つ間がある)")]
        public float fallGravity = 3.4f;

        public bool IsShown { get; private set; }
        System.Action<MineGem> onBroken, onMissed;
        bool armed;
        bool falling;
        float fallSpeed, floorY, spinSpeed;
        Vector3 spinAxis;

        void Awake() => hitTarget.SetAvailable(false);

        /// <summary>光り始める (撃てるようになる)</summary>
        public void Show(System.Action<MineGem> brokenCallback)
        {
            gameObject.SetActive(true);
            onBroken = brokenCallback;
            hitTarget.points = points;
            if (pointLabel != null) pointLabel.text = points.ToString();
            hitTarget.SetAvailable(true);
            hitTarget.Raise(9999f); // 縮んだ状態からふくらんで出る (shrinkOnHit)
            IsShown = true;
            armed = false;
        }

        /// <summary>
        /// 天井の start (ワールド座標) から落とす。床の高さ floorWorldY に当たるまでに撃たれたら broken (点数はもう入っている)、
        /// 当たったら点数なしで砕けて missed
        /// </summary>
        public void Drop(Vector3 start, float floorWorldY, System.Action<MineGem> broken, System.Action<MineGem> missed)
        {
            transform.position = start;
            falling = true;
            fallSpeed = 0f;
            floorY = floorWorldY;
            onMissed = missed;
            spinAxis = Random.onUnitSphere;
            spinSpeed = Random.Range(90f, 200f);
            Show(broken);
            if (fallTrail != null) { fallTrail.Clear(); fallTrail.emitting = true; }
        }

        public void Hide()
        {
            IsShown = false;
            falling = false;
            if (fallTrail != null) fallTrail.emitting = false;
            hitTarget.SetAvailable(false);
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsShown) return;
            if (falling && hitTarget.IsUp)
            {
                // 落ちていく (だんだん速く。結晶はくるくる回る)
                fallSpeed += fallGravity * Time.deltaTime;
                transform.position += Vector3.down * (fallSpeed * Time.deltaTime);
                if (spinPart != null) spinPart.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);
                if (transform.position.y <= floorY)
                {
                    // 床に当たった: 点数なしで砕ける
                    var p = transform.position;
                    if (shardMaterial != null) SpaceDebris.Spawn(p, 0.6f, shardMaterial, 10);
                    Sfx.Play("hit");
                    var missed = onMissed;
                    Hide();
                    missed?.Invoke(this);
                    return;
                }
            }
            if (hitTarget.IsUp) { armed = true; return; }
            if (!armed) return;
            // 当たった: きらきらした破片が飛び散る (点数はもう入っている)
            falling = false;
            if (fallTrail != null) fallTrail.emitting = false;
            var hit = hitTarget.transform.position;
            if (shardMaterial != null) SpaceDebris.Spawn(hit, 0.8f, shardMaterial, 14);
            if (sparkleMaterial != null) Explosion.Spawn(hit, 0.9f, sparkleMaterial);
            Sfx.Play("coin");
            IsShown = false;
            onBroken?.Invoke(this);
            Invoke(nameof(HideAfterShrink), 0.25f);
        }

        void HideAfterShrink() { if (!IsShown) Hide(); }
    }
}
