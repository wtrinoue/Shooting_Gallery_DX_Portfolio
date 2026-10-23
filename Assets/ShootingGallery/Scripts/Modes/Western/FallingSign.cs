using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 西部劇のカウボーイの棒に付いた点数の札が、撃たれた時にポロっと外れて地面へ落ちる動き。
    /// Drop で棒から外れ (棒といっしょに動いていた速さのまま、少し跳ねて、ゆっくり回りながら) 落ち、地面に着いたら小さく跳ねて平らに寝る。
    /// 地面に寝ている間は、流れていく景色 (地面) といっしょに後ろへ流れ、しばらくしたら地面へ沈む。最後に元の棒へ戻る (見えない所で)。
    /// 元の棒へは Restore でいつでも戻せる (次に走り出す時)。札には当たり判定がないので、撃たれることはない。
    /// </summary>
    public class FallingSign : MonoBehaviour
    {
        enum State { Idle, Falling, Lying, Sinking }

        const float Gravity = 9.8f;
        const float LieTime = 1.6f;   // 地面に寝ている秒数
        const float SinkTime = 0.5f;  // 地面へ沈む秒数
        const float FlattenTime = 0.15f;

        State state = State.Idle;
        Transform home;               // 元の親 (棒)
        Vector3 homePos;
        Quaternion homeRot;
        FaceCenter face;
        Vector3 velocity, groundVelocity, spinAxis;
        float spin, groundY, clock;
        bool bounced;
        Quaternion flatFrom, flatTo;

        public bool IsBusy => state != State.Idle;

        /// <summary>
        /// 棒から外して落とす。space = 落ちている間の親 (列車。モードが終わったらいっしょに消える)、groundWorldY = 地面の高さ (ワールド)、
        /// carry = 外れた瞬間の棒の速さ (ワールド)、ground = 地面が流れていく速さ (ワールド)
        /// </summary>
        public void Drop(Transform space, float groundWorldY, Vector3 carry, Vector3 ground)
        {
            if (state == State.Idle)
            {
                home = transform.parent;
                homePos = transform.localPosition;
                homeRot = transform.localRotation;
                face = GetComponent<FaceCenter>();
            }
            if (face != null) face.enabled = false; // (落ちている間は、プレイヤーの方を向き続けない)
            transform.SetParent(space, true);
            groundY = groundWorldY;
            groundVelocity = ground;
            // 棒と同じ速さのまま、ほんの少し上と横へ跳ねて外れる
            Vector3 side = Vector3.Cross(Vector3.up, transform.forward).normalized;
            velocity = carry + Vector3.up * Random.Range(0.6f, 1.2f) + side * Random.Range(-0.6f, 0.6f) - transform.forward * Random.Range(0.2f, 0.6f);
            // 横の軸のまわりにゆっくり回る (前か後ろへ倒れるように)
            spinAxis = (side + Random.insideUnitSphere * 0.4f).normalized;
            spin = Random.Range(140f, 280f) * (Random.value < 0.5f ? -1f : 1f);
            bounced = false;
            state = State.Falling;
        }

        /// <summary>元の棒へ戻す (次に走り出す時。落ちている途中でも)</summary>
        public void Restore()
        {
            if (state == State.Idle) return;
            state = State.Idle;
            if (home == null) return;
            transform.SetParent(home, false);
            transform.localPosition = homePos;
            transform.localRotation = homeRot;
            if (face != null) face.enabled = true;
        }

        void Update()
        {
            if (state == State.Idle) return;
            float dt = Time.deltaTime;
            switch (state)
            {
                case State.Falling:
                    velocity += Vector3.down * (Gravity * dt);
                    transform.position += velocity * dt;
                    transform.Rotate(spinAxis, spin * dt, Space.World);
                    if (transform.position.y <= groundY + 0.03f)
                    {
                        var p = transform.position;
                        transform.position = new Vector3(p.x, groundY + 0.03f, p.z);
                        if (!bounced && velocity.y < -2f)
                        {   // 小さく1回だけ跳ねる
                            bounced = true;
                            velocity = new Vector3(velocity.x * 0.5f, -velocity.y * 0.22f, velocity.z * 0.5f);
                            spin *= 0.4f;
                            break;
                        }
                        // 平らに寝る (札の面が上か下を向くよう、いちばん近い向きへ)
                        flatFrom = transform.rotation;
                        Vector3 n = transform.forward;
                        flatTo = Quaternion.FromToRotation(n, n.y >= 0f ? Vector3.up : Vector3.down) * transform.rotation;
                        velocity.y = 0f;
                        clock = 0f;
                        state = State.Lying;
                    }
                    break;
                case State.Lying:
                    clock += dt;
                    transform.rotation = Quaternion.Slerp(flatFrom, flatTo, Mathf.Clamp01(clock / FlattenTime));
                    // 地面にこすれて、すぐに地面と同じ速さになる (流れていく景色といっしょに後ろへ)
                    velocity = Vector3.Lerp(velocity, groundVelocity, Mathf.Clamp01(dt * 6f));
                    transform.position += velocity * dt;
                    if (clock >= LieTime) { clock = 0f; state = State.Sinking; }
                    break;
                case State.Sinking:
                    clock += dt;
                    transform.position += (groundVelocity + Vector3.down * (0.4f / SinkTime)) * dt;
                    if (clock >= SinkTime) Restore(); // (地面の下で元の棒へ戻る)
                    break;
            }
        }
    }
}
