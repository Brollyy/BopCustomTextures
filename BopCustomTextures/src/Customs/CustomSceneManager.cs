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
    public readonly Dictionary<SceneKey, Dictionary<string, JObject>> CustomScenes = [];
    public static readonly Regex PathRegex = new Regex(@"[\\/](?:level|scene)s?$", RegexOptions.IgnoreCase);
    public static readonly Regex FileRegex = new Regex(@"(\w+).json$", RegexOptions.IgnoreCase);

    public static bool IsCustomSceneDirectory(string path)
    {
        return PathRegex.IsMatch(path);
    }

    public int LocateCustomScenes(string path, string parentPath, uint release)
    {
        int filesLoaded = 0;
        var fullFilepaths = Directory.EnumerateFiles(path);
        foreach (var fullFilepath in fullFilepaths)
        {
            var localFilepath = fullFilepath.Substring(parentPath.Length + 1);
            if (CheckIsCustomScene(fullFilepath, localFilepath, release))
            {
                filesLoaded++;
            }
        }
        return filesLoaded;
    }

    public bool CheckIsCustomScene(string path, string localPath, uint release)
    {
        Match match = FileRegex.Match(localPath);
        if (match.Success)
        {
            SceneKey scene = ToSceneKeyOrInvalid(match.Groups[1].Value);
            if (scene != SceneKey.Invalid)
            {
                logger.LogFileLoading($"Found custom scene: {scene}");

                LoadCustomScene(path, scene, release);
                return true;
            }
        }
        return false;
    }

    public void LoadCustomScene(string path, SceneKey scene, uint release)
    {
        JObject jobj;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            MemoryStream memStream = new MemoryStream(bytes);
            using StreamReader reader = new StreamReader(memStream);
            using JsonTextReader jsonReader = new JsonTextReader(reader);

            jobj = JObject.Load(jsonReader);
        }
        catch (JsonReaderException e)
        {
            logger.LogError(e);
            CustomScenes.Remove(scene);
            return;
        }

        CustomScenes[scene] = new Dictionary<string, JObject>();
        if (release < 2)
        {
            CustomScenes[scene][""] = jobj;
        } 
        else
        {
            bool isSimple = false;
            if (jobj.TryGetValue("init", out var jinit))
            {
                isSimple = true;
                if (jinit.Type == JTokenType.Object)
                {
                    CustomScenes[scene][""] = (JObject)jinit;
                }
                else
                {
                    logger.LogWarning($"\"init\" in {scene} is a {jinit.Type} when it should be an Object.");
                }
            }
            if (jobj.TryGetValue("events", out var jevents))
            {
                isSimple = true;
                if (jinit.Type == JTokenType.Object)
                {
                    foreach (KeyValuePair<string, JToken> dict in (JObject)jevents)
                    {
                        if (dict.Value.Type == JTokenType.Object)
                        {
                            CustomScenes[scene][dict.Key] = (JObject)dict.Value;
                        }
                        else
                        {
                            logger.LogWarning($"Event \"{dict.Key}\" in {scene} is a {jinit.Type} when it should be an Object.");
                        }
                    }
                }
                else
                {
                    logger.LogWarning($"\"events\" in {scene} is a {jinit.Type} when it should be an Object.");
                }
            }
            if (!isSimple)
            {
                CustomScenes[scene][""] = jobj;
            }
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
        JObject jall = CustomScenes[sceneKey][""];

        jsonInitializer.InitCustomGameObject(jall, rootObj);
    }
}