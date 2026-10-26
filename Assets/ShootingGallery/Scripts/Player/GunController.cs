using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace ShootingGallery
{
    /// <summary>1丁の銃。見た目の切り替えと発射(レイキャスト)を担当する。</summary>
    public class GunController : MonoBehaviour
    {
        [Tooltip("WeaponType の順 (Handgun, MachineGun, Shotgun, Bomb, Sniper, Bazooka, Shuriken, FireBall, Dart, Revolver, Missile, Flashlight, Pickaxe, RingToss, CorkGun, Poi, Coin, Torpedo, Trumpet, Imagination, AirMachineGun, Lightning) に並べた見た目")]
        public GameObject[] models = new GameObject[22];
        [Tooltip("各見た目の銃口。最初の子をマズルフラッシュとして使う")]
        public Transform[] muzzles = new Transform[22];
        [Header("夏祭り (輪投げの輪 / コルク / ポイ / お金)")]
        [Tooltip("輪の形 (ドーナツ形のメッシュ) と、投げるたびに選ぶ輪の色")]
        public Mesh ringMesh;
        public Material[] ringMaterials;
        public Material corkMaterial;
        public Material poiFrameMaterial, poiPaperMaterial;
        public Material coinMaterial;
        [Tooltip("コルク銃の先に込めてあるコルク (撃つと消え、弾込めが終わると戻る)")]
        public GameObject corkLoaded;
        [Header("怪獣 / カジノ / 西部劇")]
        [Tooltip("怪獣の口から吐く炎の玉と、燃え上がる爆発")]
        public Material fireballMaterial;            // 炎の玉の芯 (白っぽく光る)
        public Material fireballOuterMaterial;       // 炎の玉の外側 (半透明の橙。ギザギザのしずく)
        public Material fireballTrailMaterial;       // 炎の尾 (色は TrailRenderer の色で付ける)
        public Material fireballExplosionMaterial;
        public Material cannonballMaterial;          // 海戦の大砲の弾 (黒い鉄の玉)
        [Tooltip("投げたダーツの軸と羽根")]
        public Material dartShaftMaterial;
        public Material dartFlightMaterial;
        public Material pickaxeHandleMaterial;
        public Material pickaxeHeadMaterial;
        [Tooltip("リボルバーの弾倉 (弾込めの間だけ回る)")]
        public Transform revolverCylinder;
        [Tooltip("弾数のある武器 (リボルバー) の残りの弾を表す、銃の上に並んだ弾。撃つたびに右から灰色になり、弾込めで左から黄色に戻る")]
        public Transform ammoRoot;
        public Renderer[] ammoBullets;
        public Material ammoFullMaterial;
        public Material ammoEmptyMaterial;
        [Tooltip("トリガーで弾込めする武器 (リボルバー) の、込めている途中の弾の色 (込め終わると ammoFullMaterial に戻る)")]
        public Material ammoLoadingMaterial;
        [Header("宇宙")]
        [Tooltip("飛んでいくミサイルの胴体 / 先端と羽根 / 噴射の炎 / 後ろに引く煙の筋")]
        public Material missileBodyMaterial;
        public Material missileFinMaterial;
        public Material missileFlameMaterial;
        public Material missileTrailMaterial;
        [Tooltip("発射台に込めてあるミサイル (撃つと消え、少しして次が込められる)")]
        public GameObject missileLoaded;
        [Header("潜水艦")]
        [Tooltip("進んでいく魚雷の胴体 / 先端と帯 / プロペラ / 後ろに引く泡 / 水の中の爆発")]
        public Material torpedoBodyMaterial;
        public Material torpedoNoseMaterial;
        public Material torpedoPropellerMaterial;
        public Material bubbleMaterial;
        public Material torpedoExplosionMaterial;
        [Tooltip("発射管に込めてある魚雷 (撃つと消え、少しして次が込められる)")]
        public GameObject torpedoLoaded;
        [Header("音楽 (トランペット)")]
        [Tooltip("吹き出す音符の色 (撃つたびにランダム) / しっぽの五線譜の筋 / 2分音符の玉の中 / 当たった時の光")]
        public Material[] noteShotMaterials;
        public Material noteLineMaterial;
        public Material noteHoleMaterial;
        public Material noteSparkMaterial;
        [Header("美術館 (イマジネーション)")]
        [Tooltip("放つ玉の色 (撃つたびにランダム) / 後ろに引く虹色の光の筋 / 当たった時にはじける光")]
        public Material[] imaginationMaterials;
        public Material imaginationTrailMaterial;
        public Material imaginationSparkMaterial;
        [Tooltip("紫の電撃 (Unknown ステージの武器) の稲妻の材質")]
        public Material lightningMaterial;
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

        [Header("Flashlight (幽霊モード: トリガーを引いている間だけ光り、照らされた的はやられる)")]
        [Tooltip("光の筋 (透ける円すい) と、本物のスポットライト")]
        public GameObject flashlightBeam;
        public Light flashlightLight;
        [Tooltip("レンズ (点いている時 / 消えている時の材質)")]
        public Renderer flashlightLens;
        public Material lensOnMaterial, lensOffMaterial;
        [Tooltip("照らして倒せる太さ (m)。光の筋のまわりのこの距離までの的に当たる")]
        public float flashlightRadius = 0.75f;
        [Tooltip("満タンから点けっぱなしで電池が切れるまでの秒数")]
        public float flashlightDrainTime = 3.5f;
        [Tooltip("空から満タンまで回復する秒数 (点けていない間に回復する)")]
        public float flashlightRechargeTime = 3f;
        [Tooltip("電池の残り: 銃の上に並ぶ目盛り (左から。緑 → 黄 → 赤、切れて充電中は赤)")]
        public Transform batteryRoot;
        public Renderer[] batteryCells;
        public Material batteryGreenMaterial, batteryYellowMaterial, batteryRedMaterial, batteryEmptyMaterial;

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
        public bool IsReloading => (current == WeaponType.Sniper && Time.time < nextFireTime) || Time.time < reloadUntil || (current == WeaponType.Flashlight && batteryDepleted);
        /// <summary>懐中電灯の電池が切れて、満タンまで回復するのを待っている間か</summary>
        public bool IsCharging => current == WeaponType.Flashlight && batteryDepleted;

        float battery = 1f;      // 懐中電灯の電池 (0〜1)
        bool batteryDepleted;    // 一度切れたら、満タンになるまで点かない
        bool flashlightHeld;     // このフレームにトリガーを引いているか (TriggerInput で受け取る)
        bool beamOn;
        /// <summary>弾数のある武器の、残りの弾の数 (弾数のない武器は 0)</summary>
        public int Ammo => ammo;

        /// <summary>撃ちきって、トリガーで弾を込めている途中 (リボルバー。満タンになるまで撃てない)</summary>
        public bool NeedsReload => ammo <= 0 && Weapons.Get(current).reloadStep > 0;
        /// <summary>トリガーで込めている途中の、込め終わった弾の数</summary>
        public int Loaded => loaded;

        int ammo;
        float reloadUntil; // 弾込めが終わる時刻
        int loaded;          // トリガーで込めている途中の、込め終わった弾の数
        float nextLoadTime;  // 次にトリガーで込められる時刻 (引いた勢いで2回込めないように、少し間をあける)
        float cylinderSpinUntil; // 込めた時に、リボルバーの弾倉を少し回す

        void Awake()
        {
            foreach (var m in muzzles)
                if (m != null && m.childCount > 0) m.GetChild(0).gameObject.SetActive(false);
            if (gaugeFill != null) gaugeFillRenderer = gaugeFill.GetComponent<Renderer>();
            // 銃の上の表示 (武器の名前・ゲージ・弾の列・電池) の元の高さ。大きな武器の時は、ここから上へずらす
            hudParts = new[] { infoText != null ? infoText.transform : null, gaugeRoot, ammoRoot, batteryRoot };
            hudHome = new Vector3[hudParts.Length];
            for (int i = 0; i < hudParts.Length; i++) if (hudParts[i] != null) hudHome[i] = hudParts[i].localPosition;
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

        Transform[] hudParts = new Transform[0];
        Vector3[] hudHome = new Vector3[0];

        /// <summary>銃の上の表示を、元の高さからどれだけ上げるか (m)。見た目が大きくて表示が埋もれてしまう武器だけ上げる</summary>
        static float HudLift(WeaponType type) => type switch
        {
            WeaponType.Bazooka => 0.1f, // 海戦の大砲: 砲身の上 (約 0.12m) より上へ
            _ => 0f
        };

        bool modelHidden;

        /// <summary>武器の見た目を隠す / 出す (操作方法の看板を見ている間は、手元をコントローラーの模型にするので。撃つことはできる)</summary>
        public void SetModelHidden(bool hidden)
        {
            modelHidden = hidden;
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null) models[i].SetActive(!hidden && i == (int)current);
        }

        public void SetWeapon(WeaponType type)
        {
            current = type;
            float lift = HudLift(type);
            for (int i = 0; i < hudParts.Length; i++)
                if (hudParts[i] != null) hudParts[i].localPosition = hudHome[i] + Vector3.up * lift;
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null) models[i].SetActive(!modelHidden && i == (int)type);
            if (missileLoaded != null) missileLoaded.SetActive(true); // 込める途中で止まっていても、持ち替えたら込めてある
            if (torpedoLoaded != null) torpedoLoaded.SetActive(true);
            // スナイパーは赤、手裏剣とダーツ (と夏祭りの武器) は白のレーザー
            bool white = type == WeaponType.Shuriken || type == WeaponType.Dart || type == WeaponType.Pickaxe || IsFestival(type);
            if (laser != null && whiteLaserMaterial != null) laser.sharedMaterial = white ? whiteLaserMaterial : redLaserMaterial;
            if (laserDotRenderer != null && whiteDotMaterial != null) laserDotRenderer.sharedMaterial = white ? whiteDotMaterial : redDotMaterial;
            ShowLaser(HasLaser(type));
            // 弾数のある武器は弾を満タンにして持つ。ただし弾込めの途中で持ち替えた武器は、その続きから
            // (夏祭りモードは見た屋台で武器が変わるので、よそ見をして戻すだけで弾込めが終わらないように)
            ammo = Weapons.Get(type).magazine;
            loaded = 0;
            reloadUntil = readyAt.TryGetValue(type, out var ready) && ready > Time.time ? ready : 0f;
            if (corkLoaded != null) corkLoaded.SetActive(Time.time >= reloadUntil);
            // 懐中電灯は電池を満タンにして持つ
            battery = 1f;
            batteryDepleted = false;
            SetBeam(false);
        }

        static bool HasLaser(WeaponType type) => type == WeaponType.Sniper || type == WeaponType.Shuriken || type == WeaponType.Dart || type == WeaponType.Pickaxe || type == WeaponType.CorkGun || type == WeaponType.Coin || IsArc(type);

        /// <summary>放物線で投げる武器 (夏祭りの輪・ポイ)。白いレーザーの代わりに、落ちる所までの放物線を見せる (お金はまっすぐ飛ぶので白いレーザー)</summary>
        static bool IsArc(WeaponType type) => type == WeaponType.RingToss || type == WeaponType.Poi;
        static bool IsFestival(WeaponType type) => IsArc(type) || type == WeaponType.CorkGun || type == WeaponType.Coin;

        /// <summary>弾込めの進み具合 (0 = 撃った直後 〜 1 = 撃てる)。弾込めのない武器はいつも 1</summary>
        public float ReloadProgress
        {
            get
            {
                float t = Weapons.Get(current).reloadTime;
                if (t <= 0f || Time.time >= reloadUntil) return 1f;
                return Mathf.Clamp01(1f - (reloadUntil - Time.time) / t);
            }
        }

        readonly System.Collections.Generic.Dictionary<WeaponType, float> readyAt = new System.Collections.Generic.Dictionary<WeaponType, float>(); // 武器ごとの、弾込めが終わる時刻
        static readonly Vector3[] arcPoints = new Vector3[200]; // 放物線の点 (使い回し。0.02秒ごと、4秒分まで)

        /// <summary>銃が的から手元へ飛んでくる間はレーザーを出さない (PlayerRig が手元に着くまで true にする)</summary>
        public bool LaserSuppressed { get; set; }

        void ShowLaser(bool show)
        {
            if (LaserSuppressed) show = false;
            if (laser != null && laser.gameObject.activeSelf != show) laser.gameObject.SetActive(show);
            if (laserDot != null && laserDot.gameObject.activeSelf != show) laserDot.gameObject.SetActive(show);
        }

        // レーザーを銃口から最初に当たる物まで伸ばし、当たった所に点を置く
        void UpdateLaser()
        {
            if (laser == null || !HasLaser(current)) return;
            ShowLaser(!IsReloading); // 次の1発までの間はレーザーを消す (飛んでくる途中も消す: LaserSuppressed)
            if (IsReloading || LaserSuppressed) return;
            var muzzle = MuzzleOf(current);
            Vector3 from = muzzle.position, dir = muzzle.forward;
            if (IsArc(current))
            {
                UpdateArc(from, dir);
                return;
            }
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

        const float ArcAimRange = 30f;    // 狙う所を探す距離 (m)
        const float ArcMissDistance = 10f; // 何も狙っていない (空など) 時に向かう距離 (m)

        const float ArcBulge = 0.07f;  // 弧のふくらみ (始まりと着く所を結ぶまっすぐな線から、真ん中でどれだけ上か。距離に対する割合)
        const float ArcSpeed = 9f;     // 飛ぶ速さ (m/秒。まっすぐな線の長さをこの速さで進む時間で着く)

        /// <summary>
        /// 放物線で投げる武器の初速と重力。銃を向けた先で最初に当たる所へ、いつも同じ形の低い弧で落ちるようにする
        /// (どこへ向けても、その所へ届く)。弧は、始まりと着く所を結ぶまっすぐな線を、真ん中で少しだけ上へふくらませた形
        /// (距離 × ArcBulge、0.06〜0.5m)。そのぶんだけの重力をかけるので、手元より低い所へ投げても急に落ちない
        /// </summary>
        Vector3 ArcVelocity(Vector3 from, Vector3 dir, out float gravity)
        {
            Vector3 to = Physics.Raycast(from, dir, out var aim, ArcAimRange, hitMask, QueryTriggerInteraction.Ignore) ? aim.point : from + dir * ArcMissDistance;
            Vector3 line = to - from;
            float dist = line.magnitude;
            float time = Mathf.Max(0.25f, dist / ArcSpeed);
            float bulge = Mathf.Clamp(dist * ArcBulge, 0.06f, 0.5f);
            // 位置 = まっすぐな線 + 上へ 4 × bulge × t(1-t) (t = 0〜1) になる初速と重力
            gravity = 8f * bulge / (time * time);
            return line / time + Vector3.up * (4f * bulge / time);
        }

        // 放物線で投げる武器: 投げた物が飛ぶ道すじを、何かに当たる所まで線で描き、落ちる所に点を置く
        void UpdateArc(Vector3 from, Vector3 dir)
        {
            const float Dt = 0.02f;
            Vector3 p = from, v = ArcVelocity(from, dir, out float gravity);
            Vector3 gStep = Vector3.down * (0.5f * gravity * Dt * Dt);
            int count = 0;
            arcPoints[count++] = p;
            bool landed = false;
            Vector3 landing = p, normal = Vector3.up;
            while (count < arcPoints.Length)
            {
                Vector3 next = p + v * Dt + gStep; // 投げた物と同じ計算 (FestivalThrow)
                v += Vector3.down * (gravity * Dt);
                if (Physics.Linecast(p, next, out var hit, hitMask, QueryTriggerInteraction.Ignore))
                {
                    arcPoints[count++] = hit.point;
                    landed = true;
                    landing = hit.point;
                    normal = hit.normal;
                    break;
                }
                arcPoints[count++] = next;
                p = next;
            }
            laser.useWorldSpace = true;
            laser.positionCount = count;
            laser.SetPositions(arcPoints);
            if (laserDot == null) return;
            laserDot.gameObject.SetActive(landed);
            if (!landed) return;
            laserDot.position = landing + normal * 0.01f;
            float size = 0.06f;
            laserDot.localScale = Vector3.one * size / Mathf.Max(0.0001f, laserDot.parent != null ? laserDot.parent.lossyScale.x : 1f);
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
            if (current == WeaponType.Flashlight) { flashlightHeld = held; return; } // 光らせるのは LateUpdate
            var def = Weapons.Get(current);
            // 撃ちきった武器 (リボルバー): トリガーを引くたびに reloadStep 発ずつ込める (満タンになるまで撃たない)
            if (NeedsReload)
            {
                if (pressed && Time.time >= nextLoadTime) LoadStep(def);
                return;
            }
            bool wantFire = def.automatic ? held : pressed;
            if (wantFire && Time.time >= nextFireTime && Time.time >= reloadUntil)
                Fire(def);
        }

        // トリガーで reloadStep 発込める。満タンになったら撃てる (次にトリガーを引いた時から)
        void LoadStep(WeaponDef def)
        {
            loaded = Mathf.Min(def.magazine, loaded + def.reloadStep);
            nextLoadTime = Time.time + 0.12f;
            cylinderSpinUntil = Time.time + 0.2f;
            XRBridge.Haptic(hapticNode, 0.25f, 0.04f);
            if (loaded < def.magazine)
            {
                Sfx.Play(current == WeaponType.Revolver ? "revolverreload" : "click"); // (リボルバーは1発込めるたびに、弾を込める音)
                return;
            }
            ammo = def.magazine;
            loaded = 0;
            nextFireTime = Mathf.Max(nextFireTime, Time.time + 0.12f);
            Sfx.Play(current == WeaponType.Revolver ? "revolverreload" : "reload");
        }

        void LateUpdate()
        {
            UpdateLaser();
            UpdateAmmoRow();
            UpdateFlashlight();
            // 弾込め中・弾を込めた時は、リボルバーの弾倉がくるくる回る
            if (revolverCylinder != null && current == WeaponType.Revolver && (Time.time < reloadUntil || Time.time < cylinderSpinUntil))
                revolverCylinder.Rotate(0f, 720f * Time.deltaTime, 0f, Space.Self);
            // コルク銃の先のコルクと、手に持ったポイは、弾込め (次のポイが来る) まで出さない
            bool ready = Time.time >= reloadUntil;
            if (corkLoaded != null && current == WeaponType.CorkGun && corkLoaded.activeSelf != ready) corkLoaded.SetActive(ready);
            var poi = (int)WeaponType.Poi < models.Length ? models[(int)WeaponType.Poi] : null;
            if (poi != null && current == WeaponType.Poi && poi.activeSelf != ready) poi.SetActive(ready);
            // 情報テキストとゲージを常にカメラへ向ける
            if (Camera.main == null) return;
            FaceCamera(infoText != null ? infoText.transform : null);
            FaceCamera(gaugeRoot);
            FaceCamera(ammoRoot);
            FaceCamera(batteryRoot);
        }

        // ---- 懐中電灯 ----

        // トリガーを引いている間だけ光る (電池が減る)。点けていない間は電池が回復する。切れたら満タンになるまで点かない
        void UpdateFlashlight()
        {
            bool isLight = current == WeaponType.Flashlight;
            if (batteryRoot != null && batteryRoot.gameObject.activeSelf != isLight) batteryRoot.gameObject.SetActive(isLight);
            if (!isLight)
            {
                SetBeam(false);
                return;
            }
            float dt = Time.deltaTime;
            bool on = flashlightHeld && !batteryDepleted;
            flashlightHeld = false; // 毎フレーム TriggerInput で受け取り直す
            if (on)
            {
                battery -= dt / Mathf.Max(0.1f, flashlightDrainTime);
                if (battery <= 0f)
                {
                    battery = 0f;
                    batteryDepleted = true; // 切れた
                    on = false;
                    Sfx.Play("powerdown");
                }
            }
            else
            {
                battery = Mathf.Min(1f, battery + dt / Mathf.Max(0.1f, flashlightRechargeTime));
                if (batteryDepleted && battery >= 1f)
                {
                    batteryDepleted = false; // 満タンになった: また点けられる
                    Sfx.Play("click");
                }
            }
            if (on && !beamOn) Sfx.Play("click");
            SetBeam(on);
            if (on) Shine();
            UpdateBatteryRow();
        }

        void SetBeam(bool on)
        {
            if (beamOn == on && (flashlightBeam == null || flashlightBeam.activeSelf == on)) return;
            beamOn = on;
            if (flashlightBeam != null) flashlightBeam.SetActive(on);
            if (flashlightLight != null) flashlightLight.enabled = on;
            if (flashlightLens != null && lensOnMaterial != null && lensOffMaterial != null) flashlightLens.sharedMaterial = on ? lensOnMaterial : lensOffMaterial;
        }

        // 光の筋に照らされた的は、その場でやられる (光っている間は、照らした的を次々に倒せる)。壁などの向こうの的は照らせない
        void Shine()
        {
            var muzzle = MuzzleOf(current);
            Vector3 origin = muzzle.position, dir = muzzle.forward;
            float range = Weapons.Get(current).range;
            int n = Physics.SphereCastNonAlloc(origin, flashlightRadius, dir, castHits, range, hitMask, QueryTriggerInteraction.Ignore);
            litThisFrame.Clear();
            for (int i = 0; i < n; i++)
            {
                var h = castHits[i];
                var t = h.collider.GetComponentInParent<Target>();
                if (t == null || !t.IsUp || !litThisFrame.Add(t)) continue; // (1つの的は1フレームに1回だけ数える)
                Vector3 point = h.distance > 0f ? h.point : h.collider.bounds.center; // 0 = 始めから光の中に重なっていた物
                if (Physics.Linecast(origin, point, out var block, hitMask, QueryTriggerInteraction.Ignore)
                    && block.collider.GetComponentInParent<Target>() != t) continue;
                // 照らされた時間をためて、たまりきったらやられる (すぐには消えない。LightExposure)
                if (LightExposure.Add(t, Time.deltaTime))
                {
                    t.Hit(point);
                    XRBridge.Haptic(hapticNode, 0.6f, 0.08f);
                }
                else XRBridge.Haptic(hapticNode, 0.15f, 0.02f); // 照らしている間は、かすかに震える
            }
        }

        readonly System.Collections.Generic.HashSet<Target> litThisFrame = new System.Collections.Generic.HashSet<Target>();

        // 電池の目盛り: 残りの分だけ左から点く (多い時は緑、減ると黄、少ないと赤)。切れて充電中は、たまった分が赤
        void UpdateBatteryRow()
        {
            if (batteryCells == null || batteryCells.Length == 0) return;
            int count = batteryCells.Length;
            int lit = Mathf.CeilToInt(battery * count - 0.001f);
            var fill = batteryDepleted || battery <= 0.25f ? batteryRedMaterial : battery <= 0.55f ? batteryYellowMaterial : batteryGreenMaterial;
            for (int i = 0; i < count; i++)
            {
                if (batteryCells[i] == null) continue;
                var m = i < lit ? fill : batteryEmptyMaterial;
                if (batteryCells[i].sharedMaterial != m) batteryCells[i].sharedMaterial = m;
            }
        }

        // 銃の上の弾の列: 弾数のある武器の時だけ出す。残りの弾は黄色、撃った分は右から灰色。弾込めの間は左から1発ずつ黄色に戻る
        void UpdateAmmoRow()
        {
            if (ammoRoot == null || ammoBullets == null) return;
            var def = Weapons.Get(current);
            bool show = def.magazine > 1; // 1発ずつ込める武器 (コルク銃・ポイ) はゲージで見せる
            if (ammoRoot.gameObject.activeSelf != show) ammoRoot.gameObject.SetActive(show);
            if (!show) return;
            int full = Mathf.Max(0, ammo);
            bool manual = NeedsReload; // トリガーで込めている途中: 込めた分だけ緑
            if (manual) full = loaded;
            else if (Time.time < reloadUntil && def.reloadTime > 0f)
                full = Mathf.FloorToInt((1f - (reloadUntil - Time.time) / def.reloadTime) * def.magazine);
            var fullMat = manual && ammoLoadingMaterial != null ? ammoLoadingMaterial : ammoFullMaterial;
            for (int i = 0; i < ammoBullets.Length; i++)
            {
                if (ammoBullets[i] == null) continue;
                bool on = i < def.magazine;
                if (ammoBullets[i].enabled != on) ammoBullets[i].enabled = on;
                var m = i < full ? fullMat : ammoEmptyMaterial;
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

            if (IsFestival(current))
            {
                var kind = current == WeaponType.RingToss ? FestivalThrow.Kind.Ring : current == WeaponType.CorkGun ? FestivalThrow.Kind.Cork
                         : current == WeaponType.Poi ? FestivalThrow.Kind.Poi : FestivalThrow.Kind.Coin;
                float gravity = def.projectileGravity;
                Vector3 velocity = IsArc(current) ? ArcVelocity(origin, muzzle.forward, out gravity) : muzzle.forward * def.projectileSpeed;
                FestivalThrow.Spawn(kind, origin, velocity, gravity, this, hitMask);
                // 手に持った輪・お金は投げたら消え、少しして次が出る (ポイとコルクは弾込めが終わるまで出ない: LateUpdate)
                if (current == WeaponType.RingToss || current == WeaponType.Coin)
                {
                    if (reloadRoutine != null) StopCoroutine(reloadRoutine);
                    reloadRoutine = isActiveAndEnabled ? StartCoroutine(Reload(def.fireInterval * 0.8f)) : null;
                }
            }
            else if (current == WeaponType.Shuriken || current == WeaponType.Dart || current == WeaponType.Pickaxe)
            {
                if (current == WeaponType.Shuriken)
                    Shuriken.Spawn(origin, muzzle.forward, def.projectileSpeed, def.range, shurikenMaterial, shurikenCenterMaterial, hitMask);
                else if (current == WeaponType.Pickaxe)
                    Shuriken.SpawnPickaxe(origin, muzzle.forward, def.projectileSpeed, def.range, pickaxeHandleMaterial, pickaxeHeadMaterial, hitMask);
                else
                    Shuriken.SpawnDart(origin, muzzle.forward, def.projectileSpeed, def.range, dartShaftMaterial, dartFlightMaterial, hitMask);
                // 手に持った1本 (つるはし) は投げたら消え、少しして次の1本が出る
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
            else if (current == WeaponType.Imagination)
                ImaginationShot.Spawn(origin, muzzle.forward, def.projectileSpeed, imaginationMaterials, imaginationTrailMaterial, imaginationSparkMaterial);
            else if (current == WeaponType.Trumpet)
                NoteShot.Spawn(origin, muzzle.forward, def.projectileSpeed, noteShotMaterials, noteLineMaterial, noteHoleMaterial, noteSparkMaterial);
            else if (current == WeaponType.Torpedo)
            {
                Torpedo.Spawn(origin, muzzle.forward, def.projectileSpeed, def.blastRadius, torpedoBodyMaterial, torpedoNoseMaterial, torpedoPropellerMaterial,
                              bubbleMaterial, torpedoExplosionMaterial);
                if (torpedoLoaded != null)
                {
                    if (reloadRoutine != null) StopCoroutine(reloadRoutine);
                    reloadRoutine = isActiveAndEnabled ? StartCoroutine(Reload(def.fireInterval * 0.9f, torpedoLoaded)) : null;
                }
            }
            else if (current == WeaponType.FireBall && fireballOuterMaterial != null)
                FireBallShot.Spawn(origin, muzzle.forward, def.projectileSpeed, def.blastRadius, fireballOuterMaterial, fireballMaterial, fireballTrailMaterial,
                                   fireballExplosionMaterial != null ? fireballExplosionMaterial : explosionMaterial);
            else if (def.projectile)
            {
                bool fire = current == WeaponType.FireBall;
                bool cannon = current == WeaponType.Bazooka;
                Bomb.Spawn(origin, muzzle.forward * def.projectileSpeed, def.blastRadius,
                           fire && fireballMaterial != null ? fireballMaterial : cannon && cannonballMaterial != null ? cannonballMaterial : bombMaterial,
                           fire && fireballExplosionMaterial != null ? fireballExplosionMaterial : explosionMaterial,
                           def.projectileGravity, cannon ? 0.14f : fire ? 0.2f : 0.09f);
            }
            else
                FireHitscan(def, muzzle, origin);

            // 弾数のある武器: 撃ちきったら弾込め (トリガーで込める武器は、次にトリガーを引くまで空のまま)
            if (def.magazine > 0 && --ammo <= 0 && def.reloadStep <= 0)
            {
                ammo = def.magazine;
                reloadUntil = Time.time + def.reloadTime;
                readyAt[current] = reloadUntil;
                if (current != WeaponType.Poi) Sfx.Play("reload"); // ポイは次のポイが来るのを待つだけ (音なし)
            }

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            // 自分の弾でスナイパーの的を取ると左手の銃はその場で非表示になるので、その時はフラッシュを出さない
            flashRoutine = isActiveAndEnabled ? StartCoroutine(Flash(muzzle)) : null;
            Sfx.PlayShot(current);
            bool heavy = current == WeaponType.Shotgun || current == WeaponType.Bomb || current == WeaponType.Sniper || current == WeaponType.Bazooka || current == WeaponType.CorkGun;
            XRBridge.Haptic(hapticNode, heavy ? 0.9f : 0.5f, heavy ? 0.12f : 0.06f);
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
                bool hitSomething = false;
                if (TryHitTarget(origin, dir, def.range, assist, out var target, out var point))
                {
                    end = point;
                    hitSomething = true;
                    target.Hit(point);
                }
                else if (Physics.Raycast(origin, dir, out var wall, def.range, hitMask, QueryTriggerInteraction.Ignore))
                    end = wall.point;
                if (current == WeaponType.Lightning) SpawnLightning(origin, end, hitSomething);
                else SpawnTracer(origin, end);
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

        // 紫の電撃: 銃口から当たった所まで、まっすぐな太いレーザーが1本走る (太さは銃の胴より少し細いくらい。的に当たらなかった時は 14m 先までで消える)
        const float LightningBeamWidth = 0.028f;

        void SpawnLightning(Vector3 from, Vector3 to, bool hit)
        {
            if (lightningMaterial == null) { SpawnTracer(from, to); return; }
            if (!hit) to = from + (to - from).normalized * Mathf.Min(Vector3.Distance(from, to), 14f);
            LightningBolt.Spawn(from, to, lightningMaterial, LightningBeamWidth, 0.12f, 0f, null, 0.85f);
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
