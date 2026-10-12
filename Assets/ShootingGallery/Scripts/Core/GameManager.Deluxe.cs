using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、豪華屋台チェンジ (DELUXE の的)。</summary>
    public partial class GameManager
    {
        // ---- 豪華屋台チェンジ ----

        /// <summary>
        /// この的を DELUXE の的に化けさせてよいか (WeaponDropper が聞く)。
        /// 豪華タイム中・チャレンジ中・ボーナスステージ中は出さない。撃たれるまでの間に時間切れが来る時も出さない。
        /// </summary>
        public bool CanOfferDeluxe(Target target, float lifetime)
        {
            if (deluxeMainStall == null || target == null || State != GameState.Playing) return false;
            if (DeluxeActive || ChallengeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + 8f;
        }

        /// <summary>
        /// DELUXE の的を撃った: 今出ている屋台 (メイン + フィーバー中の屋台) を、同じ場所で豪華版の屋台に入れ替える。
        /// 以後 mainStall / feverStalls は豪華版を指すので、フィーバー・ステージの拡張・チャレンジはそのまま豪華版の屋台で動く。
        /// </summary>
        void StartDeluxe()
        {
            if (DeluxeActive || ChallengeActive || deluxeMainStall == null) return;
            DeluxeActive = true;
            deluxeLeft = deluxeDuration;
            nextFireworkTime = Time.time + 0.3f;

            SwapStalls(deluxeMainStall, deluxeFeverStalls);
            FadeAmbience(deluxeFogColor, deluxeAmbient);

            Sfx.Play("deluxe");
            ShowMessage("DELUXE TIME!!\nGRAND STALLS OPEN!", 3f, new Color(1f, 0.8f, 0.3f));
        }

        /// <summary>豪華タイムが終わった: 豪華版の屋台を、同じ場所で普通の屋台に戻す</summary>
        void EndDeluxe()
        {
            if (!DeluxeActive) return;
            DeluxeActive = false;
            SwapStalls(normalMainStall, normalFeverStalls);
            FadeAmbience(normalFogColor, normalAmbient);
            if (State == GameState.Playing) ShowMessage("DELUXE END", 1.5f, Color.white);
        }

        /// <summary>
        /// 出ている屋台を、newMain と newFeverPool の屋台に同じ場所で入れ替える (古い屋台が沈んでから新しい屋台がせり上がる)。
        /// フィーバー屋台は newFeverPool からランダムに選ぶ。
        /// </summary>
        void SwapStalls(Stall newMain, Stall[] newFeverPool)
        {
            SwapStall(mainStall, newMain);
            mainStall = newMain;

            var pool = new List<Stall>(newFeverPool);
            for (int i = 0; i < activeFeverStalls.Count && pool.Count > 0; i++)
            {
                int j = Random.Range(0, pool.Count);
                SwapStall(activeFeverStalls[i], pool[j]);
                activeFeverStalls[i] = pool[j];
                pool.RemoveAt(j);
            }
            feverStalls = newFeverPool;
        }

        void SwapStall(Stall from, Stall to)
        {
            if (from == null || to == null) return;
            to.PlaceAt(from.RestPosition, from.RestRotation);
            // 沈めずに、その場でカードを裏返すように入れ替える: 古い屋台が横に縮んで細い線になり、
            // 金色の光と花火がはじけた瞬間に、新しい屋台が線から横へ広がって現れる
            bool shown = from.gameObject.activeInHierarchy;
            if (shown) from.SwapOut(swapOutTime);
            StartCoroutine(SwapInAfter(to, shown ? swapOutTime : 0f));
        }

        const float swapOutTime = 0.35f; // 古い屋台が縮みきるまで
        const float swapInTime = 0.6f;   // 新しい屋台が広がりきるまで

        // 古い屋台が縮みきったら、光をはじけさせて新しい屋台を広げる (待っている間に別の屋台に入れ替わったり、フィーバーが終わったりしていたら出さない)
        IEnumerator SwapInAfter(Stall stall, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            bool show = stall == mainStall || (IsFever && activeFeverStalls.Contains(stall));
            if (ChallengeActive && (machineGunStalls.Contains(stall) || stall == sniperStall)) show = false;
            if (!show) yield break;
            stall.SwapIn(swapInTime, State == GameState.Playing);
            SwapSparkle(stall.transform);
        }

        // 入れ替わる瞬間: 屋台の真ん中の縦の線に金色の光、屋台の前に色とりどりの小さな花火を散らす
        void SwapSparkle(Transform stall)
        {
            if (fireworkMaterials == null || fireworkMaterials.Length == 0) return;
            Vector3 front = -stall.forward * 1.5f; // カウンターの手前 (プレイヤー側)
            for (int i = 0; i < 4; i++)
                Explosion.Spawn(stall.position + front + Vector3.up * (0.4f + i * 0.6f), 0.5f, fireworkMaterials[0]);
            for (int i = 0; i < 8; i++)
            {
                var mat = fireworkMaterials[Random.Range(0, fireworkMaterials.Length)];
                Vector3 p = stall.position + front + stall.right * Random.Range(-1.6f, 1.6f) + Vector3.up * Random.Range(0.5f, 2.6f);
                Explosion.Spawn(p, Random.Range(0.15f, 0.28f), mat);
            }
        }

        /// <summary>夜空に花火を1発上げる (プレイヤーのまわりのランダムな方向。何色かの光の玉がぱっと広がる)</summary>
        void LaunchFirework()
        {
            nextFireworkTime = Time.time + Random.Range(0.35f, 0.8f);
            if (fireworkMaterials == null || fireworkMaterials.Length == 0) return;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 center = player.transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * Random.Range(8f, 11f)
                           + Vector3.up * Random.Range(5.5f, 8.5f);
            var mat = fireworkMaterials[Random.Range(0, fireworkMaterials.Length)];
            Explosion.Spawn(center, Random.Range(0.6f, 0.9f), mat);
            int sparks = Random.Range(5, 8);
            for (int i = 0; i < sparks; i++)
                Explosion.Spawn(center + Random.onUnitSphere * Random.Range(0.7f, 1.3f), Random.Range(0.18f, 0.3f), mat);
        }
    }
}
