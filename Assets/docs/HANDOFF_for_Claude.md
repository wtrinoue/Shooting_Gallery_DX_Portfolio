# 射的屋さんデラックス — 引き継ぎメモ (今の仕様)

最終更新: 2026-10-24 (コードを読んで、今の仕様だけに整理し直した)

- この文書は **今の仕様** だけを書く。古くなった内容は残さない。
- **経緯** (なぜそうなったか・前に試したこと・日付ごとの変更) は [HANDOFF_history.md](HANDOFF_history.md) にある。
- 細かい数値はコードが正しい。ここに書いた数値は 2026-10-24 にコードで確かめたもの。各スクリプトには日本語のコメントが丁寧に入っているので、作業前に該当のスクリプトを読むこと。
- **更新のしかた**: 何かを変えたら、(1) この文書の該当の節を今の仕様に書き直し、(2) HANDOFF_history.md の末尾に日付つきで経緯を追記する。

---

## 0. まず守ること (ユーザーの好み・決まりごと)

- **返答は必ず日本語。** ユーザーは Unity / VR に慣れていない。専門用語は短く補足し、手順は「どこを押すか」まで書く。
- **再生した時にビルダーが毎回ゲーム一式を組み立てる運用** (GalleryBuilder.BuildRoutine)。シーンには貼らない。スクリプトを保存して再生すれば反映されるので、「作り直す → 貼り直し」の案内は不要。
  - 再生中に組み立てるので、`AddComponent` した瞬間に Awake が動き、その時はまだビルダーがフィールドを入れていない。**新しい部品の Awake で、ビルダーが入れるフィールドを使わない** (初めて使う時に準備する。例: FinaleCrackers.EnsureInit、WesternRider.Signs)。
  - 組み立てはシーンに保存されないので、コードの初期値がそのまま使われる。昔は Inspector に保存された値に負けないようフィールド名を変えていた (`weaponSeconds` など) が、今の運用では不要。
- **1ファイル1 MonoBehaviour。** 新しいクラスを作る時は新しいファイルにする。
- 操作は **右手のトリガーだけ**。プレイヤーは真ん中に立ったまま見回すだけ。
- 音はほとんど **コードで波形を作る** (Core/Sfx.cs)。BGM と一部の効果音だけ mp3 (下の「6. 素材」)。
- 使い捨ての見た目 (点数の文字・爆発・弾・紙吹雪など) は **Common/FxPool.cs で使い回す** (FxPool.Get / Release)。
- ユーザーは自分でコミットする。頼まれない限り commit / push はしない。ユーザーが Claude の変更を戻すこともある (2026-10-24 に音楽ステージを元の形に戻した)。作業前に git の状態を見ること。

---

## 1. 環境

| 項目 | 内容 |
|---|---|
| Unity | 6000.3.23f1、URP (PC_RPAsset / Mobile_RPAsset。どちらも Forward+、ライトレイヤー (m_SupportsLightLayers) 有効) |
| 入力 | Input System。InputAction はコードで作る (`InputSystem_Actions.inputactions` は使っていない) |
| XR | OpenXR (Oculus Touch / Meta Quest Touch Plus)。WebXR Export (unityroom 用。WebXR/XRBridge.cs が OpenXR と WebXR の違いを吸収) |
| Quest 単体 | メニュー「ShootingGallery → Quest (Android) の VR 設定をする」(Builder/Editor/QuestSetup.cs) で一括設定。手順書は `Assets/docs/Quest_Play_Guide.pdf`、画質を良くする方法と「exceeds previous array size」の警告は `Assets/docs/Quest_Quality_Guide.md` |
| OS | Windows 11。Python は使えない |
| 企画書 | `Assets/docs/develop.txt`、Unknown ステージの仕様は `new_docs/develop.txt`、ユーザーのプロンプト履歴 (10/12〜10/15) は `new_docs/prompts.md` |

- **コンパイルの確認**: Unity の外で `dotnet build Assembly-CSharp.csproj` が通る (出力先は scratchpad に)。新しく作った .cs は Unity が csproj を作り直すまで入らないので、csproj のコピーに `<Compile Include=...>` を足して確かめる。VS Code の「型が見つからない」も同じ理由の一時的なもの。
- **Unity 上での動作確認は Claude にはできない。** 最近の変更の多くは、コンパイルが通ることしか確かめていない。

### シーン (要確認)
- git にある `Assets/Scenes/SampleScene.unity` (ビルドに入る唯一のシーン) には、カメラ・Directional Light (強さ 2)・Global Volume しかなく、**GalleryBuilder が入っていない** (2026-10-24 に確認)。ユーザーの手元で開いているシーンには、保存していない GalleryBuilder があると思われる。ビルド・他の人の環境で動かすには、GalleryBuilder を付けた空のオブジェクトをシーンに置いて保存する必要がある。ユーザーに確認すること。
- `Assets/_Recovery/` に Unity の復旧用シーンが4つコミットされている (不要なら消してよいか、ユーザーに確認)。

---

## 2. ファイル構成 (`Assets/ShootingGallery/Scripts/`)

| フォルダ | 中身 |
|---|---|
| `Builder/` | **GalleryBuilder** (partial。全オブジェクトをプリミティブとコードで作ったメッシュで組み立てる)。Controls (タイトルの看板・クレジット)・Stalls・Targets・Digits (図形の数字)・Player・WeaponDropper・WorldGate・Finale・StageBgm・Reset・Utility。`GalleryRuntimeAssets.cs` (実機で使う BGM・材質などの参照)、`Painter.cs` (美術館の絵) |
| `Builder/Modes/` | モードごとの組み立て (GalleryBuilder.〇〇.cs) |
| `Builder/Editor/` | Inspector のボタン、GalleryRuntimeAssetsSync (実機用の参照を自動でそろえる)、ConcertBgmImport (mp3 の読み込み設定)、QuestSetup |
| `Core/` | **GameManager** (partial。進行役。モードごとに GameManager.〇〇.cs)、Sfx、GoalSign (始まりの看板)、FinaleCrackers、CreditsViewer、SceneEnvironment、HudFollow、PointStyle |
| `Modes/〇〇/` | モードの中身 (舞台・的・演出) |
| `Player/` | PlayerRig (XR リグ・マウス操作・武器の保持時間・二丁拳銃)、GunController (銃・ヒットスキャン・弾・ゲージ)、ResetIndicator |
| `Stalls/` | Stall (屋台のせり上がり・沈む・滑る・入れ替え)、PopUpGimmick |
| `Targets/` | Target (的。Update 駆動の状態機械。TargetKind)、WeaponDropper (普通の的を特別な的に化けさせる) |
| `Weapons/` | Weapons.cs (WeaponType と性能表) と、飛ぶ弾 (Bomb・Missile・Torpedo・Shuriken・NoteShot など) |
| `Effects/`、`Common/` | 爆発・泡・画面の暗転 (ScreenFader)・FxPool・MeshMerger・FaceCenter・TextOutline など |
| `WebXR/` | XRBridge (asmdef ShootingGallery.WebXR) |

---

## 3. ゲームの流れ

### タイトル
- 屋台の前に3つの的: **2 MIN** (TargetKind.TimeAttack) / **100,000 POINTS** (TargetKind.Start) / **WORLD GATE**。メッセージは「CHOOSE A MODE !」。
- 最初の屋台の左右に看板: 左 = 操作方法、右 = クレジット (CREDITS の的で Resources/THIRD_PARTY_NOTICES.txt をページ送りで表示)。
- **操作方法の看板は、VR 用とPC 用の2通りの説明を、看板のボタンで切り替える** (2026-10-24):
  - `VRControls`: 左右のコントローラーの絵。右手は上から TRIG / B 長押し エンドレス / A 長押し リセット、左手は TRIG / X 長押し デバッグ。
  - `PCControls`: 左 = キーボード (上から 1 長押し デバッグ / 2 長押し エンドレス / 3 長押し リセットを上に詰め、下に WASD の並びの絵 (上の段 W、下の段 A S D) と「WASD : 見回す」)、右 = マウスの絵 (左クリック / Space 撃つ (二丁拳銃は左右いっしょに)・右ドラッグ 見回す)。GalleryBuilder.Controls.cs (MouseFigure / WasdFigure)。
  - 看板のいちばん下に **「VR ⇔ PC 表示切替」のボタン** (CREDITS の右隣。名前 ControlsToggle、種類は CREDITS と同じ TargetKind.Credits なので、PC は銃で、VR はコントローラーの白い玉 PointerBall で押せる)。撃つと GameManager.ToggleControlsDisplay で入れ替わる。
  - **最初にどちらを見せるか** は今の操作に合わせる: `PlayerRig.UsingVRControls` (`InputSystem.onAnyButtonPress` で、ボタンが押された時だけ、今と違う種類 (XRController ⇔ マウス・キーボード) の入力の時だけ切り替える。同じ種類なら早期リターン。ヘッドセットの位置・向きは数えない。VR が動いていない時はいつもマウス・キーボード、VR に入った時はコントローラーから)。**一度でもボタンで切り替えたら (controlsManual)、それ以降は入力では変えない。**
  - 看板を見ている間に手元をコントローラーの模型にする (引き金で白い玉) のは **VR の時だけ** (UsingVRControls)。PC では銃のまま。
  - CreditsViewer: クレジットを開いている間に看板の文字を隠す時、看板のボタン (Target の子) の文字は隠さない。

### ラウンド (GameManager.cs)
- 2 MIN / 100,000 POINTS の的を撃つと、プロペラの付いた看板 (GoalSign) が降りてきて 3 → 2 → 1 → GO!! で始まる。
- **2 MIN**: `TimeAttackTime` 120秒。時間切れで TIME UP!、ハイスコアを PlayerPrefs (`ShootingGalleryDX_HighScore`)。
  - **フィナーレ** (GameManager.Finale.cs / FinaleCrackers.cs): 残り `finalePrepareSeconds` (10秒) で、**全部の屋台** (メイン・フィーバー9軒・豪華屋台) のカウンターの左右にクラッカーが出る (見えるのは出ている屋台の分だけ)。時間切れの瞬間 (StopEverything より前) に、出ている屋台のクラッカーから紙吹雪と紙テープ (破裂音なし)、拍手 (`clap_in_hall.mp3`) が流れてだんだん小さくなる。
- **100,000 POINTS**: 時間切れなし。`GoalScore` 100000 点でクリア。かかった時間を競う (`ShootingGalleryDX_BestTime`)。
- 終わったら4秒でタイトルへ。
- **長押しのボタン** (PC はキー。テンキーも可): A / 3 = リセット (タイトルへ)、B / 2 = エンドレス (時間切れなし)、X / 1 = デバッグ (全部の的を試せる。輪に並ぶ屋台ごとに的の種類が決まっている)。**長押しは1つずつ**: 最初に押し始めたボタンを離すまで、ほかの長押しのボタンはゲージがたまらない (PlayerRig.holdOwner。同じフレームなら リセット → エンドレス → デバッグ の順)。
- PC の操作: 右ドラッグか WASD で視点、左クリックか Space で撃つ。**二丁拳銃 (フィーバー中など) は左クリック / Space で左右いっしょに撃つ** (前の F キーはなくした)。左の銃はカーソルの少し左を狙う (`desktopLeftAimOffset` 0.035 = 約 2°)。VR は今までどおり左手のトリガーで左の銃。視点の高さの基準は 170cm。

### いつものプレイ
- **点数の的**: 10 / 50 / 100 / 200 / 300 / 500。メイン屋台は低め、フィーバー屋台は高め。点数で色と大きさが変わる (PointStyle)。点数の数字は図形の数字 (GalleryBuilder.Digits)。値は GalleryBuilder の屋台ごとの Build〜 に直接書いてある。
- **フィーバー** (`feverDuration` 20秒): メイン屋台の最上段の赤い玉で始まる。まわりに屋台がせり上がり 360 度、左手の銃も右トリガーで撃てる (二丁拳銃)。屋台は9種類のギミック (Conveyor・FerrisWheel・PopUp・Pendulum・Carousel・Elevator・DuckParade・Roulette・Turnstile) からランダム。
- **ステージ**: `stageInterval` 30秒ごとに輪の軒数が `stageStallCounts` {6, 7, 8} に増える (フィーバー中なら、その場で輪が広がる)。フィーバー外はメイン屋台だけ。
- **豪華タイム** (`deluxeDuration` 20秒): DELUXE の的で、出ている屋台がその場で豪華版に入れ替わる。
- **特別な的** (WeaponDropper が、起き上がる瞬間の普通の的を化けさせる): 武器 (MG / SG / BOMB。`weaponSeconds` 3秒)、爆弾の的 (連鎖爆発)、青い玉 (フィーバー中にボーナスステージ)、スナイパー / マシンガンのチャレンジ、DELUXE、**別の世界のモードの的** (5秒ごとの区切りで3か所同時に出る。同時に出ていてよいのは6個まで)。
- **ボーナスステージ** (青い玉): 足元のハッチ (半径 3.3m) が開き、地下の的に 1000 / 2000 / 3000 点。8回当てると閉まる。
- **スナイパーチャレンジ** / **マシンガンチャレンジ**: 的を撃つとその屋台 (マシンガンは両隣も) が引っ込み、スナイパーは遠くの大きな的を1発、マシンガンは横長の屋台の 78 個のカップ (うち3個が爆弾)。どちらもラウンドに1回は出る (`guaranteeByTimeLeft` 55秒。100,000 POINTS は目標の6割を超えたら)。
- **UFO**: いつものプレイ中だけ頭上を横切る。**10000点**。
- **エイム補正**: 細い線で外れたら半径 0.12m の太い線で拾う (ショットガンは半分、スナイパーはなし)。壁の向こうは拾わない。

### WORLD GATE (GameManager.WorldGate.cs)
- タイトルの WORLD GATE の的で、1段だけの屋台6軒が囲み、別の世界へ飛ぶ的 (GateWorlds の順) と BACK の的が並ぶ。撃つとその世界へ行き、終わると戻る。時間切れなし、A 長押しでタイトルへ。デバッグモードの仕組みを使っている。

---

## 4. 別の世界のモード (14種類)

的を撃つと、屋台がしまわれて別の世界に切り替わり、モード専用の武器に変わる。**モードの残り時間 = 専用の武器のゲージ** で、ゲージが空になった瞬間に元の世界へ戻り始める。

| モード | 始まる的 | 中身のコード | 武器 | 時間 (秒) | BGM |
|---|---|---|---|---|---|
| 海戦 | Naval | Modes/Naval (NavalBattle) | Bazooka (大砲) | 16.5 | pirate_stage |
| 空中 | Sky | Modes/Sky (SkyBattle) | AirMachineGun | 18 | sky_stage (モードが自分で鳴らす) |
| 忍者 | Ninja | Modes/Ninja (NinjaDojo) | Shuriken | 20 | ninja_stage |
| 怪獣 | Kaiju | Modes/Kaiju (KaijuCity) | FireBall | 24 | kaiju_stage |
| カジノ | Casino | Modes/Casino (CasinoHall) | Dart (狙った先のゲームで持ち替え) | CasinoHall を参照 | casino_stage (モードが自分で鳴らす) |
| 西部劇 | Western | Modes/Western (WesternTrain) | Revolver | 19 | western_stage |
| 宇宙 | Space | Modes/Space (SpaceStage) | Missile | 26 | space_stage |
| 幽霊 | Ghost | Modes/Ghost (GhostManor) | Flashlight | 26 | ghost_stage |
| 鉱山 | Mine | Modes/Mine (MineCave) | Pickaxe | 42 | mine_stage |
| 夏祭り | Festival | Modes/Festival (FestivalStage) | 見ている屋台で変わる (CorkGun / RingToss / Poi / Coin) | 40 | festival_stage |
| 潜水艦 | Submarine | Modes/Submarine (SubmarineDive) | Torpedo | 45 | ocean_stage |
| 音楽 | Music | Modes/Music (ConcertHall) | Trumpet | 52 | music_stage (モードが自分で鳴らす) |
| 美術館 | Museum | Modes/Museum (ArtMuseum) | Imagination | 45 | museum_stage (モードが自分で鳴らす) |
| Unknown | Unknown | Modes/Unknown (UnknownStage) | Lightning (WARP BOLT) | 45 | unknown_stage |

各モードの演出・的・点数は、中身のコードの冒頭のコメントと GameManager.〇〇.cs を読むこと。最近の変更で、ここに書いておくべき点:

- **西部劇**: 行きも帰りも砂嵐 (風の音 "sandstorm" あり)。砂嵐が晴れる時の汽笛はなし (2026-10-24)。リボルバーは6発撃ちきると、トリガーを引くたびに1発ずつ込める。カウボーイの棒の点数の札は、当てるとポロっと外れて地面へ落ち、流れる景色といっしょに後ろへ流れてから沈む (Modes/Western/FallingSign.cs)。
- **Unknown**: ワープホールをくぐって星空になった時と、帰りに色が戻る時の "twinkle" はなし (2026-10-24)。
- **宇宙**: 星が出る時の "twinkle" はある (ユーザーは「このままでよい」)。
- **夏祭り**: スコアとは別にお金 (`GameManager.FestivalMoney`)。料理を買うとお金が減り、値段の分だけスコアが増える。
- **音楽** (ConcertHall):
  - ステージ: 指揮台のまわりに ±65° の扇形のひな壇 (RiserR 3.9 / 5.4 / 6.9 / 8.6、1段 0.75m)、楽器 46個。ひな壇の外 (床の上) のコントラバスは 74 / 86 / 80°、チェロの1つは 73° (段の側面に当たらないように)。※ 2026-10-24 に広げたり形を変えたりしたが、ユーザーが **この元の形に戻した**。
  - 照明: ステージの明かり (stageLights・ソロのスポットライト) は **ライトレイヤー** で、ステージより奥の部品だけを照らす (ConcertHall.SplitStageLighting。客席は照らさない)。演奏中の環境光は `GameManager.concertAmbientPlay`。
  - 演奏が始まり、ステージが明るくなりきってから (`lightRiseSeconds` 4秒) ソロの楽器が出る (OpenSolos)。
  - BGM は魔王魂の曲。拍は `Assets/Source/music_stage/music_stage.csv` (ユーザーが手で打った拍の表) で指揮棒・楽器の揺れを合わせる。

---

## 5. 全モード共通の仕組み

- **モードの始まり方** (GameManager.ModeQueue.cs): モードの的を撃ったらすぐ始める。途中のチャレンジ・モード・ボーナスは全部終わらせてから (`EndAllForNewMode`)。0.3秒以内に2つ撃った時は最初の1つだけ。
- **屋台を戻しきるまで待つ**: モードの帰りに屋台が戻りきるまで、屋台の拡張・フィーバー / 豪華タイムの残り時間を止める (`StallsBusy` / `HoldStallsFor`)。
- **チャレンジは屋台の「場所」を引っ込める** (豪華タイムで同じ場所の版が入れ替わっても出さない)。
- **視点の見張り** (GameManager.View.cs): プレイヤーを上下に動かすモード (鉱山・潜水艦・音楽) や世界を縮めるモード (怪獣) の後、視点が戻りきっていなければ元へ戻す。
- **太陽の光** (GameManager.FadeSun): 屋根の下のモードで平行光源を暗くする。ゲーム一式の中の光源 (`sunLight`) に加え、**シーンにもともとある平行光源 (`otherSuns`) も同じ割合で** 変える (2026-10-24。前は SampleScene の Directional Light が点いたままで、音楽ホールの客席の右側だけが明るかった)。
- **BGM** (GameManager.Bgm.cs): ラウンドの BGM (2 MIN / 100,000 POINTS / エンドレス。WORLD GATE とデバッグは無音) と、モードの BGM。モードの間はラウンドの BGM を止め、戻ったら続きから。2 MIN では残りが `TimeAttackBgmSync` (102秒) 以下なら、時間切れの瞬間に曲のその位置が来るように合わせる。
- **軽くする工夫**: 小さい部品は影を落とさない、FxPool で使い回す、HUD は変わった時だけ書き換える、的はすることがない時は眠る (Target.enabled = false)、動かない部品は MeshMerger で材質ごとに1つにまとめる (音楽の楽器・美術館・Unknown・カジノなど)、長い効果音は少しずつ作る。

---

## 6. 素材とクレジット

- **`Assets/Source/` (mp3 など) は .gitignore で git の管理外**。README の Google Drive のリンクで配っている。clone しただけでは BGM が無い。
- 曲の結びつけ: エディターでは `*BgmPath` 定数 (Builder/GalleryBuilder.StageBgm.cs など) から AssetDatabase で、実機では `Assets/Resources/GalleryRuntimeAssets.asset` から (GalleryRuntimeAssetsSync が自動でそろえる)。
- クレジット: リポジトリのいちばん上の `credit.txt` (使っている曲・音の一覧と各サイトの規約のまとめ) と、ゲーム内の `Resources/THIRD_PARTY_NOTICES.txt` (魔王魂 / 音楽の卵 / OpenTracks / Pixabay / 小森平 / Google Gemini の AI 生成曲、WebXR Export・Apache 2.0、M PLUS 1p・OFL)。**曲や音を足したら両方に足す。** クレジット必須は魔王魂だけ。
- 日本語のフォント: `Resources/Fonts/MPLUS1p-Medium.ttf` (WebGL では端末のフォントを借りられないため同梱)。

---

## 7. 同じ失敗をしないために

| 症状 | 原因 / 対処 |
|---|---|
| 新しい部品で NullReferenceException | Awake でビルダーが入れるフィールドを使っていた。初めて使う時に準備する |
| 平たい円柱の当たり判定が球になる | CapsuleCollider のせい。Prim() で凸 MeshCollider に置き換えている |
| マテリアルのない Renderer で警告 | 見えない当たり判定は MeshRenderer を外してコライダーだけにする (HitOnly) |
| TextMesh が岩や地面を透けて見える | 見える場所にいる間だけ Renderer を有効にする (鉱山・潜水艦で対処済み) |
| 影なしの明かりが壁を通り抜ける | ライトレイヤーで照らす物を分ける (音楽ホール) |
| 屋根の下でも外の光が差す | シーンの平行光源も FadeSun で消す (otherSuns) |
| 入れ子のコルーチンが止まらない | Target を Update 駆動の状態機械にした |
| 実行中に RenderSettings.reflectionIntensity を切り替えると物が水色に光る | 材質の側で映り込みを切る (鉱山) |
| 的が特定の屋台に偏る | 先に屋台を選んでから、その屋台の的を選ぶ (スナイパー・別の世界の的) |

---

## 8. 未解決・確認したいこと

- **Unity での通しの動作確認**: 特に、フィーバー中 / 豪華タイム中にモードを始める、モードの途中で時間切れ、モードの途中でリセット。
- **「海戦モードで屋台が戻らない」** (2026-10-15 の報告): 直ったかわかっていない。
- **シーンに GalleryBuilder が保存されていない** (上の「1. シーン」)。
- 改善の候補 (2026-10-24 にユーザーへ提案。まだ手を付けていない): 実機 (Quest 単体・WebGL) での読み込み時間と重さの計測、平行光源を1つにまとめる、README に素材の入れ方を書く、初めて遊ぶ人向けに的の説明、VR 酔い対策 (上下に動くモード)、BGM / 効果音の音量調整、unityroom のランキング、左手で撃つ切り替え。
- 音楽モードの五線譜の出し方 (早めに入れ替える・いつも3本以上・もっと上まで) は、2026-10-24 に一度変えたが、ステージを戻した時にいっしょに元に戻っている。必要ならやり直す。
