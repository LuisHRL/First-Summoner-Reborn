using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Companion diagnostic for AssetDependencyCleaner. Does not modify any project asset.
public static class AssetCleanerRemainingReferences
{
    private const string OldRoot = "Assets/tests-animations3/SkeletonArcher/";
    private const string NewRoot = "Assets/Prefabs/New-Troops/SkeletonArcher-new/";
    private static readonly Regex Reference = new Regex(@"\{[^{}\r\n]*fileID:\s*(-?\d+)[^{}\r\n]*guid:\s*([0-9a-fA-F]{32})[^{}\r\n]*\}", RegexOptions.Compiled);

    [MenuItem("Tools/Asset Cleaner/Inspect Remaining References")]
    public static void Inspect()
    {
        int found = 0;
        foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !p.StartsWith(OldRoot, StringComparison.Ordinal)))
        {
            if (!File.Exists(path)) continue;
            string content;
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Contains((byte)0)) continue;
                content = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch { continue; }

            foreach (Match match in Reference.Matches(content))
            {
                string oldGuid = match.Groups[2].Value;
                string oldPath = AssetDatabase.GUIDToAssetPath(oldGuid);
                if (!oldPath.StartsWith(OldRoot, StringComparison.Ordinal)) continue;
                string relative = oldPath.Substring(OldRoot.Length);
                string target = relative == "Prefab/SkeletonArcher.prefab"
                    ? NewRoot + "Prefab/SkeletonArcher-new.prefab"
                    : NewRoot + relative;
                string targetGuid = AssetDatabase.AssetPathToGUID(target);
                string idText = match.Groups[1].Value;
                long id;
                bool validId = long.TryParse(idText, out id);
                bool targetHasSameId = false;
                if (validId && !string.IsNullOrEmpty(targetGuid) && !target.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(target))
                    {
                        if (obj == null) continue;
                        string guid; long localId;
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out localId) && localId == id)
                        { targetHasSameId = true; break; }
                    }
                }
                int line = 1 + content.Substring(0, match.Index).Count(c => c == '\n');
                Debug.Log("[REMAINING REFERENCE] " + path + ":" + line +
                    "\n  Old: " + oldPath + " (fileID " + idText + ")" +
                    "\n  Proposed target: " + target +
                    "\n  Target exists: " + (!string.IsNullOrEmpty(targetGuid)) +
                    "\n  Same local ID visible in target: " + targetHasSameId +
                    "\n  NOTE: no replacement performed; prefab/scene root IDs may require special handling.");
                found++;
            }
        }
        Debug.Log("[Asset Cleaner] Remaining reference inspection complete. YAML references found: " + found + ". No files modified.");
    }
}
