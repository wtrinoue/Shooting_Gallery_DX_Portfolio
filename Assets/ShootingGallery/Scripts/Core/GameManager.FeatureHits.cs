using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GameManager のうち、機能付きの的 (武器の的・フィーバー・青い玉・モードの的) が撃たれた時のまとめ役。
    /// 爆弾の爆発やショットガンの散弾では、1度にいくつもの的が倒れる。倒れた機能付きの的は、その場では何もせずに積んでおき (StackFeatureHit)、
    /// フレームの終わりにまとめて調べる (ProcessFeatureHits)。どの武器・どの倒し方でも、倒れた機能付きの的は必ず調べられる。
    /// 点数の的は積まない (今まで通り、その場で点数が入る)。
    /// 今働いている機能付きの的 (モード) を ActiveFeature に記録する。スナイパーチャレンジの間にマシンガンチャレンジの的が倒れたら
    /// (逆も、同じチャレンジの的も) なかったことにする。モードが終わったら、記録は消す
    /// </summary>
    public partial class GameManager
    {
        struct FeatureHit
        {
            public TargetKind kind;
            public Vector3 point;    // 的が撃たれた所 (武器・左手の銃が、ここから手元へ飛んでくる)
            public PendingMode mode; // モードの的の時の、始め方の情報
        }

        readonly List<FeatureHit> featureHits = new List<FeatureHit>();

        /// <summary>今働いている機能付きの的 (始めた / 始まるのを待っているモード)。何もなければ TargetKind.Normal</summary>
        public TargetKind ActiveFeature { get; private set; } = TargetKind.Normal;

        /// <summary>倒れた機能付きの的を積んでおく (撃たれた時の種類と場所を、その場で覚える)</summary>
        void StackFeatureHit(Target target, Vector3 point)
        {
            var h = new FeatureHit { kind = target.kind, point = point };
            if (IsModeKind(target.kind)) h.mode = MakePendingMode(target);
            featureHits.Add(h);
        }

        void LateUpdate()
        {
            ProcessFeatureHits();
            ForgetFinishedFeature();
            UpdateFeverAssembling();
        }

        // このフレームに倒れた機能付きの的を、倒れた順に調べる。同じ種類の働き (武器を持つ・フィーバー・ボーナス・モード) は1つだけ
        void ProcessFeatureHits()
        {
            if (featureHits.Count == 0) return;

            // 1. フィーバーが始まるかを先に調べる。スナイパー・マシンガンのチャレンジが先に始まっている (始まるのを待っている) か、
            //    このフレームで先に倒れていたら、フィーバーの的は取り消す
            bool challengeFirst = IsChallengeKind(ActiveFeature);
            FeatureHit? fever = null;
            foreach (var h in featureHits)
            {
                if (h.kind == TargetKind.Fever) { if (!challengeFirst) fever = h; break; }
                if (IsChallengeKind(h.kind) && AcceptMode(h.kind)) challengeFirst = true;
            }
            if (fever != null && State == GameState.Playing)
            {
                // フィーバーが始まる: 一緒に倒れたほかの特別な的は、全部不発 (点数だけ)。屋台がそろうまで、特別な的は出ない (FeverAssembling)
                featureHits.Clear();
                StartFever(fever.Value.point); // 左手の銃が、撃った的から飛んでくる
                return;
            }

            // 2. いつも通り、倒れた順に
            bool weaponDone = false, bonusDone = false;
            FeatureHit? mode = null; // 始めるモードの的 (別の世界の的は、チャレンジ・DELUXE の的より優先する)
            foreach (var h in featureHits)
            {
                if (State != GameState.Playing) break;
                switch (h.kind)
                {
                    case TargetKind.Bomb:
                    case TargetKind.MachineGun:
                    case TargetKind.Shotgun:
                        if (weaponDone) break;
                        weaponDone = true;
                        if (MachineGunChallengeActive) { Sfx.Play("hit"); break; } // マシンガンチャレンジ中は持ち替えない (点数だけ)
                        GiveWeaponFrom(h.kind, h.point);
                        break;
                    case TargetKind.Fever:
                        Sfx.Play("hit"); // (ここまで来たのは) チャレンジが先: フィーバーは始めない (延長もしない。点数だけ)
                        break;
                    case TargetKind.BlueOrb:
                        if (bonusDone || bonusStage == null || bonusStage.IsBusy) break;
                        bonusDone = true;
                        BonusUsedThisFever = true;
                        bonusStage.Open(); // しばらく、屋台ごとに目の前の床から「下を見て」の矢印が出る (UpdateBonusArrows)
                        Sfx.Play("bonus");
                        ShowMessage("BONUS CHANCE!\nLOOK DOWN!!", 2.5f, new Color(0.35f, 0.65f, 1f));
                        break;
                    default:
                        if (!IsModeKind(h.kind) || !AcceptMode(h.kind)) break;
                        if (mode == null || (IsWorldKind(h.kind) && !IsWorldKind(mode.Value.kind))) mode = h; // (同じ優先度なら、先に倒れた方)
                        break;
                }
            }
            featureHits.Clear();
            if (mode != null && State == GameState.Playing) RequestMode(mode.Value.mode); // 始めたモードは ActiveFeature に記録される
        }

        /// <summary>スナイパー・マシンガンのチャレンジの的か</summary>
        static bool IsChallengeKind(TargetKind kind) => kind == TargetKind.Sniper || kind == TargetKind.MachineGunChallenge;

        /// <summary>別の世界へ行くモードの的か (チャレンジ・DELUXE より優先する)</summary>
        static bool IsWorldKind(TargetKind kind) =>
            IsModeKind(kind) && kind != TargetKind.Sniper && kind != TargetKind.MachineGunChallenge && kind != TargetKind.Deluxe;

        // 武器の的: 撃った的から、銃が手元へ飛んでくる
        void GiveWeaponFrom(TargetKind kind, Vector3 point)
        {
            var (weapon, name, color) = kind switch
            {
                TargetKind.Bomb => (WeaponType.Bomb, "BOMB!", new Color(1f, 0.55f, 0.15f)),
                TargetKind.MachineGun => (WeaponType.MachineGun, "MACHINE GUN!", new Color(0.4f, 0.7f, 1f)),
                _ => (WeaponType.Shotgun, "SHOTGUN!", new Color(0.4f, 1f, 0.5f)),
            };
            player.GiveWeapon(weapon, specialWeaponDuration, point);
            Sfx.Play("pickup");
            ShowMessage(name, 1.5f, color);
        }

        // このモードの的を、今始めてよいか。スナイパー・マシンガンのチャレンジが働いている間は、もう片方 (と同じ方) のチャレンジの的はなかったことにする
        bool AcceptMode(TargetKind kind)
        {
            bool challenge = kind == TargetKind.Sniper || kind == TargetKind.MachineGunChallenge;
            bool challengeRunning = ActiveFeature == TargetKind.Sniper || ActiveFeature == TargetKind.MachineGunChallenge;
            return !(challenge && challengeRunning);
        }

        // 記録しているモードが終わった (始まるのを待っている間でもなく、もう動いていない): 記録を消す
        void ForgetFinishedFeature()
        {
            if (ActiveFeature == TargetKind.Normal || pendingModeStart != null) return;
            if (State != GameState.Playing || !IsFeatureRunning(ActiveFeature)) ActiveFeature = TargetKind.Normal;
        }

        bool IsFeatureRunning(TargetKind kind) => kind switch
        {
            TargetKind.Sniper => SniperActive,
            TargetKind.MachineGunChallenge => MachineGunChallengeActive,
            TargetKind.Deluxe => DeluxeActive,
            TargetKind.Naval => NavalActive,
            TargetKind.Sky => SkyActive,
            TargetKind.Ninja => NinjaActive,
            TargetKind.Kaiju => KaijuActive,
            TargetKind.Casino => CasinoActive,
            TargetKind.Western => WesternActive,
            TargetKind.Space => SpaceActive,
            TargetKind.Ghost => GhostActive,
            TargetKind.Mine => MineActive,
            TargetKind.Festival => FestivalActive,
            TargetKind.Submarine => SubmarineActive,
            TargetKind.Music => MusicActive,
            TargetKind.Museum => MuseumActive,
            TargetKind.Unknown => UnknownActive,
            _ => false
        };
    }
}
