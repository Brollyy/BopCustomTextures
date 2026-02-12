using BopCustomTextures.Logging;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BopCustomTextures.Customs;

/// <summary>
/// Manager of mapping custom texture variant external names to internal indices.
/// Needed so CustomTextureManager and CustomJsoninitializer can work together.
/// </summary>
/// <param name="logger">Plugin-specific logger.</param>
public class CustomVariantNameManager(ILogger logger) : BaseCustomManager(logger)
{
    public readonly Dictionary<SceneKey, Dictionary<string, int>> VariantMaps = [];
    public static readonly Regex VariantRegex = new Regex(@"(?:^(\w+)[^\w\\/]+|^)(\w+)$");
    
    public void UnloadCustomTextureVariants()
    {
        VariantMaps.Clear();
    }

    public bool TryGetVariant(SceneKey scene, string name, out int variant)
    {
        if (string.IsNullOrEmpty(name))
        {
            variant = 0;
            return true;
        }
        var match = VariantRegex.Match(name);
        if (!match.Success)
        {
            logger.LogError($"Variant \"{name}\" couldn't be parsed");
            variant = -2;
            return false;
        }
        if (!string.IsNullOrEmpty(match.Groups[1].Value))
        {
            scene = ToSceneKeyOrInvalid(match.Groups[1].Value);
            if (scene == SceneKey.Invalid)
            {
                logger.LogError($"Variant \"{name}\" has an invalid scene \"{match.Groups[1].Value}\"");
                variant = -3;
                return false;
            }
        }
        if (VariantMaps.TryGetValue(scene, out var variantMap) &&
            variantMap.TryGetValue(match.Groups[2].Value, out variant))
        {
            return true;
        }
        logger.LogError($"Variant \"{match.Groups[2].Value}\" doesn't exist in scene {scene}");
        variant = -1;
        return false;
    }

    public int GetOrAddVariant(SceneKey scene, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 0;
        }
        int value;
        if (!VariantMaps.TryGetValue(scene, out var variantMap))
        {
            variantMap = [];
            VariantMaps[scene] = variantMap;
        }
        else if (variantMap.TryGetValue(name, out value))
        {
            return value;
        }
        value = variantMap.Count + 1;
        variantMap[name] = value;
        return value;
    }
}
