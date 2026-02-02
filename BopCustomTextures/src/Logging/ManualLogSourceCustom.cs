using BepInEx.Logging;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BopCustomTextures.Logging;

/// <summary>
/// Wrapper class for BepInEx's ManualLogSource with special methods for log messages with configurable log levels.
/// </summary>
/// <param name="logger">Internal BepInEx ManualLogSource</param>
/// <param name="pluginName">Plugin name to display when using LogEditor</param>
/// <param name="logFileLoading">Log level for file loading messages</param>
/// <param name="logUnloading">Log level for asset unloading messages</param>
/// <param name="logSeperateTextureSprites">Log level for sprite creation from seperate textures</param>
/// <param name="logAtlasTextureSprites">Log level for sprite creation from atlas textures</param>
public class ManualLogSourceCustom(ManualLogSource logger, string pluginName, LogLevel logFileLoading, LogLevel logUnloading, LogLevel logSeperateTextureSprites, LogLevel logAtlasTextureSprites) : ILogger
{
    private readonly ManualLogSource logger = logger;
    private readonly string pluginName = pluginName;
    private readonly LogLevel logFileLoading = logFileLoading;
    private readonly LogLevel logUnloading = logUnloading;
    private readonly LogLevel logSeperateTextureSprites = logSeperateTextureSprites;
    private readonly LogLevel logAtlasTextureSprites = logAtlasTextureSprites;

    public void LogFileLoading(object data)
    {
        logger.Log(logFileLoading, data);
    }
    public void LogUnloading(object data)
    {
        logger.Log(logUnloading, data);
    }
    public void LogSeperateTextureSprites(object data)
    {
        logger.Log(logSeperateTextureSprites, data);
    }
    public void LogAtlasTextureSprites(object data)
    {
        logger.Log(logAtlasTextureSprites, data);
    }

    public void LogEditor(LogLevel level, object data)
    {
        logger.Log(level, data);
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MixtapeEditor")
        {
            return;
        }
        
        var objs = scene.GetRootGameObjects();
        var obj = objs.FirstOrDefault(obj => obj.name == "ErrorCanvas");
        if (obj == null)
        {
            return;
        }
        obj.SetActive(true);
        obj.GetComponentInChildren<TMP_Text>().text = $"[{pluginName}] {data}";
    }
    public void LogEditorError(object data)
    {
        LogEditor(LogLevel.Error, data);
    }
    public void LogEditorWarning(object data)
    {
        LogEditor(LogLevel.Warning, data);
    }


    public void Log(LogLevel level, object data)
    {
        logger.Log(level, data);
    }

    public void LogFatal(object data)
    {
        logger.LogFatal(data);
    }

    public void LogError(object data)
    {
        logger.LogError(data);
    }

    public void LogWarning(object data)
    {
        logger.LogWarning(data);
    }

    public void LogMessage(object data)
    {
        logger.LogMessage(data);
    }

    public void LogInfo(object data)
    {
        logger.LogInfo(data);
    }

    public void LogDebug(object data)
    {
        logger.LogDebug(data);
    }
}