using Unity.Collections;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Rendering;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using BopCustomTextures.Scripts;
using ILogger = BopCustomTextures.Logging.ILogger;

namespace BopCustomTextures.Customs;

/// <summary>
/// Manages custom textures, including loading them from source files and applying them when the mixtape is played.
/// </summary>
/// <param name="logger">Plugin-specific logger</param>
public class CustomTextureManager(ILogger logger) : BaseCustomManager(logger)
{
    public readonly Dictionary<SceneKey, Dictionary<int, Texture2D>> CustomAtlasTextures = [];
    public readonly Dictionary<SceneKey, Dictionary<string, Texture2D>> CustomSeperateTextures = [];
    public readonly Dictionary<SceneKey, Dictionary<Texture2D, string>> CustomSeperateTexturesNotInited = [];
    public readonly HashSet<SceneKey> CustomSpritesInited = [];
    public readonly Dictionary<Texture2D, Dictionary<string, Sprite>> SpriteMaps = [];
    public readonly HashSet<Sprite> CustomSprites = [];
    public readonly Dictionary<Sprite, Sprite> OriginalSpriteByReplacement = [];
    public readonly Dictionary<Texture2D, (SceneKey, int)> TextureMaps = [];
    public readonly Dictionary<string, Dictionary<SceneKey, Dictionary<int, Texture2D>>> PreloadedAtlasTexturesByPackPath = new Dictionary<string, Dictionary<SceneKey, Dictionary<int, Texture2D>>>(System.StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, Dictionary<SceneKey, Dictionary<string, Texture2D>>> PreloadedSeperateTexturesByPackPath = new Dictionary<string, Dictionary<SceneKey, Dictionary<string, Texture2D>>>(System.StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, Texture2D> PreloadedTextureOverridesByPath = new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);
    public readonly List<string> DefaultPackPaths = [];
    public static readonly Regex PathRegex = new Regex(@"[\\/]text?u?r?e?s?$", RegexOptions.IgnoreCase);
    public static readonly Regex FileRegex = new Regex(@"^text?u?r?e?s?[\\/](\w+)[\\/].*?([^\\/]*\.(?:png|j(?:pe?g|pe|f?if|fi)))$", RegexOptions.IgnoreCase);
    public static readonly Regex FileRegexAtlas = new Regex(@"^sactx-(\d+)", RegexOptions.IgnoreCase);
    public static readonly Regex FileRegexSeperate = new Regex(@"^(\w+)");
    public static readonly Regex SceneAndSpriteAtlasIndexRegex = new Regex(@"^sactx-(\d+)-\d+x\d+-DXT5\|BC3-_(\w+)Atlas");

    public static bool IsCustomTextureDirectory(string path)
    {
        return PathRegex.IsMatch(path);
    }

    public int PreloadReferenceFrame(CustomTexReferenceFrame frame, string mixtapeRootPath)
    {
        ClearPreloadedTextureCaches();

        DefaultPackPaths.Clear();
        foreach (var fullPath in ResolveDefaultPackPathsByRegex(mixtapeRootPath))
        {
            DefaultPackPaths.Add(Path.GetFileName(fullPath));
        }

        var packPathsToLoad = new HashSet<string>(DefaultPackPaths, System.StringComparer.OrdinalIgnoreCase);
        foreach (var configuredPath in frame.TexturePackPaths)
        {
            packPathsToLoad.Add(configuredPath);
        }

        int filesLoaded = 0;
        foreach (var configuredPath in packPathsToLoad)
        {
            filesLoaded += PreloadTexturePack(configuredPath, mixtapeRootPath);
        }

        foreach (var overridePath in frame.TextureOverridePaths)
        {
            string resolvedPath = ResolveConfiguredPackPath(overridePath, mixtapeRootPath);
            if (!File.Exists(resolvedPath))
            {
                logger.LogWarning($"Texture override file does not exist: {resolvedPath}");
                continue;
            }
            Texture2D texture = LoadImage(resolvedPath, overridePath, Path.GetFileName(resolvedPath));
            if (texture != null)
            {
                PreloadedTextureOverridesByPath[overridePath] = texture;
                filesLoaded++;
            }
        }

        return filesLoaded;
    }

    public int ApplyRuntimeSelection(string activePackPath, Dictionary<string, string> textureOverrides, string mixtapeRootPath)
    {
        CustomAtlasTextures.Clear();
        CustomSeperateTextures.Clear();
        CustomSeperateTexturesNotInited.Clear();

        var packPaths = string.IsNullOrWhiteSpace(activePackPath)
            ? DefaultPackPaths
            : [activePackPath];

        foreach (var packPath in packPaths)
        {
            if (string.IsNullOrWhiteSpace(packPath))
            {
                continue;
            }
            if (!PreloadedAtlasTexturesByPackPath.TryGetValue(packPath, out var atlasByScene) ||
                !PreloadedSeperateTexturesByPackPath.TryGetValue(packPath, out var separateByScene))
            {
                string resolvedPath = ResolveConfiguredPackPath(packPath, mixtapeRootPath);
                logger.LogWarning($"Custom texture pack path does not exist: {resolvedPath}");
                continue;
            }
            MergeTexturePackIntoActive(atlasByScene, separateByScene);
        }

        foreach (var textureOverride in textureOverrides)
        {
            ApplyTextureOverrideFromCache(textureOverride.Key, textureOverride.Value, mixtapeRootPath);
        }

        return CustomAtlasTextures.Sum(x => x.Value.Count) + CustomSeperateTextures.Sum(x => x.Value.Count);
    }

    public void ClearPreloadedTextureCaches()
    {
        foreach (var atlasPack in PreloadedAtlasTexturesByPackPath.Values)
        {
            foreach (var atlasScene in atlasPack.Values)
            {
                foreach (var texture in atlasScene.Values)
                {
                    Object.Destroy(texture);
                }
            }
        }
        foreach (var separatePack in PreloadedSeperateTexturesByPackPath.Values)
        {
            foreach (var separateScene in separatePack.Values)
            {
                foreach (var texture in separateScene.Values)
                {
                    Object.Destroy(texture);
                }
            }
        }
        foreach (var overrideTexture in PreloadedTextureOverridesByPath.Values)
        {
            Object.Destroy(overrideTexture);
        }

        PreloadedAtlasTexturesByPackPath.Clear();
        PreloadedSeperateTexturesByPackPath.Clear();
        PreloadedTextureOverridesByPath.Clear();
        DefaultPackPaths.Clear();
    }

    public int PreloadTexturePack(string configuredPath, string mixtapeRootPath)
    {
        if (PreloadedAtlasTexturesByPackPath.ContainsKey(configuredPath) &&
            PreloadedSeperateTexturesByPackPath.ContainsKey(configuredPath))
        {
            return 0;
        }

        string resolvedPath = ResolveConfiguredPackPath(configuredPath, mixtapeRootPath);
        if (!Directory.Exists(resolvedPath))
        {
            logger.LogWarning($"Custom texture pack path does not exist: {resolvedPath}");
            PreloadedAtlasTexturesByPackPath[configuredPath] = [];
            PreloadedSeperateTexturesByPackPath[configuredPath] = [];
            return 0;
        }

        var atlasByScene = new Dictionary<SceneKey, Dictionary<int, Texture2D>>();
        var separateByScene = new Dictionary<SceneKey, Dictionary<string, Texture2D>>();
        int filesLoaded = 0;
        foreach (var fullFilepath in Directory.EnumerateFiles(resolvedPath, "*", SearchOption.AllDirectories))
        {
            if (LoadCustomTextureIntoCollections(fullFilepath, resolvedPath, configuredPath, atlasByScene, separateByScene))
            {
                filesLoaded++;
            }
        }
        PreloadedAtlasTexturesByPackPath[configuredPath] = atlasByScene;
        PreloadedSeperateTexturesByPackPath[configuredPath] = separateByScene;
        return filesLoaded;
    }

    public void MergeTexturePackIntoActive(
        Dictionary<SceneKey, Dictionary<int, Texture2D>> atlasByScene,
        Dictionary<SceneKey, Dictionary<string, Texture2D>> separateByScene
    )
    {
        foreach (var sceneEntry in atlasByScene)
        {
            if (!CustomAtlasTextures.TryGetValue(sceneEntry.Key, out var atlasTarget))
            {
                atlasTarget = [];
                CustomAtlasTextures[sceneEntry.Key] = atlasTarget;
            }
            foreach (var atlasEntry in sceneEntry.Value)
            {
                atlasTarget[atlasEntry.Key] = atlasEntry.Value;
            }
        }

        foreach (var sceneEntry in separateByScene)
        {
            if (!CustomSeperateTextures.TryGetValue(sceneEntry.Key, out var separateTarget))
            {
                separateTarget = [];
                CustomSeperateTextures[sceneEntry.Key] = separateTarget;
            }
            if (!CustomSeperateTexturesNotInited.TryGetValue(sceneEntry.Key, out var notInitedTarget))
            {
                notInitedTarget = [];
                CustomSeperateTexturesNotInited[sceneEntry.Key] = notInitedTarget;
            }
            foreach (var separateEntry in sceneEntry.Value)
            {
                separateTarget[separateEntry.Key] = separateEntry.Value;
                notInitedTarget[separateEntry.Value] = separateEntry.Key;
            }
        }
    }

    public void ApplyTextureOverrideFromCache(string qualifiedPath, string overridePath, string mixtapeRootPath)
    {
        if (!TryParseQualifiedTexturePath(qualifiedPath, out var scene, out var atlasIndex, out var spriteName, out var isAtlas))
        {
            logger.LogWarning($"Invalid texture override target: {qualifiedPath}");
            return;
        }

        if (!PreloadedTextureOverridesByPath.TryGetValue(overridePath, out var tex))
        {
            string resolvedPath = ResolveConfiguredPackPath(overridePath, mixtapeRootPath);
            logger.LogWarning($"Texture override file does not exist: {resolvedPath}");
            return;
        }

        if (isAtlas)
        {
            if (!CustomAtlasTextures.TryGetValue(scene, out var sceneAtlas))
            {
                sceneAtlas = [];
                CustomAtlasTextures[scene] = sceneAtlas;
            }
            sceneAtlas[atlasIndex] = tex;
            return;
        }

        if (!CustomSeperateTextures.TryGetValue(scene, out var sceneSeparate))
        {
            sceneSeparate = [];
            CustomSeperateTextures[scene] = sceneSeparate;
        }
        if (!CustomSeperateTexturesNotInited.TryGetValue(scene, out var sceneNotInited))
        {
            sceneNotInited = [];
            CustomSeperateTexturesNotInited[scene] = sceneNotInited;
        }
        sceneSeparate[spriteName] = tex;
        sceneNotInited[tex] = spriteName;
    }

    public bool LoadCustomTextureIntoCollections(
        string fullFilepath,
        string packRootPath,
        string localPrefix,
        Dictionary<SceneKey, Dictionary<int, Texture2D>> atlasByScene,
        Dictionary<SceneKey, Dictionary<string, Texture2D>> separateByScene
    )
    {
        string localFilepath = fullFilepath.Substring(packRootPath.Length).TrimStart('\\', '/');
        string[] splitPath = localFilepath.Split(['\\', '/'], System.StringSplitOptions.RemoveEmptyEntries);
        if (splitPath.Length < 2)
        {
            return false;
        }
        SceneKey scene = ToSceneKeyOrInvalid(splitPath[0]);
        if (scene == SceneKey.Invalid)
        {
            return false;
        }

        string filename = Path.GetFileName(fullFilepath);
        string loggedPath = $"{localPrefix}/{localFilepath}".Replace('\\', '/');
        Match matchAtlas = FileRegexAtlas.Match(filename);
        if (matchAtlas.Success)
        {
            logger.LogFileLoading($"Found custom atlas texture: {scene} ~ {filename}");
            Texture2D tex = LoadImage(fullFilepath, loggedPath, filename);
            if (tex == null)
            {
                return false;
            }
            if (!atlasByScene.TryGetValue(scene, out var atlas))
            {
                atlas = [];
                atlasByScene[scene] = atlas;
            }
            atlas[int.Parse(matchAtlas.Groups[1].Value)] = tex;
            return true;
        }

        Match matchSeperate = FileRegexSeperate.Match(filename);
        if (matchSeperate.Success)
        {
            logger.LogFileLoading($"Found custom seperate texture: {scene} ~ {filename}");
            Texture2D tex = LoadImage(fullFilepath, loggedPath, filename);
            if (tex == null)
            {
                return false;
            }
            if (!separateByScene.TryGetValue(scene, out var separate))
            {
                separate = [];
                separateByScene[scene] = separate;
            }
            separate[matchSeperate.Groups[1].Value] = tex;
            return true;
        }
        return false;
    }

    public int LoadCustomTexturePack(string configuredPath, string mixtapeRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            int filesLoaded = 0;
            foreach (var subpath in ResolveDefaultPackPathsByRegex(mixtapeRootPath))
            {
                filesLoaded += LoadCustomTexturePackResolved(subpath, Path.GetFileName(subpath));
            }
            return filesLoaded;
        }

        string resolvedPath = ResolveConfiguredPackPath(configuredPath, mixtapeRootPath);
        if (!Directory.Exists(resolvedPath))
        {
            logger.LogWarning($"Custom texture pack path does not exist: {resolvedPath}");
        }
        return LoadCustomTexturePackResolved(resolvedPath, configuredPath);
    }

    public int LoadCustomTexturePackResolved(string path, string localPrefix)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return 0;
        }

        int filesLoaded = 0;
        foreach (var fullFilepath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            if (CheckIsCustomTextureInPack(fullFilepath, path, localPrefix))
            {
                filesLoaded++;
            }
        }
        return filesLoaded;
    }

    public static List<string> ResolveDefaultPackPathsByRegex(string mixtapeRootPath)
    {
        List<string> paths = [];
        if (string.IsNullOrEmpty(mixtapeRootPath) || !Directory.Exists(mixtapeRootPath))
        {
            return paths;
        }
        foreach (var subpath in Directory.EnumerateDirectories(mixtapeRootPath))
        {
            if (IsCustomTextureDirectory(subpath))
            {
                paths.Add(subpath);
            }
        }
        return paths;
    }

    public static string ResolveConfiguredPackPath(string configuredPath, string mixtapeRootPath)
    {
        if (Path.IsPathRooted(configuredPath) || string.IsNullOrEmpty(mixtapeRootPath))
        {
            return configuredPath;
        }
        return Path.Combine(mixtapeRootPath, configuredPath);
    }

    public bool CheckIsCustomTextureInPack(string fullFilepath, string packRootPath, string localPrefix)
    {
        string localFilepath = fullFilepath.Substring(packRootPath.Length).TrimStart('\\', '/');
        string[] splitPath = localFilepath.Split(['\\', '/'], System.StringSplitOptions.RemoveEmptyEntries);
        if (splitPath.Length < 2)
        {
            return false;
        }
        SceneKey scene = ToSceneKeyOrInvalid(splitPath[0]);
        if (scene == SceneKey.Invalid)
        {
            return false;
        }

        string filename = Path.GetFileName(fullFilepath);
        string loggedPath = $"{localPrefix}/{localFilepath}".Replace('\\', '/');
        Match matchAtlas = FileRegexAtlas.Match(filename);
        if (matchAtlas.Success)
        {
            logger.LogFileLoading($"Found custom atlas texture: {scene} ~ {filename}");
            LoadCustomAtlasTexture(fullFilepath, loggedPath, filename, scene, int.Parse(matchAtlas.Groups[1].Value));
            return true;
        }
        Match matchSeperate = FileRegexSeperate.Match(filename);
        if (matchSeperate.Success)
        {
            logger.LogFileLoading($"Found custom seperate texture: {scene} ~ {filename}");
            LoadCustomSeperateTexture(fullFilepath, loggedPath, filename, scene, matchSeperate.Groups[1].Value);
            return true;
        }
        return false;
    }

    public bool ApplyTextureOverride(string qualifiedPath, string filePath)
    {
        if (!TryParseQualifiedTexturePath(qualifiedPath, out var scene, out var atlasIndex, out var spriteName, out var isAtlas))
        {
            logger.LogWarning($"Invalid texture override target: {qualifiedPath}");
            return false;
        }
        if (!File.Exists(filePath))
        {
            logger.LogWarning($"Texture override file does not exist: {filePath}");
            return false;
        }
        string filename = Path.GetFileName(filePath);
        Texture2D tex = LoadImage(filePath, filePath, filename);
        if (tex == null)
        {
            return false;
        }
        if (isAtlas)
        {
            if (!CustomAtlasTextures.ContainsKey(scene))
            {
                CustomAtlasTextures[scene] = [];
            }
            else if (CustomAtlasTextures[scene].ContainsKey(atlasIndex))
            {
                Object.Destroy(CustomAtlasTextures[scene][atlasIndex]);
            }
            CustomAtlasTextures[scene][atlasIndex] = tex;
            return true;
        }

        if (!CustomSeperateTextures.ContainsKey(scene))
        {
            CustomSeperateTextures[scene] = [];
            CustomSeperateTexturesNotInited[scene] = [];
        }
        else if (CustomSeperateTextures[scene].ContainsKey(spriteName))
        {
            Object.Destroy(CustomSeperateTextures[scene][spriteName]);
        }
        CustomSeperateTextures[scene][spriteName] = tex;
        CustomSeperateTexturesNotInited[scene][tex] = spriteName;
        return true;
    }

    public bool TryParseQualifiedTexturePath(string qualifiedPath, out SceneKey scene, out int atlasIndex, out string spriteName, out bool isAtlas)
    {
        scene = SceneKey.Invalid;
        atlasIndex = -1;
        spriteName = null;
        isAtlas = false;

        if (string.IsNullOrWhiteSpace(qualifiedPath))
        {
            return false;
        }
        string[] splitPath = qualifiedPath.Split(['\\', '/'], System.StringSplitOptions.RemoveEmptyEntries);
        if (splitPath.Length < 2)
        {
            return false;
        }
        scene = ToSceneKeyOrInvalid(splitPath[0]);
        if (scene == SceneKey.Invalid)
        {
            return false;
        }
        string textureName = splitPath[1];
        Match match = FileRegexAtlas.Match(textureName);
        if (match.Success)
        {
            isAtlas = true;
            atlasIndex = int.Parse(match.Groups[1].Value);
            return true;
        }

        spriteName = textureName;
        return true;
    }

    public void LoadCustomAtlasTexture(string path, string localPath, string filename, SceneKey scene, int spriteAtlasIndex)
    {
        Texture2D tex = LoadImage(path, localPath, filename);
        if (tex == null)
        {
            return;
        }
        if (!CustomAtlasTextures.ContainsKey(scene))
        {
            CustomAtlasTextures[scene] = new Dictionary<int, Texture2D>();
        }
        else if (CustomAtlasTextures[scene].ContainsKey(spriteAtlasIndex))
        {
            logger.LogWarning($"Duplicate atlas texture for {scene}, index {spriteAtlasIndex}");
            Object.Destroy(CustomAtlasTextures[scene][spriteAtlasIndex]);
        }
        CustomAtlasTextures[scene][spriteAtlasIndex] = tex;
    }

    public void LoadCustomSeperateTexture(string path, string localPath, string filename, SceneKey scene, string name)
    {
        Texture2D tex = LoadImage(path, localPath, filename);
        if (tex == null)
        {
            return;
        }
        if (!CustomSeperateTextures.ContainsKey(scene))
        {
            CustomSeperateTextures[scene] = new Dictionary<string, Texture2D>();
            CustomSeperateTexturesNotInited[scene] = new Dictionary<Texture2D, string>();
        }
        else if (CustomSeperateTextures[scene].ContainsKey(name))
        {
            logger.LogWarning($"Duplicate seperate texture for {scene}/{name}");
            Object.Destroy(CustomSeperateTextures[scene][name]);
        }
        CustomSeperateTextures[scene][name] = tex;
        CustomSeperateTexturesNotInited[scene][tex] = name;
    }

    public Texture2D LoadImage(string path, string localPath, string filename)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        if (!tex.LoadImage(bytes))
        {
            logger.LogWarning($"Couldn't load custom texture: {localPath} (is it a PNG/JPG?)");
            Object.Destroy(tex);
            return null;
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.name = filename;
        return tex;
    }

    public void UnloadCustomTextures(bool destroyLoadedTextures = true)
    {
        foreach (var q in SpriteMaps)
        {
            Texture2D tex = q.Key;
            var spriteMap = q.Value;
            logger.LogUnloading($"Unloading custom sprites: {tex.name}");
            foreach (var w in spriteMap)
            {
                var sprite = w.Value;
                if (!sprite.packed)
                {
                    Object.Destroy(sprite);
                }
            }
        }
        SpriteMaps.Clear();
        CustomSprites.Clear();
        OriginalSpriteByReplacement.Clear();
        CustomSpritesInited.Clear();
        if (destroyLoadedTextures)
        {
            foreach (var q in CustomAtlasTextures)
            {
                SceneKey scene = q.Key;
                var textures = q.Value;
                logger.LogUnloading($"Unloading custom atlas textures: {scene}");
                foreach (var w in textures)
                {
                    Object.Destroy(w.Value);
                }
            }

            foreach (var q in CustomSeperateTextures)
            {
                SceneKey scene = q.Key;
                var textures = q.Value;
                logger.LogUnloading($"Unloading custom seperate textures: {scene}");
                foreach (var w in textures)
                {
                    Object.Destroy(w.Value);
                }
            }
        }
        CustomAtlasTextures.Clear();
        CustomSeperateTextures.Clear();
        CustomSeperateTexturesNotInited.Clear();
        TextureMaps.Clear();
    }

    public void RestoreOriginalSpritesOnLoadedRoots(MixtapeLoaderCustom __instance)
    {
        if (__instance == null || OriginalSpriteByReplacement.Count == 0)
        {
            return;
        }

        var rootObjects = rootObjectsRef(__instance);
        if (rootObjects == null || rootObjects.Count == 0)
        {
            return;
        }

        foreach (var rootObj in rootObjects.Values)
        {
            if (rootObj == null)
            {
                continue;
            }
            var spriteRenderers = rootObj.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer == null || spriteRenderer.sprite == null)
                {
                    continue;
                }
                if (OriginalSpriteByReplacement.TryGetValue(spriteRenderer.sprite, out var original))
                {
                    spriteRenderer.sprite = original;
                    CustomSpriteSwapper script = spriteRenderer.gameObject.GetComponent<CustomSpriteSwapper>();
                    if (script != null)
                    {
                        script.last = original;
                    }
                }
            }
        }
    }

    public void InitCustomTextures(MixtapeLoaderCustom __instance, SceneKey sceneKey)
    {
        if (!(CustomAtlasTextures.ContainsKey(sceneKey) || CustomSeperateTextures.ContainsKey(sceneKey)))
        {
            return;
        }
        if (!CustomSpritesInited.Contains(sceneKey))
        {
            logger.LogInfo($"Initializing all custom sprites (invoked by {sceneKey})"); 
            InitCustomSprites(sceneKey);
        }
        logger.LogInfo($"Applying custom sprites: {sceneKey}");
        GameObject rootObj = rootObjectsRef(__instance)[sceneKey];
        InitCustomSpriteRenderers(rootObj, sceneKey);
    }

    public void InitCustomSprites(SceneKey scene)
    {
        // No way to obtain sprites only used by a certain scene, so all sprites have to be iterated through
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        HashSet<Sprite> nonPackedSprites = [];
        foreach (var sprite in sprites)
        {
            if (!CreateCustomSprite(sprite))
            {
                nonPackedSprites.Add(sprite);
            }
        }
        
        // CreateCustomSprites only covers base sprites included in a sprite atlas.
        // for base sprites not included in a sprite atlas (like a handful from molecano), the following will fix
        foreach (var a in CustomSeperateTexturesNotInited[scene])
        {
            Texture2D tex = a.Key;
            Sprite ogSprite = null;
            foreach (var sprite in nonPackedSprites)
            {
                if (sprite.texture.name == a.Value)
                {
                    SpriteMaps[sprite.texture] = new Dictionary<string, Sprite>();
                    CreateCustomSeperateSprite(sprite, tex, scene);

                    ogSprite = sprite;
                    break;
                }
            }
            if (ogSprite == null)
            {
                logger.LogWarning($"Found seperate texture that doesn't correspond to any game texture: {scene} ~ {tex.name}");
            }
            else
            {
                nonPackedSprites.Remove(ogSprite);
            }
        }

        CustomSeperateTexturesNotInited.Remove(scene);
        CustomSpritesInited.Add(scene);
    }

    public void InitCustomSpriteRenderers(GameObject rootObj, SceneKey scene)
    {
        var spriteRenderers = rootObj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var spriteRenderer in spriteRenderers)
        {
            ReplaceCustomSprite(spriteRenderer, scene);
            CustomSpriteSwapper script = spriteRenderer.gameObject.GetComponent<CustomSpriteSwapper>();
            if (script == null)
            {
                script = spriteRenderer.gameObject.AddComponent<CustomSpriteSwapper>();
            }
            script.last = spriteRenderer.sprite; // doing this in Awake() is insufficient
            script.scene = scene;
            script.textureManager = this;
        }
    }

    public void ReplaceCustomSprite(SpriteRenderer spriteRenderer, SceneKey scene)
    {
        if (spriteRenderer.sprite == null)
        {
            return;
        }
        if (SpriteMaps.ContainsKey(spriteRenderer.sprite.texture))
        {
            spriteRenderer.sprite = SpriteMaps[spriteRenderer.sprite.texture][spriteRenderer.sprite.name];
        }
    }

    public bool CreateCustomSprite(Sprite original)
    {
        if (!original.packed)
        {
            if (CustomSprites.Contains(original))
            {
                // sprite may need replacing by step 2 of InitCustomSprites
                return true;
            } 
            else
            {
                return false;
            }
        }
        var (scene, spriteAtlasIndex) = GetSceneAndSpriteAtlasIndex(original);
        if (scene == SceneKey.Invalid || !(CustomAtlasTextures.ContainsKey(scene) || CustomSeperateTextures.ContainsKey(scene)))
        {
            return true;
        }
        if (!SpriteMaps.ContainsKey(original.texture))
        {
            SpriteMaps[original.texture] = new Dictionary<string, Sprite>();
        }
        else if (SpriteMaps[original.texture].ContainsKey(original.name))
        {
            return true;
        }
        if (CustomSeperateTextures.ContainsKey(scene) && CustomSeperateTextures[scene].ContainsKey(original.name))
        {
            Texture2D tex = CustomSeperateTextures[scene][original.name];
            CreateCustomSeperateSprite(original, tex, scene);
            CustomSeperateTexturesNotInited[scene].Remove(tex);
        }
        else if (CustomAtlasTextures.ContainsKey(scene) && CustomAtlasTextures[scene].ContainsKey(spriteAtlasIndex))
        {
            Texture2D tex = CustomAtlasTextures[scene][spriteAtlasIndex];
            CreateCustomAtlasSprite(original, tex, scene);
        }
        else
        {
            SpriteMaps[original.texture][original.name] = original;
        }
        return true;
    }

    public void CreateCustomSeperateSprite(Sprite original, Texture2D tex, SceneKey scene)
    {
        logger.LogSeperateTextureSprites($" - {scene} - seperate - {original.name}");

        Sprite replacement = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            original.pivot / original.rect.size,
            original.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            original.border
        );
        replacement.name = original.name;

        // create new sprite with bounds adjusted if new texture larger than old texture
        Rect vertexBox = GetBoundingBox(original.vertices);
        vertexBox = GetWithDimensionsCentered(vertexBox, tex.width / original.pixelsPerUnit, tex.height / original.pixelsPerUnit);

        // apply new sprites bounds
        Vector3[] vertices = [
            new Vector3(vertexBox.xMin, vertexBox.yMax, 0),
            new Vector3(vertexBox.xMax, vertexBox.yMax, 0),
            new Vector3(vertexBox.xMin, vertexBox.yMin, 0),
            new Vector3(vertexBox.xMax, vertexBox.yMin, 0)
        ];
        Vector2[] uvs = [
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(0, 0),
            new Vector2(1, 0)
        ];
        NativeArray<Vector3> verticesNative = new NativeArray<Vector3>(vertices, Allocator.Temp);
        NativeArray<Vector2> uvsNative = new NativeArray<Vector2>(uvs, Allocator.Temp);
        replacement.SetVertexAttribute(VertexAttribute.Position, verticesNative);
        replacement.SetVertexAttribute(VertexAttribute.TexCoord0, uvsNative);
        verticesNative.Dispose();
        uvsNative.Dispose();

        SpriteMaps[original.texture][original.name] = replacement;
        CustomSprites.Add(replacement);
        OriginalSpriteByReplacement[replacement] = original;
    }

    public void CreateCustomAtlasSprite(Sprite original, Texture2D tex, SceneKey scene)
    {
        // Logs the creation of all atlas sprites seperately it takes soooo long that it seems like the game crashed
        logger.LogAtlasTextureSprites($" - {scene} - atlas - {original.name}");

        Sprite replacement = Sprite.Create(
            tex,
            original.rect,
            original.pivot / original.rect.size,
            original.pixelsPerUnit,
            0,
            SpriteMeshType.Tight,
            original.border
        );
        CopySpriteMesh(original, replacement); // Source of huge load time for custom atlas textures, should (somehow) be optimized
        replacement.name = original.name;

        SpriteMaps[original.texture][original.name] = replacement;
        CustomSprites.Add(replacement);
        OriginalSpriteByReplacement[replacement] = original;
    }

    public static void CopySpriteMesh(Sprite srcSprite, Sprite destSprite)
    {
        int vertexCount = srcSprite.GetVertexCount();
        destSprite.SetVertexCount(vertexCount);
        CopyVertexAttribute<Vector3>(srcSprite, destSprite, VertexAttribute.Position); // can this be done with OverrideGeometry? Would that help?
        CopyIndices(srcSprite, destSprite); // ditto this
        CopyVertexAttribute<Vector2>(srcSprite, destSprite, VertexAttribute.TexCoord0); // unavoidable
    }

    public static void CopyVertexAttribute<T>(Sprite srcSprite, Sprite destSprite, VertexAttribute attribute)
        where T : struct
    {
        NativeSlice<T> src = srcSprite.GetVertexAttribute<T>(attribute);
        NativeArray<T> dest = new NativeArray<T>(src.Length, Allocator.Temp);
        src.CopyTo(dest);
        destSprite.SetVertexAttribute(attribute, dest);
        dest.Dispose();
    }

    public static void CopyIndices(Sprite srcSprite, Sprite destSprite)
    {
        NativeArray<ushort> src = srcSprite.GetIndices();
        NativeArray<ushort> dest = new NativeArray<ushort>(src.Length, Allocator.Temp);
        src.CopyTo(dest);
        destSprite.SetIndices(dest);
        dest.Dispose();
    }

    public (SceneKey, int) GetSceneAndSpriteAtlasIndex(Sprite sprite)
    {
        if (!TextureMaps.ContainsKey(sprite.texture))
        {
            Match match = SceneAndSpriteAtlasIndexRegex.Match(sprite.texture.name);
            if (match.Success)
            {
                var index = int.Parse(match.Groups[1].Value);
                var scene = ToSceneKeyOrInvalid(match.Groups[2].Value);
                TextureMaps[sprite.texture] = (scene, index);
            }
            else
            {
                TextureMaps[sprite.texture] = (SceneKey.Invalid, -1);
            }
        }
        return TextureMaps[sprite.texture];
    }

    public static Rect GetBoundingBox(Vector2[] vertices)
    {
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        foreach (var vertex in vertices)
        {
            if (vertex.x < minX)
            {
                minX = vertex.x;
            }
            if (vertex.x > maxX)
            {
                maxX = vertex.x;
            }
            if (vertex.y < minY)
            {
                minY = vertex.y;
            }
            if (vertex.y > maxY)
            {
                maxY = vertex.y;
            }
        }
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public static Rect GetWithDimensionsCentered(Rect rect, float width, float height)
    {
        return new Rect(rect.x + (rect.width - width) / 2, rect.y + (rect.height - height) / 2, width, height);
    }
}
