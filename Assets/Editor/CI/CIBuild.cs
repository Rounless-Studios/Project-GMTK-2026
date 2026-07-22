using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RounlessStudios.CI
{
    public static class CIBuild
    {
        private const string LfsSignature = "version https://git-lfs.github.com/spec/v1";

        public static void ValidateProject()
        {
            var expected = ReadProjectVersion();
            Require(Application.unityVersion == expected,
                $"Running Unity {Application.unityVersion}, but ProjectVersion.txt requires {expected}.");

            var scenes = EnabledScenes();
            Require(scenes.Length > 0, "Build Settings contains no enabled scenes; refusing to build an empty player.");
            foreach (var scene in scenes)
            {
                Require(File.Exists(scene), $"Enabled scene does not exist: {scene}");
                Require(File.Exists(scene + ".meta"), $"Enabled scene has no .meta file: {scene}");
                Require(!IsLfsPointer(scene), $"Enabled scene is an unresolved Git LFS pointer: {scene}");
            }

            Debug.Log($"CI preflight passed: Unity {expected}; {scenes.Length} enabled scene(s). " +
                      $"Product '{PlayerSettings.productName}', company '{PlayerSettings.companyName}'.");
        }

        public static void Build()
        {
            ValidateProject();
            var targetName = RequireEnvironment("CI_BUILD_TARGET");
            var output = Path.GetFullPath(RequireEnvironment("CI_BUILD_OUTPUT"));
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            Require(!output.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase), "Build output must be outside Assets/.");
            Require(output.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                "Build output must remain inside the checked-out repository workspace.");

            Require(Enum.TryParse(targetName, out BuildTarget target), $"Unknown CI_BUILD_TARGET: {targetName}");
            Require(target == BuildTarget.StandaloneWindows64 || target == BuildTarget.WebGL,
                $"Unsupported CI build target: {target}");
            var targetGroup = target == BuildTarget.WebGL ? BuildTargetGroup.WebGL : BuildTargetGroup.Standalone;
            Require(BuildPipeline.IsBuildTargetSupported(targetGroup, target),
                $"Required {target} support is not installed in this exact Unity Editor image.");
            var version = RequireEnvironment("CI_RELEASE_VERSION");
            PlayerSettings.bundleVersion = version;

            if (Directory.Exists(output)) Directory.Delete(output, true);
            Directory.CreateDirectory(output);
            var location = target == BuildTarget.StandaloneWindows64
                ? Path.Combine(output, SanitizeFileName(PlayerSettings.productName) + ".exe")
                : output;
            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                target = target,
                locationPathName = location,
                options = BuildOptions.StrictMode
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"{target} build failed: {report.summary.result}; {report.summary.totalErrors} error(s).");
            RejectNonShippingDirectories(output);
            Debug.Log($"Built {target} version {version} to {output}; {report.summary.totalSize} bytes; " +
                      $"{report.summary.totalWarnings} warning(s).");
        }

        private static string[] EnabledScenes() => EditorBuildSettings.scenes
            .Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

        private static string ReadProjectVersion()
        {
            var path = Path.Combine(Application.dataPath, "../ProjectSettings/ProjectVersion.txt");
            var line = File.ReadLines(path).FirstOrDefault(value => value.StartsWith("m_EditorVersion: "));
            Require(line != null, "ProjectVersion.txt does not contain m_EditorVersion.");
            return line.Substring("m_EditorVersion: ".Length).Trim();
        }

        private static bool IsLfsPointer(string path)
        {
            using var reader = new StreamReader(path);
            var firstLine = reader.ReadLine();
            return string.Equals(firstLine, LfsSignature, StringComparison.Ordinal);
        }

        private static void RejectNonShippingDirectories(string output)
        {
            var forbidden = Directory.EnumerateDirectories(output, "*", SearchOption.AllDirectories)
                .FirstOrDefault(path => path.EndsWith("_BackUpThisFolder_ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase)
                                     || path.EndsWith("_BurstDebugInformation_DoNotShip", StringComparison.OrdinalIgnoreCase));
            Require(forbidden == null, $"Non-shipping Unity debug/backup directory found: {forbidden}");
        }

        private static string RequireEnvironment(string name) =>
            Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
                ? value
                : throw new BuildFailedException($"Required environment variable is missing: {name}");

        private static string SanitizeFileName(string value) =>
            string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new BuildFailedException(message);
        }
    }
}
