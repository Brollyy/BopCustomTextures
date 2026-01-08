namespace BopCustomTextures.Logging;
public interface ILogger
{
    public void LogFileLoading(object data);
    public void LogUnloading(object data);
    public void LogSeperateTextureSprites(object data);
    public void LogAtlasTextureSprites(object data);
    public void LogFatal(object data);
    public void LogError(object data);
    public void LogWarning(object data);
    public void LogMessage(object data);
    public void LogInfo(object data);
    public void LogDebug(object data);
}
