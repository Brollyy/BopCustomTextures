using BopCustomTextures.Customs;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace BopCustomTextures.Scripts;

/// <summary>
/// Unity component that swaps a spriteRenderer's sprite to a custom one if a custom one is available.
/// </summary>
[DefaultExecutionOrder(2)] // because of flow worms
internal class CustomSpriteSwapper : MonoBehaviour
{
    public Sprite LastVanilla;
    public Sprite Last;
    private List<int> _variants = [];
    public CustomTextureManager TextureManager;
    public SpriteRenderer SpriteRenderer;

    public List<int> Variants
    {
        get => _variants;
        set
        {
            _variants = value;
            ReplaceCustomSprites();
        }
    }

    void Awake()
    {
        SpriteRenderer = gameObject.GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (SpriteRenderer.sprite != Last)
        {
            LastVanilla = SpriteRenderer.sprite;
            ReplaceCustomSprites();
        }
    }

    public void ReplaceCustomSprites()
    {
        Last = TextureManager.ReplaceCustomSprite(LastVanilla, _variants);
        SpriteRenderer.sprite = Last;
    }
}

