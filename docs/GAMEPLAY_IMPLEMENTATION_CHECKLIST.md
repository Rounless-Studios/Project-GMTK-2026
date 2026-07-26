# 지옥 카운트다운 레이싱 게임 구현 체크리스트

> **원본 GDD:** `C:\Users\kkkkk\Downloads\지옥_카운트다운_레이싱_게임_GDD.md`  
> **GDD 최종 수정일:** 2026년 7월 24일  
> **체크리스트 최종 리뷰일:** 2026년 7월 26일  
> **구현 현황 기준:** 로컬 `develop` / `652e03b` + 미커밋 워킹트리 (코드·에셋·씬 YAML 실물 확인)  
> **엔진:** Unity `6000.3.19f1`  
> **플랫폼:** Windows PC, WebGL  
> **목표:** 3~5분 안에 끝나는 완성도 높은 단일 레이스

## 체크 규칙

- `[x]` 현재 프로젝트에 구현되어 있고 기반으로 사용할 수 있음
- `[ ]` 새로 구현하거나 GDD에 맞게 수정·검증해야 함
- 부분 구현은 완료로 표시하지 않고 **수정 필요**에 둔다.
- 완료 체크는 코드 작성, Play Mode 확인, 관련 테스트 통과까지 포함한다.

### 2026-07-26 리뷰의 검증 범위

이번 갱신은 **정적 검증만** 수행했다. Unity 에디터가 실행 중이 아니어서(`unity status` 연결 없음)
`run_tests`·`editor_play`·스크린샷을 돌리지 못했다. 따라서 이번 리뷰에서 확인한 것은 다음뿐이다.

- C# 소스 전수 확인, 설정 필드별 **실제 참조 여부** `rg` 검증
- `Assets/Scenes/GMTK_Race.unity`(텍스트 YAML) 및 `Assets/Prefab/**` 내 컴포넌트·직렬화 값 확인
- `GameJamDefault.asset` / `FastTest.asset` 실제 저장값 확인, Build Settings·`Packages/manifest.json` 확인

EditMode 테스트 실행 결과와 Play Mode 동작은 **이번 리뷰에서 확인하지 못했다.** 아래에서
"코드 구현 확인"이라고 적은 항목은 소스·에셋 근거가 있다는 뜻이며, 체크 규칙에 따라 `[x]`로
올리지 않았다. 테스트·Play Mode 확인은 사용자가 직접 실행한 뒤 반영한다.

### 2026-07-26 `feature/improvements` 구현 패스

다음 항목은 코드·정적 컴파일까지 구현했으며, 체크 규칙에 따라 Play Mode 확인 전에는 기존
체크박스를 완료로 올리지 않는다.

- 킷 `RaceFinish`를 런타임에서 제거하고 `RaceResultAuthority`가 레이스당 결과를 한 번만 발행
- `GmtkRccpPlayersSpawner`가 `race.aiCount`를 직접 사용
- `GameJamDefault.maximumDurability`는 반복 대파를 막는 플레이테스트 의도에 따라 `1000000` 유지
- 절차형 퀴즈가 `QuizSettings`의 제한 시간과 텍스트 답안 수를 사용
- `RaceHud` 자동 생성, 최하위·처형·목표·최종 결투·결과 피드백 추가
- 파열 밀침, 영혼 교환 상호 충돌 무시 시간, 추월 실패 감속을 실제 차량 물리에 연결
- `VehicleSettings` 및 `SpecialEventSettings` 추가
- 특수 이벤트를 덤프트럭·지진·운석 3종으로 제한하고 횟수·간격·중복·보호 페이즈 규칙 연결
- AI 장애물 회피, 성격별 부스트 사용, 최하위 긴급 반응, 뒤처짐 보정 연결
- `GMTK.GameBalance.Runtime`, `GMTK.Quiz.Runtime`, `Assembly-CSharp` 정적 컴파일 성공

---

## 0. GDD 기준 확정 목표

| 항목 | 구현 목표 |
|---|---|
| 코스 | 목표 지점까지 달리는 1랩 레이스 |
| 참가 차량 | 플레이어 1대 + AI 5대, 총 6대 |
| 설정 | 차량 수, 탈락 간격 등 주요 수치를 설정으로 분리 |
| 탈락 | 30초마다 현재 최하위 차량 처형 |
| 탈락 종료 | 두 대가 남으면 탈락 중단 |
| 최종 단계 | 남은 두 대가 닫히는 관문을 향해 질주 |
| 승리 | 관문을 먼저 통과한 차량 |
| 차량 조작 | 가속·제동·조향·충돌·부스트·기본 드리프트 |
| 스킬 | 파열, 엔진 봉인, 영혼 교환 |
| 퀴즈 | 레이스를 멈추지 않고 마우스로 답 선택. **시전자가 저주를 걸고 대상이 퀴즈로 방어한다** |
| 추월 도전 | 20초 주기 후보 판정, 8초 안에 추월 후 0.5초 유지 |
| 특수 이벤트 | 덤프트럭 습격, 지진, 운석 낙하 |
| 처형 화면 | 플레이어 주행 화면을 메인으로 유지하고 처형 차량은 오른쪽 아래 약 1/3 CCTV로 표시 |
| 결과 | 빠른 승리·패배 표시와 재시작 |

### GDD 내부 불일치

- 최신 MVP의 `1랩`을 우선하고, 트랙 설명의 `3랩 서킷`은 이전안으로 본다.
- 최신 MVP와 기준표의 `총 6대`를 우선하고, 완료 정의의 `차량 5대 단계`는 `차량 6대 단계`로 해석한다.
- 퀴즈 제한 시간이 기준표에는 `3초`, 발동 흐름에는 `4초`로 적혀 있으나 설정 기본값은 `4초`로 확정한다.
- 기본 드리프트는 MVP에 포함하되 고급 드리프트와 트릭 시스템은 제외한다.

### 2026-07-26 기획 확정 — 저주 발동 흐름 (방어형)

GDD 초안의 "시전자가 퀴즈를 풀어 저주를 적용한다"를 폐기하고, **현재 구현된 방어형 흐름을 정본으로 확정한다.**

```
시전자가 E를 누른다
  → 쿨다운이 준비됐으면 즉시 소모하고 유효 대상을 자동 선택한다 (퀴즈 없음, 발동은 항상 성공)
  → 대상이 인간 플레이어면 휴대폰 퀴즈가 뜬다
        정답        → 저주 무효 (방어 성공)
        오답·시간초과 → 저주 적용 + 화면에 페널티 표시
  → 대상이 AI면 화면 없이 성격별 방어 확률로 즉시 판정한다
```

이 확정이 바꾸는 것:

- **퀴즈는 공격 수단이 아니라 방어 수단이다.** 플레이어가 저주를 쓸 때는 퀴즈가 뜨지 않고, 퀴즈는 항상 "내가 표적이 됐다"는 신호다. 긴장의 방향이 뒤집힌 것이며 의도된 설계다.
- 쿨다운은 **발동 시점에** 소모된다. 대상이 방어에 성공해도 시전자의 쿨다운은 돌아간다. 즉 "성공/실패"는 시전자가 아니라 대상의 결과다.
- 성격별 차이를 만드는 축이 `curseTendency`(저주를 얼마나 자주 쓰는가)에서 **`quizAvoidChance`(저주를 얼마나 잘 막는가)** 로 바뀐다. 저주 발동은 쿨다운만 차면 누구나 하므로 사용 성향 축은 의미가 없다.
- 따라서 GDD의 "저주 중심 책략가"는 정의가 성립하지 않는다. 아래 AI 성격 축 확정에서 다시 정리한다.

### 2026-07-26 기획 확정 — AI 성격 축

설정의 `AIArchetype`(Rammer/Speedster/Schemer/Survivor)을 폐기하고,
**실제 구현된 `GMTK.AIPersonalityType` 4종을 정본으로 확정한다.**
GDD의 네 유형과 1:1로 대응하되 명칭과 행동 정의는 프로젝트 코드를 기준으로 한다.

| 정본 타입 | 한국어 명칭 | GDD 대응 | 행동 정의 | 현재 코드 근거 |
|---|---|---|---|---|
| `Reckless` | 폭주광 | 스피드스터 | 최고속 지향, 코너 감속을 덜 하고 NOS를 상시 흘린다 | `recklessSpeedMultiplier 1.15`, `nosInput 0.35`, 방어율 0.3 |
| `Rammer` | 난폭자 | 난폭자 | 플레이어 사거리 안에서 조준점을 플레이어 쪽으로 당겨 충돌을 유도한다 | `rammerSpeedMultiplier 1.05`, `ramStrengthMetres 7`, 방어율 0.35 |
| `Blocker` | 봉쇄자 | 책략가 | 속도를 조금 버리고 플레이어 차선으로 밀어붙여 진로를 막는다 | `blockerSpeedMultiplier 0.97`, `blockStrengthMetres 5`, 방어율 0.65 |
| `CleanRacer` | 생존자 | 생존자 | 오프셋 없이 레이싱 라인만 타고, 저주 방어에 가장 강하다 | `cleanRacerSpeedMultiplier 1.0`, 오프셋 없음, 방어율 0.8 |

- **책략가 → 봉쇄자로 재정의한다.** 방어형 흐름에서는 "저주를 많이 쓰는 AI"를 만들 수 없으므로,
  책략가의 "순위를 계략으로 조작한다"는 성격을 **진로 차단**으로 표현한다. 대신 저주 방어율을
  중간값(0.65)으로 두어 "쉽게 무너지지 않는다"는 인상은 유지한다.
- **방어율이 성격 표현의 핵심 축이다.** 이미 `CurseSettings`에 성격별 값 4개가 들어 있고
  (생존자 0.8 > 봉쇄자 0.65 > 난폭자 0.35 > 폭주광 0.3), 방어형 흐름과 일관된다.
  "빠르고 공격적인 차는 저주에 약하고, 느리고 신중한 차는 잘 막는다"가 규칙이 된다.
- **5대 구성: 폭주광 2 + 난폭자 1 + 봉쇄자 1 + 생존자 1.** GDD의 "스피드스터 2대"와 같다.
  `AiPersonalityRoster.BuildOrder`는 `i % 4`로 목록을 만든 뒤 셔플하므로 **중복되는 성격은
  항상 배열 0번**이고 셔플은 그리드 슬롯만 바꾼다. 즉 `AIPersonalityAssigner.Personalities`
  배열을 `{ Reckless, Rammer, Blocker, CleanRacer }` 순서로 두면 이 구성이 확정된다
  (현재 0번은 `Rammer`라 난폭자가 2대다).
- **설정화: 2026-07-26 구현 완료.** `AIArchetype`·`AIArchetypeProfile`·`archetypeAssignments`를 제거하고
  `AIPersonalityType`을 키로 하는 `AiPersonalityProfile`을 `AISettings`에 만들었다.
  필드는 `paceScale`, `lateralStrengthMetres`, `boostTendency`, `quizAvoidChance`, `catchupAcceleration`.
  `curseTendency`·`curseCooldownSeconds`·`curseWarningSeconds`는 방어형 흐름에서 쓰이지 않으므로 삭제했다.
- **구성은 enum 순서가 아니라 데이터가 정한다.** `AISettings.personalityAssignments`가 차량당 한 칸씩
  담고(`Reckless, Reckless, Rammer, Blocker, CleanRacer`), `AiPersonalityRoster`가 그리드 슬롯만
  셔플한다. 이전에 "배열 0번이 중복된다"던 결합을 없앴으므로 코드 배열 순서를 바꿀 필요가 없다.
- **한 성격 = 한 줄.** 이전에는 같은 성격의 수치가 세 곳(드라이버의 `switch` 페이스, 공용
  `ramStrengthMetres`/`blockStrengthMetres`, `CurseSettings`의 방어율)에 흩어져 있었다. 이제 전부
  `personalityProfiles`의 한 행이다. `aggroRangeMetres`만 공용으로 남겼다 — 성격 특성이 아니라
  "AI가 플레이어를 인지하는 거리"이기 때문이다.

### 모든 수치 설정화 원칙

문서에 적힌 게임플레이 숫자는 **코드 상수가 아니라 기본 프리셋 값**이다. 날짜, Unity 버전, 현재 테스트 개수처럼 빌드 사실을 설명하는 숫자를 제외한 모든 런타임·밸런스·연출 수치는 코드 수정 없이 Unity Inspector에서 변경할 수 있어야 한다.

- [x] 단일 루트 `GameBalanceSettings` ScriptableObject 생성 — `Assets/GameBalance/Runtime/GameBalanceSettings.cs`, `[CreateAssetMenu]` 포함
- [ ] 설정 그룹 분리 — 구현됨: `RaceSettings`, `EliminationSettings`, `ResultSettings`, `QuizSettings`, `CurseSettings`, `BoostSettings`, `DamageSettings`, `VehicleRecoverySettings`, `OvertakeSettings`, `CameraSettings`, `AISettings`(+`AiDrivingSettings`, `AiPersonalityAssignmentSettings`, `AIArchetypeProfile`), `TrackValidationSettings`, `PresentationSettings`, `PlaytestSettings` (총 14그룹) / **미구현: `VehicleSettings`, `SpecialEventSettings`** (차량 수치는 RCCP 프리팹과 `GmtkRccpVehicle.boostAcceleration`, 특수 이벤트 수치는 `RandomEventManager` 인스펙터 40여 필드에 남아 있음)
- [x] 게임 잼 기본값을 담은 `GameJamDefault` 프리셋 에셋 생성 — `Assets/GameBalance/Resources/GameBalance/GameJamDefault.asset` (**주의: 아직 Git 미추적. 구 위치 `Assets/Resources/GameBalance/`는 삭제 상태**)
- [x] 빠른 자동 테스트용 `FastTest` 프리셋 에셋 생성 — `Assets/GameBalance/Resources/GameBalance/FastTest.asset` (`intervalSeconds: 3`, `answerTimeSeconds: 2`, 동일하게 Git 미추적)
- [x] 런타임 시작 시 선택된 설정을 불변 스냅샷으로 만들어 모든 시스템에 공급 — `GameBalance.Current` / `BeginRace()`가 `Instantiate` 복제본을 스냅샷으로 고정, `EndRace()`가 해제. `GMTKRaceState`가 Racing 진입 시 호출
- [ ] 레이스 관리자, 차량, AI, HUD, 카메라, VFX, 오디오가 같은 설정 스냅샷을 참조 — **미완.** `GameBalance.Current`를 읽는 곳: `EliminationManager`, `BoostController`, `CurseController`/`CurseManager`, `DurabilityController`/`DurabilityHud`, `OvertakeManager`, `ExecutionCctvDirector`, `RaceHud`, `GmtkRccpWaypointDriver`, `GmtkRccpFallRespawner`, `AIPersonality(Assigner)`. **참조하지 않는 곳: `FinalGate`(자체 `gateDurationSeconds = 20f`), `CarExplosion`, `RandomEventManager`, `GameAudioManager`, `RaceFlow`(프롤로그·카운트다운 시간), 퀴즈 전체(`QuizSettings` 미사용), `GmtkRccpVehicle`**
- [ ] MonoBehaviour와 프리팹에 동일 수치를 중복 직렬화하지 않음 — **미완.** 중복·독립 직렬화 확인: `EliminationManager.explosionForce/upwardForce/explosionRadius`(public 필드), `CarExplosion` 12개 필드, `RandomEventManager` 전체, `FinalGate.gateDurationSeconds`, `ExecutionCctvDirector.followOffset/targetOffset/fieldOfView`, `AIPersonality`의 성격별 speedMultiplier 4개, `RaceFlow.prologueSeconds/countdownSeconds`, `QuizSessionController.feedbackDurationSeconds`, `GmtkRccpVehicle.boostAcceleration`
- [ ] 정적 상수, 메서드 내부 리터럴, 코루틴 대기시간에 밸런스 숫자를 직접 작성하지 않음 — **미완.** `ProceduralQuizQuestionSource`의 문제 제한시간 리터럴(`3f`/`4f`/`5f`), `CurseManager.HidePenaltyFeedbackAfterDelay`의 `WaitForSecondsRealtime(2.5f)`, `RaceFlow`의 `WaitForSecondsRealtime(0.45f)`·`ConfigureFeedbackDuration(1.8f)`, `StartMenuCanvas`의 게이지 상수 4개, `ExecutionCctvDirector.HideBeforeWreckVanishes = 0.1f`
- [x] 테스트도 숫자를 다시 하드코딩하지 않고 테스트 프리셋 또는 설정 스냅샷을 사용 — 상태 클래스 테스트가 설정 객체를 생성해 주입
- [ ] 설정값 변경이 재시작 후 새 레이스에 반영되고 코드 재컴파일은 요구하지 않음 — 스냅샷 구조와 재시작 카운트다운 연결은 구현, **Play Mode 미검증**
- [x] 빌드에서도 Resources 또는 명시적 프리셋 참조를 통해 설정을 로드 — `GameBalance.Load()`가 `Resources.Load<GameBalanceSettings>("GameBalance/" + name)` 사용. 프리셋이 `Assets/GameBalance/Resources/GameBalance/` 아래에 있어 빌드에 포함됨
- [x] 설정 누락 시 조용히 임의 기본값을 만들지 않고 명확한 검증 오류 출력 — `Load()`가 프리셋 부재 시 `LogError`, 검증 실패 시 필드명이 담긴 `LogError`, `OnValidate()`가 `LogWarning`

**현재 `GameJamDefault`의 즉시 조치 필요 값 2건 (실물 확인):**

1. `elimination.intervalSeconds: 10` — GDD 기준값은 30. 게다가 `warningSeconds: 10`과 같아
   `Validate()`의 `warningSeconds must be < intervalSeconds` 규칙에 걸려 **현재 프리셋은 로드할 때
   검증 오류를 뱉는다.** 밸런스 확정 시 30으로 되돌리거나 경고 시간을 함께 조정해야 한다.
2. `damage.maximumDurability: 1000000` — 대파를 미루기 위한 임시값. 이 상태에서는 파열 저주 35,
   강한 충돌 15가 사실상 무의미하고 `DurabilityHud` 비율도 항상 100%로 보인다.

### 설정 카탈로그와 GDD 기본값

코드 상태 열은 `rg`로 필드별 실제 참조를 전수 확인한 결과다.
**사용** = 런타임 시스템이 값을 읽는다 / **선언만** = 필드와 프리셋 값은 있으나 읽는 코드가 없다 / **미구현** = 필드 자체가 없다.

| 설정 그룹 | 설정 키 | GDD 기본값 또는 상태 | `GameJamDefault` 실제값 | 코드 상태 |
|---|---|---:|---:|---|
| Race | `aiCount` | 5 | 5 | **선언만** (실제 스폰 수는 `RaceData.AiBotsSelected`) |
| Race | `lapCount` | 1 | 1 | **선언만** (실제 랩 수는 `RaceData.LapsSelected`) |
| Race | `finalDuelRacerCount` | 2 | 2 | 사용 (`EliminationManager`) |
| Race | `targetRaceDurationMinSeconds` | 180 | 180 | **선언만** |
| Race | `targetRaceDurationMaxSeconds` | 300 | 300 | **선언만** |
| Elimination | `intervalSeconds` | 30 | **10** | 사용 |
| Elimination | `warningSeconds` | 10 | 10 | 사용 (현재 `intervalSeconds`와 같아 검증 오류) |
| Elimination | `intenseWarningSeconds` | 5 | 5 | 사용 |
| Elimination | `executionCameraLeadSeconds` | 3 | 3 | 사용 |
| Elimination | `executeAtRemainingSeconds` | 0 | 0 | **선언만** (0초 판정이 `Update` 타이머에 내장) |
| Result | `playerDeathCinematicMaxSeconds` | 3 | 3 | **선언만** |
| Result | `restartToControlMaxSeconds` | 5 | 5 | **선언만** |
| Quiz | `answerTimeSeconds` | 4 | 4 | **선언만** (실제 제한시간은 `ProceduralQuizQuestionSource` 리터럴 3/4/5초) — 방어형 흐름에서는 이 값이 곧 **방어 제한시간**이다 |
| Quiz | `minimumAnswerCount` | 4 | 4 | 사용 (`CurseManager` → `QuizSessionController`) |
| Quiz | `maximumAnswerCount` | 4 | 4 | 사용 (일반 객관식 보기 수를 4개로 고정) |
| Curse | `sharedCooldownSeconds` | 12 | 12 | 사용 (`CurseCooldownState`) |
| Boost | `maximumCharges` | 2 | 2 | 사용 |
| Boost | `durationSeconds` | 1.25 | 1.25 | 사용 |
| Boost | `rechargeSecondsPerCharge` | 6 | 6 | 사용 |
| Boost | `overtakeRewardCharges` | 1 | 1 | 사용 |
| Boost | `boostSpeedMultiplier` | (문서 없음) | 1.5 | 사용 (`BoostState` → `GmtkRccpVehicle.ApplyBoost`) |
| Damage | `maximumDurability` | 100 | **1000000** | 사용 |
| Damage | `damagedThreshold` | 60 | 60 | 사용 |
| Damage | `criticalThreshold` | 30 | 30 | 사용 |
| Damage | `wreckedThreshold` | 0 | 0 | 사용 |
| Damage | `wreckDurationSeconds` | 2 | 2 | 사용 |
| Damage | `recoveryDurability` | 50 | 50 | 사용 |
| Damage | `recoveryProtectionSeconds` | 1.5 | **2** | 사용 (값이 GDD와 다름) |
| Damage | `strongCollisionImpulse` / `strongCollisionDamage` | (문서 없음) | 8 / 15 | 사용 (`DurabilityController.OnCollisionEnter`) |
| Rupture | `ruptureDurabilityDamage` | 35 | 35 | 사용 |
| Rupture | `ruptureKnockbackForce` | 3 | **8** | **선언만** (밀침 미구현) |
| Engine Seal | `engineSealDurationSeconds` | 5 | 5 | 사용 |
| Soul Swap | `soulSwapMinimumDistanceMeters` | 15 | 15 | 사용 |
| Soul Swap | `soulSwapMaximumDistanceMeters` | 40 | 40 | 사용 |
| Soul Swap | `soulSwapCollisionIgnoreSeconds` | 0.3 | 0.3 | **선언만** (충돌 무시 창 미구현) |
| Overtake | `checkIntervalSeconds` | 20 | 20 | 사용 |
| Overtake | `challengeDurationSeconds` | 8 | 8 | 사용 |
| Overtake | `requiredLeadHoldSeconds` | 0.5 | 0.5 | 사용 |
| Overtake | `bindDurationSeconds` | (문서 없음) | 5 | 사용 (해제 타이머만) |
| Overtake | `bindSpeedMultiplier` | (문서 없음) | 0.5 | **선언만** — `OvertakeManager.BindSpeedMultiplier`를 읽는 차량 코드가 없어 실패 감속이 실제로 걸리지 않음 |
| AI Curse | `curseWarningSeconds` | 1 | — | **폐기 완료** (2026-07-26) — 방어형 흐름에서는 퀴즈가 곧 경고 신호다 |
| AI Curse | `hostileEffectGraceSeconds` | 2 | — | **미구현.** 연속 표적 방지용으로 유지 필요 (`AISettings`로 신규 추가) |
| AI Curse | `aiInitialCastDelayMin/MaximumSeconds`, `aiFailedCastRetrySeconds` | (문서 없음) | 2 / 5 / 1 | 사용 (`CurseManager.UpdateAiCasters`) |
| AI | `personalityProfiles[].quizAvoidChance` | 생존자 0.8 / 봉쇄자 0.65 / 난폭자 0.35 / 폭주광 0.3 | 동일 | 사용. **`CurseSettings` → `AISettings` 이전 완료** (`AISettings.QuizAvoidChanceOf`) |
| AI | `defaultQuizAvoidChance` | (문서 없음) | 0.5 | 사용 — 성격 컴포넌트가 없는 차량용 폴백 |
| Camera | `sideBySideActivationSeconds` | 0.6 | 0.6 | **선언만** (투샷 미구현) |
| Camera | `executionCctvViewportRect` | 처형 CCTV를 속도계와 겹치지 않는 오른쪽 중앙 Rect로 설정 | x .67 / y .35 / w .32 / h .30 | 사용 (`ExecutionCctvDirector`) |
| Camera | `executionTransitionSeconds` | 0.2 | 0.2 | 사용 |
| Camera | `executionReturnSeconds` | 0.25 | 0.25 | 사용 |
| Gate | `closeDurationSeconds` | 0.75 | — | **미구현** (`presentation.gateCloseSpeed 2` / `gateCloseDelaySeconds 0.2`가 **선언만**) |
| Gate | `loserExecutionDelaySeconds` | 0.25 | 0.25 | 사용 (`FinalGate`의 `gateExecutionDelaySeconds`) |
| Presentation | `wreckLingerSeconds` | (문서 없음) | 2.5 | 사용 (`EliminationManager`, `ExecutionCctvDirector`) |
| Presentation | `explosionVfxDurationSeconds` / `masterSfxVolume` / `warningAudioFadeSeconds` | (문서 없음) | 0.45 / 1 / 0.25 | **선언만** |
| Special Event | `enabled` | true | — | **미구현** (`RandomEventManager.enableEvents` 인스펙터 값이 대신 쓰임) |
| Special Event | `initialDelaySeconds` | 15 | — | **미구현** (`firstEventDelay 8`) |
| Special Event | `minimumIntervalSeconds` | 25 | — | **미구현** (`intervalRange 9~16`) |
| Special Event | `maximumIntervalSeconds` | 40 | — | **미구현** |
| Special Event | `warningLeadSeconds` | 2 | — | **미구현** (사전 경고 자체가 없음) |
| Special Event | `maximumSimultaneousEvents` | 1 | — | **미구현** |
| Special Event | `executionSafetySeconds` | 10 | — | **미구현** |
| Special Event | `maximumEventsPerRace` | 4 | — | **미구현** |
| Dump Truck | 전 항목 | 22 / 20 / 6 / 12 | — | **미구현** (이벤트 자체 없음) |
| Earthquake | `durationSeconds` | 3 | — | **미구현** (현재 1프레임 `VelocityChange` 충격) |
| Earthquake | `lateralVelocityChange` | 2 | — | **미구현** (현재 `quakeUpForceRange 4~7`로 **위로** 띄움) |
| Earthquake | `cameraShakeIntensity` | 0.35 | — | **미구현** |
| Meteor | 전 항목 | 2 / 5 / 25 / 5 / 3 | — | **미구현** (현재 물리 상자를 위에서 떨어뜨리는 방식, 경고·반경 피해 없음) |
| Track Validation | `minimumBoostStraights` | 2 | 2 | **선언만** |
| Track Validation | `minimumParallelSections` | 1 | 1 | **선언만** |
| Playtest | `ruleUnderstandingTargetSeconds` | 15 | 15 | **선언만** |
| Playtest | `minimumExternalTesterCount` | 3 | 3 | **선언만** |
| Vehicle Recovery | `fallDistanceBelowTrack` 외 4개 | (문서 없음) | 8 / 2 / 20 / 5 / 0.1 | 사용 (`GmtkRccpFallRespawner`) |
| AI Driving | `AiDrivingSettings` 24개 필드 | (문서 없음) | — | 사용 (`GmtkRccpWaypointDriver` + `AiDriving`) |
| AI | `personalityAssignment` 5개 필드 | (문서 없음) | — | 사용 (`AIPersonalityAssigner`, `AIPersonality`) |
| AI | `archetypeAssignments`, `profiles`, `GetProfile` (`AIArchetype` 축) | — | — | **폐기 완료** (2026-07-26). 코드에서 제거됨 |
| AI | `personalityAssignments` (`AIPersonalityType` 축) | 폭주광 2·난폭자 1·봉쇄자 1·생존자 1 | 동일 | 사용 (`AIPersonalityAssigner` → `AiPersonalityRoster.BuildOrder`) |
| AI | `personalityProfiles[].paceScale` | (문서 없음) | 폭주광 1.0 / 난폭자 0.96 / 생존자 0.9 / 봉쇄자 0.86 | 사용 (`GmtkRccpWaypointDriver.ConfigurePersonality`, `AIPersonality.SpeedMultiplier`) |
| AI | `personalityProfiles[].lateralStrengthMetres` | (문서 없음) | 난폭자 7 / 봉쇄자 5 / 나머지 0 | 사용 (`AIPersonality.LateralStrength` → 어댑터 → 드라이버) |
| AI | `personalityProfiles[].boostTendency` | (문서 없음) | 폭주광 0.8 / 나머지 0.5 | **선언만** — AI 부스트가 별개 작업 |
| AI | `personalityProfiles[].catchupAcceleration` | (문서 없음) | 0.1 | **선언만** — 따라잡기가 별개 작업 |

플레이어 1명은 조정 가능한 밸런스 숫자가 아니라 싱글플레이 게임 모드의 구조적 불변조건이다. `totalRacerCount`는 별도로 저장하지 않고 `1 + aiCount`로 계산한다 (`RaceSettings.TotalRacerCount`, 중복 필드 없음 — 확인).

### 숫자 외 정책 설정

GDD에서 선택지가 열려 있는 동작도 코드 분기 수정 없이 프리셋에서 고를 수 있게 한다.

| 설정 그룹 | 설정 키 | 권장 기본값 | 기획 상태 | 코드 상태 |
|---|---|---|---|---|
| Curse | `selectionMode` | `OrderedRotation` | 확정 | 구현 (`CurseController.SelectCurse`, `Random` 분기까지 존재) |
| Curse | `targetingMode` | `NearestAheadThenNearestActive` | 확정 | 구현 (3개 모드 모두 분기 존재) |
| Overtake | `cooldownRewardMode` | `Reset` | 확정 | 구현 (`Reset`/`Reduce` 분기 존재) |
| Overtake | `cooldownReductionSeconds` | 6 | 대체 `Reduce` 모드용 | 구현 |
| AI | `personalityAssignments` (`AIPersonalityType` 축) | 폭주광 2, 난폭자 1, 봉쇄자 1, 생존자 1 | 확정 (2026-07-26) | **연결 완료.** 프리셋 리스트가 구성을 정하고 로스터는 슬롯만 셔플한다 |
| Curse | `resolutionMode` | `TargetDefends` (대상이 퀴즈로 방어) | 확정 (2026-07-26) | 구현. 분기 없이 이 흐름만 존재하므로 설정 필드는 만들지 않는다 |
| Quiz | `language` | `English` | 확정 | 필드 없음. 문제 생성기가 영어 문자열만 만들어 결과적으로 충족 |
| Special Event | `blockDuringExecutionWarning` | true | 확정 | **미구현** |
| Special Event | `blockDuringQuiz` | true | 확정 | **미구현** |
| Special Event | `blockDuringOvertakeChallenge` | true | 확정 | **미구현** |
| Special Event | `blockDuringFinalDuel` | true | 확정 | **미구현** |
| Special Event | `cancelActiveOnProtectedPhase` | true | 확정 | **미구현** |
| Special Event | `preventImmediateRepeat` | true | 확정 | **미구현** |

### 설정 유효성 검증

근거: `GameBalanceSettings.Validate()` (19개 규칙) + `OnValidate()` + `GameBalance.Load()`.
EditMode 테스트는 `GameBalanceSettingsTests.cs` 11케이스로 존재하지만 **이번 리뷰에서 실행하지 못했다.**

- [x] `1 + aiCount`로 총 차량 수를 계산하고 총 차량 수를 별도 중복 저장하지 않음 — `RaceSettings.TotalRacerCount` 계산 프로퍼티
- [ ] 모든 시간·거리·피해·충전 수치는 0 이상인지 검사 — 부분. 인스펙터 입력은 `[Min]` 어트리뷰트로 막히지만 `Validate()`가 명시적으로 검사하는 것은 `boost.durationSeconds`, `boost.rechargeSecondsPerCharge`, `curse.sharedCooldownSeconds`, `overtake.checkIntervalSeconds`, `vehicleRecovery` 4개뿐. 코드로 값을 주입하는 경로(테스트·툴)는 나머지를 통과시킨다
- [x] `minimumDistanceMeters <= maximumDistanceMeters` 검사 — `soulSwapMinimumDistanceMeters <= soulSwapMaximumDistanceMeters`
- [x] `minimumAnswerCount <= maximumAnswerCount` 검사
- [x] 피해 단계가 `maximumDurability > damagedThreshold > criticalThreshold > wreckedThreshold` 순서인지 검사 — `recoveryDurability <= maximumDurability`도 함께 검사
- [x] 모든 탈락 경고 시간이 `intervalSeconds`보다 작은지 검사 — `warningSeconds`, `intenseWarningSeconds`, `executionCameraLeadSeconds` 3개 + `intense <= warning` 순서 검사
- [x] `finalDuelRacerCount`가 전체 차량 수보다 작은지 검사
- [ ] 부스트 회복량이 최대 충전량을 넘지 않도록 클램프 — 클램프가 아니라 **검증 오류로 보고**만 한다 (`overtakeRewardCharges must be <= maximumCharges`). 문서 요구는 클램프이므로 둘 중 하나로 통일 필요
- [x] 카메라 Viewport Rect가 화면 범위 안에 있는지 검사 — 크기 양수 + `0..1` 범위
- [ ] `minimumIntervalSeconds <= maximumIntervalSeconds` 검사 — `SpecialEventSettings` 자체가 없어 불가. `race.targetRaceDurationMin/Max`에는 동종 검사가 있음
- [ ] `maximumSimultaneousEvents`와 `maximumEventsPerRace`가 1 이상인지 검사 — 필드 없음
- [ ] 특수 이벤트 정의의 프리팹·경고 VFX·오디오 참조 누락 검사 — 이벤트 정의 에셋 자체가 없음
- [x] 잘못된 설정은 `OnValidate`와 런타임 시작 검증에서 구체적인 필드명과 함께 보고 — 두 경로 모두 필드명이 담긴 메시지 목록을 출력

---

## 1. 구현 현황 스냅샷 (2026-07-26)

> 아래 표는 로컬 `develop` `652e03b` + 미커밋 워킹트리의 **코드·에셋·씬 YAML 실물**을 확인해 정리했다.
> 검증 열은 `정적` = 소스·에셋 확인만 (이번 리뷰가 수행한 수준), `테스트` = EditMode 테스트 통과 확인,
> `플레이` = 사람이 Play Mode에서 확인. 이번 리뷰에서는 `정적`만 새로 확인했다.

| 시스템 | 상태 | 근거 | 검증 |
|---|---|---|---|
| 설정 시스템 (`GameBalanceSettings` 14그룹 + 프리셋 2종 + 스냅샷 공급) | 구현 | `Assets/GameBalance/Runtime/GameBalanceSettings.cs`(396줄), `GameBalance.cs`, `Assets/GameBalance/Resources/GameBalance/{GameJamDefault,FastTest}.asset` | 정적 |
| 설정 소비 커버리지 | **부분** | 위 카탈로그 표의 "선언만" 항목 다수 (퀴즈·랩 수·AI 성격 프로필·관문·특수 이벤트·오디오·추월 속박) | 정적 |
| 레이스 상태 흐름 (`Boot→PreRace→Countdown→Racing→FinalDuel→Finished`) | 구현 | `GMTKRaceState.cs`(`RacePhase` 6단계), `RaceFlow.cs`(`Phase` 5단계 → 매핑), `RaceBootstrap.cs`, `GMTKGameMode.cs` | 정적 |
| 승패 판정 단일화 | **미완** | 킷 `RaceFinish`가 `GMTK_Race`에 그대로 배치되어 랩 완주 시 독립적으로 `RaceFinishedEvent(Win/Lose)`를 던진다 | 정적 |
| 순위·진행도 (웨이포인트 arc-length) | 구현 (2026-07-26) | `TrackProgress.cs`(순수 로직) + `GmtkRaceProgress.cs`가 `RealTimeRacePositions.enabled=false`로 킷 계산을 끄고 `RacePositionTotalScores`에 **그리드 기준 주행 거리(m)** 를 기록. `Race.ScoreOf()` 소비자는 무변경 | 테스트 + 플레이 |
| 체크포인트 게이트 | 결승선 1개로 축소 | 게이트는 랩 카운터·결승선 연출·`FinalGateDoors` 기준점 용도만. `singleFinishGate` 기본 true, 트리거는 도로 폭(15 m)으로 스케일 | 플레이 |
| 랩 카운트 | 웨이포인트 공급 (2026-07-26) | `GmtkRaceProgress`가 주행 거리 ÷ 트랙 길이로 계산해 킷 `LapScores`에 기록. 게이트 1개일 때 킷 `CheckpointTracker`는 통과 직후 `currentCheckpoint=0`으로 리셋되어 **차량의 다음 콜라이더가 트리거에 닿을 때마다 랩이 또 올라가므로** 킷 카운터를 쓸 수 없다 | 플레이 |
| 탈락 (설정 간격 최하위 처형, 경고 3단, 0초 재판정, 두 대에서 중단) | 구현 | `EliminationManager.cs`(263줄), `CarExplosion.cs`, `EliminationSettings` | 정적 |
| 처형 CCTV 인셋 (메인=플레이어, 처형 대상=우하단 약 1/3) | 구현·미검증 | `ExecutionCctvDirector.cs`(593줄, Cinemachine 3.1.5 채널 분리), `CameraSettings`. **Git 미추적** | 정적 |
| 최종 관문 (규칙 + 폐쇄 연출) | 구현·플레이 미검증 | `FinalGate.cs`(규칙·시퀀스 타이밍), `FinalGateDoors.cs`(런타임 2엽 관문·폐쇄 애니메이션·통과 트리거), `PresentationSettings` 관문 6필드 | 정적 |
| 내구도·대파 | 구현 (상태머신) | `DurabilityController.cs`, `DurabilityManager.cs`, `DurabilityState.cs`, `DurabilityHud.cs`. 단계별 VFX 없음 | 정적 |
| 부스트 (2칸·1.25초·6초 재충전) | 구현 | `BoostController.cs`, `BoostManager.cs`, `BoostState.cs`, `GmtkRccpVehicle.ApplyBoost` | 정적 |
| 부스트 입력 | 구현 | `BoostController.Update`가 새 Input System의 좌·우 `Shift`를 사용. RCCP의 수동 변속과 `F` NOS 입력은 런타임 바인딩 해제로 중복 동작 방지 | 정적 |
| 저주 3종 + 공용 쿨다운 12초 | 구현 | `CurseController.cs`(284줄), `CurseManager.cs`(487줄), `CurseType.cs`, `CurseCooldownState.cs` | 정적 |
| 저주 ↔ 퀴즈 결합 방향 (방어형) | 구현·기획 확정 | **시전자가 E로 발동 → 대상이 퀴즈를 풀어 방어**(정답=무효, 오답·시간초과=적용). 2026-07-26 이 흐름을 정본으로 확정 | 정적 |
| 추월 도전 (20초 주기·8초 제한·0.5초 유지·보상) | 구현 | `OvertakeManager.cs`, `OvertakeChallengeState.cs` | 정적 |
| 추월 실패 속박(심판의 사슬) | **미완** | `BindSpeedMultiplier`를 읽는 차량·드라이버 코드가 없어 감속이 실제로 적용되지 않는다 | 정적 |
| 퀴즈 + 미니게임 (산술·수도·숫자 순서·리듬·버튼 연타 5종) | 구현 | `Assets/Quiz/Runtime/*`(17파일), 휴대폰 월드 UI, `pauseGameplayDuringQuiz` 기본 false로 레이스 계속 | 정적 |
| 퀴즈 설정화 | 구현 | `CurseManager`가 `QuizSettings`의 제한시간과 보기 수를 `QuizSessionController`에 전달. 일반 객관식은 4개 | 정적 |
| 주행 랜덤 이벤트 6종 (공사 구간·가축 횡단·비치볼·운석 상자·지진·부스트 패드) | 구현 (레거시 프로토타입) | `RandomEventManager.cs`(303줄). `GMTK_Race`에서 `enableEvents: 1`로 **활성 상태** | 정적 |
| 차량 계층 (RCCP Lite) | 구현 | `Assets/Scripts/GMTK/Rccp/*`, `GmtkVehicleAdapter.cs` | 정적 |
| AI 주행 (look-ahead·레이싱 라인·코너 속도·그리드 합류·도로 폭 프로빙) | 구현 | `GmtkRccpWaypointDriver.cs`(351줄), `GmtkRccpWaypointPath.cs`, `AiDriving.cs` | 정적 |
| AI 성격 배분·성격별 수치 설정화 | 구현 (2026-07-26) | `AISettings.personalityAssignments`가 확정 구성(폭주광 2·난폭자 1·봉쇄자 1·생존자 1)을 담고 `AiPersonalityRoster.BuildOrder(assignments, …)`가 슬롯만 셔플. 성격별 수치는 `AiPersonalityProfile` 한 행으로 통합 (`paceScale`·`lateralStrengthMetres`·`boostTendency`·`quizAvoidChance`·`catchupAcceleration`) | 정적 + 컴파일 |
| AI 부스트·AI 저주 전술 | **미구현/부분** | `TryBoost()` 호출자 없음(Reckless만 RCCP `nosInput 0.35`). AI 저주 발동은 `CurseManager.UpdateAiCasters`로 동작하나 사전 경고·연속 방지 없음 | 정적 |
| 추락 리스폰 | 구현 (커밋됨) | `GmtkRccpFallRespawner.cs`, `FallRespawnState.cs` — 둘 다 Git 추적 중 | 정적 |
| 레이스 씬·트랙 | 구현 | 베이크 산출물 전부를 `Assets/Prefab/Race Track Authoring.prefab`이 소유한다 (도로·웨이포인트 2635개/21 075 m·게이트 1개·그리드 8개). 공유 씬 `Assets/Scenes/GMTK_Race.unity`는 **51.8KB**로 트랙 데이터를 들지 않으며 베이크해도 diff가 0줄이다 | 정적 |
| Build Settings | 구현 | `ProjectSettings/EditorBuildSettings.asset`에 `Assets/Scenes/GMTK_Race.unity` 단일 씬만 enabled | 정적 |
| 팀 UI ↔ 레이스 씬 통합 | 구현 | `Assets/Prefab/UI/Main UI.prefab`이 `GMTK_Race`에 배치됨 (`StartScreen`/`Prologue`/`Countdown`/`RaceUI` 중첩, `RaceUI` 안에 `Durability`·`Curse Skill`·`Position TMP`·`Race Timer` 포함) | 정적 |
| 규칙 HUD (처형 카운트다운·최하위 경고·부스트·도전·관문) | 부분 구현 | `RaceHud`가 런타임 자동 생성되고 `RaceUI/RaceAlertPanel`의 Foozle 프레임 + TMP 3줄에 목표·처형 경고·추월/관문 문구를 출력. 월드 마커와 경고 우선순위는 미완 | 정적 + 컴파일 |
| 시작 화면·오디오 | 부분 | `StartMenuCanvas.cs`, `GameAudioManager.cs`(592줄, cue 시스템), `Assets/Sound/*` 오디오 클립 **35개**, `Audio Manager.prefab`이 `GMTK_Race`에 배치 | 정적 |
| 게임플레이 오디오 훅 | **미완** | 호출되는 것은 `PlayCountdownTick`·`PlayRaceStart`·`PlayButtonClick`뿐(모두 `RaceFlow`). 처형·저주·추월·관문·폭발·부스트 사운드 호출 지점 없음 | 정적 |
| `VehicleSettings`·`SpecialEventSettings` 설정화 | **미구현** | 차량 수치는 RCCP 프리팹과 `GmtkRccpVehicle.boostAcceleration`, 이벤트 수치는 `RandomEventManager` 인스펙터에 산재 | — |

### 자동 테스트 현황

`Assets/GameBalance/Tests/Editor` 9파일 + `Assets/Quiz/Tests/Editor` 5파일 = **14개 테스트 파일 / 132개 테스트 케이스**

| 파일 | 케이스 | 파일 | 케이스 |
|---|---:|---|---:|
| `GameBalanceSettingsTests` | 24 | `TrackProgressTests` | 14 |
| `AiDrivingTests` | 14 | `OvertakeChallengeStateTests` | 8 |
| `CurseCooldownStateTests` | 14 | `QuizSessionControllerTests` | 8 |
| `AiPersonalityRosterTests` | 12 | `QuizQuestionTests` | 5 |
| `BoostStateTests` | 12 | `FallRespawnStateTests` | 4 |
| `DurabilityStateTests` | 10 | `WorldSpaceQuizCanvasFollowerTests` | 3 |
| | | `ButtonMashProgressTests` | 2 |
| | | `NumberSequenceProgressTests` | 2 |

2026-07-26 `run_tests --mode EditMode` 실행 결과 **132개 전부 통과**.
PlayMode 자동 테스트는 없고, 대신 CLI에서 수동 생성하는 스모크 하네스 `GMTKAutoPlaytest.cs` / `RccpMigrationPlaytest.cs`가 있다.

### 알려진 미해결 항목

정적 확인으로 새로 드러난 것을 포함한다.

1. **레이스 시작 직후 여러 대가 추락 리스폰된다** — `RCCP fall respawn: GMTK_RCCP_Player -> waypoint 41`,
   `AI_1 -> 37`, `AI_3 -> 36`처럼 출발 몇 초 안에 다발로 찍힌다. 도로 콜라이더인지 그리드 높이(도로면 +0.35m)인지
   **원인 미확인**이며 화면 확인이 필요하다.
2. **킷 `RaceFinish`의 승리 조건이 랩 수와 어긋난다** — `feature/improvements`에서
   `RaceResultAuthority`가 씬 로드 시 해당 컴포넌트를 비활성화·제거하도록 코드 수정. Play Mode 확인 대기.
   랩이 이제 사실대로(주행 거리 기준) 기록되므로
   `LapScores > LapsSelected`는 선택한 랩 수 **+1랩**을 요구한다. 예전 킷 카운터는 출발선 유령 랩을 전제로 해서
   선택한 랩 수에 끝났다. 우리 승패는 탈락·최종 관문이 결정하고 `RaceFinish` 제거가 이미 미해결 항목이므로,
   제거하거나 조건을 조정해야 한다.
3. **씬에 `AudioListener`가 2개** — 플레이 중 매 프레임 "There are 2 audio listeners in the scene" 로그가
   찍혀 콘솔 버퍼(800줄)를 6초마다 밀어낸다. 실제 에러가 콘솔에서 사라지므로 진단을 방해한다.
4. **킷 `RaceFinish`가 `GMTK_Race` YAML에는 남아 있다.** 런타임 권한 계층이 씬 로드 시 제거해
   승패 발행을 막는다. 제거 시 이벤트 구독을 해제하고 `finishTrigger`를 null-safe 처리해 재시작
   이벤트 체인이 끊기던 문제는 코드 수정 완료했으며 Play Mode 확인 대기.
5. ~~**`RaceHud`가 어디에도 배치되지 않았다.**~~ → **중앙 상단 규칙 HUD 통합 완료.**
   `RaceHud`가 런타임 자동 생성되어 `RaceUI/RaceAlertPanel`의 TMP를 갱신한다. 월드 마커와 경고 우선순위는 남아 있다.
6. **`GameJamDefault.maximumDurability = 1000000`** — 대파를 미루기 위한 임시값. 최종 밸런스 값은
   팀이 정하기로 했고, 프리셋 정규화 도구도 이 값을 덮지 않는다.
7. **추월 실패 속박이 실제로 감속하지 않는다** (`BindSpeedMultiplier` 소비자 없음).
8. **저주 사전 경고와 연속 저주 유예가 없다.** 방어형 흐름 자체는 확정·구현이지만, AI가 표적을 연속으로
   찍는 것을 막는 유예(`hostileEffectGraceSeconds`)와 "표적이 됐다"는 사전 신호가 없다.
9. RCC 차량 렌즈 플레어가 URP에서 표시되지 않음 (`LegacyLensFlareUrpBridge` 미검증).
10. 레거시 랜덤 이벤트가 `GMTK_Race`에서 켜져 있다 (`enableEvents: 1`).
11. 관문 슬램 SFX 음원이 없어 `SFX_VEH_COLLISION_`이 플레이스홀더로 쓰인다 (3.14 범위).

**2026-07-26에 해소된 항목**

- ~~`BoostController`가 레거시 `Input` API를 사용한다~~ → 새 Input System의 좌·우 `Shift`로 이전하고,
  RCCP의 수동 변속 및 `F` NOS 액션을 런타임에 해제했다. RCCP NOS의 배기 불꽃은 토크 로직 없이
  `BoostController.IsBoosting` 동안 재사용한다.
- ~~트랙 산출물이 씬과 두 프리팹에 흩어져 있다~~ → 웨이포인트·게이트·스폰 포인트·도로·그리드를 모두
  `Race Track Authoring.prefab`이 소유한다. `aiWaypoints`/`checkpoints`/`spawnPoints` 참조도 프리팹 내부
  참조가 되어 공유 씬은 5.78MB → 51.8KB, **베이크 1회당 씬 diff 0줄**(프리팹 2 380줄)이 됐다. Bake Track이
  프리팹 반영까지 자동으로 하므로 사람이 Apply를 누를 일은 없다. 미사용이 된 `AI Waypoints.prefab`은
  참조 0건으로 남아 있다(삭제 여부 미결).
- ~~`Spawning`의 `Spawn Points` 14개~~ → 씬 전체 참조 0건을 확인하고 삭제했다. 스폰 위치는 베이크된
  `__GeneratedTrack/Starting Grid` 8개가 공급한다.
- ~~`GMTKAutoPlaytest`가 FastTest 프리셋을 남긴다~~ → 플레이 모드 도메인 리로드가 꺼져 있어 `GameBalance`
  정적 상태가 다음 세션까지 살아, 스모크 실행 뒤 일반 플레이에서도 **3초마다 처형**이 났다. 하네스가 끝날 때
  원래 프리셋과 `RaceData.AiBotsSelected`/`LapsSelected`를 복원한다. 하네스가 1프레임에 Play를 눌러
  `RaceFlow`·`GMTKRaceState`가 이벤트를 놓치던 문제도 함께 고쳤다.
- ~~체크포인트 게이트가 재베이크 대기 중(컴포넌트 2배·미배선 467개로 매 프레임 NRE)~~ → 게이트를 결승선
  1개로 줄이고 순위를 웨이포인트 진행도로 옮겼다. 생성기 쪽 결함도 함께 고쳤다: `ClearGenerated`가
  `Transform.Find`로 첫 생성 루트만 지워 `__GeneratedTrack`이 2개인 씬에서 수렴하지 못했고, 그 탓에
  `PersistGeneratedMesh`가 낡은 Road(= 도로 메시 에셋 자신)를 `Clear()`해 에셋을 비웠다.
  씬 레벨 전체 베이크는 웨이포인트 2635개를 프리팹에서 씬 override로 끌어와 씬을 5.8MB→12.9MB로
  부풀리므로, 게이트 수정은 새 **Bake Checkpoints Only** 버튼을 쓴다.
- ~~`FinalGate`에 씬 트리거가 없다~~ → `FinalGateDoors`가 결승선 체크포인트 위에 관문과 통과 트리거를
  런타임 생성해 `ReportGateCrossing()`을 호출한다. 20초 타임아웃 폴백도 `duelCars[0]`(최저 인덱스)에서
  실제 선두(`Race.ScoreOf` 최대) 판정으로 교체했다. **남은 일: Play Mode에서 관문 위치·방향·폭(16m)과
  폐쇄 시퀀스 확인.**
- ~~`GameJamDefault.elimination.intervalSeconds = 10`이고 `warningSeconds`와 같다~~ → GDD 기준값 30으로
  정상화했고 `Validate()`가 통과한다. 에셋 값 자체를 고정하는 EditMode 테스트를 추가했다.
- ~~AI 성격 구성이 확정안과 다르다~~ → 구성이 `AISettings.personalityAssignments`로 옮겨져 프리셋이
  정한다. `AIArchetype`/`AIArchetypeProfile`은 제거했고, 프리셋의 폐기된 `archetypeAssignments`·
  `profiles` 키도 Unity 재저장으로 정리되어 잔존 0건이다.
- ~~프리셋 2종과 `ExecutionCctvDirector.cs`가 Git 미추적~~ → 모두 추적 중이며 원격에 올라가 있다.

---

## 2. 기존 구현 세부 (RCCP 전환 전 기준)

### 2.0 차량과 레이스 기반 (킷 기준 항목)


- [x] 플레이어 차량 가속·제동·후진·조향
- [x] 핸드브레이크 입력
- [x] 휠과 노면 기반 차량 물리
- [x] 차량 간 충돌
- [x] 스키드와 타이어 마찰 기반
- [x] 체크포인트 순서 검증
- [x] 랩 진행과 완주 처리
- [x] 랩·체크포인트·구간 거리 기반 실시간 순위
- [x] 플레이어와 AI 차량 스폰
- [x] 출발 카운트다운
- [x] 기본 승리·패배 결과 화면
- [x] 일시정지와 기본 재시작

### 2.1 AI 기반 (킷 AI는 폐기, RCCP 드라이버로 대체)

- [x] AI 웨이포인트 주행 — RCCP `GmtkRccpWaypointDriver`로 재구현 (킷 `CarAIControl` 폐기)
- [x] 코너 진입 감속 — 제동거리 스캔 기반 목표 속도 (`AiDriving.CornerScanMetres` → `CornerSpeedKph` → `SmoothTargetSpeedKph`)
- [ ] 전방 장애물 감지와 회피 조향 — **미구현.** `GmtkRccpWaypointDriver`의 `Physics.Raycast` 2곳은 `ProbeEdge`(도로 폭 측정)용이며 차량·해저드를 피하는 로직은 없다
- [x] 정체 시 후진 복구 — `GmtkRccpWaypointDriver.UpdateRecovery` (`stuckSpeedKph`/`stuckDelaySeconds`/`reverseDurationSeconds`)
- [ ] 장시간 복구 실패 시 체크포인트 리스폰 — `GmtkRccpFallRespawner`는 커밋되어 있으나 트리거 조건이 **추락(트랙 아래로 떨어짐)** 뿐이다. 정체가 계속될 때의 리스폰 경로는 없다
- [x] AI 성격별 속도와 주행 성향 적용 구조 — `AIPersonality.SpeedMultiplier`(0.97~1.15) + 램/블록 측면 오프셋. **차이가 미미해 확대 필요**
- [x] `CleanRacer`, `Rammer`, `Blocker`, `Reckless` 성격 — `AIPersonalityType` 4종, 균등 셔플 배분

### 2.2 탈락

- [x] 현재 최하위 차량 검색
- [x] 시간 기반 최하위 차량 제거
- [x] 플레이어 탈락 시 패배 처리
- [x] AI 차량 탈락 처리
- [x] 탈락 차량 제어 비활성화
- [x] 폭발 물리 힘과 회전
- [x] 간단한 폭발 플래시와 발광 효과
- [x] 일정 시간 후 잔해 비활성화

### 2.3 퀴즈 시스템

- [x] 데이터 기반 문제와 문제 풀
- [x] 무작위 문제 선택
- [x] 정답·오답·시간 초과 판정
- [x] 월드 스페이스 휴대폰 UI
- [x] TextMeshPro 기반 문제와 답안 표시
- [x] 마우스 답안 선택
- [x] 수학·수도·숫자 순서·리듬·버튼 연타 문제 — `ProceduralQuizQuestionSource` 5종 + 같은 종류 연속 출제 방지
- [x] 퀴즈 시작·판정·종료 이벤트 — `QuestionStarted` / `TimerChanged` / `AnswerEvaluated` / `QuizClosed`
- [x] 퀴즈 EditMode 테스트 — 실제 케이스 수는 **20개** (5파일)

### 2.4 카메라·UI·빌드 기반

- [x] 기본 3인칭 추격 카메라 — RCCP `RCCP_Camera` (킷 `PlayerCarCameraController` 폐기). `GmtkRccpVehicle.AttachChaseCamera`가 직접 타겟 지정
- [x] 차량 속도 기반 기본 추종
- [ ] 복수 카메라 전환 — 처형 CCTV 인셋은 `ExecutionCctvDirector`로 **코드 구현 완료**(Cinemachine 채널 01 분리, 별도 출력 카메라 depth+1). Play Mode·WebGL 성능 미검증
- [x] 기본 순위·랩·출발 카운트다운 UI — 킷 GUI + `Main UI.prefab`
- [x] Unity `6000.3.19f1` 고정
- [x] Windows 64-bit와 WebGL 빌드 검증 — 이전 세션 기록. 이번 리뷰에서 재확인하지 않음
- [x] 활성 씬과 Git LFS 검증 — Build Settings에 `GMTK_Race.unity` 단일 씬 enabled 확인
- [x] 레이싱 헤드리스 및 퀴즈 Play Mode 검증 — 이전 세션 기록. 이번 리뷰에서 재확인하지 않음

---

## 2. 구현되어 있지만 GDD에 맞게 수정해야 하는 내용

### 2.1 레이스 설정

실제 차량 수·랩 수는 `GameBalanceSettings`가 아니라 **킷 선택 UI**에서 온다.
`GmtkRccpPlayersSpawner`는 `RaceData.AiBotsSelected`를, 킷 `RaceFinish`는 `RaceData.LapsSelected`를 읽고,
그 값은 `Main UI.prefab`의 `BotSelectorGUI.defaultQuantity = 5` / `LapSelectorGUI.defaultQuantity = 1`이 정한다.

- [x] 플레이어 1대와 AI 5대로 변경 — `BotSelectorGUI.defaultQuantity: 5` (프리팹 실물 확인). 그리드 스폰 포인트 8개로 수용 가능
- [x] 총 참가 차량 6대로 변경 — 위와 동일 경로. `race.aiCount: 5`도 프리셋에 저장되어 있으나 스폰에는 쓰이지 않는다
- [x] 목표 지점까지 달리는 1랩으로 변경 — `LapSelectorGUI.defaultQuantity: 1` (프리팹 실물 확인)
- [ ] 스폰·랩 수가 `RaceSettings`를 실제로 참조하도록 연결 — **미완.** 현재 `race.aiCount` / `race.lapCount`는 선언만 되어 있어, 프리셋만 바꿔서는 차량 수·랩 수가 변하지 않는다
- [ ] 문서에 등장하는 모든 런타임·밸런스·연출 수치를 `GameBalanceSettings`로 이전 — 위 카탈로그 표의 "선언만"/"미구현" 항목 잔존
- [ ] 게임 잼 빌드에서 레이스 설정 선택 UI 숨김 — `Bot Selector GUI` / `Lap Selector GUI`가 `Main UI.prefab`에 그대로 노출
- [ ] 재시작 후에도 같은 설정 유지 — 스냅샷 구조와 커스텀 카운트다운 재진입은 구현, Play Mode 미검증

### 2.2 조작감과 드리프트

`Assets/Scripts/GMTK` 전체에 드리프트·조향 보조·스핀 억제 코드가 없다
(`rg -i "drift|steerAssist|antiSpin|traction"` → 히트 없음, `handbrakeInput = 0f` 한 줄뿐).
차량 거동은 전부 RCCP 프리팹 튜닝에 맡겨져 있다.

- [ ] 저속 조향 보조와 고속 조향 감쇠 — AI 드라이버에만 `steerGainLowSpeed/HighSpeed`가 있고 플레이어 경로에는 없음
- [ ] 측면 충돌 후 트랙션 안정화
- [ ] 의도치 않은 스핀 억제
- [ ] 반대 방향을 향했을 때 빠른 방향 복구
- [ ] 관대한 트랙 이탈 리셋 — 추락 시 리스폰(`GmtkRccpFallRespawner`)만 있고 트랙 이탈·역주행 리셋은 없음
- [ ] 기본 아케이드 드리프트 진입
- [ ] 드리프트 중 조향과 횡방향 미끄러짐 조절
- [ ] 드리프트 종료 후 빠른 그립 복구
- [ ] AI 차량 질량과 충격 전달 조정

### 2.3 탈락 규칙

`EliminationManager`는 이미 모든 타이밍을 `EliminationSettings`에서 읽고, 첫 탈락도 한 주기 뒤에 발생한다.
남은 문제는 프리셋 값(현재 10초)과 경고의 화면 표현이다.

- [x] 기존 `EliminationManager` 내부 수치를 `EliminationSettings` 참조로 교체 — `E.intervalSeconds` / `warningSeconds` / `intenseWarningSeconds` / `executionCameraLeadSeconds` 참조 확인. 폭발 힘 3개만 컴포넌트 필드로 남음
- [ ] 첫 탈락과 이후 탈락을 모두 30초 간격으로 변경 — 코드는 첫 탈락도 동일 주기로 처리(`nextEliminationTime = Time.time + E.intervalSeconds`)하지만 **`GameJamDefault` 값이 10초**다
- [ ] 10초 전 최하위 경고 표식 — `EliminationWarningLevel.Warning` + `WarningChanged` 이벤트는 있으나 이를 표시하는 HUD(`RaceHud`)가 씬에 없고 월드 마커도 없음
- [ ] 5초 전 시각·오디오 강도 증가 — `Intense` 레벨 이벤트만 존재. 시각·오디오 구현 없음
- [x] 3초 전 처형 화면 연출 시작 — `Execution` 레벨에서 `ExecutionTargetChanged` → `ExecutionCctvDirector.ShowLiveTarget`. Play Mode 미검증
- [x] 0초에 최신 순위 재검증 후 최하위 처형 — `EliminateLastPlace()`가 `FindLastPlace()`를 다시 호출한 뒤 `LockedEliminationIndex`를 확정
- [x] 두 대가 남으면 탈락 중단 — `ActiveCarCount <= FinalDuelCount`에서 `EnterFinalDuel()`, `armed = false`
- [x] 두 대가 남으면 최종 관문 단계로 전환 — `FinalDuelStarted` → `GMTKRaceState`가 `RacePhase.FinalDuel`, `FinalGate.OnFinalDuel`
- [x] 한 대만 남았을 때 자동 승리하는 기존 규칙 제거 — `EliminationManager`에 자동 승리 경로 없음. 단 킷 `RaceFinish`의 랩 완주 승리 경로는 아직 살아 있음(2.7 참조)

### 2.4 순위와 진행도

모든 규칙 시스템은 킷 `RealTimeRacePositions.RacePositionTotalScores`를 `Race.ScoreOf()`로 공유한다.
동률 처리와 안전 위치 제공은 코드에 없다.

- [x] 순위표·탈락·저주·위치 교환·추월·리스폰이 같은 진행도 사용 — `EliminationManager` / `CurseController` / `OvertakeManager` / `RaceHud` 모두 `Race.ScoreOf()` 사용
- [x] 진행도 해상도 — 순위 점수가 8 m 간격 웨이포인트 폴리라인의 arc-length 투영값(m)이라 구간 내 보간이 연속적이다 (`TrackProgress`)
- [x] 탈락 차량 순위 제외 — `EliminationManager.eliminated` 집합을 `FindLastPlace` / `PickRivalAhead` / `SelectTarget` / `RaceHud.GetRank`에서 모두 건너뜀
- [x] 탈락 직전 순위 강제 갱신 — 0초에 `FindLastPlace()` 재호출
- [ ] 동률 시 트랙상 앞선 차량 우선 — 동점 시 인덱스가 낮은 쪽이 선택된다(`score < lowest` 비교). 명시적 타이브레이크 없음
- [ ] 완전 동률이면 이전 순서 유지 — 안정화 로직 없음
- [ ] 안전한 트랙 위치·방향·횡방향 위치 제공 — `GmtkRccpWaypointPath.TryGetRespawnPose`가 리스폰에만 쓰이고, 영혼 교환은 이를 사용하지 않는다

### 2.5 퀴즈 실행 방식

현재 퀴즈는 저주 대상이 인간 플레이어일 때만 실행되며, 정답이면 저주를 방어하고
오답 또는 시간 초과면 페널티를 받는다.

- [x] 퀴즈 시간·답안 수를 `QuizSettings` 참조로 교체 — `CurseManager`가 제한시간과 최소·최대 보기 수를 생성기에 전달. 출제 간격은 저주 시스템이 관리
- [x] 레이스 중 자동 퀴즈 실행 중단 — `TriggerNow()`의 유일한 호출자가 `CurseManager.BeginHumanQuiz`
- [x] 저주 활성화 시에만 퀴즈 시작 — 동일 근거. `RaceFlow`는 Racing 진입 시 컴포넌트만 enable
- [x] 퀴즈 중 레이스와 차량 물리를 멈추지 않음 — `pauseGameplayDuringQuiz` 기본 false, 타이머는 `Time.unscaledDeltaTime` 사용
- [x] 일반 객관식 문제의 보기를 4개로 고정 — 산술·수도 문제는 정답 1개와 오답 3개. 숫자 순서 등 인터랙티브 미니게임은 별도 입력 규칙 사용
- [x] 마우스 입력을 주 입력으로 유지 — `QuizUiPresenter` 버튼 + `InputSystemUIInputModule`
- [x] 정답 시 저주 페널티 없이 방어 성공 — `OnHumanQuizEvaluated`가 `IsCorrect`면 아무 효과도 적용하지 않음
- [x] 오답·시간 초과 시 저주 페널티 적용 — `ApplyPenalty(type, targetIndex)` + 화면 피드백 라벨
- [x] 저주 발동 시 시전자 쿨다운 시작 — `CurseController.CastAtAutoTarget`이 성공 시 즉시 `State.OnCastSucceeded()`
- [x] 기존 영어 문제 표시 정책 유지 — 생성기가 영어 문자열만 생성

> **2026-07-26 확정:** 이 방어형 흐름이 정본이다. GDD 초안의 "시전자가 퀴즈를 풀어 적용"은 폐기했다.
> 플레이어가 저주를 쓸 때는 퀴즈가 뜨지 않고, 퀴즈는 항상 "내가 표적이 됐다"는 신호다.
> 상세 근거와 파생 규칙은 문서 앞부분 "기획 확정 — 저주 발동 흐름 (방어형)" 참고.

### 2.6 AI 성격과 행동

**2026-07-26 확정 축:** `GMTK.AIPersonalityType` 4종 — 폭주광(`Reckless`) · 난폭자(`Rammer`) ·
봉쇄자(`Blocker`) · 생존자(`CleanRacer`). 확정 구성은 5대 = 폭주광 2 + 난폭자 1 + 봉쇄자 1 + 생존자 1.
설정의 `AIArchetype`/`AIArchetypeProfile`은 폐기한다. 상세는 문서 앞부분 "기획 확정 — AI 성격 축" 참고.

- [x] 속도·측면 강도·부스트 성향·저주 방어율·따라잡기 수치를 `AISettings`에 성격별로 저장 — `AiPersonalityProfile` 한 행에 `paceScale`·`lateralStrengthMetres`·`boostTendency`·`quizAvoidChance`·`catchupAcceleration`. 흩어져 있던 세 곳(드라이버 `switch`, 공용 ram/block 강도, `CurseSettings` 방어율)을 통합했다. **컴파일 확인, 테스트 미실행**
- [x] `AIArchetype`·`AIArchetypeProfile`·`archetypeAssignments` 제거 — 코드에서 제거 완료. `AIPersonalityType`은 설정이 키로 쓸 수 있도록 `Assets/GameBalance/Runtime/AIPersonalityType.cs`(`Gmtk2026.GameBalance`)로 이동했다 (`CurseType`과 같은 패턴, 선언 순서는 직렬화 호환을 위해 유지)
- [x] 난폭자 구현 — `AIPersonalityType.Rammer`, 플레이어 쪽 조준점 당김(`GetTargetOffset`), 측면 강도 7m
- [x] 폭주광(스피드스터) 구현 — `AIPersonalityType.Reckless`, `paceScale 1.0` + RCCP `nosInput 0.35`
- [x] 봉쇄자(책략가 재정의) 구현 — `AIPersonalityType.Blocker`, 플레이어 차선으로 측면 밀어붙임, 측면 강도 5m
- [x] 생존자 구현 — `AIPersonalityType.CleanRacer`, 오프셋 없이 레이싱 라인 주행 + 저주 방어율 0.8
- [x] AI 5대에 확정 구성대로 배분 — `personalityAssignments`가 폭주광 2 + 난폭자 1 + 봉쇄자 1 + 생존자 1을 담고, 로스터는 슬롯만 셔플한다. 코드 배열 순서와의 결합을 없앴다
- [ ] 성격별 위협 차이 확대 — `paceScale` 폭이 0.86~1.0으로 좁다. 이제 한 곳(`personalityProfiles`)에서 조정할 수 있으므로 Play Mode 튜닝만 남았다
- [ ] 직선과 긴급 순위 회복 시 부스트 사용 — `BoostController.TryBoost()` 호출자 없음. 폭주광의 NOS는 RCCP 자체 기능이라 `BoostSettings`와 무관하다
- [ ] 탈락 카운트다운 반응 — AI가 `EliminationManager` 이벤트를 구독하지 않음
- [ ] 멀리 뒤처진 AI에 작은 가속 보정 — `catchupAcceleration` 미참조
- [ ] 플레이어가 최하위일 때 AI 공격성 감소
- [ ] 최종 관문 라이벌 강화
- [ ] 눈에 띄는 순간이동 금지 — AI 순간이동 코드는 없으나 `GmtkRccpFallRespawner`의 추락 리스폰은 즉시 이동한다(추락 시에만 발생)

> "단순화된 저주 전술"과 "저주 중심 책략가" 항목은 방어형 흐름 확정으로 폐기했다.
> 저주 발동은 쿨다운만 차면 성격과 무관하게 일어나고, 성격 차이는 **방어율**로 표현한다 (3.11).

### 2.7 승패·재시작·특수 이벤트

- [ ] 기본 랩 완주 승리를 관문 통과 승리로 변경 — **미완.** 킷 `RaceFinish`가 `GMTK_Race`에 배치된 채 `OnLapCompleted`에서 `RaceFinishedEvent(Win/Lose)`를 던진다
- [ ] 중앙 레이스 관리자만 승패 판정 — **미완.** 승패를 던지는 지점이 3곳(`EliminationManager.FinishRace`, `FinalGate.ResolveWinner`, 킷 `RaceFinish`)
- [ ] 플레이어 폭발 후 3초 이내 패배 화면 — `result.playerDeathCinematicMaxSeconds` 미참조, 지연 연출 없음
- [ ] 패배부터 재조작까지 5초 이내 유지 — `result.restartToControlMaxSeconds` 미참조
- [ ] 재시작 시 차량·AI·타이머·퀴즈·저주·도전·관문 초기화 — 각 매니저의 `RestartRaceEvent` 초기화와 `RaceFlow`의 프롤로그 없는 READY/3·2·1/GO 재진입을 연결. Play Mode 미검증
- [x] `RaceFinish`의 Finish Trigger 참조 안전성 검증 — null guard와 `OnDestroy` 이벤트 구독 해제로 죽은 리스너가 재시작 체인을 끊지 않게 수정
- [ ] 현재 `RandomEventManager`를 설정 기반 `SpecialEventDirector`로 리팩터링 — 미착수
- [ ] 기존 지진과 운석 로직은 재사용 가능한 부분만 유지 — 현재 지진은 **위 방향** `VelocityChange`(4~7) + 랜덤 2로 차를 띄우고, 운석은 물리 상자를 위에서 떨어뜨린다. GDD의 횡방향 흔들림·경고 반경 피해와 형태가 다르다
- [ ] 덤프트럭 습격 이벤트 추가 — 미착수
- [ ] 소·비치볼·공사 구간·부스트 패드는 `GameJamDefault` 이벤트 목록에서 제외 — 이벤트 목록 자체가 설정에 없음. `RandomEventManager.TriggerRandomEvent`가 6종을 코드 `switch`로 균등 추첨
- [ ] 전용 특수 이벤트 단계 전까지 레거시 자동 이벤트 실행 중단 — **미완.** `GMTK_Race.unity`의 `enableEvents: 1`, `firstEventDelay: 8`, `intervalRange: 9~16`으로 켜져 있다

---

## 3. 새로 구현해야 하는 내용

## P0 — 레이스를 성립시키는 기능

### 3.1 레이스 설정과 상태

- [x] `GameBalanceSettings`와 모든 하위 설정 그룹 구현 — 14그룹. `VehicleSettings`/`SpecialEventSettings`는 별도 미구현 항목으로 추적
- [x] `GameJamDefault`와 `FastTest` 설정 프리셋 구현 — 두 에셋 모두 실물 확인 (Git 미추적 상태는 별도 항목)
- [x] 현재 선택된 프리셋을 공급하는 런타임 설정 제공자 구현 — `GameBalance.Active` / `Current` / `Load` / `BeginRace` / `EndRace`
- [ ] 기본 차량 6대·1랩·탈락 30초를 `GameJamDefault`에 저장 — 6대·1랩 값은 저장되어 있으나 **`intervalSeconds`가 10초**이고, 저장된 `aiCount`/`lapCount`는 런타임이 읽지 않는다
- [x] 저주 쿨다운 12초를 `GameJamDefault`에 저장 — `sharedCooldownSeconds: 12`, `CurseCooldownState`가 소비
- [x] 부스트 2칸·1.25초 지속·6초 재충전을 `GameJamDefault`에 저장 — 세 값 모두 저장·소비 확인
- [x] 부팅 → 메뉴 → 인트로 → 출발 → 탈락 → 최종 결투 → 승패 → 결과 상태 흐름 — `RacePhase` 6단계 (`GMTKRaceState`), `RaceFlow.Phase` 5단계를 매핑
- [ ] 상태 전환 권한을 중앙 레이스 관리자에 집중 — 상태 전환 자체는 `GMTKRaceState`가 단독으로 하지만, **승패 확정 지점이 3곳**이라 결과 판정 권한은 집중되지 않았다 (2.7 참조)

### 3.2 부스트

- [x] 충전량·지속시간·재충전·가속 배율을 `BoostSettings`에서 읽음 — `BoostState`가 `maximumCharges`/`durationSeconds`/`rechargeSecondsPerCharge`/`boostSpeedMultiplier` 사용
- [x] `Shift` 부스트 입력 — 새 Input System의 좌·우 `Shift` 사용. RCCP의 `Gear Shift Up`과 `F` NOS는 런타임 바인딩 해제로 중복 입력 차단
- [x] 최대 2칸, 사용 시 한 칸 소비
- [x] 1.25초 추가 가속 — `GmtkRccpVehicle.ApplyBoost`가 `(배수-1) × boostAcceleration`을 `ForceMode.Acceleration`으로 가함
- [x] 6초마다 한 칸 재충전
- [x] 부스트 VFX와 사운드 — `BoostController.IsBoosting` 동안 RCCP NOS의 배기 불꽃·색상·라이트를 재사용하고, `GameAudioManager`가 시작·루프·종료·충전·봉인 큐를 재생
- [ ] 부스트 충전량 HUD — `RaceHud`에 문자열은 있으나 **`RaceHud`가 씬에 배치되지 않아 표시되지 않는다**
- [x] 추월 성공 시 한 칸 회복 — `OvertakeManager.OnSuccess` → `BoostController.RewardOvertake`
- [x] 엔진 봉인 중 사용과 충전 중단 — `BoostState.ApplySeal`

### 3.3 처형 화면

> 상태: **코드 구현 완료·Play Mode 미검증.** `ExecutionCctvDirector`(593줄)가 Cinemachine 3.1.5의
> `OutputChannels.Channel01`로 전용 브레인/가상 카메라를 만들고, 플레이어 카메라는 rect·depth를 건드리지
> 않고 그대로 둔 채 CCTV 출력 카메라만 `depth + 1`로 위에 겹친다. 처형 시
> `Model_Skyline (Breakable)`을 생성해 조각 물리를 적용하고 내부 폭발 파티클 3종 중 하나를 무작위 재생한다.

- [x] 시작 시점·Viewport Rect·전환 시간·복귀 시간을 `CameraSettings`에서 읽음 — 시작 시점은 `elimination.executionCameraLeadSeconds`, 나머지는 `camera.executionCctvViewportRect`/`executionTransitionSeconds`/`executionReturnSeconds`
- [x] 3초 전 처형 대상 카메라 활성화 — `EliminationWarningLevel.Execution` → `ExecutionTargetChanged` → `ShowLiveTarget`. 대상은 잠기지 않고 매 프레임 현재 꼴찌를 따라간다
- [x] 플레이어 카메라를 원래 메인 화면에 유지 — `CapturePlayerCamera`가 원본 rect/depth를 저장만 하고 변경하지 않는다
- [x] 처형 CCTV를 오른쪽 아래로 표시 — `GameJamDefault` rect `x .66 / y 0`
- [x] 처형 CCTV를 약 1/3 크기로 표시 — `w .34 / h .34`
- [x] 메인 플레이어 화면에서 계속 조작 가능 — 입력·차량 제어를 건드리는 코드가 없다 (Play Mode 확인 필요)
- [x] 처형 종료 후 CCTV가 닫히고 플레이어 화면 유지 — 대상 확정 시 CCTV를 월드 고정 앵커에 잠그고 원본 차량 Rigidbody를 고정한다. `CarEliminated` → `wreckLingerSeconds - 0.1s` 후 축소 애니메이션 → `CompleteHide`에서 원본 rect/depth 복원
- [ ] 플레이어 자신이 대상일 때 패배 연출로 전환 — 플레이어 차량 파괴와 CCTV 잔해가 `wreckLingerSeconds` 동안 보인 뒤 패배 결과를 발행하도록 코드 구현. Play Mode에서 실제 시야·전환 확인 필요
- [ ] 복수 카메라 렌더링의 WebGL 성능 확인 — 미검증

### 3.4 최종 관문

> 상태: **코드 구현·플레이 미검증.** 규칙과 시퀀스 타이밍은 `FinalGate.cs`가, 관문 실물과 폐쇄 연출은
> `FinalGateDoors.cs`(신규)가 담당한다. 관문은 결승선 체크포인트 위에 런타임 생성되므로 씬 배선이 없다
> (다른 GMTK 매니저와 같은 `AutoAttach` 방식). 통과 트리거가 `ReportGateCrossing`을 호출하므로 실제
> 주행으로 승리가 성립한다. 두 스크립트가 **같은 `PresentationSettings` 필드**를 읽어 애니메이션과 판정이
> 어긋날 수 없다. 화면 구도(관문 위치·방향·폭 16m가 트랙에 맞는지)와 시퀀스 체감은 Play Mode 확인 필요.

- [x] 관문 폐쇄 속도·지연·처형 지연·애니메이션 시간을 `PresentationSettings`에서 읽음 — `gateOpenDurationSeconds`·`gateWidthMeters`·`gateHeightMeters`·`gateCloseDelaySeconds`·`gateCloseDurationSeconds`(0.75)·`gateExecutionDelaySeconds`(0.25) 신설, 검증 6줄 추가. 아무도 읽지 않던 `gateCloseSpeed`는 제거하고 컴포넌트의 `gateDurationSeconds = 20f` 리터럴도 제거
- [ ] 닫히는 탈출문 오브젝트 — `FinalGateDoors.BuildGate`가 2엽 문짝 + 하우징을 런타임 생성. 방향은 직전 체크포인트→결승선 벡터로 산출해 체크포인트 프리팹 회전에 의존하지 않는다. **화면상 배치 미검증**
- [x] 최종 두 대에서 관문 단계 활성화 — `EliminationManager.FinalDuelStarted` → `FinalGate.OnFinalDuel`이 생존자 목록을 만들고 `IsOpen = true`, `GateOpened` 발행
- [x] 첫 번째 관문 통과 차량 판정과 승리 — `FinalGateCrossingTrigger`가 차량 루트를 raceIndex로 해석해 `ReportGateCrossing` 호출. 호출자가 `GMTKAutoPlaytest`뿐이던 문제 해소
- [ ] 승자 뒤에서 관문 즉시 폐쇄 — `GateSlamming` → `gateCloseDelaySeconds` 후 `gateCloseDurationSeconds` 동안 SmoothStep 폐쇄. 문짝 콜라이더를 유지해 닫히면 실제로 막힌다. **연출 체감 미검증**
- [x] 패자 처형 — `CloseGateThenExecute`가 문이 닫힌 뒤 `gateExecutionDelaySeconds`(0.25초) 후 처형. GDD 지연 규정 반영
- [ ] 관문 애니메이션과 충돌 판정 동기화 — 문짝이 콜라이더를 항상 갖고 이동하므로 보이는 것과 막히는 것이 같은 지오메트리. **다만 Rigidbody 없이 transform 이동이라 밀착 시 관통 가능, Play Mode 확인 필요**
- [ ] 승리 관문 시퀀스 — 폐쇄 → 처형 → 결과 보고 순서로 구성. 플레이어가 패자면 자기 차량 파괴를 `wreckLingerSeconds` 동안 보여준 뒤 결과를 보고한다. 전용 카메라 프레이밍과 승리 연출은 3.13 범위로 남음
- [x] 20초 타임아웃 시 `duelCars[0]` 자동 승리 규칙 제거 또는 정당화 — 타임아웃 시 `LeadingDuelCar()`로 실제 선두(`Race.ScoreOf` 최대)를 승자로 판정. 낮은 인덱스가 무조건 이기던 버그 수정
- [ ] 관문 슬램 SFX — `Assets/Sound`에 음원이 없어 `SFX_VEH_COLLISION_`을 플레이스홀더로 인스펙터 노출 (3.14 범위)

### 3.5 핵심 HUD와 전용 씬

> 상태: 전용 씬과 팀 UI 통합 완료. `RaceHud`는 런타임 자동 생성되고
> `RaceUI/RaceAlertPanel`의 Foozle `Panel_1` 배경과 TMP 3줄을 갱신한다.
> `GMTK_Race`에는 `Main UI.prefab`이 있고 그 안의 `RaceUI`가 속도 게이지, `Position TMP`,
> `Race Timer TMP`, `Durability` 슬라이더, `Curse Skill` 슬라이더를 제공한다.

- [x] 현재 순위와 생존 레이서 수 표시 — `RacePositionGUI`가 `Position TMP`에 순위 숫자, `nd TMP`에 `/생존수` 출력
- [ ] 30초 처형 카운트다운 상시 표시 — `Execution TMP`가 경고 단계에서는 남은 시간을 표시하지만 평상시에는 숨김
- [ ] 최하위 경고와 월드 마커 — 중앙 `Execution TMP` 경고는 구현, 차량 위 월드 마커는 없음
- [x] 플레이어 최하위 바이탈 경고 — `ESCAPE LAST PLACE BEFORE THE TIMER HITS ZERO` 및 `YOU ARE MARKED`
- [ ] 부스트·내구도·저주·쿨다운 표시 — 내구도(`DurabilityHud` → `RaceUI/Durability`)와 저주 쿨다운(`CurseManager` → `RaceUI/Curse Skill` 슬라이더)은 배치 확인. **부스트 충전량은 표시 수단 없음**
- [ ] 저주 대상 마커 — 없음
- [x] 퀴즈 문제와 마우스 답 입력 — `QuizUiPresenter` + 월드 스페이스 휴대폰
- [x] 추월 도전 대상과 타이머 — `RaceAlertPanel/Overtake TMP`에 대상 차량과 남은 시간 표시
- [ ] 처형 경고 → 추월 도전 → 퀴즈 → 쿨다운 순서로 강조 — 우선순위 로직 없음
- [ ] 시작 직후 기본 조작 키 가이드 — `RaceFlow` 프롤로그 텍스트는 규칙 설명만 하고 조작 키는 안내하지 않는다
- [x] 1랩·6대·최종 관문을 포함한 게임 전용 씬 — `Assets/Scenes/GMTK_Race.unity`. 단 관문 오브젝트는 아직 없음(3.4)
- [x] 전용 씬 Build Settings 등록 — `EditorBuildSettings.asset`에 단일 씬 enabled 확인

## P1 — 게임의 핵심 정체성

### 3.6 내구도와 대파

> 상태: 구현. 단 `GameJamDefault.maximumDurability`가 임시로 `1000000`이라 실질적으로 대파가 발생하지 않는다.

- [x] 최대 내구도·피해 단계·대파 시간·복구량·보호 시간을 `DamageSettings`에서 읽음
- [x] 차량 내구도 100
- [x] 일반 충돌은 피해 없음 또는 매우 적게 적용
- [x] 강한 충돌과 파열 저주 피해
- [ ] 내구도 60~31 연기·스파크
- [ ] 내구도 30~1 강한 불꽃·엔진 경고
- [x] 내구도 0에서 약 2초 대파
- [x] 내구도 50으로 복구
- [x] 복구 직후 짧은 충돌 보호
- [x] 대파 자체는 직접 탈락시키지 않음
- [ ] 차체 변형 에셋이 없으면 단계별 VFX로 전달

### 3.7 저주 공통 시스템

> 상태: 구현 (`CurseManager`/`CurseController`/`CurseCooldownState`, 공용 쿨다운 12초). 대상 마커·연출 항목은 미검증.

- [ ] 저주별 거리·피해·지속시간·밀침·충돌 무시·쿨다운을 `CurseSettings`에서 읽음 — 부분. 쿨다운·피해·지속시간·거리는 참조하지만 **`ruptureKnockbackForce`와 `soulSwapCollisionIgnoreSeconds`는 선언만** 되어 있다
- [x] 파열·엔진 봉인·영혼 교환 데이터 정의 — `CurseType` + `CurseCatalog.All`
- [x] 세 저주가 공유하는 12초 쿨다운 — `CurseCooldownState`가 종류와 무관한 단일 타이머, `NextOrdered()`로 순환
- [x] 시전자보다 순위가 높은 유효 경쟁자 자동 선택 — `SelectTarget`이 탈락자·페널티 적용 불가 대상·순위 낮은 차량을 배제
- [ ] 저주 대상 표식 — 없음
- [x] `E`로 저주 활성화 — `CurseManager.Update`의 `Keyboard.current.eKey`(새 Input System 사용)
- [x] 인간 플레이어가 대상이면 퀴즈 시작 — `BeginHumanQuiz` → `QuizSessionController.TriggerNow`
- [x] 정답이면 페널티 없이 저주 방어
- [x] 오답·시간 초과면 저주 페널티 적용
- [x] 저주 발동 후 시전자 쿨다운
- [x] 퀴즈 실패 시 적용된 페널티를 화면에 잠시 표시 — `CursePenaltyFeedback` 라벨을 `RaceUI` 아래에 런타임 생성, 2.5초 표시
- [ ] 저주 소유자와 진행 방향을 명확히 표시 — 없음. 방어형 흐름에서는 **누가 나에게 걸었는지**를 퀴즈 화면에 함께 보여줘야 한다
- [ ] 표적이 됐다는 사전 신호 — 퀴즈가 예고 없이 즉시 뜬다. 짧은 경고음·화면 효과로 "표적이 됐다"를 먼저 알려야 한다
- [x] 플레이어가 시전자일 때는 퀴즈를 요구하지 않음 — 2026-07-26 확정 (방어형 흐름)

### 3.8 파열

> 상태: 구현 (`CurseController.ApplyCurse` → 대상 `DurabilityController.ApplyDamage`). 밀침은 미구현.

- [x] 대상 내구도 약 35 감소 — `C.ruptureDurabilityDamage` (프리셋 35)
- [ ] 짧은 물리적 밀침 또는 피격 반응 — **미구현.** `ruptureKnockbackForce: 8`이 저장돼 있으나 읽는 코드가 없다
- [ ] 플레이어에서 대상으로 이동하는 공격 연출 — 없음
- [x] 내구도와 대파 시스템 연동 — `DurabilityState` 경로 공유. 단 현재 `maximumDurability: 1000000`이라 체감 불가

### 3.9 엔진 봉인

> 상태: 구현 (`CurseController` → 대상 `BoostController.ApplySeal(5초)`).

- [x] 5초간 대상 부스트 사용 차단 — `BoostState.ApplySeal`, `C.engineSealDurationSeconds`
- [x] 5초간 대상 부스트 충전 중단
- [ ] 사슬·봉인·배기구 차단 연출 — 없음
- [ ] 남은 봉인 시간 아이콘 — 없음 (`BoostController.IsSealed`만 노출)

### 3.10 영혼 교환

> 상태: 부분 — 거리 조건과 위치·회전·속도 교환은 구현. 안전 조건과 충돌 무시 창은 미구현.

- [x] 대상 거리 15~40m — `CanSoulSwap`이 대상 선택 시점에 검사 (퀴즈 중 이동은 의도적으로 재검사하지 않음)
- [ ] 양 차량 접지와 유효 트랙 검사 — **미구현.** 접지·트랙 유효성 검사 없이 즉시 교환한다
- [ ] 점프·리셋·처형·관문 임계 구간 사용 금지 — 부분. `RacePhase.FinalDuel`만 차단하고 처형 경고·공중·리스폰 중 차단은 없다
- [ ] 트랙 진행 거리와 안전한 횡방향 위치 교환 — 현재는 **월드 좌표를 그대로 맞바꾼다.** `GmtkRccpWaypointPath`를 쓰지 않는다
- [ ] 전방 속도 유지와 트랙 방향 정렬 — 상대의 회전·속도를 그대로 받아쓰므로 결과적으로 방향은 맞지만, 트랙 방향 기준 정렬은 아니다
- [ ] 교환 후 약 0.3초 상호 충돌 무시 — **미구현.** `soulSwapCollisionIgnoreSeconds: 0.3`이 저장만 되어 있다

### 3.11 AI 저주

> 상태: 부분 — AI 저주 발동과 성격별 방어 확률은 구현됐으나 사전 신호·연속 표적 유예는 미구현.
> 방어형 흐름 확정에 따라 이 절의 성격 축은 "얼마나 잘 막는가"(`quizAvoidChance`)다.

- [ ] AI별 저주 쿨다운·유예·방어 확률을 `AISettings`에서 읽음 — 부분. **방어 확률은 `AISettings.personalityProfiles[].quizAvoidChance`로 이전 완료** (2026-07-26). 유예(`hostileEffectGraceSeconds`)는 아직 없고, 저주 쿨다운은 성격 무관 공용 12초라 성격별 필드를 두지 않았다
- [x] AI가 플레이어와 같은 저주 효과 사용 — `CurseController` 동일 경로
- [x] AI가 표적일 때는 화면 없이 성격별 방어 확률로 판정 — `CurseCooldownState.ResolveAiQuiz(chance, Random.value)`
- [x] 성격별 방어율 차등 — 생존자 0.8 / 봉쇄자 0.65 / 난폭자 0.35 / 폭주광 0.3. 빠르고 공격적인 차가 저주에 약하다는 규칙이 성립
- [x] AI 시전자의 발동 스케줄 — 초기 지연 2~5초 랜덤, 발동 실패 시 1초 후 재시도, 공용 쿨다운 12초 공유
- [ ] 플레이어가 표적이 됐음을 인지할 사전 신호 — 없음. `CurseActivated` 이벤트는 있으나 표시 수단이 없고 퀴즈가 곧바로 뜬다
- [ ] 여러 AI의 피할 수 없는 연속 표적 방지 — 부분. 미해결 퀴즈가 하나면 다른 AI 발동이 막히지만(`pendingHumanCurse` 가드), 퀴즈가 끝난 직후 다시 표적이 되는 것을 막는 유예(`hostileEffectGraceSeconds` 2초)가 없다
- [ ] 폭주광·난폭자가 저주에 약한 대가로 주행 위협이 커야 함 — 속도 배수 폭이 좁아 대가가 체감되지 않는다 (2.6의 위협 차이 확대와 같은 작업)

### 3.12 추월 도전

> 상태: 구현 (`OvertakeManager`/`OvertakeChallengeState`, 20초 주기·8초 제한·0.5초 유지).
> **실패 페널티는 타이머만 돌고 실제 감속이 걸리지 않는다.**

- [ ] 판정 주기·제한 시간·유지 시간·보상·속박 강도를 `OvertakeSettings`에서 읽음 — 부분. 주기·제한·유지·보상 모드·속박 시간은 읽지만 **`bindSpeedMultiplier`를 소비하는 코드가 없다**
- [x] 20초 간격으로 경쟁자 한 대 무작위 선택 — `PickRivalAhead()`가 앞선 활성 차량 중 무작위 1대
- [x] 이미 플레이어가 앞선 차량이면 해당 도전 무시 — 앞선 차량만 후보에 넣고, 후보가 없으면 이번 판정을 건너뛴다
- [ ] 대상 차량 카메라와 UI 표시 — 없음 (`RaceHud` 라벨만 있고 미배치)
- [x] 8초 안에 추월하고 0.5초 유지하면 성공 — `OvertakeChallengeState.Tick(dt, playerAhead)`
- [x] 성공 시 부스트 한 칸 회복 — `BoostController.RewardOvertake`
- [x] 성공 시 저주 쿨다운 즉시 초기화 — `cooldownRewardMode == Reset`이면 `CurseController.ResetCooldown`, `Reduce`면 `ReduceCooldown`
- [ ] 실패 시 심판의 사슬과 강한 감속 — **미완.** `IsBound = true`와 `BindSpeedMultiplier`만 노출되고 차량 쪽 소비자가 없어 주행에 영향이 없다. 사슬 연출도 없다
- [x] 플레이어가 최하위가 되면 즉시 속박 해제 — `PlayerIsLast()`면 `ReleaseBind()` + 진행 중 도전 취소
- [ ] 대상·남은 시간·앞섬 여부·실패 결과 표시 — 표시 수단 없음
- [ ] 최종 결투 단계에서 새 도전이 시작되지 않게 차단 — **미구현.** `OvertakeManager`는 `RacePhase`를 보지 않는다 (`Race.IsRaceInProgress`만 확인)

## P2 — 시그니처 완성도

### 3.13 처형·카메라 연출

- [ ] 카메라 임계값·FOV·거리·감쇠·흔들림·전환 시간을 `CameraSettings`에서 읽음 — 부분. 처형 CCTV의 전환·복귀·Rect만 설정에서 읽고, FOV·팔로우 오프셋·댐핑은 `ExecutionCctvDirector`의 컴포넌트 필드다. 대상 확정 뒤에는 월드 고정 앵커를 Follow·LookAt으로 사용해 잔해를 지나치지 않는다
- [ ] VFX 크기·지속시간·강도·잔해 유지시간을 `PresentationSettings`에서 읽음 — 부분. `executionBreakableVehiclePrefab`과 `wreckLingerSeconds`를 읽지만, 조각 폭발력·반경·상향력·토크는 컴포넌트 값이며 `explosionVfxDurationSeconds`는 선언만 되어 있다
- [ ] 붉은색 또는 지옥식 최하위 외곽선 — 없음
- [ ] 잠금 → 점화 → 경련 → 폭발 연출 — 잠금 뒤 즉시 `Model_Skyline (Breakable)` 차체 조각에 독립 Rigidbody·Collider와 폭발력·토크를 적용한다. 점화·경련 예고 단계는 아직 없음
- [x] 불길·스파크·짧게 남는 잔해 — 파괴 프리팹 내부 `Explosion01/02/03` 중 하나를 무작위 재생하고 차체 조각을 `wreckLingerSeconds` 동안 유지한 뒤 제거
- [x] 잔해가 트랙을 영구적으로 막지 않게 처리 — `CarExplosion`이 `wreckLingerSeconds`(2.5초) 후 `SetActive(false)`
- [ ] 플레이어 처형 바이탈 사운드 — 처형 대상 확정 시 플레이어(`index 0`)면 `VO_ANNOUNCER_CHALLENGE_FAILED`를 재생하도록 코드 연결. Play Mode 청취 확인 필요
- [ ] 예측형 전방 주시와 속도 기반 거리·FOV — 없음 (RCCP 기본 추종)
- [ ] 카메라 충돌 회피와 수평선 안정화 — 없음
- [ ] 옆 차량이 0.6초 유지되면 두 차량 투샷 — 미구현 (`sideBySideActivationSeconds: 0.6` 선언만)
- [ ] 저주 대상 방향으로 구도 미세 편향 — 없음
- [ ] 최종 관문에서 두 차량과 관문 프레이밍 — 없음
- [ ] 급커브·공중·조작 상실 시 기본 카메라 우선 — 없음

### 3.14 트랙·비주얼·오디오

오디오 기반은 있다: `GameAudioManager`(592줄)가 `Assets/Sound` 폴더를 스캔해 이벤트 ID → 클립 cue를
자동 매핑하고, 클립 **35개**가 임포트되어 있으며 `Audio Manager.prefab`이 `GMTK_Race`에 배치되어 있다.
문제는 **호출 지점**이다. 실제로 불리는 API는 `PlayCountdownTick` / `PlayRaceStart` / `PlayButtonClick`
3개뿐이고 모두 `RaceFlow`에서 온다.

- [ ] 오디오 볼륨·피치·페이드·경고 단계 수치를 `PresentationSettings`에서 읽음 — **미연결.** `masterSfxVolume`·`warningAudioFadeSeconds`가 선언만 되어 있고 `GameAudioManager`는 `GameBalance`를 참조하지 않는다
- [ ] 악마 유해 위 지옥 주조 고속도로 테마 — `Race Track Authoring.prefab` + `ArtTestScene`으로 작업 중. 이번 리뷰에서 룩 판정 불가
- [ ] 용광로 대로·갈비뼈 코너·제련소·심판의 직선로·관문 — 명명된 랜드마크 확인 불가
- [ ] 부스트와 추월용 넓은 직선 최소 2곳 — 검증 도구 없음 (`minimumBoostStraights: 2`는 선언만)
- [ ] 두 차량 투샷용 병렬 주행 구간 최소 1곳 — 동일
- [ ] 넓고 읽기 쉬운 도로와 관대한 장벽 — AI가 `maxRoadHalfWidthMetres: 13`으로 도로 폭을 프로빙하는 것으로 보아 폭은 확보. 사람 확인 필요
- [ ] 불·연기·파티클이 레이싱 라인을 가리지 않게 조정 — 사람 확인 필요
- [ ] 속도·부스트 연동 엔진 사운드 — RCCP 자체 엔진 사운드는 있으나 부스트 연동 없음
- [ ] 처형 카운트다운과 최하위 경고음 — 호출 지점 없음
- [ ] 저주 준비·성공·실패·피격 예고음 — 호출 지점 없음
- [ ] 추월 도전·관문·폭발·승리 사운드 — 호출 지점 없음
- [ ] 위험은 빨강, 저주는 청록·보라·녹색, 관문은 흰색·금색 계열 — 부분. `DurabilityHud`가 내구도 비율에 따라 초록/주황/빨강, 처형 CCTV 프레임이 빨강. 저주·관문 색 규약 없음
- [ ] 색상뿐 아니라 형태와 움직임으로 상태 구분 — 미검토

### 3.15 주행 특수 이벤트

> 상태: **레거시 프로토타입 6종만 존재.** `RandomEventManager`(303줄)가 `TriggerRandomEvent()`에서
> 공사 구간 · 가축 횡단 · 대형 비치볼 · 운석 상자 · 지진 · 부스트 패드를 코드 `switch`로 균등 추첨한다.
> 배치 위치는 킷 `AIWaypointSet`(씬에 `AIWaypoints` 1개 존재 확인)에서 가져온다.
> 오브젝트는 `GameObject.CreatePrimitive`로 매번 새로 만들고 `TimedDestroy`로 파괴한다 — 풀링 없음.
> `GMTK_Race.unity`에서 `enableEvents: 1`로 **켜져 있고**, `firstEventDelay: 8`, `intervalRange: 9~16`이다.
> GDD가 요구한 덤프트럭 습격은 없고, 지진·운석은 형태가 GDD와 다르다.

#### 공통 디렉터

- [ ] `SpecialEventDirector`가 `SpecialEventSettings`와 이벤트 정의 목록을 사용 — 클래스·설정 그룹 모두 없음
- [ ] 각 이벤트를 프리팹·가중치·경고·효과·오디오가 포함된 데이터 에셋으로 정의 — 없음. 전부 코드 내 프리미티브 생성
- [ ] 첫 이벤트 지연, 최소·최대 간격, 최대 동시 개수, 레이스당 최대 횟수를 설정에서 읽음 — 첫 지연·간격은 **컴포넌트 인스펙터** 값. 최대 동시 개수·레이스당 최대 횟수는 개념 자체가 없다
- [ ] 같은 이벤트가 연속으로 선택되지 않도록 설정 — 없음 (`Random.Range(0, 6)` 순수 추첨)
- [ ] 처형 경고, 퀴즈, 추월 도전, 최종 결투 중에는 새 이벤트 시작 금지 — 없음. `Race.IsRaceInProgress`만 확인하므로 처형 3초 전이나 퀴즈 중에도 발생한다
- [ ] 이미 시작한 이벤트는 상태 전환 시 안전하게 종료하거나 풀로 반환 — 부분. `RestartRaceEvent`에서 `ClearAll()`로 전부 `Destroy`. 페이즈 전환 시 정리는 없음
- [x] 특수 이벤트는 피해와 순위 손실을 만들 수 있지만 차량을 직접 탈락시키지 않음 — 어떤 이벤트도 `EliminationManager`를 호출하지 않는다. 다만 현재는 **내구도 피해 경로도 없다**(순수 물리 충격만)
- [x] 플레이어만 강제 타게팅하지 않고 같은 구간의 모든 레이서에게 같은 물리 규칙 적용 — 해저드는 웨이포인트에 놓이고 지진은 `Race.AllCarIndices()` 전원에 적용
- [ ] 모든 런타임 오브젝트는 풀링하고 트랙을 영구적으로 막지 않음 — 풀링 없음(`CreatePrimitive` + `Destroy`). 수명은 유한하므로 영구 차단은 아니다

#### 덤프트럭 습격

- [ ] 트랙에 덤프트럭 진입·퇴장 앵커 배치 — 미착수
- [ ] 경적, 진행 방향 화살표, 위험 차선 표시 후 진입 — 미착수
- [ ] 설정 속도로 도로를 가로지르거나 레이싱 라인을 따라 돌진 — 미착수 (가축 횡단 `ConstantMover`가 참고 가능)
- [ ] 충돌 차량에 설정 피해와 밀침 적용 — 미착수
- [ ] AI 장애물 감지에 포함 — AI 장애물 감지 자체가 없다 (2.1 참조)
- [ ] 설정 수명 또는 퇴장 앵커 도달 후 풀로 반환 — 미착수

#### 지진

- [ ] 전조음과 지면 균열·먼지 경고 — 없음
- [ ] 설정 시간 동안 모든 활성 차량에 약한 횡방향 흔들림 적용 — **형태 불일치.** 현재는 1프레임에 `Vector3.up * 4~7` + `insideUnitSphere * 2`를 `VelocityChange`로 가해 차를 **위로 띄운다.** 지속 시간·횡방향 개념이 없다
- [ ] 카메라 흔들림은 차량 물리와 별도 설정으로 적용 — 없음
- [x] 지진 자체는 내구도 피해를 주지 않음 — `DurabilityController.ApplyDamage`를 호출하지 않는다 (다만 착지 충격으로 강한 충돌 피해가 간접 발생할 수 있음)
- [ ] 조향 가능성을 유지하고 강제 스핀을 만들지 않음 — 공중에 띄우므로 조향 불가 구간이 생긴다

#### 운석 낙하

- [ ] 레이싱 라인 주변의 유효 충돌 지점을 선택 — 부분. 웨이포인트 + `insideUnitSphere * 4` 산포로 위치를 잡지만 유효성 검사는 없다
- [ ] 설정 시간 전에 바닥 원형 마커와 낙하음을 표시 — 없음
- [ ] 충돌 반경 안의 모든 차량에 설정 피해와 밀침 적용 — 없음. 물리 상자(4~8개, 질량 15)를 14~22m 높이에서 떨어뜨리는 방식이라 피해·반경 개념이 없다
- [ ] AI도 경고 구역을 피하도록 차선 편향 적용 — 없음
- [ ] 운석 잔해는 충돌체를 남기지 않고 설정 시간 후 풀로 반환 — 상자에 콜라이더가 남고 `meteorLifetime: 10`초 후 `Destroy`

---

## 4. 선택 기능과 스트레치 골

- [ ] 출발 타이밍 방향키 입력 스타트 부스트
- [ ] 초반 키 가이드 개선
- [ ] 저주 대상 순환 선택
- [ ] 두 번째 추월 도전
- [ ] 경쟁자 이름·초상화·고유 차량
- [ ] 사망 후 관전
- [ ] 추가 퀴즈 문제 유형
- [ ] 점수와 랭크 결과 화면
- [ ] 다음 지옥 층 스토리 장면
- [ ] 추가 카메라 행동
- [ ] 자비·죄인·저주받은 자 난이도
- [ ] 로컬 멀티플레이 또는 비동기 고스트

---

## 5. 게임 잼 비목표

아래 항목은 구현 체크 대상이 아니라 범위 방어용 목록이다.

- 온라인 멀티플레이
- 협동 플레이
- 오픈 월드 탐험
- 사실적인 차량 시뮬레이션
- 깊은 차량 커스터마이징
- 대규모 서사 허브
- 절차적 트랙 생성
- 복잡한 차량 파괴 시스템
- 두 개 이상의 레이스
- 저주 3종을 초과하는 추가 저주
- 완전 자율형 시네마틱 디렉터
- 고급 드리프트와 트릭 시스템

---

## 6. 테스트 체크리스트

> **이 절 전체는 Play Mode / 빌드 검증 항목이다.** 2026-07-26 리뷰에서는 Unity 에디터가 실행 중이
> 아니어서 하나도 실행하지 못했다. 아래 `[ ]`는 "실패"가 아니라 "미검증"이며, 코드 수준에서 이미
> 막혀 있다고 판단되는 항목만 사유를 적었다.

### 6.1 규칙과 차량

- [ ] `GameJamDefault`가 아닌 수치의 테스트 프리셋으로도 동일한 규칙 흐름 통과 — `FastTest` 프리셋과 `GMTKAutoPlaytest` 하네스가 준비되어 있음. 실행 미확인
- [ ] 총 6대가 정상 출발하고 1랩 목표 지점까지 완주
- [ ] 30초마다 최신 최하위 차량 처형 — **현재 프리셋이 10초**라 이 상태로는 확인 불가
- [ ] 순위 역전 직후에도 올바른 차량 처형
- [ ] 두 대가 남으면 탈락 중단과 관문 결투 시작
- [ ] 관문을 먼저 통과한 차량 승리, 패자 처형 — **코드 수준에서 불가.** 관문 트리거가 없어 통과 판정이 발생하지 않는다 (3.4)
- [ ] 충돌·리스폰·영혼 교환 후에도 순위 정상
- [ ] 키보드만으로 가속·제동·조향·드리프트·부스트 가능 — 드리프트 구현 없음 (2.2)
- [ ] 고속 과도한 스핀 없이 충돌 후 빠르게 복구 — 안정화 코드 없음 (2.2)
- [ ] 부스트 충전·소비·봉인 정상
- [ ] 대파 후 약 2초 뒤 정상 복구 — **현재 `maximumDurability: 1000000`이라 대파 자체가 발생하지 않는다**

### 6.2 처형 화면

- [ ] 3초 전 처형 차량이 오른쪽 아래 CCTV에 표시
- [ ] 플레이어 화면이 메인 화면으로 유지
- [ ] 메인 화면으로 계속 조작 가능
- [ ] 0초에 대상 차량 폭발
- [ ] 처형 후 CCTV가 닫히고 플레이어 화면 유지
- [ ] 플레이어 자신이 대상일 때 패배 흐름 정상
- [ ] 카메라 전환 중 레이싱 라인을 놓치지 않음
- [ ] `ExecutionCctvDirector.cs`를 Git에 커밋 — 현재 미추적이므로 다른 팀원 환경에서는 이 연출이 아예 없다

### 6.3 퀴즈·저주·도전

- [x] 저주 사용 전에는 퀴즈가 자동 실행되지 않음
- [x] 인간 플레이어가 저주 대상이면 퀴즈가 시작되고 레이스는 계속 진행
- [x] 마우스로 답 선택 가능
- [x] 정답 시 페널티 없이 저주 방어
- [x] 오답·시간 초과 시 페널티 적용과 시전자 쿨다운
- [ ] 플레이어가 시전자일 때 퀴즈 없이 즉시 발동 — 확정 흐름대로 동작하는지 Play Mode 확인
- [ ] 영혼 교환 금지 상태에서 안전하게 실패 — 금지 조건이 `FinalDuel`뿐이라 검증 범위 자체가 좁다 (3.10)
- [ ] 표적이 됐음을 알리는 사전 신호를 플레이어가 인지 — 신호 구현 없음 (3.11)
- [ ] 피할 수 없는 연속 표적화가 발생하지 않음 — 유예 구현 없음 (3.11)
- [ ] 성격별 저주 방어율 차이가 플레이에서 드러남 — 생존자가 자주 막고 폭주광이 자주 맞는지 (3.11)
- [ ] 추월 도전 성공·실패·속박 해제 정상 — 실패 시 감속이 실제로 걸리지 않음 (3.12)

### 6.4 주행 특수 이벤트

- [ ] 첫 지연 이후 설정된 최소·최대 간격 안에서 이벤트 시작 — 현재는 컴포넌트 값(8초 / 9~16초)으로 동작
- [ ] 같은 이벤트가 연속으로 발생하지 않음 — 방지 로직 없음
- [ ] 동시에 설정 개수보다 많은 이벤트가 활성화되지 않음 — 제한 없음
- [ ] 처형 경고·퀴즈·추월 도전·최종 결투 중 새 이벤트 차단 — 차단 없음
- [ ] 덤프트럭 경고·돌진·충돌·풀 반환 정상 — 이벤트 미구현
- [ ] 지진이 모든 활성 차량에 같은 규칙으로 적용 — 적용 대상은 전원이지만 효과 형태가 GDD와 다름
- [ ] 운석 경고 지점·반경 피해·밀침 정상 — 경고·반경 피해 미구현
- [ ] 이벤트 피해가 대파는 만들 수 있지만 직접 탈락시키지 않음 — 직접 탈락 경로는 없음. 대파 유발 경로(내구도 피해)도 없음
- [ ] 재시작 후 진행 중이던 이벤트와 풀 상태 초기화 — `ClearAll()` 존재, 실행 미확인

### 6.5 빌드·성능·플레이테스트

- [ ] 빌드에서 설정 에셋 변경만으로 차량 수·타이머·피해·카메라 수치 변경 가능 — **차량 수·랩 수는 불가.** `race.aiCount`/`lapCount`가 미참조 (2.1)
- [x] 코드와 프리팹을 검색해 설정 외부의 중복 밸런스 숫자가 없는지 검증 — **검증 수행함. 중복이 다수 발견되었다** (§0 "MonoBehaviour와 프리팹에 동일 수치를 중복 직렬화하지 않음" 항목의 목록). 검증 자체는 완료, 정리는 미완
- [ ] Unity 컴파일 오류 없음 — 이번 리뷰에서 `recompile`을 돌리지 못했다. `ExecutionCctvDirector`가 `Unity.Cinemachine`을 참조하고 `com.unity.cinemachine 3.1.5`가 매니페스트에 있으므로 정적으로는 문제없어 보인다 `[미검증]`
- [ ] EditMode와 PlayMode 테스트 전체 통과 — EditMode 99케이스 존재, 실행 미확인. PlayMode 자동 테스트는 없음
- [ ] Windows 64-bit와 WebGL 빌드 성공
- [ ] 차량 6대·퀴즈·폭발·복수 카메라·특수 이벤트 동시 발생 성능 안정
- [ ] Missing Script·Missing Reference·중복 AudioListener 없음 — **주의 지점 확인:** `QuizGameBootstrap.EnsureCamera`가 `Camera.main`이 없을 때 `AudioListener`가 달린 폴백 카메라를 생성한다. RCCP 카메라 초기화 순서에 따라 AudioListener가 둘이 될 수 있다 `[미검증]`
- [ ] 한 레이스가 3~5분 범위 — 현재 탈락 간격 10초라 4회 처형까지 40초. 프리셋 확정 후 재측정 필요
- [ ] 플레이어가 15초 안에 30초 탈락 규칙 이해 — 프롤로그 텍스트에는 30초 규칙이 적혀 있으나 HUD 카운트다운이 없어 확인 불가
- [ ] 플레이어가 최하위·저주 대상·추월 대상을 식별 — 세 표시 모두 화면에 없음 (3.5)
- [ ] 신규 플레이어가 일반적으로 첫 탈락까지 생존
- [ ] 카메라가 레이싱 라인을 가리지 않음
- [ ] 최종 관문이 강한 클라이맥스로 느껴짐 — 관문 오브젝트·연출 미구현 (3.4)
- [ ] 외부 플레이테스터 3명이 한 레이스 후 핵심 규칙 설명
- [ ] 결과 직후 재도전 의향 확인

---

## 7. 단계별 구현 계획

각 단계는 독립된 작업 단위다. 해당 단계의 완료 조건과 검증이 모두 통과하기 전에는 다음 단계로 넘어가지 않는다.

### 단계 0 — 작업 기준과 브랜치 정리

**목표:** 모든 구현자가 같은 최신 코드에서 시작하고 사용자 변경을 섞지 않는다.

- [ ] 최신 `develop`을 기준으로 단계별 작업 브랜치 생성
- [ ] 체크리스트 문서를 구현 브랜치에 포함
- [ ] 작업 전 `git status`에서 사용자 변경과 단계 작업을 구분
- [ ] `Packages/manifest.json`, `Packages/packages-lock.json`의 기존 변경 의도 확인
- [ ] Git 충돌, Unity YAML 충돌, 중복 GUID, 중복 클래스 발생 시 작업을 중단하고 사용자에게 보고
- [ ] **`Assets/GameBalance/Resources/`(프리셋 2종)와 `ExecutionCctvDirector.cs(+.meta)`를 커밋 대상에 포함** — 현재 미추적이고 구 위치 `Assets/Resources/GameBalance/`는 삭제 상태라, 지금 커밋하면 프리셋 없는 빌드가 푸시된다

**현재 리뷰 시점 상태 (2026-07-26, 로컬 `develop` `652e03b`):**

- `GMTK_Race.unity`는 **텍스트 YAML**이다(12.6MB). 이전 리뷰의 "바이너리 직렬화" 기록은 더 이상 맞지 않다.
  스마트 머지는 가능하지만 크기 때문에 여전히 씬 소유자 1인 원칙을 지켜야 한다.
- 미커밋 변경: `GameBalanceSettings.cs`, `GMTK_Race.unity`, `EliminationManager.cs`,
  `RaceTrackAuthoring.cs`, `CLAUDE.md`, `Packages/manifest.json`, `Packages/packages-lock.json`,
  본 체크리스트.
- 미추적 신규: `AGENTS.md`, `Assets/GameBalance/Resources/`(+`.meta`), `ExecutionCctvDirector.cs`(+`.meta`).
- 삭제됨: `Assets/Resources/` 전체 (프리셋이 `Assets/GameBalance/Resources/`로 이동).

**완료 조건:**

- [ ] 최신 `develop` 기반 브랜치에서 Unity 컴파일 성공
- [ ] 작업 대상 외 변경이 stage되지 않음
- [ ] 해결되지 않은 충돌 없음

### 단계 1 — 설정 시스템 기반

**목표:** 이후 모든 단계가 숫자와 정책을 코드 수정 없이 변경할 수 있게 한다.

**진행률: 대부분 완료. 남은 것은 미소비 필드 연결과 프리셋 값 확정이다.**

- [x] `GameBalanceSettings`와 하위 설정 그룹 구현 — 14그룹
- [x] `GameJamDefault`와 `FastTest` 프리셋 생성 — 커밋만 남음
- [x] 선택된 프리셋을 불변 스냅샷으로 공급하는 런타임 제공자 구현
- [x] 설정 유효성 검사와 구체적인 오류 메시지 구현 — 19개 규칙 + `OnValidate` + 로드 시 검증
- [x] 숫자 외 정책 설정인 저주 선택·타게팅·추월 보상 모드 구현 — 세 enum 모두 분기까지 구현
- [ ] **`VehicleSettings`·`SpecialEventSettings` 그룹 추가**
- [ ] **"선언만" 상태인 필드를 실제로 소비하도록 연결** — 특히 `race.aiCount`/`lapCount`, `quiz.*`, `bindSpeedMultiplier`, `ruptureKnockbackForce`, `soulSwapCollisionIgnoreSeconds`, 관문 3필드
- [ ] **`AIArchetype`·`AIArchetypeProfile`·`archetypeAssignments` 제거** — 연결이 아니라 폐기 대상이다 (AI 성격 축 확정)
- [ ] **`GameJamDefault`의 `intervalSeconds`를 30으로, `maximumDurability`를 100으로 확정**

**완료 조건:**

- [ ] 프리셋 에셋만 수정해 테스트 타이머와 차량 수가 변경됨 — 타이머는 가능, **차량 수는 불가** (`RaceData.AiBotsSelected` 경로)
- [x] 설정 누락이나 잘못된 범위가 시작 전에 검출됨 — `Load()`가 프리셋 부재·검증 실패를 모두 `LogError`
- [ ] 새 코드에 밸런스 리터럴이 중복되지 않음 — §0 목록의 중복 잔존

**필수 검증:**

- [ ] EditMode 설정 검증 테스트 — `GameBalanceSettingsTests` 11케이스 존재, **실행 미확인**
- [ ] `GameJamDefault`와 `FastTest` 로드 테스트
- [ ] Unity 재컴파일 없이 Inspector 값 변경 후 새 레이스에 반영

### 단계 2 — 1랩·6대 레이스 기본 구성

**목표:** 저주와 탈락 없이도 플레이어 1대와 AI 5대가 한 바퀴를 안정적으로 달린다.

- [ ] 스폰과 레이스 선택기가 `RaceSettings`를 사용하도록 변경 — **미착수.** `GmtkRccpPlayersSpawner`는 `RaceData.AiBotsSelected`를 읽는다
- [x] AI 5대와 플레이어 1대 스폰 — `BotSelectorGUI.defaultQuantity: 5`로 6대 구성. 스폰 포인트 8개
- [x] 1랩 목표 지점과 체크포인트 구성 — `LapSelectorGUI.defaultQuantity: 1`, 씬 체크포인트는 결승선 **1개**
- [x] 게임 전용 레이스 씬 분리 및 Build Settings 등록 — `GMTK_Race.unity` 단일 씬 enabled
- [ ] 레거시 자동 랜덤 이벤트를 특수 이벤트 단계 완료 전까지 비활성화 — **미완.** 씬에 `enableEvents: 1`

**완료 조건:**

- [ ] 총 6대가 겹치거나 누락되지 않고 출발 — 미검증
- [ ] 플레이어와 AI가 체크포인트 순서대로 1랩 완주 — 미검증
- [ ] Windows와 WebGL이 같은 게임 씬으로 시작 — Build Settings 기준 충족, 빌드 미검증

**필수 검증:**

- [ ] 6대 스폰 PlayMode 테스트 — `GMTKAutoPlaytest`가 `car count == ai+1`을 검사한다. 실행 미확인
- [ ] AI 5대 완주 스모크 테스트
- [ ] Missing Script·Missing Reference 검사

### 단계 3 — 아케이드 조작감·드리프트·부스트

**목표:** 퀴즈와 전투가 없어도 차량 운전 자체가 안정적이고 재미있다.

**진행률: 부스트만 구현. 조작감·드리프트는 코드가 전무하다.**

- [ ] 저속 조향 보조와 고속 조향 감쇠 — 플레이어 경로에 없음 (AI만 보유)
- [ ] 스핀 억제와 충돌 후 트랙션 회복 — 없음
- [ ] 기본 아케이드 드리프트 — 없음
- [ ] 트랙 이탈과 반대 방향 복구 — 추락 리스폰만 있음
- [x] 설정 기반 2칸 부스트와 재충전 — `BoostState` + `BoostSettings`
- [ ] AI 직선·긴급 회복 부스트 — `TryBoost()` 호출자 없음
- [ ] **`BoostController`의 레거시 `Input` API를 새 Input System으로 교체** (프로젝트 규칙)

**완료 조건:**

- [ ] 키보드만으로 가속·제동·조향·드리프트·부스트 가능 — 드리프트 없음
- [ ] 측면 충돌 후 빠르게 주행 복귀 — 안정화 없음
- [ ] AI 차량이 벽처럼 느껴지지 않음 — 미검증

**필수 검증:**

- [ ] 부스트 소비·재충전·최대치 테스트 — `BoostStateTests` 12케이스 존재, 실행 미확인
- [ ] 드리프트 진입·해제 PlayMode 확인
- [ ] 트랙 이탈·정체·역주행 복구 확인

### 단계 4 — 공용 진행도와 중앙 레이스 상태

**목표:** 순위, 탈락, 저주, 추월, 리스폰이 하나의 진행도와 판정을 공유한다.

- [x] 공용 진행도 구현 — `GmtkRaceProgress`가 웨이포인트 arc-length 주행 거리를 킷 `RacePositionTotalScores`에 기록하고, 모든 규칙 시스템은 그대로 `Race.ScoreOf()`를 읽는다
- [ ] 동률 안정화와 탈락 차량 제외 — 탈락 제외는 구현, **동률 안정화는 없음**
- [x] 레이스 최상위 상태 흐름 구현 — `GMTKRaceState` + `RacePhase` 6단계
- [ ] 기존 `RaceFinish`의 독립 승패 판정 제거 또는 어댑터화 — **미착수.** 씬에 그대로 있고 `finishTrigger` NRE 결함도 남아 있다
- [ ] 개별 차량이 승리·패배·탈락을 결정하지 못하게 제한 — 차량 컴포넌트는 결정하지 않는다. 다만 승패 확정 지점이 3곳

**완료 조건:**

- [x] 순위표와 모든 규칙 시스템이 같은 진행도 사용 — `Race.ScoreOf()` 단일 경로 확인
- [ ] 순위가 근접 상황에서 빠르게 깜빡이지 않음 — 타이브레이크 없음
- [ ] 승패 판정 진입점이 중앙 레이스 관리자 하나로 제한됨 — 3곳

**필수 검증:**

- [ ] 랩·체크포인트·구간 거리 순위 테스트
- [ ] 동률과 순위 역전 테스트
- [ ] 충돌·리스폰 후 진행도 테스트

### 단계 5 — 30초 탈락·핵심 HUD·처형 카메라

**목표:** 게임의 핵심인 "30초마다 꼴찌 처형"을 완성한다.

**진행률: 규칙과 CCTV 연출은 코드 완료. HUD가 화면에 없고 프리셋 값이 10초다.**

- [x] 설정 기반 탈락 간격과 10초·5초·3초 경고 — `EliminationWarningLevel` 3단계 + 전용 이벤트. **프리셋 값 확정 필요**
- [x] 0초에 최신 순위를 다시 계산해 최하위 처형 — `EliminateLastPlace`가 `FindLastPlace()` 재호출
- [x] 두 대가 남으면 탈락 중단 — `ActiveCarCount <= finalDuelRacerCount`
- [ ] 순위·처형 타이머·최하위 경고 HUD — 순위·생존수와 중앙 최하위 경고는 구현. 처형 타이머는 경고 단계에서만 표시되어 상시 표시는 미완
- [x] 플레이어 화면을 메인 화면에 유지 — `ExecutionCctvDirector`가 플레이어 카메라의 rect/depth를 변경하지 않음
- [x] 처형 차량 CCTV를 오른쪽 아래 설정 Rect로 표시 — `executionCctvViewportRect`
- [x] 처형 후 CCTV 정상 종료 — `wreckLingerSeconds` 후 축소 → `CompleteHide`에서 원본 복원

**완료 조건:**

- [ ] 6대에서 네 번의 처형 후 정확히 두 대가 남음
- [ ] 순위 역전 직후에도 올바른 차량이 처형됨
- [ ] 플레이어가 메인 화면으로 계속 조작 가능
- [ ] 플레이어 자신이 대상이어도 메인 주행 화면과 CCTV로 자기 차량 파괴를 `wreckLingerSeconds` 동안 보여준 뒤 패배 흐름으로 전환 — 코드 구현, Play Mode 미검증

**필수 검증:**

- [ ] `FastTest` 프리셋으로 네 번 연속 탈락 자동 테스트
- [ ] 경고 단계와 실제 판정 시간이 같은 설정을 사용하는지 확인
- [ ] 복수 카메라 Windows·WebGL 성능 스모크

### 단계 6 — 최종 관문·승패·재시작

**목표:** 두 대가 남은 뒤 실제 목표 지점에서 승자를 결정하는 P0 레이스를 완성한다.

**진행률: 관문 실물·트리거·폐쇄 연출이 코드로 구현되어 승리 조건이 성립한다(Play Mode 미검증).
남은 공백은 킷 `RaceFinish`의 랩 완주 승리 우회와 결과 화면 규정이다.**

- [x] 두 대가 남으면 최종 결투 상태로 전환 — `EliminationManager.EnterFinalDuel` → `GMTKRaceState`/`FinalGate`
- [ ] 최종 결투 전에는 일반 결승선이 승패를 발생시키지 않도록 보호 — **미착수.** 킷 `RaceFinish`가 페이즈를 확인하지 않는다. 단 관문 자체는 결투 시작 시에만 생성되므로 조기 통과로 관문 승패가 발생하지는 않는다
- [x] 첫 관문 통과 차량 승리 — `FinalGateCrossingTrigger` → `FinalGate.ReportGateCrossing`. 타임아웃 폴백도 실제 선두 판정으로 교체
- [ ] 승자 뒤 관문 폐쇄와 패자 처형 — 폐쇄 애니메이션(0.75초) → 0.25초 후 처형까지 구현. **연출 체감 Play Mode 확인 필요**
- [ ] 승리·패배 결과 화면 — `RaceFinishType.Win`에는 11초짜리
  `StreamingAssets/Ending.mp4` 전체 화면 재생을 연결했고, 재생 종료 후에는
  플레이어가 애플리케이션을 종료할 때까지 마지막 프레임을 유지한다. 패배는 여전히
  킷 `RaceFinishGUI`에 의존하며, 승리 엔딩의 실제 레이스 종단 Play Mode 검증이 필요
- [ ] 모든 런타임 상태를 초기화하는 빠른 재시작 — 결과 버튼 → `RestartRaceEvent` → 프롤로그 없는 커스텀 카운트다운 → `RaceStartedEvent` 연결 완료, Play Mode 미검증

**1랩 구조 안전 규칙:**

- 6대에서 두 대가 되려면 기본 설정 기준 네 번의 처형, 즉 최소 120초가 필요하다.
  `GameJamDefault.elimination.intervalSeconds`는 GDD 값 30초로 정상화되어 이 전제가 성립한다
  (이전에는 10초로 드리프트해 40초면 결투에 도달했다).
- [ ] 트랙 최종 진입 예상 시점을 네 번째 처형 이후로 튜닝
- [ ] 차량이 너무 일찍 도착해도 최종 결투 전에는 관문 승패가 발생하지 않게 처리 — `FinalGate.ReportGateCrossing`은 `IsOpen` 가드로 막지만, 킷 `RaceFinish`의 랩 완주 승리는 막히지 않는다

**완료 조건:**

- [ ] 메뉴부터 결과와 재시작까지 개발자 개입 없이 진행
- [ ] 관문을 먼저 통과한 차량만 승리
- [ ] 재시작 후 이전 차량·타이머·카메라·관문 상태가 남지 않음

**필수 검증:**

- [ ] 선두·후발·동시 진입 관문 판정 테스트
- [ ] 플레이어 승리·패배 PlayMode 테스트
- [ ] 연속 재시작 테스트

### 단계 7 — 내구도와 대파

**목표:** 피해가 직접 탈락시키지 않고 순위 손실을 만드는 규칙을 완성한다.

**진행률: 규칙 완료. 단계별 VFX가 없고 프리셋 값(`maximumDurability 1000000`)이 규칙을 무력화한다.**

- [x] 설정 기반 내구도와 피해 단계 — `DurabilityState` + `DamageSettings`, `DamageStage` 4단계
- [x] 강한 충돌과 파열 피해 진입점 — `OnCollisionEnter`의 `impulse.magnitude >= strongCollisionImpulse` + `CurseController` 파열
- [x] 대파 중 조작 상실 — `SuspendControls()`가 어댑터로 입력을 끊고 모든 Rigidbody를 kinematic으로 고정
- [x] 설정 시간 후 내구도 복구 — `wreckDurationSeconds` → `recoveryDurability`
- [x] 복구 직후 충돌 보호 — `recoveryProtectionSeconds` (프리셋 2초, GDD는 1.5초)
- [ ] 단계별 연기·스파크·불꽃 피드백 — `StageChanged` 이벤트만 있고 VFX 구독자가 없다
- [ ] **`GameJamDefault.maximumDurability`를 100으로 되돌리기**

**완료 조건:**

- [x] 내구도 0이 직접 탈락으로 이어지지 않음 — `DurabilityController`가 `EliminationManager`를 호출하지 않는다
- [ ] 대파와 처형 상태가 충돌하지 않음 — 코드상 독립적이지만 대파 중 차량이 kinematic이 되므로 `CarExplosion`의 폭발력이 먹히지 않을 가능성이 있다 `[미검증]`
- [ ] 시각 효과만으로 피해 단계를 구분 가능 — VFX 없음. `DurabilityHud` 색상만

**필수 검증:**

- [ ] 피해 임계값·대파·복구·보호 테스트 — `DurabilityStateTests` 10케이스 존재, 실행 미확인
- [ ] 대파 중 탈락 카운트다운 판정 테스트

### 단계 8 — 저주 발동과 퀴즈 방어 연결

**목표:** 운전을 멈추지 않고 저주 3종을 걸고, 표적이 되면 퀴즈로 막을 수 있게 한다.
(2026-07-26 확정된 방어형 흐름 기준)

- [x] 자동 퀴즈 반복 실행 중단
- [x] 설정된 저주 선택 방식과 자동 타게팅 적용
- [x] `E` 입력으로 저주 활성화 (퀴즈 없이 즉시 발동, 쿨다운 즉시 소모)
- [x] 인간 플레이어가 표적일 때 퀴즈와 마우스 답안 선택
- [x] 정답 방어·실패 페널티·시전자 쿨다운
- [x] 파열·엔진 봉인·영혼 교환 구현
- [ ] 영혼 교환 안전 조건과 트랙 기반 재배치
- [ ] 저주·쿨다운·내구도·부스트 HUD — 내구도·저주 쿨다운은 `RaceUI`에 배치됨. **부스트 충전량은 표시 수단 없음** (3.5)
- [ ] 표적 사전 신호와 시전자 표시 HUD — 누가 걸었는지, 표적이 됐다는 예고가 없다

**완료 조건:**

- [x] 세 저주가 같은 공용 효과 경로를 사용
- [x] 정답이면 효과 없이 방어하고 오답·시간 초과면 페널티 적용
- [ ] 영혼 교환이 체크포인트와 순위를 망가뜨리지 않음

**필수 검증:**

- [ ] 세 저주 성공·실패 테스트
- [ ] 봉인 중 부스트 사용·재충전 차단 테스트
- [ ] 영혼 교환 허용·금지 상태 테스트
- [x] 퀴즈 중 레이스가 계속되는지 PlayMode 확인

### 단계 9 — AI 성격·부스트·저주

**목표:** AI 5대가 확정된 네 성격을 보이며 플레이어와 같은 규칙으로 경쟁한다.
확정 축은 `AIPersonalityType`(폭주광·난폭자·봉쇄자·생존자), 확정 구성은 폭주광 2 + 나머지 1대씩.

**진행률: 설정화·배분·주행 행동 완료. 남은 것은 부스트·유예·따라잡기와 Play Mode 튜닝이다.**

- [x] 설정 에셋으로 AI 5대 성격 배분 — `AISettings.personalityAssignments`(차량당 한 칸)를 `AiPersonalityRoster.BuildOrder(assignments, …)`가 읽고 슬롯만 셔플한다
- [x] 확정 구성(폭주광 2)으로 맞추기 — 구성이 코드 배열에서 프리셋 데이터로 이동했으므로 배열 순서와의 결합 자체가 사라졌다
- [x] 폭주광·난폭자·봉쇄자·생존자 주행 행동 — 페이스 배수 + 램/블록 측면 오프셋으로 4종 모두 구현
- [x] `AIArchetype`·`AIArchetypeProfile` 폐기와 `AiPersonalityProfile` 신설 — 성격별 페이스·측면 강도·부스트 성향·저주 방어율·따라잡기를 한 행에 모았다
- [ ] AI 부스트와 순위 회복 판단 — `TryBoost()` 호출자 없음. `boostTendency`는 프로필에 자리만 잡아 뒀다. 폭주광의 NOS는 RCCP 자체 기능이라 `BoostSettings`와 무관
- [x] AI 저주 3종 발동 — `CurseManager.UpdateAiCasters` (방어형 흐름에서 발동은 성격과 무관)
- [ ] 표적 사전 신호와 연속 표적 유예 — `pendingHumanCurse` 가드만 있고 `hostileEffectGraceSeconds` 개념이 없음
- [ ] 절제된 따라잡기 — `catchupAcceleration`은 프로필에 자리만 잡아 뒀고 소비자가 없다

**완료 조건:**

- [ ] 네 성격의 위협 차이가 플레이에서 보임 — `paceScale` 폭이 0.86~1.0으로 좁다. 이제 한 곳에서 조정 가능하므로 Play Mode 튜닝만 남았다
- [ ] 성격별 저주 방어율 차이가 체감됨 — 값은 차등(0.3~0.8)이고 순서 규칙은 테스트로 고정. 체감은 미검증
- [ ] AI가 눈에 띄는 순간이동 없이 접전 형성 — AI 순간이동 코드는 없음. 접전 형성은 미검증
- [ ] 피할 수 없는 연속 표적화가 발생하지 않음 — 유예 미구현

**필수 검증:**

- [x] 확정 구성 배분 테스트 — `AiPersonalityRosterTests`에 6케이스 추가(구성 보존, 슬롯만 셔플, 시드 재현, 짧은/누락 리스트 폴백, 범위 밖 항목 폴백, 0대 안전). `GameBalanceSettingsTests`에 8케이스 추가(확정 구성, 프로필 누락, 방어율·페이스 순서 규칙, 폴백). **작성 완료·미실행**
- [ ] 성격별 의사결정 테스트 — 행동(램/블록 조준 오프셋) 테스트는 없음
- [ ] AI 저주 경고·유예 테스트
- [ ] 6대 전체 레이스 장시간 스모크

### 단계 10 — 추월 도전

**목표:** 20초 주기의 추월 퀘스트와 심판의 사슬을 완성한다.

**진행률: 규칙·보상 완료. 실패 페널티의 실제 감속과 UI가 없다.**

- [x] 무작위 경쟁자 선택과 이미 앞선 차량 무시 — `PickRivalAhead()`
- [ ] 대상·제한 시간·앞섬 여부 HUD — `RaceAlertPanel/Overtake TMP`에 대상·제한 시간은 표시하지만 앞섬 유지 상태는 별도 표시하지 않음
- [x] 8초 안에 추월 후 0.5초 유지 판정 — `OvertakeChallengeState`
- [x] 부스트 한 칸 회복과 저주 쿨다운 즉시 초기화 — `OnSuccess()`
- [ ] 실패 시 속박 — 상태 플래그·타이머만 있고 **감속이 차량에 전달되지 않는다**
- [x] 최하위가 되면 즉시 속박 해제 — `PlayerIsLast()` → `ReleaseBind()`

**완료 조건:**

- [ ] 성공과 실패가 모두 플레이어에게 명확함 — UI·연출 없음
- [x] 속박 실패가 즉시 확정 사망으로 처리되지 않음 — 실패는 탈락과 무관
- [ ] 최종 결투 단계에서는 새로운 도전이 시작되지 않음 — `OvertakeManager`가 `RacePhase`를 보지 않는다

**필수 검증:**

- [ ] 대상 선택 확률과 무시 조건 테스트
- [ ] 추월 유지시간 경계 테스트 — `OvertakeChallengeStateTests` 8케이스 존재, 실행 미확인
- [ ] 보상 모드와 속박 해제 테스트

### 단계 11 — 주행 특수 이벤트

**목표:** 기존 랜덤 이벤트 프로토타입을 공정하고 설정 가능한 덤프트럭·지진·운석 시스템으로 교체한다.

**진행률: 미착수. 레거시 프로토타입 6종이 씬에서 켜져 있다.**

- [ ] `RandomEventManager`를 `SpecialEventDirector`로 리팩터링
- [ ] 이벤트 정의 데이터 에셋과 트랙 이벤트 앵커 구현
- [ ] 공통 스케줄러·가중치 선택·중복 방지·상태 차단 구현
- [ ] 덤프트럭 습격 구현
- [ ] 지진 구현 — 현재는 위로 띄우는 1프레임 충격
- [ ] 운석 낙하 구현 — 현재는 물리 상자 낙하
- [ ] 모든 이벤트 오브젝트 풀링 — 현재 `CreatePrimitive` + `TimedDestroy`
- [ ] 소·비치볼·공사 구간·부스트 패드를 기본 프리셋에서 제외 — **당장 할 수 있는 조치:** 리팩터링 전이라도 씬의 `enableEvents`를 `0`으로 내려 레거시 이벤트를 끈다

**완료 조건:**

- [ ] 덤프트럭·지진·운석이 설정 간격으로 주행 중 발생
- [ ] 모든 이벤트에 사전 경고가 있고 회피 또는 대응 가능
- [ ] 처형 경고·퀴즈·추월 도전·최종 결투 중 새 이벤트가 시작되지 않음
- [ ] 이벤트가 차량을 직접 탈락시키거나 트랙을 영구적으로 막지 않음
- [ ] 플레이어와 AI에 동일한 물리·피해 규칙 적용

**필수 검증:**

- [ ] 이벤트 선택 가중치·연속 중복 방지 테스트
- [ ] 상태별 이벤트 차단 테스트
- [ ] 덤프트럭 충돌·지진 흔들림·운석 반경 피해 테스트
- [ ] 이벤트 중 재시작·처형·차량 대파 안전성 테스트
- [ ] Windows·WebGL 풀링과 성능 스모크

### 단계 12 — 카메라·VFX·오디오·지옥 트랙

**목표:** 조작 가독성을 유지하면서 GDD의 지옥 레이스 연출을 완성한다.

- [ ] 속도 기반 카메라 거리·FOV와 충돌 회피
- [ ] 나란히 달릴 때 두 차량 투샷
- [ ] 저주 대상과 최종 관문 프레이밍
- [ ] 잠금·점화·경련·폭발 처형 VFX — 파괴 차량 조각 물리와 랜덤 폭발 파티클 구현, 점화·경련 예고 단계 미구현
- [ ] 지옥 주조 고속도로 랜드마크와 레이싱 라인
- [ ] 엔진·부스트·처형·저주·도전·관문·승리 오디오

**완료 조건:**

- [ ] 특수 카메라가 급커브·공중·조작 상실 때 즉시 해제됨
- [ ] 불·연기·파티클이 도로와 차량을 가리지 않음
- [ ] 잔해가 트랙을 영구적으로 막지 않음

**필수 검증:**

- [ ] 카메라 전환과 중단 조건 PlayMode 테스트
- [ ] 6대·VFX·복수 카메라 성능 테스트
- [ ] 색상 외 형태와 움직임으로 경고 구분 확인

### 단계 13 — 밸런스·외부 테스트·최종 빌드

**목표:** 완료 정의와 Windows·WebGL 출시 기준을 모두 통과한다.

- [ ] 레이스 시간을 3~5분 범위로 조정
- [ ] 신규 플레이어가 첫 탈락까지 생존하도록 초반 난이도 조정
- [ ] 특수 이벤트 빈도와 피해가 처형 긴장감을 방해하지 않도록 조정
- [ ] 외부 플레이테스터 최소 3명 진행
- [ ] 설정만 변경해 밸런스 반복
- [ ] 전체 자동 테스트와 저장소 검증
- [ ] Windows와 WebGL 최종 빌드

**완료 조건:**

- [ ] MVP 완료 조건 전체 체크
- [ ] 외부 테스트 3명이 핵심 규칙 설명 가능
- [ ] Missing Reference·중복 AudioListener·성능 저하 없음
- [ ] 출시 빌드에서 빠른 재시작과 전체 레이스 반복 가능

---

## 8. 기획 일치 및 차이 리뷰

### GDD와 일치하는 핵심 (기획 합의 사항 — 구현 상태는 별도)

이 목록의 `[x]`는 **기획이 확정되었다**는 뜻이며 구현 완료를 의미하지 않는다.
구현 상태는 오른쪽에 병기했다.

- [x] 1랩, 플레이어 1대와 AI 5대의 총 6대 구성 — 구현 (킷 선택 UI 기본값 경로)
- [x] 30초마다 최하위 처형, 두 대에서 탈락 중단 — 규칙 구현. 프리셋 값이 10초
- [x] 최종 두 대의 닫히는 관문 결투 — **규칙만.** 관문 오브젝트·트리거 없음
- [x] 기본 드리프트와 부스트를 포함한 아케이드 조작 — 부스트만 구현, 드리프트 미착수
- [x] 내구도 0은 직접 탈락이 아닌 대파 — 구현. 프리셋 값 때문에 발동하지 않음
- [x] 파열·엔진 봉인·영혼 교환 3종 — 구현 (영혼 교환 안전 조건 미완)
- [x] 레이스를 멈추지 않는 마우스 퀴즈 — 구현. 퀴즈의 역할은 **방어**로 확정 (2026-07-26)
- [x] 20초 주기 추월 도전과 실패 시 심판의 사슬 — 도전 구현, 사슬 감속 미연결
- [ ] 플레이어 메인 화면 유지와 처형 차량 오른쪽 아래 CCTV — 코드 구현, Play Mode 미검증
- [x] 지옥 주조 고속도로와 제한적인 시네마틱 카메라 — 트랙 작업 중, 시네마틱 카메라는 처형 CCTV만

### GDD 내부 모순을 최신안으로 정리한 항목

| 항목 | GDD 표현 | 체크리스트 적용 |
|---|---|---|
| 랩 수 | MVP는 1랩, 트랙 설명은 3랩 | 최신 MVP 기준 1랩 |
| 차량 수 | MVP와 기준표는 6대, 완료 정의는 5대 | 플레이어 1 + AI 5, 총 6대 |
| 퀴즈 시간 | 기준표 3초, 발동 흐름 4초 | 설정 기본값 4초로 확정 (방어 제한시간) |
| AI 수와 유형 | AI 5대, 성격 유형 4개 | 프로젝트 `AIPersonalityType` 4종, 폭주광 2 + 난폭자 1 + 봉쇄자 1 + 생존자 1 |

### GDD 초안을 프로젝트 구현 기준으로 갱신한 항목 (2026-07-26)

| 항목 | GDD 초안 | 확정안 | 사유 |
|---|---|---|---|
| 저주 ↔ 퀴즈 | 시전자가 퀴즈를 풀어 저주를 **적용** | 시전자는 즉시 발동, **대상이 퀴즈를 풀어 방어** | 이미 구현된 흐름이고, 퀴즈가 "표적이 됐다"는 위협 신호로 작동해 30초 처형 긴장감과 결이 맞는다 |
| 저주 성공/실패 주체 | 시전자의 실력 | 대상의 실력 | 발동은 쿨다운만 차면 성공. 결과는 대상이 정한다 |
| AI 성격 축 | `AIArchetype` (난폭자·스피드스터·책략가·생존자) | `AIPersonalityType` (난폭자·폭주광·봉쇄자·생존자) | 런타임에 구현·검증된 축이 후자다. 설정 쪽 enum은 소비된 적이 없다 |
| 책략가 | 저주를 자주 쓰는 성격 | **봉쇄자** — 진로 차단으로 순위를 조작 | 방어형 흐름에서는 "저주를 자주 쓴다"는 축이 성립하지 않는다 |
| 성격별 저주 표현 | `curseTendency` (사용 성향) | `quizAvoidChance` (방어율) | 같은 이유. 값은 이미 성격별로 존재한다 |

### 사용자 요청으로 확장된 기획

최신 GDD는 트랙 위험 요소를 절제하라고 했지만, 최종 요청에 따라 아래 세 가지 주행 특수 이벤트를 게임 잼 구현 범위에 포함한다.

| 이벤트 | 구현 범위 | 직접 탈락 |
|---|---|---|
| 덤프트럭 습격 | 경고 후 위험 차선으로 돌진, 충돌 피해와 밀침 | 불가 |
| 지진 | 전조 후 전체 차량에 약한 횡방향 흔들림과 카메라 반응 | 불가 |
| 운석 낙하 | 경고 지점 표시 후 반경 피해와 밀침 | 불가 |

특수 이벤트는 `SpecialEventSettings`로 빈도와 강도를 조절하며 처형 경고, 퀴즈, 추월 도전, 최종 결투와 겹치지 않게 한다. 이 추가는 기존 GDD 대비 명시적인 범위 확장이다.

### GDD에 없지만 구현 안정성을 위해 추가한 항목

| 추가 항목 | 이유 | 게임 디자인 변경 여부 |
|---|---|---|
| `GameBalanceSettings`와 프리셋 | 코드 수정 없이 모든 수치 조정 | 없음 |
| 중앙 레이스 관리자 | 순위·탈락·승패의 중복 판정 방지 | 없음 |
| `FastTest` 프리셋 | 30초를 기다리지 않는 자동 테스트 | 없음 |
| 최종 결투 전 관문 승패 차단 | 1랩 선두가 너무 일찍 완주하는 경우 보호 | 없음 |
| AI 연속 저주 방지 | 플레이테스트의 공정성 요구 충족 | 없음 |

### 최종 확정 정책

기획 확정 여부이며, 오른쪽에 구현 반영 상태를 병기했다.

- [x] 퀴즈 제한 시간은 설정 기본값 4초 — **미반영.** 실제로는 문제 종류별 리터럴 3/4/5초
- [x] 저주는 파열 → 엔진 봉인 → 영혼 교환 순서로 순환 — 반영 (`CurseCooldownState.NextOrdered`)
- [x] 자동 대상은 가장 가까운 전방 차량, 없으면 가장 가까운 활성 차량 — 반영 (`NearestAheadThenNearestActive`)
- [x] 추월 성공 보상은 부스트 한 칸과 저주 쿨다운 즉시 초기화 — 반영
- [x] 퀴즈 표시 언어는 기존 프로젝트 정책대로 영어 — 반영 (설정 필드 없이 생성기 수준에서 충족)
- [x] 저주는 시전자가 즉시 발동하고 대상이 퀴즈로 방어한다 — 반영 (2026-07-26 확정)
- [x] AI 성격 축은 `AIPersonalityType` 4종, 구성은 폭주광 2대 + 난폭자 1대 + 봉쇄자 1대 + 생존자 1대 — 반영 완료 (`AISettings.personalityAssignments`)
- [x] 성격 차이는 저주 방어율로 표현한다 (생존자 0.8 > 봉쇄자 0.65 > 난폭자 0.35 > 폭주광 0.3) — 반영 완료. `AISettings.personalityProfiles[].quizAvoidChance`로 이전하고 순서 규칙을 테스트로 고정
- [x] 특수 이벤트는 덤프트럭·지진·운석 세 종류 — **미반영.** 레거시 6종이 살아 있고 덤프트럭은 없다
- [x] 특수 이벤트 수치와 활성 조건은 모두 설정에서 변경 — **미반영.** 컴포넌트 인스펙터 값

### 주요 설계 리스크

- **1랩 길이:** 총 6대가 두 대가 되려면 기본값으로 120초가 필요하므로 최종 진입이 그보다 빨라서는 안 된다.
- **복수 카메라:** 처형 화면과 플레이어 축소 화면을 동시에 렌더링하므로 WebGL 성능 검증이 필요하다.
- **마우스 퀴즈:** 주행 입력과 포인터 조작을 동시에 요구하므로 버튼 크기와 화면 위치를 조기에 테스트해야 한다.
- **방어형 저주의 예고 없는 퀴즈:** 퀴즈가 예고 없이 뜨면 "왜 갑자기?"로 읽힌다. 표적 신호와 시전자 표시가 없으면 방어형 흐름의 재미가 성립하지 않으므로 3.7·3.11의 신호 항목을 저순위로 미루지 않는다.
- **AI 5대와 네 성격:** 중복 배정되는 성격은 `AIPersonalityAssigner.Personalities` 배열 0번이 결정한다. 프리셋으로 옮기기 전까지는 이 배열이 사실상 기획 문서이므로, 순서를 바꿀 때 테스트로 고정해 둔다.
- **특수 이벤트 과부하:** 처형·퀴즈·도전과 동시에 발생하면 가독성이 무너지므로 상태 차단 규칙과 최대 동시 개수를 반드시 지킨다.
- **WebGL 이벤트 성능:** 덤프트럭·운석·지진 VFX는 풀링하고 물리 오브젝트 수를 제한해야 한다.

---

## 9. MVP 완료 조건

- [ ] 메뉴부터 결과 화면까지 개발자 개입 없이 진행 — 관문 승리가 발생하지 않아 결과까지 도달 경로가 불완전
- [ ] 레이스가 안정적으로 3~5분 진행 — 현재 프리셋 기준 40초면 결투 도달
- [ ] 플레이어 1대와 AI 5대가 정상 레이스 — 구성은 맞음, Play Mode 미검증
- [ ] 30초마다 현재 최하위 차량 처형 — 규칙 구현, 프리셋 값 10초
- [ ] 충돌·리셋·영혼 교환 중에도 순위와 탈락 판정 신뢰 가능 — 영혼 교환이 월드 좌표를 맞바꿔 트랙 진행도 기준 교환이 아니다
- [x] 플레이어가 운전 중 저주를 사용하고, 표적이 된 쪽은 퀴즈로 방어 — 구현 + 기획 확정 (2026-07-26)
- [ ] 네 성격(폭주광 2·난폭자·봉쇄자·생존자)이 확정 구성대로 배분되고 위협 차이가 보임 — 현재 난폭자 2대
- [ ] AI가 피할 수 없는 연속 처벌 없이 플레이어 공격 — 표적 유예·사전 신호 미구현
- [ ] 추월 도전 성공과 실패 모두 정상 처리 — 실패 감속 미연결
- [ ] 덤프트럭·지진·운석 특수 이벤트가 사전 경고와 함께 정상 발생 — 미착수
- [ ] 특수 이벤트가 처형·퀴즈·추월 도전·최종 결투 가독성을 방해하지 않음 — 차단 규칙 없음
- [ ] 두 대가 남으면 닫히는 관문 질주로 전환 — 상태 전환은 되지만 관문 오브젝트가 없다
- [ ] 관문을 먼저 통과한 차량만 승리 — **트리거 없음 + 킷 `RaceFinish` 랩 완주 승리 잔존**
- [ ] 승리와 패배가 시각적·기계적으로 명확 — 킷 결과 UI 의존, 규정 시간 미구현
- [ ] 재시작이 빠르고 이전 상태가 남지 않음 — 코드 연결 완료, 연속 Play Mode 검증 필요
- [ ] 폭발·차량 6대·UI·복수 카메라·특수 이벤트가 겹쳐도 성능 안정 — 미측정
- [ ] 외부 플레이테스터 3명이 핵심 규칙 설명 가능 — 미진행. 규칙 HUD가 없어 현 상태로는 어렵다

---

## 10. 작업 원칙

- 가독성, 레이스 완주 가능성, 30초 최하위 긴장감을 해치는 기능은 단순화하거나 제외한다.
- 문서의 숫자는 설정 기본값일 뿐이며 런타임 코드에 직접 작성하지 않는다.
- 새 숫자 기반 동작을 추가할 때는 먼저 적절한 설정 그룹에 필드를 추가한다.
- UI에 표시되는 시간과 실제 판정 시간은 같은 설정 필드를 사용한다.
- AI와 플레이어에게 같은 효과를 적용할 때는 별도 숫자를 복제하지 않고 공용 효과 설정을 사용한다.
- 특수 이벤트는 차량에 피해와 순위 손실을 줄 수 있지만 직접 탈락 판정을 내리지 않는다.
- 특수 이벤트보다 처형 경고, 퀴즈, 추월 도전, 최종 결투의 가독성을 우선한다.
- P0가 완성되기 전에는 스트레치 작업을 우선하지 않는다.
- 개별 차량이 탈락·승리·관문 결과를 독립적으로 판정하지 않는다.
- 구현 완료 후 테스트 결과와 함께 체크박스를 갱신한다.
- Git 병합 충돌, Unity YAML 충돌, 중복 클래스, 중복 GUID가 발생하면 임의로 해결하지 않고 충돌 내용과 권장안을 먼저 팀에 공유한다.

---

## 11. 2026-07-26 리뷰가 도출한 다음 작업 순서

P0 레이스가 성립하지 않게 만드는 것부터 정렬했다. 위쪽 4개는 개별 작업량이 작다.

**즉시 (에셋·씬 조작만, 코드 변경 없음)**

1. `Assets/GameBalance/Resources/`(프리셋 2종 + `.meta`)와 `ExecutionCctvDirector.cs`(+`.meta`)를 Git에 추가.
   지금 커밋하면 프리셋 없는 상태가 푸시된다.
2. `GameJamDefault`: `elimination.intervalSeconds` 10 → 30, `damage.maximumDurability` 1000000 → 100.
   현재는 프리셋이 `Validate()` 오류를 내는 상태다.
3. `GMTK_Race.unity`의 `RandomEventManager.enableEvents`를 `0`으로 내려 레거시 이벤트를 끈다.
4. `GMTK_Race.unity`에서 킷 `RaceFinish` 컴포넌트를 제거하거나 비활성화한다.
   랩 완주 승리 우회가 목적이며 재시작 NullReferenceException은 코드에서 방어 완료했다.

**P0 차단 요소 (코드 작업)**

5. ~~최종 관문 실물~~ → **코드 완료 (2026-07-26).** `FinalGateDoors`가 결승선 위에 관문과 통과 트리거를
   런타임 생성하고 `ReportGateCrossing`을 호출한다. 자동 승리 폴백은 실제 선두 판정으로 교체.
   **남은 일: Play Mode에서 관문 위치·방향·폭(16m)과 폐쇄 시퀀스 확인.**
6. ~~규칙 HUD 배치~~ → **부분 완료.** `RaceUI/RaceAlertPanel`에 목표·처형 경고·추월/관문 TMP를 통합했다.
   남은 일은 처형 타이머 상시 표시, 월드 마커, 경고 우선순위 정리다.
7. `race.aiCount` / `race.lapCount`를 `RaceData`에 주입해 프리셋이 실제 차량 수·랩 수를 정하게 한다.
8. 추월 실패 속박: `OvertakeManager.BindSpeedMultiplier`를 `GmtkVehicleAdapter` 경로에서 소비한다.

**확정 기획 반영 (2026-07-26 결정 사항)**

9. ~~성격 구성 확정~~ → **완료 (2026-07-26).** 구성을 코드 배열에서 `AISettings.personalityAssignments`로
   옮겨 프리셋이 정하게 했다. 로스터에 authored-composition 오버로드를 추가하고 6케이스로 고정했다.
10. ~~`AiPersonalityProfile` 설정화~~ → **완료 (2026-07-26).** `AIArchetype` 계열을 제거하고
    `AIPersonalityType` 키의 `AiPersonalityProfile`(페이스·측면 강도·부스트 성향·방어율·따라잡기)을 만들었다.
    흩어져 있던 세 곳을 한 행으로 통합. 세 어셈블리 컴파일 0 오류, EditMode 테스트 14케이스 추가·미실행.
11. 표적 사전 신호와 시전자 표시를 넣는다. 방어형 흐름에서 퀴즈가 예고 없이 뜨는 것이 최대 약점이다.
12. `hostileEffectGraceSeconds`(기본 2초)를 `AISettings`에 추가하고, 방어 판정 종료 직후
    같은 플레이어가 다시 표적이 되지 않게 막는다.
13. 성격별 `paceScale` 폭(0.86~1.0)을 넓혀 위협 차이를 체감 가능하게 한다. 이제 `personalityProfiles`
    한 곳에서 조정된다.
14. `boostTendency`·`catchupAcceleration` 소비자를 붙인다 (프로필에 자리는 있고 읽는 코드가 없다).

**품질·규칙 정리**

15. `BoostController`의 레거시 `Input` API를 새 Input System으로 교체.
16. `VehicleSettings`·`SpecialEventSettings` 그룹 추가와 "선언만" 필드 연결.
17. ~~`QuizSettings` 연결과 보기 수 확정~~ → **완료.** 제한시간과 보기 수를 생성기에 전달하고 일반 객관식은 4개로 고정.

**사용자가 직접 실행해야 하는 검증**

18. ~~`.meta` 생성과 프리셋 재저장~~ → **완료.** Unity가 `AIPersonalityType.cs.meta`를 생성하고
    프리셋 2종을 재직렬화했다. 두 에셋 모두 `personalityAssignments: [3,3,1,2,0]`(= 폭주광 2 +
    난폭자 1 + 봉쇄자 1 + 생존자 1)과 프로필 4행을 담고 있으며 폐기 키는 사라졌다.
    Unity가 새 필드로 재직렬화했다는 것은 스크립트가 에디터에서도 컴파일됐다는 뜻이다.
19. `unity command run_tests --mode EditMode`로 121개 케이스 통과 확인 — **아직 실행되지 않았다.**
20. Play Mode에서 처형 CCTV 구도·조작 지속·AudioListener 중복 여부 확인.
21. Play Mode에서 방어형 저주 흐름 확인 — 플레이어가 `E`를 눌렀을 때 퀴즈 없이 발동되는지,
    AI가 걸었을 때만 퀴즈가 뜨는지, 성격별 방어율 차이가 로그(`[Curse] 퀴즈 성공/실패`)로 보이는지.
22. Play Mode에서 성격 배분 로그(`AI personalities: seed=… 1=Reckless 2=…`)가 폭주광 2대를
    포함하는지 확인. 그리드 슬롯 순서는 시드마다 달라지는 게 정상이다.
