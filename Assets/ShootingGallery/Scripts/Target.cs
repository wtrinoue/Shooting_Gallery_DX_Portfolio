using UnityEngine;

namespace ShootingGallery
{
    public enum TargetKind { Normal, MachineGun, Shotgun, Bomb, Fever, Start, Ufo, Explosive, BlueOrb, BigBonus }

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

        public bool IsUp => up && available;
        /// <summary>見た目が表示されているか (倒れるアニメーション中も true)</summary>
        public bool IsVisible => visual != null && visual.gameObject.activeSelf;
        bool Animating => !Mathf.Approximately(angle, targetAngle);
        public bool CanRaise => manualControl && available && !up && !Animating && gameObject.activeInHierarchy;

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
                    angle = 18f;
                    targetAngle = 0f;
                    animSpeed = 18f / 0.15f;
                    SetPose(true, angle);
                }
                return;
            }

            up = false;
            lowerTimer = 0f;
            if (GameManager.I != null) GameManager.I.OnTargetHit(this, point);
            StartFall(FallTime);
            if (!manualControl)
            {
                pendingRespawn = true;
                respawnTimer = respawnDelay;
            }
        }

        bool CanRespawn()
        {
            if (!available) return false;
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

        void SnapTo(float a)
        {
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
