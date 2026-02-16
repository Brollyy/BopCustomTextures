using BopCustomTextures.Scripts;
using System.Collections.Generic;

namespace BopCustomTextures.SceneMods;

/// <summary>
/// Scene mod CustomSpriteSwapper definition
/// </summary>
public class MCustomSpriteSwapper : MBehaviour
{
    public List<int> variants;
    public Dictionary<int, int> variantsIndexed;

    public void Apply(CustomSpriteSwapper component)
    {
        base.Apply(component);
        if (variants != null) component.ApplyVariants(variants);
        else if (variantsIndexed != null) component.ApplyVariants(variantsIndexed);
    }
}
