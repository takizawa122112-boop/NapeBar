# NapeBar

Windows向けの、Keychron Nape Pro用バッテリー残量表示ツールです。
Keychron Launcherを常駐させなくても、2.4GHzレシーバー経由で残量を取得し、画面上部のカプセル型バーと通知領域に表示します。

> Unofficial community project. Not affiliated with Keychron.

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## Features

- CodexBar風の上部ステータスバー（`94%  ↻ 2.4G`）
- Windows通知領域の数字アイコン
- 2.4GHzレシーバー経由のバッテリー取得
- 60秒ごとの自動更新と手動更新
- ステータスバーのドラッグ移動、表示／非表示切り替え
- 通知領域アイコンのダブルクリックでステータスバーを表示
- Windowsログオン時の自動起動
- Keychron Launcherを開くショートカット
- 追加のNuGetパッケージ不要

## Download

最新版は [Releases](https://github.com/takizawa122112-boop/NapeBar/releases) から、`NapeBar-v*-win-x64.zip` をダウンロードしてください。

ZIPを展開し、`NapeBar.exe` を起動します。インストーラーはありません。終了する場合は通知領域のアイコンを右クリックして「終了」を選びます。

初回起動時にWindows SmartScreenが表示される場合があります。個人署名のない実行ファイルであるためで、ソースコードとビルド手順はこのリポジトリで確認できます。

## Verified hardware

| Connection | Device seen by Windows | Status |
| --- | --- | --- |
| 2.4GHz receiver | `Keychron Link-KM`, VID `0x3434`, PID `0xD026` | Verified |
| USB wired | `Nape Pro`, VID `0x3434`, PID `0x0440` | Detection code included; hardware verification welcome |

Nape Pro本体やレシーバーのファームウェアバージョンによって、表示や通信仕様が変わる可能性があります。

## How it works

NapeBarは、Keychron Launcherと同じベンダーHID経路を読み取り目的で利用します。`VID 0x3434` の `Usage Page 0xFF60` インターフェースへ32バイトの問い合わせを送り、`A7 31 <battery>` の応答に含まれる値を0〜100%として表示します。
2.4GHzレシーバーとUSB接続を同時に検出した場合は、実機確認済みの2.4GHzレシーバーを優先します。

```text
request:  A7 31 00 00 ...
response: A7 31 5E ...       # 0x5E = 94%
```

アプリは `0xFF60` インターフェースを優先して自動検出します。`0xFF60` が存在する場合、未検証の `0x008C` インターフェースへは問い合わせません。ファームウェアを書き換えたり、設定を変更したりはしません。

## Build from source

Windowsに付属する .NET Framework C#コンパイラーだけでビルドできます。

```powershell
Set-Location 'C:\path\to\NapeBar'
.\build.ps1
```

生成物:

- `build\NapeBar.exe` — 常駐アプリ
- `build\NapeBatteryProbe.exe` — 通信確認用CLI

## Troubleshooting

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

## Credits

プロトコル調査の出発点として、macOS向けの [menu-bar-nape-pro-status](https://github.com/krgpi/menu-bar-nape-pro-status) と [nape-hud](https://github.com/Yucky39/nape-hud) を参考にしました。

## Disclaimer

Keychron公式ではない個人開発ツールです。HIDのベンダー独自コマンドを使用するため、異常が起きた場合はアプリを終了し、Keychron Launcherで再接続してください。現状、バッテリー残量の読み取り以外の設定変更やファームウェア更新は行いません。

## License

MIT License. See [LICENSE](LICENSE).

---

## English

NapeBar is an unofficial Windows battery indicator for the Keychron Nape Pro trackball mouse. It reads the battery level over the 2.4GHz receiver and shows it in a CodexBar-style capsule at the top of the screen and in the Windows notification area. Keychron Launcher does not need to stay open.

The status bar can be dragged anywhere inside the physical display, including over the taskbar. Its position is saved and restored after a restart. Left-clicking the tray icon shows it again without activating it, and the tray menu includes position reset and connection-label visibility controls.

Download `NapeBar-v*-win-x64.zip` from [Releases](https://github.com/takizawa122112-boop/NapeBar/releases), extract it, and run `NapeBar.exe`.

The verified path is the 2.4GHz receiver (`Keychron Link-KM`, VID `0x3434`, PID `0xD026`). USB detection support is included, but USB hardware verification is still welcome. The app does not write firmware or change device settings.

Build with the .NET Framework C# compiler included with Windows:

```powershell
.\build.ps1
```

This is an unofficial community project and is not affiliated with Keychron. MIT licensed.

For local convenience, `build.ps1` also copies the latest tray executable to the project root. The diagnostic probe remains under `build\`.
