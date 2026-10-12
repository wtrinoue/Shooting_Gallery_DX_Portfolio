using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>穴から的がランダムに顔を出す (もぐら叩き)。的は manualControl にしておくこと。</summary>
    public class PopUpGimmick : MonoBehaviour
    {
        public Target[] targets;
        public float interval = 0.45f;
        public float stayTime = 1.4f;
        public int maxUp = 5;

        float timer;
        readonly List<Target> candidates = new List<Target>();

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval * Random.Range(0.6f, 1.4f);

            int upCount = 0;
            candidates.Clear();
            foreach (var t in targets)
            {
                if (t.IsUp) upCount++;
                else if (t.CanRaise) candidates.Add(t);
            }
            if (upCount >= maxUp || candidates.Count == 0) return;

            candidates[Random.Range(0, candidates.Count)].Raise(stayTime * Random.Range(0.8f, 1.2f));
        }
    }
}
