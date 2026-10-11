using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 効果音。音素材なしで動くよう、起動時にコードで波形を生成する。
    /// 本番の音に差し替える場合は各 AudioClip フィールドに割り当てればそちらが優先される。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Sfx : MonoBehaviour
    {
        public AudioClip handgunShot, machineGunShot, shotgunShot, bombShot, explosion, ufoHit, hit, bonus, pickup, fever, start, end;
        [Header("ボーナスステージ")]
        public AudioClip door, complete;
        [Tooltip("ド レ ミ ファ ソ ラ シ ド の8音 (空なら自動生成)")]
        public AudioClip[] scaleNotes = new AudioClip[0];

        // ド レ ミ ファ ソ ラ シ ド (C5 〜 C6)
        static readonly float[] ScaleHz = { 523.25f, 587.33f, 659.25f, 698.46f, 783.99f, 880f, 987.77f, 1046.5f };

        static Sfx instance;
        AudioSource source;
        const int Rate = 44100;

        void Awake()
        {
            instance = this;
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            if (handgunShot == null) handgunShot = Noise("HandgunShot", 0.12f, 30f, 0.8f);
            if (machineGunShot == null) machineGunShot = Noise("MachineGunShot", 0.07f, 45f, 0.6f);
            if (shotgunShot == null) shotgunShot = Noise("ShotgunShot", 0.3f, 14f, 1f);
            if (bombShot == null) bombShot = Noise("BombShot", 0.2f, 20f, 2f, 0.12f);
            if (explosion == null) explosion = Noise("Explosion", 0.8f, 5f, 4f, 0.06f);
            if (hit == null) hit = Tones("Hit", new[] { 1320f }, 0.09f);
            if (bonus == null) bonus = Tones("Bonus", new[] { 1046f, 1568f }, 0.08f);
            if (pickup == null) pickup = Tones("Pickup", new[] { 523f, 659f, 784f, 1046f }, 0.07f);
            if (fever == null) fever = Tones("Fever", new[] { 523f, 659f, 784f, 1046f, 784f, 1046f, 1318f }, 0.1f);
            if (start == null) start = Tones("Start", new[] { 784f, 1046f }, 0.12f);
            if (end == null) end = Tones("End", new[] { 1046f, 784f, 659f, 523f }, 0.15f);
            if (ufoHit == null) ufoHit = Tones("UfoHit", new[] { 1568f, 1318f, 1046f, 880f, 784f, 659f, 523f, 1046f, 1568f, 2093f }, 0.05f);
            if (door == null) door = Noise("Door", 1.3f, 2.2f, 3.5f, 0.03f); // ゴゴゴ…と床がスライドする音
            if (complete == null) complete = Fanfare();
            if (scaleNotes == null || scaleNotes.Length < ScaleHz.Length)
            {
                scaleNotes = new AudioClip[ScaleHz.Length];
                for (int i = 0; i < ScaleHz.Length; i++) scaleNotes[i] = Bell($"Note{i}", new[] { ScaleHz[i] }, 0.6f);
            }
        }

        /// <summary>音階の i 番目 (0 = ド) を鳴らす</summary>
        public static void PlayNote(int index)
        {
            if (instance == null || instance.scaleNotes.Length == 0) return;
            index = Mathf.Clamp(index, 0, instance.scaleNotes.Length - 1);
            instance.source.PlayOneShot(instance.scaleNotes[index], 0.8f);
        }

        public static void PlayShot(WeaponType type)
        {
            if (instance == null) return;
            var clip = type == WeaponType.MachineGun ? instance.machineGunShot
                     : type == WeaponType.Shotgun ? instance.shotgunShot
                     : type == WeaponType.Bomb ? instance.bombShot
                     : instance.handgunShot;
            instance.source.PlayOneShot(clip, 0.5f);
        }

        public static void Play(string name)
        {
            if (instance == null) return;
            AudioClip clip = name switch
            {
                "hit" => instance.hit,
                "bonus" => instance.bonus,
                "pickup" => instance.pickup,
                "fever" => instance.fever,
                "start" => instance.start,
                "end" => instance.end,
                "explosion" => instance.explosion,
                "ufo" => instance.ufoHit,
                "door" => instance.door,
                "complete" => instance.complete,
                _ => null
            };
            if (clip != null) instance.source.PlayOneShot(clip, 0.6f);
        }

        /// <summary>
        /// UFO のうなり音 (ループ用 1 秒)。音程が 1 秒に 6 回ゆらぐ「ウォンウォン」音。
        /// 周波数の積分が 1 秒でちょうど整数周期になるので、ループのつなぎ目でノイズが出ない。
        /// </summary>
        public static AudioClip CreateUfoHum()
        {
            const float Carrier = 420f, Depth = 90f, Lfo = 6f;
            var data = new float[Rate];
            for (int i = 0; i < Rate; i++)
            {
                float t = i / (float)Rate;
                // 位相 = ∫(Carrier + Depth*sin(2πLfo t)) dt
                float phase = 2f * Mathf.PI * Carrier * t - Depth / Lfo * Mathf.Cos(2f * Mathf.PI * Lfo * t);
                data[i] = 0.35f * Mathf.Sin(phase) + 0.12f * Mathf.Sin(2f * phase);
            }
            var clip = AudioClip.Create("UfoHum", Rate, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>鐘のような音 (倍音入りのサイン波がゆっくり消える)。freqs を同時に鳴らすと和音になる。</summary>
        static AudioClip Bell(string name, float[] freqs, float length)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            float gain = 0.45f / freqs.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 5f) * Mathf.Clamp01(i / 150f);
                float s = 0f;
                foreach (float f in freqs)
                    s += Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * f * t) + 0.12f * Mathf.Sin(6f * Mathf.PI * f * t);
                data[i] = s * gain * env;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>ドレミ完走のファンファーレ: 駆け上がる分散和音 → ドミソドの和音を長く響かせる</summary>
        static AudioClip Fanfare()
        {
            float[] run = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f, 1568f };
            const float NoteLen = 0.09f;
            var chord = Bell("Chord", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 1.6f);
            var chordData = new float[chord.samples];
            chord.GetData(chordData, 0);
            Destroy(chord); // 波形だけ使う

            int per = (int)(Rate * NoteLen);
            var data = new float[per * run.Length + chordData.Length];
            for (int k = 0; k < run.Length; k++)
                for (int i = 0; i < per * 3 && k * per + i < data.Length; i++) // 少し重ねて響かせる
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 9f) * Mathf.Clamp01(i / 100f);
                    data[k * per + i] += 0.3f * env * (Mathf.Sin(2f * Mathf.PI * run[k] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * run[k] * t));
                }
            int offset = per * run.Length;
            for (int i = 0; i < chordData.Length; i++) data[offset + i] += chordData[i] * 1.6f;
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);

            var clip = AudioClip.Create("Complete", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 減衰するホワイトノイズ (銃声)
        static AudioClip Noise(string name, float length, float decay, float volume, float lowpass = 0.35f)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), lowpass); // ローパス (小さいほど低い音)
                data[i] = Mathf.Clamp(lp * Mathf.Exp(-t * decay) * volume, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 矩形波っぽいピコピコ音を順番に鳴らす
        static AudioClip Tones(string name, float[] freqs, float noteLength)
        {
            int perNote = (int)(Rate * noteLength);
            var data = new float[perNote * freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
            {
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(1f - i / (float)perNote) * Mathf.Clamp01(i / 200f);
                    float s = Mathf.Sin(2f * Mathf.PI * freqs[k] * t);
                    data[k * perNote + i] = Mathf.Sign(s) * 0.25f * env + s * 0.15f * env;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
