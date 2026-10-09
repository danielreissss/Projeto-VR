using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds usados no teste com servidor Linux (DGTI/DCC) + óculos na rede 5G.
// Também podem ser chamados por linha de comando:
//   Unity -batchmode -quit -projectPath . -executeMethod ServerBuild.BuildLinuxServer
//   Unity -batchmode -quit -projectPath . -executeMethod ServerBuild.BuildAndroidApk
public static class ServerBuild
{
    private const string MainScene = "Assets/Scenes/ConsultorioFinal.unity";

    [MenuItem("Build/Servidor Linux (Dedicated Server)")]
    public static void BuildLinuxServer()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { MainScene },
            locationPathName = "Builds/LinuxServer/ConsultorioServer.x86_64",
            target = BuildTarget.StandaloneLinux64,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.None,
        };
        Run(options);
    }

    [MenuItem("Build/APK Meta Quest (Android)")]
    public static void BuildAndroidApk()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { MainScene },
            locationPathName = "Builds/Android/ConsultorioVR.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };
        Run(options);
    }

    private static void Run(BuildPlayerOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.locationPathName));
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[ServerBuild] {options.target}: {report.summary.result} -> {options.locationPathName}");
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
