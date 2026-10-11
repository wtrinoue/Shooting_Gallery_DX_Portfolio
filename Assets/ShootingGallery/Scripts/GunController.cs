using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace ShootingGallery
{
    /// <summary>1丁の銃。見た目の切り替えと発射(レイキャスト)を担当する。</summary>
    public class GunController : MonoBehaviour
    {
        [Tooltip("WeaponType の順 (Handgun, MachineGun, Shotgun, Bomb) に並べた見た目")]
        public GameObject[] models = new GameObject[4];
        [Tooltip("各見た目の銃口。最初の子をマズルフラッシュとして使う")]
        public Transform[] muzzles = new Transform[4];
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
            var muzzle = muzzles[(int)current] != null ? muzzles[(int)current] : transform;
            Vector3 origin = muzzle.position;

            if (def.projectile)
                Bomb.Spawn(origin, muzzle.forward * def.projectileSpeed, def.blastRadius, bombMaterial, explosionMaterial);
            else
                FireHitscan(def, muzzle, origin);

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash(muzzle));
            Sfx.PlayShot(current);
            InputDevices.GetDeviceAtXRNode(hapticNode).SendHapticImpulse(0, current == WeaponType.Shotgun || current == WeaponType.Bomb ? 0.9f : 0.5f, 0.06f);
        }

        // 弾がまっすぐ一瞬で届く武器 (ハンドガン, マシンガン, ショットガン)
        void FireHitscan(WeaponDef def, Transform muzzle, Vector3 origin)
        {
            for (int i = 0; i < def.pellets; i++)
            {
                Vector3 dir = muzzle.forward;
                if (def.spreadDeg > 0f)
                {
                    Vector2 r = Random.insideUnitCircle * Mathf.Tan(def.spreadDeg * Mathf.Deg2Rad);
                    dir = (dir + muzzle.right * r.x + muzzle.up * r.y).normalized;
                }

                Vector3 end = origin + dir * def.range;
                if (Physics.Raycast(origin, dir, out var hit, def.range, hitMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var target = hit.collider.GetComponentInParent<Target>();
                    if (target != null) target.Hit(hit.point);
                }
                SpawnTracer(origin, end);
            }
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
