using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// カジノモードの大きなスロットマシン (壇の上)。撃つと横のレバーが下がり、3つのリールが回って左から順に止まる。
    /// 出目は撃った瞬間に決まり、止まった時に GameManager へ点数を知らせる (Result)。点数と演出の強さは出目で決まる:
    ///   7 7 7 … JACKPOT (一番高い)、BAR / BELL / CHERRY / STAR がそろう … BIG WIN / WIN、2つだけそろう … 少し、そろわない … ほんの少し。
    /// 回っている間は周りの電球が速く流れ、当たると点滅する。
    /// rows = 3 の時は 3×3 のマスが見える台: 横3本と斜め2本の5本の線のうち、一番よい線の出目で点数が決まり、その線が光る。
    /// </summary>
    public class SlotMachine : MonoBehaviour
    {
        public enum Tier { Miss, Pair, Win, BigWin, Jackpot }

        [System.Serializable]
        public class ReelStrip
        {
            public string[] symbols;
        }

        [Tooltip("スロット全体の当たり判定 (kind = SlotMachine, stayUpOnHit。visual は空のオブジェクト)")]
        public Target target;
        [Tooltip("レバー (根元が軸。ローカル X まわりに、プレイヤーの方 (-Z) へお辞儀する)")]
        public Transform lever;
        [Tooltip("「SHOOT!」の目印を置く所 (レバーの玉の真上)")]
        public Transform leverHint;
        [Tooltip("3つのリール (ローカル X まわりに回る。絵柄は symbols の順に、等間隔でぐるりと並べてある)")]
        public Transform[] reels;
        [Tooltip("リールに並んだ絵柄 (上から順。リールの1周分)")]
        public string[] symbols = { "7", "BAR", "STAR", "BELL", "CHERRY", "BAR", "STAR", "CHERRY" };
        [Tooltip("見える段の数 (1 = 真ん中の1段だけ、3 = 3×3 のマス)")]
        public int rows = 1;
        [Tooltip("リールごとの絵柄の並び (空ならどのリールも symbols。長さは symbols と同じにする)")]
        public ReelStrip[] strips;
        [Tooltip("絵柄の文字を見せる、窓の正面からの角度")]
        public float visibleAngle = 40f;
        [Tooltip("当たりの線 (rows = 3 の時。Lines の順: 真ん中・上・下・左上から右下・左下から右上)。当たった線だけ点滅する")]
        public Renderer[] lineMarks;
        [Tooltip("周りの電球 (順番に並べる)")]
        public Renderer[] lamps;
        public Material lampOn;
        public Material lampOff;
        [Tooltip("当たった時に点滅する電球の色 (JACKPOT は虹色の順に変わる)")]
        public Material[] winMaterials;

        [Header("Timing")]
        public float spinSpeed = 900f;   // 度/秒
        [Tooltip("左のリールが止まり始めるまでの秒数。次のリールはここから stopGap 秒ずつ遅れる")]
        public float firstStop = 0.6f;
        public float stopGap = 0.3f;
        public float stopTime = 0.3f; // (撃ってから 0.6 + 0.3 × 2 + 0.3 = 1.5 秒で出目が決まる)

        /// <summary>リールが全部止まった (このスロット, 出目の点数, 出目の強さ, 出目の文字)</summary>
        public event System.Action<SlotMachine, int, Tier, string> Result;
        public bool IsSpinning { get; private set; }

        float spinStart;
        float[] angle;
        float[] stopFrom, stopTo;
        bool[] stopping, stopped;
        int[] result;
        int resultPoints;
        Tier resultTier;
        float leverT = 1f;
        float lampTimer;
        int lampStep;
        float winUntil;
        Tier winTier;
        int winLine = -1;
        float lineUntil;

        float Step => 360f / Mathf.Max(1, symbols.Length);

        // 3×3 の当たりの線 (リールごとの段。0 = 上、1 = 真ん中、2 = 下)
        static readonly int[][] Lines = { new[] { 1, 1, 1 }, new[] { 0, 0, 0 }, new[] { 2, 2, 2 }, new[] { 0, 1, 2 }, new[] { 2, 1, 0 } };

        // リールごとの、絵柄の文字 (面の順)。窓の正面近くに来た文字だけ見せる (文字は裏からも透けて見えるので、リールの上や裏に回った文字は隠す)
        Renderer[][] symbolTexts;

        void Awake()
        {
            angle = new float[reels.Length];
            stopFrom = new float[reels.Length];
            stopTo = new float[reels.Length];
            stopping = new bool[reels.Length];
            stopped = new bool[reels.Length];
            result = new int[reels.Length];
            symbolTexts = new Renderer[reels.Length][];
            for (int i = 0; i < reels.Length; i++)
            {
                symbolTexts[i] = new Renderer[reels[i].childCount];
                for (int s = 0; s < reels[i].childCount; s++)
                {
                    var tm = reels[i].GetChild(s).GetComponentInChildren<TextMesh>(true);
                    symbolTexts[i][s] = tm != null ? tm.GetComponent<Renderer>() : null;
                }
            }
            for (int i = 0; i < reels.Length; i++) SetReel(i, -Random.Range(0, symbols.Length) * Step);
            ShowLine(-1);
            target.SetAvailable(false);
        }

        public void Activate()
        {
            target.SetAvailable(true);
            target.Raise(9999f);
        }

        public void Deactivate()
        {
            target.SetAvailable(false);
            IsSpinning = false;
            winUntil = 0f;
            lineUntil = 0f;
            ShowLine(-1);
        }

        /// <summary>レバーを引いて回す (撃たれた時)。回っている間は false</summary>
        public bool Pull()
        {
            if (IsSpinning) return false;
            IsSpinning = true;
            spinStart = Time.time;
            leverT = 0f;
            for (int i = 0; i < reels.Length; i++) stopping[i] = stopped[i] = false;
            lineUntil = 0f;
            ShowLine(-1);
            if (rows >= 3) DecideGrid();
            else Decide();
            Sfx.Play("lever");
            return true;
        }

        // 出目を決める: 強さを確率で選んでから、その強さになる絵柄をリールごとに選ぶ
        void Decide()
        {
            string triple = ChooseTier();
            if (triple != null)
            {
                for (int i = 0; i < reels.Length; i++) result[i] = RandomIndexOf(i, triple);
                return;
            }
            if (resultTier == Tier.Pair)
            {   // 2つだけそろう (7 以外。7 が2つだと惜しすぎて紛らわしいので)
                string pair = PairSymbols[Random.Range(0, PairSymbols.Length)];
                int odd = Random.Range(0, reels.Length);
                for (int i = 0; i < reels.Length; i++) result[i] = i == odd ? RandomIndexNot(i, pair) : RandomIndexOf(i, pair);
                return;
            }
            // そろわない: 全部ちがう絵柄
            for (int i = 0; i < reels.Length; i++)
            {
                int pick;
                int guard = 0;
                do { pick = Random.Range(0, symbols.Length); guard++; }
                while (guard < 50 && SameAsBefore(pick, i));
                result[i] = pick;
            }
        }

        // 強さを確率で選ぶ (resultTier と resultPoints を決める)。3つそろう時は、その絵柄を返す
        string ChooseTier()
        {
            float r = Random.value;
            string triple = null;
            if (r < 0.06f) { resultTier = Tier.Jackpot; triple = "7"; resultPoints = 3000; }
            else if (r < 0.14f) { resultTier = Tier.BigWin; triple = "BAR"; resultPoints = 1500; }
            else if (r < 0.24f) { resultTier = Tier.BigWin; triple = "BELL"; resultPoints = 1000; }
            else if (r < 0.36f) { resultTier = Tier.Win; triple = "CHERRY"; resultPoints = 500; }
            else if (r < 0.50f) { resultTier = Tier.Win; triple = "STAR"; resultPoints = 300; }
            else if (r < 0.75f) { resultTier = Tier.Pair; resultPoints = 50; }
            else { resultTier = Tier.Miss; resultPoints = 10; }
            return triple;
        }

        static int TriplePoints(string s) => s switch { "7" => 3000, "BAR" => 1500, "BELL" => 1000, "CHERRY" => 500, _ => 300 };
        static Tier TripleTier(string s) => s switch { "7" => Tier.Jackpot, "BAR" => Tier.BigWin, "BELL" => Tier.BigWin, _ => Tier.Win };

        // 3×3 の台: 強さと、当てる線を選んでから、そうなるマスを探す (ほかの線が勝手にそろわないように何度か選び直す)。
        // 点数は最後に盤面を見て、一番よい線で決める (見えている出目と点数が必ず合う)
        void DecideGrid()
        {
            string triple = ChooseTier();
            Tier want = resultTier;
            int wantPoints = resultPoints;
            int line = Random.Range(0, Lines.Length);
            for (int attempt = 0; attempt < 600; attempt++)
            {
                var rowsOf = Lines[line];
                if (triple != null)
                    for (int i = 0; i < reels.Length; i++) result[i] = CenterFor(i, RandomIndexOf(i, triple), rowsOf[i]);
                else if (want == Tier.Pair)
                {
                    string pair = PairSymbols[Random.Range(0, PairSymbols.Length)];
                    int odd = Random.Range(0, reels.Length);
                    for (int i = 0; i < reels.Length; i++)
                        result[i] = CenterFor(i, i == odd ? RandomIndexNot(i, pair) : RandomIndexOf(i, pair), rowsOf[i]);
                }
                else
                    for (int i = 0; i < reels.Length; i++) result[i] = Random.Range(0, symbols.Length);

                EvaluateGrid(out int points, out Tier tier, out int best, out int wins);
                resultPoints = points;
                resultTier = tier;
                winLine = best;
                if (points == wantPoints && wins <= 1) return;
            }
        }

        // 盤面の5本の線を見て、一番よい線を選ぶ (wins = 3つそろった線の数)
        void EvaluateGrid(out int points, out Tier tier, out int best, out int wins)
        {
            points = 0;
            tier = Tier.Miss;
            best = -1;
            wins = 0;
            for (int l = 0; l < Lines.Length; l++)
            {
                string a = GridSymbol(0, Lines[l][0]), b = GridSymbol(1, Lines[l][1]), c = GridSymbol(2, Lines[l][2]);
                int p;
                Tier t;
                if (a == b && b == c) { p = TriplePoints(a); t = TripleTier(a); wins++; }
                else if (a == b || b == c || a == c) { p = 50; t = Tier.Pair; }
                else { p = 10; t = Tier.Miss; }
                if (p > points) { points = p; tier = t; best = l; }
            }
            if (tier == Tier.Miss) best = -1;
        }

        // 段 (0 = 上、1 = 真ん中、2 = 下) に見えている絵柄。上の段は、真ん中の次の面 (絵柄は上から下へ流れる)
        string GridSymbol(int reel, int row) => Symbol(reel, Wrap(result[reel] + 1 - row));
        // その段に index の絵柄が来るための、真ん中の面
        int CenterFor(int reel, int index, int row) => Wrap(index - 1 + row);
        int Wrap(int i) => (i % symbols.Length + symbols.Length) % symbols.Length;

        string Symbol(int reel, int index)
        {
            var strip = strips != null && reel < strips.Length && strips[reel] != null && strips[reel].symbols != null && strips[reel].symbols.Length == symbols.Length
                ? strips[reel].symbols : symbols;
            return strip[index];
        }

        static readonly string[] PairSymbols = { "BAR", "BELL", "CHERRY", "STAR" };

        bool SameAsBefore(int pick, int count)
        {
            for (int j = 0; j < count; j++) if (Symbol(j, result[j]) == Symbol(count, pick)) return true;
            return false;
        }

        int RandomIndexOf(int reel, string s)
        {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < symbols.Length; i++) if (Symbol(reel, i) == s) list.Add(i);
            return list.Count > 0 ? list[Random.Range(0, list.Count)] : 0;
        }

        int RandomIndexNot(int reel, string s)
        {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < symbols.Length; i++) if (Symbol(reel, i) != s) list.Add(i);
            return list[Random.Range(0, list.Count)];
        }

        // 当たった線だけを見せる (-1 = 全部隠す)
        void ShowLine(int line)
        {
            if (lineMarks == null) return;
            for (int i = 0; i < lineMarks.Length; i++) if (lineMarks[i] != null) lineMarks[i].enabled = i == line;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // レバー: プレイヤーの方へぐっとお辞儀して、少し止まってから、ばねのように揺れながら起き上がる
            if (leverT < 1f)
            {
                leverT = Mathf.Min(1f, leverT + dt / 0.9f);
                float down;
                if (leverT < 0.15f) down = Mathf.SmoothStep(0f, 1f, leverT / 0.15f);
                else if (leverT < 0.3f) down = 1f;
                else
                {
                    float u = (leverT - 0.3f) / 0.7f;
                    down = Mathf.Cos(u * Mathf.PI * 2.5f) * (1f - u) * (1f - u); // 起き上がって、少し行き過ぎて戻る
                }
                lever.localRotation = Quaternion.Euler(-75f * down, 0f, 0f);
            }

            if (IsSpinning) UpdateReels(dt);
            UpdateLamps(dt);
            UpdateLine();
        }

        void UpdateReels(float dt)
        {
            float t = Time.time - spinStart;
            bool all = true;
            for (int i = 0; i < reels.Length; i++)
            {
                if (stopped[i]) continue;
                all = false;
                float stopAt = firstStop + i * stopGap;
                if (!stopping[i] && t >= stopAt)
                {   // 止まる絵柄の角度 (少なくとも1周は回ってから)
                    stopping[i] = true;
                    stopFrom[i] = angle[i];
                    float targetAngle = Mathf.Repeat(-result[i] * Step, 360f);
                    float delta = Mathf.Repeat(Mathf.Repeat(stopFrom[i], 360f) - targetAngle, 360f) + 360f;
                    stopTo[i] = stopFrom[i] - delta; // 絵柄が上から下へ流れる向きに回る
                }
                if (stopping[i])
                {
                    float k = Mathf.Clamp01((t - stopAt) / stopTime);
                    float e = 1f - Mathf.Pow(1f - k, 3f);
                    SetReel(i, Mathf.Lerp(stopFrom[i], stopTo[i], e));
                    if (k >= 1f)
                    {
                        stopped[i] = true;
                        Sfx.Play("reelstop");
                    }
                }
                else SetReel(i, angle[i] - spinSpeed * dt);
            }
            if (!all) return;
            IsSpinning = false;
            winTier = resultTier;
            winUntil = resultTier >= Tier.Win ? Time.time + (resultTier == Tier.Jackpot ? 4f : 2.5f) : 0f;
            string face = "";
            if (rows >= 3)
            {   // 一番よい線の出目 (そろわない時は真ん中の線)。当たった線は点滅する
                var rowsOf = Lines[Mathf.Max(0, winLine)];
                for (int i = 0; i < reels.Length; i++) face += (i > 0 ? " " : "") + GridSymbol(i, rowsOf[i]);
                lineUntil = winLine >= 0 ? Time.time + (resultTier == Tier.Jackpot ? 4f : 2.5f) : 0f;
            }
            else
                for (int i = 0; i < reels.Length; i++) face += (i > 0 ? " " : "") + Symbol(i, result[i]);
            Result?.Invoke(this, resultPoints, resultTier, face);
        }

        void SetReel(int i, float a)
        {
            angle[i] = a;
            reels[i].localRotation = Quaternion.Euler(a, 0f, 0f);
            var texts = symbolTexts[i];
            for (int s = 0; s < texts.Length; s++)
                if (texts[s] != null) texts[s].enabled = Mathf.Abs(Mathf.DeltaAngle(0f, a + s * Step)) < visibleAngle;
        }

        // 当たった線の点滅 (当たりの後しばらく)
        void UpdateLine()
        {
            if (lineMarks == null || lineMarks.Length == 0) return;
            bool on = Time.time < lineUntil && Mathf.Repeat(Time.time, 0.3f) < 0.2f;
            ShowLine(on ? winLine : -1);
        }

        // 電球: ふだんはゆっくり、回っている間は速く流れる。当たると全部が点滅する (JACKPOT は色が変わる)
        void UpdateLamps(float dt)
        {
            if (lamps == null || lamps.Length == 0) return;
            bool win = Time.time < winUntil;
            float interval = win ? 0.1f : IsSpinning ? 0.05f : 0.25f;
            lampTimer += dt;
            if (lampTimer < interval) return;
            lampTimer = 0f;
            lampStep++;
            for (int i = 0; i < lamps.Length; i++)
            {
                Material m;
                if (win)
                {
                    bool on = lampStep % 2 == 0;
                    m = !on ? lampOff
                      : winTier == Tier.Jackpot && winMaterials != null && winMaterials.Length > 0 ? winMaterials[(i + lampStep) % winMaterials.Length]
                      : lampOn;
                }
                else m = (i + lampStep) % 3 == 0 ? lampOn : lampOff;
                lamps[i].sharedMaterial = m;
            }
        }
    }
}
