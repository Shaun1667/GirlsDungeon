# TASKS.md

## 진행 요약 (2026-11-09 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-09): Phase 1 구조 기반 + 칸 이동 구현 완료 (1-1 · 1-2) — 브랜치 `phase1-foundation`(`phase0-setup` 위), main 병합 전, 테스트 작성 · 실행 안 함 (한 번도 안 돌림 — 돌릴 때 됨), 플레이 확인 대기 0-2 · 0-3 · 1-1 · 1-2. 다음: 미정 — 후보를 먼저 묻는다 (추천: Phase 2 체력 · 사격 · HUD, 또는 층 이동).**

## 다른 PC에서 할 일
- Unity 6000.3.28f1 설치 (설치 실패 시 Hub 설치 위치를 D 드라이브로 — `Unity.exe`만 있어도 Hub는 완료로 표시하니 `Editor/Data/Tools/7z.exe`가 있는지 확인)

## Phase 0. 세팅

- [x] **0-1. 문서 · Git 기반** (2026-11-09 사용자 "MY_DEV_GUIDE대로 진행")
  - 결정 (2026-11-09, 사용자): 그래픽 3D + AI 생성 에셋 / 메타 성장 있음 / 한 판 30~60분 3구역 / 탐험 → 발견 시 턴 전투 / 이동과 사격 기본 + 스킬로 추가 행동 / 무기 속성 × 약점 + 지형
  - 결정 (2026-11-09, 사용자): 캐릭터는 오리지널로 진행한다 — 기존 게임 캐릭터는 쓰지 않는다.
  - 정한 것 (Claude, 확인 필요): 장르 턴제 전술 던전 / 조작 1인 / 메타 성장 = 해금 / 승리 = 마지막 보스 / 번역 대상 영어 / 구역당 5층 / 세계관 기본값 근미래 폐허
  - 구현: `CLAUDE.md` · `GDD.md` · `TASKS.md` · `.gitattributes` · `.gitignore`(빌드 패턴을 프로젝트 기준으로, `/Assets` 경로에 프로젝트 폴더 추가) · `Docs/Source/` 원본 보존
  - 확인: 커밋 전 `git status`로 `Library` 등이 빠졌는지 확인.

- [x] **0-2. 텍스트 표 · 어셈블리 구조** — 플레이 확인 대기
  - 구현: `Core.Localization.TextTable`(CSV 파싱 · 언어 대체) · `Game.Text`(조회) · `Game.TextLabel`(씬 고정 글) · 표 `Resources/Text/text_table.csv` · 어셈블리 Core(엔진 참조 없음) / Game / EditorTools / Tests
  - 정한 것 (Claude, 확인 필요): 작은 CSV 표 방식 (현지화 패키지 대신, 게임 코드는 `Text.Get`만 부르므로 나중에 교체 가능) / 임시 글꼴은 Unity 내장 `LegacyRuntime`(한글은 OS 글꼴로 표시) — 정식 글꼴은 GDD 열린 항목
  - 확인: 컴파일 오류 0 · 플레이 모드에서 표의 한글 문장 표시 · 콘솔 오류 0 · 캡처. 확인 못 함: 다른 PC 글꼴, 영어 전환 화면.
  - 테스트 작성, 실행 안 함: `Tests.TextTableTests` · `Tests.ProjectRulesTests`
  - 확인 방법: `Assets/_Game/Scenes/Game.unity` 플레이 → 가운데에 "개발 화면 준비 완료"와 "시드 숫자"가 보이면 정상.

- [x] **0-3. 디버그 창 · Setup 메뉴 · 첫 씬** — 플레이 확인 대기
  - 구현: 메뉴 `GirlsDungeon/Debug/Debug Window`(`EditorTools.GirlsDungeonDebugWindow`, 섹션 "판": 시드 · 상태 · 재시작) · 메뉴 `GirlsDungeon/Setup/Build Game Scene`(`EditorTools.GameSceneSetup`) · `Game.GameRoot` · `Game.GameBalance`(`Data/GameBalance.asset`, 고정 시드) · 씬 `Scenes/Game.unity` · 빌드 씬 목록을 Game 하나로
  - 정한 것 (Claude, 확인 필요): 템플릿의 `Scenes/SampleScene` · `TutorialInfo` · `Readme`는 지우지 않고 남겨 둠 (필요 없으면 지울지 답 필요) / 시드는 Phase 1 전까지 `GameRoot`가 임시 보관, `World` 생기면 옮긴다
  - 확인: Setup 메뉴 2번 연속 실행 성공 · 플레이 모드 진입 · `DebugRestart`로 시드 변경과 `DebugUsed=True` 확인 · 디버그 창 열림 · 콘솔 오류 0. 확인 못 함: 디버그 창 재시작 버튼의 실제 클릭(같은 함수를 코드로 호출해 확인), 씬 파일이 Setup 재실행 때 내부 ID까지 같은지(내용과 연결은 같게 다시 만듦).
  - 확인 방법: 메뉴 `GirlsDungeon/Debug/Debug Window` 열기 → 플레이 → "새 시드로 재시작 [상태 변경]" 누름 → 시드 숫자가 바뀌면 정상. (버튼 이름은 1-2에서 바뀜)
  - 고침 (2026-11-09): 1-2에서 "개발 화면 준비 완료" 글과 키 `ui.dev.ready`를 지우고 조작 안내로 교체.

## Phase 1. 구조 기반 + 핵심 조작

- [x] **1-1. 틱 · 이벤트 · 스냅샷 · 난수 · 맵 생성** (2026-11-09 사용자 "Phase 1 시작") — 플레이 확인 대기
  - 결정 (2026-11-09, 사용자): 8방향 이동 / 한 층 32 × 32, 방 6~8개 (방 크기 7~13칸)
  - 구현: `Core.Random`(`SeededRandom` · `RandomStreams`) · `Core.Sim`(`World` · `Unit` · `Simulation` · `RunFactory` · `ICommand` · `MoveCommand` · `GameEvent` · `WorldSnapshot`) · `Core.Map`(`FloorMap` · `MapSettings` · `DungeonGenerator` · `GridNavigation`) · `Core.Grid`(`GridPos`)
  - 수치: BALANCE 1번 · 2번
  - 정한 것 (Claude, 확인 필요): 복도 폭 1칸 / 고리 복도 2개 / 계단은 시작 방에서 걸어서 가장 먼 방 가운데 / 대각선으로 벽 모서리를 뚫고 지나가지 못함 / 한 걸음 = 1틱 / 저장 파일 쓰기와 시야는 아직 없음
  - 확인: 컴파일 오류 0 · 실제 설정으로 시드 1,000개 생성 (실패 0, 끊긴 칸 0, 막다른 칸 0, 맵 한 장 평균 1.6ms, 시작 ~ 계단 10 ~ 51걸음, 걸을 수 있는 칸 363 ~ 625).
  - 참고 (결정 필요): 방 개수가 6개 91% · 7개 9% · 8개 0.1%로 쏠린다. 방이 커서(7~13칸) 32 × 32에 잘 안 들어간다. 방을 작게(예: 6~11칸) 하면 고르게 나뉜다.
  - 확인 못 함: 저장 → 복원의 게임 내 흐름 (코드와 테스트만 있고 파일 저장은 Phase 5).
  - 테스트 작성, 실행 안 함: `Tests.RandomStreamsTests` · `DungeonGeneratorTests` · `GridNavigationTests` · `SimulationTests` · `WorldSnapshotTests` · `BalanceTests`
  - 확인 방법: 디버그 창 "맵" 섹션에서 방 개수와 시작 · 계단 칸을 본다. "이 시드로 재시작"에 같은 숫자를 넣으면 같은 맵이 나오면 정상.

- [x] **1-2. 그레이박스 화면 · 클릭 이동 · 카메라** — 플레이 확인 대기
  - 결정 (2026-11-09, 사용자): 이동 입력은 마우스 클릭만 / 카메라는 비스듬한 위 + 90도 회전
  - 구현: `Game.FloorView`(지형 메시) · `Game.PlayerView`(캡슐, 칸 사이 보간) · `Game.MoveController`(클릭 → 경로 → 한 걸음씩) · `Game.CameraRig`(따라가기 · Q/E) · `Game.GridSpace` · `Game.GameRoot` 연결 · 재질 `Data/Materials/Greybox*.mat` · 디버그 창 섹션 판 · 맵 · 이동 · 조작 안내 글 `ui.hud.controls`
  - 수치: BALANCE 3번
  - 정한 것 (Claude, 확인 필요): 카메라 회전만 Q/E 키 (이동은 클릭만) / 걷는 중 다른 칸을 누르면 지금 칸에서 경로를 바꿈 / 계단 칸은 아직 표시만 하고 층 이동은 없음 / 벽은 반투명 처리 없이 회전으로 뒤를 봄
  - 확인: 컴파일 오류 0 · Setup 메뉴 2번 연속 실행 · 플레이 모드에서 `DebugMoveTo`로 시작 (13,15) → 계단 (24,22) 18걸음, 틱 18, 화면 캡슐도 같은 칸 도착 · 벽 칸 이동 거부 · 카메라 45도 → 135도 · 콘솔 오류 0 · 캡처 2장.
  - 확인 못 함: **실제 마우스 클릭과 Q/E 키 입력, 칸 위 하이라이트** (코드에서 같은 함수를 호출해 확인, 포커스 없이 진행), 실제 FPS.
  - 확인 방법: `Assets/_Game/Scenes/Game.unity` 플레이 → 바닥 칸에 마우스를 올리면 하늘색 칸 표시 → 클릭하면 캡슐이 걸어감 → 벽을 누르면 안 움직임 → Q/E로 카메라가 90도씩 돔.
  - 피드백: 첫 실행에서 `GameRoot.balance`가 비어 시작 때 오류 → 원인: `NewScene`이 참조 없는 에셋을 메모리에서 내려, 미리 불러 둔 `GameBalance`가 빈 값으로 저장됨 → 수정: 에셋을 `NewScene` 뒤에 불러옴. 검사: `ProjectRulesTests.GameScene_HasNoEmptyGameRootReferences`.
  - 피드백: 캡처에서 벽 옆면이 뚫려 보임 → 원인: 옆면 삼각형 방향이 반대라 뒷면으로 처리되어 안 그려짐 → 수정: `FloorView.AddSide` 방향을 바꿈 · 캡처로 확인. 검사: 플레이 캡처에서 벽 옆면이 어둡게 보이는지 본다.
  - 확인: 같은 시드(381019532)를 다시 넣어 시작 칸 · 계단 · 방 개수가 같은 것을 확인.
