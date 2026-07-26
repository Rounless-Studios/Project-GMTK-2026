using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Gmtk2026.GameBalance;

namespace GMTK
{
    /// <summary>
    /// Project audio hub. Add this component to a scene-authored object and assign
    /// clips in the Inspector; playback code never creates or replaces clips.
    /// </summary>
    public sealed class GameAudioManager : MonoBehaviour
    {
        public enum CueType { Sfx, Ambience, Music, Voice }

        /// <summary>How far a cue id gets: unknown id, registered but unauthored, or playable.</summary>
        public enum CueStatus { Unknown, NoClip, Ready }

        [Serializable]
        public sealed class AudioCue
        {
            public string eventId;
            public CueType type;
            public AudioClip clip;
            public AudioClip[] variations;
            [Range(0f, 1f)] public float volume = 1f;
            public bool loop;
        }

        public static GameAudioManager Instance { get; private set; }

        [Header("Default Cue IDs")]
        [SerializeField] private string menuMusicId = "MUS_MAIN_THEME";
        [SerializeField] private string raceMusicId = "MUS_RACE_EARLY";
        [SerializeField] private string finalDuelMusicId = "MUS_TIMER_COUNT";
        [SerializeField] private string raceAmbienceId = "AMB_HELL";
        [SerializeField] private string buttonClickId = "SFX_UI_BUTTON_CLICK";
        [SerializeField] private string countdownTickId = "VO_ANNOUNCER_READY";
        [SerializeField] private string raceStartId = "VO_ANNOUNCER_GO";

        [Header("Boost Cue IDs")]
        [Tooltip("Clear an id to silence that boost moment.")]
        [SerializeField] private string boostStartId = "SFX_VEH_BOOST_START";
        [SerializeField] private string boostLoopId = "SFX_VEH_BOOST_LOOP";
        [SerializeField] private string boostEndId = "SFX_VEH_BOOST_END";
        [SerializeField] private string boostChargeId = "SFX_HUD_BOOST_CHARGE";
        [SerializeField] private string boostSealedId = "SFX_VEH_BOOST_SEALED";

        [Header("Playback")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource uiSfxSource;
        [SerializeField] private AudioSource loopSource;
        [SerializeField, Range(1f, 3f)] private float curseHitGain = 2f;

        [Header("Cue Library")]
        [Tooltip("Clips are auto-linked from Assets/Sound by event name. Empty entries are ignored safely.")]
        [SerializeField] private string soundFolder = "Assets/Sound";
        [SerializeField] private List<AudioCue> cues = new();

        // One-release migration bridge. Old scene and prefab clip assignments are
        // moved into their matching cues, then these hidden fields are cleared.
        [FormerlySerializedAs("menuMusic"), SerializeField, HideInInspector]
        private AudioClip legacyMenuMusic;
        [FormerlySerializedAs("raceMusic"), SerializeField, HideInInspector]
        private AudioClip legacyRaceMusic;
        [FormerlySerializedAs("buttonClick"), SerializeField, HideInInspector]
        private AudioClip legacyButtonClick;
        [FormerlySerializedAs("countdownTick"), SerializeField, HideInInspector]
        private AudioClip legacyCountdownTick;
        [FormerlySerializedAs("raceStart"), SerializeField, HideInInspector]
        private AudioClip legacyRaceStart;

        private static readonly string[] DefaultCueIds =
        {
            "SFX_UI_BUTTON_CLICK",
            "SFX_RACE_COUNTDOWN_TICK",
            "SFX_RACE_START",
            "SFX_VEH_ENGINE_IDLE",
            "SFX_VEH_ENGINE_RPM_LOW",
            "SFX_VEH_ENGINE_RPM_MID",
            "SFX_VEH_ENGINE_RPM_HIGH",
            "SFX_VEH_ENGINE_RPM_MAX",
            "SFX_VEH_ENGINE_DECEL",
            "SFX_VEH_ENGINE_BRAKE",
            "SFX_VEH_ENGINE_GEARSHIFT",
            "SFX_VEH_ENGINE_START",
            "SFX_VEH_ENGINE_STOP",
            "SFX_VEH_BOOST_READY",
            "SFX_VEH_BOOST_START",
            "SFX_VEH_BOOST_LOOP",
            "SFX_VEH_BOOST_END",
            "SFX_VEH_BOOST_CHARGE_DONE",
            "SFX_VEH_BOOST_CHARGING",
            "SFX_VEH_BOOST_UNAVAILABLE",
            "SFX_VEH_BOOST_SEALED",
            "SFX_VEH_TIRE_ROAD",
            "SFX_VEH_TIRE_SLIP",
            "SFX_VEH_TIRE_SHARP_TURN",
            "SFX_VEH_TIRE_SPIN",
            "SFX_VEH_TIRE_LAND",
            "SFX_VEH_JUMP",
            "SFX_VEH_COLLISION_1 ~ 9",
            "SFX_VEH_COLLISION_MID",
            "SFX_VEH_COLLISION_HEAVY",
            "SFX_VEH_COLLISION_SIDE_PVP",
            "SFX_VEH_COLLISION_WALL",
            "SFX_VEH_COLLISION_ENV",
            "SFX_VEH_COLLISION_DEBRIS",
            "SFX_VEH_DURA_100",
            "SFX_VEH_DURA_60",
            "SFX_VEH_DURA_30",
            "SFX_VEH_DURA_CRITICAL",
            "SFX_VEH_WRECK",
            "SFX_VEH_WRECK_SPIN",
            "SFX_VEH_BURNING",
            "SFX_VEH_REPAIR_START",
            "SFX_VEH_REPAIR_DONE",
            "SFX_VEH_INVINCIBLE_LOOP",
            "SFX_ELIM_TIMER_30",
            "SFX_ELIM_TIMER_20",
            "SFX_ELIM_TIMER_10",
            "SFX_ELIM_TIMER_05",
            "SFX_ELIM_TIMER_04",
            "SFX_ELIM_TIMER_03",
            "SFX_ELIM_TIMER_02",
            "SFX_ELIM_TIMER_01",
            "SFX_ELIM_TIMER_00",
            "SFX_ELIM_WARN_PLAYER_LAST",
            "SFX_ELIM_WARN_AI_LAST",
            "SFX_ELIM_WARN_START",
            "SFX_ELIM_WARN_ESCALATE",
            "SFX_ELIM_WARN_PEAK",
            "SFX_ELIM_WARN_VITAL_BEEP",
            "SFX_ELIM_WARN_ALARM",
            "SFX_ELIM_EXEC_MARK_SPAWN",
            "SFX_ELIM_EXEC_HELL_MARK",
            "SFX_ELIM_EXEC_LOCK",
            "SFX_ELIM_EXEC_CHARGE",
            "SFX_ELIM_EXEC_TWITCH",
            "SFX_ELIM_EXEC_EXPLOSION",
            "SFX_ELIM_EXEC_DEBRIS",
            "SFX_ELIM_EXEC_FIRE",
            "SFX_ELIM_EXEC_SHOCKWAVE",
            "SFX_ELIM_EXEC_CAM_IMPACT",
            "SFX_ELIM_EXEC_PLAYER",
            "SFX_ELIM_EXEC_PLAYER_EXPLOSION",
            "SFX_ELIM_DEATH",
            "SFX_ELIM_SCREEN_FADE",
            "SFX_ELIM_RESPAWN_READY",
            "SFX_ELIM_SPECTATE",
            "SFX_CURSE_COOLDOWN_DONE",
            "SFX_CURSE_READY",
            "SFX_CURSE_TARGET_SEARCH",
            "SFX_CURSE_TARGET_SET",
            "SFX_CURSE_TARGET_CHANGE",
            "SFX_CURSE_QUIZ_APPEAR",
            "SFX_CURSE_QUIZ_MOVE",
            "SFX_CURSE_QUIZ_SELECT",
            "SFX_CURSE_QUIZ_CORRECT",
            "SFX_CURSE_QUIZ_WRONG",
            "SFX_CURSE_QUIZ_TIMEOUT",
            "SFX_CURSE_QUIZ_END",
            "SFX_CURSE_CAST",
            "SFX_CURSE_DEMON_ENERGY",
            "SFX_CURSE_HIT_SUCCESS",
            "SFX_CURSE_EFFECT_APPLY",
            "SFX_CURSE_FAIL",
            "SFX_CURSE_CONSUME",
            "SFX_CURSE_COOLDOWN_START",
            "SFX_CURSE_BURST_LAUNCH",
            "SFX_CURSE_BURST_FLY",
            "SFX_CURSE_BURST_HIT",
            "SFX_CURSE_BURST_HURT",
            "SFX_CURSE_BURST_EXPLOSION",
            "SFX_CURSE_BURST_KNOCKBACK",
            "SFX_CURSE_SEAL_START",
            "SFX_CURSE_SEAL_CHAIN_SPAWN",
            "SFX_CURSE_SEAL_LOOP",
            "SFX_CURSE_SEAL_RELEASE",
            "SFX_CURSE_SWAP_PREP",
            "SFX_CURSE_SWAP_TELEPORT",
            "SFX_CURSE_SWAP_DISTORTION",
            "SFX_CURSE_SWAP_POSITION_CHANGE",
            "SFX_CURSE_SWAP_END",
            "SFX_CURSE_AI_CAST",
            "SFX_CURSE_AI_WARNING",
            "SFX_CURSE_AI_HIT",
            "SFX_CURSE_AI_FAIL",
            "SFX_CURSE_AI_SEAL",
            "SFX_CURSE_AI_SWAP",
            "SFX_CHAL_START",
            "SFX_CHAL_TARGET_SET",
            "SFX_CHAL_TARGET_HIGHLIGHT",
            "SFX_CHAL_TIME_TICK",
            "SFX_CHAL_SUCCESS",
            "SFX_CHAL_FAIL",
            "SFX_CHAL_REWARD",
            "SFX_CHAL_CHAIN_SPAWN",
            "SFX_CHAL_CHAIN_BIND",
            "SFX_CHAL_CHAIN_RESTRAIN_LOOP",
            "SFX_CHAL_CHAIN_BREAK",
            "SFX_CHAL_CHAIN_RELEASE",
            "SFX_RANK_UP",
            "SFX_RANK_DOWN",
            "SFX_RANK_FIRST",
            "SFX_RANK_LAST",
            "SFX_RANK_SWAP",
            "SFX_CP_PASS",
            "SFX_CP_LAP_COMPLETE",
            "SFX_CP_FINISH_ENTER",
            "SFX_GATE_ACTIVATE",
            "SFX_GATE_WARNING",
            "SFX_GATE_CLOSE_START",
            "SFX_GATE_MOVE_LOOP",
            "SFX_GATE_COLLISION",
            "SFX_GATE_PASS",
            "SFX_GATE_CLOSE_COMPLETE",
            "SFX_GATE_FAIL_EXECUTION",
            "SFX_UI_MENU_MOVE",
            "SFX_UI_SELECT",
            "SFX_UI_CANCEL",
            "SFX_UI_CONFIRM",
            "SFX_UI_BUTTON_HOVER",
            "SFX_UI_OPTION_CHANGE",
            "SFX_UI_RESTART",
            "SFX_UI_RESULT_SCREEN",
            "SFX_HUD_WARN_LAST",
            "SFX_HUD_CURSE_READY",
            "SFX_HUD_CURSE_HIT",
            "SFX_HUD_QUIZ_APPEAR",
            "SFX_HUD_BOOST_CHARGE",
            "SFX_HUD_BOOST_LACK",
            "SFX_HUD_DURA_DANGER",
            "SFX_HUD_CHAL_START",
            "SFX_HUD_CHAL_SUCCESS",
            "SFX_HUD_CHAL_FAIL",
            "SFX_HUD_GATE_START",
            "SFX_INPUT_BOOST_BTN",
            "SFX_INPUT_CURSE_BTN",
            "SFX_INPUT_QUIZ_INPUT",
            "SFX_INPUT_ERROR",
            "SFX_INPUT_RESTRICTED",
            "AMB_HELL",
            "AMB_FLAME",
            "AMB_FURNACE",
            "AMB_MOLTEN_METAL",
            "AMB_MACHINERY",
            "AMB_CHAIN_RATTLE",
            "AMB_DEMON_CRY",
            "AMB_DISTANT_SCREAM",
            "AMB_DEMON_BREATH",
            "AMB_VOLCANO",
            "AMB_SPARKS",
            "AMB_ASH_FALL",
            "SFX_OBJ_FIRE_BURST",
            "SFX_OBJ_FURNACE",
            "SFX_OBJ_MACHINE_ROTATE",
            "SFX_OBJ_GIANT_DOOR",
            "SFX_OBJ_CHAIN",
            "SFX_OBJ_METAL_SCRAPE",
            "SFX_OBJ_COMPRESSOR",
            "SFX_OBJ_STEAM",
            "SFX_CAM_CLOSE_OVERTAKE",
            "SFX_CAM_EXECUTION_CINE",
            "SFX_CAM_FINAL_DUEL",
            "SFX_CAM_GATE_CLIMAX",
            "SFX_CAM_EXPLOSION_SHAKE",
            "SFX_RESULT_WIN",
            "SFX_RESULT_LOSE",
            "SFX_RESULT_NEXT_HELL",
            "SFX_RESULT_GAME_END",
            "SFX_RESULT_SCREEN_OPEN",
            "MUS_MAIN_THEME",
            "MUS_RACE_START_STINGER",
            "MUS_RACE_EARLY",
            "MUS_RACE_MID",
            "MUS_RACE_TENSION",
            "MUS_ELIM_1ST",
            "MUS_ELIM_2ND",
            "MUS_ELIM_3RD",
            "MUS_RACE_LAYER_ADD",
            "MUS_TIMER_COUNT",
            "MUS_FINAL_TWO_TENSION",
            "MUS_FINAL_TWO_STRIPPED",
            "MUS_FINAL_TWO_HEARTBEAT",
            "MUS_GATE_TEMPO_UP",
            "MUS_GATE_CLIMAX",
            "MUS_VICTORY_THEME",
            "MUS_NEXT_HELL_TRANSITION",
            "MUS_DEFEAT_THEME",
            "MUS_DEATH_THEME",
            "VO_ANNOUNCER_READY",
            "VO_ANNOUNCER_GO",
            "VO_ANNOUNCER_30SEC",
            "VO_ANNOUNCER_LOWEST_DRIVER",
            "VO_ANNOUNCER_EXECUTION",
            "VO_ANNOUNCER_FINAL_DUEL",
            "VO_ANNOUNCER_GATE_CLOSING",
            "VO_ANNOUNCER_VICTORY",
            "VO_ANNOUNCER_DEFEAT",
            "VO_ANNOUNCER_CURSE_READY",
            "VO_ANNOUNCER_CHALLENGE_START",
            "VO_ANNOUNCER_CHALLENGE_SUCCESS",
            "VO_ANNOUNCER_CHALLENGE_FAILED",
        };

        private const string MusicVolumeKey = "GameAudio.MusicVolume";
        private const string SfxVolumeKey = "GameAudio.SfxVolume";

        private float currentMusicVolumeScale = 1f;
        private float currentLoopVolumeScale = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureCueList();
#if UNITY_EDITOR
            AutoLinkSoundFolder();
#endif
            MigrateLegacyClips(false);
            EnsureSources();
            musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume);
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);
            ApplyVolumes();
        }

        private void OnValidate()
        {
            EnsureCueList();
#if UNITY_EDITOR
            AutoLinkSoundFolder();
#endif
            MigrateLegacyClips(true);
        }

        private void EnsureCueList()
        {
            if (cues == null) cues = new List<AudioCue>();
            for (int i = 0; i < DefaultCueIds.Length; i++)
            {
                string id = DefaultCueIds[i].Trim();
                if (id.Length == 0 || cues.Exists(c => c != null && c.eventId == id)) continue;
                cues.Add(new AudioCue
                {
                    eventId = id,
                    type = GetCueType(id),
                    loop = id.Contains("_LOOP", StringComparison.Ordinal)
                });
            }
        }

        private static CueType GetCueType(string id)
        {
            if (id.StartsWith("MUS_", StringComparison.Ordinal)) return CueType.Music;
            if (id.StartsWith("VO_", StringComparison.Ordinal)) return CueType.Voice;
            if (id.StartsWith("AMB_", StringComparison.Ordinal)) return CueType.Ambience;
            return CueType.Sfx;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Link Clips From Assets/Sound")]
        private void AutoLinkSoundFolder()
        {
            if (string.IsNullOrWhiteSpace(soundFolder)) return;
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { soundFolder });
            var clipsByName = new Dictionary<string, List<AudioClip>>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                AudioClip audio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (audio == null) continue;
                string stem = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!clipsByName.TryGetValue(stem, out List<AudioClip> matches))
                    clipsByName[stem] = matches = new List<AudioClip>();
                matches.Add(audio);
            }

            foreach (AudioCue cue in cues)
            {
                if (cue == null || string.IsNullOrWhiteSpace(cue.eventId)) continue;
                string prefix = cue.eventId;
                int rangeIndex = prefix.IndexOf(" ~ ", StringComparison.Ordinal);
                string rangePrefix = null;
                if (rangeIndex >= 0)
                {
                    prefix = prefix.Substring(0, rangeIndex);
                    rangePrefix = prefix.TrimEnd(
                        ' ', '-', '_',
                        '0', '1', '2', '3', '4',
                        '5', '6', '7', '8', '9');
                }

                clipsByName.TryGetValue(cue.eventId, out List<AudioClip> exact);
                var variants = new List<AudioClip>();
                foreach (KeyValuePair<string, List<AudioClip>> pair in clipsByName)
                {
                    if (pair.Key.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase) ||
                        (rangePrefix != null &&
                         pair.Key.StartsWith(rangePrefix, StringComparison.OrdinalIgnoreCase)))
                        variants.AddRange(pair.Value);
                }

                if (exact != null && exact.Count > 0 || variants.Count > 0)
                {
                    cue.clip = exact != null && exact.Count > 0 ? exact[0] : variants[0];
                    cue.variations = variants.Count > 0 ? variants.ToArray() : null;
                }
            }
        }
#endif

        private void MigrateLegacyClips(bool clearMigratedFields)
        {
            MigrateLegacyClip(menuMusicId, legacyMenuMusic);
            MigrateLegacyClip(raceMusicId, legacyRaceMusic);
            MigrateLegacyClip(buttonClickId, legacyButtonClick);
            MigrateLegacyClip(countdownTickId, legacyCountdownTick);
            MigrateLegacyClip(raceStartId, legacyRaceStart);

            if (!clearMigratedFields) return;
            legacyMenuMusic = null;
            legacyRaceMusic = null;
            legacyButtonClick = null;
            legacyCountdownTick = null;
            legacyRaceStart = null;
        }

        private void MigrateLegacyClip(string eventId, AudioClip clip)
        {
            if (clip == null) return;
            AudioCue cue = FindCue(eventId);
            if (cue != null) cue.clip = clip;
        }

        private void OnEnable()
        {
            RaceFlow.PhaseChanged += OnPhaseChanged;
            EliminationManager.FinalDuelStarted += OnFinalDuelStarted;
            EliminationManager.WarningChanged += OnEliminationWarning;
            EliminationManager.EliminationTargetLocked += OnEliminationTargetLocked;
            BoostController.BoostStarted += OnBoostStarted;
            BoostController.BoostEnded += OnBoostEnded;
            BoostController.ChargeGained += OnBoostChargeGained;
            BoostController.SealChanged += OnBoostSealChanged;
            CurseController.CurseCast += OnCurseCast;
            CurseManager.CurseApplied += OnCurseApplied;
            OvertakeManager.ChallengeStarted += OnOvertakeStarted;
            OvertakeManager.ChallengeSucceeded += OnOvertakeSucceeded;
            OvertakeManager.ChallengeFailed += OnOvertakeFailed;
            FinalGate.GateSlamming += OnGateSlamming;
        }

        private void OnDisable()
        {
            RaceFlow.PhaseChanged -= OnPhaseChanged;
            EliminationManager.FinalDuelStarted -= OnFinalDuelStarted;
            EliminationManager.WarningChanged -= OnEliminationWarning;
            EliminationManager.EliminationTargetLocked -= OnEliminationTargetLocked;
            BoostController.BoostStarted -= OnBoostStarted;
            BoostController.BoostEnded -= OnBoostEnded;
            BoostController.ChargeGained -= OnBoostChargeGained;
            BoostController.SealChanged -= OnBoostSealChanged;
            CurseController.CurseCast -= OnCurseCast;
            CurseManager.CurseApplied -= OnCurseApplied;
            OvertakeManager.ChallengeStarted -= OnOvertakeStarted;
            OvertakeManager.ChallengeSucceeded -= OnOvertakeSucceeded;
            OvertakeManager.ChallengeFailed -= OnOvertakeFailed;
            FinalGate.GateSlamming -= OnGateSlamming;
        }

        private void Start()
        {
            if (RaceFlow.Instance == null)
                PlayMenuTheme();
            else
                OnPhaseChanged(RaceFlow.Instance.CurrentPhase);

            ReportBoostCues();
        }

        /// <summary>
        /// A mistyped or unauthored cue id costs nothing at compile time and everything at
        /// runtime, so the boost ids are checked once instead of failing silently.
        /// </summary>
        private void ReportBoostCues()
        {
            ReportCue(boostStartId);
            ReportCue(boostLoopId);
            ReportCue(boostEndId);
            ReportCue(boostChargeId);
            ReportCue(boostSealedId);
        }

        private void ReportCue(string cueId)
        {
            if (string.IsNullOrEmpty(cueId)) return;   // an empty id is a deliberate mute

            switch (GetCueStatus(cueId))
            {
                case CueStatus.Unknown:
                    Debug.LogError($"GameAudioManager: no cue named '{cueId}' in the sound "
                                   + "list, that moment will be silent.", this);
                    break;
                case CueStatus.NoClip:
                    Debug.LogWarning($"GameAudioManager: cue '{cueId}' has no clip assigned "
                                     + "yet, that moment will be silent.", this);
                    break;
            }
        }

        private void EnsureSources()
        {
            if (musicSource == null)
            {
                var child = new GameObject("MusicSource");
                child.transform.SetParent(transform, false);
                musicSource = child.AddComponent<AudioSource>();
            }

            if (sfxSource == null)
            {
                var child = new GameObject("SfxSource");
                child.transform.SetParent(transform, false);
                sfxSource = child.AddComponent<AudioSource>();
            }

            if (uiSfxSource == null)
            {
                var child = new GameObject("UiSfxSource");
                child.transform.SetParent(transform, false);
                uiSfxSource = child.AddComponent<AudioSource>();
            }

            if (loopSource == null)
            {
                var child = new GameObject("LoopSource");
                child.transform.SetParent(transform, false);
                loopSource = child.AddComponent<AudioSource>();
            }

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            uiSfxSource.playOnAwake = false;
            uiSfxSource.loop = false;
            uiSfxSource.spatialBlend = 0f;
            uiSfxSource.priority = 0;
            loopSource.playOnAwake = false;
            loopSource.loop = true;
        }

        private void ApplyVolumes()
        {
            if (musicSource != null)
                musicSource.volume = musicVolume * currentMusicVolumeScale;
            float master = GameBalance.Current.presentation.masterSfxVolume;
            if (sfxSource != null) sfxSource.volume = sfxVolume * master;
            if (uiSfxSource != null) uiSfxSource.volume = sfxVolume * master;
            if (loopSource != null)
                loopSource.volume = sfxVolume * master * currentLoopVolumeScale;
        }

        private void OnPhaseChanged(RaceFlow.Phase phase)
        {
            if (phase == RaceFlow.Phase.StartScreen || phase == RaceFlow.Phase.Prologue || phase == RaceFlow.Phase.Countdown)
            {
                PlayMenuTheme();
            }
            else if (phase == RaceFlow.Phase.Racing)
            {
                PlayCue(raceMusicId, 1f, true);
                PlayCue(raceAmbienceId, 1f, true);
            }
        }

        private void OnFinalDuelStarted()
        {
            PlayCue(finalDuelMusicId, 1f, true);
        }

        private void OnEliminationWarning(int target, EliminationWarningLevel level)
        {
            string cue = level switch
            {
                EliminationWarningLevel.Warning => "SFX_ELIM_WARN_START",
                EliminationWarningLevel.Intense => "SFX_ELIM_WARN_ESCALATE",
                EliminationWarningLevel.Execution => "SFX_ELIM_WARN_PEAK",
                _ => null,
            };
            if (!string.IsNullOrEmpty(cue)) PlayCue(cue);
            if (target == 0 && level == EliminationWarningLevel.Warning)
                PlayCue("SFX_ELIM_WARN_PLAYER_LAST");
        }

        private void OnEliminationTargetLocked(int target)
        {
            if (target == 0)
                PlayCue("VO_ANNOUNCER_CHALLENGE_FAILED");
        }

        // Only the player is heard: five AI cars firing the same cue would bury the mix.
        private void OnBoostStarted(BoostController boost)
        {
            if (boost == null || !boost.IsPlayer) return;
            PlayBoostCue(boostStartId);
            PlayBoostCue(boostLoopId);
        }

        private void OnBoostEnded(BoostController boost)
        {
            if (boost == null || !boost.IsPlayer) return;

            // the loop cue holds the single loop source for the boost's duration, so it has
            // to be released before the tail-off one-shot
            StopLoop();
            PlayBoostCue(boostEndId);
        }

        private void OnBoostChargeGained(BoostController boost)
        {
            if (boost != null && boost.IsPlayer) PlayBoostCue(boostChargeId);
        }

        private void OnBoostSealChanged(BoostController boost, bool isSealed)
        {
            if (isSealed && boost != null && boost.IsPlayer) PlayBoostCue(boostSealedId);
        }

        private void PlayBoostCue(string cueId)
        {
            if (!string.IsNullOrEmpty(cueId)) PlayCue(cueId);
        }

        private void OnCurseCast(CurseController caster, CurseType type, int target)
        {
            PlayCue(target == 0 ? "SFX_CURSE_INCOMING" : "SFX_CURSE_CAST");
        }

        private void OnCurseApplied(CurseController caster, CurseType type, int target)
        {
            if (target == 0)
                PlayUiCue("SFX_HUD_CURSE_HIT", curseHitGain);
        }

        private void OnOvertakeStarted(int _) => PlayCue("SFX_OVERTAKE_START");
        private void OnOvertakeSucceeded(int _) => PlayCue("SFX_OVERTAKE_SUCCESS");
        private void OnOvertakeFailed(int _) => PlayCue("SFX_OVERTAKE_FAIL");
        private void OnGateSlamming(int winner)
        {
            PlayCue("SFX_GATE_CLOSE");
            if (winner != 0)
                PlayCue("VO_ANNOUNCER_CHALLENGE_FAILED");
        }

        private void PlayContinuous(
            AudioSource source,
            AudioClip clip,
            float busVolume,
            float volumeScale,
            bool loop)
        {
            if (source == null || clip == null) return;
            if (source == musicSource)
                currentMusicVolumeScale = volumeScale;
            else if (source == loopSource)
                currentLoopVolumeScale = volumeScale;

            source.volume = Mathf.Clamp01(busVolume * volumeScale);
            source.loop = loop;
            if (source.clip == clip && source.isPlaying) return;
            source.clip = clip;
            source.Play();
        }

        public void StopMusic() => musicSource?.Stop();

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (sfxSource == null || clip == null) return;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Plays an event from the CSV sound list. Missing clips do nothing.</summary>
        public void PlayCue(string eventId, float volume = 1f)
        {
            PlayCue(eventId, volume, null);
        }

        private void PlayCue(string eventId, float volume, bool? loopOverride)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return;
            AudioCue cue = FindCue(eventId);
            if (cue == null) return;
            AudioClip clip = ResolveCueClip(cue, eventId);
            if (clip == null) return;
            float scaledVolume = Mathf.Clamp01(volume * cue.volume);
            bool loop = loopOverride ?? cue.loop;
            if (cue.type == CueType.Music)
            {
                PlayContinuous(musicSource, clip, musicVolume, scaledVolume, loop);
                return;
            }

            if (loop)
            {
                PlayContinuous(loopSource, clip, sfxVolume, scaledVolume, true);
                return;
            }

            PlaySfx(clip, scaledVolume);
        }

        private void PlayUiCue(string eventId, float gain)
        {
            if (uiSfxSource == null || string.IsNullOrWhiteSpace(eventId)) return;
            AudioCue cue = FindCue(eventId);
            if (cue == null) return;
            AudioClip clip = ResolveCueClip(cue, eventId);
            if (clip == null) return;

            // UI feedback must remain legible over the continuously playing vehicle engine.
            // PlayOneShot accepts a gain above one; keep the cap modest to avoid hard clipping.
            uiSfxSource.PlayOneShot(
                clip,
                Mathf.Clamp(gain * cue.volume, 0f, 2f));
        }

        private static AudioClip ResolveCueClip(AudioCue cue, string eventId)
        {
            AudioClip clip = cue.clip;
            if (cue.variations != null && cue.variations.Length > 0)
            {
                int baseClipCount = cue.clip != null ? 1 : 0;
                int choice = UnityEngine.Random.Range(
                    0,
                    baseClipCount + cue.variations.Length);
                clip = baseClipCount == 1 && choice == 0
                    ? cue.clip
                    : cue.variations[choice - baseClipCount];
            }

            return clip != null ? clip : Resources.Load<AudioClip>(eventId);
        }

        private AudioCue FindCue(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || cues == null) return null;
            return cues.Find(
                item => item != null &&
                        string.Equals(item.eventId, eventId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolves a cue id the same way <see cref="PlayCue(string, float)"/> does, without
        /// playing it. A mistyped id otherwise just goes silent, so callers can check their ids
        /// once at start-up and say so out loud.
        /// </summary>
        public CueStatus GetCueStatus(string eventId)
        {
            AudioCue cue = FindCue(eventId);
            if (cue == null) return CueStatus.Unknown;

            if (cue.clip != null) return CueStatus.Ready;
            if (cue.variations != null && Array.Exists(cue.variations, item => item != null))
                return CueStatus.Ready;

            // PlayCue falls back to a same-named clip under a Resources folder
            return Resources.Load<AudioClip>(eventId) != null ? CueStatus.Ready : CueStatus.NoClip;
        }

        public void StopLoop()
        {
            loopSource?.Stop();
        }

        public void PlayMenuTheme()
        {
            StopLoop();
            PlayCue(menuMusicId, 1f, true);
        }

        public void PlayCountdownTick() => PlayCue(countdownTickId);
        public void PlayRaceStart() => PlayCue(raceStartId);
        public void PlayButtonClick() => PlayCue(buttonClickId, 0.7f);

        public void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
        }

        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;
    }
}
