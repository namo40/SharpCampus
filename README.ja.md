# SharpCampus

[English](README.md) | [한국어](README.ko.md) | **日本語**

SharpCampus は [MagicOnion](https://github.com/Cysharp/MagicOnion) と [Cysharp](https://github.com/Cysharp) のライブラリ群で1対1コンソールのテトロミノ対戦ゲーム（テトリスでおなじみの落ち物パズルジャンル）を実装した、教育用リファレンスゲームサーバーです。

ゲームサーバーからコンテナデプロイまでが一つのコードベースであり、このコードに沿ってライブラリを一つずつ解説する学習サイトを提供します。

## ソリューション構成

| プロジェクト | 説明 |
| --- | --- |
| `src/SharpCampus.Shared` | クライアントに公開する契約（サービスインターフェイスと DTO）。Unity クライアントからも利用できるよう `netstandard2.1` をターゲットにします。 |
| `src/SharpCampus.Shared.Internal` | サーバー間だけでやり取りする契約で、クライアントには配布しません。 |
| `src/SharpCampus.GameCore` | ネットワークやホスティングに依存しないゲームルールとシミュレーション。こちらも `netstandard2.1` です。 |
| `src/SharpCampus.ApiServer` | アカウント、プロフィール、マッチメイキングを担当するメタゲームのホストです。 |
| `src/SharpCampus.RoomServer` | サーバー権威のティックループを回すリアルタイム対戦ホストです。 |
| `src/SharpCampus.Server.Common` | 2つのサーバーホストが共有する共通部品です。 |
| `src/SharpCampus.Cli` | ゲームをプレイし、サーバーの動作確認にも使う .NET コンソールクライアントです。 |
| `tools/SharpCampus.MasterDataTool` | ゲームのマスターデータを生成するコマンドラインツールです。 |
| `tests/SharpCampus.GameCore.Tests` | `SharpCampus.GameCore` のテスト。 |
| `tests/SharpCampus.ApiServer.Tests` | `SharpCampus.ApiServer` のテスト。 |
| `tests/SharpCampus.RoomServer.Tests` | `SharpCampus.RoomServer` のテスト。 |
| `tests/SharpCampus.ServerCommon.Tests` | `SharpCampus.Server.Common` のテスト。 |
| `tests/SharpCampus.LoadTest` | サーバーの負荷テスト用ツール。 |

パッケージのバージョンは `Directory.Packages.props` で一元管理し、全プロジェクト共通のビルド設定は `Directory.Build.props` に置いています。

## 前提環境

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/)

## ビルドとテスト

```bash
dotnet build
dotnet test
```

## ライセンス

MIT ライセンスです。[LICENSE](LICENSE) を参照してください。
