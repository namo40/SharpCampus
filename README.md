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
| `tools/SharpCampus.MasterDataTool` | Command-line tool for building game master data. |
| `tests/SharpCampus.GameCore.Tests` | Tests for `SharpCampus.GameCore`. |
| `tests/SharpCampus.ApiServer.Tests` | Tests for `SharpCampus.ApiServer`. |
| `tests/SharpCampus.RoomServer.Tests` | Tests for `SharpCampus.RoomServer`. |
| `tests/SharpCampus.ServerCommon.Tests` | Tests for `SharpCampus.Server.Common`. |
| `tests/SharpCampus.LoadTest` | Load-testing harness for the servers. |

Package versions are managed centrally in `Directory.Packages.props`, and build settings shared by every project live in `Directory.Build.props`.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/)

## Build and test

```bash
dotnet build
dotnet test
```

## License

MIT. See [LICENSE](LICENSE).
