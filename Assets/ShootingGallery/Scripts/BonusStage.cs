using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// ボーナスステージ (フィーバー中の青い玉を撃つと始まる)。
    /// プレイヤーの足元の床が左右にスライドして開き、地下室が現れる (プレイヤーは空中に立っている状態)。
    /// 地下室の底にある大きな的を撃つたびに ド→レ→ミ…→ド と音階が上がり、点数も 100, 200, 300… と増える。
    /// 8回当て切るとファンファーレが鳴り、床が閉まる。
    /// </summary>
    public class BonusStage : MonoBehaviour
    {
        [Tooltip("左右の床扉 (閉じた位置に置いておく。開く時は外側へスライドして周りの床の下に隠れる)")]
        public Transform doorLeft;
        public Transform doorRight;
        [Tooltip("扉がスライドする距離 (m)")]
        public float slideDistance = 1.5f;
        public float slideTime = 1.2f;

        [Tooltip("地下室の底に置かれた大きな的")]
        public Target bigTarget;
        [Tooltip("当てるたびに1つずつ点くランプ (点いた状態の見た目)")]
        public GameObject[] lampsOn;
        [Tooltip("床が開いている間は隠す物 (足元の目印など)")]
        public GameObject[] hideWhileOpen;

        [Tooltip("当てる回数 (ドレミファソラシド = 8)")]
        public int notes = 8;
        public int pointStep = 100;
        [Tooltip("当て切ってから床が閉まり始めるまでの秒数 (ファンファーレを聞く間)")]
        public float finishWait = 1.5f;

        enum Phase { Closed, Opening, Active, Finishing, Closing }
        Phase phase = Phase.Closed;
        float timer;
        int hits;
        Vector3 leftClosed, rightClosed;

        public bool IsBusy => phase != Phase.Closed;
        public bool IsComplete => hits >= notes;

        void Awake()
        {
            leftClosed = doorLeft.localPosition;
            rightClosed = doorRight.localPosition;
        }

        void Start()
        {
            bigTarget.SetAvailable(false);
            SetDoors(0f);
            SetLamps(0);
        }

        /// <summary>床を開けてボーナスステージを始める</summary>
        public void Open()
        {
            if (IsBusy) return;
            hits = 0;
            SetLamps(0);
            foreach (var go in hideWhileOpen) if (go != null) go.SetActive(false);
            phase = Phase.Opening;
            timer = 0f;
            Sfx.Play("door");
        }

        /// <summary>大きな的に当たった時に GameManager から呼ばれる。今回の点数を返す。</summary>
        public int RegisterHit()
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
            return hits * pointStep;
        }

        /// <summary>途中で終わる (フィーバー終了など)</summary>
        public void Close()
        {
            if (phase == Phase.Opening || phase == Phase.Active) BeginClose();
        }

        void BeginClose()
        {
            bigTarget.Lower();
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
                    SetDoors(Ease(timer / slideTime));
                    if (timer >= slideTime)
                    {
                        bigTarget.SetAvailable(true);
                        bigTarget.Raise(9999f);
                        phase = Phase.Active;
                    }
                    break;

                case Phase.Finishing:
                    if (timer >= finishWait) BeginClose();
                    break;

                case Phase.Closing:
                    // 的が倒れるのを少し待ってから床を閉める
                    SetDoors(1f - Ease((timer - 0.3f) / slideTime));
                    if (timer >= slideTime + 0.3f)
                    {
                        bigTarget.SetAvailable(false);
                        SetLamps(0);
                        foreach (var go in hideWhileOpen) if (go != null) go.SetActive(true);
                        phase = Phase.Closed;
                    }
                    break;
            }
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t); // ゆっくり動き出して、ゆっくり止まる
        }

        void SetDoors(float open)
        {
            doorLeft.localPosition = leftClosed + Vector3.left * (slideDistance * open);
            doorRight.localPosition = rightClosed + Vector3.right * (slideDistance * open);
        }

        void SetLamps(int litCount)
        {
            for (int i = 0; i < lampsOn.Length; i++)
                if (lampsOn[i] != null) lampsOn[i].SetActive(i < litCount);
        }
    }
}
