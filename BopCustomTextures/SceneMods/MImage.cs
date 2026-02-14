using UnityEngine;
using UnityEngine.UI;

namespace BopCustomTextures.SceneMods;

/// <summary>
/// Scene mod UI.Image definition
/// </summary>
public class MImage : MComponent
{
    public Material material;

    public void Apply(Image component)
    {
        if (material != null) component.material = material;
    }
}
