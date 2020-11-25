using System.IO;
using UnityEngine;

public static class Git
{
    public static string GetLastCommit()
    {
        var pathToGitFolder = Path.Combine(Application.dataPath.Remove(Application.dataPath.Length - 7), ".git");
        var currentRef = 
            File.ReadAllText(Path.Combine(pathToGitFolder, "HEAD")).Remove(0, 16).TrimEnd();
        return File.ReadAllText(Path.Combine(pathToGitFolder, "refs", "heads", currentRef)).TrimEnd();
    }
}