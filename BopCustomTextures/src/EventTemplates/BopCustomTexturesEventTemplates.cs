using System.Collections.Generic;

namespace BopCustomTextures.EventTemplates;

public class BopCustomTexturesEventTemplates
{
    public static readonly MixtapeEventTemplate sceneModTemplate = new()
    {
        dataModel = $"{MyPluginInfo.PLUGIN_GUID}/apply scene mod",
        length = 0.5f,
        properties = new Dictionary<string, object>
        {
            ["scene"] = "",
            ["key"] = ""
        }
    };

    public static readonly MixtapeEventTemplate addTextureVariantTemplate = new()
    {
        dataModel = $"{MyPluginInfo.PLUGIN_GUID}/add texture variant",
        length = 0.5f,
        properties = new Dictionary<string, object>
        {
            ["scene"] = "",
            ["variant"] = ""
        }
    };

    public static readonly MixtapeEventTemplate removeTextureVariantTemplate = new()
    {
        dataModel = $"{MyPluginInfo.PLUGIN_GUID}/remove texture variant",
        length = 0.5f,
        properties = new Dictionary<string, object>
        {
            ["scene"] = "",
            ["variant"] = ""
        }
    };

    public static readonly MixtapeEventTemplate setTextureVariantTemplate = new()
    {
        dataModel = $"{MyPluginInfo.PLUGIN_GUID}/set texture variant",
        length = 0.5f,
        properties = new Dictionary<string, object>
        {
            ["scene"] = "",
            ["variant"] = ""
        }
    };

    public static readonly MixtapeEventTemplate[] textureVariantTemplates =
    [
        addTextureVariantTemplate,
        removeTextureVariantTemplate,
        setTextureVariantTemplate
    ];

    public static readonly MixtapeEventTemplate[] templates =
    [
        addTextureVariantTemplate,
        removeTextureVariantTemplate,
        setTextureVariantTemplate,
        sceneModTemplate
    ];
}
