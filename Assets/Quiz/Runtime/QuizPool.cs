using System.Collections.Generic;
using UnityEngine;

namespace Gmtk2026.Quiz
{
    [CreateAssetMenu(fileName = "QuizPool", menuName = "GMTK/Quiz/Quiz Pool")]
    public sealed class QuizPool : ScriptableObject
    {
        [SerializeField] private List<QuizQuestion> questions = new List<QuizQuestion>();

        public IReadOnlyList<QuizQuestion> Questions => questions;

        public IEnumerable<QuizQuestion> GetValidQuestions()
        {
            foreach (QuizQuestion question in questions)
            {
                if (question != null && question.IsValid(out _))
                {
                    yield return question;
                }
            }
        }
    }
}
