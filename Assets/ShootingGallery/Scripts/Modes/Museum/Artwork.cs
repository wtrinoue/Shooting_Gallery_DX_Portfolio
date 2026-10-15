using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 美術館モードの的: 未完成の作品。キャンバス (初めは真っ白) と彫刻 (初めは白い直方体) と、天井から吊るした粘土の作品 (初めは白い粘土の塊) の3種類。
    /// イマジネーションの弾が当たるたびに作品が1段ずつ出来上がっていく (stages の数だけ当てると完成)。
    ///   キャンバス: 筆が現れて、絵の上を左右に行ったり来たりしながら塗る。段ごとの絵 (ビットマップ。下塗り → 形 → 細かい仕上げ) が、
    ///               筆の通ったあとから見えていく (絵の材質の切り抜きの値を、この絵だけ下げていく。PaintingMask)
    ///   彫刻: 当たった所にトンカチが現れて、カンカンとたたく。石のかけらが飛び、直方体 → 荒削り → 形 → 色とりどりの完成品と変わる
    ///   吊るした粘土の作品: 粘土のかたまりが飛んできて、ぺちゃっとくっつく。塊 → 大まかな形 → 形 → 色のついた完成品 (飛行機・恐竜の骨・クジラ)
    /// 未完成の作品は真っ白 (白いものが撃つべき的)。キャンバスは段が進むごとに額縁が安っぽい白木 → 木 → 銀 → 豪華な金に変わり、
    /// 彫刻は白い塊が、いろいろな色の部品でできた作品に変わっていく。
    /// 完成すると金色の光がはじけ、作品はそのまま美術館に飾られる (当たっても点数は入らない)。ArtMuseum が出したりしまったりする
    /// </summary>
    public class Artwork : MonoBehaviour
    {
        public enum Kind { Canvas, Sculpture, Hanging }

        public Kind kind = Kind.Canvas;
        public string title = "UNTITLED";
        [Tooltip("当たり判定 (kind = Artwork, stayUpOnHit, manualControl)。visual = 作品 (当たるとぐらっと揺れる)")]
        public Target target;
        [Tooltip("段ごとの見た目。0 = 未完成 (白いキャンバス / 灰色の直方体)。キャンバスは 1〜 が段ごとの絵 (Quad。前の段の上に重ねる)、彫刻は1つだけ出す")]
        public GameObject[] stages;
        [Tooltip("段を進めた1発の点数 (1段目, 2段目, …) / 完成させた時に足す点数")]
        public int[] stagePoints = { 100, 200, 300 };
        public int completeBonus = 500;
        [Tooltip("キャンバスの段ごとの額縁 (0 = 安っぽい白木 〜 最後 = 豪華な金)。その段の額縁だけを出す")]
        public GameObject[] frames;
        [Tooltip("作品の下の札 (次の1発の点数。完成したら題名)")]
        public TextMesh plaque;
        [Tooltip("当たった所に出る道具 (キャンバスは筆、彫刻はトンカチ)。作品の子にしておく")]
        public Transform tool;
        [Tooltip("彫る時に飛ぶ石のかけら・完成の光 (半透明)")]
        public Material chipMaterial;
        public Material sparkleMaterial;
        [Tooltip("完成した時に光がはじける所 (作品のローカル)")]
        public Vector3 sparkleOffset = new Vector3(0f, 0.8f, 0f);
        [Tooltip("キャンバスの1段を塗りきるまでの秒数")]
        public float paintTime = 0.9f;

        public bool IsShown { get; private set; }
        public bool IsComplete => IsShown && stage >= stages.Length - 1;
        public bool IsBlank => IsShown && !IsComplete;
        public float CompletedAt { get; private set; }

        int stage;
        float toolT = -1f;     // トンカチの動き (0〜1。-1 で出していない)
        Vector3 toolPoint;     // トンカチを出す所 (作品のローカル)
        float revealT = -1f;   // 新しい段が現れていく途中 (0〜1)
        Renderer[] paints;     // キャンバスの段ごとの絵 (0 は使わない)
        MaterialPropertyBlock block;
        float popT = -1f;      // 出てくる時のふくらみ
        Vector3 baseScale;

        const float ToolTime = 0.55f;
        const float CarveTime = 0.5f;

        void Awake()
        {
            baseScale = transform.localScale;
            block = new MaterialPropertyBlock();
            if (kind == Kind.Canvas && stages != null)
            {
                paints = new Renderer[stages.Length];
                for (int s = 1; s < stages.Length; s++)
                    if (stages[s] != null) paints[s] = stages[s].GetComponent<Renderer>();
            }
            if (tool != null) tool.gameObject.SetActive(false);
            // (作品は隠した状態で作っておく。初めて Show した時にここが呼ばれるので、ここでは隠さない)
        }

        /// <summary>未完成の作品として出す (ふくらんで現れる)</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            IsShown = true;
            stage = 0;
            SetStage(0);
            toolT = -1f;
            revealT = -1f;
            if (tool != null) tool.gameObject.SetActive(false);
            popT = 0f;
            transform.localScale = baseScale * 0.01f;
            target.points = stagePoints.Length > 0 ? stagePoints[0] : 100;
            target.SetAvailable(true);
            target.Raise(9999f);
            UpdatePlaque();
        }

        /// <summary>しまう (美術館を出る時)</summary>
        public void HideNow()
        {
            IsShown = false;
            if (target != null) target.SetAvailable(false);
            transform.localScale = baseScale == Vector3.zero ? transform.localScale : baseScale;
            gameObject.SetActive(false);
        }

        /// <summary>弾が当たった: 道具を出して1段進める。入る点数を返す (完成させた1発には完成の点数も足す)</summary>
        public int Advance(Vector3 worldPoint)
        {
            if (!IsBlank) return 0;
            int points = stage < stagePoints.Length ? stagePoints[stage] : 100;
            if (revealT >= 0f) FinishReveal(); // 前の段を塗っている途中なら、塗りきってから次へ
            stage++;
            toolPoint = transform.InverseTransformPoint(worldPoint);
            Sfx.Play(kind == Kind.Canvas ? "paint" : kind == Kind.Sculpture ? "chisel" : "clay", 0.9f); // 筆で塗る音 / ノミで彫る音 / 粘土がぺちゃっとくっつく音
            if (kind == Kind.Hanging) // 粘土は、プレイヤーの方から飛んでくる
            {
                var cam = Camera.main;
                var from = cam != null ? cam.transform.position : transform.position + Vector3.forward;
                clayFrom = transform.InverseTransformDirection((from - worldPoint).normalized);
            }
            revealT = 0f;
            if (kind == Kind.Canvas)
            {
                if (stages[stage] != null) stages[stage].SetActive(true);
                SetFrame(stage);
                PaintingMask.SetProgress(paints[stage], block, 0f);
                if (tool != null) tool.gameObject.SetActive(true);
            }
            else
            {
                SetStage(stage);
                stages[stage].transform.localScale = Vector3.one * 0.85f; // 新しい形は少し小さな所から、たたかれて落ち着く
                toolT = 0f;
                if (tool != null) tool.gameObject.SetActive(true);
                if (chipMaterial != null)
                    for (int i = 0; i < 3; i++)
                        Explosion.Spawn(worldPoint + Random.insideUnitSphere * 0.15f, Random.Range(0.18f, 0.32f), chipMaterial);
            }
            if (IsComplete)
            {
                points += completeBonus;
                CompletedAt = Time.time; // 完成したら飾られる (当たってもぐらっと揺れるだけで、もう点数は入らない)
                Sfx.Play("complete", 0.7f);
                if (sparkleMaterial != null)
                    for (int i = 0; i < 4; i++)
                        Explosion.Spawn(transform.TransformPoint(sparkleOffset) + Random.insideUnitSphere * 0.5f, Random.Range(0.4f, 0.7f), sparkleMaterial);
            }
            else target.points = stage < stagePoints.Length ? stagePoints[stage] : 100;
            UpdatePlaque();
            return points;
        }

        void UpdatePlaque()
        {
            if (plaque == null) return;
            plaque.text = IsComplete ? title : PointText.Of(target.points);
            plaque.color = IsComplete ? new Color(1f, 0.82f, 0.3f) : new Color(0.95f, 0.88f, 0.7f);
        }

        // その段の額縁だけを出す (段が進むほど豪華になる)
        void SetFrame(int s)
        {
            if (frames == null) return;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) frames[i].SetActive(i == Mathf.Min(s, frames.Length - 1));
        }

        // 段の見た目を、すぐにその段にする。キャンバスは白いキャンバス (いつも出す) と s 段目の絵だけ (下の段は隠れているので出さない)。彫刻は s 段だけ
        void SetStage(int s)
        {
            SetFrame(s);
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] == null) continue;
                bool on = kind == Kind.Canvas ? i == 0 || i == s : i == s;
                stages[i].SetActive(on);
                if (kind == Kind.Canvas && paints != null && paints[i] != null) PaintingMask.SetProgress(paints[i], block, 1f);
            }
        }

        // 塗っている途中の段を塗りきる (下の段は上の段に隠れたので、描かない)
        void FinishReveal()
        {
            revealT = -1f;
            if (kind == Kind.Canvas)
            {
                SetStage(stage);
                if (tool != null) tool.gameObject.SetActive(false);
            }
            else if (stage < stages.Length && stages[stage] != null) stages[stage].transform.localScale = Vector3.one;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (popT >= 0f)
            {
                popT += dt / 0.4f;
                float k = Mathf.Clamp01(popT);
                float e = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f); // 少し行き過ぎて戻る
                transform.localScale = baseScale * Mathf.Max(0.01f, e);
                if (popT >= 1f) { popT = -1f; transform.localScale = baseScale; }
            }
            if (revealT >= 0f)
            {
                if (kind == Kind.Canvas)
                {
                    revealT += dt / Mathf.Max(0.05f, paintTime);
                    float k = Mathf.Clamp01(revealT);
                    PaintingMask.SetProgress(paints[stage], block, k);
                    MoveBrush(k);
                }
                else
                {
                    // たたかれるたびに、ぐっと縮んで元に戻る
                    revealT += dt / CarveTime;
                    float k = Mathf.Clamp01(revealT);
                    float bump = 1f - 0.15f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * (1f - k);
                    stages[stage].transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Smooth(k)) * bump;
                }
                if (revealT >= 1f) FinishReveal();
            }
            UpdateHammer(dt);
        }

        // 筆: 塗っている段の絵の上を、塗る順番どおりに動く (筆先が絵の少し手前。左右に行き来する向きに少し傾ける)
        void MoveBrush(float k)
        {
            if (tool == null || paints[stage] == null) return;
            var layer = paints[stage].transform;
            int rows = PaintingMask.StageRows[Mathf.Clamp(stage - 1, 0, PaintingMask.StageRows.Length - 1)];
            var uv = PaintingMask.BrushAt(k, rows);
            int row = Mathf.Min(rows - 1, (int)(Mathf.Clamp01(k) * rows));
            float lean = row % 2 == 0 ? -25f : 25f; // 進む向きと反対へ柄を傾ける
            tool.position = layer.TransformPoint(new Vector3(uv.x - 0.5f, uv.y - 0.5f, 0f)) - layer.forward * 0.03f;
            tool.rotation = layer.rotation * Quaternion.Euler(-20f, 0f, lean + 8f * Mathf.Sin(k * 40f));
            tool.localScale = Vector3.one * Mathf.Min(1f, Mathf.Min(k, 1f - k) * 12f + 0.01f); // ぱっと出て、ぱっと消える
        }

        Vector3 clayFrom; // 粘土が飛んでくる向き (作品のローカル。当たった所からプレイヤーの方)

        // トンカチ: 当たった所で振り上げて2回たたく。粘土: プレイヤーの方から飛んできて、当たった所でぺちゃっとつぶれてくっつき、なじんで消える
        void UpdateHammer(float dt)
        {
            if (toolT < 0f || tool == null) return;
            toolT += dt / ToolTime;
            float k = Mathf.Clamp01(toolT);
            if (kind == Kind.Hanging)
            {
                tool.localRotation = Quaternion.LookRotation(clayFrom);
                if (k < 0.3f)
                {
                    float f = k / 0.3f;
                    tool.localPosition = toolPoint + clayFrom * (1.2f * (1f - f));
                    tool.localScale = Vector3.one;
                }
                else
                {
                    float f = (k - 0.3f) / 0.7f;
                    tool.localPosition = toolPoint;
                    float squash = Mathf.Lerp(0.35f, 0.2f, f);
                    float fade = 1f - Smooth(Mathf.Clamp01((f - 0.5f) / 0.5f)); // 後半は作品になじんで小さくなる
                    tool.localScale = new Vector3(1.5f, 1.5f, squash) * Mathf.Max(0.01f, fade);
                }
                if (toolT >= 1f)
                {
                    toolT = -1f;
                    tool.gameObject.SetActive(false);
                }
                return;
            }
            float swing = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)); // 2回振り下ろす
            tool.localPosition = toolPoint + new Vector3(0.12f, 0.1f, -0.25f);
            tool.localRotation = Quaternion.Euler(0f, -20f, 60f - 95f * (1f - swing));
            tool.localScale = Vector3.one * Mathf.Min(1f, Mathf.Min(k, 1f - k) * 8f + 0.01f);
            if (toolT >= 1f)
            {
                toolT = -1f;
                tool.gameObject.SetActive(false);
            }
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
