
using System;
using UnityEditor;
using UnityEngine;

public class ArcherAnimationImporter : AssetPostprocessor
{
    private const string AnimationFolder =
        "Assets/Imported/Archer/Animations/";

    private void OnPreprocessModel()
    {
        string path = assetPath.Replace('\\', '/');

        // Ne traiter que les FBX des animations Archer
        if (!path.StartsWith(
                AnimationFolder,
                StringComparison.OrdinalIgnoreCase))
            return;

        if (!path.EndsWith(
                ".fbx",
                StringComparison.OrdinalIgnoreCase))
            return;

        ModelImporter importer = assetImporter as ModelImporter;

        if (importer == null)
            return;

        // Configuration Humanoid
        importer.animationType = ModelImporterAnimationType.Human;

        // Chaque FBX possède son propre Avatar
        importer.avatarSetup =
            ModelImporterAvatarSetup.CreateFromThisModel;

        // Importer les animations
        importer.importAnimation = true;

        // Conserver la configuration du squelette
        HumanDescription human = importer.humanDescription;

        // Autoriser les translations des os Humanoid
        human.hasTranslationDoF = true;

        importer.humanDescription = human;
    }
}
