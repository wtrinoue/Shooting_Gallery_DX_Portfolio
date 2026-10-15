using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 夏祭りモードで投げる / 撃つ物 (輪投げの輪・コルクの弾・金魚すくいのポイ・お金)。重力で放物線を描いて飛び、当たった所で種類ごとに結果を出す:
    ///   輪   : 近くの杭 (FestivalRing の的) にかかったら点数。輪は杭の根元へ落ちて少し残る。外れたら床に寝て消える。
    ///   コルク: 当たった景品 (FestivalPrize の的) が後ろへ倒れて落ちる。
    ///   ポイ : 水面に入ったら、夏祭りの舞台 (FestivalStage) が近くの金魚をすくえたか / 破れたかを決める。
    ///   お金 : 当たった料理 (FestivalFood の的) を買う (お金 = スコアが足りるか FestivalStage が確かめる)。
    /// 見た目は投げるたびに作らず、FxPool で使い回す。
    /// </summary>
    public class FestivalThrow : MonoBehaviour
    {
        public enum Kind { Ring, Cork, Poi, Coin }

        const float MaxLifetime = 5f;
        const float RingCatchRadius = 0.24f; // 輪が落ちた所から、杭の軸までこの距離以内の杭にかかったことにする

        Kind kind;
        Vector3 velocity;
        float gravity;
        float age;
        LayerMask mask;
        Transform spin;
        Vector3 spinAxis;
        float spinSpeed;
        bool landed;
        string poolKey;

        static readonly Collider[] nearby = new Collider[32];

        /// <summary>origin から velocity で投げる (gravity = 下向きの加速度)。見た目の材質は銃 (GunController) が持っている物を使う</summary>
        public static void Spawn(Kind kind, Vector3 origin, Vector3 velocity, float gravity, GunController gun, LayerMask mask)
        {
            string key = "Festival" + kind;
            var go = FxPool.Get(key, () => Create(kind, key, gun));
            var t = go.GetComponent<FestivalThrow>();
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(velocity.sqrMagnitude > 0.0001f ? velocity : Vector3.forward));
            go.transform.localScale = Vector3.one;
            t.velocity = velocity;
            t.gravity = gravity;
            t.age = 0f;
            t.mask = mask;
            t.landed = false;
            if (t.spin != null) t.spin.localRotation = Quaternion.identity;
            // 輪は毎回ちがう色 (赤・青・黄・緑)
            if (kind == Kind.Ring && gun.ringMaterials != null && gun.ringMaterials.Length > 0)
            {
                var r = go.GetComponentInChildren<Renderer>();
                if (r != null) r.sharedMaterial = gun.ringMaterials[Random.Range(0, gun.ringMaterials.Length)];
            }
        }

        // 見た目を部品から作る (FxPool に無い時だけ)
        static GameObject Create(Kind kind, string key, GunController gun)
        {
            var g = new GameObject(key);
            var t = g.AddComponent<FestivalThrow>();
            t.kind = kind;
            t.poolKey = key;
            t.spin = new GameObject("Spin").transform;
            t.spin.SetParent(g.transform, false);
            switch (kind)
            {
                case Kind.Ring: // 太めの輪。水平に寝たまま、くるくる回って飛ぶ
                {
                    var ring = new GameObject("Ring");
                    ring.transform.SetParent(t.spin, false);
                    ring.transform.localScale = Vector3.one * 0.3f;
                    ring.AddComponent<MeshFilter>().sharedMesh = gun.ringMesh;
                    var r = ring.AddComponent<MeshRenderer>();
                    r.sharedMaterial = gun.ringMaterials != null && gun.ringMaterials.Length > 0 ? gun.ringMaterials[0] : null;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    t.spinAxis = Vector3.up;
                    t.spinSpeed = 540f;
                    break;
                }
                case Kind.Cork: // 茶色いコルクの栓 (先を前に向けて飛ぶ)
                    Part(PrimitiveType.Cylinder, t.spin, new Vector3(0f, 0f, 0f), new Vector3(0.032f, 0.02f, 0.032f), Quaternion.Euler(90f, 0f, 0f), gun.corkMaterial);
                    Part(PrimitiveType.Cylinder, t.spin, new Vector3(0f, 0f, 0.02f), new Vector3(0.026f, 0.004f, 0.026f), Quaternion.Euler(90f, 0f, 0f), gun.corkMaterial);
                    break;
                case Kind.Poi: // 白い紙を張った丸い枠と持ち手。水平に寝て飛ぶ
                    Part(PrimitiveType.Cylinder, t.spin, Vector3.zero, new Vector3(0.11f, 0.003f, 0.11f), Quaternion.identity, gun.poiPaperMaterial);
                    Part(PrimitiveType.Cylinder, t.spin, new Vector3(0f, 0.002f, 0f), new Vector3(0.12f, 0.002f, 0.12f), Quaternion.identity, gun.poiFrameMaterial);
                    Part(PrimitiveType.Cube, t.spin, new Vector3(0f, 0.002f, -0.1f), new Vector3(0.018f, 0.006f, 0.1f), Quaternion.identity, gun.poiFrameMaterial);
                    t.spinAxis = Vector3.up;
                    t.spinSpeed = 160f;
                    break;
                default: // 金色の硬貨。縦にくるくる回って飛ぶ
                    Part(PrimitiveType.Cylinder, t.spin, Vector3.zero, new Vector3(0.05f, 0.003f, 0.05f), Quaternion.Euler(90f, 0f, 0f), gun.coinMaterial);
                    t.spinAxis = Vector3.right;
                    t.spinSpeed = 1080f;
                    break;
            }
            return g;
        }

        static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var p = FxPool.Primitive(type, mat);
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = rot;
            p.transform.localScale = scale;
        }

        void Update()
        {
            if (landed) return;
            float dt = Time.deltaTime;
            age += dt;
            // 重力のかかった動きをそのまま計算する (フレームの長さによらず、手元に描いた放物線と同じ所に落ちる)
            Vector3 step = velocity * dt + Vector3.down * (0.5f * gravity * dt * dt);
            velocity += Vector3.down * (gravity * dt);
            float dist = step.magnitude;
            if (spin != null && spinSpeed != 0f) spin.Rotate(spinAxis * (spinSpeed * dt), Space.Self);
            // 輪とポイは寝たまま、コルクとお金は飛ぶ向きを向く
            if (kind == Kind.Ring || kind == Kind.Poi) transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(velocity, Vector3.up).sqrMagnitude > 0.0001f ? Vector3.ProjectOnPlane(velocity, Vector3.up) : Vector3.forward);
            else if (dist > 0f) transform.rotation = Quaternion.LookRotation(step);

            float radius = kind == Kind.Cork ? 0.05f : kind == Kind.Ring ? 0.08f : 0.04f;
            if (dist > 0f && Physics.SphereCast(transform.position, radius, step / dist, out var hit, dist, mask, QueryTriggerInteraction.Ignore))
            {
                Land(hit);
                return;
            }
            transform.position += step;
            if (age > MaxLifetime) FxPool.Release(poolKey, gameObject);
        }

        void Land(RaycastHit hit)
        {
            landed = true;
            var target = hit.collider.GetComponentInParent<Target>();
            var stage = GameManager.I != null ? GameManager.I.festivalStage : null;
            switch (kind)
            {
                case Kind.Ring:
                {
                    var peg = NearestPeg(hit.point);
                    if (peg != null)
                    {
                        // 杭にかかった: 杭の根元近くへ寝かせて残す (杭について動く)
                        transform.SetPositionAndRotation(peg.transform.position + peg.transform.up * 0.05f, peg.transform.rotation); // 杭は傾いた板に垂直なので、杭の向きに合わせる
                        transform.SetParent(peg.transform, true);
                        peg.Hit(peg.transform.position + peg.transform.up * 0.3f);
                        FxPool.ReleaseAfter(poolKey, gameObject, 1.6f);
                    }
                    else
                    {
                        transform.SetPositionAndRotation(hit.point + hit.normal * 0.02f, Quaternion.FromToRotation(Vector3.up, hit.normal)); // 傾いた板の上では板に沿って寝る
                        Sfx.Play("thunk");
                        FxPool.ReleaseAfter(poolKey, gameObject, 0.8f);
                    }
                    return;
                }
                case Kind.Cork:
                    if (target != null && target.IsUp && target.kind == TargetKind.FestivalPrize && stage != null) stage.HitPrize(target, hit.point); // 倒れるかは確率
                    else if (target != null && target.IsUp) target.Hit(hit.point);
                    else Sfx.Play("thunk");
                    FxPool.Release(poolKey, gameObject);
                    return;
                case Kind.Poi:
                    if (stage != null && stage.IsWater(hit.collider))
                    {
                        transform.position = hit.point;
                        stage.Scoop(hit.point);
                        FxPool.Release(poolKey, gameObject); // ここからは FestivalStage のポイが水の中から上がってくる
                    }
                    else
                    {
                        Sfx.Play("thunk");
                        FxPool.Release(poolKey, gameObject);
                    }
                    return;
                default:
                    if (target != null && target.IsUp && target.kind == TargetKind.FestivalFood && stage != null) stage.TryBuy(target, hit.point);
                    else if (target != null && target.IsUp) target.Hit(hit.point);
                    else Sfx.Play("coin");
                    FxPool.Release(poolKey, gameObject);
                    return;
            }
        }

        // 落ちた所のいちばん近くの、立っている杭 (杭の軸からの距離が RingCatchRadius まで)
        static Target NearestPeg(Vector3 point)
        {
            int n = Physics.OverlapSphereNonAlloc(point, 0.45f, nearby, ~0, QueryTriggerInteraction.Ignore);
            Target best = null;
            float bestD = RingCatchRadius;
            for (int i = 0; i < n; i++)
            {
                var t = nearby[i].GetComponentInParent<Target>();
                if (t == null || t.kind != TargetKind.FestivalRing || !t.IsUp) continue;
                Vector3 d = t.transform.position - point;
                d = Vector3.ProjectOnPlane(d, t.transform.up); // 杭の軸からの距離 (傾いた板の杭にも合う)
                if (d.magnitude < bestD) { bestD = d.magnitude; best = t; }
            }
            return best;
        }
    }
}
