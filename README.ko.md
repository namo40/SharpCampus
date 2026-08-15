# SharpCampus

[English](README.md) | **한국어** | [日本語](README.ja.md)

SharpCampus는 [MagicOnion](https://github.com/Cysharp/MagicOnion)과 [Cysharp](https://github.com/Cysharp) 라이브러리 생태계로 1대1 콘솔 테트로미노 대전 게임(테트리스로 잘 알려진 낙하 퍼즐 장르)을 구현한 교육용 레퍼런스 게임 서버입니다.

게임 서버부터 컨테이너 배포까지 전체가 하나의 코드베이스이며, 이 코드를 바탕으로 라이브러리를 하나씩 해설하는 학습 사이트를 제공합니다.

## 솔루션 구조

| 프로젝트 | 설명 |
| --- | --- |
| `src/SharpCampus.Shared` | 클라이언트에 공개되는 계약(서비스 인터페이스와 DTO). Unity 클라이언트에서도 쓸 수 있도록 `netstandard2.1`을 타깃으로 합니다. |
| `src/SharpCampus.Shared.Internal` | 서버끼리만 주고받는 계약으로, 클라이언트에는 배포하지 않습니다. |
| `src/SharpCampus.GameCore` | 네트워크나 호스팅에 의존하지 않는 게임 규칙과 시뮬레이션. 마찬가지로 `netstandard2.1`입니다. |
| `src/SharpCampus.ApiServer` | 메타 게임 호스트로 계정, 프로필, 매치메이킹을 담당합니다. |
| `src/SharpCampus.RoomServer` | 서버 권위 틱 루프를 돌리는 실시간 대전 호스트입니다. |
| `src/SharpCampus.Server.Common` | 두 서버 호스트가 함께 쓰는 공통 구성 요소입니다. |
| `src/SharpCampus.Cli` | 게임을 플레이하고 서버를 검증하는 데 쓰는 .NET 콘솔 클라이언트입니다. |
| `tools/SharpCampus.MasterDataTool` | 게임 마스터 데이터 원본을 검증하고, 배포된 서버가 읽는 데이터베이스 파일을 만드는 커맨드라인 도구입니다. |
| `masterdata/` | 마스터 데이터 원본으로, 테이블마다 JSON 파일이 하나씩 있습니다. 게임 상수, 중력 커브, 공격·콤보 테이블, 코인 보상과 레이팅 상수, 스킨, 미션이 들어 있습니다. |
| `tests/SharpCampus.Shared.Tests` | `SharpCampus.Shared` 테스트. |
| `tests/SharpCampus.GameCore.Tests` | `SharpCampus.GameCore` 테스트. |
| `tests/SharpCampus.ApiServer.Tests` | `SharpCampus.ApiServer` 테스트. |
| `tests/SharpCampus.RoomServer.Tests` | `SharpCampus.RoomServer` 테스트. |
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

콘솔 클라이언트로 두 서버를 조작합니다. 인자 없이 실행하면 REPL이 시작됩니다.

```bash
dotnet run --project src/SharpCampus.Cli
```

```text
cli> signup player@example.com hunter2
cli> login player@example.com hunter2
cli> whoami
cli> nickname boardsweeper
cli> skins
cli> buy mono
cli> equip mono
cli> missions
cli> claim win_1
cli> duel
cli> rank
cli> rank daily
cli> logout
```

`signup`과 `login`은 Supabase에 직접 요청해서 받은 액세스 토큰을 사용자 프로필 폴더에 저장합니다. `whoami`는 그 토큰을
ApiServer에 보내고, 서버는 토큰을 검증한 뒤 어떤 계정인지와 함께 플레이어 프로필(닉네임, 코인, 레이팅, 장착 스킨)을 응답합니다.
프로필은 첫 `whoami` 때 계정 ID에서 만든 닉네임으로 생성됩니다. `nickname`은 그 닉네임을 바꾸는 명령입니다. 닉네임은
영문자와 숫자, 밑줄만 써서 2~16자로 지어야 하고, 대소문자를 구분하지 않고 비교하므로 다른 계정이 이미 쓰는 이름은 쓸 수
없습니다. 모든 명령은 한 번만 실행하는 형태로도 쓸 수 있습니다. 예를 들면 `dotnet run --project src/SharpCampus.Cli -- whoami`처럼 씁니다.

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

`rank`는 레이팅 순위표의 상위 100위를 보여주고, 내 자리는 강조해서 표시합니다. 내가 100위 밖이면 구분선 아래에 따로
붙여 줍니다. `rank daily`는 같은 방식으로 오늘(UTC 날짜 기준) 정산된 승수 순위표를 읽습니다. 두 순위표 모두 룸 서버가
매치를 정산하면서 기록하는 Redis Sorted Set입니다. 원본은 어디까지나 PostgreSQL이라서, 순위표 기록이 실패해도 정산은
그대로 남고 다시 만들어야 할 것은 순위표뿐입니다. 일간 순위표는 키에 날짜가 들어 있고 스스로 만료되는데, 일간 초기화는
그것으로 끝입니다.

다 사용했으면 `supabase stop`으로 스택을 내리고, `docker rm -f sharpcampus-redis`로 Redis 컨테이너를 정리합니다.

## 라이선스

MIT 라이선스를 따릅니다. [LICENSE](LICENSE)를 참고하세요.
