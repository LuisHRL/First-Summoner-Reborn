using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public class AssetRipperSpriteAtlasImporter : EditorWindow
{
    private readonly Dictionary<string, List<Rect>> previewRectangles =
        new Dictionary<string, List<Rect>>(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> previewTextures =
        new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private string selectedPreviewAtlas;
    private Vector2 previewScroll;

    private DefaultAsset spritesFolder;
    private DefaultAsset spriteAtlasesFolder;
    private DefaultAsset texturesFolder;

    [Serializable]
    private class SpriteData
    {
        public string m_Name;
        public RenderDataKey m_RenderDataKey;
    }

    [Serializable]
    private class AtlasData
    {
        public string m_Name;
        public string[] m_PackedSpriteNamesToIndex;
        public PackedSpriteReference[] m_PackedSprites;
        public RenderDataEntry[] m_RenderDataMap;
    }

    [Serializable]
    private class PackedSpriteReference
    {
        public string m_Collection;
        public long m_PathID;
    }

    [Serializable]
    private class RenderDataEntry
    {
        public RenderDataKey Key;
        public RenderDataValue Value;
    }

    [Serializable]
    private class RenderDataKey
    {
        public RenderDataGuid Key;
        public long Value;
    }

    [Serializable]
    private class RenderDataGuid
    {
        public uint m_Data_0_;
        public uint m_Data_1_;
        public uint m_Data_2_;
        public uint m_Data_3_;
    }

    [Serializable]
    private class RenderDataValue
    {
        public SpriteRect m_TextureRect;
        public int m_SettingsRaw;
    }

    [Serializable]
    private class SpriteRect
    {
        public float m_X;
        public float m_Y;
        public float m_Width;
        public float m_Height;
    }

    [MenuItem("Tools/Sprite Atlas Importer")]
    public static void ShowWindow()
    {
        GetWindow<AssetRipperSpriteAtlasImporter>("Sprite Atlas Importer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AssetRipper Sprite Atlas Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Analyse et import non destructif : les sprites sont crees dans des copies des PNG.",
            MessageType.Info);

        EditorGUILayout.Space();
        spritesFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Sprites Folder", spritesFolder, typeof(DefaultAsset), false);
        spriteAtlasesFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Sprite Atlases Folder", spriteAtlasesFolder, typeof(DefaultAsset), false);
        texturesFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Textures Folder", texturesFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space();
        bool ready = spritesFolder != null && spriteAtlasesFolder != null && texturesFolder != null;
        if (!ready)
            EditorGUILayout.HelpBox("Selectionne les trois dossiers AssetRipper.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("Analyze Atlas", GUILayout.Height(32)))
                AnalyzeAtlas();
            if (GUILayout.Button("Preview Sprites", GUILayout.Height(32)))
                PreviewSprites();
            if (GUILayout.Button("Create Sprites (safe copies)", GUILayout.Height(32)))
                CreateSpritesSafe();
        }

        DrawPreview();
    }

    private static string GetRenderDataKey(RenderDataKey key)
    {
        if (key == null || key.Key == null)
            return null;

        RenderDataGuid guid = key.Key;
        return guid.m_Data_0_ + "-" + guid.m_Data_1_ + "-" +
               guid.m_Data_2_ + "-" + guid.m_Data_3_ + "-" + key.Value;
    }

    private static bool IsValidRect(SpriteRect rect, Texture2D texture)
    {
        if (rect == null || texture == null)
            return false;

        return !float.IsNaN(rect.m_X) && !float.IsNaN(rect.m_Y) &&
               !float.IsNaN(rect.m_Width) && !float.IsNaN(rect.m_Height) &&
               !float.IsInfinity(rect.m_X) && !float.IsInfinity(rect.m_Y) &&
               !float.IsInfinity(rect.m_Width) && !float.IsInfinity(rect.m_Height) &&
               rect.m_X >= 0 && rect.m_Y >= 0 &&
               rect.m_Width > 0 && rect.m_Height > 0 &&
               rect.m_X + rect.m_Width <= texture.width &&
               rect.m_Y + rect.m_Height <= texture.height;
    }

    private static string FolderPath(DefaultAsset folder, string label)
    {
        string path = AssetDatabase.GetAssetPath(folder);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            throw new InvalidOperationException("Dossier invalide : " + label);
        return path;
    }

    private static List<string> FindJsonFiles(string folder)
    {
        var result = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                result.Add(path);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private void PreviewSprites()
    {
        try
        {
            string atlasFolder = FolderPath(spriteAtlasesFolder, "Sprite Atlases Folder");
            string textureFolder = FolderPath(texturesFolder, "Textures Folder");
            FolderPath(spritesFolder, "Sprites Folder");

            // Repartir d'un apercu vide pour eviter d'afficher d'anciennes donnees.
            previewRectangles.Clear();
            previewTextures.Clear();
            selectedPreviewAtlas = null;

            var texturePaths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith("sactx-", StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    texturePaths.Add(path);
            }

            foreach (string atlasPath in FindJsonFiles(atlasFolder))
            {
                try
                {
                    TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(atlasPath);
                    AtlasData atlas = json == null ? null : JsonUtility.FromJson<AtlasData>(json.text);
                    if (atlas == null || string.IsNullOrEmpty(atlas.m_Name) || atlas.m_RenderDataMap == null)
                    {
                        Debug.LogWarning("PREVIEW : JSON incomplet : " + atlasPath);
                        continue;
                    }

                    var matches = texturePaths.FindAll(path =>
                        Path.GetFileNameWithoutExtension(path).IndexOf(
                            "-" + atlas.m_Name + "-", StringComparison.Ordinal) >= 0);
                    if (matches.Count != 1)
                    {
                        Debug.LogWarning("PREVIEW : association PNG ambigue ou absente : " + atlas.m_Name);
                        continue;
                    }

                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(matches[0]);
                    if (texture == null)
                        continue;

                    var rectangles = new List<Rect>();
                    int invalid = 0;
                    foreach (RenderDataEntry entry in atlas.m_RenderDataMap)
                    {
                        if (entry == null || entry.Value == null ||
                            !IsValidRect(entry.Value.m_TextureRect, texture))
                        {
                            invalid++;
                            continue;
                        }
                        SpriteRect rect = entry.Value.m_TextureRect;
                        rectangles.Add(new Rect(rect.m_X, rect.m_Y, rect.m_Width, rect.m_Height));
                    }

                    previewRectangles[atlas.m_Name] = rectangles;
                    previewTextures[atlas.m_Name] = texture;
                    if (selectedPreviewAtlas == null)
                        selectedPreviewAtlas = atlas.m_Name;
                    Debug.Log("PREVIEW RECTANGLES : " + atlas.m_Name +
                              " | Rectangles prepares : " + rectangles.Count +
                              " | Invalides : " + invalid);
                }
                catch (Exception ex)
                {
                    Debug.LogError("PREVIEW : erreur dans " + atlasPath + " : " + ex);
                }
            }
            Repaint();
        }
        catch (Exception ex)
        {
            Debug.LogError("Erreur pendant la previsualisation : " + ex);
        }
    }

    private void DrawPreview()
    {
        if (previewRectangles.Count == 0)
            return;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Apercu des decoupes (lecture seule)", EditorStyles.boldLabel);
        var names = new List<string>(previewRectangles.Keys);
        names.Sort(StringComparer.Ordinal);
        int selectedIndex = Mathf.Max(0, names.IndexOf(selectedPreviewAtlas));
        selectedPreviewAtlas = names[EditorGUILayout.Popup("Atlas", selectedIndex, names.ToArray())];

        Texture2D texture;
        List<Rect> rectangles;
        if (!previewTextures.TryGetValue(selectedPreviewAtlas, out texture) ||
            !previewRectangles.TryGetValue(selectedPreviewAtlas, out rectangles) || texture == null)
            return;

        EditorGUILayout.LabelField(rectangles.Count + " rectangles | " +
                                   texture.width + " x " + texture.height + " pixels");
        EditorGUILayout.HelpBox(
            "Contours cyan = rectangles du JSON. Apercu uniquement, aucun import applique. " +
            "Les rotations et les maillages serres ne sont pas encore reconstruits.",
            MessageType.Info);

        float width = Mathf.Max(100f, position.width - 42f);
        float scale = Mathf.Min(1f, width / texture.width);
        float imageWidth = texture.width * scale;
        float imageHeight = texture.height * scale;
        previewScroll = EditorGUILayout.BeginScrollView(previewScroll,
            GUILayout.MinHeight(180f), GUILayout.MaxHeight(560f));
        Rect imageRect = GUILayoutUtility.GetRect(imageWidth, imageHeight,
            GUILayout.Width(imageWidth), GUILayout.Height(imageHeight));
        EditorGUI.DrawRect(imageRect, Color.black);
        EditorGUI.DrawTextureTransparent(imageRect, texture, ScaleMode.StretchToFill);


        Handles.BeginGUI();
        Color oldColor = Handles.color;
        Handles.color = Color.cyan;
        foreach (Rect rect in rectangles)
        {
            // Unity stocke les rectangles des sprites depuis le bas de la texture ;
            // l'interface GUI utilise une origine en haut a gauche.
            float x = imageRect.x + rect.x * scale;
            float y = imageRect.y + (texture.height - rect.y - rect.height) * scale;
            float w = rect.width * scale;
            float h = rect.height * scale;
            Handles.DrawAAPolyLine(1.5f,
                new Vector3(x, y), new Vector3(x + w, y),
                new Vector3(x + w, y + h), new Vector3(x, y + h),
                new Vector3(x, y));
        }
        if (selectedPreviewAtlas == "Sprite_Atlas_UI_z_Ingame")
        {
            Rect target = new Rect(759, 1124, 120, 120);

            float x = imageRect.x + target.x * scale;
            float y = imageRect.y + (texture.height - target.y - target.height) * scale;
            float w = target.width * scale;
            float h = target.height * scale;

            Handles.color = Color.yellow;
            Handles.DrawAAPolyLine(4f,
                new Vector3(x, y),
                new Vector3(x + w, y),
                new Vector3(x + w, y + h),
                new Vector3(x, y + h),
                new Vector3(x, y));
        }
        Handles.color = oldColor;
        Handles.EndGUI();
        EditorGUILayout.EndScrollView();
    }

    private void AnalyzeAtlas()
    {
        try
        {
            string spritesPath = FolderPath(spritesFolder, "Sprites Folder");
            string atlasesPath = FolderPath(spriteAtlasesFolder, "Sprite Atlases Folder");
            string texturesPath = FolderPath(texturesFolder, "Textures Folder");

            var atlasTextures = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texturesPath }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith("sactx-", StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    atlasTextures.Add(path);
            }
            atlasTextures.Sort(StringComparer.Ordinal);

            var spriteByName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var spriteNamesByKey = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            int spriteJsonCount = 0;
            int invalidSpriteJsonCount = 0;
            foreach (string path in FindJsonFiles(spritesPath))
            {
                TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                try
                {
                    SpriteData sprite = json == null ? null : JsonUtility.FromJson<SpriteData>(json.text);
                    if (sprite == null || string.IsNullOrEmpty(sprite.m_Name))
                    {
                        invalidSpriteJsonCount++;
                        continue;
                    }

                    spriteJsonCount++;
                    HashSet<string> keys;
                    if (!spriteByName.TryGetValue(sprite.m_Name, out keys))
                    {
                        keys = new HashSet<string>(StringComparer.Ordinal);
                        spriteByName.Add(sprite.m_Name, keys);
                    }
                    string key = GetRenderDataKey(sprite.m_RenderDataKey);
                    if (!string.IsNullOrEmpty(key))
                    {
                        keys.Add(key);
                        HashSet<string> names;
                        if (!spriteNamesByKey.TryGetValue(key, out names))
                        {
                            names = new HashSet<string>(StringComparer.Ordinal);
                            spriteNamesByKey.Add(key, names);
                        }
                        names.Add(sprite.m_Name);
                    }
                }
                catch (Exception ex)
                {
                    invalidSpriteJsonCount++;
                    Debug.LogWarning("Sprite JSON illisible : " + path + " | " + ex.Message);
                }
            }

            List<string> atlasFiles = FindJsonFiles(atlasesPath);
            Debug.Log("=== ANALYSE ASSETRIPPER ===\n" +
                      "Textures sactx PNG : " + atlasTextures.Count +
                      "\nAtlas JSON : " + atlasFiles.Count +
                      "\nSprite JSON lisibles : " + spriteJsonCount +
                      "\nSprite JSON invalides : " + invalidSpriteJsonCount);

            int totalValid = 0;
            int totalInvalid = 0;
            int totalNames = 0;
            int totalKeys = 0;

            foreach (string atlasPath in atlasFiles)
            {
                try
                {
                    TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(atlasPath);
                    AtlasData atlas = json == null ? null : JsonUtility.FromJson<AtlasData>(json.text);
                    if (atlas == null || string.IsNullOrEmpty(atlas.m_Name) ||
                        atlas.m_PackedSpriteNamesToIndex == null || atlas.m_RenderDataMap == null)
                    {
                        Debug.LogWarning("Atlas JSON incomplet : " + atlasPath);
                        continue;
                    }

                    var matches = new List<string>();
                    foreach (string texturePath in atlasTextures)
                    {
                        string filename = Path.GetFileNameWithoutExtension(texturePath);
                        if (filename.IndexOf("-" + atlas.m_Name + "-", StringComparison.Ordinal) >= 0)
                            matches.Add(texturePath);
                    }

                    if (matches.Count != 1)
                    {
                        Debug.LogWarning("Atlas " + atlas.m_Name +
                                         " : " + matches.Count + " texture(s) correspondante(s). " +
                                         "Validation ignoree (association ambigue ou absente).");
                        continue;
                    }

                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(matches[0]);
                    if (texture == null)
                    {
                        Debug.LogWarning("Texture introuvable : " + matches[0]);
                        continue;
                    }

                    int valid = 0;
                    int invalid = 0;
                    var settingsCounts = new SortedDictionary<int, int>();
                    var placementKeys = new HashSet<string>(StringComparer.Ordinal);
                    var specialPacking = new List<string>();
                    foreach (RenderDataEntry entry in atlas.m_RenderDataMap)
                    {
                        if (entry == null || entry.Value == null ||
                            !IsValidRect(entry.Value.m_TextureRect, texture))
                            invalid++;
                        else
                            valid++;

                        if (entry == null || entry.Value == null)
                            continue;

                        int settings = entry.Value.m_SettingsRaw;
                        if (!settingsCounts.ContainsKey(settings))
                            settingsCounts[settings] = 0;
                        settingsCounts[settings]++;

                        string key = GetRenderDataKey(entry.Key);
                        if (!string.IsNullOrEmpty(key))
                            placementKeys.Add(key);

                        // Valeurs differentes de 67 : afficher l'identifiant et le nom
                        // retrouve dans les JSON Sprite, sans supposer une rotation.
                        if (settings != 67)
                        {
                            string namesText = "Nom introuvable";
                            HashSet<string> names;
                            if (!string.IsNullOrEmpty(key) &&
                                spriteNamesByKey.TryGetValue(key, out names))
                            {
                                var orderedNames = new List<string>(names);
                                orderedNames.Sort(StringComparer.Ordinal);
                                namesText = string.Join(", ", orderedNames.ToArray());
                            }
                            specialPacking.Add("m_SettingsRaw=" + settings +
                                " | Sprite=" + namesText +
                                " | Identifiant=" + (key ?? "absent"));
                        }
                    }
                    if (atlas.m_Name == "Sprite_Atlas_UI_z_Ingame")
                    {
                        foreach (RenderDataEntry entry in atlas.m_RenderDataMap)
                        {
                            if (entry == null || entry.Value == null)
                                continue;

                            string key = GetRenderDataKey(entry.Key);

                            if (string.IsNullOrEmpty(key) ||
                                !spriteNamesByKey.ContainsKey(key))
                            {
                                Debug.LogWarning(
                                    "SPRITE SANS CORRESPONDANCE : Atlas=" + atlas.m_Name +
                                    " | Key=" + (key ?? "absente") +
                                    " | SettingsRaw=" + entry.Value.m_SettingsRaw
                                );
                            }
                        }
                    }
                    if (atlas.m_Name == "Sprite_Atlas_UI_z_total")
                    {
                        var seenNames = new HashSet<string>(StringComparer.Ordinal);

                        foreach (string name in atlas.m_PackedSpriteNamesToIndex)
                        {
                            if (!seenNames.Add(name))
                                Debug.LogWarning("NOM EN DOUBLE : " + name);
                        }
                    }
                    int matchingNames = 0;
                    int matchingKeys = 0;
                    foreach (string name in atlas.m_PackedSpriteNamesToIndex)
                    {
                        HashSet<string> keys;
                        if (string.IsNullOrEmpty(name) || !spriteByName.TryGetValue(name, out keys))
                            continue;
                        matchingNames++;
                        foreach (string key in keys)
                        {
                            if (placementKeys.Contains(key))
                            {
                                matchingKeys++;
                                break;
                            }
                        }
                    }

                    totalValid += valid;
                    totalInvalid += invalid;
                    totalNames += matchingNames;
                    totalKeys += matchingKeys;

                    string report = "ATLAS : " + atlas.m_Name +
                                    "\nTexture : " + Path.GetFileName(matches[0]) +
                                    " (" + texture.width + "x" + texture.height + ")" +
                                    "\nNoms : " + atlas.m_PackedSpriteNamesToIndex.Length +
                                    " | References : " + (atlas.m_PackedSprites == null ? 0 : atlas.m_PackedSprites.Length) +
                                    " | Placements : " + atlas.m_RenderDataMap.Length +
                                    "\nRectangles valides : " + valid + " | Invalides : " + invalid +
                                    "\nNoms associes aux Sprite JSON : " + matchingNames +
                                    " | Identifiants associes : " + matchingKeys +
                                    "\nm_SettingsRaw : ";
                    bool first = true;
                    foreach (KeyValuePair<int, int> setting in settingsCounts)
                    {
                        if (!first) report += ", ";
                        report += setting.Key + " (" + setting.Value + " sprites)";
                        first = false;
                    }
                    Debug.Log(report);
                    foreach (string special in specialPacking)
                        Debug.Log("PACKING PARTICULIER : " + atlas.m_Name + " | " + special);
                }
                catch (Exception ex)
                {
                    Debug.LogError("Erreur atlas " + atlasPath + " : " + ex);
                }
            }

            Debug.Log("=== BILAN ===\nRectangles valides : " + totalValid +
                      "\nRectangles invalides : " + totalInvalid +
                      "\nCorrespondances par nom : " + totalNames +
                      "\nCorrespondances par identifiant : " + totalKeys +
                      "\nAucun fichier n'a ete modifie.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Erreur pendant l'analyse : " + ex);
        }
    }

    // Import non destructif : cree de nouvelles copies PNG dans Assets/Generated/AssetRipperSprites.
    // Autorise les settings 67 et 3 pour tester les sprites particuliers.
    private void CreateSpritesSafe()
    {
        const string outputRoot = "Assets/Generated/AssetRipperSprites_Complete";
        if (!EditorUtility.DisplayDialog("Creer les sprites",
            "Des COPIES des atlas PNG seront creees dans " + outputRoot +
            ". Les fichiers originaux ne seront pas modifies. " +
            "Les settings 67 et 3 seront inclus pour verification. Continuer ?",
            "Creer les copies", "Annuler"))
            return;

        try
        {
            string spritesPath = FolderPath(spritesFolder, "Sprites Folder");
            string atlasesPath = FolderPath(spriteAtlasesFolder, "Sprite Atlases Folder");
            string texturesPath = FolderPath(texturesFolder, "Textures Folder");
            EnsureAssetFolder(outputRoot);

            var namesByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            var ambiguousKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in FindJsonFiles(spritesPath))
            {
                TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (json == null) continue;
                SpriteData sprite;
                try { sprite = JsonUtility.FromJson<SpriteData>(json.text); }
                catch { continue; }
                if (sprite == null || string.IsNullOrEmpty(sprite.m_Name)) continue;
                string key = GetRenderDataKey(sprite.m_RenderDataKey);
                if (string.IsNullOrEmpty(key)) continue;
                string existing;
                if (namesByKey.TryGetValue(key, out existing) && existing != sprite.m_Name)
                    ambiguousKeys.Add(key);
                else
                    namesByKey[key] = sprite.m_Name;
            }

            var texturePaths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texturesPath }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith("sactx-", StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    texturePaths.Add(path);
            }

            int createdAtlases = 0, createdSprites = 0, skipped = 0;
            foreach (string atlasPath in FindJsonFiles(atlasesPath))
            {
                if (!Path.GetFileNameWithoutExtension(atlasPath)
        .Equals("Sprite_Atlas_UI_z_Ingame", StringComparison.Ordinal))
                    continue;
                TextAsset atlasJson = AssetDatabase.LoadAssetAtPath<TextAsset>(atlasPath);
                if (atlasJson == null) continue;
                AtlasData atlas;
                try { atlas = JsonUtility.FromJson<AtlasData>(atlasJson.text); }
                catch (Exception ex) { Debug.LogWarning("Atlas JSON illisible : " + ex.Message); continue; }
                if (atlas == null || string.IsNullOrEmpty(atlas.m_Name) || atlas.m_RenderDataMap == null)
                    continue;

                var matches = texturePaths.FindAll(path =>
                    Path.GetFileNameWithoutExtension(path).IndexOf(
                        "-" + atlas.m_Name + "-", StringComparison.Ordinal) >= 0);
                if (matches.Count != 1)
                {
                    Debug.LogWarning("Import ignore : texture absente ou ambigue pour " + atlas.m_Name);
                    continue;
                }
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(matches[0]);
                if (texture == null) continue;

                var slices = new List<UnityEditor.SpriteRect>();
                var usedNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (RenderDataEntry entry in atlas.m_RenderDataMap)
                {
                    if (entry == null || entry.Value == null ||
                        !IsValidRect(entry.Value.m_TextureRect, texture) ||
                        (entry.Value.m_SettingsRaw != 67 &&
                         entry.Value.m_SettingsRaw != 3))
                    {
                        Debug.LogWarning(
                            "PLACEMENT IGNORE : Atlas=" + atlas.m_Name +
                            " | SettingsRaw=" + (entry?.Value == null ? "absent" : entry.Value.m_SettingsRaw.ToString()) +
                            " | Key=" + (entry == null ? "absent" : GetRenderDataKey(entry.Key))
                        );
                        skipped++;
                        continue;
                    }
                    string key = GetRenderDataKey(entry.Key);
                    string spriteName;
                    if (string.IsNullOrEmpty(key) || ambiguousKeys.Contains(key) ||
    !namesByKey.TryGetValue(key, out spriteName))
                    {
                        skipped++;
                        continue;
                    }

                    string originalName = spriteName;
                    int duplicateIndex = 2;

                    while (!usedNames.Add(spriteName))
                    {
                        spriteName = originalName + "_" + duplicateIndex;
                        duplicateIndex++;
                    }
                    SpriteRect r = entry.Value.m_TextureRect;
                    slices.Add(new UnityEditor.SpriteRect
                    {
                        name = spriteName,
                        rect = new Rect(r.m_X, r.m_Y, r.m_Width, r.m_Height),
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        spriteID = GUID.Generate()
                    });
                }
                if (slices.Count == 0) continue;

                // Aucun ecrasement : si la copie existe deja, cet atlas est ignore.
                string destination = outputRoot + "/" + atlas.m_Name + ".png";
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destination) != null ||
                    File.Exists(destination))
                {
                    Debug.LogWarning("Copie deja presente, atlas ignore : " + destination);
                    continue;
                }
                if (!AssetDatabase.CopyAsset(matches[0], destination))
                {
                    Debug.LogError("Impossible de copier " + matches[0]);
                    continue;
                }
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(destination) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError("TextureImporter introuvable : " + destination);
                    continue;
                }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();

                var factories = new SpriteDataProviderFactories();
                factories.Init();
                ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                if (provider == null)
                {
                    Debug.LogError("Sprite Editor Data Provider indisponible : " + destination);
                    continue;
                }
                provider.InitSpriteEditorDataProvider();
                provider.SetSpriteRects(slices.ToArray());
                provider.Apply();
                importer.SaveAndReimport();
                createdAtlases++;
                createdSprites += slices.Count;
                Debug.Log("SPRITES CREES : " + atlas.m_Name + " | " + slices.Count +
                          " sprites | " + destination);
            }
            AssetDatabase.Refresh();
            Debug.Log("IMPORT TERMINE : " + createdAtlases + " copies PNG, " +
                      createdSprites + " sprites crees, " + skipped +
                      " placements ignores. Originaux inchanges.");
            EditorUtility.DisplayDialog("Import termine",
                createdAtlases + " atlas copies ; " + createdSprites +
                " sprites crees. Voir la Console pour les details.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("Erreur pendant la creation des sprites : " + ex);
            EditorUtility.DisplayDialog("Erreur", ex.Message, "OK");
        }
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
