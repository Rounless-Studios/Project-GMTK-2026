using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
/// display a 3,2,1,go countdown and change the race state to begin (race timer, unfreeze cars...)
/// </summary>
namespace SpinMotion
{
    public class PreRaceCountdownGUI : MonoBehaviour
    {
        public GameEvents gameEvents;
        public TMP_Text countdownTMP;
        private Coroutine raceStartCountdown;

        private void Awake()
        {
            if (IsGmtkRace())
            {
                if (countdownTMP != null) countdownTMP.gameObject.SetActive(false);
                return;
            }
            gameEvents.PlayPreRaceCountdownEvent.AddListener(OnPlayRace);
        }

        private void OnPlayRace()
        {
            // GMTK_Race uses the editor-authored GMTK countdown through RaceFlow.
            // Ignore the kit countdown event there so two countdowns cannot appear.
            if (IsGmtkRace() || GMTK.RaceFlow.OwnsGameEventsStart) return;
            if (raceStartCountdown != null) { StopCoroutine(raceStartCountdown); }
            raceStartCountdown = StartCoroutine(RaceStartCountdown());
        }

        private static bool IsGmtkRace() => SceneManager.GetActiveScene().name == "GMTK_Race";

        // it can also be done with an Animator and changing the text on the animation clip
        private IEnumerator RaceStartCountdown()
        {
            gameEvents.ToggleCarFreezeEvent.Invoke(true);
            yield return new WaitForSeconds(0.5f);
            countdownTMP.gameObject.SetActive(true);
            countdownTMP.text = "3";
            gameEvents.PlayAudioSfxEvent.Invoke(SFXType.GetReadySFX);

            yield return new WaitForSeconds(1);
            countdownTMP.text = "2";
            gameEvents.PlayAudioSfxEvent.Invoke(SFXType.GetReadySFX);

            yield return new WaitForSeconds(1);
            countdownTMP.text = "1";
            gameEvents.PlayAudioSfxEvent.Invoke(SFXType.GetReadySFX);
            
            yield return new WaitForSeconds(1);
            countdownTMP.text = "GO!";
            gameEvents.PlayAudioSfxEvent.Invoke(SFXType.GoSFX);
            gameEvents.ToggleCarFreezeEvent.Invoke(false);
            gameEvents.RaceStartedEvent.Invoke();

            yield return new WaitForSeconds(1);
            countdownTMP.gameObject.SetActive(false);
        }
    }
}
