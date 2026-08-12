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
| `tools/SharpCampus.MasterDataTool` | 게임 마스터 데이터를 생성하는 커맨드라인 도구입니다. |
| `tests/SharpCampus.GameCore.Tests` | `SharpCampus.GameCore` 테스트. |
| `tests/SharpCampus.ApiServer.Tests` | `SharpCampus.ApiServer` 테스트. |
| `tests/SharpCampus.RoomServer.Tests` | `SharpCampus.RoomServer` 테스트. |
| `tests/SharpCampus.ServerCommon.Tests` | `SharpCampus.Server.Common` 테스트. |
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

테스트는 그 자체로 완결되어 있어서 Docker나 Supabase 스택이 없어도 실행됩니다.

## 빠르게 시작하기

계정은 [Supabase Auth](https://supabase.com/docs/guides/auth)가 관리하므로 로컬 스택을 먼저 띄웁니다. Postgres와 인증 API를
Docker로 실행하고, 서버와 클라이언트가 사용하는 URL과 키를 출력합니다.

```bash
supabase start
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

그다음 메타 게임 서버를 실행합니다.

```bash
dotnet run --project src/SharpCampus.ApiServer
```

콘솔 클라이언트로 서버를 조작합니다. 인자 없이 실행하면 REPL이 시작됩니다.

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

`signup`과 `login`은 Supabase에 직접 요청해서 받은 액세스 토큰을 사용자 프로필 폴더에 저장합니다. `whoami`는 그 토큰을
ApiServer에 보내고, 서버는 토큰을 검증한 뒤 어떤 계정인지와 함께 플레이어 프로필(닉네임, 코인, 레이팅)을 응답합니다.
프로필은 첫 `whoami` 때 계정 ID에서 만든 닉네임으로 생성됩니다. `nickname`은 그 닉네임을 바꾸는 명령입니다. 닉네임은
영문자와 숫자, 밑줄만 써서 2~16자로 지어야 하고, 대소문자를 구분하지 않고 비교하므로 다른 계정이 이미 쓰는 이름은 쓸 수
없습니다. 모든 명령은 한 번만 실행하는 형태로도 쓸 수 있습니다. 예를 들면 `dotnet run --project src/SharpCampus.Cli -- whoami`처럼 씁니다.

다 사용했으면 `supabase stop`으로 스택을 내립니다.

## 라이선스

MIT 라이선스를 따릅니다. [LICENSE](LICENSE)를 참고하세요.
