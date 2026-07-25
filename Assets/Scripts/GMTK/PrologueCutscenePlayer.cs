using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GMTK
{
    /// <summary>
    /// Plays the recorded prologue above every race UI layer. If the movie has not
    /// been recorded yet (or cannot be decoded), RaceFlow waits without restoring
    /// the retired text prologue.
    /// </summary>
    public static class PrologueCutscenePlayer
    {
        private const string MovieFileName = "Prologue.mp4";
        private const float PrepareTimeoutSeconds = 8f;
        private const float MaximumPlaybackSeconds = 90f;

        public static IEnumerator Play()
        {
            string moviePath = Path.Combine(Application.streamingAssetsPath, MovieFileName);

#if !UNITY_WEBGL || UNITY_EDITOR
            if (!File.Exists(moviePath))
            {
                Debug.LogWarning(
                    $"[Prologue] Movie not found at '{moviePath}'. Skipping movie playback.");
                yield break;
            }
#endif

            GameObject overlay = null;
            RenderTexture renderTexture = null;
            VideoPlayer videoPlayer = null;
            bool prepareFailed = false;
            string prepareError = null;
            bool listenerWasPaused = AudioListener.pause;
            bool listenerPauseApplied = false;

            try
            {
                // Silence engines, music and every other scene AudioSource while
                // keeping the movie's own audio audible.
                AudioListener.pause = true;
                listenerPauseApplied = true;

                overlay = BuildOverlay(out RawImage movieImage);
                renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
                {
                    name = "PrologueMovieTexture"
                };
                renderTexture.Create();
                movieImage.texture = renderTexture;

                videoPlayer = overlay.AddComponent<VideoPlayer>();
                var audioSource = overlay.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.ignoreListenerPause = true;

                videoPlayer.playOnAwake = false;
                videoPlayer.isLooping = false;
                videoPlayer.skipOnDrop = true;
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                videoPlayer.targetTexture = renderTexture;
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                videoPlayer.SetTargetAudioSource(0, audioSource);
                videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                videoPlayer.url = new Uri(moviePath).AbsoluteUri;
                videoPlayer.errorReceived += (_, message) =>
                {
                    prepareFailed = true;
                    prepareError = message;
                };

                videoPlayer.Prepare();
                float prepareDeadline = Time.realtimeSinceStartup + PrepareTimeoutSeconds;
                while (!videoPlayer.isPrepared &&
                       !prepareFailed &&
                       Time.realtimeSinceStartup < prepareDeadline)
                {
                    yield return null;
                }

                if (!videoPlayer.isPrepared || prepareFailed)
                {
                    string reason = prepareError ?? "preparation timed out";
                    Debug.LogWarning($"[Prologue] Movie playback failed ({reason}). Skipping movie playback.");
                    DestroyOverlay(overlay, renderTexture);
                    overlay = null;
                    renderTexture = null;
                    yield break;
                }

                bool finished = false;
                videoPlayer.loopPointReached += _ => finished = true;
                videoPlayer.Play();

                float expectedLength = videoPlayer.length > 0d
                    ? Mathf.Min((float)videoPlayer.length + 2f, MaximumPlaybackSeconds)
                    : MaximumPlaybackSeconds;
                float playbackDeadline = Time.realtimeSinceStartup + expectedLength;

                while (!finished && Time.realtimeSinceStartup < playbackDeadline)
                {
                    if (WasSkipPressed())
                    {
                        Debug.Log("[Prologue] Movie skipped.");
                        break;
                    }

                    yield return null;
                }

                videoPlayer.Stop();
            }
            finally
            {
                DestroyOverlay(overlay, renderTexture);
                if (listenerPauseApplied)
                    AudioListener.pause = listenerWasPaused;
            }
        }

        private static GameObject BuildOverlay(out RawImage movieImage)
        {
            var root = new GameObject(
                "PrologueMovieOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(root);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var black = new GameObject("BlackBackground", typeof(RectTransform), typeof(Image));
            black.transform.SetParent(root.transform, false);
            Stretch(black.GetComponent<RectTransform>());
            black.GetComponent<Image>().color = Color.black;

            var movie = new GameObject("Movie", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            movie.transform.SetParent(root.transform, false);
            Stretch(movie.GetComponent<RectTransform>());
            movieImage = movie.GetComponent<RawImage>();
            movieImage.color = Color.white;
            movieImage.raycastTarget = false;
            var aspect = movie.GetComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 16f / 9f;

            return root;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool WasSkipPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.escapeKey.wasPressedThisFrame ||
                    keyboard.spaceKey.wasPressedThisFrame ||
                    keyboard.enterKey.wasPressedThisFrame);
        }

        private static void DestroyOverlay(GameObject overlay, RenderTexture renderTexture)
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }

            if (overlay != null)
                UnityEngine.Object.Destroy(overlay);
        }
    }
}
