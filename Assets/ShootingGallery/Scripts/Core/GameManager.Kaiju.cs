using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、怪獣モード (怪獣の的)。</summary>
    public partial class GameManager
    {
        // ---- 怪獣モード ----

        const float KaijuShrinkTime = 3f;   // 床と屋台がまとめて小さくなって消えるまでの秒数 (ゆっくり)
        const float KaijuGrowTime = 2.2f;   // 終わりに元の大きさへ戻るまでの秒数
        const float KaijuSmallest = 0.03f;  // 小さくなりきった時の倍率
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
        /// 怪獣の的を撃った: 床 (地面・穴のふち・ボーナスステージのハッチ) と出ている屋台をまとめて、プレイヤーの足元を中心に
        /// 世界全体がゆっくり小さくなるように縮めて消し、まわりに街を伸ばす。空は青空に。
        /// 武器は怪獣の頭 (炎の玉を吐く)。
        /// </summary>
        void StartKaiju()
        {
            if (DebugClearFor(StartKaiju)) return;
            if (kaijuCity == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            PutAwayStalls(true);
            StartWorldScale(ScaleWorld(new List<Stall>(stageStalls), true, KaijuShrinkTime, 0f));
            kaijuCity.dropDelay = KaijuShrinkTime; // 床と屋台が縮みきってから街が伸びる (縮みきった床と入れ替わりに街の地面が出る)
            kaijuCity.Begin();
            player.GiveWeapon(WeaponType.FireBall, kaijuCity.duration);
            // 縮みきったら、遠くまで続く街が見えるように霧を遠くからにして、青空にする
            StartCoroutine(WhileActiveAfter(KaijuShrinkTime, () => KaijuActive, () =>
            {
                RenderSettings.fogStartDistance = Mathf.Max(normalFogStart, 35f);
                RenderSettings.fogEndDistance = Mathf.Max(normalFogEnd, 140f);
                FadeAmbience(kaijuFogColor, kaijuAmbient, kaijuSkyColor);
            }));
            Sfx.Play("roar");
            ShowMessage("KAIJU MODE!!\nBREAK THE COLORED BUILDINGS!", 3f, new Color(0.4f, 1f, 0.4f));
        }

        /// <summary>怪獣モードの時間切れ: 街を沈め、縮めた屋台を元の大きさに戻す</summary>
        void EndKaiju()
        {
            if (!KaijuActive) return;
            int broken = kaijuCity.Destroyed;
            kaijuCity.End();
            if (player.Weapon == WeaponType.FireBall) player.ResetWeapon();
            StartWorldScale(ScaleWorld(new List<Stall>(stageStalls), false, KaijuGrowTime, kaijuCity.CloseTime));
            stageStalls.Clear();
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor);
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            if (State == GameState.Playing)
            {
                Sfx.Play(broken > 0 ? "complete" : "end");
                ShowMessage($"KAIJU MODE END\nBROKE {broken} BUILDINGS", 2.5f, new Color(0.4f, 1f, 0.4f));
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

            float logSmall = Mathf.Log(KaijuSmallest);
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float e = t * t * (3f - 2f * t);
                ApplyWorldScale(moving, ground, Mathf.Exp(logSmall * (shrink ? e : 1f - e)));
                yield return null;
            }
            ApplyWorldScale(moving, ground, shrink ? KaijuSmallest : 1f);
            if (shrink)
            {   // 小さくなりきった: 屋台は地下へしまい、床は元の大きさに戻して隠しておく (街の地面の下)
                foreach (var s in moving) s.FinishWorldShrink();
                ApplyWorldScale(new List<Stall>(), ground, 1f);
                foreach (var g in ground) g.gameObject.SetActive(false);
            }
            else foreach (var s in moving) s.FinishWorldGrow();
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
