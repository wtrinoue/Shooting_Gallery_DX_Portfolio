using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace ShootingGallery
{
    /// <summary>1丁の銃。見た目の切り替えと発射(レイキャスト)を担当する。</summary>
    public class GunController : MonoBehaviour
    {
        [Tooltip("WeaponType の順 (Handgun, MachineGun, Shotgun, Bomb, Sniper) に並べた見た目")]
        public GameObject[] models = new GameObject[5];
        [Tooltip("各見た目の銃口。最初の子をマズルフラッシュとして使う")]
        public Transform[] muzzles = new Transform[5];
        [Header("Sniper")]
        [Tooltip("スナイパーライフルのレーザーポインター (銃口から当たる所まで伸びる)")]
        public LineRenderer laser;
        [Tooltip("レーザーが当たった所に出る光の点")]
        public Transform laserDot;
        public float laserRange = 120f;
        public Material tracerMaterial;
        public Material bombMaterial;
        public Material explosionMaterial;
        public TextMesh infoText;
        [Header("Time Gauge (特殊武器の残り時間)")]
        [Tooltip("ゲージ全体。カメラの方を向き、特殊武器の時だけ表示される")]
        public Transform gaugeRoot;
        [Tooltip("減っていくバー。左端を基準に X スケールで長さを変える")]
        public Transform gaugeFill;
        public float gaugeWidth = 0.12f;
        public XRNode hapticNode = XRNode.RightHand;
        public LayerMask hitMask = ~0;
        [Tooltip("エイム補正の太さ (m)。射線からこの距離までにある的にも当たる。0 で補正なし (真っすぐ線上の的だけ)")]
        public float aimAssistRadius = 0.12f;

        WeaponType current;
        float nextFireTime;
        Coroutine flashRoutine;
        Renderer gaugeFillRenderer;
        MaterialPropertyBlock gaugeBlock;

        public WeaponType Current => current;

        void Awake()
        {
            foreach (var m in muzzles)
                if (m != null && m.childCount > 0) m.GetChild(0).gameObject.SetActive(false);
            if (gaugeFill != null) gaugeFillRenderer = gaugeFill.GetComponent<Renderer>();
            gaugeBlock = new MaterialPropertyBlock();
            SetWeapon(WeaponType.Handgun);
            SetGauge(0f, Color.white);
        }

        public void SetWeapon(WeaponType type)
        {
            current = type;
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null) models[i].SetActive(i == (int)type);
            ShowLaser(type == WeaponType.Sniper);
        }

        void ShowLaser(bool show)
        {
            if (laser != null && laser.gameObject.activeSelf != show) laser.gameObject.SetActive(show);
            if (laserDot != null && laserDot.gameObject.activeSelf != show) laserDot.gameObject.SetActive(show);
        }

        // レーザーを銃口から最初に当たる物まで伸ばし、当たった所に点を置く
        void UpdateLaser()
        {
            if (laser == null || current != WeaponType.Sniper) return;
            var muzzle = MuzzleOf(current);
            Vector3 from = muzzle.position, dir = muzzle.forward;
            bool hitSomething = Physics.Raycast(from, dir, out var hit, laserRange, hitMask, QueryTriggerInteraction.Ignore);
            Vector3 to = hitSomething ? hit.point : from + dir * laserRange;
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.SetPosition(0, from);
            laser.SetPosition(1, to);
            if (laserDot != null)
            {
                laserDot.gameObject.SetActive(hitSomething);
                if (hitSomething)
                {
                    laserDot.position = hit.point - dir * 0.01f;
                    // 遠くても見えるように距離に応じて大きくする
                    float size = 0.025f + hit.distance * 0.006f;
                    laserDot.localScale = Vector3.one * size / Mathf.Max(0.0001f, laserDot.parent != null ? laserDot.parent.lossyScale.x : 1f);
                }
            }
        }

        Transform MuzzleOf(WeaponType type)
        {
            int i = (int)type;
            return i < muzzles.Length && muzzles[i] != null ? muzzles[i] : transform;
        }

        public void SetInfo(string text)
        {
            if (infoText != null) infoText.text = text;
        }

        /// <summary>残り時間ゲージ。ratio = 1 で満タン, 0 以下で非表示。</summary>
        public void SetGauge(float ratio, Color color)
        {
            if (gaugeRoot == null || gaugeFill == null) return;
            bool show = ratio > 0f;
            if (gaugeRoot.gameObject.activeSelf != show) gaugeRoot.gameObject.SetActive(show);
            if (!show) return;

            ratio = Mathf.Clamp01(ratio);
            var s = gaugeFill.localScale;
            s.x = gaugeWidth * ratio;
            gaugeFill.localScale = s;
            var p = gaugeFill.localPosition;
            p.x = -gaugeWidth * (1f - ratio) * 0.5f; // 左端を固定して右から減らす
            gaugeFill.localPosition = p;

            if (gaugeFillRenderer != null)
            {
                gaugeBlock.SetColor("_BaseColor", color);
                gaugeFillRenderer.SetPropertyBlock(gaugeBlock);
            }
        }

        /// <summary>毎フレーム呼ぶ。pressed=このフレームで引いた, held=引きっぱなし</summary>
        public void TriggerInput(bool pressed, bool held)
        {
            var def = Weapons.Get(current);
            bool wantFire = def.automatic ? held : pressed;
            if (wantFire && Time.time >= nextFireTime)
                Fire(def);
        }

        void LateUpdate()
        {
            UpdateLaser();
            // 情報テキストとゲージを常にカメラへ向ける
            if (Camera.main == null) return;
            FaceCamera(infoText != null ? infoText.transform : null);
            FaceCamera(gaugeRoot);
        }

        static void FaceCamera(Transform t)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return;
            t.rotation = Quaternion.LookRotation(t.position - Camera.main.transform.position);
        }

        void Fire(WeaponDef def)
        {
            nextFireTime = Time.time + def.fireInterval;
            var muzzle = MuzzleOf(current);
            Vector3 origin = muzzle.position;

            if (def.projectile)
                Bomb.Spawn(origin, muzzle.forward * def.projectileSpeed, def.blastRadius, bombMaterial, explosionMaterial);
            else
                FireHitscan(def, muzzle, origin);

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash(muzzle));
            Sfx.PlayShot(current);
            bool heavy = current == WeaponType.Shotgun || current == WeaponType.Bomb || current == WeaponType.Sniper;
            InputDevices.GetDeviceAtXRNode(hapticNode).SendHapticImpulse(0, heavy ? 0.9f : 0.5f, heavy ? 0.12f : 0.06f);
        }

        // 弾がまっすぐ一瞬で届く武器 (ハンドガン, マシンガン, ショットガン)
        void FireHitscan(WeaponDef def, Transform muzzle, Vector3 origin)
        {
            // スナイパー: レーザーの先にそのまま飛ぶ1発 (エイム補正なし)。撃った結果をスナイパーチャレンジに知らせる
            if (current == WeaponType.Sniper)
            {
                Vector3 end = origin + muzzle.forward * def.range;
                Target hitTarget = null;
                if (Physics.Raycast(origin, muzzle.forward, out var hit, def.range, hitMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    hitTarget = hit.collider.GetComponentInParent<Target>();
                    if (hitTarget != null && hitTarget.IsUp) hitTarget.Hit(hit.point);
                    else hitTarget = null;
                }
                SpawnTracer(origin, end);
                if (GameManager.I != null) GameManager.I.OnSniperShot(hitTarget);
                return;
            }

            for (int i = 0; i < def.pellets; i++)
            {
                Vector3 dir = muzzle.forward;
                if (def.spreadDeg > 0f)
                {
                    Vector2 r = Random.insideUnitCircle * Mathf.Tan(def.spreadDeg * Mathf.Deg2Rad);
                    dir = (dir + muzzle.right * r.x + muzzle.up * r.y).normalized;
                }

                Vector3 end = origin + dir * def.range;
                // 弾が多いショットガンは補正を小さく (全弾に大きな補正がつくと強すぎるため)
                float assist = def.pellets > 1 ? aimAssistRadius * 0.5f : aimAssistRadius;
                if (TryHitTarget(origin, dir, def.range, assist, out var target, out var point))
                {
                    end = point;
                    target.Hit(point);
                }
                else if (Physics.Raycast(origin, dir, out var wall, def.range, hitMask, QueryTriggerInteraction.Ignore))
                    end = wall.point;
                SpawnTracer(origin, end);
            }
        }

        /// <summary>
        /// 当たる的を探す。まず細い線で調べ、外れていたら太さ assist (m) の線で、射線の近くにある的を拾う (エイム補正)。
        /// 壁や屋台の板の向こうにある的は拾わない。
        /// </summary>
        bool TryHitTarget(Vector3 origin, Vector3 dir, float range, float assist, out Target target, out Vector3 point)
        {
            target = null;
            point = Vector3.zero;
            float blockDist = range;
            if (Physics.Raycast(origin, dir, out var hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                var t = hit.collider.GetComponentInParent<Target>();
                if (t != null && t.IsUp)
                {
                    target = t;
                    point = hit.point;
                    return true;
                }
                blockDist = hit.distance;
            }
            if (assist <= 0f) return false;

            // 細い線が当たった物 (壁など) より少し奥までなら、的の縁がはみ出していれば当てる
            float best = float.MaxValue;
            foreach (var h in Physics.SphereCastAll(origin, assist, dir, Mathf.Min(range, blockDist + 0.25f), hitMask, QueryTriggerInteraction.Ignore))
            {
                if (h.distance <= 0f || h.distance >= best) continue; // 0 = 発射位置ですでに重なっている物
                var t = h.collider.GetComponentInParent<Target>();
                if (t == null || !t.IsUp) continue;
                best = h.distance;
                target = t;
                point = h.point;
            }
            return target != null;
        }

        void SpawnTracer(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.012f;
            lr.endWidth = 0.004f;
            lr.sharedMaterial = tracerMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Destroy(go, 0.05f);
        }

        IEnumerator Flash(Transform muzzle)
        {
            if (muzzle.childCount == 0) yield break;
            var flash = muzzle.GetChild(0).gameObject;
            flash.SetActive(true);
            flash.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            yield return new WaitForSeconds(0.04f);
            flash.SetActive(false);
        }
    }
}
