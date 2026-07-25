using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GMTK.Editor
{
    public static class PrologueRecorderSetup
    {
        private const string SettingsDirectory = "Assets/Editor/GMTK/Recorder";
        private const string SettingsAssetPath =
            SettingsDirectory + "/PrologueRecorderSettings.asset";
        private const string ExampleSceneName = "example_scene";

        [MenuItem("GMTK/Prologue/Prepare Recorder")]
        public static void PrepareRecorder()
        {
            if (SceneManager.GetActiveScene().name != ExampleSceneName)
            {
                EditorUtility.DisplayDialog(
                    "Prologue Recorder",
                    "Open Assets/Single_detailed_truck/example_scene.unity first, then run this menu again.",
                    "OK");
                return;
            }

            EnsureDirectory(SettingsDirectory);
            RecorderControllerSettings controller = LoadOrCreateController();
            MovieRecorderSettings movie = LoadOrCreateMovie(controller);

            controller.FrameRate = 30f;
            controller.FrameRatePlayback = FrameRatePlayback.Constant;
            controller.CapFrameRate = true;
            controller.ExitPlayMode = true;
            controller.SetRecordModeToTimeInterval(0f, 5f);

            movie.name = "GMTK Prologue MP4";
            movie.Enabled = true;
            movie.OutputFile = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "Prologue");
            movie.CaptureAlpha = false;
            movie.CaptureAudio = true;
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = 1920,
                OutputHeight = 1080
            };
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                EncodingProfile = CoreEncoderSettings.H264EncodingProfile.High
            };

            EditorUtility.SetDirty(movie);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            RecorderWindow window = EditorWindow.GetWindow<RecorderWindow>(
                false,
                "Recorder",
                true);
            window.SetRecorderControllerSettings(controller);
            window.Show();
            window.Focus();

            Debug.Log(
                "[Prologue Recorder] Ready: 1920x1080, 30 FPS, H.264 MP4, 5 seconds. " +
                "Press START RECORDING in the Recorder window.");
        }

        private static RecorderControllerSettings LoadOrCreateController()
        {
            RecorderControllerSettings controller =
                AssetDatabase.LoadAssetAtPath<RecorderControllerSettings>(SettingsAssetPath);
            if (controller != null) return controller;

            controller = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controller.name = "Prologue Recorder Settings";
            AssetDatabase.CreateAsset(controller, SettingsAssetPath);
            return controller;
        }

        private static MovieRecorderSettings LoadOrCreateMovie(
            RecorderControllerSettings controller)
        {
            MovieRecorderSettings movie =
                controller.RecorderSettings.OfType<MovieRecorderSettings>().FirstOrDefault();
            if (movie != null) return movie;

            movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            controller.AddRecorderSettings(movie);
            AssetDatabase.AddObjectToAsset(movie, controller);
            return movie;
        }

        private static void EnsureDirectory(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
