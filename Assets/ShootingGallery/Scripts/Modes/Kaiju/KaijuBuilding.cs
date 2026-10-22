using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 怪獣モードの的のビル・タワー (色とりどりで、ほかの灰色の建物より一回り大きい。上に大きく点数)。
    /// 炎の玉が当たると、いくつものかけら (pieces) に割れて、ばらばらに外へはじけながら地面へ落ち、爆発と土煙が上がる。
    /// 落ちたかけらは地面でしばらく転がってから沈んで消え、少しすると地面から建て直されて、また的になる。
    /// かけらの動きは body のローカル (根元が地面 y = 0) で計算する。
    /// </summary>
    public class KaijuBuilding : MonoBehaviour
    {
        [Tooltip("このビルの当たり判定 (kind = KaijuBuilding, manualControl。visual は空のオブジェクト)")]
        public Target target;
        [Tooltip("ビルの見た目 (根元が地面)。かけらはこの直下に並べる")]
        public Transform body;
        [Tooltip("割れて落ちるかけら (body の直下。それぞれの真ん中が原点)")]
        public Transform[] pieces;
        [Tooltip("点数の看板 (崩れている間は隠す)")]
        public GameObject sign;
        [Tooltip("ビル全体の当たり判定 (立っている間だけ有効。かけらには当たり判定がない)")]
        public Collider hitBox;
        [Tooltip("ビルの大きさ (幅・高さ・奥行き。body のローカル)。かけらのはじけ方や爆発・土煙の大きさに使う")]
        public Vector3 size = new Vector3(1f, 3f, 1f);
        public Material explosionMaterial;
        public Material dustMaterial;
        [Tooltip("崩れてから建て直されるまでの秒数")]
        public float rebuildDelay = 4f;
        public float rebuildTime = 0.8f;

        const float Gravity = 14f;      // (body のローカルの単位で。ビルの高さが数 m なので、本物より速めに落ちる)
        const float FallTime = 1.8f;    // 割れてから、かけらが地面に落ちて落ち着くまで
        const float SinkTime = 1.2f;    // 落ちたかけらが地面へ沈んで消えるまで

        enum State { Off, Standing, Collapsing, Rubble, Rebuilding }
        State state = State.Off;
        float timer;
        Vector3 bodyScale;
        Vector3[] homePos, homeScale, vel, spinAxis;
        Quaternion[] homeRot;
        float[] spin, delay, half;
        bool[] landed;
        int puffs;

        void Awake()
        {
            bodyScale = body.localScale;
            int n = pieces != null ? pieces.Length : 0;
            homePos = new Vector3[n]; homeScale = new Vector3[n]; vel = new Vector3[n]; spinAxis = new Vector3[n];
            homeRot = new Quaternion[n];
            spin = new float[n]; delay = new float[n]; half = new float[n];
            landed = new bool[n];
            for (int i = 0; i < n; i++)
            {
                homePos[i] = pieces[i].localPosition;
                homeRot[i] = pieces[i].localRotation;
                homeScale[i] = pieces[i].localScale;
                // かけらの大きさの半分 (地面に着いた時の高さ)。かけらの中の見た目から測る
                var r = pieces[i].GetComponentsInChildren<Renderer>(true);
                float h = 0.15f;
                foreach (var rend in r)
                {
                    var mf = rend.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var b = mf.sharedMesh.bounds;
                    var s = Vector3.Scale(b.size, rend.transform.localScale);
                    h = Mathf.Max(h, Mathf.Min(s.x, Mathf.Min(s.y, s.z)) * 0.5f);
                }
                half[i] = h;
            }
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
            body.localScale = bodyScale;
            body.localRotation = Quaternion.identity;
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i].localPosition = homePos[i];
                pieces[i].localRotation = homeRot[i];
                pieces[i].localScale = homeScale[i];
                pieces[i].gameObject.SetActive(true);
            }
            if (sign != null) sign.SetActive(true);
            if (hitBox != null) hitBox.enabled = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (state)
            {
                case State.Standing:
                    if (!target.IsUp && !target.IsVisible) Break(); // 当たった (当たり判定の見た目は空なので、倒れたら見えなくなる)
                    break;
                case State.Collapsing:
                    timer += dt;
                    UpdatePieces(dt);
                    // 足元から土煙が次々に上がる
                    if (puffs < 6 && timer >= puffs * 0.22f)
                    {
                        puffs++;
                        var p = body.TransformPoint(new Vector3(Random.Range(-0.7f, 0.7f) * size.x, Random.Range(0.1f, 0.5f), Random.Range(-0.7f, 0.7f) * size.z));
                        Explosion.Spawn(p, Random.Range(0.5f, 0.9f) * size.x * body.lossyScale.x, dustMaterial);
                    }
                    if (timer >= FallTime)
                    {
                        state = State.Rubble;
                        timer = 0f;
                    }
                    break;
                case State.Rubble:
                {
                    // 落ちたかけらが、ゆっくり地面へ沈んで消える
                    timer += dt;
                    float k = Mathf.Clamp01(timer / SinkTime);
                    for (int i = 0; i < pieces.Length; i++)
                    {
                        pieces[i].localScale = homeScale[i] * (1f - k * k);
                        var p = pieces[i].localPosition;
                        p.y -= dt * half[i];
                        pieces[i].localPosition = p;
                        if (k >= 1f) pieces[i].gameObject.SetActive(false);
                    }
                    if (timer >= rebuildDelay)
                    {
                        state = State.Rebuilding;
                        timer = 0f;
                        for (int i = 0; i < pieces.Length; i++)
                        {
                            pieces[i].localPosition = homePos[i];
                            pieces[i].localRotation = homeRot[i];
                            pieces[i].localScale = homeScale[i];
                            pieces[i].gameObject.SetActive(true);
                        }
                    }
                    break;
                }
                case State.Rebuilding:
                {
                    // 地面から伸びて建て直される
                    timer += dt;
                    float k = Mathf.Clamp01(timer / rebuildTime);
                    float e = 1f - (1f - k) * (1f - k);
                    body.localScale = new Vector3(bodyScale.x, bodyScale.y * Mathf.Max(0.02f, e), bodyScale.z);
                    if (k >= 1f) Activate();
                    break;
                }
            }
        }

        // 割れる: かけらごとに、ビルの真ん中から外へはじける速さと、回る向きを決める (上のかけらほど高く、遠くへ)
        void Break()
        {
            state = State.Collapsing;
            timer = 0f;
            puffs = 0;
            if (hitBox != null) hitBox.enabled = false;
            if (sign != null) sign.SetActive(false);
            float s = body.lossyScale.x;
            Explosion.Spawn(body.TransformPoint(new Vector3(0f, size.y * 0.75f, 0f)), size.x * 1.3f * s, explosionMaterial);
            Explosion.Spawn(body.TransformPoint(new Vector3(0f, size.y * 0.4f, 0f)), size.x * 1.0f * s, explosionMaterial);
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = homePos[i];
                float up = Mathf.Clamp01(p.y / Mathf.Max(0.1f, size.y));
                var outward = new Vector3(p.x, 0f, p.z);
                if (outward.sqrMagnitude < 0.0004f) outward = Random.insideUnitCircle.normalized;
                outward = new Vector3(outward.x, 0f, outward.z).normalized;
                outward = (outward + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f))).normalized;
                vel[i] = outward * Random.Range(0.6f, 1.6f) * (0.6f + up) * Mathf.Max(1f, size.x * 0.6f) + Vector3.up * Random.Range(0.3f, 2.2f) * up;
                spinAxis[i] = Random.onUnitSphere;
                spin[i] = Random.Range(120f, 360f);
                delay[i] = (1f - up) * 0.18f + Random.Range(0f, 0.08f); // (上から順に割れて落ちる)
                landed[i] = false;
            }
        }

        void UpdatePieces(float dt)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (timer < delay[i]) continue;
                var tr = pieces[i];
                var p = tr.localPosition;
                if (!landed[i])
                {
                    vel[i] += Vector3.down * Gravity * dt;
                    p += vel[i] * dt;
                    tr.localRotation = Quaternion.AngleAxis(spin[i] * dt, spinAxis[i]) * tr.localRotation;
                    if (p.y <= half[i])
                    {   // 地面に落ちた: 小さくはねて止まる
                        p.y = half[i];
                        if (vel[i].y < -2f) vel[i] = new Vector3(vel[i].x * 0.4f, -vel[i].y * 0.25f, vel[i].z * 0.4f);
                        else landed[i] = true;
                        spin[i] *= 0.4f;
                    }
                }
                tr.localPosition = p;
            }
        }
    }
}
