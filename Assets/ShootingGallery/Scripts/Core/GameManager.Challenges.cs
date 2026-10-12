using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、スナイパーチャレンジとマシンガンチャレンジ (屋台を引っ込めて、その方向にチャレンジの的を出す)。</summary>
    public partial class GameManager
    {
        // ---- スナイパーチャレンジ ----

        /// <summary>
        /// この的をスナイパーの的に化けさせてよいか (WeaponDropper が聞く)。
        /// 化けてから撃たれるまで (lifetime) + チャレンジの制限時間のあいだに、屋台の拡張・時間切れが来ない時だけ OK。
        /// フィーバー屋台の場合、フィーバーの残り時間はチャレンジ中は止まるので、撃たれるまでの分だけあればよい。
        /// </summary>
        public bool CanOfferSniper(Target target, float lifetime)
        {
            return sniper != null && CanOfferChallenge(target, lifetime, sniper.timeLimit + sniper.appearTime);
        }

        /// <summary>この的をマシンガンチャレンジの的に化けさせてよいか (WeaponDropper が聞く)。条件はスナイパーと同じ</summary>
        public bool CanOfferMachineGunChallenge(Target target, float lifetime)
        {
            return machineGunChallenge != null && CanOfferChallenge(target, lifetime, machineGunChallenge.Duration);
        }

        // challengeTime = チャレンジが始まってから終わるまでの最長の秒数
        bool CanOfferChallenge(Target target, float lifetime, float challengeTime)
        {
            if (target == null || State != GameState.Playing || ChallengeActive) return false;
            if (bonusStage != null && bonusStage.IsBusy) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;

            // 時間切れまでには、撃たれるまで + チャレンジ全体の時間が必要
            if (timeLeft < lifetime + challengeTime + 1.5f) return false;
            // 屋台の拡張はチャレンジ中は待たせるので、的が出ている間に拡張が来なければよい
            if (Stage < stageStallCounts.Length - 1)
            {
                float untilNextStage = (Stage + 1) * stageInterval - elapsed;
                if (untilNextStage < lifetime + 1.5f) return false;
            }
            if (stall != mainStall && (!IsFever || feverLeft < lifetime + 1.5f)) return false;
            return true;
        }

        /// <summary>今プレイヤーの周りに出ている屋台 (メイン + フィーバー中の屋台)。動いている途中の屋台は除く</summary>
        public List<Stall> ShownStalls()
        {
            var list = new List<Stall>();
            if (mainStall != null && mainStall.IsSettled) list.Add(mainStall);
            if (IsFever)
                foreach (var s in activeFeverStalls)
                    if (s.IsSettled) list.Add(s);
            return list;
        }

        /// <summary>スナイパーの的を撃った: その屋台を引っ込め、屋台があった方の遠くに大きな的を出し、スナイパーライフルを渡す</summary>
        void StartSniper(Target target)
        {
            if (sniper == null || ChallengeActive) return;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null) return;

            sniperStall = stall;
            stall.Sink(stallMoveDuration);
            Vector3 eye = player.head != null ? player.head.transform.position : player.transform.position + Vector3.up * 1.6f;
            sniper.Show(stall.transform.position - player.transform.position, eye);

            player.SetDualWield(false); // ライフルは右手の1丁だけ
            player.GiveWeapon(WeaponType.Sniper, sniper.timeLimit);
            SniperShotsLeft = Mathf.Max(1, sniper.shots);
            Sfx.Play("sniperready");
            ShowMessage($"SNIPER CHALLENGE!  {SniperShotsLeft} SHOTS\n{sniper.Tier.name} {sniper.Tier.distance:0}m  CENTER {sniper.MaxPoints} PTS", 3f, new Color(1f, 0.3f, 0.35f));
        }

        /// <summary>スナイパーチャレンジの残りの弾の数</summary>
        public int SniperShotsLeft { get; private set; }

        /// <summary>
        /// スナイパーライフルの1発が撃たれた (GunController から)。hit = 当たった的 (なければ null)。
        /// 大きな的に当たったら成功で終わり。外れたら残りの弾を1つ減らし、なくなったら失敗で終わる。
        /// </summary>
        public void OnSniperShot(Target hit)
        {
            if (!SniperActive) return;
            bool success = hit != null && hit.kind == TargetKind.SniperBig;
            if (success)
            {
                EndSniper(true, null);
                return;
            }
            SniperShotsLeft--;
            if (SniperShotsLeft <= 0)
            {
                EndSniper(false, "MISS...");
                return;
            }
            Sfx.Play("snipermiss");
            ShowMessage(SniperShotsLeft == 1 ? "MISS!\nLAST SHOT!" : $"MISS!\n{SniperShotsLeft} SHOTS LEFT", 1.5f, new Color(1f, 0.55f, 0.5f));
        }

        /// <summary>チャレンジを終える: 大きな的を消し、引っ込めた屋台を戻し、銃を元に戻す</summary>
        void EndSniper(bool success, string failMessage)
        {
            var stall = sniperStall;
            sniperStall = null;
            sniper.Hide(success);

            // 屋台を戻す (チャレンジ中にフィーバーが終わったフィーバー屋台は戻さない)
            bool show = stall == mainStall || (IsFever && activeFeverStalls.Contains(stall));
            if (stall != null && show) stall.Rise(stallMoveDuration, State == GameState.Playing);

            if (player.Weapon == WeaponType.Sniper) player.ResetWeapon(); // 外した1発で別の武器の的を取った時はそのまま
            player.SetDualWield(IsFever);
            if (!success && failMessage != null)
            {
                Sfx.Play("snipermiss");
                ShowMessage(failMessage, 2f, new Color(0.7f, 0.7f, 0.8f));
            }
        }

        // ---- マシンガンチャレンジ ----

        /// <summary>
        /// マシンガンチャレンジの的を撃った: その屋台と両隣の屋台を引っ込め、同じ方向に横長の屋台 (棚にカップが並ぶ) をせり上げる。
        /// 屋台がせり上がり終わったらマシンガンを渡す (Update)。フィーバー外はメイン屋台しか出ていないので、引っ込むのはメイン屋台だけ。
        /// </summary>
        void StartMachineGunChallenge(Target target)
        {
            if (machineGunChallenge == null || ChallengeActive) return;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null) return;

            // 輪に並んでいる順 (メイン屋台 → フィーバー屋台。ArrangeFeverStalls と同じ順) から両隣を探す
            var ring = new List<Stall> { mainStall };
            if (IsFever) ring.AddRange(activeFeverStalls);
            machineGunStalls.Clear();
            machineGunStalls.Add(stall);
            int i = ring.IndexOf(stall);
            if (i >= 0 && ring.Count > 1)
            {
                foreach (var s in new[] { ring[(i + ring.Count - 1) % ring.Count], ring[(i + 1) % ring.Count] })
                    if (!machineGunStalls.Contains(s)) machineGunStalls.Add(s);
            }
            foreach (var s in machineGunStalls) s.Sink(stallMoveDuration);

            MachineGunChallengeActive = true;
            Vector3 center = player.transform.position;
            Vector3 dir = stall.transform.position - center;
            dir.y = 0f;
            machineGunChallenge.Show(center, dir, dir.magnitude);
            machineGunGiven = false;

            Sfx.Play("sniperready");
            ShowMessage($"MACHINE GUN CHALLENGE!\n{machineGunChallenge.LayoutName}", 2.5f, new Color(1f, 0.85f, 0.2f));
        }

        /// <summary>横長の屋台がせり上がり終わった: マシンガンを渡す</summary>
        void GiveChallengeMachineGun()
        {
            machineGunGiven = true;
            player.GiveWeapon(WeaponType.MachineGun, machineGunChallenge.ShootDuration);
            Sfx.Play("pickup");
            ShowMessage($"SHOOT ALL {machineGunChallenge.Total} CUPS!\nBOMBS INSIDE!", 2f, new Color(1f, 0.85f, 0.2f));
        }

        /// <summary>チャレンジを終える (全部落とした / マシンガンの時間切れ): 横長の屋台を沈め、引っ込めた屋台を戻し、銃を元に戻す</summary>
        void EndMachineGunChallenge()
        {
            if (!MachineGunChallengeActive) return;
            MachineGunChallengeActive = false;
            bool cleared = machineGunChallenge.AllCleared;
            int hits = machineGunChallenge.Hits, total = machineGunChallenge.Total;
            machineGunChallenge.Hide();

            // 最後のカップが落ちるのを少し見せてから、横長の屋台が沈むのと同時に元の屋台を戻す
            StartCoroutine(RiseStallsAfter(new List<Stall>(machineGunStalls), machineGunChallenge.SwapBackDelay));
            machineGunStalls.Clear();

            if (machineGunGiven && player.Weapon == WeaponType.MachineGun) player.ResetWeapon();
            if (State != GameState.Playing) return;
            if (cleared)
            {
                Sfx.Play("complete");
                ShowMessage($"PERFECT!!\nALL {total} CLEAR!", 3f, new Color(1f, 0.85f, 0.3f));
            }
            else
            {
                Sfx.Play("snipermiss");
                ShowMessage($"TIME UP!\n{hits} / {total} CUPS", 2f, new Color(0.7f, 0.7f, 0.8f));
            }
        }

        // 引っ込めた屋台を戻す (待っている間にフィーバーが終わったフィーバー屋台は戻さない)
        IEnumerator RiseStallsAfter(List<Stall> stalls, float delay)
        {
            yield return new WaitForSeconds(delay);
            ShowHatch(); // 別の世界から戻った: 床下のハッチも戻す (隠していなければ何もしない)
            foreach (var s in stalls)
            {
                if (ChallengeActive && (machineGunStalls.Contains(s) || s == sniperStall)) continue; // 念のため: もう次のチャレンジで使っている
                bool show = s == mainStall || (IsFever && activeFeverStalls.Contains(s));
                if (show) s.Rise(stallMoveDuration, State == GameState.Playing);
            }
        }
    }
}
