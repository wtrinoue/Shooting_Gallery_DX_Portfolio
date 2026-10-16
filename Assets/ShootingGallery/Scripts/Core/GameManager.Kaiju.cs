using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、怪獣モード (怪獣の的)。</summary>
    public partial class GameManager
    {
        // ---- 怪獣モード ----

        const float KaijuShrinkTime = 3.5f; // 床・屋台・大きな街がまとめて小さくなっていく秒数 (ゆっくり)
        const float KaijuCityRatio = 0.15f; // その間に全部が小さくなる倍率 (街は 1 / これ 倍の大きさから、今の大きさまで縮む)
        const float KaijuVanishTime = 0.5f; // 足元に小さく残った屋台と床が、さらに縮んで消えるまでの秒数
        const float KaijuGrowTime = 2.2f;   // 終わりに元の大きさへ戻るまでの秒数
        const float KaijuSmallest = 0.01f;  // 屋台と床が消える時の倍率 (戻る時はここから大きくなる)
        Coroutine kaijuWorldRoutine;
        Vector3[] kaijuGroundPos, kaijuGroundScale;
        bool[] kaijuGroundActive;

        /// <summary>この的を怪獣の的 (怪獣モード) に化けさせてよいか (WeaponDropper が聞く)。条件は海戦モードと同じ</summary>
        public bool CanOfferKaiju(Target target, float lifetime)
        {
            if (kaijuCity == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + kaijuCity.duration + kaijuCity.CloseTime + 2f;
        }

        /// <summary>
        /// 怪獣の的を撃った:
        ///   1. まず屋台のまわりに、見上げるほど大きなビルの街が地面から伸びてくる (屋台はそのまま。的はしまう)。
        ///   2. 次に、床 (地面・穴のふち・ボーナスステージのハッチ)・屋台・街の全部が、プレイヤーの足元を中心に同じ調子でゆっくり小さくなっていく
        ///      (プレイヤーがどんどん大きくなっていくように見える)。街が今の大きさまで縮んだら、足元に小さく残った屋台と床が消える。
        ///   3. 落ち着いたら青空になり、手が怪獣の頭になって、炎の玉でビルを壊せる。
        /// </summary>
        void StartKaiju()
        {
            if (DebugClearFor(StartKaiju)) return;
            if (kaijuCity == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            PutAwayStalls(true);
            foreach (var s in stageStalls) s.SetTargetsAvailable(false);
            kaijuCity.dropDelay = 0.2f;
            kaijuCity.Begin(1f / KaijuCityRatio); // 1. 大きな街が伸びてくる
            StartWorldScale(ScaleWorld(new List<Stall>(stageStalls), true, KaijuShrinkTime, kaijuCity.RiseDuration + 0.4f)); // 2. 全部まとめて小さくなる
            // 大きな街が遠くまで見えるように、霧を遠くからにする
            RenderSettings.fogStartDistance = Mathf.Max(normalFogStart, 35f);
            RenderSettings.fogEndDistance = Mathf.Max(normalFogEnd, 140f);
            Sfx.Play("roar");
            ShowMessage("KAIJU MODE!!", 2f, new Color(0.4f, 1f, 0.4f));
        }

        // 3. 世界全体が小さくなりきった: 青空にして、怪獣の頭を渡す (手元のゲージが残り時間)
        void OnKaijuSettled()
        {
            kaijuCity.FinishShrink();
            if (!KaijuActive) return;
            FadeAmbience(kaijuFogColor, kaijuAmbient, kaijuSkyColor);
            if (State != GameState.Playing) return;
            player.GiveStageWeapon(WeaponType.FireBall, kaijuCity.TimeLeft);
            Sfx.Play("roar");
            ShowMessage("SMASH BUILDINGS, CARS,\nPLANES & HELICOPTERS!", 2.5f, new Color(0.4f, 1f, 0.4f));
        }

        /// <summary>怪獣モードの時間切れ: 街を沈め、縮めた屋台を元の大きさに戻す</summary>
        void EndKaiju()
        {
            if (!KaijuActive) return;
            int broken = kaijuCity.Destroyed;
            kaijuCity.End();
            ReleaseStageWeapon(WeaponType.FireBall);
            StartWorldScale(ScaleWorld(new List<Stall>(stageStalls), false, KaijuGrowTime, kaijuCity.CloseTime));
            HoldStallsFor(kaijuCity.CloseTime + KaijuGrowTime + 0.2f); // 大きくなって戻りきるまで、屋台の拡張 (別の屋台がせり上がる) を待たせる
            stageStalls.Clear();
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor);
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            if (State == GameState.Playing)
            {
                Sfx.Play(broken > 0 ? "complete" : "end");
                ShowMessage($"KAIJU MODE END\nSMASHED {broken}", 2.5f, new Color(0.4f, 1f, 0.4f));
            }
        }

        void StartWorldScale(IEnumerator routine)
        {
            if (kaijuWorldRoutine != null) StopCoroutine(kaijuWorldRoutine);
            kaijuWorldRoutine = StartCoroutine(routine);
        }

        /// <summary>
        /// 床の部品 (KaijuCity.groundParts) と屋台を、プレイヤーの足元を中心に同じ倍率でまとめて小さくする (shrink) / 元の大きさへ戻す。
        /// 1つのまとまりとして毎フレーム同じ倍率を渡すので、屋台とハッチがばらばらでなく、世界全体が小さくなって見える。
        /// 倍率は比が一定の調子で変わる (最後に急に消えず、ずっと同じ調子で小さくなる)。戻す時、待っている間にフィーバーが終わった屋台は戻さない
        /// </summary>
        IEnumerator ScaleWorld(List<Stall> stalls, bool shrink, float duration, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var ground = kaijuCity.groundParts ?? new Transform[0];
            if (shrink || kaijuGroundPos == null)
            {
                kaijuGroundPos = new Vector3[ground.Length];
                kaijuGroundScale = new Vector3[ground.Length];
                kaijuGroundActive = new bool[ground.Length];
                for (int i = 0; i < ground.Length; i++)
                {
                    kaijuGroundPos[i] = ground[i].localPosition;
                    kaijuGroundScale[i] = ground[i].localScale;
                    kaijuGroundActive[i] = ground[i].gameObject.activeSelf;
                }
            }
            var moving = new List<Stall>();
            foreach (var s in stalls)
            {
                if (!shrink)
                {
                    bool show = s == mainStall || (IsFever && activeFeverStalls.Contains(s));
                    if (!show || ChallengeActive) continue;
                }
                if (s.BeginWorldScale(!shrink, State == GameState.Playing)) moving.Add(s);
            }
            if (!shrink)
                for (int i = 0; i < ground.Length; i++) ground[i].gameObject.SetActive(kaijuGroundActive[i]);

            if (shrink)
            {
                // 床・屋台・大きな街を、同じ倍率でいっしょに小さくする (街は 1 / KaijuCityRatio 倍 → 1 倍、床と屋台は 1 倍 → KaijuCityRatio 倍)
                float logRatio = Mathf.Log(KaijuCityRatio);
                for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
                {
                    float e = t * t * (3f - 2f * t);
                    float s = Mathf.Exp(logRatio * e);
                    ApplyWorldScale(moving, ground, s);
                    kaijuCity.SetCityScale(s / KaijuCityRatio, e);
                    yield return null;
                }
                ApplyWorldScale(moving, ground, KaijuCityRatio);
                kaijuCity.SetCityScale(1f, 1f);
                // 足元に小さく残った屋台と床が、さらに縮んで消える (街の地面がもうその下に来ている)
                float logFrom = Mathf.Log(KaijuCityRatio), logTo = Mathf.Log(KaijuSmallest);
                for (float t = 0f; t < 1f; t += Time.deltaTime / KaijuVanishTime)
                {
                    ApplyWorldScale(moving, ground, Mathf.Exp(Mathf.Lerp(logFrom, logTo, t * t)));
                    yield return null;
                }
                // 小さくなりきった: 屋台は地下へしまい、床は元の大きさに戻して隠しておく (街の地面の下)
                foreach (var s in moving) s.FinishWorldShrink();
                ApplyWorldScale(new List<Stall>(), ground, 1f);
                foreach (var g in ground) g.gameObject.SetActive(false);
                kaijuWorldRoutine = null;
                OnKaijuSettled();
                yield break;
            }

            float logSmall = Mathf.Log(KaijuSmallest);
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float e = t * t * (3f - 2f * t);
                ApplyWorldScale(moving, ground, Mathf.Exp(logSmall * (1f - e)));
                yield return null;
            }
            ApplyWorldScale(moving, ground, 1f);
            foreach (var s in moving) s.FinishWorldGrow();
            kaijuWorldRoutine = null;
        }

        void ApplyWorldScale(List<Stall> stalls, Transform[] ground, float s)
        {
            foreach (var st in stalls) st.SetWorldScale(s);
            for (int i = 0; i < ground.Length; i++)
            {
                ground[i].localPosition = kaijuGroundPos[i] * s;
                ground[i].localScale = kaijuGroundScale[i] * s;
            }
        }
    }
}
