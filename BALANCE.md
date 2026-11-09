# BALANCE.md — 수치 문서

> 재미 검증 전 출발값이다. 값을 바꾸면 이 표, 데이터 에셋, `Tests/EditMode/BalanceTests.cs`를 함께 고친다.

## 0. 데이터 에셋 위치
| 수치 | 에셋 | 코드 |
|---|---|---|
| 판 · 맵 · 이동 · 화면 | `Assets/_Game/Data/GameBalance.asset` | `Game.GameBalance` |

## 1. 맵 (결정 2026-11-09, 사용자: 32 × 32, 방 6~8개)
| 번호 | 항목 | 값 | 필드 | 근거 |
|---|---|---|---|---|
| 1-1 | 맵 크기 | 32 × 32 | `mapWidth` · `mapHeight` | 한 층 2~4분 목표 |
| 1-2 | 방 개수 | 6~8 | `minRooms` · `maxRooms` | 사용자 결정 |
| 1-3 | 방 한 변 | 7~13칸 | `minRoomSize` · `maxRoomSize` | 엄폐 전투가 들어갈 크기 |
| 1-4 | 방 사이 최소 간격 | 1칸 | `roomMargin` | 벽 1칸 |
| 1-5 | 방 놓기 시도 | 판당 300 | `roomAttempts` | 재시도 상한 |
| 1-6 | 배치 다시 하기 | 50 | `layoutAttempts` | 재시도 상한 |
| 1-7 | 복도가 벽을 뚫는 비용 | 5 (기존 길 1) | `corridorWallCost` | 기존 길 재활용 |
| 1-8 | 추가 복도 (고리) | 2 | `extraConnections` | 돌아갈 길 |

## 2. 이동
| 번호 | 항목 | 값 | 필드 |
|---|---|---|---|
| 2-1 | 한 걸음 비용 | 1틱 | `moveCostTicks` |
| 2-2 | 한 걸음 화면 시간 | 0.14초 | `stepSeconds` |

## 3. 화면 (규칙에 영향 없음)
| 번호 | 항목 | 값 | 필드 |
|---|---|---|---|
| 3-1 | 벽 높이 | 1.2 | `wallHeight` |
| 3-2 | 카메라 내려다보는 각도 | 52도 | `cameraPitch` |
| 3-3 | 카메라 시작 방향 | 45도 | `cameraStartYaw` |
| 3-4 | 카메라 회전 단위 | 90도 | `cameraYawStep` |
| 3-5 | 카메라 거리 | 17 | `cameraDistance` |
| 3-6 | 따라가기 · 회전 빠르기 | 8 · 10 | `cameraFollowSharpness` · `cameraRotateSharpness` |

## 4. 검산
- 코드: `BalanceTests`가 1번 · 2번을 에셋 값과 비교한다.
- 맵 규칙: `DungeonGeneratorTests`가 시드 300개로 방 개수 · 연결 · 테두리를 검사한다.
