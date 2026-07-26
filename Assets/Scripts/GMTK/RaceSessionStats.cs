using System.Text;
using Gmtk2026.Quiz;
using SpinMotion;
using UnityEngine;

namespace GMTK
{
    /// <summary>Lightweight per-race statistics and stable rival identities for the result screen.</summary>
    [DisallowMultipleComponent]
    public sealed class RaceSessionStats : MonoBehaviour
    {
        private static readonly string[] RivalNames =
        {
            "YOU", "MALICE", "BRIMSTONE", "VEX", "CINDER", "MOURNER",
            "HERETIC", "WRAITH"
        };

        public static RaceSessionStats Instance { get; private set; }

        public float MaximumSpeedKph { get; private set; }
        public float TimeInFirst { get; private set; }
        public float TimeInLast { get; private set; }
        public int QuizCorrect { get; private set; }
        public int QuizTotal { get; private set; }
        public int Overtakes { get; private set; }
        public int EliminationsWitnessed { get; private set; }

        private int previousRank;
        private QuizSessionController quiz;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            GameObject host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<RaceSessionStats>() == null)
                host.AddComponent<RaceSessionStats>();
        }

        public static string NameOf(int raceIndex) =>
            raceIndex >= 0 && raceIndex < RivalNames.Length
                ? RivalNames[raceIndex]
                : $"RACER {raceIndex + 1:00}";

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            GameEvents events = Race.Events;
            if (events != null)
            {
                events.RaceStartedEvent.AddListener(ResetStats);
                events.RestartRaceEvent.AddListener(ResetStats);
            }

            EliminationManager.CarEliminated += OnCarEliminated;
            quiz = FindFirstObjectByType<QuizSessionController>(FindObjectsInactive.Include);
            if (quiz != null) quiz.AnswerEvaluated += OnQuizAnswered;
        }

        private void OnDestroy()
        {
            GameEvents events = Race.Events;
            if (events != null)
            {
                events.RaceStartedEvent.RemoveListener(ResetStats);
                events.RestartRaceEvent.RemoveListener(ResetStats);
            }

            EliminationManager.CarEliminated -= OnCarEliminated;
            if (quiz != null) quiz.AnswerEvaluated -= OnQuizAnswered;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!Race.IsRaceInProgress || Race.CarCount <= 0) return;

            GameObject player = Race.CarByIndex(0);
            Rigidbody body = player != null ? player.GetComponent<Rigidbody>() : null;
            if (body != null)
                MaximumSpeedKph = Mathf.Max(MaximumSpeedKph, body.linearVelocity.magnitude * 3.6f);

            int rank = RankOfPlayer();
            if (rank == 1) TimeInFirst += Time.deltaTime;
            if (rank == Race.CarCount) TimeInLast += Time.deltaTime;
            if (previousRank > 0 && rank < previousRank)
                Overtakes += previousRank - rank;
            previousRank = rank;
        }

        public string BuildResultSummary()
        {
            var text = new StringBuilder();
            text.Append("BEST SPEED  ").Append(MaximumSpeedKph.ToString("0")).AppendLine(" KM/H")
                .Append("OVERTAKES   ").Append(Overtakes).AppendLine()
                .Append("QUIZZES     ").Append(QuizCorrect).Append('/').Append(QuizTotal).AppendLine()
                .Append("TIME FIRST  ").Append(TimeInFirst.ToString("0.0")).AppendLine("s")
                .Append("TIME LAST   ").Append(TimeInLast.ToString("0.0")).AppendLine("s")
                .Append("EXECUTIONS  ").Append(EliminationsWitnessed);
            return text.ToString();
        }

        private void ResetStats()
        {
            MaximumSpeedKph = 0f;
            TimeInFirst = 0f;
            TimeInLast = 0f;
            QuizCorrect = 0;
            QuizTotal = 0;
            Overtakes = 0;
            EliminationsWitnessed = 0;
            previousRank = 0;
        }

        private void OnQuizAnswered(QuizAnswerResult result)
        {
            QuizTotal++;
            if (result.IsCorrect) QuizCorrect++;
        }

        private void OnCarEliminated(int _) => EliminationsWitnessed++;

        private static int RankOfPlayer()
        {
            int rank = 1;
            double playerScore = Race.ScoreOf(0);
            for (int i = 1; i < Race.CarCount; i++)
                if (Race.ScoreOf(i) > playerScore) rank++;
            return rank;
        }
    }
}
