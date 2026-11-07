// Standalone, non-destructive Editor smoke test for migration backup and rollback.
// Generates only disposable files and deletes them in finally.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal static class AssetDependencyManagerSafetyTests
{
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
