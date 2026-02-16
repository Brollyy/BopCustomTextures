using System;
using System.Collections.Generic;
using UnityEngine;

namespace BopCustomTextures.SceneMods;

/// <summary>
/// Scene mod material definition.
/// </summary>
public class MMaterial
{
    public Shader shader;
    public Dictionary<string, float> floats = [];
    public Dictionary<string, int> integers = [];
    public List<string> disableKeywords = [];
    public List<string> enableKeywords = [];

    public Material Apply(Material material)
    {
        if (shader != null) material.shader = shader;
        foreach (var pair in integers)
        {
            material.SetInteger(pair.Key, pair.Value);
        }
        foreach (var pair in floats)
        {
            material.SetFloat(pair.Key, pair.Value);
        }
        foreach (var keyword in enableKeywords)
        {
            Console.WriteLine(keyword);
            material.EnableKeyword(keyword);
        }
        foreach (var keyword in disableKeywords)
        {
            material.DisableKeyword(keyword);
        }
        return material;
    }
}
