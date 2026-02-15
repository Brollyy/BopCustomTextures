using BopCustomTextures.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace BopCustomTextures.Customs;

/// <summary>
/// Snapshot of all customTex-referenced asset paths for a mixtape.
/// Built once per loaded directory and reused during runtime switching.
/// </summary>
public class CustomTexReferenceFrame
{
    public readonly HashSet<string> TexturePackPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> ScenePackPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> TextureOverridePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> SceneOverridePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static CustomTexReferenceFrame Build(string mixtapeRootPath, ILogger logger)
    {
        var frame = new CustomTexReferenceFrame();
        string mixtapeJsonPath = Path.Combine(mixtapeRootPath, "mixtape.json");
        if (!File.Exists(mixtapeJsonPath))
        {
            return frame;
        }

        try
        {
            var json = JObject.Parse(File.ReadAllText(mixtapeJsonPath));
            if (json["entities"] is not JArray entities)
            {
                return frame;
            }

            foreach (var token in entities)
            {
                if (token is not JObject entity)
                {
                    continue;
                }
                string dataModel = entity["type"]?.ToString();
                if (string.IsNullOrWhiteSpace(dataModel) || !dataModel.StartsWith("customTex/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (entity["properties"] is not JObject properties)
                {
                    continue;
                }

                string eventName = dataModel.Substring("customTex/".Length);
                switch (eventName)
                {
                    case "set texture pack":
                        TryAddNonEmpty(properties["path"]?.ToString(), frame.TexturePackPaths);
                        break;
                    case "set scene mod pack":
                        TryAddNonEmpty(properties["path"]?.ToString(), frame.ScenePackPaths);
                        break;
                    case "set texture override":
                        TryAddNonEmpty(properties["path"]?.ToString(), frame.TextureOverridePaths);
                        break;
                    case "set scene mod override":
                        TryAddNonEmpty(properties["path"]?.ToString(), frame.SceneOverridePaths);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            logger.LogWarning($"Failed to build customTex reference frame from mixtape.json: {e.Message}");
        }

        return frame;
    }

    private static void TryAddNonEmpty(string value, HashSet<string> set)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            set.Add(value);
        }
    }
}
