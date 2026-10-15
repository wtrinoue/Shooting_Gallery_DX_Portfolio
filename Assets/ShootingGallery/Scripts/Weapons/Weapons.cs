using UnityEngine;

namespace ShootingGallery
{
    public enum WeaponType { Handgun = 0, MachineGun = 1, Shotgun = 2, Bomb = 3, Sniper = 4, Bazooka = 5, Shuriken = 6, FireBall = 7, Dart = 8, Revolver = 9, Missile = 10, Flashlight = 11, Pickaxe = 12, RingToss = 13, CorkGun = 14, Poi = 15, Coin = 16, Torpedo = 17, Trumpet = 18, Imagination = 19, AirMachineGun = 20 }

    public struct WeaponDef
    {
        public string displayName;
        public float fireInterval;   // 次弾までの秒数
        public bool automatic;       // トリガー長押しで連射するか
        public int pellets;          // 1回の発射で飛ぶ弾数
        public float spreadDeg;      // 弾のばらつき(度)
        public float range;

        public bool projectile;      // true なら弾を飛ばす (爆弾)
        public float projectileSpeed;
        public float blastRadius;
        public float projectileGravity; // 弾にかかる重力 (小さいほどまっすぐ飛ぶ)

        public int magazine;         // 続けて撃てる弾の数 (0 なら弾切れなし)。撃ちきると reloadTime 秒の弾込め
        public float reloadTime;
        public bool reloadGauge;     // true なら銃の上のゲージで弾込めの進み具合を見せる (撃つと空になり、満たされたらまた撃てる)
    }

    public static class Weapons
    {
        public static WeaponDef Get(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.MachineGun:
                    return new WeaponDef { displayName = "MACHINE GUN", fireInterval = 0.08f, automatic = true, pellets = 1, spreadDeg = 1.5f, range = 60f };
                case WeaponType.AirMachineGun: // 空中ステージ専用。昔の飛行機の水冷式の機関銃 (性能はマシンガンと同じ)
                    return new WeaponDef { displayName = "MACHINE GUN", fireInterval = 0.08f, automatic = true, pellets = 1, spreadDeg = 1.5f, range = 60f };
                case WeaponType.Shotgun:
                    return new WeaponDef { displayName = "SHOTGUN", fireInterval = 0.6f, automatic = false, pellets = 14, spreadDeg = 12f, range = 40f };
                case WeaponType.Sniper: // スナイパーチャレンジ専用。3発まで撃てる (当たったらチャレンジ終了)。連射で外さないように、次の1発まで間をあける
                    return new WeaponDef { displayName = "SNIPER", fireInterval = 1.5f, automatic = false, pellets = 1, spreadDeg = 0f, range = 120f };
                case WeaponType.Bomb:
                    return new WeaponDef { displayName = "BOMB", fireInterval = 0.7f, automatic = false, pellets = 1, projectile = true, projectileSpeed = 11f, blastRadius = 1.0f, projectileGravity = 5f };
                case WeaponType.Bazooka: // 海戦モード専用。見た目は木の台に載った大砲 (帆船の大砲)。砲弾が速くほぼまっすぐ飛び、当たった所で大きく爆発する
                    return new WeaponDef { displayName = "CANNON", fireInterval = 0.45f, automatic = false, pellets = 1, projectile = true, projectileSpeed = 32f, blastRadius = 1.6f, projectileGravity = 1f };
                case WeaponType.Shuriken: // 忍者モード専用。手裏剣を投げる (まっすぐ速く飛び、当たった所に刺さる)。白いレーザーで狙う
                    return new WeaponDef { displayName = "SHURIKEN", fireInterval = 0.22f, automatic = false, pellets = 1, range = 30f, projectileSpeed = 22f };
                case WeaponType.FireBall: // 怪獣モード専用。怪獣の口から炎の玉を吐く (少し山なりに飛び、当たった所で燃え上がる)
                    return new WeaponDef { displayName = "FIRE BREATH", fireInterval = 0.35f, automatic = true, pellets = 1, projectile = true, projectileSpeed = 18f, blastRadius = 1.3f, projectileGravity = 2f };
                case WeaponType.Dart: // カジノモード専用。ダーツを投げる (まっすぐ飛び、当たった所に刺さる)。白いレーザーで狙う
                    return new WeaponDef { displayName = "DARTS", fireInterval = 0.25f, automatic = false, pellets = 1, range = 30f, projectileSpeed = 20f };
                case WeaponType.Revolver: // 西部劇モード専用。6発撃つと弾込めの時間が入る
                    return new WeaponDef { displayName = "REVOLVER", fireInterval = 0.22f, automatic = false, pellets = 1, spreadDeg = 0f, range = 60f, magazine = 6, reloadTime = 1.6f };
                case WeaponType.Missile: // 宇宙モード専用。ミサイルが煙を引いてまっすぐ速く飛び、当たった所で爆発する
                    return new WeaponDef { displayName = "MISSILE", fireInterval = 0.4f, automatic = false, pellets = 1, projectile = true, projectileSpeed = 26f, blastRadius = 1.5f, projectileGravity = 0f };
                case WeaponType.Torpedo: // 潜水艦モード専用。プロペラで少しゆっくり進む魚雷。泡を引いてまっすぐ進み、当たった所で爆発する
                    return new WeaponDef { displayName = "TORPEDO", fireInterval = 0.5f, automatic = false, pellets = 1, projectile = true, projectileSpeed = 9f, blastRadius = 1.7f, projectileGravity = 0f };
                case WeaponType.Trumpet: // 音楽モード専用。音符を吹き出す (それなりの速さでまっすぐ飛び、しっぽに波打つ五線譜をなびかせる)。押しっぱなしで次々に
                    return new WeaponDef { displayName = "TRUMPET", fireInterval = 0.2f, automatic = true, pellets = 1, range = 40f, projectile = true, projectileSpeed = 15f, projectileGravity = 0f };
                case WeaponType.Imagination: // 美術館モード専用。虹色に光るイマジネーションの玉を放つ (まっすぐ飛ぶ)。作品に当たると、キャンバスでは筆、彫刻ではトンカチになる。押しっぱなしで次々に
                    return new WeaponDef { displayName = "IMAGINATION", fireInterval = 0.22f, automatic = true, pellets = 1, range = 40f, projectile = true, projectileSpeed = 16f, projectileGravity = 0f };
                case WeaponType.Pickaxe: // 発掘モード専用。つるはしを縦にくるくる回しながら投げる。押しっぱなしで次々に投げられる (マシンガンほど速くない。手裏剣と同じく、当たった所に刺さる)。白いレーザーで狙う
                    return new WeaponDef { displayName = "PICKAXE", fireInterval = 0.15f, automatic = true, pellets = 1, range = 35f, projectileSpeed = 19f };
                case WeaponType.RingToss: // 夏祭りモード (輪投げの屋台)。銃を向けた所へ、いつも同じ形の丸い放物線で輪が飛ぶ (GunController.ArcVelocity。projectileSpeed は使わない)
                    return new WeaponDef { displayName = "RING TOSS", fireInterval = 0.45f, automatic = false, pellets = 1, range = 25f, projectile = true, projectileSpeed = 7.5f, projectileGravity = 6f };
                case WeaponType.CorkGun: // 夏祭りモード (射的の屋台)。コルクの弾がほぼまっすぐ飛ぶ。1発ごとに弾込め (ゲージがたまるまで撃てない)
                    return new WeaponDef { displayName = "CORK GUN", fireInterval = 0.2f, automatic = false, pellets = 1, range = 30f, projectile = true, projectileSpeed = 24f, projectileGravity = 1.5f, magazine = 1, reloadTime = 0.8f, reloadGauge = true };
                case WeaponType.Poi: // 夏祭りモード (金魚すくい)。ポイを向けた所へ輪投げと同じ放物線で投げ入れる。確率ですくえたり破れたりする。投げたら次のポイが来るまで待つ (ゲージ)
                    return new WeaponDef { displayName = "POI", fireInterval = 0.2f, automatic = false, pellets = 1, range = 20f, projectile = true, projectileSpeed = 6.5f, projectileGravity = 6f, magazine = 1, reloadTime = 1.4f, reloadGauge = true };
                case WeaponType.Coin: // 夏祭りモード (食べ物の屋台)。お金をまっすぐ投げて、当たったメニューの板の料理を買う (スコアがお金)。白いレーザーで狙う
                    return new WeaponDef { displayName = "COIN", fireInterval = 0.35f, automatic = false, pellets = 1, range = 25f, projectile = true, projectileSpeed = 16f, projectileGravity = 0f };
                case WeaponType.Flashlight: // 幽霊モード専用。トリガーを引いている間だけ光り、照らした的を倒す。電池が減り、使わないと回復する (GunController)
                    return new WeaponDef { displayName = "FLASHLIGHT", fireInterval = 0.1f, automatic = true, pellets = 1, range = 25f };
                default:
                    return new WeaponDef { displayName = "HANDGUN", fireInterval = 0.2f, automatic = false, pellets = 1, spreadDeg = 0f, range = 60f };
            }
        }
    }
}
