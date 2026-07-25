# Project-GMTK-2026 — Unity 기술 개발 지침

GMTK 2026 잼 출품용 레이싱 게임. 이 파일은 이 저장소에서 작업하는 에이전트의 **기본 규칙**이다.
사용자의 명시적 지시가 이 파일보다 우선한다. 응답과 커밋 설명은 한국어로 쓴다.

## 프로젝트 스냅샷

- Unity **6000.3.19f1 고정** (URP 17.3, Input System 1.19, Entities 1.4.6, Test Framework 1.6)
- 차량: **RCCP Lite 2.22** 기준. `Racing Starter Kit`은 레이스·체크포인트·순위
  인프라만 유지하고 차량 생성·입력·AI는 `GMTK/Rccp` 어댑터가 담당
- 저장소: `Rounless-Studios/Project-GMTK-2026`, 통합 브랜치는 `main`
- 게임플레이 축: 레이스 + 퀴즈/저주(Curse)/부스트/내구도(Durability)/탈락(Elimination) + 랜덤 이벤트

## 코드 배치

| 위치 | 어셈블리 / 네임스페이스 | 성격 |
|---|---|---|
| `Assets/Scripts/GMTK/` | Assembly-CSharp, `namespace GMTK` | 게임플레이 MonoBehaviour (`Race`, `*Manager`, `*Controller`) |
| `Assets/Scripts/GMTK/Rccp/` | `namespace GMTK.Rccp` | **RCCP 의존을 격리하는 유일한 경계**. 다른 곳에서 `RCCP_*` 직접 참조 금지 |
| `Assets/GameBalance/` | `GMTK.GameBalance.Runtime` / `Gmtk2026.GameBalance` | 엔진 비의존 순수 상태 로직 + EditMode 테스트 |
| `Assets/Quiz/` | `GMTK.Quiz.Runtime` / `Gmtk2026.Quiz` | 퀴즈/미니게임 (`Assets/Quiz/README.md` 참고) |
| `Assets/Editor/CI/CIBuild.cs` | Editor | CI 빌드 진입점. 임의 변경 금지 |

**새 규칙/수치 로직은 `GameBalance`에 순수 C#으로 넣고 테스트를 붙인다.** MonoBehaviour는 입력·표현·수명주기만 담당하는 얇은 껍데기로 유지한다.

## 절대 금지

- Unity 버전, `ProjectSettings/`(입력·물리·태그·렌더링·PlayerSettings), WebGL 압축 설정을 **요청 없이** 변경
- `.meta` 파일 직접 생성·삭제·GUID 수정 — 에디터가 만들게 한다. 반대로 새 에셋 커밋 시 `.meta` 누락도 금지
- `.unity` / `.prefab` / `.asset` YAML을 텍스트 에디터로 손편집 (아래 "에디터 조작"으로 처리)
- 서드파티 폴더 수정: `Assets/BxB Studio/`, `Assets/Racing Starter Kit/`, `Assets/Realistic Car Controller Pro/`, `Assets/SkySeries Freebie/`, `Assets/[Free] Phone/`, `Packages/dev.bxbstudio.*`
  → 필요하면 `Assets/Scripts/GMTK/` 쪽에 어댑터/브릿지를 만든다
- `Library/`, `Temp/`, `Logs/`, `*.csproj`, `*.sln`, 빌드 산출물 커밋 (`.gitignore` 준수)
- `main` force-push. 사용자가 요청하지 않은 커밋/푸시/PR 생성

## C# / Unity 코딩 규칙

- 인스펙터 노출은 `[SerializeField] private`. `public` 필드 금지
- 컴포넌트 참조는 `Awake()`/`Start()`에서 캐시. `Update`/`FixedUpdate`/`LateUpdate` 안에서
  `GetComponent` / `FindObjectOfType` / `GameObject.Find` / `Camera.main` 호출 금지
- 입력은 **새 Input System만** (`InputSystem_Actions.inputactions`). `Input.GetKey*`, `Input.GetAxis*` 금지
- 물리·차량 힘은 `FixedUpdate`에서, 연출·카메라는 `Update`/`LateUpdate`에서. `Time.deltaTime` 누락 주의
- 매 프레임 경로에서 LINQ·`new`·문자열 조합·`Debug.Log` 금지. 로그는 원인 규명 시에만, 남기지 않는다
- `ScriptableObject`에는 `[CreateAssetMenu]`. 백그라운드 스레드에서 Unity API 호출 금지
- `.editorconfig` 준수: C# 4 space, JSON/asmdef 2 space, UTF-8, LF, 마지막 개행
- 기존 파일의 주석 밀도·명명·스타일을 따른다. 요청되지 않은 리팩터링·파일 분할을 끼워넣지 않는다

## Unity 에디터 조작 (핵심 도구)

에디터가 켜져 있으면 **Unity CLI로 실행 중인 에디터를 직접 조작**한다 (`com.unity.pipeline`이 약 100개 명령을 노출).
씬/프리팹 편집은 이 경로만 사용한다. `unity`는 PATH에 없으므로 실행 파일을 직접 지정한다.

```bash
# Bash 도구:        U="$LOCALAPPDATA/Unity/bin/unity.exe"; "$U" status
# PowerShell:       & "$env:LOCALAPPDATA\Unity\bin\unity.exe" status
"$U" status                          # 연결된 에디터(포트/프로젝트/상태) — 항상 이것부터
"$U" command                         # 사용 가능한 명령 + 파라미터 전체 목록
"$U" command get_scene_hierarchy     # 씬 구조 파악
"$U" command open_scene --path "Assets/Scenes/bora.unity"
"$U" command recompile ; "$U" command recompile_status   # 컴파일 확인
"$U" command console --tail 50 --level error             # 에러 로그
"$U" command run_tests --mode EditMode                   # 테스트, 이후 test_status
"$U" command editor_play / editor_stop / editor_pause
"$U" command screenshot --view game --output <경로>       # 시각 확인
"$U" command get_performance_stats                       # 프레임/메모리
```

- 경로는 `Assets`(authoring root) 기준. `create_scene`/`create_script`/`import_asset`도 이 루트 아래로만
- `set_*_settings`, `delete_asset`, `switch_build_target` 등 파괴적 명령은 **`--dry_run` 먼저**, `--confirm`은 사용자 승인 후
- `create_script`로 만든 타입은 `recompile` → `recompile_status` 완료 전에는 `attach_script` 불가
- 에디터가 안 떠 있으면: 테스트는 `"$U" test --mode EditMode`(배치 모드, 프로젝트 루트에서), 빌드는 `"$U" build`
- 스크립트 편집 자체는 파일 도구로 직접 한다. `eval`은 조사용 최후 수단
- **플레이 중 `eval`을 부르면 강제 동기 재컴파일 → 어셈블리 리로드로 플레이 모드가 끊긴다.** 이 프로젝트는
  Enter Play Mode Options(도메인·씬 리로드 비활성)를 쓰므로 더 잘 끊긴다. 런타임 상태는 로그로 보고,
  꼭 값을 읽어야 하면 **주행 마지막에 eval 한 번**으로 필요한 값을 전부 덤프한다 (반환값은 리로드 전에 나온다)
- 콘솔 버퍼는 800줄이라 `AudioListener` 중복 경고처럼 매 프레임 찍히는 로그가 있으면 6초 만에 에러가
  밀려난다. `console`이 비어 보여도 안심하지 말고 `%LOCALAPPDATA%\Unity\Editor\Editor.log`로 교차 확인

## 검증 정책

- 컴파일 통과는 동작 확인이 아니다. **"동작한다"는 표현은 실제 실행·테스트·스크린샷으로 확인했을 때만** 쓴다
- 순수 로직 변경 → EditMode 테스트 추가/갱신 후 `run_tests`. 기존 테스트: `Assets/{GameBalance,Quiz}/Tests/Editor/`
- 씬·프리팹·물리 값 변경은 자동 검증이 어렵다. 무엇을 어떻게 바꿨는지와 **사람이 확인해야 할 항목**을 명시한다
- 확인하지 못한 추정은 `[미검증]`으로 표시한다. 테스트가 깨지면 출력과 함께 그대로 보고한다
- 콘솔 에러/워닝을 확인 없이 "무해하다"고 넘기지 않는다

## Git / 통합 (`COLLABORATION.md`)

- 브랜치: `feat/<slug>`, `fix/<slug>`, `polish/<slug>`, `chore/<slug>`. 짧게 유지, squash merge
- **다른 브랜치가 이미 체크아웃돼 있으면 `git worktree`로 작업 공간을 분리해서 올린다.** 여러 사람·에이전트가
  한 워킹트리를 공유하면 미커밋 변경이 상시 섞여 브랜치 전환이 막히고, `git commit -a` 한 번이 남의 작업을
  쓸어담는다
  - `git worktree add D:\Project-GMTK-2026-<용도> -b feat/<slug> origin/develop` — `.git`과 LFS 저장소를
    공유하므로 클론보다 훨씬 가볍고, 같은 브랜치 중복 체크아웃은 git이 막아준다
  - 내가 만들거나 고친 파일만 새 워크트리로 복사해 커밋한다. 옮기기 전에
    `git diff origin/develop -- <파일>`로 남의 변경이 섞이지 않았는지 확인한다
  - `origin/develop`에서 브랜치를 파면 upstream이 `develop`으로 잡히므로, 푸시는
    `git push -u origin feat/<slug>`처럼 대상을 반드시 명시한다
  - 머지는 `git checkout develop && git merge --ff-only feat/<slug>` 후 푸시. 끝나면 워크트리를 피처
    브랜치로 되돌려 `develop`을 비워둔다 (다른 워크트리가 잡을 수 있게)
  - 새 워크트리에는 `Library/`가 없어 첫 Unity 실행에서 전체 임포트가 한 번 돈다. 코드·테스트만 확인할
    때는 배치 모드(`unity test --mode EditMode`)로 충분하다
- 씬·프리팹은 한 사람이 소유. 충돌 큰 파일은 프리팹/컴포넌트 경계로 나눠 작업
- 커밋 범위는 작업 단위로 자른다. 자동 생성된 `.mat`/`ProjectSettings` 잡음 diff를 함께 커밋하지 않는다
- 외부 아트/오디오는 임포트 시점에 출처·라이선스·저작자 기록 (필수)
- CI/릴리스 세부는 `docs/CI-CD.md` (PR: 테스트 + WebGL 스모크 / `main`: staging 배포 / 프로덕션: 수동 승인)

## 작업 방식

- 조사는 병렬로. 서드파티 대용량 소스는 통독하지 말고 `Grep`으로 필요한 심볼만 찾는다
- 비자명한 변경(씬 구조, RCCP 배선, 물리 튜닝)은 먼저 계획을 짧게 제시하고 진행한다
- 요청된 범위만 완결한다. 막힌 부분이 있으면 나머지를 끝내고 무엇을 왜 남겼는지 밝힌다
- 반복 확인이 필요한 사실(포트, 절차, 실패 원인)은 memento에 `topic="project-gmtk-2026"`으로 저장한다
