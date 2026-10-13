using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace ShootingGallery
{
    /// <summary>GalleryBuilder のうち、プレイヤー (XR リグ・銃) の生成。</summary>
    public partial class GalleryBuilder
    {
        // ------------------------------------------------------------------
        // プレイヤー (XRリグ + 銃)
        // ------------------------------------------------------------------

        PlayerRig BuildPlayer()
        {
            // 参照を全部つないでから Awake させるため、組み立て中は非アクティブにしておく
            var rigGo = new GameObject("PlayerRig");
            rigGo.SetActive(false);
            rigGo.transform.SetParent(world, false);
            var offset = Group(rigGo.transform, "CameraOffset");

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.14f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<AudioSource>();
            camGo.AddComponent<Sfx>();
            AddPoseDriver(camGo, "<XRHMD>/centerEyePosition", "<XRHMD>/centerEyeRotation");
            // 目の前の黒い板 (場面の切り替えで暗くする。ふだんは透明で表示しない)
            var fadeMat = TransparentMat("ScreenFade", Color.black);
            fadeMat.renderQueue = 4000; // いちばん最後に描く (手の銃や文字も覆う)
            var fader = Prim(PrimitiveType.Quad, "ScreenFade", camGo.transform, new Vector3(0f, 0f, 0.05f), new Vector3(0.6f, 0.6f, 1f), fadeMat, false);
            fader.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fader.AddComponent<ScreenFader>();

            var right = Group(offset, "RightHand");
            right.localPosition = new Vector3(0.22f, 1.3f, 0.35f);
            AddPoseDriver(right.gameObject, "<XRController>{RightHand}/pointerPosition", "<XRController>{RightHand}/pointerRotation");

            var left = Group(offset, "LeftHand");
            left.localPosition = new Vector3(-0.22f, 1.3f, 0.35f);
            AddPoseDriver(left.gameObject, "<XRController>{LeftHand}/pointerPosition", "<XRController>{LeftHand}/pointerRotation");

            var rig = rigGo.AddComponent<PlayerRig>();
            rig.cameraOffset = offset;
            rig.head = cam;
            rig.rightHand = right;
            rig.leftHand = left;
            rig.rightGun = BuildGun(right, "RightGun", XRNode.RightHand);
            builtGun = rig.rightGun; // 武器の的のカプセルの中のフィギュアに使う
            rig.leftGun = BuildGun(left, "LeftGun", XRNode.LeftHand);
            rig.resetIndicator = BuildResetIndicator(camGo.transform);
            rig.endlessIndicator = BuildResetIndicator(camGo.transform, "EndlessIndicator", "HOLD B : ENDLESS", "ENDLESS!", new Color(0.3f, 1f, 0.85f));
            rig.debugIndicator = BuildResetIndicator(camGo.transform, "DebugIndicator", "HOLD X : DEBUG", "DEBUG!", new Color(0.6f, 1f, 0.3f));
            rigGo.SetActive(true);
            return rig;
        }

        static void AddPoseDriver(GameObject go, string position, string rotation)
        {
            var tpd = go.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.ignoreTrackingState = true;
            tpd.positionInput = new InputActionProperty(new InputAction(go.name + " Position", InputActionType.Value, position, expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction(go.name + " Rotation", InputActionType.Value, rotation, expectedControlType: "Quaternion"));
        }

        GunController BuildGun(Transform hand, string name, XRNode node)
        {
            var gunGo = new GameObject(name);
            gunGo.transform.SetParent(hand, false);
            var gun = gunGo.AddComponent<GunController>();
            gun.hapticNode = node;
            gun.tracerMaterial = Mat("Tracer", new Color(1f, 0.9f, 0.5f), null, true);

            var metal = Mat("Gun_Metal", new Color(0.18f, 0.18f, 0.2f));
            var grip = Mat("Gun_Grip", new Color(0.35f, 0.2f, 0.1f));
            var blue = Mat("Gun_Blue", new Color(0.2f, 0.35f, 0.7f));
            var green = Mat("Gun_Green", new Color(0.2f, 0.5f, 0.25f));
            var flashMat = Mat("MuzzleFlash", new Color(1f, 0.8f, 0.3f), null, true);

            // 0: ハンドガン
            var hg = Group(gunGo.transform, "Model_Handgun");
            GunPart(PrimitiveType.Cube, hg, new Vector3(0f, 0f, 0.05f), new Vector3(0.035f, 0.045f, 0.17f), metal);
            GunPart(PrimitiveType.Cube, hg, new Vector3(0f, -0.055f, -0.015f), new Vector3(0.03f, 0.09f, 0.04f), grip, 15f);
            var hgMuzzle = Muzzle(hg, new Vector3(0f, 0.005f, 0.14f), flashMat);

            // 1: マシンガン
            var mg = Group(gunGo.transform, "Model_MachineGun");
            GunPart(PrimitiveType.Cube, mg, new Vector3(0f, 0f, 0.08f), new Vector3(0.05f, 0.065f, 0.28f), blue);
            GunPart(PrimitiveType.Cylinder, mg, new Vector3(0f, 0.01f, 0.28f), new Vector3(0.022f, 0.07f, 0.022f), metal, 90f);
            GunPart(PrimitiveType.Cube, mg, new Vector3(0f, -0.08f, 0.1f), new Vector3(0.03f, 0.11f, 0.045f), metal, -10f);
            GunPart(PrimitiveType.Cube, mg, new Vector3(0f, -0.06f, -0.02f), new Vector3(0.03f, 0.09f, 0.04f), grip, 15f);
            var mgMuzzle = Muzzle(mg, new Vector3(0f, 0.01f, 0.36f), flashMat);

            // 2: ショットガン (二連)
            var sg = Group(gunGo.transform, "Model_Shotgun");
            GunPart(PrimitiveType.Cube, sg, new Vector3(0f, 0f, 0.04f), new Vector3(0.05f, 0.055f, 0.2f), green);
            GunPart(PrimitiveType.Cylinder, sg, new Vector3(-0.013f, 0.01f, 0.28f), new Vector3(0.024f, 0.15f, 0.024f), metal, 90f);
            GunPart(PrimitiveType.Cylinder, sg, new Vector3(0.013f, 0.01f, 0.28f), new Vector3(0.024f, 0.15f, 0.024f), metal, 90f);
            GunPart(PrimitiveType.Cube, sg, new Vector3(0f, -0.06f, -0.04f), new Vector3(0.035f, 0.1f, 0.05f), grip, 20f);
            var sgMuzzle = Muzzle(sg, new Vector3(0f, 0.01f, 0.44f), flashMat);

            // 3: 爆弾ランチャー (太い筒)
            var bm = Group(gunGo.transform, "Model_Bomb");
            var orange = Mat("Gun_Orange", new Color(0.9f, 0.45f, 0.1f));
            GunPart(PrimitiveType.Cylinder, bm, new Vector3(0f, 0.01f, 0.12f), new Vector3(0.075f, 0.16f, 0.075f), orange, 90f);
            GunPart(PrimitiveType.Cylinder, bm, new Vector3(0f, 0.01f, 0.285f), new Vector3(0.085f, 0.012f, 0.085f), metal, 90f);
            GunPart(PrimitiveType.Cube, bm, new Vector3(0f, -0.06f, -0.01f), new Vector3(0.035f, 0.09f, 0.045f), grip, 15f);
            var bmMuzzle = Muzzle(bm, new Vector3(0f, 0.01f, 0.3f), flashMat);

            // 4: スナイパーライフル (長い銃身 + スコープ + ストック)。レーザーポインター付き
            var sr = Group(gunGo.transform, "Model_Sniper");
            var sniperBody = Mat("Gun_Sniper", new Color(0.25f, 0.27f, 0.22f));
            GunPart(PrimitiveType.Cube, sr, new Vector3(0f, 0f, 0.06f), new Vector3(0.04f, 0.05f, 0.26f), sniperBody);
            GunPart(PrimitiveType.Cylinder, sr, new Vector3(0f, 0.01f, 0.36f), new Vector3(0.018f, 0.17f, 0.018f), metal, 90f);
            GunPart(PrimitiveType.Cylinder, sr, new Vector3(0f, 0.055f, 0.08f), new Vector3(0.032f, 0.08f, 0.032f), metal, 90f);   // スコープ
            GunPart(PrimitiveType.Cylinder, sr, new Vector3(0f, 0.055f, 0.165f), new Vector3(0.042f, 0.012f, 0.042f), metal, 90f); // スコープの前玉
            GunPart(PrimitiveType.Cube, sr, new Vector3(0f, -0.015f, -0.12f), new Vector3(0.035f, 0.06f, 0.12f), grip);           // ストック
            GunPart(PrimitiveType.Cube, sr, new Vector3(0f, -0.06f, -0.02f), new Vector3(0.03f, 0.09f, 0.04f), grip, 15f);
            var srMuzzle = Muzzle(sr, new Vector3(0f, 0.01f, 0.53f), flashMat);

            // 5: バズーカ (海戦モード専用。肩にかつぐ長い筒 + 照準 + 前後の握り)
            var bz = Group(gunGo.transform, "Model_Bazooka");
            var olive = Mat("Gun_Bazooka", new Color(0.3f, 0.36f, 0.2f));
            GunPart(PrimitiveType.Cylinder, bz, new Vector3(0f, 0.04f, 0.1f), new Vector3(0.09f, 0.3f, 0.09f), olive, 90f);            // 筒
            GunPart(PrimitiveType.Cylinder, bz, new Vector3(0f, 0.04f, 0.4f), new Vector3(0.11f, 0.03f, 0.11f), metal, 90f);          // 筒先
            GunPart(PrimitiveType.Cylinder, bz, new Vector3(0f, 0.04f, -0.2f), new Vector3(0.105f, 0.025f, 0.105f), metal, 90f);       // 後ろ
            GunPart(PrimitiveType.Cube, bz, new Vector3(0.06f, 0.09f, 0.12f), new Vector3(0.02f, 0.04f, 0.06f), metal);                 // 照準
            GunPart(PrimitiveType.Cube, bz, new Vector3(0f, -0.04f, -0.01f), new Vector3(0.03f, 0.09f, 0.04f), grip, 15f);
            GunPart(PrimitiveType.Cube, bz, new Vector3(0f, -0.03f, 0.2f), new Vector3(0.03f, 0.07f, 0.035f), grip, 10f);
            var bzMuzzle = Muzzle(bz, new Vector3(0f, 0.04f, 0.42f), flashMat);

            // 6: 手裏剣 (忍者モード専用。手のひらの先に1枚持っている。投げると消えて、少しして次の1枚が出る)
            var sk = Group(gunGo.transform, "Model_Shuriken");
            var steel = Mat("Shuriken_Steel", new Color(0.75f, 0.77f, 0.8f), null, false, 0.85f);
            var skHub = Mat("Ninja_Eye", new Color(0.02f, 0.02f, 0.02f));
            var skSpin = Group(sk, "Spin");
            skSpin.localPosition = new Vector3(0f, 0.01f, 0.07f);
            for (int i = 0; i < 4; i++)
            {
                var blade = Prim(PrimitiveType.Cube, "Blade", skSpin, Vector3.zero, new Vector3(0.022f, 0.005f, 0.12f), steel, false);
                blade.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
                blade.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Prim(PrimitiveType.Cylinder, "Hub", skSpin, Vector3.zero, new Vector3(0.035f, 0.004f, 0.035f), skHub, false);
            var skTurn = skSpin.gameObject.AddComponent<Rotator>(); // 持っている間もゆっくり回る
            skTurn.speed = 90f;
            var skMuzzle = Group(sk, "Muzzle"); // マズルフラッシュは無し (子を持たない)
            skMuzzle.localPosition = new Vector3(0f, 0.01f, 0.14f);

            // 7: 怪獣の頭 (怪獣モード専用。緑の頭に黄色い目と白い歯、背中のとげ。口から炎の玉を吐く)
            var kj = Group(gunGo.transform, "Model_KaijuHead");
            var kGreen = Mat("Kaiju_Skin", new Color(0.2f, 0.55f, 0.25f), null, false, 0.4f);
            var kDark = Mat("Kaiju_Dark", new Color(0.1f, 0.3f, 0.12f));
            var kEye = Mat("Kaiju_Eye", new Color(1f, 0.9f, 0.2f), new Color(1f, 0.8f, 0.1f));
            var kTooth = Mat("Target_White", Color.white);
            GunPart(PrimitiveType.Sphere, kj, new Vector3(0f, 0.01f, 0.02f), new Vector3(0.11f, 0.1f, 0.14f), kGreen);          // 頭
            GunPart(PrimitiveType.Cube, kj, new Vector3(0f, 0.015f, 0.12f), new Vector3(0.08f, 0.035f, 0.12f), kGreen);         // 上あご
            GunPart(PrimitiveType.Cube, kj, new Vector3(0f, -0.03f, 0.11f), new Vector3(0.07f, 0.02f, 0.1f), kDark, 12f);       // 下あご (少し開いた口)
            for (int i = 0; i < 4; i++)
            {
                GunPart(PrimitiveType.Cube, kj, new Vector3(-0.027f + i * 0.018f, -0.006f, 0.165f), new Vector3(0.008f, 0.014f, 0.008f), kTooth); // 上の歯
                GunPart(PrimitiveType.Cube, kj, new Vector3(-0.027f + i * 0.018f, -0.024f, 0.15f), new Vector3(0.007f, 0.012f, 0.007f), kTooth);  // 下の歯
            }
            foreach (float s in new[] { -1f, 1f })
            {
                GunPart(PrimitiveType.Sphere, kj, new Vector3(s * 0.035f, 0.05f, 0.07f), new Vector3(0.025f, 0.02f, 0.02f), kEye);
                GunPart(PrimitiveType.Cube, kj, new Vector3(s * 0.035f, 0.065f, 0.07f), new Vector3(0.03f, 0.008f, 0.012f), kDark, s * 10f); // まゆ
                GunPart(PrimitiveType.Sphere, kj, new Vector3(s * 0.02f, 0.035f, 0.18f), Vector3.one * 0.008f, kDark);                          // 鼻の穴
            }
            for (int i = 0; i < 4; i++) // 背中のとげ
                GunPart(PrimitiveType.Cube, kj, new Vector3(0f, 0.065f - i * 0.008f, -0.01f - i * 0.03f), new Vector3(0.012f, 0.04f - i * 0.006f, 0.03f), kDark, -25f);
            var kjMuzzle = Muzzle(kj, new Vector3(0f, -0.012f, 0.19f), Mat("Kaiju_Flame", new Color(1f, 0.5f, 0.1f), null, true));

            // 8: ダーツ (カジノモード専用。指先に1本持っている。投げると消えて、すぐ次の1本が出る)
            var dt = Group(gunGo.transform, "Model_Dart");
            var dartShaft = Mat("Dart_Shaft", new Color(0.8f, 0.8f, 0.85f), null, false, 0.8f);
            var dartFlight = Mat("Dart_Flight", new Color(1f, 0.2f, 0.45f), new Color(0.3f, 0.05f, 0.12f));
            GunPart(PrimitiveType.Cylinder, dt, new Vector3(0f, 0.01f, 0.07f), new Vector3(0.012f, 0.06f, 0.012f), dartShaft, 90f);
            GunPart(PrimitiveType.Cylinder, dt, new Vector3(0f, 0.01f, 0.155f), new Vector3(0.004f, 0.03f, 0.004f), dartShaft, 90f);
            GunPart(PrimitiveType.Cube, dt, new Vector3(0f, 0.01f, 0f), new Vector3(0.05f, 0.002f, 0.04f), dartFlight);
            GunPart(PrimitiveType.Cube, dt, new Vector3(0f, 0.01f, 0f), new Vector3(0.002f, 0.05f, 0.04f), dartFlight);
            var dtMuzzle = Group(dt, "Muzzle"); // マズルフラッシュは無し
            dtMuzzle.localPosition = new Vector3(0f, 0.01f, 0.12f);

            // 9: リボルバー (西部劇モード専用。長い銃身・回る弾倉・撃鉄・木の握り。6発撃つと弾倉が回って弾込め)
            var rv = Group(gunGo.transform, "Model_Revolver");
            var steelDark = Mat("Revolver_Steel", new Color(0.25f, 0.25f, 0.28f), null, false, 0.8f);
            var woodGrip = Mat("Revolver_Grip", new Color(0.45f, 0.25f, 0.12f), null, false, 0.4f);
            GunPart(PrimitiveType.Cylinder, rv, new Vector3(0f, 0.015f, 0.13f), new Vector3(0.018f, 0.08f, 0.018f), steelDark, 90f); // 銃身
            GunPart(PrimitiveType.Cube, rv, new Vector3(0f, 0.032f, 0.2f), new Vector3(0.005f, 0.012f, 0.008f), steelDark);          // 照星
            GunPart(PrimitiveType.Cube, rv, new Vector3(0f, 0.005f, 0.02f), new Vector3(0.03f, 0.04f, 0.07f), steelDark);             // 本体
            var cylinder = Group(rv, "Cylinder");
            cylinder.localPosition = new Vector3(0f, 0.01f, 0.035f);
            cylinder.localRotation = Quaternion.Euler(90f, 0f, 0f); // ローカル Y が銃身の向き (この軸で回る)
            GunPart(PrimitiveType.Cylinder, cylinder, Vector3.zero, new Vector3(0.036f, 0.022f, 0.036f), steelDark);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                GunPart(PrimitiveType.Cylinder, cylinder, new Vector3(Mathf.Cos(a) * 0.012f, 0f, Mathf.Sin(a) * 0.012f), new Vector3(0.007f, 0.023f, 0.007f), Mat("Revolver_Brass", new Color(0.9f, 0.7f, 0.3f), null, false, 0.7f));
            }
            GunPart(PrimitiveType.Cube, rv, new Vector3(0f, 0.03f, -0.015f), new Vector3(0.01f, 0.018f, 0.015f), steelDark, -30f); // 撃鉄
            GunPart(PrimitiveType.Cube, rv, new Vector3(0f, -0.045f, -0.025f), new Vector3(0.028f, 0.075f, 0.035f), woodGrip, 18f);   // 握り
            GunPart(PrimitiveType.Cube, rv, new Vector3(0f, -0.022f, 0.025f), new Vector3(0.004f, 0.02f, 0.025f), steelDark);        // 用心金
            var rvMuzzle = Muzzle(rv, new Vector3(0f, 0.015f, 0.215f), flashMat);

            // 10: ミサイルランチャー (宇宙モード専用。白と青の発射筒の先に、赤い先端のミサイルが込めてある。撃つと先端が消えて、すぐ次が込められる)
            var ms = Group(gunGo.transform, "Model_Missile");
            var msWhite = Mat("Missile_Body", new Color(0.92f, 0.93f, 0.96f), null, false, 0.6f);
            var msBlue = Mat("Missile_Blue", new Color(0.2f, 0.4f, 0.9f), new Color(0.04f, 0.08f, 0.2f), false, 0.5f);
            var msRed = Mat("Missile_Nose", new Color(0.95f, 0.15f, 0.12f), new Color(0.25f, 0.02f, 0.02f), false, 0.6f);
            GunPart(PrimitiveType.Cylinder, ms, new Vector3(0f, 0.025f, 0.06f), new Vector3(0.05f, 0.11f, 0.05f), msWhite, 90f);  // 発射筒
            GunPart(PrimitiveType.Cylinder, ms, new Vector3(0f, 0.025f, 0.165f), new Vector3(0.056f, 0.008f, 0.056f), msBlue, 90f); // 筒の先の青い輪
            GunPart(PrimitiveType.Cylinder, ms, new Vector3(0f, 0.025f, -0.045f), new Vector3(0.056f, 0.008f, 0.056f), msBlue, 90f); // 筒の後ろの青い輪
            GunPart(PrimitiveType.Cube, ms, new Vector3(0f, 0.058f, 0.05f), new Vector3(0.01f, 0.016f, 0.05f), msBlue);              // 照準
            GunPart(PrimitiveType.Cube, ms, new Vector3(0f, -0.02f, 0.01f), new Vector3(0.024f, 0.06f, 0.03f), msBlue, 15f);        // 握り
            var loaded = Group(ms, "LoadedMissile"); // 筒から顔を出しているミサイルの先
            GunPart(PrimitiveType.Sphere, loaded, new Vector3(0f, 0.025f, 0.18f), new Vector3(0.036f, 0.036f, 0.06f), msRed);
            GunPart(PrimitiveType.Cylinder, loaded, new Vector3(0f, 0.025f, 0.165f), new Vector3(0.036f, 0.01f, 0.036f), msWhite, 90f);
            var msMuzzle = Muzzle(ms, new Vector3(0f, 0.025f, 0.2f), Mat("Missile_Flash", new Color(0.7f, 0.85f, 1f), null, true));

            // 11: 懐中電灯 (幽霊モード専用。黒い胴に金の頭、トリガーを引いている間だけレンズが光り、光の筋とスポットライトが出る)
            var fl = Group(gunGo.transform, "Model_Flashlight");
            var flBody = Mat("Flashlight_Body", new Color(0.12f, 0.12f, 0.14f), null, false, 0.6f);
            var flTrim = Mat("Flashlight_Trim", new Color(0.95f, 0.75f, 0.15f), null, false, 0.5f);
            GunPart(PrimitiveType.Cylinder, fl, new Vector3(0f, 0f, 0.03f), new Vector3(0.032f, 0.075f, 0.032f), flBody, 90f);   // 胴
            for (int i = 0; i < 3; i++)
                GunPart(PrimitiveType.Cylinder, fl, new Vector3(0f, 0f, -0.025f + i * 0.02f), new Vector3(0.035f, 0.004f, 0.035f), Mat("Flashlight_Rubber", new Color(0.05f, 0.05f, 0.05f)), 90f); // 滑り止め
            GunPart(PrimitiveType.Cylinder, fl, new Vector3(0f, 0f, 0.125f), new Vector3(0.05f, 0.025f, 0.05f), flTrim, 90f);    // 頭
            GunPart(PrimitiveType.Cylinder, fl, new Vector3(0f, 0f, 0.153f), new Vector3(0.056f, 0.006f, 0.056f), flBody, 90f);  // 縁
            GunPart(PrimitiveType.Cube, fl, new Vector3(0f, 0.019f, 0.045f), new Vector3(0.012f, 0.008f, 0.02f), Mat("Flashlight_Switch", new Color(0.85f, 0.15f, 0.12f))); // スイッチ
            var lensOff = Mat("Flashlight_LensOff", new Color(0.55f, 0.55f, 0.5f), null, false, 0.9f);
            var lensOn = Mat("Flashlight_LensOn", new Color(1f, 0.97f, 0.85f), new Color(3f, 2.8f, 2.2f), true);
            var lens = RemoveCollider(Prim(PrimitiveType.Cylinder, "Lens", fl, new Vector3(0f, 0f, 0.158f), new Vector3(0.046f, 0.003f, 0.046f), lensOff));
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lens.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var flMuzzle = Group(fl, "Muzzle"); // マズルフラッシュは無し
            flMuzzle.localPosition = new Vector3(0f, 0f, 0.16f);
            // 光の筋 (先が広がる透ける円すいを2重に) とスポットライト。光っている間だけ出す
            var beam = Group(fl, "Beam");
            beam.localPosition = new Vector3(0f, 0f, 0.16f);
            foreach (var (radius, length, alpha, name2) in new[] { (0.9f, 7f, 0.1f, "Flashlight_Beam"), (0.4f, 5f, 0.12f, "Flashlight_BeamCore") })
            {
                var cone = new GameObject("BeamCone");
                cone.transform.SetParent(beam, false);
                cone.transform.localScale = new Vector3(radius, radius, length);
                cone.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
                var cr = cone.AddComponent<MeshRenderer>();
                cr.sharedMaterial = TransparentMat(name2, new Color(1f, 0.95f, 0.75f, alpha));
                cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cr.receiveShadows = false;
            }
            var lightGo = new GameObject("FlashlightLight");
            lightGo.transform.SetParent(fl, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.16f);
            var spot = lightGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 30f;
            spot.spotAngle = 30f;
            spot.innerSpotAngle = 16f;
            spot.intensity = 8f;
            spot.color = new Color(1f, 0.95f, 0.8f);
            spot.shadows = LightShadows.None;
            spot.enabled = false;
            beam.gameObject.SetActive(false);

            var laserMat = Mat("Laser", new Color(1f, 0.1f, 0.1f), null, true);
            var laserGo = new GameObject("Laser");
            laserGo.transform.SetParent(gunGo.transform, false);
            var laser = laserGo.AddComponent<LineRenderer>();
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.startWidth = 0.004f;
            laser.endWidth = 0.004f;
            laser.sharedMaterial = laserMat;
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.receiveShadows = false;
            var dot = RemoveCollider(Prim(PrimitiveType.Sphere, "LaserDot", gunGo.transform, Vector3.zero, Vector3.one * 0.03f, Mat("LaserDot", new Color(1f, 0.25f, 0.2f), null, true)));
            dot.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gun.laser = laser;
            gun.laserDot = dot.transform;
            gun.whiteLaserMaterial = Mat("Laser_White", new Color(1f, 1f, 1f), null, true);
            gun.whiteDotMaterial = Mat("LaserDot_White", new Color(1f, 1f, 1f), null, true);
            gun.shurikenMaterial = steel;
            gun.shurikenCenterMaterial = skHub;
            laserGo.SetActive(false);
            dot.SetActive(false);

            gun.bombMaterial = Mat("Bomb", new Color(0.12f, 0.12f, 0.14f), new Color(0.5f, 0.15f, 0f));
            gun.explosionMaterial = TransparentMat("Explosion", new Color(1f, 0.55f, 0.15f, 0.8f));
            gun.models = new[] { hg.gameObject, mg.gameObject, sg.gameObject, bm.gameObject, sr.gameObject, bz.gameObject, sk.gameObject, kj.gameObject, dt.gameObject, rv.gameObject, ms.gameObject, fl.gameObject };
            gun.muzzles = new[] { hgMuzzle, mgMuzzle, sgMuzzle, bmMuzzle, srMuzzle, bzMuzzle, skMuzzle, kjMuzzle, dtMuzzle, rvMuzzle, msMuzzle, flMuzzle };
            gun.fireballMaterial = Mat("Fireball", new Color(1f, 0.55f, 0.1f), new Color(3f, 1.2f, 0.2f));
            gun.fireballExplosionMaterial = TransparentMat("Fire_Explosion", new Color(1f, 0.45f, 0.08f, 0.85f));
            gun.dartShaftMaterial = dartShaft;
            gun.dartFlightMaterial = dartFlight;
            gun.revolverCylinder = cylinder;
            gun.missileBodyMaterial = msWhite;
            gun.missileFinMaterial = msRed;
            gun.missileFlameMaterial = Mat("Missile_Flame", new Color(0.6f, 0.8f, 1f), new Color(1.5f, 2.2f, 4f), true);
            gun.missileTrailMaterial = TransparentMat("Missile_Trail", new Color(0.85f, 0.9f, 1f, 0.45f));
            gun.missileLoaded = loaded.gameObject;
            gun.infoText = MakeText(gunGo.transform, "InfoText", "HANDGUN", new Vector3(0f, 0.11f, 0f), 0.025f, Color.white);

            // 特殊武器の残り時間ゲージ (銃の上に浮かぶ横長バー)
            const float GaugeWidth = 0.14f;
            var gauge = Group(gunGo.transform, "TimeGauge");
            gauge.localPosition = new Vector3(0f, 0.08f, 0f);
            RemoveCollider(Prim(PrimitiveType.Cube, "Frame", gauge, Vector3.zero, new Vector3(GaugeWidth + 0.008f, 0.02f, 0.002f), Mat("Gauge_Frame", new Color(0.05f, 0.05f, 0.06f), null, true)));
            var fill = RemoveCollider(Prim(PrimitiveType.Cube, "Fill", gauge, new Vector3(0f, 0f, -0.002f), new Vector3(GaugeWidth, 0.012f, 0.002f), Mat("Gauge_Fill", Color.white, null, true)));
            fill.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gun.gaugeRoot = gauge;
            gun.gaugeFill = fill.transform;
            gun.gaugeWidth = GaugeWidth;

            // 弾数のある武器 (リボルバー) の残りの弾: 銃の上 (文字の上) に6発並ぶ。黄色 = 残り、灰色 = 撃った分
            var ammoRow = Group(gunGo.transform, "AmmoRow");
            ammoRow.localPosition = new Vector3(0f, 0.145f, 0f);
            var ammoFull = Mat("Ammo_Full", new Color(1f, 0.82f, 0.2f), null, true);
            var ammoEmpty = Mat("Ammo_Empty", new Color(0.35f, 0.35f, 0.38f), null, true);
            var bullets = new List<Renderer>();
            const int AmmoSlots = 6;
            for (int i = 0; i < AmmoSlots; i++)
            {
                var b = RemoveCollider(Prim(PrimitiveType.Capsule, "Bullet", ammoRow, new Vector3((i - (AmmoSlots - 1) / 2f) * 0.019f, 0f, 0f), new Vector3(0.011f, 0.012f, 0.011f), ammoFull));
                var r = b.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bullets.Add(r);
            }
            ammoRow.gameObject.SetActive(false);
            gun.ammoRoot = ammoRow;
            gun.ammoBullets = bullets.ToArray();
            gun.ammoFullMaterial = ammoFull;
            gun.ammoEmptyMaterial = ammoEmpty;
            gun.flashlightBeam = beam.gameObject;
            gun.flashlightLight = spot;
            gun.flashlightLens = lens.GetComponent<Renderer>();
            gun.lensOnMaterial = lensOn;
            gun.lensOffMaterial = lensOff;

            // 懐中電灯の電池: 銃の上 (文字の上) に、電池の形の枠と5つの目盛り。残りの分だけ左から点く (緑 → 黄 → 赤、切れて充電中は赤)
            var batteryRow = Group(gunGo.transform, "BatteryRow");
            batteryRow.localPosition = new Vector3(0f, 0.145f, 0f);
            const int Cells = 5;
            const float CellW = 0.017f;
            var frameMat = Mat("Battery_Frame", new Color(0.85f, 0.85f, 0.88f), null, true);
            float bodyW = Cells * CellW + 0.008f;
            RemoveCollider(Prim(PrimitiveType.Cube, "BatteryFrame", batteryRow, new Vector3(0f, 0f, 0.001f), new Vector3(bodyW + 0.004f, 0.022f, 0.001f), frameMat));
            RemoveCollider(Prim(PrimitiveType.Cube, "BatteryInside", batteryRow, new Vector3(0f, 0f, 0f), new Vector3(bodyW, 0.018f, 0.001f), Mat("Battery_Inside", new Color(0.05f, 0.05f, 0.06f), null, true)));
            RemoveCollider(Prim(PrimitiveType.Cube, "BatteryTip", batteryRow, new Vector3(bodyW / 2f + 0.004f, 0f, 0.001f), new Vector3(0.005f, 0.01f, 0.001f), frameMat));
            var cells = new List<Renderer>();
            var cellEmpty = Mat("Battery_Empty", new Color(0.2f, 0.2f, 0.22f), null, true);
            for (int i = 0; i < Cells; i++)
            {
                var c = RemoveCollider(Prim(PrimitiveType.Cube, "Cell", batteryRow, new Vector3((i - (Cells - 1) / 2f) * CellW, 0f, -0.001f), new Vector3(CellW - 0.004f, 0.013f, 0.001f), cellEmpty));
                var r = c.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cells.Add(r);
            }
            batteryRow.gameObject.SetActive(false);
            gun.batteryRoot = batteryRow;
            gun.batteryCells = cells.ToArray();
            gun.batteryGreenMaterial = Mat("Battery_Green", new Color(0.3f, 1f, 0.35f), null, true);
            gun.batteryYellowMaterial = Mat("Battery_Yellow", new Color(1f, 0.85f, 0.2f), null, true);
            gun.batteryRedMaterial = Mat("Battery_Red", new Color(1f, 0.25f, 0.2f), null, true);
            gun.batteryEmptyMaterial = cellEmpty;
            return gun;
        }

        void GunPart(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, float xRot = 0f)
        {
            var go = Prim(type, type.ToString(), parent, pos, scale, mat);
            go.transform.localRotation = Quaternion.Euler(xRot, 0f, 0f);
            RemoveCollider(go);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Transform Muzzle(Transform parent, Vector3 pos, Material flashMat)
        {
            var muzzle = Group(parent, "Muzzle");
            muzzle.localPosition = pos;
            var flash = Prim(PrimitiveType.Sphere, "Flash", muzzle, Vector3.forward * 0.03f, new Vector3(0.07f, 0.07f, 0.11f), flashMat);
            RemoveCollider(flash);
            flash.SetActive(false);
            return muzzle;
        }
    }
}
