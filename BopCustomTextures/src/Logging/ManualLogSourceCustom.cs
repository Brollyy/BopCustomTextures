using BepInEx.Logging;

namespace BopCustomTextures.Logging;

public class ManualLogSourceCustom(ManualLogSource logger, LogLevel logFileLoading, LogLevel logUnloading, LogLevel logSeperateTextureSprites, LogLevel logAtlasTextureSprites) : ILogger
{
    private ManualLogSource logger = logger;
    private LogLevel logFileLoading = logFileLoading;
    private LogLevel logUnloading = logUnloading;
    private LogLevel logSeperateTextureSprites = logSeperateTextureSprites;
    private LogLevel logAtlasTextureSprites = logAtlasTextureSprites;

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