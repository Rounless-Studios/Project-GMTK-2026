using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GMTK.EditorTools
{
    /// <summary>
    /// A WebGL build for "does it run" checks, not for submission.
    /// <para>
    /// The shipping build compresses with Brotli, which is the slowest step of a WebGL build by a wide
    /// margin. This one turns compression off, builds to its own folder so it can never be mistaken for
    /// a release artifact, and puts the original settings back afterwards — including when the build
    /// fails. The release path stays <see cref="RounlessStudios.CI.CIBuild"/>, untouched.
    /// </para>
    /// </summary>
    public static class GmtkFastWebGlBuild
    {
        private const string OutputDirectory = "build/webgl-fast";

        [MenuItem("GMTK/Build/WebGL (fast iteration)")]
        public static void Build()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[FastWebGL] Build Settings has no enabled scene.");
                return;
            }

            WebGLCompressionFormat compression = PlayerSettings.WebGL.compressionFormat;
            bool fallback = PlayerSettings.WebGL.decompressionFallback;
            bool dataCaching = PlayerSettings.WebGL.dataCaching;

            // Uncompressed output loads from any host without special headers, which also removes the
            // itch.io Content-Encoding question from an iteration build.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = false;

            var timer = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                Directory.CreateDirectory(OutputDirectory);

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = BuildTarget.WebGL,
                    locationPathName = OutputDirectory,
                });

                timer.Stop();

                if (report.summary.result == BuildResult.Succeeded)
                    Debug.Log($"[FastWebGL] {OutputDirectory} in {timer.Elapsed.TotalMinutes:F1} min, " +
                              $"{report.summary.totalSize / (1024 * 1024)} MB, " +
                              $"{report.summary.totalWarnings} warning(s). Compression was off: this is " +
                              "not a submission build.");
                else
                    Debug.LogError($"[FastWebGL] {report.summary.result} after " +
                                   $"{timer.Elapsed.TotalMinutes:F1} min, " +
                                   $"{report.summary.totalErrors} error(s).");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FastWebGL] build threw after {timer.Elapsed.TotalMinutes:F1} min: {exception}");
                throw;
            }
            finally
            {
                // the shipping settings go back even if the build failed, so a release build later
                // cannot silently ship uncompressed
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.WebGL.dataCaching = dataCaching;
                AssetDatabase.SaveAssets();

                Debug.Log($"[FastWebGL] restored compression={compression} " +
                          $"decompressionFallback={fallback} dataCaching={dataCaching}");
            }
        }
    }
}
