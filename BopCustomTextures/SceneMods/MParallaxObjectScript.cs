namespace BopCustomTextures.SceneMods;

/// <summary>
/// Scene mod ParallaxObjectScript definition
/// </summary>
public class MParallaxObjectScript : MBehaviour
{
    public float? parallaxScale;
    public float? loopDistance;

    public void Apply(ParallaxObjectScript component)
    {
        base.Apply(component);
        if (parallaxScale != null) component.parallaxScale = (float)parallaxScale;
        if (loopDistance != null) component.loopDistance = (float)loopDistance;
    }
}
