# SharpCampus

[English](README.md) | **한국어** | [日本語](README.ja.md)

SharpCampus는 [MagicOnion](https://github.com/Cysharp/MagicOnion)과 [Cysharp](https://github.com/Cysharp) 라이브러리 생태계로 1대1 콘솔 테트로미노 대전 게임(테트리스로 잘 알려진 낙하 퍼즐 장르)을 구현한 교육용 레퍼런스 게임 서버입니다.

게임 서버부터 컨테이너 배포까지 전체가 하나의 코드베이스이며, 이 코드를 바탕으로 라이브러리를 하나씩 해설하는 [학습 사이트](https://namo40.github.io/SharpCampus/ko/)를 제공합니다.

## 솔루션 구조

| 프로젝트 | 설명 |
| --- | --- |
| `src/SharpCampus.Shared` | 클라이언트에 공개되는 계약(서비스 인터페이스와 DTO). Unity 클라이언트에서도 쓸 수 있도록 `netstandard2.1`을 타깃으로 합니다. |
| `src/SharpCampus.Shared.Internal` | 서버끼리만 주고받는 계약으로, 클라이언트에는 배포하지 않습니다. |
| `src/SharpCampus.GameCore` | 네트워크나 호스팅에 의존하지 않는 게임 규칙과 시뮬레이션. 마찬가지로 `netstandard2.1`입니다. |
| `src/SharpCampus.ApiServer` | 메타 게임 호스트로 계정, 프로필, 매치메이킹을 담당합니다. |
| `src/SharpCampus.RoomServer` | 서버 권위 틱 루프를 돌리는 실시간 대전 호스트입니다. |
| `src/SharpCampus.BotServer` | 대기열에 상대가 없을 때 투입되는 봇 호스트입니다. 봇 계정으로 로그인해 일반 클라이언트와 같은 경로로 플레이합니다. |
| `src/SharpCampus.Server.Common` | 서버 호스트들이 함께 쓰는 공통 구성 요소입니다. |
| `src/SharpCampus.Client` | 실제로 게임을 플레이하는 .NET 콘솔 클라이언트로, 전체 화면 메뉴 방식의 터미널 UI입니다. |
| `src/SharpCampus.Cli` | 서버 동작을 확인하는 .NET 콘솔 클라이언트로, 명령 하나가 서비스 호출 하나에 대응합니다. |
| `src/SharpCampus.Client.Common` | 두 클라이언트가 함께 쓰는 로그인, 세션 저장, 다국어 처리, 대전 화면 렌더링입니다. |
| `tools/SharpCampus.MasterDataTool` | 게임 마스터 데이터 원본을 검증하고, 배포된 서버가 읽는 데이터베이스 파일을 만드는 커맨드라인 도구입니다. |
| `masterdata/` | 마스터 데이터 원본으로, 테이블마다 JSON 파일이 하나씩 있습니다. 게임 상수, 중력 커브, 공격·콤보 테이블, 코인 보상과 레이팅 상수, 스킨, 미션이 들어 있습니다. |
| `tests/SharpCampus.Shared.Tests` | `SharpCampus.Shared` 테스트. |
| `tests/SharpCampus.GameCore.Tests` | `SharpCampus.GameCore` 테스트. |
| `tests/SharpCampus.ApiServer.Tests` | `SharpCampus.ApiServer` 테스트. |
| `tests/SharpCampus.RoomServer.Tests` | `SharpCampus.RoomServer` 테스트. |
| `tests/SharpCampus.BotServer.Tests` | `SharpCampus.BotServer` 테스트. |
| `tests/SharpCampus.ServerCommon.Tests` | `SharpCampus.Server.Common` 테스트. |
| `tests/SharpCampus.MasterDataTool.Tests` | `SharpCampus.MasterDataTool` 테스트. |
| `tests/SharpCampus.LoadTest` | 서버 부하 테스트 도구. |

패키지 버전은 `Directory.Packages.props`에서 중앙 관리하고, 모든 프로젝트가 공유하는 빌드 설정은 `Directory.Build.props`에 있습니다.

## 사전 준비

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (실행 중이어야 합니다)
- [Supabase CLI](https://supabase.com/docs/guides/local-development/cli/getting-started)

## 빌드와 테스트

```bash
dotnet build
dotnet test
```

테스트는 그 자체로 완결되어 있어서 Docker나 Supabase 스택이 없어도 실행됩니다. Redis 어댑터를 검증하는 테스트는
`localhost:6379`에 아무것도 없으면 스스로 건너뛰고, 아래의 컨테이너가 떠 있으면 실행됩니다.

## 빠르게 시작하기

계정은 [Supabase Auth](https://supabase.com/docs/guides/auth)가 관리하므로 로컬 스택을 먼저 띄웁니다. Postgres와 인증 API를
Docker로 실행하고, 서버와 클라이언트가 사용하는 URL과 키를 출력합니다.

```bash
supabase start
```

매치메이킹은 대기열과 매치 티켓, 룸 서버 레지스트리를 Redis에 둡니다. Supabase 스택에는 Redis가 없으므로 따로 하나
띄웁니다.

```bash
docker run -d --name sharpcampus-redis -p 6379:6379 redis:8
```

프로필은 같은 Postgres 인스턴스 안에 게임 전용 테이블로 저장합니다. PowerShell에서는 이렇게 만듭니다.

```powershell
Get-Content deploy/db/schema.sql | docker exec -i supabase_db_SharpCampus psql -U postgres -d postgres
```

POSIX 셸에서는 이렇게 만듭니다.

```bash
docker exec -i supabase_db_SharpCampus psql -U postgres -d postgres < deploy/db/schema.sql
```

`deploy/db/schema.sql`은 테이블을 지우고 다시 만들기 때문에, 다시 실행하는 것이 곧 데이터베이스 초기화입니다.

게임 밸런스 수치는 `masterdata/` 폴더에 테이블별 JSON 파일로 들어 있습니다. 틱 레이트, 중력 커브, 공격·콤보
테이블, 코인 보상과 레이팅 상수, 스킨, 미션이 여기에 해당합니다. 메타 게임 서버와 대전 서버가 시작할 때 이
파일들을 직접 읽으므로, 값을 고치고 서버를 다시 시작하기만 하면 반영됩니다.
`tools/SharpCampus.MasterDataTool`은 `validate` 명령으로 원본이 스키마에 맞는지 검사하고,
`build` 명령으로 컨테이너 이미지에 넣을 데이터베이스 파일 하나를 만듭니다. 로컬 실행에는 둘 다 필요하지 않습니다.

이어서 메타 게임 서버를 실행합니다.

```bash
dotnet run --project src/SharpCampus.ApiServer
```

터미널을 하나 더 열어 대전 서버도 실행합니다.

```bash
dotnet run --project src/SharpCampus.RoomServer
```

터미널을 하나 더 열어 봇 서버도 실행합니다.

```bash
dotnet run --project src/SharpCampus.BotServer
```

세 서버는 클라이언트를 받는 gRPC 포트와 별개로 운영용 HTTP/1 포트를 하나씩 더 엽니다. 메타 게임 서버는 5011,
대전 서버는 5012, 봇 서버는 5013번입니다. 이 포트의 `/metrics`는 .NET 런타임과 Kestrel, MagicOnion 지표에 더해
틱 소요 시간, 진행 중인 방 수, 대기열 길이, 플레이 중인 봇 수까지 담은 Prometheus 스크레이프 대상입니다.
`/healthz`는 프로세스가 살아 있는 한 응답하고, `/readyz`는 여기에 인스턴스가 없으면 서비스할 수 없는 Redis와
Postgres 상태까지 더합니다. 봇 서버는 그 둘에 의존하지 않으므로 살아 있으면 곧 준비된 상태입니다.

서버에 부하를 걸 때는 `tests/SharpCampus.LoadTest`를 실행합니다. 컨트롤러와 워커를 한 프로세스에서 함께
띄우는 [DFrame](https://github.com/Cysharp/DFrame) 기반 부하 테스트 도구입니다.

```bash
dotnet run --project tests/SharpCampus.LoadTest
```

<http://localhost:7312>을 열면 워크로드와 가상 플레이어 수, 실행 횟수를 고르고 결과를 확인할 수 있습니다.
`DuelMatchWorkload`는 가상 플레이어끼리 실제 대전을 치릅니다. 한 번의 실행이 한 판이고, 봇 서버가 쓰는
플레이어를 그대로 돌리므로 대기열 호출과 입장 토큰, 허브, 입력까지 전부 콘솔 클라이언트와 같은 경로를 지납니다.
`LeaderboardQueryWorkload`는 레이팅 보드를 조회합니다. 둘 다 메타 게임 서버와 대전 서버, Supabase 스택,
Redis가 떠 있어야 하고, 봇 서버는 꺼 두어야 합니다. 그래야 대체 상대로 들어온 봇이 측정에 섞이지 않습니다.
이 도구가 쓰는 계정(`loadtest<N>@sharpcampus.dev`)은 봇 계정과 마찬가지로 처음 쓸 때 자동으로 가입됩니다.

콘솔 클라이언트는 둘입니다. `src/SharpCampus.Client`가 게임 본체이고, `src/SharpCampus.Cli`는 서버 API를
명령 하나에 호출 하나씩 짚어 보는 REPL입니다. 게임이 하는 호출은 전부 REPL에도 명령으로 있으니 REPL부터
봅니다. 인자 없이 실행하면 REPL이 시작됩니다.

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

`signup`과 `login`은 Supabase에 직접 요청해서 받은 액세스 토큰을 사용자 프로필 폴더에 저장합니다. `whoami`는 그 토큰을
ApiServer에 보내고, 서버는 토큰을 검증한 뒤 어떤 계정인지와 함께 플레이어 프로필(닉네임, 코인, 레이팅, 장착 스킨)을 응답합니다.
프로필은 첫 `whoami` 때 계정 ID에서 만든 닉네임으로 생성됩니다. `nickname`은 그 닉네임을 바꾸는 명령입니다. 닉네임은
영문자와 숫자, 밑줄만 써서 2~16자로 지어야 하고, 대소문자를 구분하지 않고 비교하므로 다른 계정이 이미 쓰는 이름은 쓸 수
없습니다. 모든 명령은 한 번만 실행하는 형태로도 쓸 수 있습니다. 예를 들면 `dotnet run --project src/SharpCampus.Cli -- whoami`처럼 씁니다.

`guest`는 이메일 없이 시작하는 방법입니다. Supabase가 익명으로 로그인시켜 주고, 그 계정도 다른 계정과 똑같이
플레이하고 코인을 벌고 순위에 오릅니다. `link`는 그 계정에 이메일과 비밀번호를 붙여 정식 계정으로 만드는
명령입니다. 사용자 ID가 그대로이므로 게스트로 모아 둔 코인과 레이팅, 대전 기록도 하나 빠짐없이 남습니다.
이를 위해 로컬 스택 두 곳(`supabase/config.toml`과 클러스터의 GoTrue 매니페스트) 모두 익명 로그인을 켜
두었습니다.

`skins`는 코스메틱 보드 테마 6종을 가격, 보유 여부, 장착 여부와 함께 보여줍니다. `buy`는 대전에서 번 코인으로 스킨을
사는 명령입니다. 잔액 확인과 코인 차감, 소유 기록이 단일 데이터베이스 트랜잭션으로 처리되므로, 잔액을 넘는 구매나 중복
구매는 동시에 요청해도 성립하지 않습니다. `equip`은 다음 대전부터 내 보드를 어떤 테마로 그릴지 고르는 명령입니다. 무료
테마는 모든 계정이 처음부터 갖고 있습니다.

`missions`는 일일 미션과 오늘 치른 대전이 각 미션을 얼마나 채웠는지 보여줍니다. 미션이 세는 것은 플레이한 판 수,
승수, 지운 줄 수, 보낸 가비지 줄 수, 하드 드롭 횟수입니다. 여기서 하루는 UTC 날짜라서 모든 미션은 UTC 자정에 새로
시작합니다. `claim`은 달성한 미션의 코인 보상을 받는 명령입니다. 같은 미션을 두 번 요청해도 보상은 한 번만
지급되므로, 연결이 끊긴 뒤 다시 요청해도 손해는 없습니다.

`duel`은 매치메이킹 대기열에 들어가서 상대가 나타날 때까지 상태를 확인합니다. ApiServer는 두 계정을 짝지어 부하가 가장
낮은 룸 서버에 방을 만들라고 요청하고, 각 클라이언트에 그 방의 주소와 수명이 짧은 입장 토큰을 응답합니다. 클라이언트는
그 주소로 접속하고, 대전은 콘솔에서 실시간으로 진행됩니다. 두 보드가 나란히 그려지고, 내 조각은 키보드로 조작하며,
끝나면 재대결을 물어보고, 코인과 레이팅이 양쪽 프로필에 정산됩니다. 각 보드는 그 주인이 장착한 스킨으로 그려지며, 상대
화면에서도 마찬가지입니다. 서로 다른 계정으로 로그인한 클라이언트 두 개에서 실행하면 혼자서도 한 판을 치를 수 있습니다.

대기열에 아무도 없으면 기다려도 상대가 생기지 않습니다. 그래서 설정한 시간(기본 2분, 개발 설정에서는 15초)이 지나면
ApiServer가 봇 서버에 상대를 요청합니다. 봇은 자기 계정으로 로그인해 클라이언트와 똑같은 세 번의 호출로 같은 대기열에
들어가므로, 봇을 자리에 앉히는 것도 위에서 설명한 그 매칭 과정 그대로입니다. 즉, 방도 입장 토큰도 정산도 상대가 봇이라는
사실을 알지 못합니다. 봇은 한 수 앞만 보는 휴리스틱으로 둡니다. 조각마다 놓을 수 있는 자리를 모두 점수로 매겨 보드가 가장
낮고 평평하며 빈칸을 묻지 않는 결과를 고른 다음, 거기까지 가는 입력을 R3 스트림으로 한 박자에 하나씩 보냅니다. 그래야
방이 틱마다 받아 주는 입력 한도 안에 전부 들어갑니다.

`history`는 최근에 치른 대전을 최신순으로 되짚어 줍니다. 상대가 누구였는지, 어떻게 끝났는지, 그 결과로
레이팅과 코인이 얼마나 움직였는지를 보여줍니다. 메타 게임 서버의 `IMatchHistoryService`가 이미 Postgres에
남아 있는 정산된 대전 기록에서 호출한 계정의 몫만 골라 응답하므로, 정산이 기록되는 순간 목록에 나타납니다.

`rank`는 레이팅 순위표의 상위 100위를 보여주고, 내 자리는 강조해서 표시합니다. 내가 100위 밖이면 구분선 아래에 따로
붙여 줍니다. `rank daily`는 같은 방식으로 오늘(UTC 날짜 기준) 정산된 승수 순위표를 읽습니다. 두 순위표 모두 룸 서버가
매치를 정산하면서 기록하는 Redis Sorted Set입니다. 원본은 어디까지나 PostgreSQL이라서, 순위표 기록이 실패해도 정산은
그대로 남고 다시 만들어야 할 것은 순위표뿐입니다. 일간 순위표는 키에 날짜가 들어 있고 스스로 만료되는데, 일간 초기화는
그것으로 끝입니다.

게임 본체는 다른 클라이언트입니다. 여기에는 입력할 명령이 없고, 화면 전체를 차지한 채 타이틀 화면부터
방향키와 Enter만으로 조작합니다.

```bash
dotnet run --project src/SharpCampus.Client
```

타이틀 화면에는 로그인, 회원가입, 게스트로 플레이, 종료가 있고, 그다음은 로비입니다. 로비 위쪽에는 닉네임과
코인, 레이팅, 장착 스킨이 있고 그 아래에 대전 시작, 상점, 미션, 순위, 대전 기록, 계정, 종료 메뉴가 있습니다.
대전 시작을 고르면 대기열에 들어가고(기다리는 동안 Esc를 누르면 대기열에서 빠집니다), 상대가 잡히면 REPL의
`duel`이 쓰는 것과 같은 보드 렌더러에 화면 전체를 넘깁니다. 조작 키도 그대로입니다. 대전이 끝나면 결과와
재대결 확인이 이어지고, 어느 쪽으로 답하든 로비로 돌아옵니다. 다만 터미널을 온전히 써야 하는 클라이언트라서,
입력이나 출력이 리다이렉트되어 있으면 안내만 출력하고 종료합니다.

속을 들여다보면 두 클라이언트는 하나입니다. 로그인과 세션 파일, 번역 문자열, 대전 화면 렌더링이 전부
`src/SharpCampus.Client.Common`에 있어서 어느 쪽으로 시작하든 대전은 똑같이 진행됩니다. `--lang en|ko|ja`
옵션과 서버 주소를 지정하는 `SHARPCAMPUS_SERVER` 환경 변수도 양쪽이 같습니다.

다 사용했으면 `supabase stop`으로 스택을 내리고, `docker rm -f sharpcampus-redis`로 Redis 컨테이너를 정리합니다.

## Docker로 전체 스택 띄우기

빠르게 시작하기가 소스 트리에서 서버를 직접 실행하는 방식이었다면, 이번에는 실제로 배포되는 형태 그대로 띄웁니다.
컴포즈 파일 하나가 메타 게임 서버와 대전 서버 두 대(`room-1`, `room-2`), 봇 서버를 컨테이너 이미지로 빌드하고,
그 앞에 Envoy 진입점을 세우고, 이 스택 전용 Redis까지 함께 올립니다. Supabase만은 여전히 개발자 본인의 스택이라서,
위와 같은 명령으로 `supabase start`를 실행해 두고 스키마도 적용해 둔 상태여야 합니다.

```bash
docker compose -f deploy/docker/docker-compose.yml up --build -d
docker compose -f deploy/docker/docker-compose.yml down
```

첫 빌드는 서버와 마스터 데이터 데이터베이스를 이미지 안에서 컴파일하므로 몇 분 걸립니다.

클라이언트는 5000번 포트의 진입점 하나로 모든 서버에 닿습니다. 실행하기 전에 `SHARPCAMPUS_SERVER` 환경 변수로
그 주소를 지정합니다.

```bash
SHARPCAMPUS_SERVER=http://localhost:5000 dotnet run --project src/SharpCampus.Cli -- status
```

```powershell
$env:SHARPCAMPUS_SERVER = 'http://localhost:5000'; dotnet run --project src/SharpCampus.Cli -- status
```

`room-id` 헤더가 없는 gRPC 호출은 메타 게임 서버로 갑니다. 대전 접속은 이 헤더를 달고 오는데, Envoy는 헤더에 적힌
방이 어느 서버 것인지를 내부 엔드포인트(메타 게임 서버가 Redis를 읽어 응답합니다)에 물어본 뒤, 그 방을 가진 대전
서버 인스턴스로 스트림을 넘깁니다. 대전 서버는 호스트 쪽으로 포트를 하나도 열지 않으며, `status`에서 룸 서버가
연결 불가로 나오는 것도 바로 그 때문입니다. 대기열, 대전, 미션, 상점, 순위표까지 나머지는 전부 5000번 포트 하나로
동작합니다.

지표 수집은 별도 프로파일로 분리해 두었습니다.

```bash
docker compose -f deploy/docker/docker-compose.yml --profile observability up -d
```

이렇게 하면 서버 네 대의 지표 엔드포인트를 모두 긁어 가는 Prometheus가 함께 뜹니다. 주소는
<http://localhost:19090>입니다. 흔히 쓰는 9090은 Windows가 예약해 둔 포트 범위 안에 들어가기 때문입니다.

## Kubernetes(kind)로 전체 스택 띄우기

같은 이미지와 같은 Envoy 진입점을 이번에는 노드 3개짜리 로컬 kind 클러스터에 올립니다. 이번 구성은 그 자체로
완결되어 있어서, 게임 서버 옆에 PostgreSQL과 Supabase Auth(GoTrue)까지 클러스터 안에서 함께 돌립니다. 즉, 위의
Docker 구성과 달리 개발자 본인의 `supabase start` 스택이 필요하지 않습니다. 대전 서버 두 대는 StatefulSet이
되고, 롤링 업데이트는 진행 중인 대전이 끝난 뒤에야 그 판을 돌리던 파드를 내립니다.

준비물은 이미지를 빌드하는 데만 쓰는 Docker Desktop과 [kind](https://kind.sigs.k8s.io/),
[kubectl](https://kubernetes.io/docs/tasks/tools/)입니다. 반대로 개발용 Supabase 스택은 떠 있으면 안 됩니다.
클러스터가 자체 인증 엔드포인트로 호스트의 54321번 포트를 가져가기 때문이니, 떠 있다면 먼저 `supabase stop`으로
내립니다. 그러면 콘솔 클라이언트에 박혀 있는 인증 주소가 그대로 클러스터 안의 GoTrue에 닿습니다.

```bash
docker compose -f deploy/docker/docker-compose.yml build
kind create cluster --config deploy/k8s/kind-cluster.yaml
kind load docker-image sharpcampus-apiserver sharpcampus-roomserver sharpcampus-botserver --name sharpcampus
kubectl apply -f deploy/k8s/manifests/
kubectl create configmap sharpcampus-schema --from-file=schema.sql=deploy/db/schema.sql -n sharpcampus
kubectl get pods -n sharpcampus
```

게임 스키마는 `deploy/db/schema.sql` 파일 하나로 유지하므로, 매니페스트에 복사해 넣지 않고 그 파일에서 바로
클러스터로 건네줍니다. `kubectl create configmap` 줄이 하는 일이 그것입니다. 데이터베이스 파드는 아직 없는
것을 마운트할 수 없으니 그 컨피그맵이 생길 때까지 기다렸다가, 처음 부팅할 때 스키마를 적용합니다.

모든 파드가 준비 상태가 되면, 클라이언트는 컴포즈 때와 똑같이 `SHARPCAMPUS_SERVER=http://localhost:5000`으로
접속합니다. 클러스터가 진입점을 같은 호스트 포트에 연결해 두기 때문입니다. 가입과 로그인도 그대로 됩니다. 인증
역시 클라이언트가 이미 쓰던 그 호스트 포트에서 응답하기 때문입니다.

이 구성이 흥미로워지는 지점은 롤링 업데이트입니다. `kubectl rollout restart statefulset/room -n sharpcampus`는
대전 서버를 한 대씩 교체하는데, 교체 대상이 된 서버는 그냥 멈추지 않고 드레이닝을 거칩니다. 즉, 먼저
매치메이킹에서 빠지고, 아직 돌리고 있는 대전을 전부 끝낸 다음에 종료합니다. 그동안 새 대전은 계속 남은 대전
서버로 들어가므로, 롤링 업데이트가 진행 중인 대전을 중간에 끊는 일은 없습니다.

다 사용했으면 `kind delete cluster --name sharpcampus`로 클러스터를 정리합니다.

## 라이선스

MIT 라이선스를 따릅니다. [LICENSE](LICENSE)를 참고하세요.
