namespace BopCustomTextures.Logging;

/// <summary>
/// Logging interface specific to BopCustomTextures. Includes special methods for log messages with configurable log levels.
/// </summary>
public interface ILogger
{
    public void LogFileLoading(object data);
    public void LogUnloading(object data);
    public void LogSeperateTextureSprites(object data);
    public void LogAtlasTextureSprites(object data);

    public void LogEditorError(object data);
    public void LogEditorWarning(object data);

    public void LogFatal(object data);
    public void LogError(object data);
    public void LogWarning(object data);
    public void LogMessage(object data);
    public void LogInfo(object data);
    public void LogDebug(object data);
}
