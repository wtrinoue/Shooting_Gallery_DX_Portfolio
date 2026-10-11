using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 普通の点数の的が起き上がる時に、ときどき武器の的 (MG / SG / BOMB) に化けさせる。
    /// 的そのものが化けるので、コンベアーなら流れ、観覧車なら回り、もぐら叩きなら穴から出る…と
    /// 屋台ごとの動きにそのまま従う。倒されるか一定時間たつと元の的に戻る。
    /// 出やすさは マシンガン > ショットガン > 爆弾。
    /// </summary>
    public class WeaponDropper : MonoBehaviour
    {
        [Tooltip("武器の的の見た目 (使い回す)。TargetKind が名前の最後に付いた子 (Look_MachineGun など)")]
        public GameObject[] looks;

        [Header("出やすさ (合計が 1 でなくてもよい)")]
        public float machineGunWeight = 6f;
        public float shotgunWeight = 3f;
        public float bombWeight = 1f;

        [Header("Timing")]
        [Tooltip("武器の的が出てから次に出るまでの間隔 (秒)")]
        public Vector2 interval = new Vector2(4f, 7f);
        public Vector2 feverInterval = new Vector2(2f, 4f);
        [Tooltip("起き上がった的が化ける確率 (間隔が空いている時だけ)")]
        [Range(0f, 1f)] public float chance = 0.35f;
        [Tooltip("撃たれずにこの秒数たったら引っ込む")]
        public float lifetime = 6f;
        public int maxActive = 2;
        public int weaponPoints = 100;

        [Header("爆弾の的 (当てると爆発して周りの的を倒す。武器の的とは別枠で出る)")]
        public Vector2 explosiveInterval = new Vector2(5f, 9f);
        public Vector2 explosiveFeverInterval = new Vector2(2.5f, 5f);
        [Range(0f, 1f)] public float explosiveChance = 0.3f;
        public int maxExplosives = 1;
        public int explosivePoints = 50;

        [Header("青い玉 (フィーバー中にメイン以外の屋台に出る。撃つとボーナスステージ)")]
        [Range(0f, 1f)] public float blueOrbChance = 0.12f;
        public int blueOrbPoints = 300;
        public float blueOrbLifetime = 8f;
        float nextBlueOrbTime;

        class Disguise
        {
            public GameObject look;
            public TargetKind kind;
            public int originalPoints;
            public float since;
        }

        readonly Dictionary<Target, Disguise> active = new Dictionary<Target, Disguise>();
        readonly List<Target> toRestore = new List<Target>();
        float nextAllowedTime;
        float nextExplosiveTime;

        void OnEnable() => Target.Rising += OnTargetRising;
        void OnDisable() => Target.Rising -= OnTargetRising;

        void Start()
        {
            foreach (var look in looks) look.SetActive(false);
        }

        void Update()
        {
            // 消えた的は元に戻す / 長く出ている的は引っ込める
            toRestore.Clear();
            foreach (var pair in active)
            {
                if (!pair.Key.IsVisible) toRestore.Add(pair.Key);
                else if (pair.Key.IsUp && Time.time - pair.Value.since > (pair.Value.kind == TargetKind.BlueOrb ? blueOrbLifetime : lifetime))
                    pair.Key.Lower();
            }
            foreach (var t in toRestore) Restore(t);
        }

        void OnTargetRising(Target target)
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;
            if (target.kind != TargetKind.Normal || target.face == null || active.ContainsKey(target)) return;

            // 青い玉: フィーバー中だけ、メイン以外の屋台に1つまで (撃たれずに消えたらまた出る)
            if (gm.IsFever && !gm.BonusUsedThisFever && (gm.bonusStage == null || !gm.bonusStage.IsBusy)
                && Count(TargetKind.BlueOrb) == 0 && Time.time >= nextBlueOrbTime
                && target.GetComponentInParent<Stall>() != gm.mainStall && Random.value < blueOrbChance)
            {
                if (ApplyDisguise(target, TargetKind.BlueOrb, blueOrbPoints)) nextBlueOrbTime = Time.time + 3f;
                return;
            }

            // 爆弾の的
            if (Time.time >= nextExplosiveTime && Count(TargetKind.Explosive) < maxExplosives && Random.value < explosiveChance)
            {
                if (ApplyDisguise(target, TargetKind.Explosive, explosivePoints))
                {
                    var r = gm.IsFever ? explosiveFeverInterval : explosiveInterval;
                    nextExplosiveTime = Time.time + Random.Range(r.x, r.y);
                }
                return;
            }

            // 武器の的
            if (Time.time < nextAllowedTime || active.Count - Count(TargetKind.Explosive) >= maxActive) return;
            if (Random.value > chance) return;
            if (ApplyDisguise(target, PickKind(), weaponPoints))
            {
                var range = gm.IsFever ? feverInterval : interval;
                nextAllowedTime = Time.time + Random.Range(range.x, range.y);
            }
        }

        // 的の「顔」を武器の箱 (や爆弾) に付け替える
        bool ApplyDisguise(Target target, TargetKind kind, int points)
        {
            var look = TakeLook(kind);
            if (look == null) return false;

            target.face.SetActive(false);
            look.transform.SetParent(target.visual, false);
            look.transform.localPosition = target.face.transform.localPosition;
            look.transform.localRotation = Quaternion.identity;
            look.SetActive(true);
            active[target] = new Disguise { look = look, kind = kind, originalPoints = target.points, since = Time.time };
            target.kind = kind;
            target.points = points;
            return true;
        }

        int Count(TargetKind kind)
        {
            int n = 0;
            foreach (var d in active.Values) if (d.kind == kind) n++;
            return n;
        }

        void Restore(Target target)
        {
            var d = active[target];
            active.Remove(target);
            target.kind = TargetKind.Normal;
            target.points = d.originalPoints;
            if (target.face != null) target.face.SetActive(true);
            d.look.SetActive(false);
            d.look.transform.SetParent(transform, false);
        }

        GameObject TakeLook(TargetKind kind)
        {
            string suffix = "_" + kind;
            foreach (var look in looks)
                if (!look.activeSelf && look.transform.parent == transform && look.name.EndsWith(suffix)) return look;
            return null;
        }

        TargetKind PickKind()
        {
            float r = Random.value * (machineGunWeight + shotgunWeight + bombWeight);
            if (r < machineGunWeight) return TargetKind.MachineGun;
            if (r < machineGunWeight + shotgunWeight) return TargetKind.Shotgun;
            return TargetKind.Bomb;
        }
    }
}
