using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ILogger = BopCustomTextures.Logging.ILogger;

namespace BopCustomTextures.Customs;

/// <summary>
/// Manages scene mods, including loading them from the source file and applying them when the mixtape is played.
/// </summary>
/// <param name="logger">Plugin-specific logger</param>
public class CustomSceneManager(ILogger logger) : BaseCustomManager(logger)
{
    public CustomJsonInitializer jsonInitializer = new CustomJsonInitializer(logger);
    public readonly Dictionary<SceneKey, JObject> CustomScenes = [];
    public readonly Dictionary<string, Dictionary<SceneKey, JObject>> PreloadedScenesByPackPath = new Dictionary<string, Dictionary<SceneKey, JObject>>(System.StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, JObject> PreloadedSceneOverridesByPath = new Dictionary<string, JObject>(System.StringComparer.OrdinalIgnoreCase);
    public readonly List<string> DefaultPackPaths = [];
    public static readonly Regex PathRegex = new Regex(@"[\\/](?:level|scene)s?$", RegexOptions.IgnoreCase);
    public static readonly Regex FileRegex = new Regex(@"(\w+).json$", RegexOptions.IgnoreCase);

    public static bool IsCustomSceneDirectory(string path)
    {
        return PathRegex.IsMatch(path);
    }

    public int PreloadReferenceFrame(CustomTexReferenceFrame frame, string mixtapeRootPath)
    {
        ClearPreloadedSceneCaches();

        DefaultPackPaths.Clear();
        foreach (var fullPath in ResolveDefaultPackPathsByRegex(mixtapeRootPath))
        {
            DefaultPackPaths.Add(Path.GetFileName(fullPath));
        }

        var packPathsToLoad = new HashSet<string>(DefaultPackPaths, System.StringComparer.OrdinalIgnoreCase);
        foreach (var configuredPath in frame.ScenePackPaths)
        {
            packPathsToLoad.Add(configuredPath);
        }

        int filesLoaded = 0;
        foreach (var configuredPath in packPathsToLoad)
        {
            filesLoaded += PreloadScenePack(configuredPath, mixtapeRootPath);
        }

        foreach (var overridePath in frame.SceneOverridePaths)
        {
            string resolvedPath = ResolveConfiguredPackPath(overridePath, mixtapeRootPath);
            if (!File.Exists(resolvedPath))
            {
                logger.LogWarning($"Scene override file does not exist: {resolvedPath}");
                continue;
            }

            if (TryLoadSceneJson(resolvedPath, out var sceneJson))
            {
                PreloadedSceneOverridesByPath[overridePath] = sceneJson;
                filesLoaded++;
            }
        }

        return filesLoaded;
    }

    public int ApplyRuntimeSelection(string activePackPath, Dictionary<string, string> sceneOverrides, string mixtapeRootPath)
    {
        CustomScenes.Clear();

        var packPaths = string.IsNullOrWhiteSpace(activePackPath)
            ? DefaultPackPaths
            : [activePackPath];

        foreach (var packPath in packPaths)
        {
            if (string.IsNullOrWhiteSpace(packPath))
            {
                continue;
            }
            if (!PreloadedScenesByPackPath.TryGetValue(packPath, out var scenes))
            {
                string resolvedPath = ResolveConfiguredPackPath(packPath, mixtapeRootPath);
                logger.LogWarning($"Custom scene mod pack path does not exist: {resolvedPath}");
                continue;
            }
            foreach (var scene in scenes)
            {
                CustomScenes[scene.Key] = (JObject)scene.Value.DeepClone();
            }
        }

        foreach (var sceneOverride in sceneOverrides)
        {
            SceneKey scene = ToSceneKeyOrInvalid(sceneOverride.Key);
            if (scene == SceneKey.Invalid)
            {
                logger.LogWarning($"Invalid scene override target: {sceneOverride.Key}");
                continue;
            }
            if (!PreloadedSceneOverridesByPath.TryGetValue(sceneOverride.Value, out var overrideJson))
            {
                string resolvedPath = ResolveConfiguredPackPath(sceneOverride.Value, mixtapeRootPath);
                logger.LogWarning($"Scene override file does not exist: {resolvedPath}");
                continue;
            }
            CustomScenes[scene] = (JObject)overrideJson.DeepClone();
        }

        return CustomScenes.Count;
    }

    public int PreloadScenePack(string configuredPath, string mixtapeRootPath)
    {
        if (PreloadedScenesByPackPath.ContainsKey(configuredPath))
        {
            return 0;
        }

        string resolvedPath = ResolveConfiguredPackPath(configuredPath, mixtapeRootPath);
        if (!Directory.Exists(resolvedPath))
        {
            logger.LogWarning($"Custom scene mod pack path does not exist: {resolvedPath}");
            PreloadedScenesByPackPath[configuredPath] = [];
            return 0;
        }

        var scenes = new Dictionary<SceneKey, JObject>();
        int filesLoaded = 0;
        foreach (var fullFilepath in Directory.EnumerateFiles(resolvedPath, "*.json", SearchOption.AllDirectories))
        {
            if (LoadCustomSceneIntoCollection(fullFilepath, resolvedPath, configuredPath, scenes))
            {
                filesLoaded++;
            }
        }
        PreloadedScenesByPackPath[configuredPath] = scenes;
        return filesLoaded;
    }

    public void ClearPreloadedSceneCaches()
    {
        PreloadedScenesByPackPath.Clear();
        PreloadedSceneOverridesByPath.Clear();
        DefaultPackPaths.Clear();
    }

    public bool LoadCustomSceneIntoCollection(
        string path,
        string packRootPath,
        string localPrefix,
        Dictionary<SceneKey, JObject> scenes
    )
    {
        string localPath = path.Substring(packRootPath.Length).TrimStart('\\', '/');
        string filename = Path.GetFileName(localPath);
        Match match = FileRegex.Match(filename);
        if (!match.Success)
        {
            return false;
        }
        SceneKey scene = ToSceneKeyOrInvalid(match.Groups[1].Value);
        if (scene == SceneKey.Invalid)
        {
            return false;
        }

        logger.LogFileLoading($"Found custom scene: {scene} ({localPrefix}/{localPath})");
        if (!TryLoadSceneJson(path, out var json))
        {
            return false;
        }
        scenes[scene] = json;
        return true;
    }

    public bool TryLoadSceneJson(string path, out JObject json)
    {
        json = null;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            MemoryStream memStream = new MemoryStream(bytes);
            using StreamReader reader = new StreamReader(memStream);
            using JsonTextReader jsonReader = new JsonTextReader(reader);
            json = JObject.Load(jsonReader);
            return true;
        }
        catch (JsonReaderException e)
        {
            logger.LogError(e);
            return false;
        }
    }

    public int LoadCustomScenePack(string configuredPath, string mixtapeRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            int filesLoaded = 0;
            foreach (var subpath in ResolveDefaultPackPathsByRegex(mixtapeRootPath))
            {
                filesLoaded += LoadCustomScenePackResolved(subpath, Path.GetFileName(subpath));
            }
            return filesLoaded;
        }

        string resolvedPath = ResolveConfiguredPackPath(configuredPath, mixtapeRootPath);
        if (!Directory.Exists(resolvedPath))
        {
            logger.LogWarning($"Custom scene mod pack path does not exist: {resolvedPath}");
        }
        return LoadCustomScenePackResolved(resolvedPath, configuredPath);
    }

    public int LoadCustomScenePackResolved(string path, string localPrefix)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return 0;
        }

        int filesLoaded = 0;
        foreach (var fullFilepath in Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories))
        {
            if (CheckIsCustomSceneInPack(fullFilepath, path, localPrefix))
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
            if (IsCustomSceneDirectory(subpath))
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

    public bool CheckIsCustomSceneInPack(string path, string packRootPath, string localPrefix)
    {
        string localPath = path.Substring(packRootPath.Length).TrimStart('\\', '/');
        string filename = Path.GetFileName(localPath);
        Match match = FileRegex.Match(filename);
        if (!match.Success)
        {
            return false;
        }
        SceneKey scene = ToSceneKeyOrInvalid(match.Groups[1].Value);
        if (scene == SceneKey.Invalid)
        {
            return false;
        }

        logger.LogFileLoading($"Found custom scene: {scene} ({localPrefix}/{localPath})");
        LoadCustomScene(path, $"{localPrefix}/{localPath}".Replace('\\', '/'), scene);
        return true;
    }

    public bool ApplySceneOverride(string sceneName, string filePath)
    {
        SceneKey scene = ToSceneKeyOrInvalid(sceneName);
        if (scene == SceneKey.Invalid)
        {
            logger.LogWarning($"Invalid scene override target: {sceneName}");
            return false;
        }
        if (!File.Exists(filePath))
        {
            logger.LogWarning($"Scene override file does not exist: {filePath}");
            return false;
        }
        LoadCustomScene(filePath, filePath, scene);
        return true;
    }

    public void LoadCustomScene(string path, string localPath, SceneKey scene)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            MemoryStream memStream = new MemoryStream(bytes);
            using StreamReader reader = new StreamReader(memStream);
            using JsonTextReader jsonReader = new JsonTextReader(reader);
            CustomScenes[scene] = JObject.Load(jsonReader);
        }
        catch (JsonReaderException e)
        {
            logger.LogError(e);
            CustomScenes.Remove(scene);
        }

    }

    public void UnloadCustomScenes()
    {
        if (CustomScenes.Count > 0)
        {
            logger.LogUnloading("Unloading all custom scenes");
            CustomScenes.Clear();
        }
    }

    public void InitCustomScene(MixtapeLoaderCustom __instance, SceneKey sceneKey)
    {
        if (!CustomScenes.ContainsKey(sceneKey))
        {
            return;
        }
        logger.LogInfo($"Applying custom scene: {sceneKey}");
        GameObject rootObj = rootObjectsRef(__instance)[sceneKey];
        JObject jall = CustomScenes[sceneKey];
        foreach (KeyValuePair<string, JToken> dict in jall)
        {
            jsonInitializer.InitCustomGameObject(dict.Value, dict.Key, rootObj);
        }
    }
}
