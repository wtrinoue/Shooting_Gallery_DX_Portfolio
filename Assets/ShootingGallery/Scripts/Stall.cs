using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>屋台1つ分。的の一括制御と、フィーバー時のせり上がり / 輪が広がるときの移動演出。</summary>
    public class Stall : MonoBehaviour
    {
        public Target[] targets;
        public float hiddenDepth = 4f;

        Vector3 restPos;
        bool restPosCaptured;
        Coroutine moveRoutine;
        bool enableTargetsOnArrive; // せり上がり途中に Slide されても、着いたら的を出す

        /// <summary>表示中で、せり上がり / 沈み込みの途中でない</summary>
        public bool IsSettled => gameObject.activeInHierarchy && moveRoutine == null;

        void Awake() => CaptureRestPos();

        void CaptureRestPos()
        {
            if (restPosCaptured) return;
            restPos = transform.localPosition;
            restPosCaptured = true;
        }

        public void SetTargetsAvailable(bool value)
        {
            foreach (var t in targets)
                if (t != null) t.SetAvailable(value);
        }

        /// <summary>地面の下へ瞬時に隠す (起動時用)</summary>
        public void HideImmediate()
        {
            CaptureRestPos();
            SetTargetsAvailable(false);
            transform.localPosition = restPos + Vector3.down * hiddenDepth;
            gameObject.SetActive(false);
        }

        /// <summary>隠れている屋台の置き場所を変える (フィーバーのたびにランダムな場所へ並べ替える用)</summary>
        public void PlaceAt(Vector3 localPos, Quaternion localRot)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = null;
            restPos = localPos;
            restPosCaptured = true;
            transform.localRotation = localRot;
            transform.localPosition = restPos + Vector3.down * hiddenDepth;
        }

        /// <summary>地下からせり上がる。enableTargets = false なら的は出さない (ゲーム終了時に屋台だけ戻す時など)</summary>
        public void Rise(float duration, bool enableTargets = true)
        {
            CaptureRestPos();
            gameObject.SetActive(true);
            enableTargetsOnArrive = enableTargets;
            Move(restPos, transform.localRotation, duration, null);
        }

        public void Sink(float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            enableTargetsOnArrive = false;
            SetTargetsAvailable(false);
            Move(restPos + Vector3.down * hiddenDepth, transform.localRotation, duration, () => gameObject.SetActive(false));
        }

        /// <summary>
        /// 出ている屋台を地上のまま別の場所へ滑らせる (輪が広がるときに後ろへ下がる / 元の位置へ戻る用)。
        /// 的は出したまま動く。隠れている屋台なら置き場所だけ変える。
        /// </summary>
        public void Slide(Vector3 localPos, Quaternion localRot, float duration)
        {
            CaptureRestPos();
            restPos = localPos;
            if (!gameObject.activeInHierarchy)
            {
                transform.localRotation = localRot;
                transform.localPosition = restPos + Vector3.down * hiddenDepth;
                return;
            }
            Move(restPos, localRot, duration, null);
        }

        void Move(Vector3 toPos, Quaternion toRot, float duration, System.Action onDone)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(MoveTo(toPos, toRot, duration, onDone));
        }

        IEnumerator MoveTo(Vector3 toPos, Quaternion toRot, float duration, System.Action onDone)
        {
            Vector3 fromPos = transform.localPosition;
            Quaternion fromRot = transform.localRotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // ease out
                transform.localPosition = Vector3.LerpUnclamped(fromPos, toPos, e);
                transform.localRotation = Quaternion.SlerpUnclamped(fromRot, toRot, e);
                yield return null;
            }
            transform.localPosition = toPos;
            transform.localRotation = toRot;
            moveRoutine = null;
            if (enableTargetsOnArrive)
            {
                enableTargetsOnArrive = false;
                SetTargetsAvailable(true);
            }
            onDone?.Invoke();
        }
    }
}
