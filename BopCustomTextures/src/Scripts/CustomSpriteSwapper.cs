using BopCustomTextures.Customs;
using UnityEngine;

namespace BopCustomTextures.Scripts;

/// <summary>
/// Unity component that swaps a spriteRenderer's sprite to a custom one if a custom one is available.
/// </summary>
[DefaultExecutionOrder(2)] // because of flow worms
internal class CustomSpriteSwapper : MonoBehaviour
{
    public Sprite last;
    public SceneKey scene;
    public CustomTextureManager textureManager;
    public SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (spriteRenderer.sprite != last)
        {
            textureManager.ReplaceCustomSprite(spriteRenderer, scene);
            last = spriteRenderer.sprite;
        }
    }
}

