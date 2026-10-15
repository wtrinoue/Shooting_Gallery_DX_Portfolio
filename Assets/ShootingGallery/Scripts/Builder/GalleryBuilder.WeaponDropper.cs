using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GalleryBuilder のうち、武器などの特殊な的の見た目 (普通の的が化ける) と UFO の生成。</summary>
    public partial class GalleryBuilder
    {
        // ------------------------------------------------------------------
        // 武器の的のランダム出現 / UFO
        // ------------------------------------------------------------------

        void BuildWeaponDropper()
        {
            var go = new GameObject("WeaponDropper");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            // 武器の的の見た目 (箱 + 文字)。普通の的の「顔」と付け替えて使い回す。同時に出るのは最大2個
            var looks = new List<GameObject>();
            foreach (var kind in new[] { TargetKind.MachineGun, TargetKind.Shotgun, TargetKind.Bomb, TargetKind.Explosive, TargetKind.BlueOrb, TargetKind.Sniper, TargetKind.MachineGunChallenge, TargetKind.Deluxe, TargetKind.Naval, TargetKind.Sky, TargetKind.Ninja, TargetKind.Kaiju, TargetKind.Casino, TargetKind.Western, TargetKind.Space, TargetKind.Ghost, TargetKind.Mine, TargetKind.Festival, TargetKind.Submarine, TargetKind.Music, TargetKind.Museum, TargetKind.GateBack })
                for (int i = 0; i < 2; i++)
                    looks.Add(WeaponLook(go.transform, kind));

            var dropper = go.AddComponent<WeaponDropper>();
            dropper.looks = looks.ToArray();
            foreach (var look in looks) look.SetActive(false);
            go.SetActive(true);
        }

        /// <summary>武器の的の見た目。的の根元を原点に、円盤と同じ高さに箱が来るように作る。</summary>
        GameObject WeaponLook(Transform parent, TargetKind kind)
        {
            var look = Group(parent, $"Look_{kind}");
            if (kind == TargetKind.Deluxe) return DeluxeLook(look);
            if (kind == TargetKind.Naval) return NavalLook(look);
            if (kind == TargetKind.Sky) return SkyLook(look);
            if (kind == TargetKind.Ninja) return NinjaLook(look);
            if (kind == TargetKind.Kaiju) return KaijuLook(look);
            if (kind == TargetKind.Casino) return CasinoLook(look);
            if (kind == TargetKind.Western) return WesternLook(look);
            if (kind == TargetKind.Space) return SpaceLook(look);
            if (kind == TargetKind.Ghost) return GhostLook(look);
            if (kind == TargetKind.Mine) return MineLook(look);
            if (kind == TargetKind.Submarine) return SubmarineLook(look);
            if (kind == TargetKind.Music) return MusicLook(look);
            if (kind == TargetKind.Museum) return MuseumLook(look);
            if (kind == TargetKind.GateBack) return GateBackLook(look);
            if (kind == TargetKind.Festival) return FestivalLook(look);

            if (kind == TargetKind.BlueOrb)
            {
                // 青い玉: フィーバーの赤い玉の青い版。輪が回っている
                Prim(PrimitiveType.Sphere, "BlueOrb", look, new Vector3(0f, TargetDiscY, 0f), Vector3.one * 0.24f,
                     Mat("Target_BlueOrb", new Color(0.15f, 0.45f, 1f), new Color(0.2f, 0.6f, 1.8f)));
                var spin = Group(look, "Spinner");
                spin.localPosition = new Vector3(0f, TargetDiscY, 0f);
                var rotator = spin.gameObject.AddComponent<Rotator>();
                rotator.axis = Vector3.up;
                rotator.speed = 150f;
                var ring = Prim(PrimitiveType.Cylinder, "Ring", spin, Vector3.zero, new Vector3(0.36f, 0.008f, 0.36f),
                                Mat("Target_BlueRing", new Color(0.7f, 0.9f, 1f), new Color(0.3f, 0.5f, 0.9f)), false);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                return look.gameObject;
            }

            if (kind == TargetKind.Explosive)
            {
                // 爆弾の的: 黒くて丸い爆弾 + 導火線 + 火花 + 赤い帯
                Prim(PrimitiveType.Sphere, "BombBody", look, new Vector3(0f, TargetDiscY, 0f), Vector3.one * 0.22f,
                     Mat("Explosive_Body", new Color(0.08f, 0.08f, 0.1f)));
                var band = Prim(PrimitiveType.Cylinder, "Band", look, new Vector3(0f, TargetDiscY, 0f), new Vector3(0.226f, 0.02f, 0.226f),
                     Mat("Explosive_Band", new Color(0.9f, 0.1f, 0.05f), new Color(0.8f, 0.05f, 0f)), false);
                band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Cylinder, "Cap", look, new Vector3(0f, TargetDiscY + 0.115f, 0f), new Vector3(0.06f, 0.02f, 0.06f),
                     Mat("Gun_Metal", new Color(0.18f, 0.18f, 0.2f)), false);
                var fuse = Prim(PrimitiveType.Cylinder, "Fuse", look, new Vector3(0.02f, TargetDiscY + 0.16f, 0f), new Vector3(0.012f, 0.04f, 0.012f),
                     Mat("Rope", new Color(0.85f, 0.8f, 0.65f)), false);
                fuse.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
                Prim(PrimitiveType.Sphere, "Spark", look, new Vector3(0.04f, TargetDiscY + 0.2f, 0f), Vector3.one * 0.04f,
                     Mat("Explosive_Spark", new Color(1f, 0.85f, 0.3f), new Color(2f, 1.4f, 0.3f)), false);
                return look.gameObject;
            }

            if (kind == TargetKind.Sniper)
            {
                // スナイパーの的: 黒い板に赤い照準 (丸 + 十字) と「SNIPER」の文字
                var c = new Vector3(0f, TargetDiscY, 0f);
                Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.28f, 0.28f, 0.04f), Mat("Sniper_Plate", new Color(0.08f, 0.08f, 0.1f)));
                var red = Mat("Sniper_Red", new Color(1f, 0.15f, 0.15f), new Color(1.6f, 0.1f, 0.1f));
                var ringOuter = Prim(PrimitiveType.Cylinder, "Scope", look, c + new Vector3(0f, 0.02f, -0.022f), new Vector3(0.17f, 0.002f, 0.17f), red, false);
                ringOuter.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var ringInner = Prim(PrimitiveType.Cylinder, "ScopeHole", look, c + new Vector3(0f, 0.02f, -0.024f), new Vector3(0.13f, 0.002f, 0.13f),
                                     Mat("Sniper_Plate", new Color(0.08f, 0.08f, 0.1f)), false);
                ringInner.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Cube, "CrossH", look, c + new Vector3(0f, 0.02f, -0.026f), new Vector3(0.2f, 0.012f, 0.002f), red, false);
                Prim(PrimitiveType.Cube, "CrossV", look, c + new Vector3(0f, 0.02f, -0.026f), new Vector3(0.012f, 0.2f, 0.002f), red, false);
                MakeText(look, "Label", "SNIPER", c + new Vector3(0f, -0.105f, -0.025f), 0.05f, Color.white);
                return look.gameObject;
            }

            if (kind == TargetKind.MachineGunChallenge)
            {
                // マシンガンチャレンジの的: 紺の板に小さな金の的がずらりと並び、「MG CHALLENGE」の文字
                var c = new Vector3(0f, TargetDiscY, 0f);
                Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.28f, 0.04f),
                     Mat("MgChallenge_Plate", new Color(0.08f, 0.14f, 0.4f), new Color(0.03f, 0.08f, 0.3f)));
                var dot = Mat("MgChallenge_Gold", new Color(1f, 0.8f, 0.15f), new Color(1.2f, 0.85f, 0.1f));
                for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 4; column++)
                    {
                        var d = Prim(PrimitiveType.Cylinder, "Dot", look, c + new Vector3(-0.09f + column * 0.06f + (row % 2) * 0.015f, 0.095f - row * 0.05f, -0.022f),
                                     new Vector3(0.035f, 0.002f, 0.035f), dot, false);
                        d.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    }
                MakeText(look, "Label", "MG CHALLENGE", c + new Vector3(0f, -0.09f, -0.025f), 0.04f, Color.white);
                return look.gameObject;
            }

            // 武器の的: 円柱のカプセル (横はガラス、上下は武器ごとの色の鉄のふたをリベットで留めてある) の中で、
            // 手に持つ武器を小さくしたフィギュアがぐるぐる回る
            (string label, Color col, WeaponType weapon) = kind switch
            {
                TargetKind.MachineGun => ("MACHINE GUN", new Color(0.25f, 0.5f, 1f), WeaponType.MachineGun),
                TargetKind.Shotgun => ("SHOTGUN", new Color(0.25f, 0.85f, 0.35f), WeaponType.Shotgun),
                _ => ("BOMB", new Color(1f, 0.5f, 0.1f), WeaponType.Bomb)
            };
            WeaponCapsule(look, kind, col, label, weapon);
            return look.gameObject;
        }

        const float CapsuleRadius = 0.1f;   // ガラスの半径
        const float CapsuleHeight = 0.24f;  // ガラスの高さ
        const float LidHeight = 0.03f;

        void WeaponCapsule(Transform look, TargetKind kind, Color col, string label, WeaponType weapon)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            var lid = Mat($"Capsule_Lid_{kind}", col * 0.8f, col * 0.25f, false, 0.75f);
            var steel = Mat("Capsule_Steel", new Color(0.62f, 0.64f, 0.68f), null, false, 0.85f);
            var rivet = Mat("Capsule_Rivet", new Color(0.82f, 0.83f, 0.86f), null, false, 0.9f);

            // ガラス (当たり判定はこれ) と、光の映り込みの筋
            Prim(PrimitiveType.Cylinder, "Glass", look, c, new Vector3(CapsuleRadius * 2f, CapsuleHeight / 2f, CapsuleRadius * 2f),
                 TransparentMat("Capsule_Glass", new Color(0.75f, 0.9f, 1f, 0.16f)));
            foreach (var (angle, width) in new[] { (-35f, 0.012f), (-20f, 0.005f) })
            {
                var glint = Group(look, "Glint");
                glint.localPosition = c;
                glint.localRotation = Quaternion.Euler(0f, angle, 0f);
                Prim(PrimitiveType.Cube, "Shine", glint, new Vector3(0f, 0f, -CapsuleRadius - 0.001f), new Vector3(width, CapsuleHeight * 0.85f, 0.002f),
                     TransparentMat("Capsule_Shine", new Color(1f, 1f, 1f, 0.45f)), false);
            }

            // 上と下のふた: 色付きの鉄の円盤と、ガラスを押さえる銀の帯、まわりにぐるりとリベット
            foreach (float s in new[] { -1f, 1f })
            {
                float y = s * (CapsuleHeight / 2f + LidHeight / 2f);
                Prim(PrimitiveType.Cylinder, "Lid", look, c + Vector3.up * y, new Vector3(CapsuleRadius * 2f + 0.03f, LidHeight / 2f, CapsuleRadius * 2f + 0.03f), lid, false);
                Prim(PrimitiveType.Cylinder, "LidCap", look, c + Vector3.up * (y + s * LidHeight * 0.6f), new Vector3(CapsuleRadius * 1.4f, 0.004f, CapsuleRadius * 1.4f), steel, false);
                Prim(PrimitiveType.Cylinder, "Band", look, c + Vector3.up * (s * (CapsuleHeight / 2f - 0.004f)), new Vector3(CapsuleRadius * 2f + 0.008f, 0.005f, CapsuleRadius * 2f + 0.008f), steel, false);
                const int Rivets = 12;
                for (int i = 0; i < Rivets; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI * 2f / Rivets;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Prim(PrimitiveType.Sphere, "Rivet", look, c + Vector3.up * y + dir * (CapsuleRadius + 0.015f), Vector3.one * 0.009f, rivet, false);
                }
            }
            // 下のふたの前に武器の名前
            MakeText(look, "Label", label, c + new Vector3(0f, -(CapsuleHeight / 2f + LidHeight / 2f), -(CapsuleRadius + 0.0155f)), 0.018f, Color.white);

            // 中で回る武器のフィギュア (手に持つ武器の見た目を小さくしたもの)
            var spin = Group(look, "Figure");
            spin.localPosition = c;
            var rot = spin.gameObject.AddComponent<Rotator>();
            rot.axis = Vector3.up;
            rot.speed = 100f;
            Prim(PrimitiveType.Cylinder, "Stand", look, c + Vector3.down * (CapsuleHeight / 2f - 0.012f), new Vector3(0.06f, 0.008f, 0.06f), steel, false); // 台座
            var model = builtGun != null && (int)weapon < builtGun.models.Length ? builtGun.models[(int)weapon] : null;
            if (model == null) return;
            var figure = Instantiate(model, spin, false);
            figure.name = "Figure_" + weapon;
            figure.SetActive(true);
            figure.transform.localPosition = Vector3.zero;
            figure.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // 横向きに置いて、回ると形がよく分かるように
            figure.transform.localScale = Vector3.one;
            foreach (var col2 in figure.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col2);
            // カプセルの中に収まる大きさにして、真ん中に置く (生成中は非表示なので Renderer.bounds は使えない。メッシュの大きさから計算する)
            var bounds = new Bounds();
            bool any = false;
            foreach (var mf in figure.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !ActiveWithin(mf.transform, figure.transform)) continue; // 消えているマズルフラッシュなどは数えない
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = spin.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            if (!any) return;
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float k = 0.17f / Mathf.Max(0.01f, size);
            figure.transform.localScale = Vector3.one * k;
            var center = bounds.center; // 縮める前の中心 (spin のローカル)
            figure.transform.localPosition = -center * k;
        }

        // t から root までの間が全部表示になっているか
        static bool ActiveWithin(Transform t, Transform root)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (!p.gameObject.activeSelf) return false;
                if (p == root) return true;
            }
            return true;
        }


        void BuildUfo()
        {
            var go = new GameObject("UfoFlyer");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            var root = Group(go.transform, "UFO");
            root.localPosition = new Vector3(0f, 6.5f, 2f); // 編集中に見た目を確認できる位置 (再生時は UfoFlyer が動かす)
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Ufo;
            target.points = 1000;
            target.manualControl = true;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;

            // 円盤 (当たり判定はこれ) + ドーム + 回るライト
            var saucer = Prim(PrimitiveType.Sphere, "Saucer", pivot, Vector3.zero, new Vector3(1.3f, 0.28f, 1.3f), Mat("Ufo_Body", new Color(0.7f, 0.72f, 0.8f)));
            saucer.GetComponent<SphereCollider>().radius = 0.7f; // 見た目より少し大きめにして遠くでも当てやすく
            Prim(PrimitiveType.Sphere, "Dome", pivot, new Vector3(0f, 0.12f, 0f), new Vector3(0.55f, 0.4f, 0.55f),
                 Mat("Ufo_Dome", new Color(0.4f, 0.9f, 1f), new Color(0.2f, 0.6f, 0.8f)), false);
            var lights = Group(pivot, "Lights");
            var yellow = Mat("Ufo_LightA", new Color(1f, 0.9f, 0.3f), new Color(1.5f, 1.2f, 0.2f));
            var magenta = Mat("Ufo_LightB", new Color(1f, 0.3f, 0.9f), new Color(1.4f, 0.2f, 1.2f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Prim(PrimitiveType.Sphere, "Light", lights, new Vector3(Mathf.Cos(a), -0.03f, Mathf.Sin(a)) * 0.6f, Vector3.one * 0.1f, i % 2 == 0 ? yellow : magenta, false);
            }
            target.spinner = lights;
            target.spinSpeed = 120f;

            // うなり音: UFO の位置から聞こえる立体音響
            var hum = root.gameObject.AddComponent<AudioSource>();
            hum.loop = true;
            hum.playOnAwake = false;
            hum.spatialBlend = 1f;
            hum.rolloffMode = AudioRolloffMode.Linear;
            hum.minDistance = 4f;
            hum.maxDistance = 45f;
            hum.volume = 0.7f;
            hum.dopplerLevel = 0.5f;

            var flyer = go.AddComponent<UfoFlyer>();
            flyer.ufo = target;
            target.SetAvailable(false); // 飛ぶ時だけ見える (シーンに生成した時も隠しておく)
            flyer.hum = hum;
            go.SetActive(true);
        }
    }
}
