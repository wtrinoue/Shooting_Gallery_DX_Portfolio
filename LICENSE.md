# 射的屋さんデラックス (Shooting Gallery DX) — ライセンスと使用素材

3D モデル・色・下の表にない効果音は、すべてゲームのコードで生成しています。

## 音楽

| 使う場面 | 曲名 / 作者 | 出典 | ライセンス・条件 |
|---|---|---|---|
| ラウンドの BGM (2 MIN / 100,000 POINTS / エンドレス) | 天国と地獄 (オッフェンバック) | [音楽の卵](https://ontama-m.com/) (takai) | [音楽の卵 利用規約](https://ontama-m.com/ongaku.html)。クレジット表記は不要 (あると喜ばれる) |
| 美術館モード | ロシアの踊り (チャイコフスキー「くるみ割り人形」より) | [音楽の卵](https://ontama-m.com/) (takai) | 同上 |
| 空中ステージ | 大空は君のもの | [音楽の卵](https://ontama-m.com/) (takai) | 同上 |
| カジノモード | maou_bgm_acoustic01b | [魔王魂](https://maou.audio/) | [魔王魂 利用規約](https://maou.audio/rule/)。**クレジット表記が必須**(「音楽：魔王魂」) |
| 音楽モード | (魔王魂の曲) | [魔王魂](https://maou.audio/) | 同上 |
| 夏祭りモード | maou_bgm_ethnic09 | [魔王魂](https://maou.audio/) | 同上 |
| 西部劇モード | american western country | [魔王魂](https://maou.audio/) | 同上 |
| 幽霊モード | Noisy Grave / こおろぎ | [OpenTracks](https://opentracks.com/) (旧 DOVA-SYNDROME) | OpenTracks 音源利用ライセンス。表記は不要 |
| 海戦モード | Battle on the Ship / beckman | [OpenTracks](https://opentracks.com/) | 同上 |
| 忍者モード | 風切 / ゆうり (Yuli Audio Craft) | [OpenTracks](https://opentracks.com/) | 同上 |
| 宇宙モード | Space / leberch | [Pixabay](https://pixabay.com/) | Pixabay Content License。表記は不要 |
| 潜水艦モード | Ocean Stillness / kallydru | [Pixabay](https://pixabay.com/) | 同上 |
| Unknown ステージ | Liminal Pigments / lux-aeterna | [Pixabay](https://pixabay.com/) | 同上 |
| 怪獣モード | Candy Coated Catastrophe | Google Gemini (AI で生成) | AI で作ったことを明記し、人が作ったと思わせない |
| 鉱山モード | Veins of Glowing Stone | Google Gemini (AI で生成) | 同上 |

## 効果音 (ファイルを使っているもの)

| 使う場面 | 名前 / 作者 | 出典 | ライセンス・条件 |
|---|---|---|---|
| 2 MIN の終わりの拍手 | 拍手 (ホール) | [小森平](https://taira-komori.net/event01.html) | 小森平の利用規約。できればリンクを表示 |
| 西部劇のリボルバーの銃声 | Pistol Shot / mrfriends | [Pixabay](https://pixabay.com/) | Pixabay Content License |
| 西部劇の弾を込める音 | Gun Reload / universfield | [Pixabay](https://pixabay.com/) | 同上 |

**どの素材サイトにも共通の禁止事項**: 曲・音そのものの再配布や販売、自作と偽ること。

## フォント

| 名前 | 作者 | ライセンス |
|---|---|---|
| M PLUS 1p (Medium) — ゲーム内の文字すべて | Copyright 2016 The M+ Project Authors ([GitHub](https://github.com/coz-m/MPLUS_FONTS)) | SIL Open Font License 1.1 |

## ライブラリ・パッケージ

| 名前 (パッケージ) | バージョン | 作者 | ライセンス | 用途 |
|---|---|---|---|---|
| WebXR Export (`com.de-panther.webxr`) | 0.25.0 | Oren Weizman (De-Panther) ([GitHub](https://github.com/De-Panther/unity-webxr-export)) | Apache License 2.0 | WebGL (unityroom) で VR を動かす |
| WebXR Interactions (`com.de-panther.webxr-interactions`) | 0.25.0 | 同上 | Apache License 2.0 | 同上 (コントローラーなど) |
| └ 中に含まれる Unity WebVR Export | — | Mozilla Corporation | Apache License 2.0 | WebXR Export の元になったもの |
| └ 中に含まれる XR SDK | — | Unity | Unity Companion License | 同上 |
| Meta XR Audio SDK (`com.meta.xr.sdk.audio`) | 85.0.0 | Meta Platforms, Inc. | [Oculus SDK License](https://developers.meta.com/horizon/licenses/oculussdk/) (中に Kiss FFT・libjpeg などの第三者のライセンス表記あり) | 立体音響 (Project Settings → Audio の Spatializer Plugin。WebGL では使われない) |
| Universal Render Pipeline (`com.unity.render-pipelines.universal`) | 17.3.0 | Unity Technologies | Unity Companion License | 描画 |
| Input System (`com.unity.inputsystem`) | 1.20.0 | Unity Technologies | Unity Companion License | 入力 |
| OpenXR Plugin (`com.unity.xr.openxr`) | 1.16.1 | Unity Technologies | Unity Companion License | Quest / PC VR |
| Unity OpenXR: Meta (`com.unity.xr.meta-openxr`) | 2.7.0 | Unity Technologies | Unity Companion License | Quest |
| XR Plugin Management (`com.unity.xr.management`) | 4.6.1 | Unity Technologies | Unity Companion License | VR の切り替え |
| AI Navigation / Timeline / uGUI / Visual Scripting ほか | — | Unity Technologies | Unity Companion License | Unity 標準 |
| Unity エンジン本体 | 6000.3.23f1 | Unity Technologies | Unity Terms of Service | Made with Unity |

開発用のパッケージ (Rider・Visual Studio との連携、Unity Version Control、Test Framework) はゲームに入らないので省いています。

## 表記について

- ゲーム内のクレジット: `Assets/Resources/THIRD_PARTY_NOTICES.txt` (CREDITS の看板でページ送りして表示)
- Apache License 2.0 と SIL Open Font License 1.1 の全文は、上のファイルの最後に入れています。
