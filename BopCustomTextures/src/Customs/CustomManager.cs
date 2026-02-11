using BopCustomTextures.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace BopCustomTextures.Customs;

/// <summary>
/// Manages all custom assets using specific manager classes.
/// </summary>
public class CustomManager : BaseCustomManager
{
    public string version;
    public uint release;
    public bool hasCustomAssets = false;

    public string lastPath;
    public DateTime lastModified;
    public bool readNecessary = true;

    public CustomSceneManager sceneManager;
    public CustomTextureManager textureManager;
    public CustomVariantNameManager variantManager;
    public CustomFileManager fileManager;

    /// <param name="logger">Plugin-specific logger.</param>
    /// <param name="tempPath">Where to temporarily save source files in custom mixtape while custom mixtape is loaded.</param>
    /// <param name="sceneModTemplate">Mixtape event template for applying scene mods.</param>
    /// <param name="textureVariantTemplates">Mixtape event templates concerning custom textures.</param>
    public CustomManager(ILogger logger, string tempPath, MixtapeEventTemplate sceneModTemplate, MixtapeEventTemplate[] textureVariantTemplates) : base(logger)
    {
        variantManager = new CustomVariantNameManager(logger);
        sceneManager = new CustomSceneManager(logger, variantManager, sceneModTemplate);
        textureManager = new CustomTextureManager(logger, variantManager, textureVariantTemplates);
        fileManager = new CustomFileManager(logger, tempPath);
    }

    public void ReadDirectory(string path, bool backup, bool upgrade)
    {
        if (!readNecessary)
        {
            return;
        }

        hasCustomAssets = GetMixtapeVersion(path);
        if (release > BopCustomTexturesPlugin.LowestRelease)
        {
            logger.LogOutdatedPlugin(
                $"Mixtape requires {MyPluginInfo.PLUGIN_GUID} v{version}+, " +
                $"but you are on v{MyPluginInfo.PLUGIN_VERSION}. You may have to update {MyPluginInfo.PLUGIN_GUID} to play properly."
            );
        }
        else if (release < BopCustomTexturesPlugin.LowestRelease && backup && upgrade)
        {
            logger.LogUpgradeMixtape(
                $"Mixtape was made for {MyPluginInfo.PLUGIN_GUID} v{version}, " +
                $"while you are on v{MyPluginInfo.PLUGIN_VERSION}. Save this mixtape in the editor to update its version!"
            );
        }

        int filesLoaded = 0;
        var subpaths = Directory.EnumerateDirectories(path);
        foreach (var subpath in subpaths)
        {
            if (CustomTextureManager.IsCustomTextureDirectory(subpath))
            {
                filesLoaded += textureManager.LocateCustomTextures(subpath, path);
                if (backup)
                {
                    fileManager.BackupDirectory(subpath, subpath.Substring(path.Length + 1));
                }
            }
        }
        foreach (var subpath in subpaths)
        {
            if (CustomSceneManager.IsCustomSceneDirectory(subpath))
            {
                filesLoaded += sceneManager.LocateCustomScenes(subpath, path, release);
                if (backup)
                {
                    fileManager.BackupDirectory(subpath, subpath.Substring(path.Length + 1));
                }
            }
        }
        if (filesLoaded > 0)
        {
            logger.LogInfo($"Loaded {filesLoaded} custom assets");
            if (!hasCustomAssets && backup)
            {
                logger.LogUpgradeMixtape(
                    "This mixtape with custom assets is missing a \"BopCustomTextues.json\" file specifying version. " +
                    "Save this mixtape in the editor to add a \"BopCustomTextures.json\" file automatically!"
                );
                hasCustomAssets = true;
            }
        }
        else
        {
            logger.LogInfo("No custom assets found");
        }
        sceneManager.UpdateEventTemplates();
        textureManager.UpdateEventTemplates();
    }

    public void WriteDirectory(string path, bool upgrade)
    {
        if (hasCustomAssets)
        {
            logger.LogInfo("Saving with custom files");
            fileManager.WriteDirectory(path);
            WriteMixtapeVersion(path, upgrade);
        };
    }

    public void ResetAll()
    {
        sceneManager.UnloadCustomScenes();
        textureManager.UnloadCustomTextures();
        variantManager.UnloadCustomTextureVariants();
        fileManager.DeleteTempDirectory();
        lastPath = null;
        lastModified = default;
        hasCustomAssets = false;
        readNecessary = true;
    }

    public void ResetIfNecessary(string path)
    {
        var modified = File.GetLastWriteTime(path);
        if (lastPath != path || lastModified != modified)
        {
            ResetAll();
        }
        else
        {
            logger.LogInfo("Avoided customs reload for reopened mixtape");
            readNecessary = false;
        }
        lastPath = path;
        lastModified = modified;
    }

    public void DeleteTempDirectory()
    {
        fileManager.DeleteTempDirectory();
    }

    public void InitScene(MixtapeLoaderCustom __instance, SceneKey sceneKey)
    {
        textureManager.InitCustomTextures(__instance, sceneKey);
        sceneManager.InitCustomScene(__instance, sceneKey);
    }

    public void Prepare(MixtapeLoaderCustom __instance)
    {
        foreach (var dict in rootObjectsRef(__instance))
        {
            sceneManager.InitCustomScene(__instance, dict.Key);
        }
        PrepareEvents(__instance, entitiesRef(__instance));
    }

    public void PrepareEvents(MixtapeLoaderCustom __instance, Entity[] entities)
    {
        sceneManager.PrepareEvents(__instance, entities);
        textureManager.PrepareEvents(__instance, entities);
    }

    public bool GetMixtapeVersion(string path)
    {
        string filePath = Path.Combine(path, $"{MyPluginInfo.PLUGIN_GUID}.json");
        if (!File.Exists(filePath))
        {
            version = BopCustomTexturesPlugin.LowestVersion;
            release = BopCustomTexturesPlugin.LowestRelease;
            return false;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            MemoryStream memStream = new MemoryStream(bytes);
            using StreamReader reader = new StreamReader(memStream);
            using JsonTextReader jsonReader = new JsonTextReader(reader);
            var jobj = JObject.Load(jsonReader);

            if (jobj.TryGetValue("release", out var jrelease))
            {
                if (jrelease.Type == JTokenType.Integer)
                {
                    release = (uint)jrelease;
                } 
                else
                {
                    logger.LogWarning($"Release is a {jrelease.Type} when it should be an int, will treat as latest.");
                    release = BopCustomTexturesPlugin.LowestRelease;
                }
            } 
            else
            {
                logger.LogWarning("Version data missing release, will treat as latest.");
                release = BopCustomTexturesPlugin.LowestRelease;
            }
            if (jobj.TryGetValue("version", out var jversion))
            {
                if (jversion.Type == JTokenType.String)
                {
                    version = (string)jversion;
                }
                else
                {
                    logger.LogWarning($"Version is a {jversion.Type} when it should be an int, will treat as latest.");
                    version = BopCustomTexturesPlugin.LowestVersion;
                }
            }
            else
            {
                logger.LogWarning("Version data missing version, will treat as latest.");
                version = BopCustomTexturesPlugin.LowestVersion;
            }

        }
        catch (JsonReaderException e)
        {
            logger.LogError($"Error reading verison data, will treat as latest: {e}");
            version = BopCustomTexturesPlugin.LowestVersion;
            release = BopCustomTexturesPlugin.LowestRelease;
        }
        
        return true;
    }

    public void WriteMixtapeVersion(string path, bool upgrade)
    {
        var jobj = new JObject();
        jobj["version"] = new JValue(upgrade ? BopCustomTexturesPlugin.LowestVersion : version);
        jobj["release"] = new JValue(upgrade ? BopCustomTexturesPlugin.LowestRelease : release);

        try
        {
            using StreamWriter outputFile = new StreamWriter(Path.Combine(path, $"{MyPluginInfo.PLUGIN_GUID}.json"));
            outputFile.Write(JsonConvert.SerializeObject(jobj));
        } 
        catch (Exception e)
        {
            logger.LogError($"Error writing version data: {e}");
        }
    }
}
