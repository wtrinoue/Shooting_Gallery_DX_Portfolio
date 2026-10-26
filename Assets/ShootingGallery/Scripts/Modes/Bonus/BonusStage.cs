using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// ボーナスステージ (フィーバー中の青い玉を撃つと始まる)。
    /// プレイヤーの足元の丸い穴をふさいでいる絞り羽根が、カメラの絞りのように回りながら開き、半球の地下室が現れる
    /// (プレイヤーは空中に浮いたまま)。地下室の内側にずらりと並んだ的のどれを撃っても、撃つたびに
    /// ド→レ→ミ…→ド と音階が上がる。的の点数は開くたびに 1000 / 2000 / 3000 からランダムに決まり、的に書いてある。
    /// 的は撃っても壊れず (ぷるっと縮んで戻る)、当てるたびにその的の点数が 500 ずつ下がる (500 より下にはならない)。
    /// 8回当て切るとファンファーレが鳴り、絞りが閉じる。
    /// </summary>
    public class BonusStage : MonoBehaviour
    {
        [Tooltip("羽根。穴のふちにある回転の軸 (閉じた向きで置いておく)。開く時は irisAxis のまわりに回る")]
        public Transform[] irisBlades;
        [Tooltip("開く時に羽根が回る軸 (羽根のローカル)。Y = 床の上で回って、周りの床の下へ隠れる")]
        public Vector3 irisAxis = Vector3.up;
        [Tooltip("開く時に羽根が回る角度 (度)")]
        public float irisOpenAngle = 160f;
        public float slideTime = 1.4f;

        [Tooltip("地下室の内側に並んだ的 (kind = BigBonus, manualControl)。どれを撃っても次の音になる")]
        public Target[] targets;
        [Tooltip("当てるたびに1つずつ点くランプ (点いた状態の見た目)")]
        public GameObject[] lampsOn;
        [Tooltip("床が開いている間は隠す物 (足元の目印など)")]
        public GameObject[] hideWhileOpen;
        [Tooltip("開く時に、屋台ごとに目の前の床から出る「下を見て」の矢印の見本 (屋台の数だけ複製する)")]
        public BonusArrow arrow;
        [Tooltip("矢印のハッチを置ける、穴の真ん中からいちばん近い距離 (穴のふちにかからない)")]
        public float arrowRadius = 2.4f;
        [Tooltip("矢印を出しておく秒数 (たったら、しまう)")]
        public float arrowDuration = 6f;
        [Tooltip("開ききってから、この秒数たったら閉じる")]
        public float openDuration = 10f;
        const float ArrowHideTime = 0.8f; // 矢印がしまわれきるまでの秒数 (台が下がる + 扉が閉じる + 小さくなる)
        float closeWait = 0.3f;

        [Tooltip("的の点数を書く文字 (targets と同じ順)")]
        public TextMesh[] pointLabels;

        [Tooltip("当てる回数 (ドレミファソラシド = 8)")]
        public int notes = 8;
        [Tooltip("開くたびに、的ごとにこの中からランダムに点数を決める")]
        public int[] pointChoices = { 1000, 2000, 3000 };
        [Tooltip("的に当てるたびに、その的の点数をこれだけ下げる")]
        public int pointDrop = 500;
        [Tooltip("下がっても、これより下にはならない")]
        public int minPoints = 500;
        [Tooltip("当て切ってから絞りが閉じ始めるまでの秒数 (ファンファーレを聞く間)")]
        public float finishWait = 1.5f;
        [Tooltip("開き終わってから、的が1つずつ出てくる間隔 (秒)")]
        public float raiseInterval = 0.04f;

        enum Phase { Closed, Opening, Active, Finishing, Closing }
        Phase phase = Phase.Closed;
        float timer;
        int hits;
        int raised; // Active になってから出した的の数
        int[] currentPoints; // 的ごとの今の点数 (target.points は GameManager が入った点数を書き込むので、こちらで覚えておく)
        Quaternion[] closedRotations;

        public bool IsBusy => phase != Phase.Closed;
        public bool IsComplete => hits >= notes;

        void Awake()
        {
            closedRotations = new Quaternion[irisBlades.Length];
            for (int i = 0; i < irisBlades.Length; i++) closedRotations[i] = irisBlades[i].localRotation;
            // 的は撃っても壊れない (ぷるっと縮んで戻るだけ)。作り直す前のシーンでもそうなるように、ここで決める
            foreach (var t in targets) if (t != null) t.stayUpOnHit = true;
        }

        void Start()
        {
            foreach (var t in targets) t.SetAvailable(false);
            SetIris(0f);
            SetLamps(0);
            HideArrows(true);
        }

        /// <summary>絞りを開けてボーナスステージを始める。しばらくの間、出ている屋台の目の前に「下を見て」の矢印が出る (UpdateArrows)</summary>
        public void Open()
        {
            if (IsBusy) return;
            hits = 0;
            SetLamps(0);
            AssignPoints();
            foreach (var go in hideWhileOpen) if (go != null) go.SetActive(false);
            phase = Phase.Opening;
            arrowTimeLeft = 0f; // (「下を見て」の矢印は、絞りが開ききってから出す)
            timer = 0f;
            Sfx.Play("door");
        }

        /// <summary>地下室の的に当たった時に GameManager から呼ばれる。当たった的の点数を返す (0 なら数えない)。</summary>
        public int RegisterHit(Target target)
        {
            if (phase != Phase.Active) return 0;
            Sfx.PlayNote(hits);
            hits++;
            SetLamps(hits);
            if (hits >= notes)
            {
                Sfx.Play("complete");
                phase = Phase.Finishing;
                timer = 0f;
            }
            // 当たった的の今の点数を入れ、その的の点数を pointDrop ずつ下げる (minPoints より下にはしない)
            int i = System.Array.IndexOf(targets, target);
            if (i < 0 || currentPoints == null) return target.points;
            int points = currentPoints[i];
            SetPoints(i, Mathf.Max(minPoints, points - pointDrop));
            return points;
        }

        // ---- 「下を見て」の矢印 (屋台ごとに1つ。arrow を見本に複製して使い回す) ----
        float arrowTimeLeft;
        readonly System.Collections.Generic.Dictionary<Stall, BonusArrow> arrows = new System.Collections.Generic.Dictionary<Stall, BonusArrow>();
        readonly System.Collections.Generic.List<BonusArrow> spareArrows = new System.Collections.Generic.List<BonusArrow>();
        readonly System.Collections.Generic.List<Stall> goneStalls = new System.Collections.Generic.List<Stall>();

        /// <summary>矢印を出している間か (GameManager はこの間だけ UpdateArrows を呼ぶ)</summary>
        public bool ArrowsActive => arrowTimeLeft > 0f || arrows.Count > 0;

        /// <summary>
        /// 出ている屋台 (stalls。せり上がる・滑る途中も含む) の目の前に、矢印を出す / 屋台といっしょに滑らせる。毎フレーム呼ぶ。
        /// 屋台の位置は親 (穴の真ん中が原点) のローカル座標。front = 屋台の中心から、足元のハッチの枠の手前の端まで。
        /// 新しく出てきた屋台には新しい矢印が出て、いなくなった屋台の矢印はしまう。決まった秒数がたったら全部しまう
        /// </summary>
        public void UpdateArrows(System.Collections.Generic.List<Stall> stalls, float front)
        {
            if (arrow == null) return;
            arrowTimeLeft -= Time.deltaTime;
            if (arrowTimeLeft <= 0f) { HideArrows(); return; }
            goneStalls.Clear();
            foreach (var kv in arrows)
                if (!stalls.Contains(kv.Key)) goneStalls.Add(kv.Key);
            foreach (var s in goneStalls) ReleaseArrow(s);
            foreach (var s in stalls)
            {
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                if (!arrows.TryGetValue(s, out var a))
                {
                    a = TakeArrow();
                    arrows[s] = a;
                }
                // 屋台の足元のハッチのすぐ手前 (穴のふちにはかからない)。屋台が滑ると、いっしょに滑る
                var p = s.transform.localPosition;
                var dir = new Vector3(p.x, 0f, p.z);
                float dist = dir.magnitude;
                if (dist < 0.01f) continue;
                float r = Mathf.Max(arrowRadius, dist - front - a.width / 2f - 0.06f);
                a.Place(dir / dist * r);
                a.Show();
            }
        }

        BonusArrow TakeArrow()
        {
            for (int i = spareArrows.Count - 1; i >= 0; i--)
            {
                var a = spareArrows[i];
                spareArrows.RemoveAt(i);
                if (a != null) return a;
            }
            var copy = Instantiate(arrow, arrow.transform.parent);
            copy.name = arrow.name;
            copy.HideImmediate();
            return copy;
        }

        void ReleaseArrow(Stall s)
        {
            if (!arrows.TryGetValue(s, out var a)) return;
            arrows.Remove(s);
            if (a == null) return;
            a.Hide();
            spareArrows.Add(a);
        }

        /// <summary>矢印を全部しまう (別の世界へ行く時なども GameManager から)。instant ならその場で消す</summary>
        public void HideArrows(bool instant = false)
        {
            arrowTimeLeft = 0f;
            foreach (var a in arrows.Values)
            {
                if (a == null) continue;
                if (instant) a.HideImmediate(); else a.Hide();
                spareArrows.Add(a);
            }
            arrows.Clear();
            if (instant && arrow != null) arrow.HideImmediate();
        }

        /// <summary>途中で終わる (フィーバー終了など)</summary>
        public void Close()
        {
            if (phase == Phase.Opening || phase == Phase.Active) BeginClose();
        }

        void BeginClose()
        {
            foreach (var t in targets) t.Lower();
            // 地下室が閉じ始めた: 矢印も残っていればしまう (しまいきってから絞りを回す)
            closeWait = arrows.Count > 0 ? ArrowHideTime : 0.3f;
            HideArrows();
            phase = Phase.Closing;
            timer = 0f;
            Sfx.Play("door");
        }

        void Update()
        {
            if (phase == Phase.Closed) return;
            timer += Time.deltaTime;

            switch (phase)
            {
                case Phase.Opening:
                    SetIris(Ease(timer / slideTime));
                    if (timer >= slideTime)
                    {
                        foreach (var t in targets) t.SetAvailable(true); // manualControl なので隠れたまま、下で1つずつ出す
                        raised = 0;
                        // 「下を見て」の矢印は、絞りが開ききってから出す (回っている羽根が、矢印のハッチの穴から見えないように)
                        arrowTimeLeft = arrowDuration;
                        phase = Phase.Active;
                        timer = 0f;
                    }
                    break;

                case Phase.Active:
                    // 的を1つずつポンポンと出す
                    while (raised < targets.Length && timer >= raised * raiseInterval)
                        targets[raised++].Raise(9999f);
                    if (timer >= openDuration) BeginClose(); // 開いてから決まった秒数で閉じる
                    break;

                case Phase.Finishing:
                    if (timer >= finishWait) BeginClose();
                    break;

                case Phase.Closing:
                    // 的が縮んで消える (と、矢印がしまわれる) のを少し待ってから絞りを閉じる
                    SetIris(1f - Ease((timer - closeWait) / slideTime));
                    if (timer >= slideTime + closeWait)
                    {
                        foreach (var t in targets) t.SetAvailable(false);
                        SetLamps(0);
                        foreach (var go in hideWhileOpen) if (go != null) go.SetActive(true);
                        phase = Phase.Closed;
                    }
                    break;
            }
        }

        // 的ごとに点数をランダムに決めて、的の文字も書き換える。点数が高いほど派手な色の文字
        void AssignPoints()
        {
            if (pointChoices == null || pointChoices.Length == 0) return;
            for (int i = 0; i < targets.Length; i++)
                SetPoints(i, pointChoices[Random.Range(0, pointChoices.Length)]);
        }

        // i 番目の的の点数を p にして、的の文字も書き換える。点数が高いほど派手な色の文字
        void SetPoints(int i, int p)
        {
            if (i < 0 || i >= targets.Length) return;
            if (currentPoints == null || currentPoints.Length != targets.Length) currentPoints = new int[targets.Length];
            currentPoints[i] = p;
            targets[i].points = p;
            if (pointLabels == null || i >= pointLabels.Length || pointLabels[i] == null) return;
            pointLabels[i].text = p.ToString();
            pointLabels[i].color = p >= 3000 ? new Color(1f, 0.35f, 0.55f) : p >= 2000 ? new Color(1f, 0.85f, 0.25f) : p >= 1000 ? Color.white : new Color(0.7f, 0.85f, 1f);
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t); // ゆっくり動き出して、ゆっくり止まる
        }

        // 絞り羽根を回す (0 = 閉じている, 1 = 開いている)。全部の羽根が同じ向きに回るので、渦を巻くように開く。
        // 開ききった羽根は周りの床の下にあって、屋台や矢印のハッチの穴から見えてしまうので、SetActive(false) で消しておく
        void SetIris(float open)
        {
            bool hide = open >= 0.999f;
            for (int i = 0; i < irisBlades.Length; i++)
                if (irisBlades[i].gameObject.activeSelf == hide) irisBlades[i].gameObject.SetActive(!hide);
            for (int i = 0; i < irisBlades.Length; i++)
                irisBlades[i].localRotation = closedRotations[i] * Quaternion.AngleAxis(irisOpenAngle * open, irisAxis);
        }

        void SetLamps(int litCount)
        {
            for (int i = 0; i < lampsOn.Length; i++)
                if (lampsOn[i] != null) lampsOn[i].SetActive(i < litCount);
        }
    }
}
