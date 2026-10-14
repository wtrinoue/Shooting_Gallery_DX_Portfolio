using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 音楽モードの曲と楽器の音。効果音 (Sfx) と同じく、音素材なしで起動時にコードで波形を作る。
    ///   曲: ハ長調 4/4 拍子・8小節のループ (弦の和音 + フルートの旋律 + ハープの分散和音 + 低音 + ティンパニ + 金管の合いの手 + シンバル)。
    ///       ループの終わりからはみ出した音の余韻は頭に回して足すので、つなぎ目で途切れない。
    ///   楽器の音: 楽器の種類 (Voice) ごとに1つずつ作り、鳴らす時に AudioSource.pitch で高さを変える (大きい楽器ほど低く)。
    /// 作るのは数フレームに分けて (Build)。WebGL でも固まらないように
    /// </summary>
    public static class ConcertMusic
    {
        public const int Rate = 32000;
        public const float Bpm = 112f;
        public const int Bars = 8;
        public static float BeatSeconds => 60f / Bpm;
        public static int LoopBeats => Bars * 4;

        /// <summary>楽器の音色</summary>
        public enum Voice { Strings, Flute, Reed, Brass, Pluck, Piano, Timpani, Bell, Mallet, Drum, Snare, Cymbal, Gong }

        // 小節ごとの和音 (C - Am - F - G - C - F - G7 - C)
        static readonly int[][] Chords =
        {
            new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 },
            new[] { 60, 64, 67 }, new[] { 53, 57, 60 }, new[] { 55, 59, 65 }, new[] { 60, 64, 67 },
        };
        static readonly int[] Roots = { 48, 45, 41, 43, 48, 41, 43, 48 };

        // 旋律 (音の高さ, 拍)。高さ 0 は休み
        static readonly (int midi, float beats)[] Melody =
        {
            (76, 1f), (79, 1f), (84, 1f), (79, 1f),
            (81, 1f), (79, 0.5f), (77, 0.5f), (76, 2f),
            (77, 1f), (81, 1f), (84, 1f), (81, 1f),
            (79, 2f), (77, 1f), (74, 1f),
            (76, 0.5f), (77, 0.5f), (79, 1f), (84, 1f), (88, 1f),
            (86, 1f), (84, 1f), (81, 1f), (77, 1f),
            (79, 1f), (83, 1f), (86, 1f), (83, 1f),
            (84, 3f), (0, 1f),
        };

        /// <summary>音の高さ (MIDI の番号。60 = 真ん中のド) の周波数</summary>
        public static float Hz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        /// <summary>beat 拍目 (ループの頭から) の和音</summary>
        public static int[] ChordAt(float beat)
        {
            int bar = Mathf.FloorToInt(Mathf.Repeat(beat, LoopBeats) / 4f);
            return Chords[Mathf.Clamp(bar, 0, Bars - 1)];
        }

        /// <summary>今の和音の音のうち、near にいちばん近い高さ (撃った楽器の音が曲に合うように)</summary>
        public static int ChordToneNear(float beat, int near)
        {
            int best = near, bestDist = int.MaxValue;
            foreach (int c in ChordAt(beat))
                for (int oct = -4; oct <= 4; oct++)
                {
                    int m = c + oct * 12;
                    int d = Mathf.Abs(m - near);
                    if (d < bestDist) { bestDist = d; best = m; }
                }
            return best;
        }

        /// <summary>楽器の音の、元の高さ (この高さで作り、pitch で上げ下げする)。高さのない打楽器は 0</summary>
        public static int BaseMidi(Voice v) => v switch
        {
            Voice.Strings => 60,
            Voice.Flute => 74,
            Voice.Reed => 60,
            Voice.Brass => 55,
            Voice.Pluck => 64,
            Voice.Piano => 60,
            Voice.Timpani => 43,
            Voice.Bell => 79,
            Voice.Mallet => 76,
            _ => 0,
        };

        static float LengthOf(Voice v) => v switch
        {
            Voice.Gong => 3.5f,
            Voice.Cymbal => 2.2f,
            Voice.Bell => 2f,
            Voice.Piano => 2f,
            Voice.Timpani => 1.6f,
            Voice.Pluck => 1.4f,
            Voice.Drum => 0.9f,
            Voice.Snare => 0.5f,
            Voice.Mallet => 0.9f,
            _ => 0.9f, // 弦・管は 0.6 秒のばして余韻
        };

        /// <summary>作った曲 (なければ null) と楽器の音</summary>
        public static AudioClip Loop { get; private set; }
        public static AudioClip Curtain { get; private set; }
        static readonly Dictionary<Voice, AudioClip> voices = new Dictionary<Voice, AudioClip>();
        static bool building;

        public static AudioClip VoiceClip(Voice v) => voices.TryGetValue(v, out var c) ? c : null;

        // 再生のたびに作り直す (エディターで「ドメインの再読み込み」を切っていると、前の再生で消えた音を覚えたままになるので)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            building = false;
            voices.Clear();
            Loop = null;
            Curtain = null;
        }

        /// <summary>曲と楽器の音を作る (まだなら)。host のコルーチンで、1フレームあたり数ミリ秒ずつ</summary>
        public static void Build(MonoBehaviour host)
        {
            if (building || Loop != null || host == null) return;
            building = true;
            host.StartCoroutine(BuildRoutine());
        }

        static IEnumerator BuildRoutine()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // 楽器の音
            foreach (Voice v in System.Enum.GetValues(typeof(Voice)))
            {
                if (voices.ContainsKey(v)) continue;
                float len = LengthOf(v);
                int hold = v == Voice.Strings || v == Voice.Flute || v == Voice.Reed || v == Voice.Brass ? 1 : 0;
                var data = new float[(int)(len * Rate)];
                float hz = BaseMidi(v) > 0 ? Hz(BaseMidi(v)) : 0f;
                Render(data, 0, hz, hold > 0 ? 0.35f : 0.05f, v, 0.8f, wrap: false);
                Normalize(data, 0.85f);
                voices[v] = MakeClip($"Concert_{v}", data);
                if (watch.ElapsedMilliseconds > 4) { yield return null; watch.Restart(); }
            }

            // カーテンが動く音 (シャーッという布の音と、レールのリングがカラカラ鳴る音)
            if (Curtain == null)
            {
                Curtain = CurtainSwish();
                yield return null;
                watch.Restart();
            }

            // 曲 (8小節のループ)
            float beatSec = BeatSeconds;
            var buf = new float[Mathf.RoundToInt(LoopBeats * beatSec * Rate)];
            var notes = Score();
            foreach (var n in notes)
            {
                Render(buf, Mathf.RoundToInt(n.beat * beatSec * Rate), n.midi > 0 ? Hz(n.midi) : 0f, n.beats * beatSec, n.voice, n.vol, wrap: true);
                if (watch.ElapsedMilliseconds > 4) { yield return null; watch.Restart(); }
            }
            Normalize(buf, 0.8f);
            Loop = MakeClip("Concert_Loop", buf);
            building = false;
        }

        struct Note
        {
            public float beat, beats, vol;
            public int midi;
            public Voice voice;
            public Note(float beat, float beats, int midi, Voice voice, float vol) { this.beat = beat; this.beats = beats; this.midi = midi; this.voice = voice; this.vol = vol; }
        }

        // 曲の譜面
        static List<Note> Score()
        {
            var list = new List<Note>();
            for (int bar = 0; bar < Bars; bar++)
            {
                float b0 = bar * 4f;
                var chord = Chords[bar];
                int root = Roots[bar];
                // 弦の和音 (のばす)
                foreach (int c in chord) list.Add(new Note(b0, 3.9f, c, Voice.Strings, 0.05f));
                // 低音 (チェロ・コントラバス): 2分音符で根音と5度
                list.Add(new Note(b0, 1.9f, root, Voice.Strings, 0.1f));
                list.Add(new Note(b0 + 2f, 1.9f, bar % 2 == 0 ? root + 7 : root, Voice.Strings, 0.08f));
                // ハープの分散和音 (8分音符で上がって下がる)
                int[] arp = { chord[0], chord[1], chord[2], chord[0] + 12, chord[2] + 12, chord[0] + 12, chord[2], chord[1] };
                for (int i = 0; i < 8; i++) list.Add(new Note(b0 + i * 0.5f, 0.5f, arp[i] + 12, Voice.Pluck, 0.045f));
                // ティンパニ: 1拍目に根音、3拍目に軽く
                list.Add(new Note(b0, 1f, root, Voice.Timpani, 0.22f));
                list.Add(new Note(b0 + 2f, 1f, bar % 2 == 0 ? root + 7 : root, Voice.Timpani, 0.1f));
            }
            // 金管の合いの手 (1・4・8小節目)
            foreach (int bar in new[] { 0, 3, 7 })
            {
                var chord = Chords[bar];
                float len = bar == 7 ? 1.9f : 0.9f;
                foreach (int c in chord) list.Add(new Note(bar * 4f, len, c - 12, Voice.Brass, 0.06f));
            }
            list.Add(new Note(3 * 4f + 2f, 0.9f, 55, Voice.Brass, 0.06f));
            list.Add(new Note(3 * 4f + 3f, 0.9f, 59, Voice.Brass, 0.06f));
            // シンバル (1・5小節目の頭)
            list.Add(new Note(0f, 1f, 0, Voice.Cymbal, 0.08f));
            list.Add(new Note(16f, 1f, 0, Voice.Cymbal, 0.06f));
            // 旋律: フルート。後半 (5〜8小節) はバイオリンが1オクターブ下で重なる
            float beat = 0f;
            foreach (var (midi, beats) in Melody)
            {
                if (midi > 0)
                {
                    list.Add(new Note(beat, beats * 0.92f, midi, Voice.Flute, 0.16f));
                    if (beat >= 16f) list.Add(new Note(beat, beats * 0.95f, midi - 12, Voice.Strings, 0.06f));
                }
                beat += beats;
            }
            return list;
        }

        // ------------------------------------------------------------------
        // 音を作る
        // ------------------------------------------------------------------

        const int TableSize = 1024;
        static float[] stringsTable, fluteTable, reedTable, brassTable;
        static readonly System.Random rng = new System.Random(4321);

        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        // 1周期ぶんの波形 (倍音の強さを並べたもの)
        static float[] Table(float[] harmonics)
        {
            var t = new float[TableSize];
            for (int i = 0; i < TableSize; i++)
            {
                float x = i / (float)TableSize * Mathf.PI * 2f;
                float s = 0f;
                for (int h = 0; h < harmonics.Length; h++) s += harmonics[h] * Mathf.Sin(x * (h + 1));
                t[i] = s;
            }
            float peak = 0f;
            foreach (float v in t) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < TableSize; i++) t[i] /= Mathf.Max(0.001f, peak);
            return t;
        }

        static void EnsureTables()
        {
            if (stringsTable != null) return;
            var saw = new float[10];
            for (int h = 0; h < saw.Length; h++) saw[h] = 1f / (h + 1);
            stringsTable = Table(saw);                                                     // のこぎり波 (弦)
            fluteTable = Table(new[] { 1f, 0.3f, 0.1f, 0.04f });                            // ほぼ正弦波 (フルート)
            reedTable = Table(new[] { 1f, 0.12f, 0.55f, 0.08f, 0.35f, 0.05f, 0.2f });       // 奇数の倍音が強い (クラリネット・サックス)
            var brass = new float[10];
            for (int h = 0; h < brass.Length; h++) brass[h] = 1f / Mathf.Pow(h + 1, 0.6f);
            brassTable = Table(brass);                                                     // 明るい (金管)
        }

        /// <summary>
        /// buf の start サンプル目から、高さ hz・長さ dur 秒の音を vol の大きさで足す。
        /// wrap なら buf の終わりからはみ出した分を頭に回す (ループのつなぎ目で音が切れないように)
        /// </summary>
        static void Render(float[] buf, int start, float hz, float dur, Voice v, float vol, bool wrap)
        {
            EnsureTables();
            float release = v switch
            {
                Voice.Strings => 0.35f, Voice.Flute => 0.2f, Voice.Reed => 0.15f, Voice.Brass => 0.15f,
                Voice.Pluck => wrap ? 0.9f : LengthOf(v), // 曲の中のハープは短めに (作る手間を減らす)
                _ => LengthOf(v),
            };
            // ハープの3つの倍音の減り方 (毎サンプル Exp を呼ばずに掛け算で減らす)
            float pe1 = 1f, pe2 = 1f, pe3 = 1f;
            float pd1 = Mathf.Exp(-3.6f / Rate), pd2 = Mathf.Exp(-5.2f / Rate), pd3 = Mathf.Exp(-6.8f / Rate);
            int n = Mathf.Min((int)((dur + release) * Rate), wrap ? buf.Length : buf.Length - start);
            float phase = 0f, phase2 = 0.37f;
            float lp = 0f, prevNoise = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s;
                switch (v)
                {
                    case Voice.Strings:
                    case Voice.Flute:
                    case Voice.Reed:
                    case Voice.Brass:
                    {
                        float attack = v == Voice.Strings ? 0.07f : v == Voice.Flute ? 0.05f : 0.03f;
                        float env = Mathf.Min(1f, t / attack);
                        if (t > dur) env *= Mathf.Max(0f, 1f - (t - dur) / release);
                        else env *= 1f - 0.12f * (t / Mathf.Max(0.01f, dur));
                        float vib = 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 5.5f * t) * Mathf.Min(1f, t / 0.3f);
                        float[] table = v == Voice.Strings ? stringsTable : v == Voice.Flute ? fluteTable : v == Voice.Reed ? reedTable : brassTable;
                        phase += hz * vib / Rate;
                        phase -= Mathf.Floor(phase);
                        s = table[(int)(phase * TableSize) & (TableSize - 1)];
                        if (v == Voice.Strings) // 少しずらした2本目を重ねて、弦の合奏らしい厚みに
                        {
                            phase2 += hz * 1.004f * vib / Rate;
                            phase2 -= Mathf.Floor(phase2);
                            s = 0.6f * s + 0.6f * table[(int)(phase2 * TableSize) & (TableSize - 1)];
                        }
                        if (v == Voice.Flute) s += 0.04f * Noise(); // 息の音
                        if (v == Voice.Brass) s *= 0.7f + 0.3f * Mathf.Min(1f, t / 0.08f); // 吹き始めに明るくなる
                        s *= env;
                        break;
                    }
                    case Voice.Pluck:
                    {
                        float w = 2f * Mathf.PI * hz * t;
                        s = Mathf.Sin(w) * pe1 + Mathf.Sin(2f * w) * pe2 * 0.5f + Mathf.Sin(3f * w) * pe3 * 0.33f;
                        pe1 *= pd1; pe2 *= pd2; pe3 *= pd3;
                        s *= Mathf.Min(1f, t / 0.003f);
                        break;
                    }
                    case Voice.Piano:
                        s = 0f;
                        for (int h = 1; h <= 6; h++) s += Mathf.Sin(2f * Mathf.PI * hz * h * (1f + 0.0004f * h * h) * t) * Mathf.Exp(-t * (1.2f + 0.9f * h)) / Mathf.Pow(h, 1.2f);
                        s *= Mathf.Min(1f, t / 0.004f);
                        break;
                    case Voice.Bell:
                        s = Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * 2.5f)
                          + 0.4f * Mathf.Sin(2f * Mathf.PI * hz * 2.76f * t) * Mathf.Exp(-t * 4f)
                          + 0.2f * Mathf.Sin(2f * Mathf.PI * hz * 5.4f * t) * Mathf.Exp(-t * 7f);
                        s *= Mathf.Min(1f, t / 0.002f);
                        break;
                    case Voice.Mallet: // 木琴: 短く、カツッとした響き
                        s = Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * 7f) + 0.35f * Mathf.Sin(2f * Mathf.PI * hz * 3.93f * t) * Mathf.Exp(-t * 16f);
                        s *= Mathf.Min(1f, t / 0.001f);
                        break;
                    case Voice.Timpani:
                    {
                        float f = hz * (1f + 0.03f * Mathf.Exp(-t * 10f));
                        phase += f / Rate;
                        s = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * 2.2f)
                          + 0.4f * Mathf.Sin(2f * Mathf.PI * phase * 1.5f) * Mathf.Exp(-t * 3.5f)
                          + 0.35f * Noise() * Mathf.Exp(-t * 35f);
                        break;
                    }
                    case Voice.Drum: // 大太鼓: ドーン
                    {
                        float f = 55f * (1f + 0.8f * Mathf.Exp(-t * 18f));
                        phase += f / Rate;
                        s = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * 3.5f) + 0.3f * Noise() * Mathf.Exp(-t * 40f);
                        break;
                    }
                    case Voice.Snare: // 小太鼓: タッ (響き線のザラッとした音)
                        s = 0.7f * Noise() * Mathf.Exp(-t * 16f) + 0.5f * Mathf.Sin(2f * Mathf.PI * 190f * t) * Mathf.Exp(-t * 22f);
                        break;
                    case Voice.Cymbal: // シャーン (高い音だけ残したノイズ)
                    {
                        float nz = Noise();
                        s = (nz - prevNoise) * 0.6f * Mathf.Exp(-t * 1.8f) * Mathf.Min(1f, t / 0.002f);
                        prevNoise = nz;
                        break;
                    }
                    case Voice.Gong: // ゴォーン (ゆっくり立ち上がって長く響く、うなる音)
                    {
                        float bend = 1f - 0.02f * Mathf.Exp(-t * 1.5f);
                        s = Mathf.Sin(2f * Mathf.PI * 92f * bend * t)
                          + 0.7f * Mathf.Sin(2f * Mathf.PI * 92f * 1.53f * bend * t)
                          + 0.5f * Mathf.Sin(2f * Mathf.PI * 92f * 2.11f * t)
                          + 0.35f * Mathf.Sin(2f * Mathf.PI * 92f * 2.84f * t);
                        lp += (Noise() - lp) * 0.05f;
                        s = (s * 0.3f + lp * 0.8f) * Mathf.Min(1f, t / 0.15f) * Mathf.Exp(-t * 0.7f);
                        break;
                    }
                    default:
                        s = 0f;
                        break;
                }
                int idx = start + i;
                if (idx >= buf.Length)
                {
                    if (!wrap) break;
                    idx %= buf.Length;
                }
                buf[idx] += s * vol;
            }
        }

        static AudioClip CurtainSwish()
        {
            const float Len = 2.2f;
            var data = new float[(int)(Len * Rate)];
            float lp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / Len)) * (0.75f + 0.25f * Mathf.Sin(t * 9f));
                lp += (Noise() - lp) * 0.12f;
                data[i] = lp * env * 0.9f;
            }
            // レールのリングがカラカラ鳴る
            for (int k = 0; k < 28; k++)
            {
                int at = (int)((0.1f + k * 0.072f + (float)rng.NextDouble() * 0.03f) * Rate);
                float hz = 2600f + (float)rng.NextDouble() * 900f;
                for (int i = 0; i < Rate / 25 && at + i < data.Length; i++)
                {
                    float t = i / (float)Rate;
                    data[at + i] += 0.12f * Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * 90f);
                }
            }
            Normalize(data, 0.7f);
            return MakeClip("Concert_Curtain", data);
        }

        static void Normalize(float[] data, float peakTo)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak < 0.0001f) return;
            float k = peakTo / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
