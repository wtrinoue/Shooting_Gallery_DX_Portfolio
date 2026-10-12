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
        [Header("スナイパーチャレンジ")]
        public AudioClip sniperShot, sniperReady, sniperMiss;
        [Header("ボーナスステージ")]
        public AudioClip door, complete;
        [Header("ステージアップ (屋台が増える時)")]
        public AudioClip stageUp;
        [Header("豪華屋台チェンジ")]
        public AudioClip deluxe;
        [Header("マシンガンチャレンジ (カップを倒した音。空なら自動生成。複数入れると毎回ランダムに選ぶ)")]
        public AudioClip[] canHits = new AudioClip[0];
        [Header("同時に鳴る音の抑え方")]
        [Tooltip("同じ種類の音がこの秒数以内に続いたら、2回目以降は鳴らさない (複数の的を同時に倒した時など)")]
        public float sameSoundGap = 0.07f;
        [Tooltip("この秒数以内に同じ種類の音が続くあいだは、だんだん小さくする (爆弾の連鎖やマシンガンの連続ヒット)")]
        public float burstWindow = 0.4f;
        [Range(0.1f, 1f), Tooltip("続けて鳴った時の音量の下限 (1回目に対する割合)")]
        public float burstMinVolume = 0.35f;
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
            if (stageUp == null) stageUp = PowerUp();
            if (deluxe == null) deluxe = DeluxeFanfare();
            if (sniperShot == null) sniperShot = Noise("SniperShot", 0.9f, 4.5f, 2.2f, 0.5f); // 乾いた大きな発砲音が長く尾を引く
            if (sniperReady == null) sniperReady = Tones("SniperReady", new[] { 1760f, 0f, 1760f, 0f, 2349f }, 0.07f); // ピッ ピッ ピーン (照準)
            if (sniperMiss == null) sniperMiss = Tones("SniperMiss", new[] { 392f, 330f, 262f }, 0.16f); // ざんねん…
            if (canHits == null || canHits.Length == 0) // カーン、カラン… (高さを変えて3種類。続けて当てても単調にならないように)
                canHits = new[] { CanHit("CanHit0", 1250f, 1), CanHit("CanHit1", 1080f, 2), CanHit("CanHit2", 1420f, 3) };
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
                     : type == WeaponType.Sniper ? instance.sniperShot
                     : instance.handgunShot;
            instance.PlayLimited("shot", clip, 0.5f, fade: false); // 二丁拳銃で左右同時に撃っても1発分の音
        }

        // 音の種類ごとの、最後に鳴らした時刻と、続けて鳴った回数
        readonly System.Collections.Generic.Dictionary<string, (float time, int count)> recent =
            new System.Collections.Generic.Dictionary<string, (float, int)>();

        /// <summary>
        /// 同じ種類の音が重なりすぎないように鳴らす。ほぼ同時 (sameSoundGap 以内) なら鳴らさず、
        /// 短い間隔で続く時は回数に応じて音量を下げる。
        /// </summary>
        void PlayLimited(string group, AudioClip clip, float volume, bool fade = true)
        {
            if (clip == null) return;
            float now = Time.unscaledTime;
            int count = 0;
            if (recent.TryGetValue(group, out var last))
            {
                float gap = now - last.time;
                if (gap < sameSoundGap) return;
                if (fade && gap < burstWindow) count = last.count + 1;
            }
            recent[group] = (now, count);
            volume *= Mathf.Max(burstMinVolume, 1f / (1f + 0.5f * count));
            source.PlayOneShot(clip, volume);
        }

        public static void Play(string name)
        {
            if (instance == null) return;
            if (name == "can")
            {
                var cans = instance.canHits;
                if (cans != null && cans.Length > 0) instance.PlayLimited("can", cans[Random.Range(0, cans.Length)], 0.7f);
                return;
            }
            // 的に当たった音 (hit / bonus) は同じ種類として扱い、同時に倒しても1回だけ鳴らす
            string group = name == "bonus" ? "hit" : name;
            bool limited = name == "hit" || name == "bonus" || name == "explosion" || name == "pickup" || name == "ufo";
            AudioClip clip = name switch
            {
                "stageup" => instance.stageUp,
                "sniperready" => instance.sniperReady,
                "snipermiss" => instance.sniperMiss,
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
                "deluxe" => instance.deluxe,
                _ => null
            };
            if (clip == null) return;
            if (limited) instance.PlayLimited(group, clip, 0.6f);
            else instance.source.PlayOneShot(clip, 0.6f);
        }

        /// <summary>
        /// ステージアップ (グレードアップ) 音: 「ギュイーン」と上がるスイープ → キラッとした3段の上昇音 → 明るい和音。
        /// </summary>
        static AudioClip PowerUp()
        {
            const float SweepLen = 0.45f;
            float[] steps = { 1046.5f, 1318.5f, 1568f };   // ド ミ ソ (高い方)
            const float StepLen = 0.08f;
            var chord = Bell("StageUpChord", new[] { 783.99f, 1046.5f, 1318.5f, 2093f }, 1.2f);
            var chordData = new float[chord.samples];
            chord.GetData(chordData, 0);
            Destroy(chord);

            int sweepN = (int)(Rate * SweepLen), stepN = (int)(Rate * StepLen);
            int chordStart = sweepN + stepN * steps.Length;
            var data = new float[chordStart + chordData.Length];

            // 1) 200Hz → 1000Hz へ指数的に上がるスイープ (ビブラート付き、矩形波ぎみ)
            float phase = 0f;
            for (int i = 0; i < sweepN; i++)
            {
                float t = i / (float)sweepN;
                float f = 200f * Mathf.Pow(5f, t) * (1f + 0.03f * Mathf.Sin(2f * Mathf.PI * 18f * i / Rate));
                phase += 2f * Mathf.PI * f / Rate;
                float s = Mathf.Sin(phase);
                float env = Mathf.Clamp01(i / 300f) * (0.5f + 0.5f * t);
                data[i] = (0.18f * Mathf.Sign(s) + 0.15f * s) * env;
            }
            // 2) キラッ・キラッ・キラッ
            for (int k = 0; k < steps.Length; k++)
                for (int i = 0; i < stepN * 2 && sweepN + k * stepN + i < data.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 14f) * Mathf.Clamp01(i / 80f);
                    float s = Mathf.Sin(2f * Mathf.PI * steps[k] * t);
                    data[sweepN + k * stepN + i] += (0.2f * Mathf.Sign(s) + 0.15f * s) * env;
                }
            // 3) 明るい和音を響かせる (少し揺らしてキラキラ感)
            for (int i = 0; i < chordData.Length; i++)
            {
                float t = i / (float)Rate;
                data[chordStart + i] += chordData[i] * 1.5f * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 9f * t));
            }
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);

            var clip = AudioClip.Create("StageUp", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
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

        /// <summary>
        /// 豪華屋台チェンジの音: ハープのように2オクターブを駆け上がるきらきらした分散和音 → 大きな和音がうねりながら長く響く。
        /// </summary>
        static AudioClip DeluxeFanfare()
        {
            float[] run = { 523.25f, 659.25f, 783.99f, 987.77f, 1046.5f, 1318.5f, 1568f, 1975.5f, 2093f }; // ド ミ ソ シ ド…
            const float NoteLen = 0.055f;
            var chord = Bell("DeluxeChord", new[] { 261.63f, 523.25f, 659.25f, 783.99f, 987.77f, 1318.5f }, 2.2f);
            var chordData = new float[chord.samples];
            chord.GetData(chordData, 0);
            Destroy(chord);

            int per = (int)(Rate * NoteLen);
            int chordStart = per * run.Length;
            var data = new float[chordStart + chordData.Length];
            for (int k = 0; k < run.Length; k++)
                for (int i = 0; i < per * 6 && k * per + i < data.Length; i++) // 長く重ねてハープのように
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 6f) * Mathf.Clamp01(i / 60f);
                    data[k * per + i] += 0.22f * env * (Mathf.Sin(2f * Mathf.PI * run[k] * t) + 0.25f * Mathf.Sin(6f * Mathf.PI * run[k] * t));
                }
            for (int i = 0; i < chordData.Length; i++)
            {
                float t = i / (float)Rate;
                data[chordStart + i] += chordData[i] * 2f * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 6f * t)); // うねり
            }
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);

            var clip = AudioClip.Create("Deluxe", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 空き缶を倒した音: 「カーン」(金属らしい、倍音が整数倍でない響き + 当たった瞬間のノイズ) の後に、
        /// 台に落ちて跳ねる「カラン、カラ」を小さく2回重ねる。baseHz で音の高さ、seed で跳ねる間隔を変える。
        /// </summary>
        static AudioClip CanHit(string name, float baseHz, int seed)
        {
            var rng = new System.Random(seed);
            float[] ratios = { 1f, 1.58f, 2.31f, 3.17f, 4.43f };
            float[] amps = { 1f, 0.7f, 0.5f, 0.35f, 0.25f };
            float[] decays = { 14f, 18f, 24f, 30f, 38f };
            // (始まる秒数, 音量, 高さの倍率): 当たった音 → 跳ねる音2回
            var strikes = new[]
            {
                (0f, 1f, 1f),
                (0.15f + 0.05f * (float)rng.NextDouble(), 0.45f, 0.96f),
                (0.29f + 0.05f * (float)rng.NextDouble(), 0.2f, 1.03f)
            };
            int n = (int)(Rate * 0.6f);
            var data = new float[n];
            foreach (var (startSec, gain, pitch) in strikes)
            {
                int s0 = (int)(startSec * Rate);
                for (int i = 0; s0 + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float v = 0f;
                    for (int k = 0; k < ratios.Length; k++)
                        v += amps[k] * Mathf.Exp(-t * decays[k] * (gain < 1f ? 1.6f : 1f)) * Mathf.Sin(2f * Mathf.PI * baseHz * pitch * ratios[k] * t);
                    // 当たった瞬間の「カッ」 (ごく短いノイズ)
                    v += ((float)rng.NextDouble() * 2f - 1f) * 1.2f * Mathf.Exp(-t * 400f);
                    data[s0 + i] += v * gain * 0.22f * Mathf.Clamp01(i / 20f);
                }
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
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
                if (freqs[k] <= 0f) continue; // 0 は休符
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
