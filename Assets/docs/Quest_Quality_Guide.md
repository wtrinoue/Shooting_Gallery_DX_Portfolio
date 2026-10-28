# 射的屋さんデラックス — Quest 単体 (APK) の画質を良くする方法

作成: 2026-10-24 (プロジェクトの設定を読んで調べた内容。設定はまだ何も変えていない)

---

## 今の設定

Quest 単体 (Android) は、画質の設定 (Quality) の **「Mobile」** を使う。中身は `Assets/Settings/Mobile_RPAsset` (と `Mobile_Renderer`)。
PC は「PC」(`Assets/Settings/PC_RPAsset`)、WebGL (unityroom) も「Mobile」。

| 項目 | 今の値 (Mobile) | 参考: PC |
|---|---|---|
| Render Scale (描く解像度) | **0.8** | 1.0 |
| MSAA (ギザギザを消す) | **なし** | なし |
| 影の解像度 / 柔らかい影 | 1024 / なし | 2048 / あり |
| HDR | あり | あり |
| Foveated Rendering (視線の外を粗く描く) | **オフ** | − |
| グラフィックス API | Vulkan (OK) | − |
| 描き方 | Forward (明かり 32 まで) | Forward+ (明かり 256 まで) |

---

## 画質を良くする方法 (効果が大きい順)

### 1. Render Scale を 0.8 → 1.0 にする ★いちばん効く
今は 8割の解像度で描いて引き伸ばしているので、全体がぼやけて見える。

- 場所: Project ウィンドウで `Assets/Settings/Mobile_RPAsset` を選ぶ → Inspector の **Quality → Render Scale** を **1** にする
- Quest 3 なら 1.2 くらいまで上げても動くことがある (重さを見ながら決める)。

### 2. MSAA を 4x にする ★ギザギザが消える
細い棒・文字・的のふちのギザギザは、VR で特に目立つ。Quest は MSAA 4x が比較的軽い作りの機械なので、おすすめ。

- 場所: 同じ `Mobile_RPAsset` の **Quality → Anti Aliasing (MSAA)** を **4x** にする

### 3. 重くなった分を Foveated Rendering で取り戻す
視線の中心はくっきり、外側だけ粗く描く Quest の機能。外側はもともとレンズでぼやけるので、見た目はほとんど変わらずに軽くなる。

- 場所: **Edit → Project Settings → XR Plug-in Management → OpenXR → Android のタブ** → **Foveated Rendering** にチェック
- ※ 有効にした後、どのくらい粗くするかを決めるコードが少し必要 (`XRDisplaySubsystem.foveatedRenderingLevel` など)。

### 4. HDR を切る (軽くなった分を 1・2 に回す)
Quest では HDR の負担が大きいわりに、見た目の差は小さめ。

- 場所: `Mobile_RPAsset` の **Quality → HDR** のチェックを外す
- ※ Bloom (光のにじみ) の出方が少し変わることがある。

### 5. 影をきれいにする (余裕があれば)
- 場所: `Mobile_RPAsset` の **Lighting → Main Light → Shadow Resolution** を **2048**、**Soft Shadows** にチェック
- ※ 影はかなり重いので、最後に回す。

### おまけ: 画面の後処理 (Post Processing)
シーンの Global Volume (`Assets/Settings/SampleSceneProfile`) に **Bloom・Vignette (周りを暗くする)・Tonemapping** が入っている。
Quest では後処理が重いうえ、Vignette は VR だと視界の端が暗く見えるだけになりがち。Quest 用には外すか弱めると、その分を 1・2 に回せる。

---

## おすすめの進め方

1. まず **「1. Render Scale 1.0」と「2. MSAA 4x」だけ** 変えて、実機で遊んでみる。見た目の改善は、ほとんどこの2つで感じられるはず。
2. カクつく (72fps を切る) なら、**「3. Foveated Rendering」と「4. HDR オフ」** で軽くする。
3. まだ余裕があれば **「5. 影」**。

メニュー「ShootingGallery → Quest (Android) の VR 設定をする」(`Assets/ShootingGallery/Scripts/Builder/Editor/QuestSetup.cs`) に、1〜4 の設定と Foveated Rendering のコードをまとめて入れることもできる (未対応)。

---

## 注意: 設定を切り替えた後に出る警告

Console に、次のような警告が大量に出ることがある。

```
Property (_AdditionalLightsPosition) exceeds previous array size (256 vs 32). Cap to previous size. Restart Unity to recreate the arrays.
```

(`urp_ReflProbes_...` の (64 vs 32) なども同じ)

**原因**
URP は明かりのデータの入れ物 (シェーダーの配列) を、Unity を起動して最初に描いた時の大きさで作り、後から大きくできない。
Mobile の設定 (明かり 32 まで) で描いた後に、PC の設定 (Forward+、明かり 256 まで) に切り替わると出る。
ビルド先を Android / WebGL に切り替えた後や、画質の設定を切り替えた後に起きやすい。

**直し方**
Unity を再起動すれば消える。ゲームのコードとは関係なく、ビルドしたゲームにも影響しない。

**気をつけること**
- 警告が出ている間は、エディターの中で明かりが 32 個までに切り詰められる。音楽ホールのように明かりの多い場面は本番と見え方が違うことがあるので、見た目を確かめる前に一度 Unity を再起動すること。
- 防ぎたい時は、同じエディターの中でビルド先や画質の設定を行き来しないのがいちばん簡単。
- (Mobile も Forward+ にすれば起きなくなるが、Quest 単体では重くなるのでおすすめしない)
