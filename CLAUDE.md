# CLAUDE.md

## 프로젝트 개요
GirlsDungeon — 오리지널 소녀 캐릭터 한 명을 조종하는 3D 전술 던전 크롤러. 액스컴류의 엄폐 · 측면 · 명중 확률 전투를 로그라이크 던전과 합친다. 기획은 GDD.md, 작업 순서와 일지는 TASKS.md, 수치는 BALANCE.md(첫 수치가 생길 때 만든다).
- **새 세션을 시작하면** 이 문서 → TASKS.md 맨 위 "진행 요약" 순서로 읽고 다음 항목을 확인한 뒤 시작한다. 다른 PC면 Docs/진행상황.md도. "다음 작업: 미정"이면 후보를 먼저 묻는다.
- 수치는 BALANCE.md와 데이터 에셋 기준이며 코드에 임의로 넣지 않는다.

## 참조 문서
| 문서 | 역할 | 상태 |
|---|---|---|
| `GDD.md` | 기획 | 초안 (2026-11-09) |
| `TASKS.md` | 진행 요약 · 작업 순서 · 일지 | 사용 중 |
| `BALANCE.md` | 수치 | 사용 중 (재미 검증 전 출발값) |
| `Docs/ART_ASSETS.md` | 그림 · 모델 목록 · 출처 · 라이선스, 모델 붙이는 방법 | 사용 중 (2026-11-11) |
| `Docs/Source/MY_DEV_GUIDE.md` | 작업 방식 원본 (고치지 않음) | 기준 |
| `Docs/Source/ROGUELIKE_STRUCTURE.md` | 로그라이크 구조 원본 (고치지 않음) | 기준 |

## 협업 방식
- 한국어. 1인 개발. 쉬운 말로 설명하고, 오래 걸리는 작업 중에는 지금 하는 일을 한두 줄로 알린다.
- 설계 결정은 선택지 + 추천안으로 묻고 `결정 (날짜, 사용자): … — 이유`로 TASKS에 남긴다. 사소한 것은 정하고 `정한 것 (Claude, 확인 필요)`로 보고한다.
- 한 항목 = 작업 브랜치(`phaseN-…`) 커밋 + 보고. 지시 없이 main에 커밋 · 푸시하지 않는다. "main에 병합하고 푸시" = ff 병합 · 푸시 · 브랜치 삭제.
- 보고: 한 일 · 정한 것 · 확인한 것 · 확인 못 한 것 · 확인 방법 · 커밋 상태.
- 테스트 실행은 사용자가 지시할 때만 (결정 2026-11-09, 가이드 기본값) — Claude는 작성만, "테스트 작성, 실행 안 함". 컴파일 확인 · Setup 메뉴 · 플레이 모드 확인은 Claude가 한다. 몇 항목마다 "테스트 돌릴 때 됐다"고 알린다.

## 작업 원칙
- 한 번에 기능 하나 (TASKS 현재 항목만). 요청받지 않은 시스템을 미리 만들지 않는다 — 단 핵심 설계 규칙의 구조는 처음부터.
- 구현 전 문서를 읽고 구현 후 문서를 고친다. 다르면 문서를 실제 기준으로 고치고 알린다.
- 에디터 수작업은 목록으로 안내, 반복 생성은 멱등 Setup 메뉴로.
- 완료 = 테스트 통과 + 확인 방법 + 사용자 플레이 확인. 구현만 = `— 플레이 확인 대기`.
- 실패 · 생략 · 미확인은 그대로 보고. 직접 본 것만 됐다고 말한다. 겪은 문제는 TASKS "피드백"에.

## 기술 스택
| 항목 | 값 | 상태 |
|---|---|---|
| 엔진 | Unity 6.3 LTS (6000.3.28f1), 에디터 위치 `D:\Unity\Editor` | 확정 2026-11-09, 변경 금지 |
| 렌더링 | URP 17.3 | 확정 2026-11-09, 변경 금지 |
| 입력 | Input System 1.20 (새 방식만) | 확정 2026-11-09, 변경 금지 |
| UI | uGUI | 확정 2026-11-09. 글꼴은 임시 (TASKS 0-2) |
| 그래픽 | 3D, 그레이박스 → AI 생성 에셋 | 결정 2026-11-09, 사용자 |
| 테스트 | Unity Test Framework 1.6 (EditMode) | 확정 2026-11-09 |
| 저장소 | GitHub `Shaun1667/GirlsDungeon` | 확정 |

## 폴더 구조
저장소 루트에 문서, `GirlsDungeon/`가 Unity 프로젝트다.
```
GirlsDungeon/Assets/_Game/
  Scripts/Core/    규칙 코드 — 엔진을 참조하지 않는다 (GirlsDungeon.Core)
  Scripts/Game/    엔진 어댑터 · 컴포지션 루트 · 화면 (GirlsDungeon.Game)
  Scripts/Editor/  에디터 도구 · 디버그 창 · Setup 메뉴 (GirlsDungeon.EditorTools)
  Resources/Text/  텍스트 표 text_table.csv
  Data/            데이터 에셋 (GameBalance.asset …)
  Scenes/          Game.unity (Setup 메뉴가 만든다)
  Tests/EditMode/  단위 테스트 (GirlsDungeon.Tests)
```

## 코딩 규칙
- 네임스페이스는 `GirlsDungeon.Core` · `GirlsDungeon.Game` · `GirlsDungeon.EditorTools` · `GirlsDungeon.Tests`. `Core`는 `UnityEngine`을 쓰지 않는다.
- 화면에 보이는 모든 글은 텍스트 표 한곳(`Resources/Text/text_table.csv`) — 코드 · 프리팹 · 데이터에 문장 금지, 코드는 `Text.Get(키)`, 문장을 이어 붙이지 않고 `{0}` 자리표시자. 키는 `분류.대상.용도`. 새 글 = 표 한 줄. 파싱은 `Core.Localization.TextTable`, 게임 쪽 조회는 `Game.Text` 한 곳. 씬의 고정 글은 `TextLabel`에 키만 넣는다.
- Core · Game 코드의 문자열에는 한글을 쓰지 않는다 (인스펙터 머리글 · 툴팁 포함, 테스트로 검사). 주석과 에디터 도구는 한글 가능.
- `GirlsDungeon.Game.Text`는 `UnityEngine.UI.Text`와 이름이 겹친다 → UI 컴포넌트는 항상 `UnityEngine.UI.Text`로 전체 이름을 쓴다.
- 개발자 디버그 기능은 에디터 디버그 창(`GirlsDungeon/Debug/Debug Window`)에 섹션으로, 게임 쪽 진입점은 `GameRoot.DebugXxx`, 상태를 바꾸면 [상태 변경] 표시. 디버그를 쓴 판은 `GameRoot.DebugUsed`로 표시해 기록에서 뺀다. 디버그 글은 표에 넣지 않아도 된다.
- 씬 연결은 `GameRoot` 한 곳. 씬은 Setup 메뉴(`GirlsDungeon/Setup/Build Game Scene`, `EditorTools.GameSceneSetup`)가 만들고 몇 번 실행해도 같다. 씬 구성을 바꾸면 이 메뉴를 고친다.
- 수치는 `Game.GameBalance`(`Data/GameBalance.asset`) 한 곳. 새 수치 = 필드 + BALANCE 표 + 검산 테스트.
- 난수는 `Core.Random.RandomStreams` 한 곳, 용도마다 `RandomPurpose` 하나. 전역 난수(`System.Random`, `UnityEngine.Random`)를 규칙에 쓰지 않는다. 새 용도 = enum 끝에 추가.
- 세계 상태는 `Core.Sim.World` 한 곳. 새 상태 = 필드 + `WorldSnapshot` 버전 증가 + 인코더 + 왕복 테스트.
- 규칙 진행은 `Core.Sim.Simulation.Execute(커맨드)` 한 곳. 새 행동 = `ICommand` 하나. 틱 순서는 `Simulation` 설명의 ① ~ ⑦을 따른다. 규칙 → 화면은 `EventsPublished`의 `GameEvent`로만.
- 새 판 만들기 · 다음 층은 `Core.Sim.RunFactory` 한 곳(`NewRun` · `NextFloor`), 계단 · 승리 판정은 `Core.Sim.FloorFlow`(커맨드마다 `Simulation`이 부름 — 맵 수치를 받아야 동작). 층별 적 강화는 `EnemySpawner`(`world.FloorIndex`). 결과 화면 · 새 판 버튼은 `HudView` → `GameRoot`. 맵 생성은 `Core.Map.DungeonGenerator`, 칸 이동 판정과 경로는 `Core.Map.GridNavigation` 한 곳.
- 엄폐물 배치는 `Core.Map.FloorDecor` 한 곳(꾸미기 난수, 맵 모양 뒤). 시야 · 사선은 `Core.Map.Sight`, 엄폐 판정은 `Core.Map.Cover` 한 곳 — 미리보기 · 실제 · AI가 같은 함수를 쓴다. 설 수 있는 칸은 `FloorMap.IsWalkable`, 시야를 막는 칸은 `FloorMap.BlocksSight`. 새 칸 종류 = `TileType` 끝에 추가 + 이 두 함수 + `FloorView`.
- 유닛 능력치는 `Core.Sim.CombatSettings.Units` 표(에셋 `units` 배열) 한 곳. 새 적 = `UnitKind` 끝에 추가 + 표 한 줄. 적 배치는 `EnemySpawner`, 탐험 ↔ 전투 전환 · 사망은 `CombatFlow` 한 곳(커맨드마다 `Simulation`이 부름). 적 차례 행동은 `EnemyTurn.Run`.
- 사격 · 피해는 `Core.Sim.CombatRules` 한 곳(`Preview` = 미리보기, `Resolve` = 실제, 같은 계산). 전투 이동 범위 · 경로는 `CombatMovement` 한 곳(범위 표시 · 이동 · 적 AI 공용). 차례 넘기기는 `TurnFlow`, 적 행동은 `EnemyTurn`. 전투 행동 = `CombatMoveCommand` · `AttackCommand` · `EndTurnCommand` — 화면은 이 커맨드만 낸다(`MoveController`). 쏠 수 있는 적마다 선(엄폐 받음 흰색 · 없음 노랑 · 고른 적 밝은 노랑, `MoveController.RefreshTargetLines`), 적 클릭은 대상 고르기(확인 버튼)이고 확인할 때 `AttackCommand`를 낸다(`MoveController.SelectTarget` · `ConfirmAttack`). 전투 중 이동 칸 클릭은 바로 걷지 않고 이동 예약(`MoveController.PlanMove`, 화면 상태 `plannedCell` — World에 넣지 않음) + 홀로그램(`PlayerView.ShowGhost`, 재질 `Data/Materials/Hologram.mat`)이고, 선 · 미리보기는 예약 칸에서 `CombatRules.Preview(…, from)`로 계산한다. 확인하면 `CombatMoveCommand` → `AttackCommand`를 차례로 낸다(예약 칸 다시 클릭 · 대상 없이 Enter = `ConfirmMove` 이동만, 오른쪽 클릭 · Esc = 스킬 조준 → 대상 → 예약 순 취소). 스킬(수류탄 · 밀치기 · 감시)도 예약이 있으면 예약 칸으로 걸은 뒤 쓴다(`MoveController.RunAfterPlannedMove`) — 스킬 미리보기는 세계를 스냅샷으로 복제해 예약 칸으로 실제로 걸어 본 결과에서 계산한다(`PreviewWorld`, 감시 반응까지 같은 난수 → 확인 결과와 같음, 커맨드가 실행되면 버림). 탐험 중 클릭은 그대로 바로 걷는다.
- 스킬 바 2 · 3 · 4번 칸은 캐릭터마다 다르다: `Core.Sim.SkillKind`(None · Grenade · Push · Sonic), 칸은 캐릭터 줄 `CharacterStats.Skills`(에셋 `characters[].skills`, 고르지 않은 판은 `CombatSettings.DefaultSkills`), 조회 `CombatSettings.SkillSlots` · `HasSkill` — 스킬 커맨드가 확인한다. 사격 1번 · 감시 5번 · 턴 종료 Space는 공통. 화면은 `SkillBarView`(카드 `slotCard2` ~ `slotCard4`, 빈 칸은 숨김) → `MoveController.ToggleAim`(가진 스킬만). 밀치기의 화면 이름은 "넉백 공격". 새 스킬 = `SkillKind` 끝에 추가 + 규칙 한 곳 + 커맨드(HasSkill 확인) + 재사용 대기 World 필드 + `SkillBarView`의 이름 · 재사용 대기 · 조준 연결.
- 음파 공격은 `Core.Sim.SonicAttack` 한 곳(`Preview` · `Resolve` — 보이는 적 하나, 엄폐 무시 피해 · 안정 피해), 커맨드 `SonicAttackCommand`, 재사용 대기 `World.SonicCooldown`. 화면은 `MoveController` 조준 모드(`AimMode.Sonic`, 적 클릭) + `HudView.ShowSonicPreview`.
- 범위 공격(수류탄 스킬)은 `Core.Sim.AreaAttack` 한 곳(`Preview` · `Resolve`), 커맨드 `AreaAttackCommand`, 재사용 대기 `World.GrenadeCooldown`. 화면은 `MoveController` 조준 모드 + `HudView.ShowAreaPreview`.
- 경계 상태 · 감시는 `Core.Sim.Alert` 한 곳(`CheckZones` · `Wake` · `InWatch` · `React`). 전투 중 걷기는 반드시 `CombatMovement.Walk(world, settings, …)`로 — 걸음마다 감시 반응을 확인한다. `Simulation`은 전투 수치를 받아야 경계 칸을 확인한다(게임에서는 항상 넘긴다). 위협 칸 화면은 `Game.ThreatView`.
- 부서지는 엄폐물은 `Core.Sim.CoverBreak` 한 곳(`Facing` · `InArea` · `Hit`), 손상은 `FloorMap` 상자 · 낮은 벽 손상 값. 피해 계산 뒤에 깎는다. 부서지면 `CoverDestroyed` → `GameRoot`가 지형을 다시 그린다.
- 낭떠러지는 `TileType.Chasm`(걸을 수 없음 · 시야를 막지 않음 · 엄폐 없음), 배치는 `FloorDecor.PlaceChasms`(엄폐물보다 먼저). 밀치기는 `Core.Sim.Push` 한 곳(`Aim` · `Preview` · `Resolve`, 한 줄 밀림 `Slide`), 커맨드 `PushCommand`, 재사용 대기 `World.PushCooldown`. 화면은 `MoveController` 조준 모드(`AimMode.Push`) + `HudView.ShowPushPreview`. 공격으로 미는 것(돌격병 `units[].pushOnHit`)은 `CombatRules.Resolve` → `Push.Knockback`, 밀린 결과 · 낙하(적 즉사 / 플레이어 50% 피해 후 `Push.NearestOpenFloor`)는 `Push.ApplySlide` 한 곳.
- 무기는 `Core.Sim.CombatSettings.Weapons` 표(에셋 `weapons` 배열, 이름 · 규칙 글은 `nameKey` · `ruleKey`) 한 곳, 든 무기는 `World.PlayerWeapon`. 플레이어 공격 · 사거리 · 안정 피해 · 치명은 `Loadout.Apply`가 무기에서 덮어쓴다(효과 합산 한 지점 — 3-4 파츠도 여기). 무기 규칙(권총 `MoveAfterShot` · 산탄총 거리 감소 · 저격총 `NoShotAfterMove` · 기관단총 `ExtraShotsPerTurn` · 기관총 `MoveRangeBonus` · 도검 `Melee` · `IgnoresCover`)은 `CombatRules.Preview` · `AttackCommand` · `Loadout`(`CanShoot` · `CanCombatMove` · `KeepsTurnAfterShot` · `InRange`)에서. 이번 차례 사격 수는 `World.PlayerShots`. 이번 차례에 걸은 칸 수는 `World.PlayerMoveUsed`, 남은 이동 칸은 `Loadout.MoveLeft` 한 곳(범위 표시 · `CombatMoveCommand` 공용) — 행동 전에는 남은 칸만큼 여러 번 나눠 걸을 수 있다. 새 무기 = `WeaponKind` 끝에 추가 + 표 한 줄.
- 보상 · 바닥 물건은 `Core.Sim.Loot` 한 곳(파일 `LootRules.cs` — `Loot.cs`는 Unity가 컴파일 목록에 넣지 않아 이름을 바꿈): 전투 끝 보상 `DropAfterCombat`(`CombatFlow`가 부름), 보급 상자 `OpenCrate`, 줍기 `CheckPickup`(커맨드마다 `Simulation`이 부름), 교체 `Swap`. 물건은 `World.Items`, 난수는 `RandomPurpose.Item`. 보급 상자 칸은 `TileType.SupplyCrate` · `SupplyCrateOpened`, 배치는 `FloorDecor.PlaceSupplyCrates`(아이템 난수). 화면: 물건 `Game.ItemsView`, 비교 창 · 무기 표시 `HudView`, 상자 클릭 `MoveController.RequestOpenCrate`.
- 유닛 그림은 ID로만: `units[].visualId` → 그림 목록 `Game.UnitVisualCatalog`(`Data/UnitVisuals.asset`, Setup 메뉴가 줄을 만들고 넣어 둔 프리팹은 유지). 프리팹이 없으면 도형. 지금은 플레이어만(`PlayerView` — 걷는 방향 바라보기 · Animator 값: 속도 `Speed` · 엄폐 `Cover`(전투 중 서 있을 때 가장 가까운 적에 대한 `Cover.Against`, 0/1/2) · 사격 트리거 `Fire`(플레이어의 `UnitAttacked`, 걷는 중이면 다 걸은 뒤) — 값 이름은 그림 목록 줄에서). 목록 · 출처 · 라이선스는 `Docs/ART_ASSETS.md`. 개인 테스트 모델은 `Assets/_Local/`(git 제외) — 그 모델을 그림 목록에 넣은 채로 커밋하지 않는다.
- 유닛 이름은 에셋 `units[].nameKey`(텍스트 표 키), 화면에서는 `GameBalance.NameKey(kind)`.
- 고를 수 있는 캐릭터는 `Core.Sim.CombatSettings.Characters` 표(에셋 `characters` 배열: 체력 · 안정지수 · 시야 · 방어 · 이동 · 시작 무기, 이름 `nameKey` · 그림 `visualId`) 한 곳, 고른 값은 `World.PlayerCharacter`. 적용은 `RunFactory.NewRun(…, character)`(유닛 표 플레이어 줄 위에 덮어씀), 이동 기준값은 `CombatSettings.PlayerBaseMoveRange`(`Loadout.Apply`). 시작 화면은 `Game.CharacterSelectView`(판 시작 · 결과 화면 "새 판"에서 뜸) → `GameRoot.OnCharacterPicked`. 플레이어 그림은 `GameBalance.PlayerVisualId(character)`. 새 캐릭터 = `CharacterKind` 끝에 추가 + 표 한 줄 + 텍스트 표 이름.
- 플레이어 시야는 `Core.Map.Vision.Compute` 한 곳(사선 + 서 있는 방 전체). 본 칸 기록은 `World.Explored`, 갱신은 `Core.Sim.VisionSystem`(커맨드마다). 전투 시작 · 적 표시 · 적 배치 · 클릭 가능 칸이 모두 이것을 쓴다.
- 안개는 `Game.FogView`(전역 셰이더 값 `_GD_FogTex` · `_GD_FogRect` · `_GD_FogStrength`)와 `Shaders/FogOverlay.shader`. 새 지형 메시는 `FloorView.AddPart`로 만들어야 안개 재질이 덧붙는다.
- 화면은 사건을 `Game.EventPlayer`(사건 재생기, `Core.Sim.IEventSource`)를 거쳐 하나씩 받는다 — 적 차례가 걸음 · 사격 단위로 시간을 두고 보인다(기다림은 BALANCE 3-14 ~ 3-20). 규칙 상태(World)는 이미 끝난 결과이므로 화면은 위치 · 피해를 사건에서 읽고, 체력 · 결과 화면은 재생이 끝난 뒤 맞춘다. 재생 중(`IsPlaying`)에는 입력을 받지 않는다. 안개(`FogView`) · 위협 칸(`ThreatView`)만 Simulation에서 바로 받는다. 카메라는 `FocusChanged` → `CameraRig.SetTarget`.
- 화면: 적 표시 `Game.UnitsView`(보이거나 전투 중인 적만, 칸 사이를 걸음), 공격 연출 `Game.CombatFxView`(탄도 선 · 피해 숫자 · 흔들림), HUD `Game.HudView`, 스킬 바 `Game.SkillBarView` + 카드 `SkillCardView`(아래 가운데: 사격 1 · 수류탄 2 · 밀치기 3 · 감시 4 · 턴 종료 Space, 재사용 대기 · 설명 창). UI 입력은 EventSystem + `InputSystemUIInputModule`.
- 칸 ↔ 3D 위치 변환은 `Game.GridSpace` 한 곳. 지형 그리기 `FloorView`, 플레이어 그리기 `PlayerView`, 클릭 이동 `MoveController`, 카메라 `CameraRig`(Q/E 회전 · 휠 확대 · 드래그 이동 — 클릭과 드래그는 `PointerDrag`로 가른다, 클릭은 뗄 때). 화면 쪽은 규칙을 바꾸지 않고 커맨드만 낸다.
- Setup 메뉴는 에셋을 `NewScene` 뒤에 불러온다 — 앞에서 불러 두면 참조가 비어 저장된다 (TASKS 1-2 피드백).

## 핵심 설계 규칙 (변경 금지)
바꾸고 싶어지면 영향 범위를 표로 정리하고 사용자 결정을 받은 뒤에만.
1. 규칙과 표현 분리 — 규칙은 엔진을 모른다.
2. 고정 틱 하나 — 모든 지속시간은 틱, 처리 순서 고정. 탐험과 턴 전투는 시계만 다르다.
3. 상태는 스냅샷 하나 (`World`) — AI · 시스템은 상태를 들지 않는다.
4. 시드 난수 주입 · 용도별 분리 · 같은 시드 = 같은 결과 (명중 굴림 포함).
5. 모든 엔티티 고유 ID.
6. 명중 · 피해 미리보기와 실제 결과는 같은 계산을 쓴다.
7. 표시 값 = 실제 값 — 특성 · 장비 · 유물 효과는 한 조회 지점에서 합산.
8. 판 밖 데이터(메타 성장 · 기록)는 스냅샷에 넣지 않고 별도 파일.

## 아트 · 그래픽 규칙
- 그레이박스로 먼저 돌아가게 한다. 데이터는 비주얼을 ID로만 참조하고, 프리팹이 없으면 도형으로 그린다.
- 에셋 목록 문서는 외부 · AI 에셋을 받기 전에 만든다 (`Docs/ART_ASSETS.md`).

## Git · 환경
- 커밋 메시지 `MM.DD/Phase-항목/PascalCase요약`, 수정은 `…Fix`. 한 항목 = 한 커밋.
- 커밋 전 `git status` → 내 파일만 `git add` → `git diff --cached --stat`. 다른 세션 변경은 제외.
- 줄바꿈은 `.gitattributes`로 고정. 빌드 출력은 `.gitignore`에 프로젝트 기준 경로로만 제외.
- 저장 데이터 · 기록 파일을 테스트로 덮어쓰지 않는다. 삭제 전에 대상을 확인한다.
- Unity MCP: 파일을 바꾼 직후 첫 호출은 실패하기 쉽다 → 콘솔 읽기로 깨우고 컴파일이 끝난 뒤 무거운 호출을 한다. 플레이 중에 스크립트를 고치지 않는다.
