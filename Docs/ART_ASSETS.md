# ART_ASSETS.md — 그림 · 모델 목록

외부 · AI 에셋을 넣기 전에 여기에 줄을 먼저 만든다 (CLAUDE.md 아트 규칙). 출처 · 라이선스를 모르면 넣지 않는다.
콘텐츠 규칙: 캐릭터 · 이름 · 그림은 오리지널 — 특정 기존 게임의 캐릭터 모델은 프로젝트(git)에 넣지 않는다.

## 모델을 붙이는 방법 (모델 자리, 2026-11-11)
- 데이터는 그림을 ID로만 참조한다: 유닛 표 `units[].visualId` (GameBalance) → 그림 목록 `Assets/_Game/Data/UnitVisuals.asset`의 같은 ID 줄.
- 그 줄의 `prefab`이 비어 있으면 그레이박스 도형(캡슐)을 그린다. 프리팹을 넣으면 그 모델을 그린다 — 코드 · 규칙은 그대로.
- 줄의 값: `scale`(크기) · `heightOffset`(발이 바닥에 닿게 위아래) · `yawOffset`(모델 앞이 +Z가 아닐 때 돌림) · `speedParameter`(Animator의 걷기 속도 float 이름, 걷는 동안 1 · 서 있으면 0).
- 지금은 플레이어(`player`)만 모델을 쓴다 (`PlayerView`). 적(`rifleman` · `assault`)은 줄만 있고 아직 도형 (`UnitsView`는 3-x에서).
- 모델은 걷는 방향을 바라본다. 클릭 판정은 바닥 평면이라 모델 콜라이더는 끈다.

### 직접 넣어 보기 (어떤 모델이든 같은 순서)
1. FBX를 `Assets` 아래로 끌어다 놓는다 (VRM은 UniVRM 패키지가 먼저 필요).
2. 모델 임포트 설정 → Rig: Humanoid (애니메이션을 쓸 때). 재질이 분홍색이면 URP 재질로 바꾼다 (Edit → Rendering → Materials → Convert…).
3. 씬에 끌어다 놓고 크기 · 방향을 확인한 뒤 프리팹으로 저장한다.
4. `UnitVisuals.asset`의 `player` 줄 `prefab`에 넣고, 키가 약 1.6 ~ 1.8(칸 = 1)이 되게 `scale`, 발이 바닥에 닿게 `heightOffset`을 맞춘다.
5. 플레이 → 캡슐 대신 모델이 걷는다.

개인 테스트용 모델은 `Assets/_Local/` 아래에 둔다 — 이 폴더는 `.gitignore`로 git에 올라가지 않는다. 그런 모델을 `UnitVisuals.asset`에 넣은 채로 커밋하지 않는다 (다른 PC에서는 빈 참조가 된다 — 커밋 전에 비우기).

## 목록
| 그림 ID | 쓰는 곳 | 지금 | 넣을 것 | 출처 · 라이선스 | 상태 |
|---|---|---|---|---|---|
| player | 주인공 (`PlayerView`) | 분홍 캡슐 | 오리지널 주인공 모델 (외형 미정) | — | 미정 |
| rifleman | 사격병 (`UnitsView`) | 빨강 캡슐 | — | — | 미정 |
| assault | 돌격병 (`UnitsView`) | 주황 캡슐 | — | — | 미정 |
| (지형 · 상자 · 보급 상자 · 무기 · 구급상자) | `FloorView` · `ItemsView` | 그레이박스 도형 | — | — | 미정 |
