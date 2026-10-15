using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 音楽モードの、空中に浮かぶ五線譜。ステージの手前 (真ん中より少し外側) の決まった所から、プレイヤーの横を通り少し後ろまで伸びる帯。
    /// 帯はまっすぐに近いなめらかな線で、上り・下りの角度がついている (ConcertHall が終わりの高さを決める)。ゆるい波がゆっくり流れるが、出てくる所は動かない。
    /// 記号は始まりの縦線・ト音記号かヘ音記号・調号 (シャープかフラットを 0〜4 個、記号に合った正しい線と間に)・拍子記号。
    /// 記号はプレイヤーの方を向くので、左右で見た目の読む向きが変わる。どちらも見た目で左から右に読めるように、左右で流し方を変える (Leading):
    /// - プレイヤーから見て左の五線譜 (Leading): 楽譜と同じ順に流れる。先頭 (いちばん先に出てくる) に記号が並び、その後ろに 1小節目・2小節目… と続く。
    ///   全部が同じ速さで流れ、帯は先頭の縦線のところまで伸びる。流し終わったら最後の小節線が帯の後ろの端になって流れていき、終わりで消える (Tail)。
    /// - 右の五線譜: 記号は始まりに止まっていて、帯が伸びて現れたら (Reveal)、記号の後ろから小節を次々に流し出す (Play)。
    ///   流し終わったら (Drain)、記号ごと帯の終わりの方へ流れていって消える (Retract)。
    /// モードの終わりは、帯が始まりの方から消えていく (Retract)。
    /// 音符 (MusicNote) は五線の線か間 (下から 0〜8 の9か所) に乗って、ステージからプレイヤーの方へ流れてくる。音符が的
    /// </summary>
    public class MusicStaff : MonoBehaviour
    {
        [Tooltip("五線 (下から5本) / 小節線")]
        public LineRenderer[] lines;
        public LineRenderer[] barLines;
        [Tooltip("流す音符 (使い回す)")]
        public MusicNote[] notes;
        [Tooltip("ト音記号 / ヘ音記号 (原点が真ん中の線。出すたびにどちらか)")]
        public Transform trebleClef;
        public Transform bassClef;
        [Tooltip("調号のシャープ / フラット (原点がその音の線か間の高さ。使い回す。4個ずつ)")]
        public Transform[] sharps;
        public Transform[] flats;
        [Tooltip("当たった音符がはじける時の光")]
        public Material sparkMaterial;

        [Tooltip("線の間隔 (m)")]
        public float lineSpacing = 0.3f;
        public int segments = 48;
        [Tooltip("右の五線譜: 帯が伸びて現れる秒数 / 帯が始まりの方から消えていく秒数 (左右とも、モードの終わりにも使う)")]
        public float revealSeconds = 2f; // 名前を変えて、シーンに保存された前の値 (revealTime 3 など) を使わない
        public float retractSeconds = 1.8f;
        [Tooltip("小節を出し続ける秒数 (過ぎたら、その時の小節で終わる) / 記号・音符が端から端まで流れる秒数 (1本の五線譜はみな同じ速さ)")]
        public float notesSeconds = 6f;
        public float travelSeconds = 6.5f; // ステージの端からプレイヤーの後ろへ回り込むまで (約 25m) を 6.5 秒
        [Tooltip("音符1つが小節の中で最低限とる幅 (m)。横に長い音符 (タイでつないだ物など) はそのぶん足す。小節の幅に入りきらない並びは抽選し直す")]
        public float minNoteGap = 0.8f;
        [Tooltip("小節線と、小節のいちばん端の音符の幅のふちとのすき間 (m)")]
        public float barPadding = 0.1f;
        [Tooltip("抽選した小節のストック: 一度に補充する小節の数 / 残りがこれ以下になったら補充する (拍子の長さごと)")]
        public int stockRefill = 8;
        public int stockLow = 3;
        [Range(0f, 1f), Tooltip("小節の抽選の初めに、全休符だけの小節にする確率")]
        public float wholeRestChance = 0.1f;
        [Range(0f, 1f), Tooltip("小節の最後にフェルマータを付ける確率")]
        public float fermataChance = 0.1f;
        [Tooltip("音符の当たり判定の大きさの倍率 (ビルダーで作った大きさに掛ける。見た目は変わらない)")]
        public float noteHitScale = 1.35f;
        [Tooltip("始める前に用意しておく、記号の種類ごとの数。足りなくなったら1小節前に複製して増やす")]
        public int prewarmPerKind = 2;
        [Tooltip("帯がプレイヤーの横を回る所の、プレイヤーからの横の距離 (m)。ここから後ろへ回り込む")]
        public float aroundRadius = 6.5f;
        [Tooltip("流れる小節線の間隔 (m。拍子に関係なく一定)。音符と同じ速さで流れる")]
        public float barSpacing = 6f;
        [Tooltip("拍子記号 (原点が真ん中の線。出すたびにどれか1つ)。並びは 4分の2・4分の3・4分の4・C・8分の3・8分の6・2分の2・C に縦線")]
        public Transform[] timeSignatures;
        [Range(0f, 1f), Tooltip("金色の音符 (高得点) になる確率")]
        public float goldChance = 0.12f;
        [Range(0f, 1f), Tooltip("線の濃さ: 始まり (ステージ側) / 終わり。間はなめらかに変わる")]
        public float startAlpha = 0.25f;
        [Range(0f, 1f)] public float endAlpha = 1f;

        // 先頭の縦線から後ろへの距離 (m): 音部記号の真ん中 / 調号の1つ目 / 調号の間隔 / 拍子記号から1小節目までのすき間
        const float ClefAt = 0.4f, KeyAt = 0.9f, KeyStep = 0.33f, NoteGap = 0.35f; // KeyStep はシャープの幅 (線の間隔 1 つ分) より少し広く
        const float TimeHalfW = 0.22f; // 拍子記号の幅の半分 (m)

        // 調号の並ぶ順と、ト音記号の五線での位置 (下の線 = 0 〜 上の線 = 8、9 は上の線のすぐ上)。ヘ音記号は2つ下
        static readonly int[] SharpSlots = { 8, 5, 9, 6, 3, 7, 4 }; // ファ ド ソ レ ラ ミ シ
        static readonly int[] FlatSlots = { 4, 7, 3, 6, 2, 5, 1 };  // シ ミ ラ レ ソ ド ファ

        // 拍子記号ごとの1小節の長さ (32分音符が何個分か)。timeSignatures と同じ並び: 2/4・3/4・4/4・C・3/8・6/8・2/2・C に縦線
        static readonly int[] TimeSigLength = { 16, 24, 32, 32, 12, 24, 32, 32 };

        enum Phase { Idle, Reveal, Play, Drain, Tail, Retract } // Reveal・Drain は右の五線譜、Tail は左の五線譜だけ
        Phase phase = Phase.Idle;
        float phaseStart;
        Vector3 p0, p1, p2, p3; // 通り道 (3次ベジェ曲線。この五線譜のローカル)
        Vector3 focus;          // 帯の面を向ける所 (プレイヤーの頭)
        float amp, waves, wavePhase, uMin, uMax, pathLen = 1f;
        float retractFrom;      // 帯が始まりの方から消え始めた時の後ろの端 (u)
        float shift;            // 右の五線譜: 終わりに、記号ごと帯の終わりへ流れていく量 (u)
        // 流れ始めてから進んだ距離 (m)。左の五線譜は先頭の縦線が始まりから進んだ距離、右の五線譜は音符を流し始めてから記号の後ろ (FlowStartU) から進んだ距離。
        // 流れの出口 (FlowStartU) から距離 s の所に出てくる物は、head = s の時に出てくる。
        // 左: 1小節目の始まり (拍子記号の後ろ) は head = headerLen、小節 m の始まりは headerLen + m × BarLen、その後ろの小節線 (番号 m+1) は headerLen + (m+1) × BarLen。
        // 右: 小節線 k は head = k × BarLen で出てくる。小節 m は小節線 m (先に出る) と m+1 の間で、小節の始まりは記号の側 (あとから出てくる小節線 m+1 の側)
        float head;
        bool flowing;           // head が進んでいるか (右の五線譜は帯が伸びきってから)
        float headerLen;        // 始まりの縦線から1小節目の始まりまでの長さ (m。音部記号・調号・拍子記号のぶん)
        int nextMeasure;        // 次に音符を並べる小節の番号 (0 = 1小節目)
        int lastBar;            // 最後に出す小節線の番号 (流し終わったら、最後の小節の後ろの線で止める)
        // 記号を先頭にして楽譜の順に流すか (プレイヤーから見て左の五線譜)。右の五線譜は記号を始まりに止めて、その後ろから小節を流し出す
        bool Leading => Side < 0f;
        // 音符・小節線が出てくる所 (u)。左は帯の始まり、右は記号の後ろ
        float FlowStartU => Leading ? 0f : headerLen / pathLen;

        // 音符の使い回し (notes と、足りない時に増やした複製) と、種類ごとの見本
        readonly List<MusicNote> pool = new List<MusicNote>();
        readonly Dictionary<int, MusicNote> templates = new Dictionary<int, MusicNote>();
        MeasureStock stock;
        // 並べたがまだ出していない音符 (head が at になったら、始まりから流し始める)
        struct Pending { public MusicNote note; public float at; }
        readonly List<Pending> pending = new List<Pending>();
        bool treble;
        int keyCount, lastSlot = -1;
        int timeSig = -1;       // 出している拍子記号 (timeSignatures の番号。なければ -1)
        // 拍子記号の真ん中 (先頭の縦線からの距離 m): 調号の後ろ。調号がなければ音部記号のすぐ後ろ
        float TimeAt => keyCount > 0 ? KeyAt + keyCount * KeyStep + 0.13f : KeyAt;
        // 小節線の間隔 (m。拍子に関係なく一定)
        float BarLen => Mathf.Max(0.5f, barSpacing);
        // 今の拍子の1小節の長さ (32分音符が何個分か)。拍子記号がなければ 4分の4
        int MeasureLength => timeSig >= 0 && timeSig < TimeSigLength.Length ? TimeSigLength[timeSig] : 32;
        bool keySharp;
        readonly Vector3[] pts = new Vector3[65];

        public bool IsBusy => phase != Phase.Idle;
        // 流れる速さ (u / 秒)。記号も小節線も音符も同じ速さで、出てくる所から終わりまでを travelSeconds 秒で流れる
        float NoteSpeed => (1f - FlowStartU) / Mathf.Max(1f, travelSeconds);
        /// <summary>出てきた側 (-1 = 客席から見て左 / 1 = 右)</summary>
        public float Side { get; private set; }
        /// <summary>段 (0〜1。ConcertHall が、同じ側のほかの五線譜と重ならないように割り当てる。大きいほど奥・高い所を通る)</summary>
        public float Level { get; set; } = -1f;

        public void Init()
        {
            if (pool.Count == 0) BuildPool();
            foreach (var n in pool) n.Hide();
            pending.Clear();
            HideMarks();
            phase = Phase.Idle;
            gameObject.SetActive(false);
        }

        // 音符の使い回しの準備: 種類ごとの見本を覚え、種類ごとに prewarmPerKind 個まで複製しておく。
        // 抽選の候補 (種類と長さと幅) を作り、どの拍子の小節も先に何小節分か抽選してためておく
        void BuildPool()
        {
            templates.Clear();
            foreach (var n in notes)
            {
                if (n == null) continue;
                // 当たり判定 (Hit。見た目のない Collider だけの物) を大きくする。複製はこの後に作るので、同じ大きさを受け継ぐ
                foreach (var col in n.GetComponentsInChildren<Collider>(true)) col.transform.localScale *= noteHitScale;
                pool.Add(n);
                if (!templates.ContainsKey(n.kind)) templates.Add(n.kind, n);
            }
            var candidates = new List<MeasureStock.Candidate>();
            MeasureStock.Candidate? wholeRest = null, fermata = null;
            foreach (var pair in templates)
            {
                int have = 0;
                foreach (var n in pool) if (n.kind == pair.Key) have++;
                for (; have < prewarmPerKind; have++) Clone(pair.Value);
                var c = new MeasureStock.Candidate { kind = pair.Key, length = pair.Value.length32, width = NoteWidth(pair.Value) };
                switch (pair.Value.role)
                {
                    case MusicNote.MeasureRole.WholeRest: wholeRest = c; break;
                    case MusicNote.MeasureRole.Fermata: fermata = c; break;
                    default: if (c.length > 0) candidates.Add(c); break;
                }
            }
            if (candidates.Count == 0)
                Debug.LogWarning($"{name}: 音符の長さ (MusicNote.length32) がありません。ShootingGallery を「作り直す」してください");
            else if (templates.Count == 1 && notes.Length > 1) // 作り直す前の音符は種類がみな 0 (4分音符) になり、見た目と長さが合わない
                Debug.LogWarning($"{name}: 音符の種類 (MusicNote.kind) が入っていません。小節の長さが合わなくなるので、ShootingGallery を「作り直す → 貼り直し」してください");
            stock = new MeasureStock(candidates, wholeRest, wholeRestChance, fermata, fermataChance, BarLen - 2f * barPadding, stockRefill, stockLow);
            foreach (int len in TimeSigLength) stock.Prepare(len);
        }

        MusicNote Clone(MusicNote src)
        {
            var go = Instantiate(src.gameObject, src.transform.parent);
            go.name = src.name + "_Copy";
            var n = go.GetComponent<MusicNote>();
            n.Hide();
            pool.Add(n);
            return n;
        }

        // 音符が小節の中で最低限とる幅 (m)
        float NoteWidth(MusicNote n) => minNoteGap + 2f * n.extraHalfWidth;

        // その種類の空いている音符。なければ複製して増やす
        MusicNote FreeNote(int kind)
        {
            foreach (var n in pool) if (n.kind == kind && !n.inUse && !n.queued) return n;
            return templates.TryGetValue(kind, out var t) ? Clone(t) : null;
        }

        /// <summary>
        /// 五線譜を出す。start (ステージの奥) から end (プレイヤーの横を通り過ぎた後ろ) まで、なめらかに伸びる (この五線譜のローカル)。
        /// 帯の面はいつも focusPoint (プレイヤーの頭) の方を向き、まわりをやわらかく包むように傾く。音符は start から end へ流れる
        /// </summary>
        public void Begin(Vector3 start, Vector3 end, Vector3 focusPoint)
        {
            Side = Mathf.Sign(start.x);
            focus = focusPoint;
            p0 = start;
            p3 = end;
            // 通り道: ステージの端から手前へ伸び、プレイヤーの横の外側 (around) を回って、後ろへ巻き込むように終わりへ (プレイヤーに近づきすぎない)。
            // 高さは始まりから終わりへなめらかに変わる (角度は start と end の高さの差でつく)
            var around = new Vector3(Side * aroundRadius, Mathf.Lerp(start.y, end.y, 0.7f), -2.5f);
            p1 = Vector3.Lerp(start, around, 0.55f); // 始まりより外へはふくらませない (舞台の両わきの幕にかからないように)
            p2 = around;
            amp = Random.Range(0.05f, 0.1f);   // ゆるい波
            waves = Random.Range(1f, 1.6f);
            wavePhase = Random.value;
            uMin = uMax = 0f;
            shift = 0f;
            retractFrom = 0f;
            head = 0f;
            flowing = Leading; // 左はすぐ流れ始める。右は帯が伸びきってから
            nextMeasure = 0;
            lastBar = int.MaxValue;
            ClearPending();
            pathLen = 0f;
            var prev = Bezier(0f);
            for (int i = 1; i <= 32; i++) { var p = Bezier(i / 32f); pathLen += (p - prev).magnitude; prev = p; }
            pathLen = Mathf.Max(1f, pathLen);

            // 音部記号と調号を選ぶ (シャープかフラットを 0〜4 個)
            treble = Random.value < 0.6f;
            keyCount = Random.Range(0, 5);
            keySharp = Random.value < 0.5f;
            timeSig = timeSignatures != null && timeSignatures.Length > 0 ? Random.Range(0, timeSignatures.Length) : -1;
            // 1小節目は拍子記号の後ろから (拍子記号がなければ調号、調号もなければ音部記号の後ろから)
            if (timeSig >= 0) headerLen = TimeAt + TimeHalfW + NoteGap;
            else headerLen = keyCount > 0 ? KeyAt + keyCount * KeyStep + NoteGap : ClefAt + 0.45f;

            foreach (var n in pool) n.Hide();
            HideMarks();
            gameObject.SetActive(true);
            SetPhase(Leading ? Phase.Play : Phase.Reveal);
            UpdateLines();
            UpdateMarks();
        }

        /// <summary>モードの終わり: 音符をすぐに消し、五線譜は流れながら始まりの方から消えていく</summary>
        public void Stop()
        {
            if (phase == Phase.Idle) return;
            foreach (var n in pool) if (n.inUse) n.Hide();
            ClearPending();
            lastBar = Mathf.Min(lastBar, NewestBar()); // もう新しい小節線は出さない
            retractFrom = uMin;
            if (phase != Phase.Retract) SetPhase(Phase.Retract);
        }

        public void HideInstant()
        {
            foreach (var n in pool) n.Hide();
            ClearPending();
            HideMarks();
            phase = Phase.Idle;
            gameObject.SetActive(false);
        }

        void SetPhase(Phase p)
        {
            phase = p;
            phaseStart = Time.time;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time - phaseStart;
            wavePhase += dt * 0.12f; // 波がゆっくり線に沿って流れる
            if (phase == Phase.Idle) return;
            if (flowing) head += NoteSpeed * pathLen * dt; // 記号・小節線・音符はみな同じ速さ (m/秒) で流れる
            if (Leading) uMax = Mathf.Min(1f, head / pathLen); // 左: 帯は先頭の縦線のところまで伸びている
            stock?.TopUp(); // 抽選のストックが少なくなっていたら補充 (1フレームに1つの拍子の長さまで)

            switch (phase)
            {
                case Phase.Reveal: // 右: 帯が始まりから伸びて現れ、伸びきったら記号の後ろから小節を流し始める
                    uMax = Smooth(t / revealSeconds);
                    if (t >= revealSeconds)
                    {
                        uMax = 1f;
                        flowing = true;
                        head = 0f;
                        SetPhase(Phase.Play);
                    }
                    break;
                case Phase.Play:
                    // 1小節先まで音符を並べておく (足りない音符の複製も、流す1小節前に済ませる)。小節を出し続ける時間が過ぎたら、最後の小節の後ろの線で終わる
                    if (t < notesSeconds)
                        while (head + BarLen >= MeasureStartAt(nextMeasure)) ScheduleMeasure();
                    else if (lastBar == int.MaxValue) lastBar = nextMeasure;
                    SpawnPending();
                    if (lastBar != int.MaxValue && pending.Count == 0) SetPhase(Leading ? Phase.Tail : Phase.Drain);
                    break;
                case Phase.Drain: // 右: 音符が流れきったら、記号ごと帯の終わりへ流れていく
                    if (!AnyNote() || t >= travelSeconds + 1f)
                    {
                        retractFrom = 0f;
                        SetPhase(Phase.Retract);
                    }
                    break;
                case Phase.Tail:
                    // 最後の小節線が帯の後ろの端になって流れていき、終わりまで行ったら消える
                    uMin = Mathf.Max(uMin, BarU(lastBar));
                    if (uMin >= 1f)
                    {
                        HideInstant();
                        return;
                    }
                    break;
                case Phase.Retract:
                    // 帯が始まりの方から消えていく。右の五線譜は、始まりに止まっていた記号も帯の終わりへ流れていく
                    float k = Smooth(t / retractSeconds);
                    uMin = Mathf.Lerp(retractFrom, 1f, k);
                    if (!Leading) shift = k;
                    if (t >= retractSeconds)
                    {
                        HideInstant();
                        return;
                    }
                    break;
            }
            UpdateNotes(dt);
            UpdateLines();
            UpdateMarks();
        }

        bool AnyNote()
        {
            foreach (var n in pool) if (n.inUse) return true;
            return false;
        }

        // ---- 小節 ----

        // 小節 m の、先に出てくる側の端が出てくる head。左は小節の始まり (1小節目は拍子記号の後ろ)、右は小節線 m
        float MeasureStartAt(int m) => Leading ? headerLen + m * BarLen : m * BarLen;

        /// <summary>
        /// 次の小節 (nextMeasure 番目。前の小節線 (1小節目は拍子記号) と後ろの小節線 nextMeasure+1 の間) に、ストックから出した1小節分の音符を並べる。
        /// 音符ごとに最低限の幅 (NoteWidth) をとり、余った幅は音符の長さに比例して後ろに足す (長い音符ほど後ろが広い)。入りきらない時は全体を縮める。
        /// 小節の始まりは記号の側: 左の五線譜は記号が先頭なので、小節の中の最初の音符ほど先に出てくる。右の五線譜は記号が始まりに止まっているので、最初の音符ほど後に出てくる
        /// </summary>
        void ScheduleMeasure()
        {
            int length = MeasureLength;
            float measureStart = MeasureStartAt(nextMeasure); // この小節の先に出てくる側の端が出てくる head
            nextMeasure++;
            if (stock == null) return;
            var kinds = stock.Take(length);
            if (kinds.Length == 0) return; // 抽選できなかった (長さの組み合わせで作れない) 時は空の小節

            float avail = BarLen - 2f * barPadding;
            float need = 0f;
            foreach (int k in kinds) need += NoteWidth(templates[k]);
            float scale = need > avail ? avail / need : 1f;
            float extra = Mathf.Max(0f, avail - need);
            float x = barPadding; // 小節の始まり (記号の側の小節線) からの距離 (m)
            foreach (int k in kinds)
            {
                var tmpl = templates[k];
                float w = NoteWidth(tmpl) * scale;
                // 余った幅の分け方: 音符・休符は長さに比例して後ろに足す。全休符は小節まるごとぶんで、その真ん中に置く。フェルマータは拍がないので足さない (小節の最後に付く)
                float share = tmpl.role == MusicNote.MeasureRole.WholeRest ? extra
                            : tmpl.role == MusicNote.MeasureRole.Fermata ? 0f
                            : extra * tmpl.length32 / length;
                float center = tmpl.role == MusicNote.MeasureRole.WholeRest ? BarLen * 0.5f : x + w * 0.5f; // 全休符の小節は全休符1つだけなので、小節のちょうど真ん中
                x += w + share;
                var note = FreeNote(k);
                if (note == null) continue;
                note.queued = true;
                // 左: 小節の始まりが先に出てくるので、そこから center 進んだ時に出てくる。
                // 右: 小節の始まりは後から出てくる (measureStart + BarLen) ので、その center 前に出てくる
                pending.Add(new Pending { note = note, at = Leading ? measureStart + center : measureStart + BarLen - center });
            }
        }

        // 出す時が来た音符を流し始める (出るはずだった所からのずれを、出す位置に足す)
        void SpawnPending()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (head < p.at) continue;
                pending.RemoveAt(i);
                p.note.queued = false;
                SpawnNote(p.note, FlowStartU + (head - p.at) / pathLen);
            }
        }

        void ClearPending()
        {
            foreach (var p in pending) if (p.note != null) p.note.queued = false;
            pending.Clear();
        }

        void SpawnNote(MusicNote note, float u)
        {
            if (note == null || note.inUse) return;
            if (note.hasFixedHeight) note.slot = note.fixedSlot; // 休符・フェルマータは決まった高さ (PlaceNote)
            else
            {
                int slot;
                // 前の音符から2つ以上離れた高さにする (線と間に上下に散らばるように)
                do slot = Random.Range(0, 9); while (lastSlot >= 0 && Mathf.Abs(slot - lastSlot) < 2);
                lastSlot = slot;
                note.slot = slot;
            }
            note.u = u; // 始まりから流れ出す (記号の後ろについていく)
            note.speed = NoteSpeed;
            note.Show(Random.value < goldChance);
            PlaceNote(note);
        }

        void UpdateNotes(float dt)
        {
            foreach (var n in pool)
            {
                if (!n.inUse) continue;
                if (n.popped)
                {
                    if (Time.time - n.popTime > 0.3f) n.Hide();
                    continue;
                }
                n.u += n.speed * dt;
                if (n.u > 1f || n.u < uMin) { n.Hide(); continue; } // 帯の終わりまで流れたら消える
                PlaceNote(n);
                if (n.target.IsUp) { n.armed = true; continue; }
                if (!n.armed) continue;
                // 当たった (点数と音は GameManager → ConcertHall): 光がはじけて、縮んで消える
                n.popped = true;
                n.popTime = Time.time;
                if (sparkMaterial != null)
                {
                    Explosion.Spawn(n.transform.position, 0.35f, sparkMaterial);
                    SpaceDebris.Spawn(n.transform.position, 0.3f, sparkMaterial, 6);
                }
            }
        }

        void PlaceNote(MusicNote n)
        {
            var p = Point(n.u, n.hasFixedHeight ? n.fixedHeight : (n.slot - 4) * lineSpacing * 0.5f);
            n.transform.localPosition = p;
            // 表 (-Z) がプレイヤーの方。左右にゆらゆら首を振って、玉の丸みと厚みが見えるようにする
            float sway = Mathf.Sin(Time.time * 2.2f + n.slot * 1.3f) * 28f;
            n.transform.localRotation = Facing(n.u, p) * Quaternion.Euler(0f, sway, 0f);
        }

        // ---- 音部記号と調号 ----

        void HideMarks()
        {
            if (trebleClef != null) trebleClef.gameObject.SetActive(false);
            if (bassClef != null) bassClef.gameObject.SetActive(false);
            if (sharps != null) foreach (var s in sharps) if (s != null) s.gameObject.SetActive(false);
            if (flats != null) foreach (var f in flats) if (f != null) f.gameObject.SetActive(false);
            if (timeSignatures != null) foreach (var m in timeSignatures) if (m != null) m.gameObject.SetActive(false);
        }

        // 始まりの縦線から meters (m) の所に置く記号の位置 u。まだ出てきていない・帯の見えている所の外・終わりの近くなら -1 (出さない)。
        // 左は先頭の縦線から後ろへ (流れていく)、右は帯の始まりから先へ (止まっていて、終わりに shift で流れていく)
        float MarkU(float meters)
        {
            float u = Leading ? (head - meters) / pathLen : meters / pathLen + shift;
            return u < 0f || u < uMin || u > uMax || u > 0.98f ? -1f : u;
        }

        // 小節線 k の位置 u (まだ出てきていなければ出てくる所より手前)。左は k 小節目の後ろの線 (1 〜)、右は記号の後ろに k 番目に出てきた線 (0 〜)
        float BarU(int k) => Leading ? (head - headerLen - k * BarLen) / pathLen : FlowStartU + (head - k * BarLen) / pathLen;

        // 今までに出てきたいちばん新しい小節線の番号
        int NewestBar() => Mathf.FloorToInt((head - (Leading ? headerLen : 0f)) / BarLen);

        void UpdateMarks()
        {
            var clef = treble ? trebleClef : bassClef;
            var other = treble ? bassClef : trebleClef;
            if (other != null) other.gameObject.SetActive(false);
            PlaceMark(clef, MarkU(ClefAt), 4); // 音部記号の原点は真ん中の線 (slot 4)。ト音記号の渦は上から4本目、ヘ音記号の玉は上から2本目の線に来る
            if (timeSignatures != null) // 拍子記号: 選んだ物だけを調号の後ろに。原点は真ん中の線 (slot 4)
                for (int i = 0; i < timeSignatures.Length; i++)
                {
                    if (timeSignatures[i] == null) continue;
                    if (i == timeSig) PlaceMark(timeSignatures[i], MarkU(TimeAt), 4);
                    else timeSignatures[i].gameObject.SetActive(false);
                }
            int offset = treble ? 0 : -2; // ヘ音記号は同じ音が2つ下
            var use = keySharp ? sharps : flats;
            var unused = keySharp ? flats : sharps;
            if (unused != null) foreach (var m in unused) if (m != null) m.gameObject.SetActive(false);
            if (use == null) return;
            for (int i = 0; i < use.Length; i++)
            {
                if (use[i] == null) continue;
                if (i >= keyCount) { use[i].gameObject.SetActive(false); continue; }
                int slot = (keySharp ? SharpSlots[i] : FlatSlots[i]) + offset;
                PlaceMark(use[i], MarkU(KeyAt + i * KeyStep), slot);
            }
        }

        // 記号を u の所の、slot (下の線 = 0) の高さに置く。u < 0 なら隠す
        void PlaceMark(Transform mark, float u, int slot)
        {
            if (mark == null) return;
            bool show = u >= 0f;
            mark.gameObject.SetActive(show);
            if (!show) return;
            var p = Point(u, (slot - 4) * lineSpacing * 0.5f);
            mark.localPosition = p;
            mark.localRotation = Facing(u, p);
        }

        // 記号の向き: 表 (-Z) をプレイヤーの頭の方へ向け、記号の上を帯の「上」(BandUp) にそろえる (傾いた帯の上でも正しく読める)
        Quaternion Facing(float u, Vector3 p)
        {
            var away = p - focus;
            return away.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(away.normalized, BandUp(u)) : Quaternion.identity;
        }

        // ---- 線 ----

        void UpdateLines()
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(segments * (uMax - uMin)) + 1);
            count = Mathf.Min(count, pts.Length);
            // 線の濃さ: 始まりは薄く、終わりほど濃い (今見えている両端の u から)
            var c0 = new Color(1f, 1f, 1f, Mathf.Lerp(startAlpha, endAlpha, uMin));
            var c1 = new Color(1f, 1f, 1f, Mathf.Lerp(startAlpha, endAlpha, uMax));
            for (int k = 0; k < lines.Length; k++)
            {
                var lr = lines[k];
                if (lr == null) continue;
                if (uMax - uMin < 0.002f) { lr.positionCount = 0; continue; }
                lr.startColor = c0;
                lr.endColor = c1;
                float off = (k - 2) * lineSpacing;
                for (int i = 0; i < count; i++) pts[i] = Point(Mathf.Lerp(uMin, uMax, i / (float)(count - 1)), off);
                lr.positionCount = count;
                lr.SetPositions(pts);
            }
            // 1本目は五線譜の始まりの縦線 (音部記号の前)。左は先頭で、帯はここまで伸びている。右は帯の始まりで、終わりに記号といっしょに流れていく
            if (barLines.Length > 0) DrawBar(barLines[0], Leading ? head / pathLen : shift);
            // 2本目からは小節線: barSpacing ごとに1本ずつ出てきて (左は1小節目の後ろから、右は記号の後ろから)、記号・音符と同じ速さで終わりへ流れて消える。
            // 出てきた小節線 k を barLines[1 + k % 本数] に割り当てる。流し終わったら最後の小節の後ろの線 (lastBar) より後は出さない
            for (int b = 1; b < barLines.Length; b++) if (barLines[b] != null) barLines[b].positionCount = 0;
            if (!flowing || barLines.Length < 2) return;
            int newest = Mathf.Min(NewestBar(), lastBar);
            int first = Leading ? 1 : 0; // 左は1小節目の前に小節線を引かない (拍子記号のすぐ後ろから始まる)
            for (int k = newest; k >= first && k > newest - (barLines.Length - 1); k--)
            {
                float u = BarU(k);
                if (u > 1f) break;
                DrawBar(barLines[1 + k % (barLines.Length - 1)], u);
            }
        }

        // 縦線を u の所に引く (帯の見えている所の外なら消す)
        void DrawBar(LineRenderer lr, float u)
        {
            if (lr == null) return;
            if (u < uMin || u > uMax) { lr.positionCount = 0; return; }
            lr.startColor = lr.endColor = new Color(1f, 1f, 1f, Mathf.Lerp(startAlpha, endAlpha, u));
            lr.positionCount = 2;
            lr.SetPosition(0, Point(u, -2f * lineSpacing));
            lr.SetPosition(1, Point(u, 2f * lineSpacing));
        }

        Vector3 Bezier(float u)
        {
            float v = 1f - u;
            return v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
        }

        Vector3 BezierTangent(float u)
        {
            float v = 1f - u;
            return 3f * v * v * (p1 - p0) + 6f * v * u * (p2 - p1) + 3f * u * u * (p3 - p2);
        }

        /// <summary>
        /// u の所の帯の「上」の向き (5本の線が並ぶ向き)。帯の面がプレイヤーの頭 (focus) の方を向くように、進む向きと頭への向きの両方に直角。
        /// 横を通る所ではほぼ真上、頭より上を通る所では帯が下を向くように傾いて、まわりをやわらかく包む。真上より少しだけ上向きに寄せて、急に裏返らないようにする
        /// </summary>
        Vector3 BandUp(float u)
        {
            var t = BezierTangent(u).normalized;
            var toFocus = focus - Bezier(u);
            var d = Vector3.Cross(t, toFocus);
            if (d.sqrMagnitude < 0.0001f) return Vector3.up;
            // 進む向きと頭への向きがほぼ同じ (ステージの奥の方) では傾きが決まらないので、真上に寄せる (sin の大きさで混ぜる)
            float sin = d.magnitude / Mathf.Max(0.001f, toFocus.magnitude);
            d.Normalize();
            if (d.y < 0f) d = -d;
            d = Vector3.Lerp(Vector3.up, d, 0.85f * Smooth(sin / 0.6f));
            d -= t * Vector3.Dot(d, t);
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.up;
        }

        /// <summary>通り道に沿って u (0 = ステージ側 〜 1 = 終わり) の所の、真ん中の線から帯の「上」へ yOffset の位置 (この五線譜のローカル)。
        /// 波は始まり (左は記号や音符が出てくる所、右は記号が並ぶ所) では止まっていて、先へ行くほどゆれる</summary>
        Vector3 Point(float u, float yOffset)
        {
            float calm = Leading ? Smooth(u / 0.15f) : Smooth((u - FlowStartU * 0.6f) / 0.25f);
            float off = amp * calm * Mathf.Sin((u * waves + wavePhase) * Mathf.PI * 2f) + yOffset;
            return Bezier(u) + BandUp(u) * off;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
