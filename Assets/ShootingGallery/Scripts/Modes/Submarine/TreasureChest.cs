using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 潜水艦モードの宝箱 (海底・沈んだ船・神殿にある)。ふたがパカッと開いたり、パタンと閉じたりをくり返す。
    /// 開いている間だけ、中の金貨と宝石がきらきら光って撃てる (点数の高い的)。閉じている間は撃っても何もない。
    /// 当てると金貨が飛び散ってふたがバタンと閉まり、しばらく開かない。
    /// </summary>
    public class TreasureChest : MonoBehaviour
    {
        [Tooltip("ふた (後ろのちょうつがいのまわりに回る)")]
        public Transform lid;
        public float openAngle = 105f;
        [Tooltip("当たり判定 (kind = SubmarineObject, manualControl, shrinkOnHit)。visual = 中の金貨と宝石の山 (開いている間だけ出る)")]
        public Target hitTarget;
        public int points = 1000;
        [Tooltip("中の光 (開いている間だけ光る)")]
        public Light glow;
        [Tooltip("閉じている秒数 / 開いている秒数 (この範囲でばらばら)")]
        public Vector2 closedTime = new Vector2(1.8f, 3.8f);
        public Vector2 openTime = new Vector2(1.6f, 2.6f);
        [Tooltip("当てた後、次に開くまでの秒数")]
        public Vector2 cooldown = new Vector2(4f, 6f);
        public float lidTime = 0.35f;
        public Material coinMaterial, sparkleMaterial, bubbleMaterial;

        public bool IsShown { get; private set; }

        enum State { Closed, Opening, Open, Closing }
        State state;
        float stateUntil, lidK, glowBase;
        bool armed;

        void Awake()
        {
            if (glow != null) glowBase = glow.intensity;
            if (hitTarget != null) hitTarget.SetAvailable(false);
            lidK = 0f;
            SetLid(0f);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            IsShown = true;
            hitTarget.points = points;
            hitTarget.SetAvailable(true); // manualControl なので、開くまでは出ない
            state = State.Closed;
            stateUntil = Time.time + Random.Range(0.5f, closedTime.y);
            armed = false;
            lidK = 0f;
            SetLid(0f);
        }

        public void Hide()
        {
            IsShown = false;
            hitTarget.SetAvailable(false);
            lidK = 0f;
            SetLid(0f);
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsShown) return;
            float now = Time.time;
            switch (state)
            {
                case State.Closed:
                    if (now >= stateUntil) state = State.Opening;
                    break;
                case State.Opening:
                    lidK = Mathf.MoveTowards(lidK, 1f, Time.deltaTime / lidTime);
                    SetLid(EaseOutBack(lidK));
                    if (lidK > 0.5f && !hitTarget.IsUp && hitTarget.CanRaise) { hitTarget.Raise(9999f); armed = false; } // 中の宝がふくらんで光る
                    if (lidK >= 1f) { state = State.Open; stateUntil = now + Random.Range(openTime.x, openTime.y); }
                    break;
                case State.Open:
                    if (now >= stateUntil) Close(false);
                    break;
                case State.Closing:
                    lidK = Mathf.MoveTowards(lidK, 0f, Time.deltaTime / (lidTime * 0.7f));
                    SetLid(lidK * lidK);
                    if (lidK <= 0f)
                    {
                        state = State.Closed;
                        Sfx.Play("thunk", 0.3f); // 宝箱は4つあって何度も閉まるので小さめ
                        Bubble.Burst(lid.position, 0.3f, bubbleMaterial, 5); // パタンと閉まって、泡がぽこっと出る
                    }
                    break;
            }

            if (hitTarget.IsUp) { armed = true; return; }
            if (!armed) return;
            // 当たった: 金貨と宝石が飛び散り、ふたがバタンと閉まって、しばらく開かない
            armed = false;
            var p = hitTarget.transform.position;
            if (coinMaterial != null) SpaceDebris.Spawn(p + Vector3.up * 0.2f, 1f, coinMaterial, 16);
            if (sparkleMaterial != null) Explosion.Spawn(p, 1.1f, sparkleMaterial);
            Bubble.Burst(p, 0.5f, bubbleMaterial, 12);
            Sfx.Play("coin");
            Close(true);
        }

        void Close(bool looted)
        {
            if (hitTarget.IsUp) hitTarget.Lower(); // 中の宝が縮んで隠れる
            armed = false;
            state = State.Closing;
            stateUntil = Time.time + (looted ? Random.Range(cooldown.x, cooldown.y) : Random.Range(closedTime.x, closedTime.y));
        }

        // ふたの開き具合 (0 = 閉じている 〜 1 = 開いている)。中の光も合わせる
        void SetLid(float k)
        {
            if (lid != null) lid.localRotation = Quaternion.Euler(openAngle * k, 0f, 0f);
            if (glow != null) glow.intensity = glowBase * Mathf.Clamp01(k);
        }

        static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.7f;
            float x = t - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }
    }
}
