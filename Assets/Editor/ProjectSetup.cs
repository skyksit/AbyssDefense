using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// 처음 프로젝트를 열면 메인 씬 생성, 빌드 목록 등록, 세로 화면 설정을 자동으로 한다.
/// 명령줄: Unity.exe -batchmode -projectPath . -executeMethod ProjectSetup.BuildWindowsTest -quit
[InitializeOnLoad]
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    static ProjectSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ScenePath) && !Application.isBatchMode) Setup();
        };
    }

    [MenuItem("AbyssDefense/1. 프로젝트 초기 설정")]
    public static void Setup()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.AddComponent<AudioListener>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.companyName = "Retrople";
        PlayerSettings.productName = "Abyss Defense";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.retrople.abydef");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.runInBackground = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 540;
        PlayerSettings.defaultScreenHeight = 960;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AbyssDefense] 초기 설정 완료: " + ScenePath + ". Game 뷰를 1080x1920(세로)로 맞추고 Play 하세요.");
    }

    [MenuItem("AbyssDefense/2. PC 테스트 빌드 (Windows)")]
    public static void BuildWindowsTest()
    {
        if (!File.Exists(ScenePath)) Setup();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Win/AbyssDefense.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
        });
        Debug.Log($"[AbyssDefense] PC 빌드 결과: {report.summary.result}, 오류 {report.summary.totalErrors}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }

    [MenuItem("AbyssDefense/3. 안드로이드 APK 빌드")]
    public static void BuildApk()
    {
        if (!File.Exists(ScenePath)) Setup();
        Directory.CreateDirectory("Builds");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/AbyssDefense.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });
        Debug.Log($"[AbyssDefense] APK 빌드 결과: {report.summary.result}, 크기 {report.summary.totalSize / (1024 * 1024)}MB → Builds/AbyssDefense.apk");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }

    [MenuItem("AbyssDefense/세이브 데이터 삭제")]
    public static void ClearSave()
    {
        foreach (var f in new[] { "save.json", "save_test.json" })
        {
            var p = Path.Combine(Application.persistentDataPath, f);
            if (File.Exists(p)) File.Delete(p);
        }
        Debug.Log("[AbyssDefense] 세이브 삭제 완료");
    }
}
