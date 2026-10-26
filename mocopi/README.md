# mocopi Receiver Plugin for Unity

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](./LICENSE)
[![Unity](https://img.shields.io/badge/Unity-6+-blue.svg)](https://unity.com/)

mocopi Receiver Plugin for Unity is a plugin for receiving motion data transmitted from the mocopi app and applying it to 3D avatars in Unity in real-time.

## License Notice
- This project is licensed under the Apache License 2.0 - see the [LICENSE](./LICENSE) file for details.
- Notwithstanding the foregoing, this repository does not include the mocopi logo or application icons. Use of these assets requires entering into a separate mocopi Logo and Icon License Agreement. ([here](https://www.sony.co.jp/en/Products/mocopi-dev/en/others/LogoGuideline.html))

## Overview

**mocopi** is a motion capture system that captures full-body motion data using a smartphone app or PC app combined with mocopi sensors. By using this Unity plugin, you can easily apply captured motion data to 3D avatars in your Unity applications.

### Key Features

- **Real-time Motion Capture**: Directly receive motion data from mocopi app via UDP
- **Avatar Integration**: Apply motion data to Unity avatars with automatic bone mapping
- **Cross-Platform Support**: Works on Windows, macOS, Android, and iOS
- **Easy Integration**: Simple APIs for quick setup and customization

## Supported Platforms

| Platform | Supported Versions |
|----------|-------------------|
| **Windows** | Windows 10/11 |
| **macOS** | macOS 10.15+ |
| **Android** | Android 11+ |
| **iOS** | iOS 15.7+ |

> **Note**: Linux is not currently supported.

## Requirements

### Development Environment
- **Unity**: Unity 6000.0.60f1 or later
- **Network**: Local network connection between mocopi app and Unity application

### Runtime Requirements
- mocopi app (Mobile version: Android/iOS, or PC version: Windows)
- UDP communication connection between mocopi app and Unity application
- Maximum motion data frame rate: Depends on mocopi app settings

## Installation

1. Download the latest release from [Releases](https://github.com/sony/mocopi-receiver-plugin-unity/releases)
2. Extract the package to your Unity project's `Packages` directory
3. Unity will automatically import the package

## Quick Start

### 1. Avatar Setup

1. Drag & drop a humanoid avatar model file (FBX format, etc.) into `Assets`
   - To use the sample model, please use `Assets/MocopiReceiver/Samples/ReceiverSample/Models/MocopiAvatar.fbx`
2. Select the model asset and click the Rig tab in the Inspector
3. Set the following items and click the `Apply` button:
   - **Animation Type**: Humanoid
   - **Avatar Definition**: Create From This Model
4. Click the `Configure...` button to check bone mapping
5. Drag & drop the model asset into the Hierarchy window
6. Select the avatar object and add `Mocopi Avatar` from `Add Component`

### 2. Receiver Setup

1. Drag & drop the following Prefab into the Hierarchy window:
   ```
   Assets/MocopiReceiver/Resources/Prefab/MocopiSimpleReceiver.prefab
   ```
2. Select the Receiver object and expand `Mocopi Simple Receiver (Script)` in the Inspector
3. Open `Avatar Settings` and click the `+` button
4. Set the following in `Element 0`:
   - **Mocopi Avatar**: Drag & drop the Avatar object from Hierarchy
   - **Port**: UDP communication port number with mocopi app (default: 12351)

### 3. Network Setup

Please ensure that the smartphone running the mocopi app and your Unity application are connected to the same local network. The plugin communicates via UDP on port 12351 by default.

## Sample Projects

### ゲーム結果とランキング

UIPanels は1920×1080を基準に、ウィンドウの大きさに合わせて配置・拡縮します。説明欄は上部中央、結果は左側、ランキングは右側です。狭い画面ではメニュー用カメラが引き、手で触れるボタンの位置を保ったまま画面内に収めます。1920×1080・1280×720・1024×768で1人／2人表示を確認しています。

最後の壁の判定が終わると、0.5秒待って結果画面に切り替わります。2人プレイでは両者の終了後に待ち時間が始まります。

結果画面の右側に、このPC内の上位10件を表示します。得点が高い順、同点なら接触ブロック数が少ない順です。ランクインした場合だけキーボードで名前（12文字まで）を入力し、「登録」をクリックしてください。日本語入力に対応し、「登録しない」でスキップできます。1人・協力・対戦は別ランキングで、協力はチーム名、対戦は各プレイヤーの名前を登録します。登録後の「もう一度」「選択に戻る」はマウスでも従来のタッチ操作でも選べます。

### 2Pと協力プレイ

GameMode／GameMode-Halloweenの2Pはsuguruを使用します。mocopi受信先・Humanoid骨格・体の当たり判定を設定し、壁の形状、速度、出現間隔、枚数、保存ポーズ、クリア判定を各シーンの1Pに揃えています。

人数選択（1P／2P）と遊び方の選択（協力／対戦）を分けています。2Pを選んでもアバター・遊び方の選択中は1Pと同じカメラ表示を維持し、協力を決定すると2人を映す引きの画面、対戦を決定すると分割画面に切り替わります。選び直すと1Pと同じ選択用の表示に戻ります。

Suguruの付属テクスチャ6枚は `Assets/Models/suguru/Materials` の材質に割り当て済みです。Maoと同じSoft Toonシェーダーを使用し、FBXの材質リマップに保存しています。再設定は `Tools > mocopi > Configure Suguru Materials` から実行できます。

協力プレイは共通のカメラとHUDで二人を横並びに表示し、足元に直径10mの共通サウンドビジュアライザーを配置します。各プレイヤーの壁の当たり判定と得点は独立し、結果は合計します。対戦は従来の画面分割です。

### ハロウィンステージ

`GameMode-Halloween.unity` には、各プレイヤーの通路脇に12個の光るかぼちゃと16匹の羽ばたくコウモリ、奥の月、墓石、薄い地霧と火の粉を配置しています。カウントダウン・ラウンド終了・壁のクリア時に、かぼちゃの光がゆっくり強まり火の粉が増えます。飾りに当たり判定はありません。

演出全体は `Player1Arena/Environment/HalloweenDecor` と2P側の同名オブジェクトで調整できます。素材とメッシュは `Assets/Environment/Halloween` に保存され、通常のGameModeと共有するネオン素材は変更しません。`Tools > mocopi > Decorate Halloween Scene` で装飾を再生成できます（手動で移動した装飾も初期配置に戻ります）。

### ポーズ型の撮影・保存

`Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/CaptureMaskDataMode.unity` を開いて Play すると、mocopi のポーズを壁の穴として撮影できます。**Space** で撮影（3秒後）、**S** で保存、**R** で撮り直し、**P** で壁を再配置します。

無地の壁の右隣で撮影結果を確認し、既存の `WallMaskData` と互換のある JSON として保存できます。詳しくは [CaptureMaskDataMode の操作手順](Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/CaptureMaskDataMode.md) を参照してください。

Unity の編集モードで **Tools > mocopi > Capture 30 Random Poses** を実行すると、CaptureMode のモデルのコピーにランダムなポーズと横位置を設定し、30種類を撮影・保存できます。撮影後に穴の余白を外側へ5cm広げます。保存先は通常の型ライブラリで、確認用画像・JSONのコピー・撮影記録は `output/captured-random-poses-*` に作成されます。シーン内のモデルや壁の設定は変更しません。

**右20・中央20・左20の計60ポーズ**を `Assets/StreamingAssets/PoseMasks` に同梱しています。腕・肘・体の傾き、片足立ち、横開き、しゃがみ12枚、小さなジャンプ6枚（15〜18cm）を組み合わせ、横移動しただけの同一形も除外しています。現在のGameMode／GameMode-Halloweenの1P用の30行×60列・1セル10cmで、輪郭の外側に5cmの余白を付けています。2Pも1Pと同じ壁テンプレートを使うため、二人ともこの60枚を使用できます。

WallManager の **Use Saved Masks** が有効で **Saved Mask Directory** が空なら、同梱60枚とこのPCで撮影した型を合わせて抽選します。同じ形のローカルコピーは重複しません。フォルダを指定した場合はそのフォルダだけを使用します。同梱データはビルドにも含まれるため、別PCへの移動で型が消えません。

**Tools > mocopi > Capture 60 Balanced Poses** で60枚を再撮影できます。確認画像・撮影記録は `output/captured-balanced-poses-*` に保存し、全60枚の撮影成功後に同梱JSONを更新します。**Validate 60 Balanced Poses** で枚数・左右中央の内訳・ゲームの実際の壁への適用を検証できます。

This repository includes sample scenes demonstrating basic usage:

- **Basic Receiver Sample**: Shows fundamental setup and avatar control
- **Advanced Integration**: Demonstrates custom bone mapping and data processing

To check the samples, please refer to `Assets/MocopiReceiver/Samples/`.

## Troubleshooting

### Common Issues

**Q: Motion data is not being received**
- Please verify that both devices are connected to the same network
- Please check UDP port configuration (default: 12351)
- Please confirm that the mocopi app is transmitting data

**Q: Avatar movement appears jerky**
- Please check network latency and stability
- Please verify frame rate settings in the mocopi app
- Please consider implementing motion smoothing

For additional troubleshooting, please see the [FAQ](https://www.sony.co.jp/en/Products/mocopi-dev/jp/documents/ReceiverPlugin/Unity/TroubleShoot.html).

## Support

For technical support and questions, please join the following Discord server:

**Discord**: https://discord.gg/k55wY45y5N

## Resources

- **mocopi Official Developer Site**: [https://sony.net/mocopi-dev/](https://sony.net/mocopi-dev/)
- **Documentation**: [Unity Plugin Guide](https://www.sony.co.jp/Products/mocopi-dev/jp/documents/ReceiverPlugin/Unity/AboutPlugin.html)

---

**Copyright © 2025 Sony Corporation. All rights reserved.**
