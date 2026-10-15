using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 1小節ぶんの音符の並びを抽選して、ためておく所 (MusicStaff が使う。MonoBehaviour ではない普通のクラス)。
    /// 抽選のしかた:
    /// 1. 初めに、全休符を入れるかを抽選する (wholeRestChance)。入れる時はその小節は全休符だけにして、次の小節へ (全休符は拍子ぶんの長さなので決まった長さがない)。
    /// 2. 入れない時は、小節の長さ (32分音符が何個分か) を「残り」として、残り以下の長さの音符だけを候補に絞り、その中からランダムに1つ選んで残りから引く。
    ///    残りが 0 になるまで繰り返すので、小節の中の音符の長さの合計はいつも拍子とぴったり同じになる。
    /// 3. 最後に、小節の最後にフェルマータを付けるかを抽選する (fermataChance。音符ではないので拍を持たない。全休符の小節には付けない)。
    /// 流す直前に抽選すると間に合わないので、拍子の長さごとに何小節分か先に抽選してためておき、流す時はためた物を出すだけにする。
    /// 少なくなったら (lowCount 以下) 一度に refillCount 小節分を補充する
    /// </summary>
    public class MeasureStock
    {
        /// <summary>抽選の候補 (音符の種類・長さ (32分音符が何個分か)・小節の中で最低限とる幅 m)</summary>
        public struct Candidate
        {
            public int kind;
            public int length;
            public float width;
        }

        const int MaxTries = 30; // 小節の幅に入りきらない並びを引き直す回数の上限 (越えたら最後の並びを使い、MusicStaff が縮めて並べる)

        readonly List<Candidate> candidates;
        readonly Candidate? wholeRest, fermata; // なければ null
        readonly float wholeRestChance, fermataChance;
        readonly Dictionary<int, Queue<int[]>> stocks = new Dictionary<int, Queue<int[]>>(); // 小節の長さ → ためた小節 (音符の種類の並び)
        readonly List<int> picked = new List<int>();
        readonly List<int> fits = new List<int>();
        readonly float maxWidth;
        readonly int refillCount, lowCount;

        public bool IsEmpty => candidates.Count == 0 && wholeRest == null;

        /// <param name="candidates">長さで抽選する音符・休符</param>
        /// <param name="wholeRest">全休符 (小節の初めに wholeRestChance で、出たら小節はそれだけ)</param>
        /// <param name="fermata">フェルマータ (小節の最後に fermataChance で付く)</param>
        /// <param name="maxWidth">小節に並べられる幅 (m)。音符の幅の合計がこれを越える並びは引き直す (0 以下なら幅を見ない)</param>
        public MeasureStock(List<Candidate> candidates, Candidate? wholeRest, float wholeRestChance, Candidate? fermata, float fermataChance,
                            float maxWidth, int refillCount, int lowCount)
        {
            this.candidates = candidates;
            this.wholeRest = wholeRest;
            this.wholeRestChance = wholeRestChance;
            this.fermata = fermata;
            this.fermataChance = fermataChance;
            this.maxWidth = maxWidth;
            this.refillCount = Mathf.Max(1, refillCount);
            this.lowCount = Mathf.Clamp(lowCount, 0, this.refillCount - 1);
        }

        /// <summary>その長さの小節を、まだなければ refillCount 小節分ためておく (始める前に呼ぶ)</summary>
        public void Prepare(int length)
        {
            var q = QueueFor(length);
            if (q.Count < refillCount) Refill(q, length, refillCount - q.Count);
        }

        /// <summary>ためた小節を1つ出す (音符の種類の並び。前から順に小節の始まり側)。空なら、その場で1つ抽選する (ふつうは起きない)</summary>
        public int[] Take(int length)
        {
            var q = QueueFor(length);
            if (q.Count == 0) Refill(q, length, 1);
            return q.Dequeue();
        }

        /// <summary>少なくなった長さを1つだけ補充する (毎フレーム呼ぶ。1フレームに1つの長さまでにして、重くならないようにする)。補充したら true</summary>
        public bool TopUp()
        {
            foreach (var pair in stocks)
                if (pair.Value.Count <= lowCount)
                {
                    Refill(pair.Value, pair.Key, refillCount - pair.Value.Count);
                    return true;
                }
            return false;
        }

        Queue<int[]> QueueFor(int length)
        {
            if (!stocks.TryGetValue(length, out var q))
            {
                q = new Queue<int[]>();
                stocks.Add(length, q);
            }
            return q;
        }

        void Refill(Queue<int[]> q, int length, int count)
        {
            for (int i = 0; i < count; i++) q.Enqueue(Draw(length));
        }

        // 1小節分を抽選する
        int[] Draw(int length)
        {
            // 初めに全休符を入れるか。入れる時は、その小節は全休符1つだけ (フェルマータも付けない。MusicStaff が小節の真ん中に置く)
            if (wholeRest != null && Random.value < wholeRestChance)
                return new[] { wholeRest.Value.kind };

            // 小節の最後にフェルマータを付けるか (付ける時は、そのぶん音符に使える幅が減る)
            bool withFermata = fermata != null && Random.value < fermataChance;
            float limit = maxWidth - (withFermata ? fermata.Value.width : 0f);

            int[] last = null;
            for (int tries = 0; tries < MaxTries; tries++)
            {
                picked.Clear();
                int remaining = length;
                float width = 0f;
                bool ok = true;
                while (remaining > 0)
                {
                    // 残りの長さ以下の音符だけを候補にして、その中からランダムに選ぶ
                    fits.Clear();
                    for (int i = 0; i < candidates.Count; i++)
                        if (candidates[i].length > 0 && candidates[i].length <= remaining) fits.Add(i);
                    if (fits.Count == 0) { ok = false; break; }
                    var c = candidates[fits[Random.Range(0, fits.Count)]];
                    picked.Add(c.kind);
                    remaining -= c.length;
                    width += c.width;
                }
                if (!ok) continue;
                last = picked.ToArray();
                if (maxWidth <= 0f || width <= limit) break;
            }
            // どうしてもぴったりにならない時 (候補の長さの組み合わせで作れない時) は、全休符があれば全休符、なければ空の小節
            if (last == null) return wholeRest != null ? new[] { wholeRest.Value.kind } : new int[0];
            return WithFermata(last, withFermata);
        }

        int[] WithFermata(int[] kinds, bool withFermata)
        {
            if (!withFermata || kinds.Length == 0) return kinds;
            var result = new int[kinds.Length + 1];
            kinds.CopyTo(result, 0);
            result[kinds.Length] = fermata.Value.kind;
            return result;
        }
    }
}
