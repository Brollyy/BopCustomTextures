using System;
using UnityEngine;
using UnityEngine.UI;

namespace BopCustomTextures.SceneMods;

/// <summary>
/// Scene mod UI.Image definition
/// </summary>
public class MImage : MComponent, IMRenderable
{
    public Material material;
    public MMaterial mmaterial;

    public Material Material { get => material; set => material = value; }
    public MMaterial MMaterial { get => mmaterial; set => mmaterial = value; }

    public void Apply(Image component)
    {
        if (material != null) component.material = material;
        if (mmaterial != null) 
        {
            
            component.material = mmaterial.Apply(component.material);
            foreach (var keyword in component.material.enabledKeywords)
            {
                Console.WriteLine(keyword);
            }
            
        }
    }
}
