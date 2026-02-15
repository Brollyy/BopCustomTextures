using System.Collections.Generic;
using System.Globalization;

namespace BopCustomTextures.Customs;

public static class CustomTexMixtapeEvents
{
    public const string Category = "customTex";

    public static readonly MixtapeEventTemplate ToggleCustomTextures = new MixtapeEventTemplate
    {
        dataModel = "customTex/toggle custom textures",
        length = 0.5f,
        resizable = false,
        ephemeral = false,
        properties = new Dictionary<string, object>
        {
            ["enabled"] = true
        }
    };

    public static readonly MixtapeEventTemplate SetTexturePack = new MixtapeEventTemplate
    {
        dataModel = "customTex/set texture pack",
        length = 0.5f,
        resizable = false,
        ephemeral = false,
        properties = new Dictionary<string, object>
        {
            ["path"] = "textures"
        }
    };

    public static readonly MixtapeEventTemplate SetTextureOverride = new MixtapeEventTemplate
    {
        dataModel = "customTex/set texture override",
        length = 0.5f,
        resizable = false,
        ephemeral = false,
        properties = new Dictionary<string, object>
        {
            ["qualifiedPath"] = "",
            ["path"] = ""
        }
    };

    public static readonly MixtapeEventTemplate ClearTextureOverride = new MixtapeEventTemplate
    {
        dataModel = "customTex/clear texture override",
        length = 0.5f,
        resizable = false,
        ephemeral = false,
        properties = new Dictionary<string, object>
        {
            ["qualifiedPath"] = ""
        }
    };

    public static readonly MixtapeEventTemplate SetSceneModPack = new MixtapeEventTemplate
    {
        dataModel = "customTex/set scene mod pack",
        length = 0.5f,
        resizable = false,
        ephemeral = false,
        properties = new Dictionary<string, object>
        {
            ["path"] = "levels"
        }
    };

    public static readonly MixtapeEventTemplate SetSceneModOverride = new MixtapeEventTemplate
    {
        dataModel = "customTex/set scene mod override",
        length = 0.5f,
        resizable = false,
        ephemeral = false,
        properties = new Dictionary<string, object>
        {
            ["scene"] = "",
            ["path"] = ""
        }
    };

    public static readonly MixtapeEventTemplate ClearSceneModOverride = new MixtapeEventTemplate
    {
        dataModel = "customTex/clear scene mod override",
        length = 0.5f,
        resizable = false,
        ephemeral = false,
        properties = new Dictionary<string, object>
        {
            ["scene"] = ""
        }
    };

    public static readonly MixtapeEventTemplate[] Templates =
    [
        ToggleCustomTextures,
        SetTexturePack,
        SetTextureOverride,
        ClearTextureOverride,
        SetSceneModPack,
        SetSceneModOverride,
        ClearSceneModOverride
    ];

    public static readonly HashSet<string> EventNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "toggle custom textures",
        "set texture pack",
        "set texture override",
        "clear texture override",
        "set scene mod pack",
        "set scene mod override",
        "clear scene mod override"
    };

    public static bool IsCustomTexEventName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && EventNames.Contains(name);
    }

    public static string ToDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
    }

    public static void InjectGames(List<string> games)
    {
        if (games == null || games.Contains(Category))
        {
            return;
        }
        games.Insert(0, Category);
    }

    public static void InjectGameEvents(Dictionary<string, List<MixtapeEventTemplate>> gameEvents)
    {
        if (gameEvents == null)
        {
            return;
        }

        if (!gameEvents.TryGetValue(Category, out var events))
        {
            events = [];
        }
        foreach (var template in Templates)
        {
            if (!events.Exists(x => x.dataModel == template.dataModel))
            {
                events.Add(template);
            }
        }

        // The editor level list is rendered from gameEvents.Keys, so dictionary insertion order controls category order.
        // Keep original order and insert customTex right after accessibility.
        var reordered = new Dictionary<string, List<MixtapeEventTemplate>>(gameEvents.Count + 1);
        bool inserted = false;
        foreach (var pair in gameEvents)
        {
            if (string.Equals(pair.Key, Category, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            reordered[pair.Key] = pair.Value;
            if (!inserted && string.Equals(pair.Key, "accessibility", System.StringComparison.OrdinalIgnoreCase))
            {
                reordered[Category] = events;
                inserted = true;
            }
        }
        if (!inserted)
        {
            reordered[Category] = events;
        }
        gameEvents.Clear();
        foreach (var pair in reordered)
        {
            gameEvents[pair.Key] = pair.Value;
        }
    }

    public static void InjectAllEvents(Dictionary<string, MixtapeEventTemplate> allEvents)
    {
        if (allEvents == null)
        {
            return;
        }
        foreach (var template in Templates)
        {
            allEvents[template.dataModel] = template;
        }
    }
}
