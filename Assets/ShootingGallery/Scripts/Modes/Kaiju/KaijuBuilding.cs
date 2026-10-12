using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 怪獣モードの的のビル (黄色や青の、ほかより一回り大きいビル。屋上に大きく点数)。
    /// 炎の玉が当たると、揺れながら崩れて地面に沈み、爆発と土煙が上がる。しばらくすると建て直されて、また的になる。
    /// </summary>
    public class KaijuBuilding : MonoBehaviour
    {
        [Tooltip("このビルの当たり判定 (kind = KaijuBuilding, manualControl。visual は空のオブジェクト)")]
        public Target target;
        [Tooltip("崩れる部分 (ビルの見た目。根元が地面)")]
        public Transform body;
        [Tooltip("ビルの大きさ (幅・高さ・奥行き m)。崩れる時の爆発や土煙の大きさに使う")]
        public Vector3 size = new Vector3(1f, 3f, 1f);
        public Material explosionMaterial;
        public Material dustMaterial;
        [Tooltip("崩れてから建て直されるまでの秒数")]
        public float rebuildDelay = 5f;
        public float collapseTime = 1.1f;
        public float rebuildTime = 0.8f;

        enum State { Off, Standing, Collapsing, Rubble, Rebuilding }
        State state = State.Off;
        float timer;
        Vector3 bodyPos, bodyScale;
        int puffs;

        void Awake()
        {
            bodyPos = body.localPosition;
            bodyScale = body.localScale;
            target.SetAvailable(false);
        }

        /// <summary>的として出す (街がせり上がり終わった時)</summary>
        public void Activate()
        {
            ResetBody();
            state = State.Standing;
            target.SetAvailable(true);
            target.Raise(9999f);
        }

        /// <summary>的をしまう (怪獣モードの終わり)。崩れていても元の形に戻す</summary>
        public void Deactivate()
        {
            state = State.Off;
            target.SetAvailable(false);
            ResetBody();
        }

        void ResetBody()
        {
            body.localPosition = bodyPos;
            body.localScale = bodyScale;
            body.localRotation = Quaternion.identity;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (state)
            {
                case State.Standing:
                    if (!target.IsUp && !target.IsVisible) // 当たった (当たり判定の見た目は空なので、倒れたら見えなくなる)
                    {
                        state = State.Collapsing;
                        timer = 0f;
                        puffs = 0;
                        Explosion.Spawn(body.position + Vector3.up * size.y * 0.7f, size.x * 1.2f, explosionMaterial);
                    }
                    break;
                case State.Collapsing:
                {
                    // 揺れながら縮んで地面へ沈み、土煙が次々に上がる
                    timer += dt;
                    float k = Mathf.Clamp01(timer / collapseTime);
                    body.localScale = new Vector3(bodyScale.x * (1f + 0.08f * k), bodyScale.y * Mathf.Lerp(1f, 0.08f, k * k), bodyScale.z * (1f + 0.08f * k));
                    body.localPosition = bodyPos + new Vector3(Mathf.Sin(timer * 60f) * 0.04f * size.x, 0f, Mathf.Cos(timer * 53f) * 0.04f * size.x) * (1f - k);
                    body.localRotation = Quaternion.Euler(Mathf.Sin(timer * 7f) * 6f * k, 0f, Mathf.Cos(timer * 5f) * 6f * k);
                    if (puffs < 5 && timer >= puffs * collapseTime / 5f)
                    {
                        puffs++;
                        var p = body.position + new Vector3(Random.Range(-1f, 1f) * size.x * 0.6f, Random.Range(0.1f, 0.6f), Random.Range(-1f, 1f) * size.z * 0.6f);
                        Explosion.Spawn(p, Random.Range(0.5f, 0.9f) * size.x, dustMaterial);
                    }
                    if (k >= 1f)
                    {
                        state = State.Rubble;
                        timer = 0f;
                    }
                    break;
                }
                case State.Rubble:
                    timer += dt;
                    if (timer >= rebuildDelay)
                    {
                        state = State.Rebuilding;
                        timer = 0f;
                        body.localRotation = Quaternion.identity;
                    }
                    break;
                case State.Rebuilding:
                {
                    // 地面から伸びて建て直される
                    timer += dt;
                    float k = Mathf.Clamp01(timer / rebuildTime);
                    float e = 1f - (1f - k) * (1f - k);
                    body.localScale = new Vector3(bodyScale.x, bodyScale.y * Mathf.Max(0.08f, e), bodyScale.z);
                    body.localPosition = bodyPos;
                    if (k >= 1f) Activate();
                    break;
                }
            }
        }
    }
}
