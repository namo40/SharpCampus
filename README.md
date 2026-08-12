# SharpCampus

**English** | [한국어](README.ko.md) | [日本語](README.ja.md)

SharpCampus is an educational reference game server built with [MagicOnion](https://github.com/Cysharp/MagicOnion) and the [Cysharp](https://github.com/Cysharp) library ecosystem, implementing a 1v1 console tetromino duel in the falling-block puzzle genre known from Tetris.

Everything from the game servers down to containerized deployment lives in this one codebase, and a companion learning site walks through each library against this code.

## Solution structure

| Project | Description |
| --- | --- |
| `src/SharpCampus.Shared` | Client-facing contracts: service interfaces and DTOs. Targets `netstandard2.1` so a Unity client can consume it. |
| `src/SharpCampus.Shared.Internal` | Server-to-server contracts that are never shipped to clients. |
| `src/SharpCampus.GameCore` | Game rules and simulation, free of networking and hosting concerns. Also `netstandard2.1`. |
| `src/SharpCampus.ApiServer` | Meta-game host: accounts, profiles, and matchmaking. |
| `src/SharpCampus.RoomServer` | Real-time match host running the server-authoritative tick loop. |
| `src/SharpCampus.Server.Common` | Building blocks shared by both server hosts. |
| `src/SharpCampus.Cli` | .NET console client used to play the game and to exercise the servers. |
| `tools/SharpCampus.MasterDataTool` | Command-line tool that validates the game master data sources and builds the database a deployed server loads. |
| `masterdata/` | Master data sources, one JSON file per table: game constants, gravity curve, attack and combo tables, coin payouts and rating constants, skins and missions. |
| `tests/SharpCampus.Shared.Tests` | Tests for `SharpCampus.Shared`. |
| `tests/SharpCampus.GameCore.Tests` | Tests for `SharpCampus.GameCore`. |
| `tests/SharpCampus.ApiServer.Tests` | Tests for `SharpCampus.ApiServer`. |
| `tests/SharpCampus.RoomServer.Tests` | Tests for `SharpCampus.RoomServer`. |
| `tests/SharpCampus.ServerCommon.Tests` | Tests for `SharpCampus.Server.Common`. |
| `tests/SharpCampus.MasterDataTool.Tests` | Tests for `SharpCampus.MasterDataTool`. |
| `tests/SharpCampus.LoadTest` | Load-testing harness for the servers. |

Package versions are managed centrally in `Directory.Packages.props`, and build settings shared by every project live in `Directory.Build.props`.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/), running
- [Supabase CLI](https://supabase.com/docs/guides/local-development/cli/getting-started)

## Build and test

```bash
dotnet build
dotnet test
```

The test suite is self-contained — it needs neither Docker nor a running Supabase stack.

## Quick start

Accounts live in [Supabase Auth](https://supabase.com/docs/guides/auth), so bring the local stack up first. It runs
Postgres and the auth API in Docker, and prints the URL and keys the servers and client are configured with.

```bash
supabase start
```

Profiles live in the same Postgres instance, in tables of the game's own. Create them with PowerShell:

```powershell
Get-Content deploy/db/schema.sql | docker exec -i supabase_db_SharpCampus psql -U postgres -d postgres
```

or with a POSIX shell:

```bash
docker exec -i supabase_db_SharpCampus psql -U postgres -d postgres < deploy/db/schema.sql
```

`deploy/db/schema.sql` drops the tables before recreating them, so running it again is how you reset the database.

Game balance lives in `masterdata/` as editable JSON, one file per table — tick rate, gravity curve,
attack and combo tables, coin payouts and rating constants, skins and missions. The meta-game and
match servers import those files at startup, so editing a value and restarting is all a balance
change takes. `tools/SharpCampus.MasterDataTool` checks the sources against the schema with its
`validate` command and compiles them into the single database file a container image ships with
using `build`; a local run needs neither.

Start the meta-game server:

```bash
dotnet run --project src/SharpCampus.ApiServer
```

And drive it from the console client, which starts a REPL when you pass no arguments:

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

`signup` and `login` talk to Supabase directly and store the resulting access token under your user profile;
`whoami` sends that token to the ApiServer, which verifies it and answers with the account behind it plus its
player profile — nickname, coins and rating. The first `whoami` is what creates that profile, under a nickname
derived from the account id. `nickname` renames it: 2 to 16 letters, digits or underscores, unique across accounts
and compared case-insensitively. Every command also works as a one-shot invocation, for example
`dotnet run --project src/SharpCampus.Cli -- whoami`.

Stop the stack with `supabase stop` when you are done.

## License

MIT. See [LICENSE](LICENSE).
