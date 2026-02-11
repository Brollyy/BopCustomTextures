using BopCustomTextures.Scripts;
using System.Collections.Generic;

namespace BopCustomTextures.SceneMods;

/// <summary>
/// Scene mod CustomSpriteSwapper definition
/// </summary>
public class MCustomSpriteSwapper : MComponent
{
    public List<int> variants;
    public Dictionary<int, int> variantsIndexed;

    public void Apply(CustomSpriteSwapper component)
    {
        if (variants != null) component.ApplyVariants(variants);
        else if (variantsIndexed != null) component.ApplyVariants(variantsIndexed);
    }
}
