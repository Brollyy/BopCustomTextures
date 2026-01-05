using UnityEngine;

namespace BopCustomTextures;

[DefaultExecutionOrder(2)] // because of flow worms
internal class CustomSpriteScript : MonoBehaviour
{
    private Sprite last;
    public SceneKey scene;
    public SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        last = spriteRenderer.sprite;
    }

    void LateUpdate()
    {
        if (spriteRenderer.sprite != last)
        {
            CustomTextureManagement.ReplaceCustomSprite(spriteRenderer, scene);
            last = spriteRenderer.sprite;
        }
    }
}

