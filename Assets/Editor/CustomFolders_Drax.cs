using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using static System.IO.Directory;
using static System.IO.Path;
using static UnityEngine.Application;
using static UnityEditor.AssetDatabase;

public class CustomFolders_Drax : MonoBehaviour
{
    [MenuItem("Tools/Create/CreateDefaultFolders")]
    public static void CreateDefaultFolders()
    {
        Dir("_Projects","Scenes","Fonts","Scripts", "Anims", "Materials", "Assets", "Sprites", "Videos","SFX", "Models", "Prefabs","Audios"); 
        Refresh();
    }
    public static void Dir(string root, params string[] dir)
    {
        var fullpath = Combine(dataPath, root);
        foreach(var newDirectory in dir)
        CreateDirectory(Combine(fullpath, newDirectory));
    }

}
