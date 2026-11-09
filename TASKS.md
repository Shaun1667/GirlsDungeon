# TASKS.md

## 진행 요약 (2026-11-09 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-09): Phase 0 세팅 구현 완료 (0-1 ~ 0-3) — 브랜치 `phase0-setup`, main 병합 전, 테스트 2개 파일 작성 · 실행 안 함, 플레이 확인 대기 0-2 · 0-3. 다음: 미정 — 후보를 먼저 묻는다 (추천: Phase 1 틱 · 스냅샷 · 난수 · 맵 + 칸 이동).**

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
  - 확인 방법: 메뉴 `GirlsDungeon/Debug/Debug Window` 열기 → 플레이 → "재시작 [상태 변경]" 누름 → 시드 숫자가 바뀌면 정상.
