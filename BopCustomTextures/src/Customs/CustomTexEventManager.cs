using System;
using BopCustomTextures.Logging;

namespace BopCustomTextures.Customs;

/// <summary>
/// Manages scheduling and execution of customTex/* mixtape events.
/// </summary>
/// <param name="logger">Plugin-specific logger</param>
public class CustomTexEventManager(ILogger logger) : BaseCustomManager(logger)
{
    public readonly object scheduledCustomTexHandle = new object();
    public Scheduler schedulerWithScheduledCustomTexEvents;

    public void ScheduleCustomTexEvents(MixtapeLoaderCustom instance, CustomManager manager)
    {
        UnscheduleCustomTexEvents();
        manager.ResetCustomTexEventRuntimeState(instance);

        var scheduler = schedulerRef(instance);
        if (scheduler == null)
        {
            return;
        }
        var entities = entitiesRef(instance);
        if (entities == null || entities.Length == 0)
        {
            return;
        }

        int count = 0;
        schedulerWithScheduledCustomTexEvents = scheduler;
        foreach (var entity in entities)
        {
            if (entity?.dataModel == null || !entity.dataModel.StartsWith("customTex/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var eventEntity = entity;
            scheduler.Schedule((double)eventEntity.beat, () => HandleCustomTexEvent(instance, manager, eventEntity), scheduledCustomTexHandle);
            count++;
        }
        if (count > 0)
        {
            logger.LogInfo($"Scheduled {count} customTex events");
        }
    }

    public void UnscheduleCustomTexEvents()
    {
        if (schedulerWithScheduledCustomTexEvents == null)
        {
            return;
        }
        try
        {
            schedulerWithScheduledCustomTexEvents.UnscheduleAll(scheduledCustomTexHandle);
        }
        catch { }
        schedulerWithScheduledCustomTexEvents = null;
    }

    public void HandleCustomTexEvent(MixtapeLoaderCustom instance, CustomManager manager, Entity entity)
    {
        if (entity?.dataModel == null)
        {
            return;
        }

        string eventName = entity.dataModel.Substring("customTex/".Length);
        bool changed = false;
        bool sceneStateChanged = false;
        bool textureStateChanged = false;
        switch (eventName)
        {
            case "toggle custom textures":
                if (!TryGetBool(entity, "enabled", out var customTexturesEnabled))
                {
                    logger.LogWarning("customTex/toggleCustomTextures requires boolean property \"enabled\"");
                    return;
                }
                changed = manager.customTexturesEnabled != customTexturesEnabled;
                manager.customTexturesEnabled = customTexturesEnabled;
                textureStateChanged = changed;
                break;
            case "set texture pack":
                if (!TryGetString(entity, "path", out var texturePackPath))
                {
                    logger.LogWarning("customTex/setTexturePack requires string property \"path\"");
                    return;
                }
                changed = !string.Equals(manager.activeTexturePackPath, texturePackPath, StringComparison.Ordinal);
                manager.activeTexturePackPath = texturePackPath;
                textureStateChanged = changed;
                break;
            case "set texture override":
                if (!TryGetString(entity, "qualifiedPath", out var textureTarget) ||
                    !TryGetString(entity, "path", out var texturePath))
                {
                    logger.LogWarning("customTex/setTextureOverride requires string properties \"qualifiedPath\" and \"path\"");
                    return;
                }
                changed = !manager.textureOverrides.TryGetValue(textureTarget, out var currentTexturePath) ||
                    !string.Equals(currentTexturePath, texturePath, StringComparison.Ordinal);
                manager.textureOverrides[textureTarget] = texturePath;
                textureStateChanged = changed;
                break;
            case "clear texture override":
                if (!TryGetString(entity, "qualifiedPath", out var clearTextureTarget))
                {
                    logger.LogWarning("customTex/clearTextureOverride requires string property \"qualifiedPath\"");
                    return;
                }
                changed = manager.textureOverrides.Remove(clearTextureTarget);
                textureStateChanged = changed;
                break;
            case "set scene mod pack":
                if (!TryGetString(entity, "path", out var scenePackPath))
                {
                    logger.LogWarning("customTex/setSceneModPack requires string property \"path\"");
                    return;
                }
                changed = !string.Equals(manager.activeSceneModPackPath, scenePackPath, StringComparison.Ordinal);
                manager.activeSceneModPackPath = scenePackPath;
                sceneStateChanged = changed;
                break;
            case "set scene mod override":
                if (!TryGetString(entity, "scene", out var sceneTarget) ||
                    !TryGetString(entity, "path", out var scenePath))
                {
                    logger.LogWarning("customTex/setSceneModOverride requires string properties \"scene\" and \"path\"");
                    return;
                }
                changed = !manager.sceneModOverrides.TryGetValue(sceneTarget, out var currentScenePath) ||
                    !string.Equals(currentScenePath, scenePath, StringComparison.Ordinal);
                manager.sceneModOverrides[sceneTarget] = scenePath;
                sceneStateChanged = changed;
                break;
            case "clear scene mod override":
                if (!TryGetString(entity, "scene", out var clearSceneTarget))
                {
                    logger.LogWarning("customTex/clearSceneModOverride requires string property \"scene\"");
                    return;
                }
                changed = manager.sceneModOverrides.Remove(clearSceneTarget);
                sceneStateChanged = changed;
                break;
            default:
                logger.LogWarning($"Unknown customTex event: {entity.dataModel}");
                return;
        }

        if (!changed)
        {
            logger.LogInfo($"Skipping customTex event (no state change): {entity.dataModel} @ beat {entity.beat}");
            return;
        }

        manager.customTexRuntimeDirty = true;
        logger.LogInfo($"Applying customTex event: {entity.dataModel} @ beat {entity.beat}");
        manager.RebuildCustomAssets(
            instance,
            textureStateChanged: textureStateChanged,
            sceneStateChanged: sceneStateChanged
        );
    }

    public static bool TryGetString(Entity entity, string key, out string value)
    {
        value = null;
        if (entity.dynamicData == null || !entity.dynamicData.TryGetValue(key, out var valueObj) || valueObj == null)
        {
            return false;
        }
        value = valueObj.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool TryGetBool(Entity entity, string key, out bool value)
    {
        value = false;
        if (entity.dynamicData == null || !entity.dynamicData.TryGetValue(key, out var valueObj) || valueObj == null)
        {
            return false;
        }
        if (valueObj is bool boolVal)
        {
            value = boolVal;
            return true;
        }
        if (valueObj is int intVal)
        {
            value = intVal != 0;
            return true;
        }
        if (bool.TryParse(valueObj.ToString(), out var parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }
}
