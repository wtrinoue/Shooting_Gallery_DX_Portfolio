using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace ShootingGallery
{
    /// <summary>1丁の銃。見た目の切り替えと発射(レイキャスト)を担当する。</summary>
    public class GunController : MonoBehaviour
    {
        [Tooltip("WeaponType の順 (Handgun, MachineGun, Shotgun, Bomb, Sniper, Bazooka, Shuriken, FireBall, Dart, Revolver, Missile) に並べた見た目")]
        public GameObject[] models = new GameObject[11];
        [Tooltip("各見た目の銃口。最初の子をマズルフラッシュとして使う")]
        public Transform[] muzzles = new Transform[11];
        [Header("怪獣 / カジノ / 西部劇")]
        [Tooltip("怪獣の口から吐く炎の玉と、燃え上がる爆発")]
        public Material fireballMaterial;
        public Material fireballExplosionMaterial;
        [Tooltip("投げたダーツの軸と羽根")]
        public Material dartShaftMaterial;
        public Material dartFlightMaterial;
        [Tooltip("リボルバーの弾倉 (弾込めの間だけ回る)")]
        public Transform revolverCylinder;
        [Tooltip("弾数のある武器 (リボルバー) の残りの弾を表す、銃の上に並んだ弾。撃つたびに右から灰色になり、弾込めで左から黄色に戻る")]
        public Transform ammoRoot;
        public Renderer[] ammoBullets;
        public Material ammoFullMaterial;
        public Material ammoEmptyMaterial;
        [Header("宇宙")]
        [Tooltip("飛んでいくミサイルの胴体 / 先端と羽根 / 噴射の炎 / 後ろに引く煙の筋")]
        public Material missileBodyMaterial;
        public Material missileFinMaterial;
        public Material missileFlameMaterial;
        public Material missileTrailMaterial;
        [Tooltip("発射台に込めてあるミサイル (撃つと消え、少しして次が込められる)")]
        public GameObject missileLoaded;
        [Header("Sniper / Shuriken")]
        [Tooltip("レーザーポインター (銃口から当たる所まで伸びる)。スナイパーは赤、手裏剣は白")]
        public LineRenderer laser;
        [Tooltip("レーザーが当たった所に出る光の点")]
        public Transform laserDot;
        public float laserRange = 120f;
        [Tooltip("手裏剣の時のレーザーと点の色 (白)")]
        public Material whiteLaserMaterial;
        public Material whiteDotMaterial;
        [Tooltip("投げた手裏剣の刃と真ん中の輪")]
        public Material shurikenMaterial;
        public Material shurikenCenterMaterial;
        Material redLaserMaterial, redDotMaterial;
        Renderer laserDotRenderer;
        Coroutine reloadRoutine;
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
        /// <summary>
        /// 次を撃てるようになるのを待っている間: スナイパーライフルの次の1発まで (レーザーを消して、撃てないことが分かるようにする) /
        /// リボルバーの弾込め中
        /// </summary>
        public bool IsReloading => (current == WeaponType.Sniper && Time.time < nextFireTime) || Time.time < reloadUntil;
        /// <summary>弾数のある武器の、残りの弾の数 (弾数のない武器は 0)</summary>
        public int Ammo => ammo;

        int ammo;
        float reloadUntil; // 弾込めが終わる時刻

        void Awake()
        {
            foreach (var m in muzzles)
                if (m != null && m.childCount > 0) m.GetChild(0).gameObject.SetActive(false);
            if (gaugeFill != null) gaugeFillRenderer = gaugeFill.GetComponent<Renderer>();
            gaugeBlock = new MaterialPropertyBlock();
            if (laser != null) redLaserMaterial = laser.sharedMaterial;
            if (laserDot != null)
            {
                laserDotRenderer = laserDot.GetComponent<Renderer>();
                if (laserDotRenderer != null) redDotMaterial = laserDotRenderer.sharedMaterial;
            }
            SetWeapon(WeaponType.Handgun);
            SetGauge(0f, Color.white);
        }

        // 非表示になるとコルーチンが途中で止まるので、付いたままのマズルフラッシュを消しておく
        void OnDisable()
        {
            flashRoutine = null;
            reloadRoutine = null;
            foreach (var m in muzzles)
                if (m != null && m.childCount > 0) m.GetChild(0).gameObject.SetActive(false);
        }

        public void SetWeapon(WeaponType type)
        {
            current = type;
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null) models[i].SetActive(i == (int)type);
            if (missileLoaded != null) missileLoaded.SetActive(true); // 込める途中で止まっていても、持ち替えたら込めてある
            // スナイパーは赤、手裏剣とダーツは白のレーザー
            bool white = type == WeaponType.Shuriken || type == WeaponType.Dart;
            if (laser != null && whiteLaserMaterial != null) laser.sharedMaterial = white ? whiteLaserMaterial : redLaserMaterial;
            if (laserDotRenderer != null && whiteDotMaterial != null) laserDotRenderer.sharedMaterial = white ? whiteDotMaterial : redDotMaterial;
            ShowLaser(HasLaser(type));
            // 弾数のある武器は弾を満タンにして持つ
            ammo = Weapons.Get(type).magazine;
            reloadUntil = 0f;
        }

        static bool HasLaser(WeaponType type) => type == WeaponType.Sniper || type == WeaponType.Shuriken || type == WeaponType.Dart;

        void ShowLaser(bool show)
        {
            if (laser != null && laser.gameObject.activeSelf != show) laser.gameObject.SetActive(show);
            if (laserDot != null && laserDot.gameObject.activeSelf != show) laserDot.gameObject.SetActive(show);
        }

        // レーザーを銃口から最初に当たる物まで伸ばし、当たった所に点を置く
        void UpdateLaser()
        {
            if (laser == null || !HasLaser(current)) return;
            ShowLaser(!IsReloading); // 次の1発までの間はレーザーを消す
            if (IsReloading) return;
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

        string lastInfo; // 最後に書いた文字 (TextMesh.text を読むと文字列が作られるので、こちらで覚えておく)

        public void SetInfo(string text)
        {
            // 同じ文字なら書き換えない (毎フレーム呼ばれるので、文字の形を作り直さないように)
            if (infoText == null || text == lastInfo) return;
            lastInfo = text;
            infoText.text = text;
        }

        /// <summary>残り時間ゲージ。ratio = 1 で満タン, 0 以下で非表示。</summary>
        Color lastGaugeColor = new Color(-1f, -1f, -1f, -1f);

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

            if (gaugeFillRenderer != null && color != lastGaugeColor) // 色が変わった時だけ渡す
            {
                lastGaugeColor = color;
                gaugeBlock.SetColor("_BaseColor", color);
                gaugeFillRenderer.SetPropertyBlock(gaugeBlock);
            }
        }

        /// <summary>毎フレーム呼ぶ。pressed=このフレームで引いた, held=引きっぱなし</summary>
        public void TriggerInput(bool pressed, bool held)
        {
            var def = Weapons.Get(current);
            bool wantFire = def.automatic ? held : pressed;
            if (wantFire && Time.time >= nextFireTime && Time.time >= reloadUntil)
                Fire(def);
        }

        void LateUpdate()
        {
            UpdateLaser();
            UpdateAmmoRow();
            // 弾込め中はリボルバーの弾倉がくるくる回る
            if (revolverCylinder != null && current == WeaponType.Revolver && Time.time < reloadUntil)
                revolverCylinder.Rotate(0f, 720f * Time.deltaTime, 0f, Space.Self);
            // 情報テキストとゲージを常にカメラへ向ける
            if (Camera.main == null) return;
            FaceCamera(infoText != null ? infoText.transform : null);
            FaceCamera(gaugeRoot);
            FaceCamera(ammoRoot);
        }

        // 銃の上の弾の列: 弾数のある武器の時だけ出す。残りの弾は黄色、撃った分は右から灰色。弾込めの間は左から1発ずつ黄色に戻る
        void UpdateAmmoRow()
        {
            if (ammoRoot == null || ammoBullets == null) return;
            var def = Weapons.Get(current);
            bool show = def.magazine > 0;
            if (ammoRoot.gameObject.activeSelf != show) ammoRoot.gameObject.SetActive(show);
            if (!show) return;
            int full = ammo;
            if (Time.time < reloadUntil && def.reloadTime > 0f)
                full = Mathf.FloorToInt((1f - (reloadUntil - Time.time) / def.reloadTime) * def.magazine);
            for (int i = 0; i < ammoBullets.Length; i++)
            {
                if (ammoBullets[i] == null) continue;
                bool on = i < def.magazine;
                if (ammoBullets[i].enabled != on) ammoBullets[i].enabled = on;
                var m = i < full ? ammoFullMaterial : ammoEmptyMaterial;
                if (ammoBullets[i].sharedMaterial != m) ammoBullets[i].sharedMaterial = m;
            }
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

            if (current == WeaponType.Shuriken || current == WeaponType.Dart)
            {
                if (current == WeaponType.Shuriken)
                    Shuriken.Spawn(origin, muzzle.forward, def.projectileSpeed, def.range, shurikenMaterial, shurikenCenterMaterial, hitMask);
                else
                    Shuriken.SpawnDart(origin, muzzle.forward, def.projectileSpeed, def.range, dartShaftMaterial, dartFlightMaterial, hitMask);
                // 手に持った1本は投げたら消え、少しして次の1本が出る
                if (reloadRoutine != null) StopCoroutine(reloadRoutine);
                reloadRoutine = isActiveAndEnabled ? StartCoroutine(Reload(def.fireInterval * 0.8f)) : null;
            }
            else if (current == WeaponType.Missile)
            {
                Missile.Spawn(origin, muzzle.forward, def.projectileSpeed, def.blastRadius, missileBodyMaterial, missileFinMaterial, missileFlameMaterial,
                              missileTrailMaterial, explosionMaterial);
                if (missileLoaded != null)
                {
                    if (reloadRoutine != null) StopCoroutine(reloadRoutine);
                    reloadRoutine = isActiveAndEnabled ? StartCoroutine(Reload(def.fireInterval * 0.9f, missileLoaded)) : null;
                }
            }
            else if (def.projectile)
            {
                bool fire = current == WeaponType.FireBall;
                Bomb.Spawn(origin, muzzle.forward * def.projectileSpeed, def.blastRadius, fire && fireballMaterial != null ? fireballMaterial : bombMaterial,
                           fire && fireballExplosionMaterial != null ? fireballExplosionMaterial : explosionMaterial,
                           def.projectileGravity, current == WeaponType.Bazooka ? 0.14f : fire ? 0.2f : 0.09f);
            }
            else
                FireHitscan(def, muzzle, origin);

            // 弾数のある武器 (リボルバー): 撃ちきったら弾込め
            if (def.magazine > 0 && --ammo <= 0)
            {
                ammo = def.magazine;
                reloadUntil = Time.time + def.reloadTime;
                Sfx.Play("reload");
            }

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            // 自分の弾でスナイパーの的を取ると左手の銃はその場で非表示になるので、その時はフラッシュを出さない
            flashRoutine = isActiveAndEnabled ? StartCoroutine(Flash(muzzle)) : null;
            Sfx.PlayShot(current);
            bool heavy = current == WeaponType.Shotgun || current == WeaponType.Bomb || current == WeaponType.Sniper || current == WeaponType.Bazooka;
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
        /// 壁や屋台の板の向こうにある的は拾わない (銃口から的まで見通せる時だけ当てる)。
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
            int hitCount = Physics.SphereCastNonAlloc(origin, assist, dir, castHits, Mathf.Min(range, blockDist + 0.25f), hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++) // 結果は使い回しの配列に入れる (1発ごとに配列を作らない)
            {
                var h = castHits[i];
                if (h.distance <= 0f || h.distance >= best) continue; // 0 = 発射位置ですでに重なっている物
                var t = h.collider.GetComponentInParent<Target>();
                if (t == null || !t.IsUp) continue;
                // 銃口からその的までの間に別の物 (回転扉の板など) があれば、裏側の的なので当てない
                if (Physics.Linecast(origin, h.point, out var block, hitMask, QueryTriggerInteraction.Ignore)
                    && block.collider.GetComponentInParent<Target>() != t) continue;
                best = h.distance;
                target = t;
                point = h.point;
            }
            return target != null;
        }

        static readonly RaycastHit[] castHits = new RaycastHit[64]; // エイム補正の太い線に当たった物 (使い回し)

        void SpawnTracer(Vector3 from, Vector3 to)
        {
            // 弾の軌跡は1発ごとに出るので、作って消さずに FxPool で使い回す
            var go = FxPool.Get("Tracer", () =>
            {
                var g = new GameObject("Tracer");
                var line = g.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = 0.012f;
                line.endWidth = 0.004f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                return g;
            });
            var lr = go.GetComponent<LineRenderer>();
            lr.sharedMaterial = tracerMaterial;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            FxPool.ReleaseAfter("Tracer", go, 0.05f);
        }

        // 投げる武器 (手裏剣 / ダーツ): 手に持った1本が消えて、少しして次の1本が出る。part を渡すとその部品だけ (込めてあるミサイル)
        IEnumerator Reload(float delay, GameObject part = null)
        {
            var type = current;
            var held = part != null ? part : (int)type < models.Length ? models[(int)type] : null;
            if (held == null) yield break;
            held.SetActive(false);
            yield return new WaitForSeconds(delay);
            reloadRoutine = null;
            held.SetActive(part != null || current == type);
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
