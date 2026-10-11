using UnityEngine;

namespace ShootingGallery
{
    public enum TargetKind { Normal, MachineGun, Shotgun, Bomb, Fever, Start, Ufo, Explosive, BlueOrb, BigBonus, Sniper, SniperBig, MachineGunChallenge, MachineGunChallengeTarget }

    /// <summary>
    /// 的。撃たれると後ろに倒れて消え、一定時間後に起き上がる。
    /// visual(Pivot) の子にあるコライダーで当たり判定を取る。
    /// manualControl の的は自動で起き上がらず、Raise() で外部 (PopUpGimmick) から出す。
    /// </summary>
    public class Target : MonoBehaviour
    {
        const float RiseTime = 0.25f;
        const float FallTime = 0.15f;

        public TargetKind kind = TargetKind.Normal;
        public int points = 10;
        public float respawnDelay = 3f;
        [Tooltip("倒れる時に回転させる見た目のルート")]
        public Transform visual;
        [Tooltip("ONなら自動で復活せず、Raise() で出す")]
        public bool manualControl;
        [Tooltip("点数の的の「顔」(円盤と数字)。武器の的に化ける時に隠される")]
        public GameObject face;
        [Tooltip("ONなら当てても倒れず、ぐらっと揺れるだけ (何回も当てるボーナスの大きな的)")]
        public bool stayUpOnHit;
        [Tooltip("stayUpOnHit の時、次に当たりとして数えるまでの間隔 (ショットガンの散弾で一度に何回も数えないように)")]
        public float multiHitInterval = 0.15f;
        [Tooltip("stayUpOnHit の時、当たってぐらっと揺れる角度 (度)")]
        public float hitWobbleDeg = 18f;
        [Header("Knock Off (台に置いたカップ用)")]
        [Tooltip("ONなら当たっても倒れず、後ろへ弾き飛ばされ、くるくる回りながら台の後ろへ落ちる")]
        public bool knockOffOnHit;
        [Tooltip("後ろへ飛ぶ速さ (m/秒)")]
        public float knockBackSpeed = 1.1f;
        [Tooltip("上へ跳ねる速さ (m/秒)")]
        public float knockUpSpeed = 1.2f;
        [Tooltip("後ろへ動ける最大の距離 (m)。奥の壁を突き抜けないように、そこからは壁ぞいに落ちる")]
        public float knockMaxBack = 0.4f;
        [Tooltip("根元からどれだけ下まで落ちたら消えるか (m)。台の高さにすると床まで落ちる")]
        public float knockDropHeight = 1f;

        /// <summary>的が起き上がり始めた瞬間に呼ばれる (WeaponDropper が武器の的に化けさせるのに使う)</summary>
        public static event System.Action<Target> Rising;

        [Header("Movement")]
        public bool moving;
        public float moveAmplitude = 0.5f;
        public float moveSpeed = 1.5f;
        [Tooltip("指定するとY軸回転し続ける (特殊な的の演出用)")]
        public Transform spinner;
        public float spinSpeed = 120f;

        Vector3 basePos;
        float phase;

        bool up = true;
        bool available = true;
        float angle;          // 0 = 立っている, 90 = 倒れている
        float targetAngle;
        float animSpeed;
        bool pendingRespawn;
        float respawnTimer;
        float lowerTimer;
        float nextMultiHitTime;
        // 弾き飛ばされている間の状態 (knockOffOnHit)
        bool knocking;
        float knockTime;
        Vector3 knockVel, knockStart, knockBackDir, knockSpinAxis, knockHome;
        float knockSpin, knockFloorY;

        public bool IsUp => up && available;
        /// <summary>見た目が表示されているか (倒れるアニメーション中も true)</summary>
        public bool IsVisible => visual != null && visual.gameObject.activeSelf;
        bool Animating => !Mathf.Approximately(angle, targetAngle);
        public bool CanRaise => manualControl && available && !up && !Animating && !knocking && gameObject.activeInHierarchy;

        void Awake()
        {
            basePos = transform.localPosition;
            phase = Random.value * 10f;
        }

        void Update()
        {
            if (moving)
                transform.localPosition = basePos + Vector3.right * Mathf.Sin((Time.time + phase) * moveSpeed) * moveAmplitude;
            if (spinner != null)
                spinner.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            if (knocking) UpdateKnockOff();

            // 起き上がり / 倒れるアニメーション
            if (Animating)
            {
                angle = Mathf.MoveTowards(angle, targetAngle, animSpeed * Time.deltaTime);
                if (!Animating)
                {
                    if (targetAngle >= 90f) SetPose(false, 90f);
                    else
                    {
                        SetPose(true, 0f);
                        up = available;
                    }
                }
                else SetPose(true, angle);
            }

            // 自動復活
            if (pendingRespawn)
            {
                respawnTimer -= Time.deltaTime;
                if (respawnTimer <= 0f && !Animating && CanRespawn())
                {
                    pendingRespawn = false;
                    StartRise();
                }
            }

            // Raise() で出した的を引っ込める
            if (lowerTimer > 0f && up)
            {
                lowerTimer -= Time.deltaTime;
                if (lowerTimer <= 0f)
                {
                    up = false;
                    StartFall(0.2f);
                }
            }
        }

        /// <summary>的を出す / しまう。しまった的は復活しない。</summary>
        public void SetAvailable(bool value)
        {
            available = value;
            pendingRespawn = false;
            lowerTimer = 0f;
            up = false;

            if (!value || manualControl)
            {
                SnapTo(90f);
                return;
            }
            if (!gameObject.activeInHierarchy)
            {
                SnapTo(0f);
                up = true;
                return;
            }
            SnapTo(90f);
            StartRise();
        }

        /// <summary>置き場所を変える (moving の揺れの中心もここになる)</summary>
        public void Place(Vector3 localPos)
        {
            basePos = localPos;
            transform.localPosition = localPos;
        }

        /// <summary>出ている的を (撃たれずに) 引っ込める。自動復活する的は、いつも通り後で起き上がる。</summary>
        public void Lower()
        {
            if (!up) return;
            up = false;
            lowerTimer = 0f;
            StartFall(0.2f);
            if (!manualControl)
            {
                pendingRespawn = true;
                respawnTimer = respawnDelay;
            }
        }

        /// <summary>
        /// 出ている的をいったん倒して、delay 秒後に起き上がらせる (自動復活する的だけ)。
        /// 起き上がる瞬間に Rising が呼ばれるので、狙った的を特殊な的に化けさせるのに使う。
        /// </summary>
        public bool Reappear(float delay)
        {
            if (manualControl || !IsUp || Animating) return false;
            up = false;
            lowerTimer = 0f;
            StartFall(0.2f);
            pendingRespawn = true;
            respawnTimer = delay;
            return true;
        }

        /// <summary>manualControl の的を stay 秒だけ出す</summary>
        public void Raise(float stay)
        {
            if (!CanRaise) return;
            lowerTimer = stay;
            StartRise();
        }

        public void Hit(Vector3 point)
        {
            if (!IsUp) return;

            if (stayUpOnHit)
            {
                if (Time.time < nextMultiHitTime) return;
                nextMultiHitTime = Time.time + multiHitInterval;
                if (GameManager.I != null) GameManager.I.OnTargetHit(this, point);
                if (up) // ぐらっと後ろに揺れて戻る (OnTargetHit の中で Lower() された時は揺らさない)
                {
                    angle = hitWobbleDeg;
                    targetAngle = 0f;
                    animSpeed = hitWobbleDeg / 0.15f;
                    SetPose(true, angle);
                }
                return;
            }

            up = false;
            lowerTimer = 0f;
            if (GameManager.I != null) GameManager.I.OnTargetHit(this, point);
            if (knockOffOnHit) StartKnockOff();
            else StartFall(FallTime);
            if (!manualControl)
            {
                pendingRespawn = true;
                respawnTimer = respawnDelay;
            }
        }

        bool CanRespawn()
        {
            if (!available || knocking) return false;
            if (kind == TargetKind.Fever && GameManager.I != null && GameManager.I.IsFever) return false;
            return true;
        }

        void StartRise()
        {
            targetAngle = 0f;
            animSpeed = 90f / RiseTime;
            Rising?.Invoke(this);
            SetPose(true, angle);
        }

        void StartFall(float duration)
        {
            targetAngle = 90f;
            animSpeed = 90f / duration;
        }

        /// <summary>
        /// 弾き飛ばされる (knockOffOnHit)。奥 (+Z。表の -Z がプレイヤー向き) へ跳ねて、回りながら落ち、
        /// 根元から knockDropHeight だけ下まで落ちたら消える。見た目 (visual) だけを動かし、消える時に元の位置へ戻す。
        /// </summary>
        void StartKnockOff()
        {
            knocking = true;
            knockTime = 0f;
            angle = targetAngle = 90f; // 倒れた扱い (見た目は落ちきるまで出しておく)
            knockHome = visual.localPosition;
            knockStart = visual.position;
            knockBackDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            knockSpinAxis = transform.right; // この軸の正の回転で、上が奥へ倒れる
            knockVel = knockBackDir * knockBackSpeed * Random.Range(0.85f, 1.15f)
                     + Vector3.up * knockUpSpeed * Random.Range(0.8f, 1.2f)
                     + transform.right * Random.Range(-0.25f, 0.25f);
            knockSpin = Random.Range(450f, 750f);
            knockFloorY = transform.position.y - knockDropHeight;
        }

        void UpdateKnockOff()
        {
            float dt = Time.deltaTime;
            knockTime += dt;
            knockVel += Physics.gravity * dt;
            Vector3 p = visual.position + knockVel * dt;
            // 奥へは knockMaxBack まで (奥の壁に当たったら、そこから壁ぞいに落ちる)
            float back = Vector3.Dot(p - knockStart, knockBackDir);
            if (back > knockMaxBack)
            {
                p -= knockBackDir * (back - knockMaxBack);
                knockVel -= knockBackDir * Mathf.Max(0f, Vector3.Dot(knockVel, knockBackDir));
            }
            visual.position = p;
            visual.rotation = Quaternion.AngleAxis(knockSpin * dt, knockSpinAxis) * visual.rotation;
            if (p.y < knockFloorY || knockTime > 3f) EndKnockOff();
        }

        void EndKnockOff()
        {
            knocking = false;
            visual.localPosition = knockHome;
            SetPose(false, 90f);
        }

        void SnapTo(float a)
        {
            if (knocking) // 飛んでいる途中でも、見た目を元の位置へ戻してから
            {
                knocking = false;
                visual.localPosition = knockHome;
            }
            angle = targetAngle = a;
            SetPose(a < 90f, a);
        }

        void SetPose(bool visible, float tiltDeg)
        {
            if (visual == null) return;
            visual.gameObject.SetActive(visible);
            visual.localRotation = Quaternion.Euler(tiltDeg, 0f, 0f);
        }
    }
}
