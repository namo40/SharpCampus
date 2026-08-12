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
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)（起動している必要があります）
- [Supabase CLI](https://supabase.com/docs/guides/local-development/cli/getting-started)

## ビルドとテスト

```bash
dotnet build
dotnet test
```

テストは単体で完結しており、Docker も Supabase スタックも必要ありません。

## クイックスタート

アカウントは [Supabase Auth](https://supabase.com/docs/guides/auth) が管理するので、まずローカルスタックを起動します。
Postgres と認証 API を Docker で動かし、サーバーとクライアントが使う URL とキーを表示します。

```bash
supabase start
```

プロフィールは同じ Postgres インスタンスにゲーム専用のテーブルとして保存します。PowerShell では次のように作成します。

```powershell
Get-Content deploy/db/schema.sql | docker exec -i supabase_db_SharpCampus psql -U postgres -d postgres
```

POSIX シェルでは次のように作成します。

```bash
docker exec -i supabase_db_SharpCampus psql -U postgres -d postgres < deploy/db/schema.sql
```

`deploy/db/schema.sql` はテーブルを削除してから作り直すので、もう一度実行することがデータベースの初期化になります。

続いてメタゲームサーバーを起動します。

```bash
dotnet run --project src/SharpCampus.ApiServer
```

コンソールクライアントから操作します。引数なしで実行すると REPL が起動します。

```bash
dotnet run --project src/SharpCampus.Cli
```

```text
cli> signup player@example.com hunter2
cli> login player@example.com hunter2
cli> whoami
cli> nickname boardsweeper
cli> logout
```

`signup` と `login` は Supabase に直接リクエストし、受け取ったアクセストークンをユーザープロファイルの下に保存します。
`whoami` はそのトークンを ApiServer に送り、サーバーが検証したうえでどのアカウントかを返し、あわせてプレイヤー
プロフィール（ニックネーム、コイン、レーティング）も返します。プロフィールは最初の `whoami` のときに、アカウント ID から
作ったニックネームで生成されます。`nickname` はそのニックネームを変更するコマンドです。ニックネームは英数字と
アンダースコアだけの2〜16文字で、大文字と小文字を区別せずに比較するため、他のアカウントが使っている名前は指定できません。
すべてのコマンドは単発の実行にも対応しています。たとえば `dotnet run --project src/SharpCampus.Cli -- whoami` のように使います。

使い終わったら `supabase stop` でスタックを停止します。

## ライセンス

MIT ライセンスです。[LICENSE](LICENSE) を参照してください。
