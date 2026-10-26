using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GameManager のうち、フィーバーの的を撃った時の、周りの屋台の出し方 (ラウンドの種類ごと)。
    /// 2 MIN: メイン屋台に近い場所から順にせり上がる。
    /// 100,000 POINTS: メイン屋台の後ろに列になって地面から生え、列が時計の針のように右回りに回りながら、
    ///   自分の場所に来た屋台から順にプレイヤーの方へ押し出されて並ぶ。全部並び終わってから足元のハッチを出す。
    /// WORLD GATE・ステージアップ・別の世界から戻った時などは、今まで通り一度にせり上がる。
    /// </summary>
    public partial class GameManager
    {
        enum FeverEntry { AllAtOnce, InOrder, Procession }

        [Tooltip("2 MIN のフィーバー: メイン屋台から1つ離れるごとに、せり上がりを遅らせる秒数")]
        public float feverInOrderStep = 0.3f;
        [Tooltip("100,000 POINTS のフィーバー: 列が次の屋台の場所まで回る秒数")]
        public float feverProcessionSlotTime = 0.45f;
        [Tooltip("100,000 POINTS のフィーバー: 列の屋台どうしの間隔 (m)")]
        public float feverProcessionSpacing = 3f;

        Coroutine feverEntryRoutine;

        // ---- フィーバーの的を撃ってから、周りの屋台が全部そろうまで ----
        // この間は特別な的 (武器・爆弾・青い玉・チャレンジ・DELUXE・別の世界) を出さない (WeaponDropper が見る)。
        // 始まった時に出ていた特別な的は、点数の的に戻す (撃っても不発)。屋台が動いている途中に別の仕組みが屋台を動かして、こじれないように
        /// <summary>フィーバーの屋台がそろうのを待っている間か</summary>
        public bool FeverAssembling { get; private set; }
        float feverAssembleGiveUp; // (念のため) この時刻を過ぎたら、そろわなくても待つのをやめる
        /// <summary>出ている特別な的を、全部点数の的に戻してほしい (フィーバーが始まった時。WeaponDropper が受け取る)</summary>
        public static event System.Action SpecialTargetsCancelled;

        void BeginFeverAssembling()
        {
            FeverAssembling = true;
            feverAssembleGiveUp = Time.time + 30f;
            SpecialTargetsCancelled?.Invoke();
        }

        // 屋台が全部そろったか (出し方が終わり、出ている屋台が全部止まった) を毎フレーム調べる。チャレンジなどで沈んでいる屋台は数えない
        void UpdateFeverAssembling()
        {
            if (!FeverAssembling) return;
            if (!IsFever || State != GameState.Playing || Time.time > feverAssembleGiveUp) { FeverAssembling = false; return; }
            if (feverEntryRoutine != null) return;
            if (mainStall != null && mainStall.gameObject.activeSelf && !mainStall.IsSettled) return;
            foreach (var s in activeFeverStalls)
                if (s != null && s.gameObject.activeSelf && !s.IsSettled) return;
            FeverAssembling = false;
        }
        readonly List<Stall> feverEntryStalls = new List<Stall>(); // 出し方の途中の屋台 (まだせり上がっていない / 列で回っている)
        readonly Dictionary<Stall, (Vector3 pos, Quaternion rot)> feverEntrySlots = new Dictionary<Stall, (Vector3, Quaternion)>(); // その屋台が並ぶ輪の場所

        /// <summary>
        /// チャレンジ (スナイパー・マシンガン) で引っ込める屋台が、出し方の途中だった: 列から外し、地上に出ていればその場で沈め、
        /// 自分の輪の場所の地下に控えさせる (チャレンジが終わったら、いつもの戻し方でその場所にせり上がる)。
        /// 出し方の途中の屋台でなければ false (いつも通り Sink する)
        /// </summary>
        readonly HashSet<Stall> heldBackStalls = new HashSet<Stall>(); // 出てくる途中にチャレンジで控えさせた屋台 (チャレンジが終わったら、下から生えてくる)

        readonly List<Stall> feverEntryLine = new List<Stall>(); // 出し方の屋台の並び (コルーチンが番号で動かす。豪華版に変身したら、ここを入れ替える)
        readonly List<(Stall stall, int index)> feverEntryShadows = new List<(Stall, int)>(); // 変身して縮んでいる途中の前の屋台 (消えるまで同じ列の場所を動かす)

        /// <summary>
        /// 豪華版 ⇔ 普通版の入れ替え (SwapStall) で、屋台 from が to に変身する: 出し方の途中なら、to が from の代わりに列・順番待ちを引き継ぐ
        /// (from は、縮んで消えるまで同じ場所を動かす)。控えさせていた屋台なら、控えも to に移す。
        /// まだ出ていない (順番待ち) なら true (to は出さずに、順番が来たらせり上がる)
        /// </summary>
        bool PassFeverEntry(Stall from, Stall to)
        {
            if (heldBackStalls.Remove(from)) heldBackStalls.Add(to);
            int index = feverEntryLine.IndexOf(from);
            if (index < 0) return false;
            feverEntryLine[index] = to;
            if (feverEntrySlots.TryGetValue(from, out var slot)) feverEntrySlots[to] = slot;
            int waiting = feverEntryStalls.IndexOf(from);
            if (waiting >= 0) feverEntryStalls[waiting] = to;
            if (from.gameObject.activeInHierarchy) { feverEntryShadows.Add((from, index)); return false; }
            return waiting >= 0;
        }

        // 列の index 番目の屋台 (と、そこで変身して縮んでいる途中の前の屋台) を置く。屋台がほかの動きに取り消されていたら false
        bool DriveEntry(int index, Vector3 pos, Quaternion rot)
        {
            for (int k = feverEntryShadows.Count - 1; k >= 0; k--)
                if (feverEntryShadows[k].index == index && !feverEntryShadows[k].stall.DriveTo(pos, rot)) feverEntryShadows.RemoveAt(k);
            return feverEntryLine[index].DriveTo(pos, rot);
        }

        /// <summary>チャレンジで引っ込めた屋台を戻す: 地下から足元のハッチを開けてせり上がる。出てくる途中で控えさせた屋台なら、出てきた合図の音も鳴らす</summary>
        void RiseAfterChallenge(Stall s)
        {
            s.Rise(stallMoveDuration, State == GameState.Playing);
            if (heldBackStalls.Remove(s)) PlayStallSound(s);
        }

        bool LeaveFeverEntry(Stall s, float duration)
        {
            if (s == null || !feverEntryStalls.Remove(s) || !feverEntrySlots.TryGetValue(s, out var slot)) return false;
            s.SinkAway(duration, slot.pos, slot.rot);
            heldBackStalls.Add(s);
            return true;
        }

        /// <summary>フィーバーの的を撃った時の出し方 (ラウンドの種類で決める)</summary>
        FeverEntry FeverEntryForRound => GateMode || DebugMode || Endless ? FeverEntry.AllAtOnce : TimeAttack ? FeverEntry.InOrder : FeverEntry.Procession;

        /// <summary>
        /// 出し方の途中で輪を並べ直す時 (ステージアップ): 途中の屋台をすぐ出し、並べ直しの Slide に任せる。
        /// riseRest = false (フィーバーの終わり) なら、出さずに止めるだけ (沈めるのは EndFever)
        /// </summary>
        void StopFeverEntry(bool riseRest)
        {
            if (feverEntryRoutine != null) { StopCoroutine(feverEntryRoutine); feverEntryRoutine = null; }
            if (riseRest)
                foreach (var s in feverEntryStalls)
                {
                    if (s == null) continue;
                    s.Release(true);
                    if (!s.gameObject.activeSelf && activeFeverStalls.Contains(s) && !IsChallengeSpot(s)) s.Rise(stallMoveDuration); // (豪華版に入れ替わった屋台・チャレンジの場所は出さない)
                }
            feverEntryStalls.Clear();
            feverEntryLine.Clear();
            feverEntryShadows.Clear();
            if (!riseRest) heldBackStalls.Clear(); // (フィーバーの終わり)
        }

        /// <summary>屋台が出てきた / 並んだ合図の音 (A ボタンのリセットと同じ「ゴゴゴ…」) を、その屋台の地上の場所から立体音響で鳴らす</summary>
        static void PlayStallSound(Stall stall)
        {
            var parent = stall.transform.parent;
            var ground = parent != null ? parent.TransformPoint(stall.RestPosition) : stall.RestPosition;
            Sfx.PlayAt("door", ground + Vector3.up * 1.5f);
        }

        // 輪の、メイン屋台から右回りに angle 度の所の向き
        Quaternion RingRot(float baseYaw, float angle) => Quaternion.Euler(0f, baseYaw + angle, 0f);

        /// <summary>新しく出す屋台 (added) を、出し方に合わせて動かし始める。slots = 各屋台の輪の番号 (1〜n-1)</summary>
        void BeginFeverEntry(FeverEntry entry, List<Stall> added, List<int> slots, int n, float baseYaw, float radius)
        {
            feverEntryStalls.Clear();
            feverEntryStalls.AddRange(added);
            feverEntrySlots.Clear();
            for (int i = 0; i < added.Count; i++)
            {
                var rot = RingRot(baseYaw, 360f / n * slots[i]);
                feverEntrySlots[added[i]] = (rot * new Vector3(0f, 0f, radius), rot);
            }
            feverEntryLine.Clear();
            feverEntryLine.AddRange(added);
            feverEntryShadows.Clear();
            if (entry == FeverEntry.InOrder) feverEntryRoutine = StartCoroutine(RiseInOrder(slots, n));
            else feverEntryRoutine = StartCoroutine(Procession(slots, n, baseYaw, radius));
        }

        // 2 MIN: メイン屋台に近い場所 (左右の同じ近さは同時) から順にせり上がる
        IEnumerator RiseInOrder(List<int> slots, int n)
        {
            var stalls = feverEntryLine; // (豪華版に変身したら、変身した屋台が入っている)
            int far = n / 2;
            for (int d = 1; d <= far; d++)
            {
                if (d > 1) yield return new WaitForSeconds(feverInOrderStep);
                if (!IsFever || AnyWorldMode) break; // 別の世界へ行った: 残りは戻ってきた時にまとめて出る
                for (int i = 0; i < stalls.Count; i++)
                {
                    if (Mathf.Min(slots[i], n - slots[i]) != d || !feverEntryStalls.Contains(stalls[i]) || !activeFeverStalls.Contains(stalls[i])) continue;
                    stalls[i].Rise(stallMoveDuration);
                    PlayStallSound(stalls[i]); // 生えてくる屋台の場所から鳴らす
                    feverEntryStalls.Remove(stalls[i]);
                }
            }
            feverEntryStalls.Clear();
            feverEntryLine.Clear();
            feverEntryShadows.Clear();
            feverEntryRoutine = null;
        }

        // 100,000 POINTS: メイン屋台の後ろに輪の番号順の列で生え、列が右回りに回って、自分の場所に来た屋台からプレイヤーの方へ押し出される
        IEnumerator Procession(List<int> slots, int n, float baseYaw, float radius)
        {
            var stalls = feverEntryLine; // (豪華版に変身したら、変身した屋台が入っている)
            float step = 360f / n;
            int count = stalls.Count;
            // 列の中の順番 (輪の番号の小さい屋台ほどメイン屋台のすぐ後ろ)
            var order = new List<int>();
            for (int i = 0; i < count; i++) order.Add(i);
            order.Sort((a, b) => slots[a].CompareTo(slots[b]));
            var lineRadius = new float[count];
            for (int k = 0; k < count; k++) lineRadius[order[k]] = radius + feverProcessionSpacing * (k + 1);

            // 1. メイン屋台の後ろに、地面から生える (手前から少しずつずらして)
            for (int i = 0; i < count; i++) stalls[i].BeginDrive(RingRot(baseYaw, 0f) * new Vector3(0f, 0f, lineRadius[i]), RingRot(baseYaw, 0f));
            float grow = stallMoveDuration, lag = 0.08f;
            float growAll = grow + lag * count;
            for (float t = 0f; t < growAll; t += Time.deltaTime)
            {
                if (!IsFever) yield break;
                for (int k = 0; k < count; k++)
                {
                    int i = order[k];
                    float u = Mathf.Clamp01((t - lag * k) / grow);
                    float e = 1f - Mathf.Pow(1f - u, 3f);
                    var rot = RingRot(baseYaw, 0f);
                    DriveEntry(i, rot * new Vector3(0f, 0f, lineRadius[i]) + Vector3.down * (stalls[i].hiddenDepth * (1f - e)), rot);
                }
                yield return null;
            }

            // 2. 列が右回りに回る。自分の場所に来た屋台は、そこでプレイヤーの方 (輪の半径) まで押し出されて止まる
            float speed = step / Mathf.Max(0.05f, feverProcessionSlotTime);
            float push = Mathf.Max(0.1f, feverProcessionSlotTime);
            var arrivedAt = new float[count];
            for (int i = 0; i < count; i++) arrivedAt[i] = -1f;
            int done = 0;
            for (float t = 0f; done < count; t += Time.deltaTime)
            {
                if (!IsFever) yield break;
                float hand = speed * t;
                for (int i = 0; i < count; i++)
                {
                    if (arrivedAt[i] == -2f) continue; // 並び終わった
                    float slotAngle = step * slots[i];
                    Vector3 pos;
                    Quaternion rot;
                    if (hand < slotAngle)
                    {
                        rot = RingRot(baseYaw, hand);
                        pos = rot * new Vector3(0f, 0f, lineRadius[i]);
                    }
                    else
                    {
                        if (arrivedAt[i] < 0f) arrivedAt[i] = t;
                        float u = Mathf.Clamp01((t - arrivedAt[i]) / push);
                        float e = 1f - Mathf.Pow(1f - u, 3f);
                        rot = RingRot(baseYaw, slotAngle);
                        pos = rot * new Vector3(0f, 0f, Mathf.Lerp(lineRadius[i], radius, e));
                        if (u >= 1f)
                        {
                            DriveEntry(i, pos, rot);
                            stalls[i].Release(true); // 並んだ屋台から的を出す
                            PlayStallSound(stalls[i]); // 自分の場所に着いた屋台から鳴らす
                            arrivedAt[i] = -2f;
                            done++;
                            continue;
                        }
                    }
                    if (!DriveEntry(i, pos, rot)) { arrivedAt[i] = -2f; done++; } // 途中でほかの動き (チャレンジで沈むなど) に取り消された
                }
                yield return null;
            }

            // 3. 全部並び終わってから、足元のハッチを出す
            foreach (var s in stalls) s.ShowHatch();
            feverEntryStalls.Clear();
            feverEntryLine.Clear();
            feverEntryShadows.Clear();
            feverEntryRoutine = null;
        }
    }
}
