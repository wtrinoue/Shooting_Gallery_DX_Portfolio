using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 金魚すくいの水槽で泳ぐ金魚 1匹 (的)。水槽の中をゆらゆら向きを変えながら泳ぎ、ふちに近づくと真ん中の方へ曲がる。尾びれを振る。
    /// すくわれた (Target が縮んで消えた) あとは、次に出てくる時に水槽の別の所から出る。ポイが破れた時はびっくりして速く逃げる (Flee)。
    /// 位置は親 (水槽の真ん中) のローカル座標で動かす。
    /// </summary>
    public class FestivalFish : MonoBehaviour
    {
        public Target target;
        [Tooltip("左右に振る尾びれ")]
        public Transform tail;
        [Tooltip("泳げる範囲 (水槽の真ん中からの X / Z の半分の大きさ)")]
        public Vector2 halfSize = new Vector2(2.5f, 1f);
        [Tooltip("泳ぐ深さ (親のローカル Y)")]
        public float swimY = -0.15f;
        public float speed = 0.35f;
        [Tooltip("向きを変える速さ (度/秒)")]
        public float turnRate = 120f;

        float heading, turnTo, nextTurn, fleeUntil, phase;
        bool hidden;

        /// <summary>ポイに乗せられている間 (FestivalStage が動かす。泳がない)</summary>
        public bool Captured { get; set; }

        void Start()
        {
            phase = Random.value * 10f;
            Relocate();
        }

        // 水槽の中のランダムな所へ移す (すくわれて、また出てくる時)
        void Relocate()
        {
            heading = turnTo = Random.Range(0f, 360f);
            transform.localPosition = new Vector3(Random.Range(-halfSize.x, halfSize.x) * 0.85f, swimY, Random.Range(-halfSize.y, halfSize.y) * 0.85f);
            transform.localRotation = Quaternion.Euler(0f, heading, 0f);
        }

        /// <summary>ポイが破れた: びっくりして、反対の向きへ少しの間速く泳ぐ</summary>
        public void Flee()
        {
            fleeUntil = Time.time + 0.9f;
            turnTo = heading + 180f;
        }

        void Update()
        {
            if (Captured) return;
            if (target != null && !target.IsVisible)
            {
                hidden = true;
                return;
            }
            if (hidden)
            {
                hidden = false;
                Relocate();
            }
            float dt = Time.deltaTime, now = Time.time;
            if (now >= nextTurn)
            {
                turnTo = heading + Random.Range(-100f, 100f);
                nextTurn = now + Random.Range(0.8f, 2.6f);
            }
            var p = transform.localPosition;
            // ふちに近づいたら真ん中の方へ曲がる
            if (Mathf.Abs(p.x) > halfSize.x - 0.2f || Mathf.Abs(p.z) > halfSize.y - 0.15f)
                turnTo = Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg + Random.Range(-30f, 30f);
            bool fleeing = now < fleeUntil;
            heading = Mathf.MoveTowardsAngle(heading, turnTo, turnRate * (fleeing ? 3f : 1f) * dt);
            float sp = speed * (fleeing ? 3.5f : 1f) * (0.75f + 0.25f * Mathf.Sin(now * 1.7f + phase));
            var dir = new Vector3(Mathf.Sin(heading * Mathf.Deg2Rad), 0f, Mathf.Cos(heading * Mathf.Deg2Rad));
            p += dir * (sp * dt);
            p.x = Mathf.Clamp(p.x, -halfSize.x, halfSize.x);
            p.z = Mathf.Clamp(p.z, -halfSize.y, halfSize.y);
            p.y = swimY + Mathf.Sin(now * 1.3f + phase) * 0.02f;
            transform.localPosition = p;
            transform.localRotation = Quaternion.Euler(0f, heading, 0f);
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(now * (fleeing ? 26f : 11f) + phase) * 28f, 0f);
        }
    }
}
