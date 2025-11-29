// Assets/Editor/PostBuildATT.cs
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class PostBuildATT
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        plist.root.SetString("NSUserTrackingUsageDescription",
            "このアプリでは最適な広告を表示するために情報を使用します");

        plist.WriteToFile(plistPath);
    }
}