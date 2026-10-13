using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildProject
{
    public static void Prepare()
    {
        Directory.CreateDirectory("Assets/Scenes");
        if (!File.Exists("Assets/Scenes/Main.unity"))
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Camera").AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.09f,.11f);camera.orthographic=true;
            camera.gameObject.AddComponent<AudioListener>(); EditorSceneManager.SaveScene(scene,"Assets/Scenes/Main.unity");
        }
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Main.unity",true)};
        PlayerSettings.companyName="DustAndPowder";PlayerSettings.productName="Пыль и порох";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.runInBackground=true;PlayerSettings.resizableWindow=true;
        PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.template="PROJECT:Bang";
        PlayerSettings.WebGL.memorySize=256;
        AssetDatabase.SaveAssets();
        Debug.Log("BANG_PROJECT_PREPARED");
    }
    public static void Windows()
    { Prepare();Build("Builds/Windows/DustAndPowder.exe",BuildTarget.StandaloneWindows64); }
    public static void Web()
    { Prepare();Build("Builds/Web",BuildTarget.WebGL); }
    public static void TurnWindows()
    { PrepareTurn();Build("Builds/OneTurn/DustAndPowder-OneTurn.exe",BuildTarget.StandaloneWindows64); }
    public static void TurnWeb()
    { PrepareTurn();Build("Builds/Web",BuildTarget.WebGL); }
    static void PrepareTurn()
    {
        Prepare();PrepareTurnAssets.Prepare();
        if(!File.Exists("Assets/Scenes/OneTurn.unity"))
        {var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var camera=new GameObject("Camera").AddComponent<Camera>();camera.cullingMask=0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.09f,.11f);camera.gameObject.AddComponent<AudioListener>();EditorSceneManager.SaveScene(scene,"Assets/Scenes/OneTurn.unity");}
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/OneTurn.unity",true)};
    }
    static void Build(string path,BuildTarget target)
    {
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=path,target=target,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
        Debug.Log("BANG_BUILD_SUCCESS "+path);
    }
}
