using UnityEngine;

namespace Gmtk2026.Quiz
{
    public sealed class ButtonMashProgress
    {
        public int TargetTaps { get; private set; }
        public int CurrentTaps { get; private set; }
        public bool IsComplete => CurrentTaps >= TargetTaps;
        public float Normalized => TargetTaps > 0
            ? Mathf.Clamp01((float)CurrentTaps / TargetTaps)
            : 1f;

        public void Reset(int targetTaps)
        {
            TargetTaps = Mathf.Max(1, targetTaps);
            CurrentTaps = 0;
        }

        public bool Tap()
        {
            if (IsComplete)
            {
                return false;
            }

            CurrentTaps++;
            return true;
        }
    }
}
