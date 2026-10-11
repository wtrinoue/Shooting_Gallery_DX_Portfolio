using UnityEngine;

namespace ShootingGallery
{
    public enum WeaponType { Handgun = 0, MachineGun = 1, Shotgun = 2, Bomb = 3, Sniper = 4 }

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
    }

    public static class Weapons
    {
        public static WeaponDef Get(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.MachineGun:
                    return new WeaponDef { displayName = "MACHINE GUN", fireInterval = 0.08f, automatic = true, pellets = 1, spreadDeg = 1.5f, range = 60f };
                case WeaponType.Shotgun:
                    return new WeaponDef { displayName = "SHOTGUN", fireInterval = 0.6f, automatic = false, pellets = 14, spreadDeg = 12f, range = 40f };
                case WeaponType.Sniper: // スナイパーチャレンジ専用。1発だけ撃てる (撃ったらチャレンジ終了)
                    return new WeaponDef { displayName = "SNIPER", fireInterval = 0.5f, automatic = false, pellets = 1, spreadDeg = 0f, range = 120f };
                case WeaponType.Bomb:
                    return new WeaponDef { displayName = "BOMB", fireInterval = 0.7f, automatic = false, pellets = 1, projectile = true, projectileSpeed = 11f, blastRadius = 1.0f };
                default:
                    return new WeaponDef { displayName = "HANDGUN", fireInterval = 0.2f, automatic = false, pellets = 1, spreadDeg = 0f, range = 60f };
            }
        }
    }
}
