using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 音楽モードの、空中に浮かぶ波打つ五線譜。ステージの手前 (真ん中より少し外側) から飛び出して、プレイヤーの横を通り後ろへ抜ける長い帯。
    /// 波がうねりながら流れる。ステージ側の端から伸びて現れ (Reveal)、音符を次々に流し (Play)、流れ終わったらステージ側から消えていく (Retract)。
    /// 音符 (MusicNote) は五線の線か間 (下から 0〜8 の9か所) に乗って、川の流れのようにステージからプレイヤーの方へ流れてくる。音符が的
    /// </summary>
    public class MusicStaff : MonoBehaviour
    {
        [Tooltip("五線 (下から5本) / 小節線")]
        public LineRenderer[] lines;
        public LineRenderer[] barLines;
        [Tooltip("流す音符 (使い回す)")]
        public MusicNote[] notes;
        [Tooltip("五線譜の先頭の飾り (ト音記号の代わりの光る玉など。なければ出さない)")]
        public Transform clef;
        [Tooltip("当たった音符がはじける時の光")]
        public Material sparkMaterial;

        [Tooltip("線の間隔 (m)")]
        public float lineSpacing = 0.3f;
        public int segments = 40;
        [Tooltip("伸びて現れる / 消えていく秒数")]
        public float revealTime = 2.5f;
        [Tooltip("音符を流し続ける秒数 / 音符を出す間隔 (秒) / 音符が端から端まで流れる秒数")]
        public float spawnTime = 6f;
        public float noteInterval = 0.5f;
        public float noteTravel = 7f;
        [Range(0f, 1f), Tooltip("金色の音符 (高得点) になる確率")]
        public float goldChance = 0.12f;

        enum Phase { Idle, Reveal, Play, Drain, Retract }
        Phase phase = Phase.Idle;
        float phaseStart, nextNote;
        Vector3 p0, p1, p2, p3; // 通り道 (3次ベジェ曲線。この五線譜のローカル)
        float amp, waves, wavePhase, uMin, uMax;
        int lastSlot = -1;
        readonly Vector3[] pts = new Vector3[65];

        public bool IsBusy => phase != Phase.Idle;
        /// <summary>出てきた側 (-1 = 客席から見て左 / 1 = 右)</summary>
        public float Side { get; private set; }
        /// <summary>通っているコース (ConcertHall が、ほかの五線譜と重ならないように割り当てる)</summary>
        public int Lane { get; set; } = -1;

        public void Init()
        {
            foreach (var n in notes) if (n != null) n.Hide();
            phase = Phase.Idle;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 五線譜を出す。start (ステージ側の始まり) から end (プレイヤーの横を通り過ぎた後ろ) まで、ゆるく曲がりながら伸びる (この五線譜のローカル)。
        /// 音符は start から end へ流れる
        /// </summary>
        public void Begin(Vector3 start, Vector3 end)
        {
            Side = Mathf.Sign(start.x);
            p0 = start;
            p3 = end;
            // ステージから手前へまっすぐ飛び出し、プレイヤーの近くで横へふくらんで通り過ぎる
            p1 = start + new Vector3(Side * 0.6f, 0.8f, -(start.z - end.z) * 0.3f);
            p2 = end + new Vector3(Side * 1.2f, 0.4f, (start.z - end.z) * 0.35f);
            amp = Random.Range(0.25f, 0.45f);
            waves = Random.Range(2f, 3.2f);
            wavePhase = Random.value;
            uMin = uMax = 0f;
            foreach (var n in notes) if (n != null) n.Hide();
            gameObject.SetActive(true);
            SetPhase(Phase.Reveal);
            UpdateLines();
        }

        /// <summary>モードの終わり: 音符をすぐに消し、五線譜は端から消えていく</summary>
        public void Stop()
        {
            if (phase == Phase.Idle) return;
            foreach (var n in notes) if (n != null && n.inUse) n.Hide();
            if (phase != Phase.Retract) SetPhase(Phase.Retract);
        }

        public void HideInstant()
        {
            foreach (var n in notes) if (n != null) n.Hide();
            phase = Phase.Idle;
            gameObject.SetActive(false);
        }

        void SetPhase(Phase p)
        {
            phase = p;
            phaseStart = Time.time;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time - phaseStart;
            wavePhase += dt * 0.22f; // 波が線に沿って流れる

            switch (phase)
            {
                case Phase.Reveal:
                    uMax = Smooth(t / revealTime);
                    if (t >= revealTime) { uMax = 1f; SetPhase(Phase.Play); nextNote = Time.time; }
                    break;
                case Phase.Play:
                    if (Time.time >= nextNote)
                    {
                        nextNote = Time.time + noteInterval * Random.Range(0.75f, 1.3f);
                        SpawnNote();
                    }
                    if (t >= spawnTime) SetPhase(Phase.Drain);
                    break;
                case Phase.Drain:
                    if (!AnyNote() || t >= noteTravel + 1f) SetPhase(Phase.Retract);
                    break;
                case Phase.Retract:
                    uMin = Smooth(t / revealTime);
                    if (t >= revealTime)
                    {
                        HideInstant();
                        return;
                    }
                    break;
                case Phase.Idle:
                    return;
            }
            UpdateNotes(dt);
            UpdateLines();
        }

        bool AnyNote()
        {
            foreach (var n in notes) if (n != null && n.inUse) return true;
            return false;
        }

        void SpawnNote()
        {
            // 空いている音符をランダムに選ぶ (種類がばらけるように)
            int start = Random.Range(0, notes.Length);
            MusicNote note = null;
            for (int i = 0; i < notes.Length; i++)
            {
                var n = notes[(start + i) % notes.Length];
                if (n != null && !n.inUse) { note = n; break; }
            }
            if (note == null) return;
            int slot;
            // 前の音符から2つ以上離れた高さにする (線と間に上下に散らばるように)
            do slot = Random.Range(0, 9); while (lastSlot >= 0 && Mathf.Abs(slot - lastSlot) < 2);
            lastSlot = slot;
            note.slot = slot;
            note.u = 0f;
            note.speed = 1f / Mathf.Max(1f, noteTravel) * Random.Range(0.9f, 1.15f);
            note.Show(Random.value < goldChance);
            PlaceNote(note);
        }

        void UpdateNotes(float dt)
        {
            foreach (var n in notes)
            {
                if (n == null || !n.inUse) continue;
                if (n.popped)
                {
                    if (Time.time - n.popTime > 0.3f) n.Hide();
                    continue;
                }
                n.u += n.speed * dt;
                if (n.u > 1f || n.u < uMin) { n.Hide(); continue; }
                PlaceNote(n);
                if (n.target.IsUp) { n.armed = true; continue; }
                if (!n.armed) continue;
                // 当たった (点数と音は GameManager → ConcertHall): 光がはじけて、縮んで消える
                n.popped = true;
                n.popTime = Time.time;
                if (sparkMaterial != null)
                {
                    Explosion.Spawn(n.transform.position, 0.35f, sparkMaterial);
                    SpaceDebris.Spawn(n.transform.position, 0.3f, sparkMaterial, 6);
                }
            }
        }

        void PlaceNote(MusicNote n)
        {
            var p = Point(n.u, (n.slot - 4) * lineSpacing * 0.5f);
            n.transform.localPosition = p;
            var d = new Vector3(p.x, 0f, p.z);
            if (d.sqrMagnitude > 0.0001f) n.transform.localRotation = Quaternion.LookRotation(d.normalized); // 表 (-Z) がプレイヤーの方
        }

        void UpdateLines()
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(segments * (uMax - uMin)) + 1);
            count = Mathf.Min(count, pts.Length);
            for (int k = 0; k < lines.Length; k++)
            {
                var lr = lines[k];
                if (lr == null) continue;
                if (uMax - uMin < 0.002f) { lr.positionCount = 0; continue; }
                float off = (k - 2) * lineSpacing;
                for (int i = 0; i < count; i++) pts[i] = Point(Mathf.Lerp(uMin, uMax, i / (float)(count - 1)), off);
                lr.positionCount = count;
                lr.SetPositions(pts);
            }
            for (int b = 0; b < barLines.Length; b++)
            {
                var lr = barLines[b];
                if (lr == null) continue;
                float u = barLines.Length == 1 ? 0.5f : b / (float)(barLines.Length - 1); // 両端と、その間に等間隔
                if (u < uMin || u > uMax) { lr.positionCount = 0; continue; }
                lr.positionCount = 2;
                lr.SetPosition(0, Point(u, -2f * lineSpacing));
                lr.SetPosition(1, Point(u, 2f * lineSpacing));
            }
            if (clef != null)
            {
                bool show = uMin <= 0.02f && uMax > 0.02f;
                clef.gameObject.SetActive(show);
                if (show)
                {
                    clef.localPosition = Point(0.02f, 0f);
                    var d = clef.localPosition;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.0001f) clef.localRotation = Quaternion.LookRotation(d.normalized);
                }
            }
        }

        /// <summary>通り道に沿って u (0 = ステージ側 〜 1 = プレイヤーの後ろ) の所の、真ん中の線から yOffset 上の位置 (この五線譜のローカル)</summary>
        Vector3 Point(float u, float yOffset)
        {
            float v = 1f - u;
            var p = v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
            p.y += amp * Mathf.Sin((u * waves + wavePhase) * Mathf.PI * 2f) + yOffset;
            return p;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
