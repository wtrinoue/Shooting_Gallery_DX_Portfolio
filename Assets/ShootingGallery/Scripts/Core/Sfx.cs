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
        [Header("忍者モード (手裏剣を投げる音 / 刺さる音 / ふすまが開け閉めする音)")]
        public AudioClip shurikenThrow, thunk, fusuma;
        [Header("怪獣 / カジノ / 西部劇 (怪獣の雄叫び / スロットのレバーと止まる音 / コイン / 弾込め / 汽笛)")]
        public AudioClip roar, slotLever, reelStop, coin, reload, whistle;
        [Header("カジノの明かりが点く演出 (真っ暗になる音 / ドラムロール / シンバル / 歓声)")]
        public AudioClip blackout, drumroll, crash, cheer;
        [Header("音楽モード (コンサートホールの拍手。パチパチ)")]
        public AudioClip applause;
        [Header("宇宙モード (ミサイルの発射音 / 星がきらめく音 / 宇宙へ飛び出す音 / 宇宙から戻る音)")]
        public AudioClip missile, twinkle, warpIn, warpOut;
        [Header("マシンガンチャレンジ (カップを倒した音。空なら自動生成。複数入れると毎回ランダムに選ぶ)")]
        public AudioClip[] canHits = new AudioClip[0];
        [Header("西部劇の砂嵐 (ゴーッと吹き荒れる風の音)")]
        public AudioClip sandstorm;
        [Header("幽霊モード (ひゅ～どろどろ… と闇に包まれる音 / 的を倒した時のぽわんという音)")]
        public AudioClip ghostWail, ghostPop;
        [Header("懐中電灯 (カチッと点く・消える音 / 電池が切れる音)")]
        public AudioClip click, powerDown;
        [Header("発掘モード (ザクッザクッと穴を掘る音)")]
        public AudioClip dig;
        [Header("夏祭りモード (コルク銃のポン / 水のちゃぽん / ポイが破れるビリッ / 祭り太鼓。花火は音なし)")]
        public AudioClip corkPop, splash, rip, taiko;
        [Header("潜水艦モード (魚雷を撃つボシュッという音 / ゴボゴボと水に沈む泡の音 / 潜水艦が閉じるガチャン)")]
        public AudioClip torpedo, bubbles, hatchClose;
        [Tooltip("美術館モード: 筆でキャンバスを塗る音 (サッ、サッ…) / ノミをトンカチでたたいて石を彫る音 (カン、カン、ぱらぱら)")]
        public AudioClip paintBrush, chisel;
        [Tooltip("美術館モード: 白い閃光に包まれる音 (シュワーッと高まって、パァーンと光る)")]
        public AudioClip flash;
        [Tooltip("美術館モード: 粘土がぺちゃっとくっついて、むにゅっと押しつけられる音")]
        public AudioClip clay;
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
            if (hit == null) hit = Tones("Hit", new[] { 1320f }, 0.09f);
            if (bonus == null) bonus = Tones("Bonus", new[] { 1046f, 1568f }, 0.08f);
            if (pickup == null) pickup = Tones("Pickup", new[] { 523f, 659f, 784f, 1046f }, 0.07f);
            if (fever == null) fever = Tones("Fever", new[] { 523f, 659f, 784f, 1046f, 784f, 1046f, 1318f }, 0.1f);
            if (start == null) start = Tones("Start", new[] { 784f, 1046f }, 0.12f);
            if (end == null) end = Tones("End", new[] { 1046f, 784f, 659f, 523f }, 0.15f);
            if (thunk == null) thunk = Noise("Thunk", 0.08f, 60f, 1.2f, 0.15f);                     // トッ (刺さる)
            if (click == null) click = Noise("Click", 0.03f, 90f, 0.9f, 0.5f);                    // カチッ
            if (powerDown == null) powerDown = Tones("PowerDown", new[] { 880f, 660f, 440f, 220f }, 0.07f); // ピュルル… (電池切れ)
            if (reelStop == null) reelStop = Tones("ReelStop", new[] { 880f }, 0.05f);               // ピッ
            if (coin == null) coin = Tones("Coin", new[] { 1975.5f, 2637f }, 0.06f);                 // チャリン
            if (reload == null) reload = Tones("Reload", new[] { 1400f, 0f, 0f, 1700f, 0f, 2100f }, 0.035f); // カチャカチャ
            if (sniperReady == null) sniperReady = Tones("SniperReady", new[] { 1760f, 0f, 1760f, 0f, 2349f }, 0.07f); // ピッ ピッ ピーン (照準)
            if (sniperMiss == null) sniperMiss = Tones("SniperMiss", new[] { 392f, 330f, 262f }, 0.16f); // ざんねん…

            // 長い音 (ファンファーレ・風・ドラムロールなど) は、起動時に一度に作ると数百ミリ秒固まるので、数フレームに分けて作る
            // (タイトル画面の間に作り終わる。作り終わる前に鳴らそうとした音は鳴らないだけ)
            StartCoroutine(GenerateLongClips());
        }

        /// <summary>長い音を、1フレームあたり数ミリ秒ずつ作る</summary>
        System.Collections.IEnumerator GenerateLongClips()
        {
            var steps = new System.Action[]
            {
                () => { if (explosion == null) explosion = Noise("Explosion", 0.8f, 5f, 4f, 0.06f); },
                () => { if (ufoHit == null) ufoHit = Tones("UfoHit", new[] { 1568f, 1318f, 1046f, 880f, 784f, 659f, 523f, 1046f, 1568f, 2093f }, 0.05f); },
                () => { if (door == null) door = Noise("Door", 1.3f, 2.2f, 3.5f, 0.03f); }, // ゴゴゴ…と床がスライドする音
                () => { if (complete == null) complete = Fanfare(); },
                () => { if (stageUp == null) stageUp = PowerUp(); },
                () => { if (deluxe == null) deluxe = DeluxeFanfare(); },
                () => { if (shurikenThrow == null) shurikenThrow = Swish("ShurikenThrow", 0.2f, 0.55f, 0.6f); }, // シュッ
                () => { if (fusuma == null) fusuma = Swish("Fusuma", 0.3f, 0.12f, 0.45f); }, // スーッ (ふすま)
                () => { if (roar == null) roar = Roar(); }, // ガオーッ
                () => { if (slotLever == null) slotLever = Noise("SlotLever", 0.25f, 14f, 1.4f, 0.08f); }, // ガシャン
                () => { if (whistle == null) whistle = Bell("Whistle", new[] { 587.33f, 739.99f, 880f }, 1.3f); }, // ポーッ (汽笛)
                () => { if (sandstorm == null) sandstorm = Wind(); }, // ゴォーッ (砂嵐)
                () => { if (ghostWail == null) ghostWail = GhostWail(); }, // ひゅ～どろどろ…
                () => { if (ghostPop == null) ghostPop = GhostPop(); }, // ぽわん
                () => { if (dig == null) dig = Dig(); }, // ザクッ ザクッ ザクッ
                () => { if (corkPop == null) corkPop = CorkPop(); }, // ポン
                () => { if (splash == null) splash = Splash(); }, // ちゃぽん
                () => { if (rip == null) rip = Noise("Rip", 0.18f, 18f, 1.1f, 0.85f); }, // ビリッ
                () => { if (taiko == null) taiko = Taiko(); }, // ドン ドン ドドン
                () => { if (torpedo == null) torpedo = TorpedoLaunch(); }, // ボシュッ…ぶくぶく
                () => { if (bubbles == null) bubbles = Bubbles("Bubbles", 2.2f, 38f, 11); }, // ゴボゴボゴボ…
                () => { if (hatchClose == null) hatchClose = HatchClose(); }, // ガチャン (金属のふたが閉まる)
                () => { if (paintBrush == null) paintBrush = BrushStrokes(); }, // サッ、サッ、サッ、サッ (筆で塗る)
                () => { if (chisel == null) chisel = Chisel(); }, // カン、カン、ぱらぱら (石を彫る)
                () => { if (flash == null) flash = Flash(); }, // シュワーッ…パァーン (白い閃光)
                () => { if (clay == null) clay = Clay(); }, // ぺちゃっ…むにゅ (粘土)

                () => { if (blackout == null) blackout = Blackout(); }, // バツン
                () => { if (drumroll == null) drumroll = Drumroll(3.6f); }, // ドロロロロ…
                () => { if (crash == null) crash = Noise("Crash", 2f, 2.2f, 1.6f, 0.9f); }, // ジャーン (シンバル)
                () => { if (cheer == null) cheer = Cheer(); }, // ワーッ
                () => { if (applause == null) applause = Applause(4.5f); }, // パチパチパチ… (大勢の拍手)
                () => { if (missile == null) missile = Missile(); }, // シュゴーッ
                () => { if (twinkle == null) twinkle = Twinkle(); }, // キラキラ…
                () => { if (warpIn == null) warpIn = Warp("WarpIn", true); }, // ヒュイーン (上がる)
                () => { if (warpOut == null) warpOut = Warp("WarpOut", false); }, // ヒュウゥン (下がる)
                () => { if (sniperShot == null) sniperShot = Noise("SniperShot", 0.9f, 4.5f, 2.2f, 0.5f); }, // 乾いた大きな発砲音が長く尾を引く
                () => { if (canHits == null || canHits.Length == 0) canHits = new[] { CanHit("CanHit0", 1250f, 1), CanHit("CanHit1", 1080f, 2), CanHit("CanHit2", 1420f, 3) }; }, // カーン、カラン… (高さを変えて3種類。続けて当てても単調にならないように)
                () =>
                {
                    if (scaleNotes != null && scaleNotes.Length >= ScaleHz.Length) return;
                    var notes = new AudioClip[ScaleHz.Length]; // 全部そろってから入れる (途中で鳴らされても大丈夫なように)
                    for (int i = 0; i < ScaleHz.Length; i++) notes[i] = Bell($"Note{i}", new[] { ScaleHz[i] }, 0.6f);
                    scaleNotes = notes;
                },
            };
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            foreach (var step in steps)
            {
                step();
                if (watch.ElapsedMilliseconds < 4) continue; // このフレームはまだ余裕がある
                yield return null;
                watch.Restart();
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
            // トランペット: 撃っても音は鳴らさない (ユーザーの指定)
            if (type == WeaponType.Trumpet) return;
            if (type == WeaponType.Imagination) return; // イマジネーション: 撃っても音は鳴らさない (当たった時に、塗る / 彫る音が鳴る)
            var clip = type == WeaponType.MachineGun || type == WeaponType.AirMachineGun ? instance.machineGunShot
                     : type == WeaponType.Shotgun ? instance.shotgunShot
                     : type == WeaponType.Bomb || type == WeaponType.Bazooka || type == WeaponType.FireBall ? instance.bombShot
                     : type == WeaponType.Missile ? instance.missile
                     : type == WeaponType.Torpedo ? instance.torpedo
                     : type == WeaponType.Sniper ? instance.sniperShot
                     : type == WeaponType.Shuriken || type == WeaponType.Dart || type == WeaponType.Pickaxe ? instance.shurikenThrow
                     : type == WeaponType.CorkGun ? instance.corkPop
                     : type == WeaponType.RingToss || type == WeaponType.Poi || type == WeaponType.Coin ? instance.fusuma // ふわっと投げる音
                     : instance.handgunShot;
            // 二丁拳銃で左右同時に撃っても1発分の音。つるはしは押しっぱなしで次々に投げるので、続けて聞いても気にならない小ささに
            bool soft = type == WeaponType.Pickaxe || type == WeaponType.RingToss || type == WeaponType.Poi || type == WeaponType.Coin;
            instance.PlayLimited("shot", clip, soft ? 0.25f : 0.5f, fade: false);
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

        public static void Play(string name) => Play(name, 1f);

        /// <summary>volumeScale 倍の大きさで鳴らす (1 = いつもの大きさ)</summary>
        public static void Play(string name, float volumeScale)
        {
            if (instance == null) return;
            if (name == "can")
            {
                var cans = instance.canHits;
                if (cans != null && cans.Length > 0) instance.PlayLimited("can", cans[Random.Range(0, cans.Length)], 0.7f * volumeScale);
                return;
            }
            // 的に当たった音 (hit / bonus) は同じ種類として扱い、同時に倒しても1回だけ鳴らす
            string group = name == "bonus" ? "hit" : name;
            bool limited = name == "hit" || name == "bonus" || name == "explosion" || name == "pickup" || name == "ufo" || name == "thunk" || name == "fusuma" || name == "coin" || name == "reelstop" || name == "ghostpop" || name == "splash" || name == "rip" || name == "paint" || name == "chisel" || name == "clay";
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
                "thunk" => instance.thunk,
                "fusuma" => instance.fusuma,
                "roar" => instance.roar,
                "lever" => instance.slotLever,
                "reelstop" => instance.reelStop,
                "coin" => instance.coin,
                "reload" => instance.reload,
                "whistle" => instance.whistle,
                "blackout" => instance.blackout,
                "sandstorm" => instance.sandstorm,
                "drumroll" => instance.drumroll,
                "crash" => instance.crash,
                "cheer" => instance.cheer,
                "applause" => instance.applause,
                "missile" => instance.missile,
                "twinkle" => instance.twinkle,
                "ghost" => instance.ghostWail,
                "ghostpop" => instance.ghostPop,
                "click" => instance.click,
                "powerdown" => instance.powerDown,
                "dig" => instance.dig,
                "splash" => instance.splash,
                "rip" => instance.rip,
                "taiko" => instance.taiko,
                "torpedo" => instance.torpedo,
                "bubbles" => instance.bubbles,
                "hatchclose" => instance.hatchClose,
                "paint" => instance.paintBrush,
                "chisel" => instance.chisel,
                "flash" => instance.flash,
                "clay" => instance.clay,
                "warpin" => instance.warpIn,
                "warpout" => instance.warpOut,
                _ => null
            };
            if (clip == null) return;
            if (limited) instance.PlayLimited(group, clip, 0.6f * volumeScale);
            else instance.source.PlayOneShot(clip, 0.6f * volumeScale);
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

        /// <summary>怪獣の雄叫び: 低くうなるノイズと、上がってから下がる太い音を重ねる</summary>
        static AudioClip Roar()
        {
            const float Length = 1.4f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            float lp = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.06f);
                float f = Mathf.Lerp(110f, 170f, Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.4f))) * (1f + 0.04f * Mathf.Sin(i * 0.004f));
                phase += 2f * Mathf.PI * f / Rate;
                float env = Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1f - t) * 2.5f);
                data[i] = Mathf.Clamp((lp * 2.2f + 0.35f * Mathf.Sign(Mathf.Sin(phase)) * 0.6f + 0.25f * Mathf.Sin(phase * 2f)) * env, -1f, 1f);
            }
            var clip = AudioClip.Create("Roar", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>大きなスイッチを切って真っ暗になる音: 「バツン」(短い金属音) と、低い「ドン」、電気が落ちる「ブゥン…」</summary>
        /// <summary>
        /// 砂嵐の風の音 (3.4 秒): こもったノイズが、ゴォーッと強まったり弱まったり (突風) しながら吹き荒れ、最後に静まる。
        /// 砂がぶつかるザラザラした高い音も少し混ぜる。
        /// </summary>
        static AudioClip Wind()
        {
            const float Length = 3.4f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            var rng = new System.Random(7);
            float low = 0f, mid = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                float gust = 0.6f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.9f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * 2.3f * t + 1f);
                low = Mathf.Lerp(low, white, 0.02f + 0.03f * gust); // 突風の時は少し高い音になる (ヒューッ)
                mid = Mathf.Lerp(mid, white, 0.25f);
                float env = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((Length - t) / 1.0f);
                float grit = (mid - low) * 0.12f * gust;  // ザラザラ
                data[i] = Mathf.Clamp((low * 5.5f * gust + grit) * env, -1f, 1f);
            }
            var clip = AudioClip.Create("Sandstorm", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>幽霊の世界へ入る音: ゆらゆら震える高い音が「ひゅ～」と上がって下がり、低い太鼓が「どろどろ…」と鳴る</summary>
        static AudioClip GhostWail()
        {
            const float Length = 2.6f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            var rng = new System.Random(13);
            float phase = 0f, low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, k = t / Length;
                // ひゅ～: 上がってから長く下がる、ビブラートの強い音 (テルミンのような)
                float f = 520f * Mathf.Pow(2f, 0.6f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.6f)) - 0.9f * Mathf.Max(0f, k - 0.45f)) * (1f + 0.035f * Mathf.Sin(2f * Mathf.PI * 6f * t));
                phase += 2f * Mathf.PI * f / Rate;
                float wail = (Mathf.Sin(phase) * 0.8f + Mathf.Sin(phase * 2f) * 0.12f) * Mathf.Clamp01(t * 4f) * Mathf.Clamp01((1.9f - t) * 1.5f);
                // どろどろ: 低いうなりを速く揺らす (後半)
                float white = (float)rng.NextDouble() * 2f - 1f;
                low = Mathf.Lerp(low, white, 0.01f);
                float roll = low * 9f * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 11f * t)) * Mathf.Clamp01((t - 0.8f) * 2f) * Mathf.Clamp01((Length - t) * 1.5f);
                data[i] = Mathf.Clamp(wail * 0.32f + roll * 0.5f, -1f, 1f);
            }
            var clip = AudioClip.Create("GhostWail", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>穴を掘る音: 土にスコップが刺さる「ザクッ」を3回 (低いドスッと、土がこぼれるザラザラ)</summary>
        static AudioClip Dig()
        {
            const float Length = 1.3f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            var rng = new System.Random(31);
            float low = 0f, mid = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                low = Mathf.Lerp(low, white, 0.04f);
                mid = Mathf.Lerp(mid, white, 0.3f);
                float sum = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float s = t - k * 0.4f;
                    if (s < 0f) continue;
                    sum += low * 6f * Mathf.Exp(-s * 25f) + mid * 0.5f * Mathf.Exp(-s * 9f);
                }
                data[i] = Mathf.Clamp(sum, -1f, 1f);
            }
            var clip = AudioClip.Create("Dig", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>コルク銃: 「ポン」と抜ける短い音 (下がる音程 + 空気の抜けるノイズ)</summary>
        static AudioClip CorkPop()
        {
            const float Length = 0.18f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            var rng = new System.Random(5);
            float phase = 0f, air = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 700f * Mathf.Exp(-t * 18f) + 220f;
                phase += 2f * Mathf.PI * f / Rate;
                air = Mathf.Lerp(air, (float)rng.NextDouble() * 2f - 1f, 0.5f);
                data[i] = Mathf.Clamp(Mathf.Sin(phase) * Mathf.Exp(-t * 30f) * 0.7f + air * Mathf.Exp(-t * 45f) * 0.5f, -1f, 1f);
            }
            var clip = AudioClip.Create("CorkPop", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>水に入る音: 「ちゃぽん」(はねる水のノイズ + 上がる泡の音)</summary>
        static AudioClip Splash()
        {
            const float Length = 0.4f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            var rng = new System.Random(9);
            float phase = 0f, wet = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                wet = Mathf.Lerp(wet, (float)rng.NextDouble() * 2f - 1f, 0.45f);
                float f = 500f + 900f * Mathf.Clamp01((t - 0.03f) * 8f);
                phase += 2f * Mathf.PI * f / Rate;
                float bubble = t > 0.03f ? Mathf.Sin(phase) * Mathf.Exp(-(t - 0.03f) * 14f) * 0.35f : 0f;
                data[i] = Mathf.Clamp(wet * Mathf.Exp(-t * 16f) * 0.8f + bubble, -1f, 1f);
            }
            var clip = AudioClip.Create("Splash", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 泡の音: 「ゴボゴボ…」。音の高さが上がっていく短い「ポコッ」をばらばらにたくさん鳴らし、低い水のざわめきを重ねる。
        /// perSecond = 1秒あたりの泡の数
        /// </summary>
        static AudioClip Bubbles(string name, float length, float perSecond, int seed)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random(seed);
            int count = Mathf.RoundToInt(length * perSecond);
            for (int b = 0; b < count; b++)
            {
                int start = rng.Next(0, Mathf.Max(1, n - Rate / 10));
                float f0 = 280f + (float)rng.NextDouble() * 800f;
                float amp = 0.15f + (float)rng.NextDouble() * 0.25f;
                float phase = 0f;
                int len = Rate / 18;
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    phase += 2f * Mathf.PI * f0 * (1f + t * 25f) / Rate; // ポコッと高さが上がる
                    data[start + i] += Mathf.Sin(phase) * Mathf.Exp(-t * 55f) * amp;
                }
            }
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, 0.03f); // 低い水のざわめき
                float env = Mathf.Clamp01(t * 8f) * Mathf.Clamp01((length - t) * 3f);
                data[i] = Mathf.Clamp((data[i] + lp * 0.9f) * env, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>魚雷の発射音: 「ボシュッ」と低く押し出す音のあとに、ぶくぶく泡の音</summary>
        static AudioClip TorpedoLaunch()
        {
            var bubbles = Bubbles("TorpedoBubbles", 0.8f, 45f, 5);
            const float Length = 0.8f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            bubbles.GetData(data, 0);
            float lp = 0f;
            var rng = new System.Random(17);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, 0.12f);
                float thump = lp * 2.4f * Mathf.Exp(-t * 14f) + Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 18f) * 0.5f;
                data[i] = Mathf.Clamp(data[i] * 0.7f + thump, -1f, 1f);
            }
            var clip = AudioClip.Create("Torpedo", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>潜水艦が閉じる音: 「ガチャン」(金属の短い衝撃音と、少し響く低い金属の鳴り)</summary>
        static AudioClip HatchClose()
        {
            const float Length = 0.9f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            var rng = new System.Random(23);
            float lp = 0f;
            float[] ring = { 140f, 323f, 587f, 911f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, 0.5f);
                float clank = lp * Mathf.Exp(-t * 45f) * 1.1f;
                float tone = 0f;
                for (int k = 0; k < ring.Length; k++) tone += Mathf.Sin(2f * Mathf.PI * ring[k] * t) * Mathf.Exp(-t * (5f + k * 4f)) / (k + 1.5f);
                data[i] = Mathf.Clamp(clank + tone * 0.7f, -1f, 1f);
            }
            var clip = AudioClip.Create("HatchClose", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>祭り太鼓: 「ドン ドン ドドン」(低い太鼓の音を4つ)</summary>
        static AudioClip Taiko()
        {
            float[] hits = { 0f, 0.42f, 0.84f, 1.05f };
            const float Length = 1.8f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            var rng = new System.Random(3);
            foreach (float at in hits)
            {
                int start = (int)(at * Rate);
                float phase = 0f, skin = 0f;
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate;
                    if (t > 0.7f) break;
                    float f = 95f + 60f * Mathf.Exp(-t * 20f);
                    phase += 2f * Mathf.PI * f / Rate;
                    skin = Mathf.Lerp(skin, (float)rng.NextDouble() * 2f - 1f, 0.2f);
                    data[i] += Mathf.Sin(phase) * Mathf.Exp(-t * 6f) * 0.8f + skin * Mathf.Exp(-t * 40f) * 0.4f;
                }
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create("Taiko", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>お化けを倒した音: 「ぽわん」と下がる柔らかい音と、煙が抜けるような小さなノイズ</summary>
        static AudioClip GhostPop()
        {
            const float Length = 0.45f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            var rng = new System.Random(21);
            float phase = 0f, soft = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 900f * Mathf.Exp(-t * 4.5f) + 180f;
                phase += 2f * Mathf.PI * f / Rate;
                float tone = Mathf.Sin(phase) * Mathf.Exp(-t * 7f) * Mathf.Clamp01(t * 200f);
                soft = Mathf.Lerp(soft, (float)rng.NextDouble() * 2f - 1f, 0.15f);
                data[i] = Mathf.Clamp(tone * 0.5f + soft * Mathf.Exp(-t * 12f) * 0.5f, -1f, 1f);
            }
            var clip = AudioClip.Create("GhostPop", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Blackout()
        {
            const float Length = 0.9f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.5f);
                float click = lp * Mathf.Exp(-t * 60f) * 1.4f;
                float thump = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 7f) * 0.7f;
                float f = Mathf.Lerp(120f, 40f, t / Length);  // 電気が落ちていく低いうなり
                float hum = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.12f * Mathf.Exp(-t * 3f) * Mathf.Clamp01(t * 30f);
                data[i] = Mathf.Clamp(click + thump + hum, -1f, 1f);
            }
            var clip = AudioClip.Create("Blackout", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>ドラムロール: スネアを細かく速く叩き続け、だんだん大きくなる (length 秒)</summary>
        static AudioClip Drumroll(float length)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            float lp = 0f;
            const float HitsPerSec = 26f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float sinceHit = t % (1f / HitsPerSec);
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.6f);
                float body = Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.3f;
                float env = Mathf.Exp(-sinceHit * 45f) * (0.75f + 0.25f * Random.value);
                float swell = Mathf.Lerp(0.18f, 0.75f, Mathf.Pow(t / length, 1.6f));
                data[i] = Mathf.Clamp((lp + body) * env * swell, -1f, 1f);
            }
            var clip = AudioClip.Create("Drumroll", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>大勢の歓声: 高さの違う「ワーッ」という声 (ゆらぐ帯域のノイズ) を重ね、口笛と拍手を少し混ぜる</summary>
        static AudioClip Cheer()
        {
            const float Length = 2.8f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            const int Voices = 10;
            var lp1 = new float[Voices];
            var lp2 = new float[Voices];
            var rate = new float[Voices];
            var phase = new float[Voices];
            for (int v = 0; v < Voices; v++) { rate[v] = Random.Range(2f, 6f); phase[v] = Random.value * 10f; }
            float clapLp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((Length - t) / 1.4f);
                float s = 0f;
                for (int v = 0; v < Voices; v++)
                {
                    // 2段のローパスの差 = 声のような帯域のノイズ。声ごとに大きさがゆらぐ
                    float x = Random.Range(-1f, 1f);
                    float cut = 0.05f + 0.03f * (v % 4);
                    lp1[v] = Mathf.Lerp(lp1[v], x, cut * 2f);
                    lp2[v] = Mathf.Lerp(lp2[v], lp1[v], cut);
                    float wobble = 0.6f + 0.4f * Mathf.Sin(t * rate[v] + phase[v]);
                    s += (lp1[v] - lp2[v]) * wobble;
                }
                s *= 1.1f;
                // 口笛 (ピューッと上がる)
                if (t > 0.3f && t < 1.1f)
                {
                    float wt = (t - 0.3f) / 0.8f;
                    s += Mathf.Sin(2f * Mathf.PI * (1800f + 900f * wt) * t) * 0.08f * Mathf.Sin(Mathf.PI * wt);
                }
                // 拍手 (細かいパチパチ)
                clapLp = Mathf.Lerp(clapLp, Random.Range(-1f, 1f), 0.7f);
                float clap = Random.value < 0.004f ? 1f : 0f;
                s += clapLp * clap * 0.6f;
                data[i] = Mathf.Clamp(s * env, -1f, 1f);
            }
            // 拍手の1つ1つを少し長くする (短すぎるとプチプチになるので、前の値を引きずる)
            for (int i = 1; i < n; i++) data[i] = Mathf.Clamp(data[i] + data[i - 1] * 0.25f, -1f, 1f);
            var clip = AudioClip.Create("Cheer", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 大勢の拍手 (パチパチ): 1人ずつ手をたたく間隔・手の音の高さ・大きさの違う人を大勢重ねる。
        /// 1回の「パチ」は、ごく短い雑音を帯域で絞って (手のひらの音の高さ) すぐに消す。始まりはばらばらに広がり、終わりは1人ずつやめていく。
        /// 最後に部屋の響きを少し足す
        /// </summary>
        static AudioClip Applause(float length)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            const int Clappers = 30;
            int clapLen = (int)(0.04f * Rate);
            for (int c = 0; c < Clappers; c++)
            {
                float period = Random.Range(0.17f, 0.3f);           // その人が手をたたく間隔 (秒)
                float fc = Random.Range(900f, 2600f);               // 手の音の高さ (手の形・大きさで違う)
                float f = 2f * Mathf.Sin(Mathf.PI * fc / Rate);
                float loud = Random.Range(0.45f, 1f);
                float start = Random.Range(0f, 0.4f);               // たたき始めがばらばらに広がる
                float stop = length - Random.Range(0.3f, 2f);       // 1人ずつやめていく
                for (float t = start; t < stop; t += period * Random.Range(0.85f, 1.15f))
                {
                    int i0 = (int)(t * Rate);
                    float tau = Random.Range(0.004f, 0.009f) * Rate;  // パチの短さ
                    float amp = loud * Random.Range(0.6f, 1f);
                    float low = 0f, band = 0f;
                    for (int k = 0; k < clapLen && i0 + k < n; k++)
                    {
                        float env = Mathf.Exp(-k / tau) * Mathf.Clamp01(k / (0.0006f * Rate)); // 一瞬で立ち上がって、すぐ消える
                        float x = Random.Range(-1f, 1f) * env;
                        // 帯域で絞る (状態変数フィルター)。帯域の音を中心に、高い所を少し残して乾いた音に
                        float high = x - low - 0.7f * band;
                        band += f * high;
                        low += f * band;
                        data[i0 + k] += (band * 0.9f + high * 0.25f) * amp;
                    }
                }
            }
            // 部屋の響き (少し遅れた音を薄く重ねる)
            int d1 = (int)(0.023f * Rate), d2 = (int)(0.041f * Rate);
            for (int i = n - 1; i >= d2; i--) data[i] += data[i - d1] * 0.18f + data[i - d2] * 0.12f;
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.8f / peak;
            for (int i = 0; i < n; i++) data[i] *= gain;
            var clip = AudioClip.Create("Applause", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>ミサイルの発射音: 「シュッ」と点火してから、「ゴーッ」と噴射の音が遠ざかる</summary>
        static AudioClip Missile()
        {
            const float Length = 0.7f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float x = Random.Range(-1f, 1f);
                lp = Mathf.Lerp(lp, x, 0.45f);
                lp2 = Mathf.Lerp(lp2, x, 0.08f);
                float ignite = lp * Mathf.Exp(-t * 30f) * 0.9f;
                float roar = lp2 * 2.2f * Mathf.Clamp01(t * 20f) * Mathf.Exp(-t * 3.2f);
                data[i] = Mathf.Clamp(ignite + roar, -1f, 1f);
            }
            var clip = AudioClip.Create("Missile", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>星がきらめく音: 高い鈴の音がばらばらに鳴って消えていく</summary>
        static AudioClip Twinkle()
        {
            const float Length = 2.4f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            float[] notes = { 1568f, 1760f, 2093f, 2349f, 2637f, 3136f };
            var rng = new System.Random(7);
            for (int k = 0; k < 22; k++)
            {
                float start = (float)rng.NextDouble() * (Length - 0.8f);
                float f = notes[rng.Next(notes.Length)];
                float gain = 0.12f * (1f - start / Length * 0.6f);
                int s0 = (int)(start * Rate);
                for (int i = 0; s0 + i < n && i < Rate; i++)
                {
                    float t = i / (float)Rate;
                    data[s0 + i] += gain * Mathf.Exp(-t * 6f) * Mathf.Clamp01(i / 60f) * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t));
                }
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create("Twinkle", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>宇宙へ飛び出す音 (up = 上がっていく) / 戻ってくる音 (下がっていく): ゆっくり音程が動く、うねる和音</summary>
        static AudioClip Warp(string name, bool up)
        {
            const float Length = 2.2f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            float p1 = 0f, p2 = 0f, p3 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, k = t / Length;
                float e = up ? k : 1f - k;
                float f = 110f * Mathf.Pow(4f, e);
                p1 += 2f * Mathf.PI * f / Rate;
                p2 += 2f * Mathf.PI * f * 1.5f * (1f + 0.004f * Mathf.Sin(t * 5f)) / Rate;
                p3 += 2f * Mathf.PI * f * 2.01f / Rate;
                float env = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((Length - t) * 2f);
                data[i] = Mathf.Clamp((Mathf.Sin(p1) * 0.3f + Mathf.Sin(p2) * 0.2f + Mathf.Sin(p3) * 0.12f) * env * (0.8f + 0.2f * Mathf.Sin(t * 14f)), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 筆でキャンバスを塗る音: 毛先が布目をこする、ざらっとした「サッ」を4回 (左右に行き来して塗る筆の動きと同じ 0.9 秒)。
        /// 低い音と高い音を取り除いた、こすれるノイズに、布目に引っかかる細かい粒を混ぜる
        /// </summary>
        static AudioClip BrushStrokes()
        {
            const float Length = 0.9f;
            const int Strokes = 4;
            int n = (int)(Rate * Length);
            var data = new float[n];
            float lp = 0f, slow = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float u = t / Length * Strokes;
                float k = u - Mathf.Floor(u);
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.6f));
                env *= env * (1f - 0.35f * (t / Length)) * (Mathf.FloorToInt(u) % 2 == 0 ? 1f : 0.85f);
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.55f);
                slow = Mathf.Lerp(slow, lp, 0.06f);
                float scratch = lp - slow; // こすれる「シャー」の帯だけ残す
                float grain = Random.value < 0.015f ? Random.Range(-1f, 1f) : 0f; // 布目に引っかかる粒
                data[i] = Mathf.Clamp((scratch * 1.1f + grain * 0.35f) * env * 0.7f, -1f, 1f);
            }
            var clip = AudioClip.Create("PaintBrush", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 白い閃光の音: 明るいノイズが「シュワーッ」と高まり (光に包まれるまで)、光った瞬間に高い和音が「パァーン」と広がって、
        /// きらきらした余韻を残して消える
        /// </summary>
        static AudioClip Flash()
        {
            const float Length = 1.8f, Peak = 0.38f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            float lp = 0f, slow = 0f;
            float[] chord = { 1046.5f, 1318.5f, 1568f, 2093f, 2637f }; // ド・ミ・ソの明るい和音 (高め)
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // シュワーッ: 高い音だけ残したノイズが、光るまでに大きくなり、光ったあとすぐ引く
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.7f);
                slow = Mathf.Lerp(slow, lp, 0.1f);
                float swell = t < Peak ? Mathf.Pow(t / Peak, 2.2f) : Mathf.Exp(-(t - Peak) * 9f);
                float v = (lp - slow) * swell * 0.45f;
                // パァーン: 光った瞬間から広がる和音 (少し揺らして、きらきらさせる)
                if (t >= Peak)
                {
                    float d = t - Peak;
                    float env = Mathf.Clamp01(d * 60f) * Mathf.Exp(-d * 2.6f);
                    float sum = 0f;
                    for (int k = 0; k < chord.Length; k++)
                        sum += Mathf.Sin(2f * Mathf.PI * chord[k] * d * (1f + 0.003f * Mathf.Sin(d * (9f + k * 3f)))) * (1f - k * 0.12f);
                    v += sum / chord.Length * env * 0.6f;
                    // きらきら: 細かい高い粒
                    if (Random.value < 0.002f * Mathf.Exp(-d * 2f)) v += Random.Range(-0.4f, 0.4f);
                }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create("Flash", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 粘土の音: 飛んできた粘土が「ぺちゃっ」とくっつき (低くこもった短い音と、湿った音)、そのあと「むにゅ」と押しつけられる
        /// </summary>
        static AudioClip Clay()
        {
            const float Length = 0.6f, Hit = 0.16f, Press = 0.3f;
            int n = (int)(Rate * Length);
            var data = new float[n];
            float lp = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.18f); // こもった、湿った音
                float v = 0f;
                if (t >= Hit)
                {
                    float d = t - Hit;
                    float f = Mathf.Lerp(190f, 70f, Mathf.Clamp01(d / 0.08f)); // ぺちゃっ: 高めから低く落ちる
                    phase += 2f * Mathf.PI * f / Rate;
                    v += Mathf.Sin(phase) * Mathf.Exp(-d * 30f) * 0.7f;
                    v += lp * Mathf.Exp(-d * 45f) * 1.1f;
                }
                if (t >= Press) // むにゅ: ゆっくりふくらんでしぼむ、くぐもった音
                {
                    float d = t - Press;
                    float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(d / 0.25f));
                    v += lp * env * 0.35f * (1f + 0.5f * Mathf.Sin(d * 90f));
                }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create("Clay", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 石を彫る音: トンカチでノミをたたく金属の「カン」を2回 (トンカチを振り下ろす時と同じ間) と、それぞれのあとに石のかけらが落ちる「ぱらぱら」
        /// </summary>
        static AudioClip Chisel()
        {
            const float Length = 0.75f;
            float[] strikes = { 0.13f, 0.41f };
            int n = (int)(Rate * Length);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float v = 0f;
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.3f);
                for (int s = 0; s < strikes.Length; s++)
                {
                    float d = t - strikes[s];
                    if (d < 0f) continue;
                    float ring = Mathf.Exp(-d * 34f);
                    float w = 2f * Mathf.PI * d;
                    v += (Mathf.Sin(w * 2380f) * 0.45f + Mathf.Sin(w * 3810f) * 0.3f + Mathf.Sin(w * 5230f) * 0.18f) * ring * 0.55f; // カン (金属)
                    v += lp * Mathf.Exp(-d * 70f) * 0.6f;                                                                       // コッ (石に当たる)
                    if (d > 0.03f && Random.value < 0.004f * Mathf.Exp(-(d - 0.03f) * 9f) * 8f) v += Random.Range(-0.5f, 0.5f);   // ぱらぱら (かけら)
                }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create("Chisel", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 風を切る音:大きくなってから小さくなるノイズ (lowpass が大きいほど高い「シュッ」、小さいほど柔らかい「スーッ」)
        static AudioClip Swish(string name, float length, float lowpass, float volume)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), lowpass);
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.6f)); // 早めに大きくなり、ゆっくり消える
                data[i] = Mathf.Clamp(lp * env * volume, -1f, 1f);
            }
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
