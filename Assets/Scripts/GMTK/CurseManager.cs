using System.Collections;
using System.Collections.Generic;
using Gmtk2026.GameBalance;
using Gmtk2026.Quiz;
using GMTK.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GMTK
{
    /// <summary>
    /// Owns curse activation for the race. The player casts with E while AI racers cast after
    /// staggered cooldown checks. Valid rivals are selected by <see cref="CurseController"/>,
    /// human targets receive a phone quiz, and AI targets resolve the same quiz through their
    /// personality-based success chance.
    /// </summary>
    public sealed class CurseManager : MonoBehaviour
    {
        private sealed class PendingCurse
        {
            public CurseController Caster;
            public CurseType Type;
            public int TargetIndex;
        }

        private sealed class PendingAiCurse
        {
            public CurseController Caster;
            public CurseType Type;
            public int TargetIndex;
            public Coroutine Routine;
        }

        public static CurseManager Instance { get; private set; }

        public static event System.Action<CurseController, CurseType, int> CurseActivated;
        public static event System.Action<CurseController, CurseType, int> CurseApplied;
        public static event System.Action<CurseController, CurseType, int, bool> CurseResolved;

        private GameEvents gameEvents;
        private QuizSessionController quiz;
        private PendingCurse pendingHumanCurse;
        private CurseController playerCurse;
        private Slider cooldownSlider;
        private TMP_Text penaltyFeedbackText;
        private Coroutine penaltyFeedbackRoutine;
        private readonly List<CurseController> aiCurses = new List<CurseController>();
        private readonly List<PendingAiCurse> pendingAiCurses =
            new List<PendingAiCurse>();
        private readonly List<GameObject> activeCurseEffects = new List<GameObject>();
        private readonly Dictionary<CurseController, float> aiNextCastAttemptAt =
            new Dictionary<CurseController, float>();

        private CurseSettings Settings => GameBalance.Current.curse;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<CurseManager>() == null)
                host.AddComponent<CurseManager>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            gameEvents = Race.Events;
            if (gameEvents != null)
            {
                gameEvents.RaceStartedEvent.AddListener(EnsureControllers);
                gameEvents.RestartRaceEvent.AddListener(EnsureControllers);
            }

            StartCoroutine(BindQuizSystem());
        }

        private void OnDestroy()
        {
            if (gameEvents != null)
            {
                gameEvents.RaceStartedEvent.RemoveListener(EnsureControllers);
                gameEvents.RestartRaceEvent.RemoveListener(EnsureControllers);
            }

            if (quiz != null)
                quiz.AnswerEvaluated -= OnHumanQuizEvaluated;

            ClearPendingAiCurses();
            ClearCurseEffects();
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            UpdateCooldownSlider();

            if (!Race.IsRaceInProgress || playerCurse == null)
                return;

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                playerCurse.CastAtAutoTarget();

            UpdateAiCasters();
        }

        public bool TryActivateCurse(
            CurseController caster,
            CurseType type,
            int targetIndex)
        {
            if (caster == null || pendingHumanCurse != null)
                return false;

            GameObject targetCar = Race.CarByIndex(targetIndex);
            CurseController target = targetCar != null
                ? targetCar.GetComponent<CurseController>()
                : null;
            if (target == null)
                return false;

            if (targetIndex == 0 || target.IsPlayer)
            {
                bool began = BeginHumanQuiz(caster, type, targetIndex);
                if (began)
                {
                    CurseActivated?.Invoke(caster, type, targetIndex);
                    LogCurseActivated(caster, type, targetIndex);
                }
                return began;
            }

            CurseActivated?.Invoke(caster, type, targetIndex);
            LogCurseActivated(caster, type, targetIndex);
            BeginAiQuizResolution(caster, type, targetIndex);
            return true;
        }

        private void BeginAiQuizResolution(
            CurseController caster,
            CurseType type,
            int targetIndex)
        {
            var pending = new PendingAiCurse
            {
                Caster = caster,
                Type = type,
                TargetIndex = targetIndex,
            };
            pendingAiCurses.Add(pending);
            pending.Routine = StartCoroutine(ResolveAiCurseAfterDelay(pending));
        }

        private IEnumerator ResolveAiCurseAfterDelay(PendingAiCurse pending)
        {
            float minimumDelay = Mathf.Min(
                Settings.aiQuizResolutionDelayMinimumSeconds,
                Settings.aiQuizResolutionDelayMaximumSeconds);
            float maximumDelay = Mathf.Max(
                Settings.aiQuizResolutionDelayMinimumSeconds,
                Settings.aiQuizResolutionDelayMaximumSeconds);
            float delay = Random.Range(
                minimumDelay,
                maximumDelay);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            pendingAiCurses.Remove(pending);
            if (!Race.IsRaceInProgress || pending.Caster == null)
                yield break;

            GameObject targetCar = Race.CarByIndex(pending.TargetIndex);
            if (targetCar == null || !targetCar.activeInHierarchy)
                yield break;

            float successChance = GetAiQuizSuccessChance(targetCar);
            bool succeeded = CurseCooldownState.ResolveAiQuiz(
                successChance,
                Random.value);
            bool penaltyApplied = false;
            if (!succeeded)
            {
                penaltyApplied = pending.Caster.ApplyPenalty(
                    pending.Type,
                    pending.TargetIndex);
            }
            if (penaltyApplied)
            {
                NotifyCurseApplied(
                    pending.Caster,
                    pending.Type,
                    pending.TargetIndex);
            }

            LogCurseResolved(
                pending.Caster,
                pending.Type,
                pending.TargetIndex,
                succeeded,
                penaltyApplied,
                $"AI ability roll after {delay:0.00}s");
            CurseResolved?.Invoke(
                pending.Caster,
                pending.Type,
                pending.TargetIndex,
                succeeded);
        }

        private bool BeginHumanQuiz(
            CurseController caster,
            CurseType type,
            int targetIndex)
        {
            if (quiz == null || quiz.State != QuizSessionState.Waiting)
                return false;

            pendingHumanCurse = new PendingCurse
            {
                Caster = caster,
                Type = type,
                TargetIndex = targetIndex,
            };

            quiz.enabled = true;
            if (quiz.TriggerNow())
                return true;

            pendingHumanCurse = null;
            return false;
        }

        private void OnHumanQuizEvaluated(QuizAnswerResult result)
        {
            PendingCurse pending = pendingHumanCurse;
            if (pending == null)
                return;

            pendingHumanCurse = null;
            bool penaltyApplied = false;
            if (!result.IsCorrect)
            {
                penaltyApplied = pending.Caster.ApplyPenalty(
                    pending.Type,
                    pending.TargetIndex);
                if (penaltyApplied)
                {
                    NotifyCurseApplied(
                        pending.Caster,
                        pending.Type,
                        pending.TargetIndex);
                    ShowPenaltyFeedback(pending.Type);
                }
            }

            LogCurseResolved(
                pending.Caster,
                pending.Type,
                pending.TargetIndex,
                result.IsCorrect,
                penaltyApplied,
                result.TimedOut ? "human quiz timeout" : "human quiz");
            CurseResolved?.Invoke(
                pending.Caster,
                pending.Type,
                pending.TargetIndex,
                result.IsCorrect);
        }

        /// <summary>
        /// How likely an AI target is to answer its (off-screen) defence quiz and shrug the curse
        /// off. One number per personality, stored beside the rest of that personality's values.
        /// </summary>
        private static float GetAiQuizSuccessChance(GameObject targetCar)
        {
            AISettings ai = GameBalance.Current.ai;
            AIPersonality personality = targetCar.GetComponent<AIPersonality>();

            return personality == null
                ? ai.defaultQuizAvoidChance
                : ai.QuizAvoidChanceOf(personality.type);
        }

        private void LogCurseActivated(
            CurseController caster,
            CurseType type,
            int targetIndex)
        {
            Debug.Log(
                $"[Curse] 발동 | {DescribeRacer(caster.RaceIndex)} -> " +
                $"{DescribeRacer(targetIndex)} | 저주: {type}");
        }

        private void LogCurseResolved(
            CurseController caster,
            CurseType type,
            int targetIndex,
            bool succeeded,
            bool penaltyApplied,
            string resolutionSource)
        {
            string target = DescribeRacer(targetIndex);
            if (succeeded)
            {
                Debug.Log(
                    $"[Curse] 퀴즈 성공 | {target} | 판정: {resolutionSource} | " +
                    "페널티 없음");
                return;
            }

            string penalty = DescribePenalty(caster, type);
            if (penaltyApplied)
            {
                Debug.Log(
                    $"[Curse] 퀴즈 실패 | {target} | 판정: {resolutionSource} | " +
                    $"페널티 적용: {penalty}");
            }
            else
            {
                Debug.LogWarning(
                    $"[Curse] 퀴즈 실패 | {target} | 판정: {resolutionSource} | " +
                    $"페널티 적용 실패: {penalty}");
            }
        }

        private static string DescribeRacer(int raceIndex)
        {
            GameObject car = Race.CarByIndex(raceIndex);
            string name = car != null ? car.name : "Unknown";
            string role = raceIndex == 0 ? "Player" : "AI";
            return $"{name} ({role}, index {raceIndex})";
        }

        private string DescribePenalty(CurseController caster, CurseType type)
        {
            switch (type)
            {
                case CurseType.Rupture:
                    return $"Rupture / 내구도 -{Settings.ruptureDurabilityDamage:0.#}";
                case CurseType.EngineSeal:
                    return
                        $"Engine Seal / 부스트 봉인 {Settings.engineSealDurationSeconds:0.#}초";
                case CurseType.SoulSwap:
                    return
                        $"Soul Swap / {DescribeRacer(caster.RaceIndex)}와 위치 교환";
                default:
                    return type.ToString();
            }
        }

        private void EnsureControllers()
        {
            ClearPendingAiCurses();
            ClearCurseEffects();
            pendingHumanCurse = null;
            playerCurse = null;
            aiCurses.Clear();
            aiNextCastAttemptAt.Clear();

            foreach (int index in Race.AllCarIndices())
            {
                GameObject car = Race.CarByIndex(index);
                if (car == null) continue;

                CurseController controller = car.GetComponent<CurseController>();
                if (controller == null)
                    controller = car.AddComponent<CurseController>();
                else
                    controller.ResetForRace();

                controller.RaceIndex = index;
                if (index == 0)
                    playerCurse = controller;
                else
                {
                    aiCurses.Add(controller);
                    aiNextCastAttemptAt[controller] = Time.time + Random.Range(
                        Settings.aiInitialCastDelayMinimumSeconds,
                        Settings.aiInitialCastDelayMaximumSeconds);
                }
            }

            EnsureCooldownSlider();
            EnsurePenaltyFeedback();
        }

        private void ClearPendingAiCurses()
        {
            foreach (PendingAiCurse pending in pendingAiCurses)
            {
                if (pending?.Routine != null)
                    StopCoroutine(pending.Routine);
            }
            pendingAiCurses.Clear();
        }

        private void NotifyCurseApplied(
            CurseController caster,
            CurseType type,
            int targetIndex)
        {
            SpawnCurseEffect(targetIndex);
            CurseApplied?.Invoke(caster, type, targetIndex);
        }

        private void SpawnCurseEffect(int targetIndex)
        {
            GameObject target = Race.CarByIndex(targetIndex);
            GameObject prefab = GameBalance.Current.presentation.curseAppliedEffectPrefab;
            if (target == null || prefab == null)
                return;

            activeCurseEffects.RemoveAll(effect => effect == null);
            Vector3 center = FindVehicleVisualCenter(target);
            GameObject effect = Instantiate(prefab, center, Quaternion.identity);
            effect.name = $"FX_Cursed_{target.name}";
            effect.transform.localScale *=
                GameBalance.Current.presentation.curseAppliedEffectScale;
            effect.transform.SetParent(target.transform, true);
            activeCurseEffects.Add(effect);
        }

        private static Vector3 FindVehicleVisualCenter(GameObject vehicle)
        {
            Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combined = default;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null ||
                    renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer ||
                    renderer is LineRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? combined.center : vehicle.transform.position;
        }

        private void ClearCurseEffects()
        {
            foreach (GameObject effect in activeCurseEffects)
                if (effect != null)
                    Destroy(effect);
            activeCurseEffects.Clear();
        }

        private void UpdateAiCasters()
        {
            if (pendingHumanCurse != null)
                return;

            for (int i = 0; i < aiCurses.Count; i++)
            {
                CurseController caster = aiCurses[i];
                if (caster == null || !caster.CanCast)
                    continue;

                if (aiNextCastAttemptAt.TryGetValue(caster, out float nextAttempt) &&
                    Time.time < nextAttempt)
                {
                    continue;
                }

                bool cast = caster.CastAtAutoTarget();
                aiNextCastAttemptAt[caster] = Time.time +
                    (cast ? 0f : Settings.aiFailedCastRetrySeconds);

                // Only one unresolved human phone quiz can be active at a time.
                if (pendingHumanCurse != null)
                    return;
            }
        }

        private IEnumerator BindQuizSystem()
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (quiz == null && Time.realtimeSinceStartup < deadline)
            {
                quiz = FindFirstObjectByType<QuizSessionController>(
                    FindObjectsInactive.Include);
                if (quiz == null)
                    yield return null;
            }

            if (quiz != null)
            {
                QuizSettings settings = GameBalance.Current.quiz;
                quiz.ConfigureQuestionRules(
                    settings.answerTimeSeconds,
                    settings.minimumAnswerCount,
                    settings.maximumAnswerCount);
                quiz.AnswerEvaluated += OnHumanQuizEvaluated;
            }
        }

        private void EnsureCooldownSlider()
        {
            if (cooldownSlider != null)
                return;

            StartMenuCanvas menu = FindFirstObjectByType<StartMenuCanvas>(
                FindObjectsInactive.Include);
            if (menu == null)
                return;

            menu.EnsureUi();
            if (menu.RacePanel == null)
                return;

            Transform curseSkill = menu.RacePanel.transform.Find("Curse Skill");
            if (curseSkill == null)
                return;

            cooldownSlider = curseSkill.GetComponent<Slider>();
            if (cooldownSlider == null)
                return;

            cooldownSlider.minValue = 0f;
            cooldownSlider.maxValue = 1f;
            cooldownSlider.wholeNumbers = false;
            cooldownSlider.interactable = false;
            cooldownSlider.SetValueWithoutNotify(1f);
        }

        private void UpdateCooldownSlider()
        {
            if (cooldownSlider == null || playerCurse == null)
                return;

            cooldownSlider.SetValueWithoutNotify(playerCurse.CooldownProgress);
        }

        private void EnsurePenaltyFeedback()
        {
            if (penaltyFeedbackText != null)
                return;

            StartMenuCanvas menu = FindFirstObjectByType<StartMenuCanvas>(
                FindObjectsInactive.Include);
            if (menu == null)
                return;

            menu.EnsureUi();
            if (menu.RacePanel == null)
                return;

            Transform existing = menu.RacePanel.transform.Find("CursePenaltyFeedback");
            if (existing != null)
            {
                penaltyFeedbackText = existing.GetComponent<TMP_Text>();
                return;
            }

            GameObject label = new GameObject(
                "CursePenaltyFeedback",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            label.transform.SetParent(menu.RacePanel.transform, false);

            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.72f);
            rect.anchorMax = new Vector2(0.5f, 0.72f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(960f, 150f);

            penaltyFeedbackText = label.GetComponent<TextMeshProUGUI>();
            penaltyFeedbackText.alignment = TextAlignmentOptions.Center;
            penaltyFeedbackText.color = new Color(1f, 0.24f, 0.08f, 1f);
            penaltyFeedbackText.fontSize = 42f;
            penaltyFeedbackText.fontStyle = FontStyles.Bold;
            penaltyFeedbackText.outlineColor = Color.black;
            penaltyFeedbackText.outlineWidth = 0.22f;
            penaltyFeedbackText.raycastTarget = false;
            label.SetActive(false);
        }

        private void ShowPenaltyFeedback(CurseType type)
        {
            EnsurePenaltyFeedback();
            if (penaltyFeedbackText == null)
                return;

            if (penaltyFeedbackRoutine != null)
                StopCoroutine(penaltyFeedbackRoutine);

            penaltyFeedbackText.text = DescribePenaltyForUi(type);
            penaltyFeedbackText.gameObject.SetActive(true);
            penaltyFeedbackRoutine = StartCoroutine(HidePenaltyFeedbackAfterDelay());
        }

        private IEnumerator HidePenaltyFeedbackAfterDelay()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            if (penaltyFeedbackText != null)
                penaltyFeedbackText.gameObject.SetActive(false);
            penaltyFeedbackRoutine = null;
        }

        private string DescribePenaltyForUi(CurseType type)
        {
            switch (type)
            {
                case CurseType.Rupture:
                    return
                        $"RUPTURE\nDURABILITY -{Settings.ruptureDurabilityDamage:0.#}";
                case CurseType.EngineSeal:
                    return
                        $"ENGINE SEALED\nBOOST DISABLED FOR " +
                        $"{Settings.engineSealDurationSeconds:0.#}s";
                case CurseType.SoulSwap:
                    return "SOUL SWAP\nPOSITIONS EXCHANGED";
                default:
                    return $"CURSED\n{type.ToString().ToUpperInvariant()}";
            }
        }
    }
}
