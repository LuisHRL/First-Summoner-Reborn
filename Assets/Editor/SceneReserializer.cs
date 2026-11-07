
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SceneReserializer
{
    [MenuItem("Tools/Asset Dependency Manager/Reserialize GameScene")]
    public static void Reserialize()
    {
        const string path = "Assets/Scenes/GameScene.unity";

        if (!File.Exists(path))
        {
            Debug.LogError("[ADM] GameScene introuvable.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "Reserialize GameScene",
            "Une sauvegarde de GameScene existe-t-elle ?",
            "Oui, continuer",
            "Annuler"))
            return;

        try
        {
            AssetDatabase.ForceReserializeAssets(
                new[] { path },
                ForceReserializeAssetsOptions.ReserializeAssets
            );

            byte[] header = new byte[5];

            using (var stream = File.OpenRead(path))
                stream.Read(header, 0, header.Length);

            string text = System.Text.Encoding.ASCII.GetString(header);

            if (text == "%YAML")
                Debug.Log("[ADM] GameScene convertie en YAML.");
            else
                Debug.LogWarning(
                    "[ADM] GameScene reste en format binaire."
                );
        }
        catch (Exception ex)
        {
            Debug.LogError("[ADM] Erreur : " + ex);
        }
    }
}
