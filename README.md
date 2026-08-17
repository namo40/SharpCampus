# SharpCampus

**English** | [한국어](README.ko.md) | [日本語](README.ja.md)

SharpCampus is an educational reference game server built with [MagicOnion](https://github.com/Cysharp/MagicOnion) and the [Cysharp](https://github.com/Cysharp) library ecosystem, implementing a 1v1 console tetromino duel in the falling-block puzzle genre known from Tetris.

Everything from the game servers down to containerized deployment lives in this one codebase, and a companion [learning site](https://namo40.github.io/SharpCampus/) walks through each library against this code.

## Solution structure

| Project | Description |
| --- | --- |
| `src/SharpCampus.Shared` | Client-facing contracts: service interfaces and DTOs. Targets `netstandard2.1` so a Unity client can consume it. |
| `src/SharpCampus.Shared.Internal` | Server-to-server contracts that are never shipped to clients. |
| `src/SharpCampus.GameCore` | Game rules and simulation, free of networking and hosting concerns. Also `netstandard2.1`. |
| `src/SharpCampus.ApiServer` | Meta-game host: accounts, profiles, and matchmaking. |
| `src/SharpCampus.RoomServer` | Real-time match host running the server-authoritative tick loop. |
| `src/SharpCampus.BotServer` | Fallback bot host: signs bots in and plays them through the ordinary client path. |
| `src/SharpCampus.Server.Common` | Building blocks shared by the server hosts. |
| `src/SharpCampus.Client` | .NET console client the game is played in: a fullscreen, menu-driven terminal UI. |
| `src/SharpCampus.Cli` | .NET console client that exercises the servers, one command per service call. |
| `src/SharpCampus.Client.Common` | Sign-in, session storage, localization and duel rendering, shared by both clients. |
| `tools/SharpCampus.MasterDataTool` | Command-line tool that validates the game master data sources and builds the database a deployed server loads. |
| `masterdata/` | Master data sources, one JSON file per table: game constants, gravity curve, attack and combo tables, coin payouts and rating constants, skins and missions. |
| `tests/SharpCampus.Shared.Tests` | Tests for `SharpCampus.Shared`. |
| `tests/SharpCampus.GameCore.Tests` | Tests for `SharpCampus.GameCore`. |
| `tests/SharpCampus.ApiServer.Tests` | Tests for `SharpCampus.ApiServer`. |
| `tests/SharpCampus.RoomServer.Tests` | Tests for `SharpCampus.RoomServer`. |
| `tests/SharpCampus.BotServer.Tests` | Tests for `SharpCampus.BotServer`. |
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

The test suite is self-contained — it needs neither Docker nor a running Supabase stack. The tests that
exercise the Redis adapters skip themselves when nothing is listening on `localhost:6379`, and run when
the container below is up.

## Quick start

Accounts live in [Supabase Auth](https://supabase.com/docs/guides/auth), so bring the local stack up first. It runs
Postgres and the auth API in Docker, and prints the URL and keys the servers and client are configured with.

```bash
supabase start
```

Matchmaking keeps its queue, its match tickets and the room server registry in Redis, which the Supabase
stack does not include. Run one alongside it:

```bash
docker run -d --name sharpcampus-redis -p 6379:6379 redis:8
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

and the match server, in a second terminal:

```bash
dotnet run --project src/SharpCampus.RoomServer
```

and the bot server, in a third:

```bash
dotnet run --project src/SharpCampus.BotServer
```

Each of them opens an HTTP/1 port for operations beside the gRPC one it serves clients on — 5011 for the
meta-game server, 5012 for the match server and 5013 for the bot server. `/metrics` on that port is a
Prometheus scrape carrying the .NET runtime, Kestrel and MagicOnion counters along with this game's own —
tick duration, live rooms, queue depth and bots in play; `/healthz` answers for as long as the process
does, and `/readyz` adds the Redis and Postgres an instance cannot serve without. The bot server has
neither behind it, so it is ready as soon as it is alive.

Put them under load with `tests/SharpCampus.LoadTest`, a [DFrame](https://github.com/Cysharp/DFrame)
harness that hosts its controller and one worker in a single process:

```bash
dotnet run --project tests/SharpCampus.LoadTest
```

Open <http://localhost:7312> to choose a workload, how many virtual players run it and how many times,
and to read what came back. `DuelMatchWorkload` plays whole matches between virtual players — one run
of it is one match, played by the bot server's own player, so the queue calls, the entry token, the hub
and the inputs are a console client's — and `LeaderboardQueryWorkload` reads the rating board. Both
want the meta-game server, the match server, the Supabase stack and Redis up, and the bot server
stopped, so that no fallback opponent joins what you are measuring. The accounts the harness plays as
(`loadtest<N>@sharpcampus.dev`) are signed up on first use, as the bots' own are.

Two console clients connect to them: `src/SharpCampus.Client` is the game, and `src/SharpCampus.Cli`
is a REPL that walks the server API one command per call. The REPL comes first here, because every
call the game makes is a command in it. It starts when you pass no arguments:

```bash
dotnet run --project src/SharpCampus.Cli
```

```text
cli> signup player@example.com hunter2
cli> login player@example.com hunter2
cli> guest
cli> link player@example.com hunter2
cli> whoami
cli> nickname boardsweeper
cli> skins
cli> buy mono
cli> equip mono
cli> missions
cli> claim win_1
cli> duel
cli> history
cli> rank
cli> rank daily
cli> logout
```

`signup` and `login` talk to Supabase directly and store the resulting access token under your user profile;
`whoami` sends that token to the ApiServer, which verifies it and answers with the account behind it plus its
player profile — nickname, coins, rating and equipped skin. The first `whoami` is what creates that profile, under a nickname
derived from the account id. `nickname` renames it: 2 to 16 letters, digits or underscores, unique across accounts
and compared case-insensitively. Every command also works as a one-shot invocation, for example
`dotnet run --project src/SharpCampus.Cli -- whoami`.

`guest` is the way in without an email: Supabase signs the client in anonymously, and the account
behind it plays, earns and ranks like any other. `link` makes that same account permanent by
attaching an email and a password to it — the user id never changes, so the coins, rating and match
history a guest ran up carry over untouched. Both local stacks have anonymous sign-ins switched on
for it, in `supabase/config.toml` and in the cluster's GoTrue manifest.

`skins` lists the six cosmetic board themes with their coin prices, what you already own and what you
are wearing. `buy` spends match winnings on one — the balance check, the coin debit and the ownership
row settle in a single database transaction, so neither an overdraft nor a double purchase can slip
through — and `equip` picks the theme your boards are drawn in from the next match on. The free theme
is what every account starts with.

`missions` lists the daily missions and how far today's matches have carried you towards each one — matches
played, wins, lines cleared, garbage sent and hard drops. A day here is a UTC date, so every mission starts
over at midnight UTC. `claim` takes the coins a finished mission pays; claiming the same mission a second
time pays nothing extra, so a retry after a dropped connection costs you nothing.

`duel` joins the matchmaking queue and polls until an opponent turns up. The ApiServer pairs the two
accounts, asks the least loaded room server to stand up a room for them, and answers each client with
that room's address and a short-lived entry token; the client connects there and the match plays out
live in the console: both boards side by side, your pieces under keyboard control, a rematch offer
when it ends, and coins and rating settled to both profiles. Each board is drawn in its owner's
equipped skin, on both screens. Run it from two clients signed in as different accounts to play
against yourself.

With nobody else queueing there would be nothing to wait for. Past a configured wait — two minutes,
and fifteen seconds under the development settings — the ApiServer asks the bot server for an
opponent. The bot signs in to an account of its own and joins the same queue through the same three
calls a client makes, so the pairing pass that seats it is the one above, unchanged: nothing about
rooms, entry tokens or settlement knows a bot is involved. It plays a one-move heuristic — for each
piece it scores every drop that piece can reach, takes the one leaving the board lowest, flattest
and free of buried cells, and streams the inputs that get there through R3, one per beat so the
room's per-tick input allowance takes all of them.

`history` reads your recent matches back, newest first: who you played, how it ended and what it
moved your rating and coins by. `IMatchHistoryService` on the meta-game server answers it from the
settled matches Postgres already holds, scoped to the caller, so a match shows up there as soon as
settlement writes it.

`rank` lists the first hundred places of the rating board with your own row highlighted, and adds it
under a break when you sit further down than the hundredth. `rank daily` reads the same board for the
wins settled today, by UTC date. Both are Redis sorted sets the room server writes to as each match
settles — Postgres stays the source of truth, so a push that never lands leaves a board to rebuild
rather than a payout to recover. The daily board carries its date in the key and expires on its own,
which is the whole of the daily reset.

The game itself is the other client, and there is nothing to type in it: it takes the whole terminal
and runs on the arrow keys and Enter from the title screen on.

```bash
dotnet run --project src/SharpCampus.Client
```

It opens on Log in, Sign up, Play as guest and Exit, then settles into a lobby that carries your
nickname, coins, rating and equipped skin above a menu of Play a duel, Shop, Missions, Rankings,
Match history, Account and Exit. Playing a duel queues you up — Esc leaves the queue while you are
still waiting — and hands the whole screen to the board renderer the REPL's `duel` draws, under the
same keys; the result and the rematch offer follow the match, and answering it either way returns
you to the lobby. It wants a terminal to itself: with input or output redirected it prints a notice
and exits.

Underneath they are one client. `src/SharpCampus.Client.Common` holds the sign-in, the session file,
the localized strings and the duel rendering stack, so a match plays out identically whichever one
you started it from, and both take the same `--lang en|ko|ja` option and read the same
`SHARPCAMPUS_SERVER` variable for the server address.

Stop the stack with `supabase stop` and the Redis container with `docker rm -f sharpcampus-redis` when
you are done.

## Full stack in Docker

The Quick start runs the servers out of the source tree; this is the shape they deploy in. A single
compose file builds the meta-game server, two match server instances (`room-1` and `room-2`) and the
bot server into container images, puts an Envoy entry point in front of them and brings up a Redis of
the stack's own. Supabase stays your own stack: `supabase start` has to be running with the schema
applied, by the same commands as above.

```bash
docker compose -f deploy/docker/docker-compose.yml up --build -d
docker compose -f deploy/docker/docker-compose.yml down
```

The first build compiles the servers and the master data database into the images, so it takes a few
minutes.

The client reaches everything through the entry point on port 5000, which the `SHARPCAMPUS_SERVER`
environment variable points it at:

```bash
SHARPCAMPUS_SERVER=http://localhost:5000 dotnet run --project src/SharpCampus.Cli -- status
```

```powershell
$env:SHARPCAMPUS_SERVER = 'http://localhost:5000'; dotnet run --project src/SharpCampus.Cli -- status
```

A gRPC call that carries no `room-id` header goes to the meta-game server. A duel connection carries
one, and Envoy looks that room up — through an internal endpoint the meta-game server answers off
Redis — to route the stream to the match server instance that owns it. The match servers publish no
port to the host at all, which is precisely why `status` reports the room server as unreachable;
everything else — queueing, duels, missions, the shop and the rankings — works through port 5000
alone.

Metrics are a profile of their own:

```bash
docker compose -f deploy/docker/docker-compose.yml --profile observability up -d
```

That adds a Prometheus scraping all four servers' metric endpoints, published on
<http://localhost:19090> — the customary 9090 falls inside a port range Windows reserves.

## The full stack on Kubernetes (kind)

The same images behind the same Envoy entry point, this time on a local three-node kind cluster — and
this one is fully self-contained: a PostgreSQL and a Supabase Auth (GoTrue) of its own run in the
cluster beside the servers, so unlike the compose stack above nothing here needs your own
`supabase start` stack. The two match servers become a StatefulSet, and a rolling update lets a live
match finish before the pod playing it goes down.

It wants Docker Desktop, this time only to build the images, plus [kind](https://kind.sigs.k8s.io/)
and [kubectl](https://kubernetes.io/docs/tasks/tools/). What it does not want is your own Supabase
dev stack running: the cluster claims host port 54321 for its own auth endpoint, so `supabase stop`
first if it is up. The console client then reaches the in-cluster GoTrue at the auth address compiled
into it, unchanged.

```bash
docker compose -f deploy/docker/docker-compose.yml build
kind create cluster --config deploy/k8s/kind-cluster.yaml
kind load docker-image sharpcampus-apiserver sharpcampus-roomserver sharpcampus-botserver --name sharpcampus
kubectl apply -f deploy/k8s/manifests/
kubectl create configmap sharpcampus-schema --from-file=schema.sql=deploy/db/schema.sql -n sharpcampus
kubectl get pods -n sharpcampus
```

The game schema stays the single file `deploy/db/schema.sql`, so the `kubectl create configmap` line
hands it to the cluster from that file rather than from a copy pasted into a manifest. The database
pod cannot mount what is not there yet, so it waits for that configmap and applies the schema on its
first boot.

Once every pod reports ready, the client connects exactly as it did under compose, with
`SHARPCAMPUS_SERVER=http://localhost:5000` — the cluster maps the entry point onto that same host
port. Signing up and logging in work unchanged too, because auth answers on the host port the client
already uses.

What makes this shape worth running is what a rolling update does to it.
`kubectl rollout restart statefulset/room -n sharpcampus` replaces the match servers one at a time,
and a server being replaced drains rather than stops: it leaves matchmaking first, then plays out the
duels it is still running before it exits. New matches keep landing on the other match server
meanwhile, so a rolling update never cuts a live match short.

Tear the cluster back down with `kind delete cluster --name sharpcampus`.

## License

MIT. See [LICENSE](LICENSE).
