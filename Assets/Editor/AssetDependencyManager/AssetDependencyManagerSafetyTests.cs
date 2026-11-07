// Standalone, non-destructive Editor smoke test for migration backup and rollback.
// Generates only disposable files and deletes them in finally.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal static class AssetDependencyManagerSafetyTests
{
    // Uses disposable ScriptableObject assets to exercise the real destination resolver.
    internal static void RunReferenceMatching()
    {
        string token = Guid.NewGuid().ToString("N");
        string sourcePath = "Assets/__ADM_Source_" + token + ".asset";
        string destinationPath = "Assets/__ADM_Destination_" + token + ".asset";
        string mismatchPath = "Assets/__ADM_Mismatch_" + token + ".asset";
        try
        {
            var source = ScriptableObject.CreateInstance<ADMReferenceFixture>();
            source.name = "ReferenceFixture";
            AssetDatabase.CreateAsset(source, sourcePath);
            var destination = ScriptableObject.CreateInstance<ADMReferenceFixture>();
            destination.name = "ReferenceFixture";
            AssetDatabase.CreateAsset(destination, destinationPath);
            var mismatch = ScriptableObject.CreateInstance<ADMReferenceFixture>();
            mismatch.name = "DifferentName";
            AssetDatabase.CreateAsset(mismatch, mismatchPath);
            AssetDatabase.SaveAssets();

            MethodInfo resolver = typeof(AssetDependencyManager).GetMethod(
                "TryResolveSerializedDestination", BindingFlags.Static | BindingFlags.NonPublic);
            if (resolver == null) throw new InvalidOperationException("Reference resolver not found.");

            // Separate main assets should share a local ID, but have distinct GUIDs.
            string sourceGuid, destinationGuid;
            long sourceId, destinationId;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out sourceGuid, out sourceId) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(destination, out destinationGuid, out destinationId))
                throw new InvalidOperationException("Could not read fixture GUID/fileID.");
            if (sourceGuid == destinationGuid || sourceId != destinationId)
                throw new InvalidOperationException("Test fixtures have unexpected GUID/fileID.");

            object[] good = { source, destinationPath, null, null };
            bool accepted = (bool)resolver.Invoke(null, good);
            if (!accepted || good[2] != destination)
                throw new InvalidOperationException("Matching type/name/fileID reference was rejected: " + good[3]);

            object[] bad = { source, mismatchPath, null, null };
            if ((bool)resolver.Invoke(null, bad) || bad[2] != null)
                throw new InvalidOperationException("Mismatched object name was accepted.");

            Debug.Log("[ADM] Reference matching self-test PASSED (temporary assets only).");
        }
        catch (Exception ex)
        {
            Debug.LogError("[ADM] Reference matching self-test FAILED: " + ex);
            throw;
        }
        finally
        {
            AssetDatabase.DeleteAsset(sourcePath);
            AssetDatabase.DeleteAsset(destinationPath);
            AssetDatabase.DeleteAsset(mismatchPath);
        }
    }

    internal static void Run()
    {
        string assetRelative = "Assets/__ADM_SafetyTest_" + Guid.NewGuid().ToString("N") + ".txt";
        string assetAbsolute = Path.GetFullPath(assetRelative);
        string backupRoot = Path.Combine(Path.GetTempPath(), "ADM_SafetyTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Type manager = typeof(AssetDependencyManager);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo backup = manager.GetMethod("BackupMigrationFile", flags);
            MethodInfo restore = manager.GetMethod("RestoreMigrationFile", flags);
            MethodInfo path = manager.GetMethod("MigrationBackupPath", flags);
            if (backup == null || restore == null || path == null)
                throw new InvalidOperationException("Migration backup API was changed; update this test.");

            // Reject traversal before any file can be touched.
            bool rejected = false;
            try { path.Invoke(null, new object[] { backupRoot, "Assets/../outside.txt" }); }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
            { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Traversal path was accepted.");

            const string original = "original migration fixture";
            File.WriteAllText(assetAbsolute, original);
            backup.Invoke(null, new object[] { backupRoot, assetRelative });
            File.WriteAllText(assetAbsolute, "simulated partial migration");
            restore.Invoke(null, new object[] { backupRoot, assetRelative });
            if (File.ReadAllText(assetAbsolute) != original)
                throw new InvalidOperationException("Rollback did not restore original bytes.");

            Debug.Log("[ADM] Backup and rollback self-test PASSED (temporary fixture only).");
        }
        catch (Exception ex)
        {
            Debug.LogError("[ADM] Backup and rollback self-test FAILED: " + ex);
            throw;
        }
        finally
        {
            if (File.Exists(assetAbsolute)) File.Delete(assetAbsolute);
            if (Directory.Exists(backupRoot)) Directory.Delete(backupRoot, true);
        }
    }
}

// Editor-only fixture type; no production asset depends on it.
internal sealed class ADMReferenceFixture : ScriptableObject { }
