using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 発掘モードの宝石 (洞窟の壁や床から生えた、光る結晶のかたまり)。宝石そのものに点数があり、すぐそばに大きく点数が浮かんでいる。
    /// 当てると縮んで、きらきらした破片が飛び散って消える。少しすると、どこか別の空いている宝石がまた光り始める (MineCave)。
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

        public bool IsShown { get; private set; }
        System.Action<MineGem> onBroken;
        bool armed;

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

        public void Hide()
        {
            IsShown = false;
            hitTarget.SetAvailable(false);
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsShown) return;
            if (hitTarget.IsUp) { armed = true; return; }
            if (!armed) return;
            // 当たった: きらきらした破片が飛び散る (点数はもう入っている)
            var p = hitTarget.transform.position;
            if (shardMaterial != null) SpaceDebris.Spawn(p, 0.8f, shardMaterial, 14);
            if (sparkleMaterial != null) Explosion.Spawn(p, 0.9f, sparkleMaterial);
            Sfx.Play("coin");
            IsShown = false;
            onBroken?.Invoke(this);
            Invoke(nameof(HideAfterShrink), 0.25f);
        }

        void HideAfterShrink() { if (!IsShown) Hide(); }
    }
}
