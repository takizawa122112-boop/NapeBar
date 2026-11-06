# NapeBar

Keychron Nape Pro用の、バッテリー残量表示ツールです。macOSとWindowsに対応しています。
Keychron Launcherを開かなくても、残量を画面に常に表示できます。

> Unofficial community project. Not affiliated with Keychron.

![Platform](https://img.shields.io/badge/platform-macOS%20%7C%20Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## Download

最新版は [Releases](https://github.com/takizawa122112-boop/NapeBar/releases/latest) からダウンロードしてください。

| OS | ファイル | 対応環境 |
| --- | --- | --- |
| macOS | `NapeBar-v*-mac.zip` | macOS 14 (Sonoma) 以降、Apple Silicon / Intel |
| Windows | `NapeBar-v*-win-x64.zip` | Windows 10 / 11 (x64) |

## 対応する接続方法

| 接続 | macOS | Windows |
| --- | --- | --- |
| Bluetooth | ✅ 残量 | — |
| USB-C 有線 | ✅ 残量・レイヤー・向き | 検出コードあり（実機確認は未） |
| 2.4GHz レシーバー | 検出コードあり（実機確認は未） | ✅ 残量 |

接続を切り替えても、アプリを再起動する必要はありません（macOS）。

---

## macOS

メニューバーに `67% BT` のように、残量と接続方式を表示します。

### 機能

- Bluetooth / USB-C / 2.4GHzレシーバーの自動切り替え
- メニューバーに残量と接続方式（`BT` / `USB` / `2.4G`）を表示
- USB-C / 2.4GHz接続のときは、レイヤーと向きも表示可能
- ログイン時の自動起動
- 外部のライブラリやドライバーは不要

### インストール

1. `NapeBar-v*-mac.zip` をダウンロードして展開します。
2. `NapeBar.app` を「アプリケーション」フォルダへ移動します。
3. 初回起動時は、Appleの公証を受けていないため、開けない旨の警告が出ます。
   「システム設定 → プライバシーとセキュリティ」を開き、NapeBarの「このまま開く」を押してください。
4. Bluetoothの使用許可を求められたら「許可」を押します。Bluetooth接続の残量を読むために使います。

ターミナルを使える場合は、次のコマンドで警告を出さずに開けるようにもできます。

```bash
xattr -dr com.apple.quarantine /Applications/NapeBar.app
```

終了するには、メニューバーのNapeBarをクリックして「終了」を押します。

### 仕組み

- **USB-C / 2.4GHz**: Keychron Launcherと同じベンダーHID（`Usage Page 0xFF60`）へ、読み取り用の問い合わせ（`A7 31`）を送ります。
- **Bluetooth**: Bluetooth接続ではベンダーHIDが見えないため、Nape Proが出している標準のBattery Service（`0x180F` / Battery Level `0x2A19`）を読み取ります。残量が変わると通知を受けて更新します。

両方見える場合は、レイヤーと向きも取れるUSB-C / 2.4GHz側を優先します。
問い合わせ先は、Nape Pro（PID `0x0440`）とKeychron Link-KM（PID `0xD026`）だけです。ほかのKeychron製キーボードには問い合わせません。

### ソースからビルド

Xcodeと [XcodeGen](https://github.com/yonaskolb/XcodeGen) が必要です。

```bash
brew install xcodegen
cd mac
./build.sh
```

`mac/build/NapeBar-mac.zip` が作られます。

---

## Windows

画面上部のカプセル型バーと、通知領域に残量を表示します。

### 機能

- CodexBar風の上部ステータスバー（`94%  ↻ 2.4G`）
- Windows通知領域の数字アイコン
- 2.4GHzレシーバー経由のバッテリー取得
- 60秒ごとの自動更新と手動更新
- ステータスバーのドラッグ移動、表示／非表示切り替え
- 通知領域アイコンのダブルクリックでステータスバーを表示
- Windowsログオン時の自動起動
- Keychron Launcherを開くショートカット
- 追加のNuGetパッケージ不要

### インストール

ZIPを展開し、`NapeBar.exe` を起動します。インストーラーはありません。終了する場合は、通知領域のアイコンを右クリックして「終了」を選びます。

初回起動時に、Windows SmartScreenが表示される場合があります。個人署名のない実行ファイルであるためで、ソースコードとビルド手順はこのリポジトリで確認できます。

### 確認済みの機器

| Connection | Device seen by Windows | Status |
| --- | --- | --- |
| 2.4GHz receiver | `Keychron Link-KM`, VID `0x3434`, PID `0xD026` | Verified |
| USB wired | `Nape Pro`, VID `0x3434`, PID `0x0440` | Detection code included; hardware verification welcome |

Nape Pro本体やレシーバーのファームウェアバージョンによって、表示や通信仕様が変わる可能性があります。

### 仕組み

NapeBarは、Keychron Launcherと同じベンダーHID経路を、読み取り目的で利用します。`VID 0x3434` の `Usage Page 0xFF60` インターフェースへ32バイトの問い合わせを送り、`A7 31 <battery>` の応答に含まれる値を0〜100%として表示します。
2.4GHzレシーバーとUSB接続を同時に検出した場合は、実機確認済みの2.4GHzレシーバーを優先します。

```text
request:  A7 31 00 00 ...
response: A7 31 5E ...       # 0x5E = 94%
```

アプリは `0xFF60` インターフェースを優先して自動検出します。`0xFF60` が存在する場合、未検証の `0x008C` インターフェースへは問い合わせません。ファームウェアを書き換えたり、設定を変更したりはしません。

### ソースからビルド

Windowsに付属する .NET Framework C#コンパイラーだけでビルドできます。

```powershell
Set-Location 'C:\path\to\NapeBar\windows'
.\build.ps1
```

生成物:

- `windows\build\NapeBar.exe` — 常駐アプリ
- `windows\build\NapeBatteryProbe.exe` — 通信確認用CLI

### トラブルシューティング

デバイスの列挙:

```powershell
.\build\NapeBatteryProbe.exe --list
```

バッテリー問い合わせ:

```powershell
.\build\NapeBatteryProbe.exe
```

成功すると、次のように表示されます。

```text
BATTERY=94%  DEVICE=Keychron Link-KM  CONNECTION=2.4G
```

応答経路だけを確認する場合:

```powershell
.\build\NapeBatteryProbe.exe --ping
```

Keychron Launcherが同時にHIDデバイスを使用していると、Windows側でインターフェースを開けない場合があります。その場合はLauncherのタブを閉じてから再試行してください。

ログは `%LOCALAPPDATA%\NapeProBatteryTray\app.log` に保存されます。
`app.log` は最大2MiBで、上限を超えると直前のログを `app.old.log` として1世代だけ保持します。
予期しない例外が発生した場合も、このログに記録して常駐を継続するようにしています。

---

## Credits

- macOS版は、[menu-bar-nape-pro-status](https://github.com/krgpi/menu-bar-nape-pro-status)（MIT License, © krgpi）を元に、Bluetooth対応と接続の自動切り替えを加えたものです。ライセンス表記は [mac/THIRD_PARTY_NOTICES.md](mac/THIRD_PARTY_NOTICES.md) にあります。
- プロトコル調査の出発点として、[nape-hud](https://github.com/Yucky39/nape-hud) も参考にしました。

## Disclaimer

Keychron公式ではない個人開発ツールです。HIDのベンダー独自コマンドを使用するため、異常が起きた場合はアプリを終了し、Keychron Launcherで再接続してください。バッテリー残量などの読み取り以外の設定変更や、ファームウェアの更新は行いません。

## License

MIT License. See [LICENSE](LICENSE).

---

## English

NapeBar is an unofficial battery indicator for the Keychron Nape Pro trackball, available for macOS and Windows. Keychron Launcher does not need to stay open.

Download the latest release from [Releases](https://github.com/takizawa122112-boop/NapeBar/releases/latest):

- **macOS** (14 Sonoma or later, Apple Silicon / Intel): `NapeBar-v*-mac.zip`. Move `NapeBar.app` to Applications. The app is not notarized, so on first launch open System Settings → Privacy & Security and click "Open Anyway". Allow Bluetooth access when asked.
- **Windows** (10 / 11 x64): `NapeBar-v*-win-x64.zip`. Extract it and run `NapeBar.exe`.

On macOS, NapeBar shows the battery level and connection type (e.g. `67% BT`) in the menu bar and switches automatically between Bluetooth, USB-C, and the 2.4GHz receiver without a restart. Over USB-C or the receiver it reads the vendor HID interface (`0xFF60`, command `A7 31`) and can also show the active layer and orientation. Over Bluetooth, where the vendor HID interface is not exposed, it reads the standard Battery Service (`0x180F` / `0x2A19`).

On Windows, it shows the battery level in a CodexBar-style capsule at the top of the screen and in the notification area. The verified path is the 2.4GHz receiver (`Keychron Link-KM`, VID `0x3434`, PID `0xD026`).

The app does not write firmware or change device settings. The macOS version is based on [menu-bar-nape-pro-status](https://github.com/krgpi/menu-bar-nape-pro-status) by krgpi (MIT). This is an unofficial community project and is not affiliated with Keychron. MIT licensed.
