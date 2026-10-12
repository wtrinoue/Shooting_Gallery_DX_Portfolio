using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// ボーナスステージ (フィーバー中の青い玉を撃つと始まる)。
    /// プレイヤーの足元の丸い穴をふさいでいる絞り羽根が、カメラの絞りのように回りながら開き、半球の地下室が現れる
    /// (プレイヤーは空中に浮いたまま)。地下室の内側にずらりと並んだ的のどれを撃っても、撃つたびに
    /// ド→レ→ミ…→ド と音階が上がる。的の点数は開くたびに 1000 / 2000 / 3000 からランダムに決まり、的に書いてある。当たった的は縮んで消える。
    /// 8回当て切るとファンファーレが鳴り、絞りが閉じる。
    /// </summary>
    public class BonusStage : MonoBehaviour
    {
        [Tooltip("絞り羽根。穴のふちにある回転の軸 (閉じた向きで置いておく)。開く時はこの Y 軸まわりに回って床の下へ隠れる")]
        public Transform[] irisBlades;
        [Tooltip("開く時に羽根が回る角度 (度)")]
        public float irisOpenAngle = 160f;
        public float slideTime = 1.4f;

        [Tooltip("地下室の内側に並んだ的 (kind = BigBonus, manualControl)。どれを撃っても次の音になる")]
        public Target[] targets;
        [Tooltip("当てるたびに1つずつ点くランプ (点いた状態の見た目)")]
        public GameObject[] lampsOn;
        [Tooltip("床が開いている間は隠す物 (足元の目印など)")]
        public GameObject[] hideWhileOpen;

        [Tooltip("的の点数を書く文字 (targets と同じ順)")]
        public TextMesh[] pointLabels;

        [Tooltip("当てる回数 (ドレミファソラシド = 8)")]
        public int notes = 8;
        [Tooltip("開くたびに、的ごとにこの中からランダムに点数を決める")]
        public int[] pointChoices = { 1000, 2000, 3000 };
        [Tooltip("当て切ってから絞りが閉じ始めるまでの秒数 (ファンファーレを聞く間)")]
        public float finishWait = 1.5f;
        [Tooltip("開き終わってから、的が1つずつ出てくる間隔 (秒)")]
        public float raiseInterval = 0.04f;

        enum Phase { Closed, Opening, Active, Finishing, Closing }
        Phase phase = Phase.Closed;
        float timer;
        int hits;
        int raised; // Active になってから出した的の数
        Quaternion[] closedRotations;

        public bool IsBusy => phase != Phase.Closed;
        public bool IsComplete => hits >= notes;

        void Awake()
        {
            closedRotations = new Quaternion[irisBlades.Length];
            for (int i = 0; i < irisBlades.Length; i++) closedRotations[i] = irisBlades[i].localRotation;
        }

        void Start()
        {
            foreach (var t in targets) t.SetAvailable(false);
            SetIris(0f);
            SetLamps(0);
        }

        /// <summary>絞りを開けてボーナスステージを始める</summary>
        public void Open()
        {
            if (IsBusy) return;
            hits = 0;
            SetLamps(0);
            AssignPoints();
            foreach (var go in hideWhileOpen) if (go != null) go.SetActive(false);
            phase = Phase.Opening;
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
            return target.points;
        }

        /// <summary>途中で終わる (フィーバー終了など)</summary>
        public void Close()
        {
            if (phase == Phase.Opening || phase == Phase.Active) BeginClose();
        }

        void BeginClose()
        {
            foreach (var t in targets) t.Lower();
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
                        phase = Phase.Active;
                        timer = 0f;
                    }
                    break;

                case Phase.Active:
                    // 的を1つずつポンポンと出す
                    while (raised < targets.Length && timer >= raised * raiseInterval)
                        targets[raised++].Raise(9999f);
                    break;

                case Phase.Finishing:
                    if (timer >= finishWait) BeginClose();
                    break;

                case Phase.Closing:
                    // 的が縮んで消えるのを少し待ってから絞りを閉じる
                    SetIris(1f - Ease((timer - 0.3f) / slideTime));
                    if (timer >= slideTime + 0.3f)
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
            {
                int p = pointChoices[Random.Range(0, pointChoices.Length)];
                targets[i].points = p;
                if (pointLabels == null || i >= pointLabels.Length || pointLabels[i] == null) continue;
                pointLabels[i].text = p.ToString();
                pointLabels[i].color = p >= 3000 ? new Color(1f, 0.35f, 0.55f) : p >= 2000 ? new Color(1f, 0.85f, 0.25f) : Color.white;
            }
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t); // ゆっくり動き出して、ゆっくり止まる
        }

        // 絞り羽根を回す (0 = 閉じている, 1 = 開いている)。全部の羽根が同じ向きに回るので、渦を巻くように開く
        void SetIris(float open)
        {
            for (int i = 0; i < irisBlades.Length; i++)
                irisBlades[i].localRotation = closedRotations[i] * Quaternion.Euler(0f, irisOpenAngle * open, 0f);
        }

        void SetLamps(int litCount)
        {
            for (int i = 0; i < lampsOn.Length; i++)
                if (lampsOn[i] != null) lampsOn[i].SetActive(i < litCount);
        }
    }
}
