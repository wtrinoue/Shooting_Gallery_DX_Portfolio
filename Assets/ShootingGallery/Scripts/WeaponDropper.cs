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

        [Header("スナイパーの的 (撃つとスナイパーチャレンジ。同時に1つまで。屋台の拡張に巻き込まれない時だけ出る)")]
        [Tooltip("スナイパーの的が消えて (撃たれて / 時間切れで) から次に出るまでの間隔 (秒)")]
        public Vector2 sniperGap = new Vector2(6f, 12f);
        [Tooltip("フィーバー中の間隔 (秒)。フィーバー中はメイン以外の屋台に出る")]
        public Vector2 sniperFeverGap = new Vector2(3f, 6f);
        [Tooltip("ゲーム開始から最初のスナイパーの的が出るまでの秒数")]
        public Vector2 sniperStartDelay = new Vector2(4f, 7f);
        [Tooltip("フィーバーが始まってから最初のスナイパーの的が出るまでの秒数 (屋台がせり上がり終わってから)")]
        public Vector2 sniperFeverStartDelay = new Vector2(1.5f, 3f);
        [Tooltip("撃たれずにこの秒数たったら引っ込む")]
        public float sniperLifetime = 5f;
        public int sniperTargetPoints = 100;
        [Tooltip("出す屋台を決めてから、その屋台の的が起き上がるのを待つ最長の秒数 (過ぎたら選び直す)")]
        public float sniperReserveTimeout = 4f;
        float nextSniperTime;

        [Header("マシンガンチャレンジの的 (撃つとマシンガンチャレンジ。出し方はスナイパーの的と同じで、チャレンジの的は全部合わせて同時に1つまで)")]
        [Tooltip("マシンガンチャレンジが終わってから、次のマシンガンチャレンジの的が出るまでの間隔 (秒)")]
        public Vector2 mgChallengeGap = new Vector2(10f, 16f);
        [Tooltip("フィーバー中の間隔 (秒)")]
        public Vector2 mgChallengeFeverGap = new Vector2(5f, 9f);
        [Tooltip("ゲーム開始から最初のマシンガンチャレンジの的が出るまでの秒数")]
        public Vector2 mgChallengeStartDelay = new Vector2(12f, 18f);
        [Tooltip("フィーバーが始まってから最初のマシンガンチャレンジの的が出るまでの秒数 (屋台がせり上がり終わってから)")]
        public Vector2 mgChallengeFeverStartDelay = new Vector2(4f, 7f);
        [Tooltip("撃たれずにこの秒数たったら引っ込む")]
        public float mgChallengeLifetime = 5f;
        public int mgChallengeTargetPoints = 100;
        [Tooltip("チャレンジが終わってから、もう一方のチャレンジの的が出るまでの最短の秒数 (続けて出すぎないように)")]
        public Vector2 challengeSeparation = new Vector2(3f, 5f);
        [Tooltip("ラウンドの残り時間がこの秒数になってもまだ1回も出ていないチャレンジの的は、すぐに出す (スナイパーもマシンガンも1ラウンドに必ず1回は出るように)")]
        public float guaranteeByTimeLeft = 55f;
        float nextMgChallengeTime;
        float separatedUntil;              // チャレンジが終わってから、次のチャレンジの的を出してよくなる時刻
        bool sniperShown, mgChallengeShown; // このラウンドでもう出たか

        bool wasPlaying, wasFever, challengeWasBusy;
        TargetKind lastChallengeKind = TargetKind.Sniper;
        // チャレンジの的 (スナイパー / マシンガン) を出す予定の屋台 / 的 (どの屋台にも等しく出るよう、先に屋台をランダムに決めておく)
        TargetKind reservedKind;
        Stall reservedStall;
        Target reservedTarget;
        float reservedAt;

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
                else if (pair.Key.IsUp && Time.time - pair.Value.since > LifetimeOf(pair.Value.kind))
                    pair.Key.Lower();
            }
            foreach (var t in toRestore) Restore(t);

            UpdateChallengeReservation();
        }

        /// <summary>
        /// チャレンジの的 (スナイパー / マシンガン) を出す時が来たら、今出ている屋台からランダムに1軒選び、その屋台の立っている的を1つ
        /// いったん倒して起き上がらせる (起き上がる時に OnTargetRising で化ける)。
        /// フィーバー外はメイン屋台、フィーバー中はメイン以外の屋台 (フィーバー屋台) から選ぶ。
        /// 倒せる的がない屋台 (もぐら叩きなど) は、その屋台の的が次に起き上がるのを待つ。
        /// チャレンジの的は種類を合わせて同時に1つまで。両方の時間が来ていたら、先に時間が来た方を出す。
        /// </summary>
        void UpdateChallengeReservation()
        {
            var gm = GameManager.I;
            bool playing = gm != null && gm.State == GameState.Playing;
            bool fever = playing && gm.IsFever;
            bool sniperBusy = playing && (gm.SniperActive || Count(TargetKind.Sniper) > 0);
            bool mgBusy = playing && (gm.MachineGunChallengeActive || Count(TargetKind.MachineGunChallenge) > 0);
            bool busy = sniperBusy || mgBusy;
            if (sniperBusy) lastChallengeKind = TargetKind.Sniper;
            else if (mgBusy) lastChallengeKind = TargetKind.MachineGunChallenge;

            if (playing && !wasPlaying) // ゲーム開始直後には出さない
            {
                nextSniperTime = Time.time + Random.Range(sniperStartDelay.x, sniperStartDelay.y);
                nextMgChallengeTime = Time.time + Random.Range(mgChallengeStartDelay.x, mgChallengeStartDelay.y);
                sniperShown = mgChallengeShown = false;
                separatedUntil = 0f;
            }
            else if (fever && !wasFever) // フィーバーが始まったら早めに出す (屋台がせり上がる 1.2 秒も待つ)
            {
                nextSniperTime = Mathf.Min(nextSniperTime, Time.time + gm.stallMoveDuration + Random.Range(sniperFeverStartDelay.x, sniperFeverStartDelay.y));
                nextMgChallengeTime = Mathf.Min(nextMgChallengeTime, Time.time + gm.stallMoveDuration + Random.Range(mgChallengeFeverStartDelay.x, mgChallengeFeverStartDelay.y));
            }
            if (challengeWasBusy && !busy) // 前のチャレンジの的 / チャレンジが終わった所から間隔を数える
            {
                float separated = Time.time + Random.Range(challengeSeparation.x, challengeSeparation.y);
                separatedUntil = separated;
                if (lastChallengeKind == TargetKind.Sniper)
                {
                    var gap = fever ? sniperFeverGap : sniperGap;
                    nextSniperTime = Time.time + Random.Range(gap.x, gap.y);
                    nextMgChallengeTime = Mathf.Max(nextMgChallengeTime, separated);
                }
                else
                {
                    var gap = fever ? mgChallengeFeverGap : mgChallengeGap;
                    nextMgChallengeTime = Time.time + Random.Range(gap.x, gap.y);
                    nextSniperTime = Mathf.Max(nextSniperTime, separated);
                }
            }
            wasPlaying = playing;
            wasFever = fever;
            challengeWasBusy = busy;

            if (!playing || busy)
            {
                ClearChallengeReservation();
                return;
            }
            if (reservedStall != null)
            {
                if (Time.time - reservedAt > sniperReserveTimeout || !reservedStall.IsSettled) ClearChallengeReservation();
                return;
            }
            // 1回も出ていないチャレンジは、ラウンドの残りが少なくなったら (前のチャレンジから少し間をあけて) すぐに出す
            if (gm.TimeLeft <= guaranteeByTimeLeft)
            {
                float soon = Mathf.Max(Time.time, separatedUntil);
                if (!sniperShown) nextSniperTime = Mathf.Min(nextSniperTime, soon);
                if (!mgChallengeShown) nextMgChallengeTime = Mathf.Min(nextMgChallengeTime, soon);
            }
            bool sniperDue = Time.time >= nextSniperTime;
            bool mgDue = Time.time >= nextMgChallengeTime;
            if (!sniperDue && !mgDue) return;
            // 両方の時間が来ていたら、まだ出ていない方 → 先に時間が来た方 の順で選ぶ
            TargetKind kind;
            if (sniperDue && mgDue && sniperShown != mgChallengeShown)
                kind = sniperShown ? TargetKind.MachineGunChallenge : TargetKind.Sniper;
            else
                kind = sniperDue && (!mgDue || nextSniperTime <= nextMgChallengeTime) ? TargetKind.Sniper : TargetKind.MachineGunChallenge;

            var stalls = gm.ShownStalls();
            if (fever) stalls.Remove(gm.mainStall); // フィーバー中はまわりの屋台に出す
            if (stalls.Count == 0) return;
            var stall = stalls[Random.Range(0, stalls.Count)];

            var candidates = new List<Target>();
            foreach (var t in stall.targets)
                if (t != null && t.kind == TargetKind.Normal && t.face != null && t.IsUp && !t.manualControl && !active.ContainsKey(t))
                    candidates.Add(t);
            Target pick = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
            if (!CanOfferChallenge(kind, pick != null ? pick : stall.targets.Length > 0 ? stall.targets[0] : null, LifetimeOf(kind) + 1f))
            {
                // 今は出せない (拡張が近い等)。少ししてから選び直す
                if (kind == TargetKind.Sniper) nextSniperTime = Time.time + 1f;
                else nextMgChallengeTime = Time.time + 1f;
                return;
            }

            reservedKind = kind;
            reservedStall = stall;
            reservedAt = Time.time;
            if (pick != null && pick.Reappear(0.4f)) reservedTarget = pick;
        }

        void ClearChallengeReservation()
        {
            reservedStall = null;
            reservedTarget = null;
        }

        bool CanOfferChallenge(TargetKind kind, Target target, float lifetime)
        {
            var gm = GameManager.I;
            return kind == TargetKind.Sniper ? gm.CanOfferSniper(target, lifetime) : gm.CanOfferMachineGunChallenge(target, lifetime);
        }

        void OnTargetRising(Target target)
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;
            if (target.kind != TargetKind.Normal || target.face == null || active.ContainsKey(target)) return;

            // チャレンジの的 (スナイパー / マシンガン): 予約した的 (または予約した屋台で起き上がった的) だけが化ける。
            // 拡張・時間切れ・フィーバー終了に巻き込まれないかは GameManager が判断
            if (reservedStall != null && (target == reservedTarget || (reservedTarget == null && target.GetComponentInParent<Stall>() == reservedStall)))
            {
                var kind = reservedKind;
                ClearChallengeReservation();
                if (Count(TargetKind.Sniper) == 0 && Count(TargetKind.MachineGunChallenge) == 0 && CanOfferChallenge(kind, target, LifetimeOf(kind))
                    && ApplyDisguise(target, kind, kind == TargetKind.Sniper ? sniperTargetPoints : mgChallengeTargetPoints))
                {
                    if (kind == TargetKind.Sniper) sniperShown = true;
                    else mgChallengeShown = true;
                    return;
                }
            }

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

            // 武器の的 (マシンガンチャレンジ中は持ち替えないので出さない)
            if (gm.MachineGunChallengeActive) return;
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

        float LifetimeOf(TargetKind kind) => kind switch
        {
            TargetKind.BlueOrb => blueOrbLifetime,
            TargetKind.Sniper => sniperLifetime,
            TargetKind.MachineGunChallenge => mgChallengeLifetime,
            _ => lifetime
        };

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
