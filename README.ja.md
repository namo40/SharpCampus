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
| `tools/SharpCampus.MasterDataTool` | ゲームのマスターデータ原本を検証し、デプロイ先のサーバーが読むデータベースファイルを生成するコマンドラインツールです。 |
| `masterdata/` | マスターデータの原本で、テーブルごとに JSON ファイルが1つずつあります。ゲーム定数、重力カーブ、攻撃・コンボテーブル、コイン報酬とレーティング定数、スキン、ミッションが入っています。 |
| `tests/SharpCampus.Shared.Tests` | `SharpCampus.Shared` のテスト。 |
| `tests/SharpCampus.GameCore.Tests` | `SharpCampus.GameCore` のテスト。 |
| `tests/SharpCampus.ApiServer.Tests` | `SharpCampus.ApiServer` のテスト。 |
| `tests/SharpCampus.RoomServer.Tests` | `SharpCampus.RoomServer` のテスト。 |
| `tests/SharpCampus.ServerCommon.Tests` | `SharpCampus.Server.Common` のテスト。 |
| `tests/SharpCampus.MasterDataTool.Tests` | `SharpCampus.MasterDataTool` のテスト。 |
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

テストは単体で完結しており、Docker も Supabase スタックも必要ありません。Redis アダプターを検証するテストは
`localhost:6379` で何も待ち受けていなければ自動でスキップし、下記のコンテナが起動していれば実行されます。

## クイックスタート

アカウントは [Supabase Auth](https://supabase.com/docs/guides/auth) が管理するので、まずローカルスタックを起動します。
Postgres と認証 API を Docker で動かし、サーバーとクライアントが使う URL とキーを表示します。

```bash
supabase start
```

マッチメイキングは待ち行列とマッチチケット、ルームサーバーのレジストリを Redis に置きます。Supabase スタックには
Redis が含まれないので、別途一つ起動します。

```bash
docker run -d --name sharpcampus-redis -p 6379:6379 redis:8
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

ゲームバランスの数値は `masterdata/` にテーブルごとの JSON ファイルとして置いてあります（ティックレート、重力カーブ、
攻撃・コンボテーブル、コイン報酬とレーティング定数、スキン、ミッション）。メタゲームサーバーと対戦サーバーが起動時に
これらのファイルを直接読み込むので、値を書き換えてサーバーを起動し直せば反映されます。
`tools/SharpCampus.MasterDataTool` は `validate` コマンドで原本がスキーマに
合っているかを確認し、`build` コマンドでコンテナイメージに載せるデータベースファイルを一つ生成します。ローカル実行では
どちらも不要です。

続いてメタゲームサーバーを起動します。

```bash
dotnet run --project src/SharpCampus.ApiServer
```

もう一つターミナルを開いて対戦サーバーも起動します。

```bash
dotnet run --project src/SharpCampus.RoomServer
```

コンソールクライアントから両サーバーを操作します。引数なしで実行すると REPL が起動します。

```bash
dotnet run --project src/SharpCampus.Cli
```

```text
cli> signup player@example.com hunter2
cli> login player@example.com hunter2
cli> whoami
cli> nickname boardsweeper
cli> duel
cli> logout
```

`signup` と `login` は Supabase に直接リクエストし、受け取ったアクセストークンをユーザープロファイルの下に保存します。
`whoami` はそのトークンを ApiServer に送り、サーバーが検証したうえでどのアカウントかを返し、あわせてプレイヤー
プロフィール（ニックネーム、コイン、レーティング）も返します。プロフィールは最初の `whoami` のときに、アカウント ID から
作ったニックネームで生成されます。`nickname` はそのニックネームを変更するコマンドです。ニックネームは英数字と
アンダースコアだけの2〜16文字で、大文字と小文字を区別せずに比較するため、他のアカウントが使っている名前は指定できません。
すべてのコマンドは単発の実行にも対応しています。たとえば `dotnet run --project src/SharpCampus.Cli -- whoami` のように使います。

`duel` はマッチメイキングの待ち行列に入り、相手が現れるまで状態を問い合わせ続けます。ApiServer は二つのアカウントを
組み合わせ、最も負荷の低いルームサーバーに部屋を用意するよう依頼し、各クライアントにその部屋のアドレスと寿命の短い
入場トークンを返します。クライアントはそのアドレスに接続し、ランダムな入力で対戦を最後まで進めます。別々のアカウントで
ログインしたクライアント二つから実行すると一試合を通して見られます。実際に遊べるクライアントができるまでの暫定的な
コマンドです。

使い終わったら `supabase stop` でスタックを停止し、`docker rm -f sharpcampus-redis` で Redis コンテナを片付けます。

## ライセンス

MIT ライセンスです。[LICENSE](LICENSE) を参照してください。
