using System.Collections.Generic;
using Gmtk2026.GameBalance;
using GMTK.Rccp;
using UnityEditor;
using UnityEngine;

namespace GMTK.EditorTools
{
    /// <summary>
    /// Tuning bench for the waypoint AI: the numbers on the left, what the cars are actually doing on
    /// the right, in one window so a change can be judged in the same lap it was made.
    /// <para>
    /// A race normally reads a frozen snapshot of the balance preset so it cannot change under the
    /// players. "Live tuning" points the systems back at the live asset instead — that is what makes
    /// dragging a slider mid-race do anything. It is off by default and never serialised.
    /// </para>
    /// </summary>
    public sealed class GmtkAiTunerWindow : EditorWindow
    {
        private static readonly AIPersonalityType[] Personalities =
        {
            AIPersonalityType.Reckless,
            AIPersonalityType.Rammer,
            AIPersonalityType.Blocker,
            AIPersonalityType.CleanRacer,
        };

        private readonly List<GmtkRccpWaypointDriver> drivers = new();
        private Vector2 scroll;
        private double nextRefresh;

        [MenuItem("GMTK/AI Tuner")]
        public static void Open()
        {
            GetWindow<GmtkAiTunerWindow>("AI Tuner").minSize = new Vector2(430f, 460f);
        }

        private void OnEnable() => EditorApplication.update += RepaintWhilePlaying;

        private void OnDisable() => EditorApplication.update -= RepaintWhilePlaying;

        /// <summary>The live table is worthless if it only updates when the mouse moves.</summary>
        private void RepaintWhilePlaying()
        {
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < nextRefresh) return;

            nextRefresh = EditorApplication.timeSinceStartup + 0.2f;
            Repaint();
        }

        private void OnGUI()
        {
            GameBalanceSettings preset = GameBalance.Active;
            if (preset == null)
            {
                EditorGUILayout.HelpBox("No active balance preset.", MessageType.Error);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawLiveTuningHeader(preset);
            EditorGUILayout.Space();
            DrawDrivingSettings(preset);
            EditorGUILayout.Space();
            DrawPersonalityTable(preset);
            EditorGUILayout.Space();
            DrawLiveCars();

            EditorGUILayout.EndScrollView();
        }

        private static void DrawLiveTuningHeader(GameBalanceSettings preset)
        {
            EditorGUILayout.LabelField($"프리셋: {preset.name}", EditorStyles.boldLabel);

            bool live = EditorGUILayout.ToggleLeft(
                "라이브 튜닝 (주행 중 값 변경을 즉시 반영)", GameBalance.LiveTuning);
            if (live != GameBalance.LiveTuning) GameBalance.LiveTuning = live;

            if (EditorApplication.isPlaying && !live)
                EditorGUILayout.HelpBox(
                    "레이스는 시작 시점의 스냅샷을 읽습니다. 지금 값을 바꿔도 이번 주행에는 반영되지 않습니다.",
                    MessageType.Info);

            EditorGUILayout.HelpBox(
                "여기서 바꾼 값은 프리셋 에셋에 저장됩니다. 성격별 값을 GDD 기준으로 되돌리려면 " +
                "GMTK/Game Balance/Normalize Presets To GDD 를 실행하세요.",
                MessageType.None);
        }

        private static void DrawDrivingSettings(GameBalanceSettings preset)
        {
            AiDrivingSettings d = preset.ai.driving;
            EditorGUILayout.LabelField("주행 (전 AI 공통)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            float straight = EditorGUILayout.Slider("직선 목표 속도 kph", d.straightSpeedKph, 80f, 260f);
            float grip = EditorGUILayout.Slider("코너 그립 m/s2", d.cornerGrip, 6f, 26f);
            float minCorner = EditorGUILayout.Slider("최저 코너 속도 kph", d.minCornerSpeedKph, 20f, 120f);
            float apex = EditorGUILayout.Slider("에이펙스 안쪽 m", d.apexOffsetMetres, 0f, 8f);

            EditorGUILayout.LabelField("라이벌", EditorStyles.miniBoldLabel);
            float scan = EditorGUILayout.Slider("인지 거리 m", d.rivalScanMetres, 10f, 90f);
            float overtake = EditorGUILayout.Slider("추월 오프셋 m", d.overtakeOffsetMetres, 0f, 8f);
            float closing = EditorGUILayout.Slider("추월 최소 접근 kph", d.overtakeMinClosingKph, 0f, 30f);
            float follow = EditorGUILayout.Slider("추종 간격 m", d.followGapMetres, 0f, 20f);
            float lift = EditorGUILayout.Slider("추종 감속 kph", d.followLiftKph, 0f, 40f);
            float block = EditorGUILayout.Slider("방어 오프셋 m", d.blockOffsetMetres, 0f, 6f);

            EditorGUILayout.LabelField("부스트", EditorStyles.miniBoldLabel);
            float straightDegrees =
                EditorGUILayout.Slider("직선 판정 각도", d.boostStraightMaximumDegrees, 0f, 30f);
            float boostMinimum = EditorGUILayout.Slider("최소 발동 속도 kph", d.boostMinimumSpeedKph, 0f, 120f);
            bool telemetry = EditorGUILayout.ToggleLeft("주행 텔레메트리 로그 (1대)", d.logDriveTelemetry);

            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(preset, "Tune AI Driving");
            d.straightSpeedKph = straight;
            d.cornerGrip = grip;
            d.minCornerSpeedKph = minCorner;
            d.apexOffsetMetres = apex;
            d.rivalScanMetres = scan;
            d.overtakeOffsetMetres = overtake;
            d.overtakeMinClosingKph = closing;
            d.followGapMetres = follow;
            d.followLiftKph = lift;
            d.blockOffsetMetres = block;
            d.boostStraightMaximumDegrees = straightDegrees;
            d.boostMinimumSpeedKph = boostMinimum;
            d.logDriveTelemetry = telemetry;
            EditorUtility.SetDirty(preset);
        }

        private static void DrawPersonalityTable(GameBalanceSettings preset)
        {
            EditorGUILayout.LabelField("성격", EditorStyles.boldLabel);

            foreach (AIPersonalityType personality in Personalities)
            {
                AiPersonalityProfile profile = preset.ai.GetProfile(personality);
                if (profile == null)
                {
                    EditorGUILayout.HelpBox($"{personality}: 프리셋에 행이 없습니다.", MessageType.Warning);
                    continue;
                }

                EditorGUILayout.LabelField(KoreanName(personality), EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();
                float pace = EditorGUILayout.Slider("페이스", profile.paceScale, 0.5f, 1.5f);
                float braking = EditorGUILayout.Slider("제동 자신감", profile.brakingConfidence, 0.7f, 1.35f);
                float aggression = EditorGUILayout.Slider("추월 과감성", profile.overtakeAggression, 0f, 2f);
                float defence = EditorGUILayout.Slider("방어", profile.blockStrength, 0f, 1f);
                float tolerance =
                    EditorGUILayout.Slider("최소 차간 m", profile.contactToleranceMetres, 0f, 15f);
                float boost = EditorGUILayout.Slider("부스트 성향", profile.boostTendency, 0f, 1f);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(preset, "Tune AI Personality");
                    profile.paceScale = pace;
                    profile.brakingConfidence = braking;
                    profile.overtakeAggression = aggression;
                    profile.blockStrength = defence;
                    profile.contactToleranceMetres = tolerance;
                    profile.boostTendency = boost;
                    EditorUtility.SetDirty(preset);
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawLiveCars()
        {
            EditorGUILayout.LabelField("주행 중", EditorStyles.boldLabel);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 AI 상태가 표시됩니다.", MessageType.None);
                return;
            }

            drivers.Clear();
            drivers.AddRange(FindObjectsByType<GmtkRccpWaypointDriver>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None));

            if (drivers.Count == 0)
            {
                EditorGUILayout.HelpBox("AI 차량이 없습니다.", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("차량", GUILayout.Width(110f));
                EditorGUILayout.LabelField("성격", GUILayout.Width(70f));
                EditorGUILayout.LabelField("속도/목표", GUILayout.Width(90f));
                EditorGUILayout.LabelField("앞/뒤 간격", GUILayout.Width(90f));
                EditorGUILayout.LabelField("부스트", GUILayout.Width(50f));
            }

            foreach (GmtkRccpWaypointDriver driver in drivers)
            {
                if (driver == null) continue;

                Rigidbody body = driver.GetComponent<Rigidbody>();
                float speedKph = body != null ? body.linearVelocity.magnitude * 3.6f : 0f;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(driver.name, GUILayout.Width(110f));
                    EditorGUILayout.LabelField(KoreanName(driver.Personality), GUILayout.Width(70f));
                    EditorGUILayout.LabelField(
                        $"{speedKph:0}/{driver.TargetSpeedKph:0}", GUILayout.Width(90f));
                    EditorGUILayout.LabelField(
                        $"{Gap(driver.GapAheadMetres)}/{Gap(driver.GapBehindMetres)}", GUILayout.Width(90f));
                    EditorGUILayout.LabelField(driver.HasBoost ? "O" : "—", GUILayout.Width(50f));
                }
            }
        }

        private static string Gap(float metres) => metres <= 0f ? "—" : $"{metres:0}m";

        private static string KoreanName(AIPersonalityType personality) => personality switch
        {
            AIPersonalityType.Reckless => "폭주광",
            AIPersonalityType.Rammer => "난폭자",
            AIPersonalityType.Blocker => "봉쇄자",
            AIPersonalityType.CleanRacer => "생존자",
            _ => personality.ToString(),
        };
    }
}
