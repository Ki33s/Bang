using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Build requests let project tooling use an already-open editor instead of starting a second one.
[InitializeOnLoad]
public static class EditorTaskRunner
{
    [Serializable] class Request { public string task; }
    [Serializable] class Status { public string task, state, message; }
    static bool busy;static double nextCheck;
    static EditorTaskRunner(){EditorApplication.update+=Check;}
    [MenuItem("Пыль и порох/Подготовить модели и анимации")]
    static void Prepare(){PrepareTurnAssets.Prepare();}
    [MenuItem("Пыль и порох/Собрать один ход Windows")]
    static void Windows(){BuildProject.TurnWindows();}
    [MenuItem("Пыль и порох/Собрать один ход Web")]
    static void Web(){BuildProject.TurnWeb();}
    static void Check()
    {
        if(Application.isBatchMode||busy||EditorApplication.timeSinceStartup<nextCheck)return;
        nextCheck=EditorApplication.timeSinceStartup+1;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        const string requestPath="Logs/EditorRequest.json";if(!File.Exists(requestPath))return;
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
        // Rename this individual request before executing, so it cannot run twice on the next update.
        File.Copy(requestPath,"Logs/EditorRequest.received.json",true);File.Delete(requestPath);
        busy=true;Write(request.task,"running","");
        try
        {
            switch(request.task){case "Prepare":Prepare();break;case "TurnWindows":Windows();break;case "TurnWeb":Web();break;default:throw new ArgumentException("Unknown editor task");}
            Write(request.task,"completed","");
        }
        catch(Exception ex){Debug.LogException(ex);Write(request.task,"failed",ex.Message);}
        finally{busy=false;}
    }
    static void Write(string task,string state,string message)
    {File.WriteAllText("Logs/EditorTaskStatus.json",JsonUtility.ToJson(new Status{task=task,state=state,message=message},true));}
}
