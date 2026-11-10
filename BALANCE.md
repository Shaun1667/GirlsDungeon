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
| 1-9 | 방마다 막는 엄폐물(상자 · 기둥) | 2~4 | `minCoverObjectsPerRoom` · `maxCoverObjectsPerRoom` | 방마다 숨을 곳 (2026-11-10, Claude 정함 · 확인 필요) |
| 1-10 | 엄폐물 중 기둥 비율 | 30% | `pillarPercent` | 나머지는 상자 |
| 1-11 | 방마다 낮은 벽 | 1~2개 | `minLowWallsPerRoom` · `maxLowWallsPerRoom` | |
| 1-12 | 낮은 벽 길이 | 2~3칸 | `minLowWallLength` · `maxLowWallLength` | |
| 1-13 | 꾸미기 시도 | 방마다 30 | `decorAttempts` | 재시도 상한 |
| 1-14 | 엄폐물 위치 규칙 | 방 테두리 줄 제외, 다른 엄폐물 · 시작 · 계단과 8칸 이웃 금지, 놓을 때마다 연결 검사 | (코드) `FloorDecor` | 길이 막히지 않게 |

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
| 3-4 | 카메라 회전 단위 | ~~90도~~ → 30도 (2026-11-10, 사용자) | `cameraYawStep` |
| 3-5 | 카메라 거리 | 17 | `cameraDistance` |
| 3-6 | 따라가기 · 회전 빠르기 | 8 · 10 | `cameraFollowSharpness` · `cameraRotateSharpness` |
| 3-7 | 낮은 엄폐물 높이 (상자 · 낮은 벽) | 0.55 | `lowCoverHeight` |
| 3-8 | 상자 · 기둥 크기 | 0.8 · 0.7 | `crateSize` · `pillarSize` |
| 3-9 | 낮은 벽 두께 | 0.14 | `lowWallThickness` |

## 4. 유닛 능력치 (결정 2026-11-10, 사용자 — 출발값)
| 번호 | 유닛 | 체력 | 안정지수 | 시야 | 필드 |
|---|---|---|---|---|---|
| 4-1 | 플레이어 | 20 | 6 | 9칸 | `units[Player]` |
| 4-2 | 사격병 | 10 | 3 | 9칸 | `units[Rifleman]` |

시야 9칸은 Claude가 정함 (확인 필요). 칸 가운데 사이 직선 거리이고, 벽 · 기둥에 가리면 보이지 않는다.

## 5. 전투
| 번호 | 항목 | 값 | 필드 | 근거 |
|---|---|---|---|---|
| 5-1 | 층당 적 수 | 4~6 | `minEnemiesPerFloor` · `maxEnemiesPerFloor` | 사용자 결정 |
| 5-2 | 적과 시작 칸 최소 거리 | 8칸 (가로 · 세로 · 대각 중 큰 값) | `enemyMinDistanceFromStart` | 구조 문서 출발값. 시작 칸에서 보이는 칸에도 놓지 않음 |
| 5-3 | 맞을 때 안정지수 감소 | 1 | `stabilityLossPerHit` | 사용자 결정 (2-3에서 사용) |
| 5-4 | 전투 끝 안정지수 | 가득 참 | (코드) `CombatFlow` | 사용자 결정 |
| 5-5 | 한 라운드(턴 종료) 비용 | 1틱 | `endTurnCostTicks` | Claude 정함 (확인 필요) |
| 5-6 | 엄폐 피해 감소 | 70% (안정지수 > 0일 때) | (2-3) | 사용자 결정 |

## 6. 검산
- 코드: `BalanceTests`가 1번 · 2번 · 4번 · 5번을 에셋 값과 비교한다.
- 맵 규칙: `DungeonGeneratorTests`가 시드 300개로 방 개수 · 연결 · 테두리를 검사한다.
