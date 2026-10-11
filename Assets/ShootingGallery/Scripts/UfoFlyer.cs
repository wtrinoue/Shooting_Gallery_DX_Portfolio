using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// ときどき UFO がプレイヤーの頭上を一直線に横切る。当てると 1000 点。
    /// 飛んでいる間は UFO から立体音響でうなり音が鳴るので、音のする方を見れば見つけられる。
    /// </summary>
    public class UfoFlyer : MonoBehaviour
    {
        public Target ufo;
        public AudioSource hum;

        [Header("Flight")]
        public float height = 6.5f;
        [Tooltip("頭上からどれだけ横にずれた所を通るか (0 = 真上)。この範囲でランダム")]
        public float maxOffset = 2.5f;
        [Tooltip("飛ぶ直線の長さの半分 (この距離の所から現れて、反対側のこの距離まで飛ぶ)")]
        public float halfLength = 15f;
        [Tooltip("横切るのにかかる秒数")]
        public float flightTime = 8f;

        [Header("Timing")]
        public float firstDelay = 10f;
        public Vector2 interval = new Vector2(15f, 25f);

        float timer;
        bool flying;
        float elapsed;
        Vector3 pathCenter;   // 頭上の通過点 (このオブジェクトからの相対)
        Vector3 pathDir;      // 進む向き (水平)
        bool wasPlaying;

        void Start()
        {
            if (hum != null && hum.clip == null) hum.clip = Sfx.CreateUfoHum();
            ufo.SetAvailable(false);
            timer = firstDelay;
        }

        void Update()
        {
            var gm = GameManager.I;
            bool playing = gm != null && gm.State == GameState.Playing;
            if (!playing)
            {
                if (flying) EndFlight();
                if (wasPlaying) timer = firstDelay;
                wasPlaying = false;
                return;
            }
            wasPlaying = true;

            if (!flying)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f) BeginFlight(gm.IsFever);
                return;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / flightTime;
            MoveAlongPath(t);

            // 飛び終わった or 撃ち落とされて消えた
            if (t >= 1f || (elapsed > 0.5f && !ufo.IsVisible)) EndFlight();
        }

        void BeginFlight(bool fever)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            if (fever)
            {
                // フィーバー中: どの方向からでも来る
                float yaw = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                pathDir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                pathCenter = Vector3.Cross(Vector3.up, pathDir) * Random.Range(-maxOffset, maxOffset);
            }
            else
            {
                // 通常時: 正面 (メイン屋台側) が見やすいよう、少し前の頭上を左右に横切る
                float yaw = (90f + Random.Range(-30f, 30f)) * Mathf.Deg2Rad;
                pathDir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * side;
                pathCenter = Vector3.forward * Random.Range(0.5f, maxOffset);
            }
            pathCenter += Vector3.up * height;

            elapsed = 0f;
            flying = true;
            MoveAlongPath(0f);
            ufo.SetAvailable(true);
            ufo.Raise(flightTime);
            if (hum != null) hum.Play();
        }

        void EndFlight()
        {
            flying = false;
            ufo.SetAvailable(false);
            if (hum != null) hum.Stop();
            timer = Random.Range(interval.x, interval.y);
        }

        void MoveAlongPath(float t)
        {
            float bob = Mathf.Sin(elapsed * 2.2f) * 0.3f; // ふわふわ上下する
            var local = pathCenter + pathDir * Mathf.Lerp(-halfLength, halfLength, t) + Vector3.up * bob;
            ufo.transform.position = transform.TransformPoint(local);

            // 撃たれた時にプレイヤーから見て奥へ倒れるよう、プレイヤーと反対側を向かせる
            var away = local;
            away.y = 0f;
            ufo.transform.rotation = Quaternion.LookRotation(transform.TransformDirection(away.sqrMagnitude > 0.25f ? away.normalized : pathDir));
        }
    }
}
