using UnityEngine;

namespace BopCustomTextures.SceneMods;

/// <summary>
/// Scene mod MonoBehaviour definition
/// </summary>
public abstract class MBehaviour : MComponent
{
    public bool? enabled;
    public void Apply(Behaviour component)
    {
        if (enabled != null) component.enabled = (bool)enabled;
    }
}