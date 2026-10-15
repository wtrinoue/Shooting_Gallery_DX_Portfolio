using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 西部劇モードの的: 馬に乗ったカウボーイ (たまに馬車)。射的の人形のように、地面から伸びた棒の上に立体の人形が乗っていて、
    /// 列車と並んで走る (棒ごと動く。馬は上下にゆれて走っているように見える)。当てると人形が後ろへ倒れ、棒ごと地面へ沈んで消える。
    /// 位置は親 (WesternTrain。プレイヤーの足元が原点、+Z が列車の進む向き) のローカル座標で決める。反撃はしない。
    /// </summary>
    public class WesternRider : MonoBehaviour
    {
        public enum Route { Overtake, FallBehind, Approach }

        public string displayName = "COWBOY";
        [Tooltip("ゆれる部分 (人形と棒)")]
        public Transform body;
        [Tooltip("人形の当たり判定 (visual = 人形。当たると後ろへ倒れる)")]
        public Target hitTarget;
        [Tooltip("点数を書いた札 (いつもプレイヤーの方を向く)")]
        public TextMesh[] pointLabels;
        public int[] pointChoices = { 100, 200 };
        public Route[] routeChoices = { Route.Overtake, Route.FallBehind };
        [Tooltip("列車に対して前後に動く速さ (m/秒)")]
        public float speed = 2.5f;
        [Tooltip("線路からの横の距離の範囲 (m)")]
        public Vector2 distance = new Vector2(7f, 12f);
        [Tooltip("地面の高さ (プレイヤーの足元が 0)")]
        public float groundY = -1.1f;
        [Tooltip("走っている時のゆれ (m) と速さ")]
        public float gallopHeight = 0.08f;
        public float gallopSpeed = 9f;

        const float FarZ = 30f;      // ここより前後は霧で見えない (出てくる所 / 去っていく所)
        const float SinkTime = 0.8f;

        public bool IsRunning { get; private set; }
        public bool IsBusy => IsRunning || sinking;
        public int Points => hitTarget != null ? hitTarget.points : 0;

        System.Action<WesternRider> onDown;
        Route route;
        float t, side, dist, phase, sinkT;
        bool sinking, hit;

        void Awake() => hitTarget.SetAvailable(false);

        /// <summary>走り始める。leftSide = 線路の左側を走るか</summary>
        public void Launch(bool leftSide, System.Action<WesternRider> downCallback)
        {
            gameObject.SetActive(true);
            route = routeChoices.Length > 0 ? routeChoices[Random.Range(0, routeChoices.Length)] : Route.Overtake;
            onDown = downCallback;
            side = leftSide ? -1f : 1f;
            dist = Random.Range(distance.x, distance.y);
            t = 0f;
            phase = Random.value * 10f;
            IsRunning = true;
            sinking = hit = false;
            body.localPosition = Vector3.zero;
            Place();

            hitTarget.points = pointChoices.Length > 0 ? pointChoices[Random.Range(0, pointChoices.Length)] : 100;
            foreach (var label in pointLabels) if (label != null) label.text = PointText.Of(hitTarget.points);
            hitTarget.SetAvailable(true);
            hitTarget.Raise(9999f);
        }

        /// <summary>撃たれずに去る (西部劇モードの終わり)。地面へ沈んで消える</summary>
        public void Retreat()
        {
            if (!IsRunning) return;
            IsRunning = false;
            sinking = true;
            sinkT = 0f;
            hitTarget.SetAvailable(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsRunning)
            {
                t += dt;
                Place();
                // パカパカと上下にゆれる
                float w = (Time.time + phase) * gallopSpeed;
                body.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(w)) * gallopHeight);
                body.localRotation = Quaternion.Euler(Mathf.Sin(w) * 3f, 0f, 0f);

                if (t > 0.4f && !hitTarget.IsUp && !hit)
                {   // 当たった: 人形が倒れるのを少し見せてから沈む
                    hit = true;
                    sinkT = -0.35f;
                    onDown?.Invoke(this);
                }
                if (hit)
                {
                    sinkT += dt;
                    if (sinkT >= 0f) { IsRunning = false; sinking = true; }
                }
                else if (Mathf.Abs(transform.localPosition.z) > FarZ + 1f && t > 2f) Vanish(); // 霧の向こうへ走り去った
                return;
            }
            if (sinking)
            {
                sinkT += dt;
                transform.localPosition += Vector3.down * (dt * 3f);
                if (sinkT >= SinkTime) Vanish();
            }
        }

        void Vanish()
        {
            IsRunning = sinking = false;
            hitTarget.SetAvailable(false);
            gameObject.SetActive(false);
        }

        // 列車に対する位置。追い越す / 追い越される / 遠くから寄ってくる
        void Place()
        {
            float x, z;
            switch (route)
            {
                case Route.Overtake: // 後ろの霧の中から、ゆっくり前へ追い越していく
                    z = -FarZ + speed * t;
                    x = side * dist;
                    break;
                case Route.FallBehind: // 前の霧の中から、ゆっくり後ろへ遅れていく
                    z = FarZ - speed * t;
                    x = side * dist;
                    break;
                default: // 斜め前の遠くから寄ってきて、並んで走ってから遅れていく
                {
                    float k = Mathf.Clamp01(t / 4f);
                    float e = k * k * (3f - 2f * k);
                    x = side * Mathf.Lerp(dist + 18f, dist, e);
                    z = Mathf.Lerp(14f, 2f, e) - Mathf.Max(0f, t - 6f) * speed;
                    break;
                }
            }
            transform.localPosition = new Vector3(x, groundY, z);
            transform.localRotation = Quaternion.identity; // 列車と同じ向きに走る
        }
    }
}
