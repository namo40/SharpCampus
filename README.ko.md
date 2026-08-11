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
- [Docker](https://www.docker.com/)

## 빌드와 테스트

```bash
dotnet build
dotnet test
```

## 라이선스

MIT 라이선스를 따릅니다. [LICENSE](LICENSE)를 참고하세요.
