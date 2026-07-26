using System;
using System.Collections.Generic;

namespace Gmtk2026.Quiz
{
    public interface IQuizQuestionSource
    {
        IEnumerable<QuizQuestion> CreateQuestions(int count, Random random);
    }
}
