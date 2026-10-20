using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>Build one portable prototype without changing the product scene list.</summary>
public static class PrototypePlaytestBuild
{
    private const string StatusKey = "TechnicalSwordAction.PrototypePlaytestBuild";
    private const string PendingKey = StatusKey + ".Pending";
    private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    public static string Status => SessionState.GetString(StatusKey, "Idle");

    [Serializable] public sealed class Result
    {
        public bool completed, success;
        public string buildId, sourceCommit, unityVersion, scriptingBackend, outputDirectory, reportPath, error;
        public bool workingTreeDirty;
        public int errors, warnings;
        public long bytes;
        public double seconds;
        public string[] messages;
    }
    [Serializable] private sealed class PackageInfo
    {
        public string buildId, sourceCommit, unityVersion, scriptingBackend;
        public bool workingTreeDirty;
        public string configuration = "Development", platform = "Windows x86_64", scene = "PrototypeCombatValidation";
    }

    [MenuItem("Tools/Technical Sword Action/Prototype/Build Windows Playtest")]
    public static void Queue()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Stop PlayMode and wait for compilation before building.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Review unsaved scenes before building; no scene is saved or discarded automatically.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("Windows 64-bit build support is not installed.");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException("Select Windows 64-bit as the active build target first.");
        if (Status.StartsWith("Queued:", StringComparison.Ordinal) || Status.StartsWith("Building:", StringComparison.Ordinal))
            throw new InvalidOperationException("A playtest build is already queued/running.");

        string commit = Git("rev-parse HEAD").Trim();
        if (commit.Length != 40 || commit.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("Cannot identify the source commit.");
        string id = "TechnicalSwordAction-Win64-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + commit.Substring(0, 8);
        string directory = Path.Combine(Root, "Builds", "Playtest", id);
        if (Directory.Exists(directory)) throw new InvalidOperationException("Refusing to overwrite an existing build.");
        var result = new Result {
            buildId = id, sourceCommit = commit, workingTreeDirty = !string.IsNullOrWhiteSpace(Git("status --porcelain=v1")),
            unityVersion = Application.unityVersion,
            scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString(),
            outputDirectory = Path.Combine(directory, "Player"), reportPath = Path.Combine(directory, "BuildReport.json")
        };
        Directory.CreateDirectory(directory);
        SessionState.SetString(StatusKey, "Queued:" + result.reportPath);
        SessionState.SetString(PendingKey, JsonUtility.ToJson(result));
        RestoreQueuedBuild();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    [InitializeOnLoadMethod]
    private static void RestoreQueuedBuild()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(PendingKey, ""))) return;
        EditorApplication.update -= RunPending;
        EditorApplication.update += RunPending;
    }

    public static void RunPending()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
        EditorApplication.update -= RunPending;
        string pending = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(pending)) return;
        SessionState.EraseString(PendingKey);
        Build(JsonUtility.FromJson<Result>(pending));
    }

    public static void CancelQueued()
    {
        if (BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Do not cancel an active Unity build.");
        EditorApplication.update -= RunPending;
        SessionState.EraseString(PendingKey);
        SessionState.SetString(StatusKey, "Idle");
    }

    private static void Build(Result result)
    {
        SessionState.SetString(StatusKey, "Building:" + result.reportPath);
        var fullscreen = PlayerSettings.fullScreenMode;
        int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
        bool native = PlayerSettings.defaultIsNativeResolution, resizable = PlayerSettings.resizableWindow;
        try
        {
            // Only this build uses a modest, resizable window; always restore project settings.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultIsNativeResolution = false; PlayerSettings.resizableWindow = true;
            Directory.CreateDirectory(result.outputDirectory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { PrototypeCombatSceneBuilder.ScenePath },
                locationPathName = Path.Combine(result.outputDirectory, "TechnicalSwordAction.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            result.errors = (int)report.summary.totalErrors; result.warnings = (int)report.summary.totalWarnings;
            result.bytes = (long)report.summary.totalSize; result.seconds = report.summary.totalTime.TotalSeconds;
            result.messages = report.steps.SelectMany(step => step.messages)
                .Where(message => message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning)
                .Select(message => message.type + ": " + message.content).ToArray();
            result.success = report.summary.result == BuildResult.Succeeded;
            if (!result.success) throw new InvalidOperationException("Build result: " + report.summary.result);
            File.Copy(Path.Combine(Root, "docs", "playtest", "README-ja.txt"), Path.Combine(result.outputDirectory, "README.txt"));
            var info = new PackageInfo { buildId = result.buildId, sourceCommit = result.sourceCommit,
                unityVersion = result.unityVersion, scriptingBackend = result.scriptingBackend, workingTreeDirty = result.workingTreeDirty };
            File.WriteAllText(Path.Combine(result.outputDirectory, "BUILD_INFO.json"), JsonUtility.ToJson(info, true), new System.Text.UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            result.success = false; result.error = exception.ToString(); Debug.LogException(exception);
        }
        finally
        {
            PlayerSettings.fullScreenMode = fullscreen;
            PlayerSettings.defaultScreenWidth = width; PlayerSettings.defaultScreenHeight = height;
            PlayerSettings.defaultIsNativeResolution = native; PlayerSettings.resizableWindow = resizable;
            result.completed = true;
            File.WriteAllText(result.reportPath, JsonUtility.ToJson(result, true), new System.Text.UTF8Encoding(false));
            SessionState.SetString(StatusKey, JsonUtility.ToJson(result));
        }
    }

    private static string Git(string arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("git", arguments) {
            WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        } };
        process.Start();
        string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(10000) || process.ExitCode != 0) throw new InvalidOperationException("Cannot inspect Git source state: " + error);
        return output;
    }
}
