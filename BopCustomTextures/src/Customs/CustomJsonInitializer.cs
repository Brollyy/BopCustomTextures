using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Color = UnityEngine.Color;
using ILogger = BopCustomTextures.Logging.ILogger;
using System.Text.RegularExpressions;

namespace BopCustomTextures.Customs;

/// <summary>
/// Class of methods used to apply scene mods.
/// </summary>
/// <param name="logger">Plugin-specific logger</param>
public class CustomJsonInitializer(ILogger logger) : BaseCustomManager(logger)
{
    public void InitCustomGameObject(JObject jobj, GameObject obj)
    {
        foreach (KeyValuePair<string, JToken> dict in jobj)
        {
            if (dict.Key.StartsWith("!"))
            {
                if (dict.Value.Type != JTokenType.Object)
                {
                    logger.LogWarning($"JSON Componnent \"{dict.Key}\" is a {dict.Value.Type} when it should be a JObject");
                    return;
                }
                InitCustomComponent((JObject)dict.Value, dict.Key.Substring(1), obj);
            }
            else
            {
                if (dict.Value.Type != JTokenType.Object)
                {
                    logger.LogWarning($"JSON GameObject \"{dict.Key}\" is a {dict.Value.Type} when it should be a Object");
                    return;
                }
                bool matched = false;
                var jchildObj = (JObject)dict.Value;
                foreach (var childObj in FindGameObjectsInChildren(obj, dict.Key))
                {
                    matched = true;
                    InitCustomGameObject(jchildObj, childObj);
                }
                if (!matched)
                {
                    logger.LogWarning($"JSON GameObject \"{dict.Key}\" didn't correspond to any existent GameObjects");
                }
            }
        }
    }

    public void InitCustomComponent(JObject jcomponent, string name, GameObject obj)
    {
        switch (name)
        {
            case "Transform":
                InitCustomTransform(jcomponent, obj);
                break;
            case "SpriteRenderer":
                InitCustomSpriteRenderer(jcomponent, obj);
                break;
            case "ParallaxObjectScript":
                InitCustomParallaxObjectScript(jcomponent, obj);
                break;
            default:
                logger.LogWarning($"JSON Componnent \"{name}\" is an unknown/unsupported component");
                break;
        }
    }

    // UNITY COMPONENTS //
    public void InitCustomTransform(JObject jtransform, GameObject obj)
    {
        var transform = obj.transform;
        transform.localPosition = InitCustomVector3(jtransform, "LocalPosition", transform.localPosition);
        transform.localRotation = InitCustomQuaternion(jtransform, "LocalRotation", transform.localRotation);
        transform.localScale = InitCustomVector3(jtransform, "LocalScale", transform.localScale);
    }
    public void InitCustomSpriteRenderer(JObject jspriteRenderer, GameObject obj)
    {
        var spriteRenderer = obj.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            logger.LogWarning($"GameObject \"{obj.name}\" does not have a spriteRenderer");
            return;
        }
        spriteRenderer.color = InitCustomColor(jspriteRenderer, "Color", spriteRenderer.color);
        spriteRenderer.size = InitCustomVector2(jspriteRenderer, "Size", spriteRenderer.size);
        spriteRenderer.flipX = InitCustomBool(jspriteRenderer, "FlipX", spriteRenderer.flipX);
        spriteRenderer.flipY = InitCustomBool(jspriteRenderer, "FlipY", spriteRenderer.flipY);
    }

    // BITS & BOPS SCRIPTS //
    public void InitCustomParallaxObjectScript(JObject jparallaxObjectScript, GameObject obj)
    {
        var parallaxObjectScript = obj.GetComponent<ParallaxObjectScript>();
        if (parallaxObjectScript == null)
        {
            logger.LogWarning($"GameObject \"{obj.name}\" does not have a parallaxObjectScript");
            return;
        }
        parallaxObjectScript.parallaxScale = InitCustomFloat(jparallaxObjectScript, "ParallaxScale", parallaxObjectScript.parallaxScale);
        parallaxObjectScript.loopDistance = InitCustomFloat(jparallaxObjectScript, "LoopDistance", parallaxObjectScript.loopDistance);
    }

    // STRUCTS //
    public Vector2 InitCustomVector2(JObject jobj, string key, Vector2 vector2)
    {
        if (jobj.ContainsKey(key))
        {
            if (jobj[key].GetType() == typeof(JObject))
            {
                JObject jvector2 = (JObject)jobj[key];
                vector2.x = InitCustomFloat(jvector2, "x", vector2.x);
                vector2.y = InitCustomFloat(jvector2, "y", vector2.y);
            }
            else
            {
                logger.LogWarning($"JSON Vector2 \"{key}\" is a {jobj[key].GetType()} when it should be a JObject");
            }
        }
        return vector2;
    }
    public Vector3 InitCustomVector3(JObject jobj, string key, Vector3 vector3)
    {
        if (jobj.ContainsKey(key))
        {
            if (jobj[key].GetType() == typeof(JObject))
            {
                JObject jvector3 = (JObject)jobj[key];
                vector3.x = InitCustomFloat(jvector3, "x", vector3.x);
                vector3.y = InitCustomFloat(jvector3, "y", vector3.y);
                vector3.z = InitCustomFloat(jvector3, "z", vector3.z);
            }
            else
            {
                logger.LogWarning($"JSON Vector3 \"{key}\" is a {jobj[key].GetType()} when it should be a JObject");
            }
        }
        return vector3;
    }

    public Quaternion InitCustomQuaternion(JObject jobj, string key, Quaternion quaternion)
    {
        if (jobj.ContainsKey(key))
        {
            if (jobj[key].GetType() == typeof(JObject))
            {
                JObject jvector3 = (JObject)jobj[key];
                quaternion.x = InitCustomFloat(jvector3, "x", quaternion.x);
                quaternion.y = InitCustomFloat(jvector3, "y", quaternion.y);
                quaternion.z = InitCustomFloat(jvector3, "z", quaternion.z);
                quaternion.w = InitCustomFloat(jvector3, "w", quaternion.w);
            }
            else
            {
                logger.LogWarning($"JSON Quaternion \"{key}\" is a {jobj[key].GetType()} when it should be a JObject");
            }
        }
        return quaternion;
    }

    public Color InitCustomColor(JObject jobj, string key, Color color)
    {
        if (jobj.ContainsKey(key))
        {
            if (jobj[key].GetType() == typeof(JObject))
            {
                JObject jcolor = (JObject)jobj[key];
                color.r = InitCustomFloat(jcolor, "r", color.r);
                color.g = InitCustomFloat(jcolor, "g", color.g);
                color.b = InitCustomFloat(jcolor, "b", color.b);
                color.a = InitCustomFloat(jcolor, "a", color.a);
            }
            else
            {
                logger.LogWarning($"JSON Color \"{key}\" is a {jobj[key].GetType()} when it should be a JObject");
            }
        }
        return color;
    }

    // PRIMITIVES // 
    public float InitCustomFloat(JObject jobj, string key, float num)
    {
        if (jobj.ContainsKey(key))
        {
            if (jobj[key].GetType() == typeof(JValue))
            {
                JValue jval = (JValue)jobj[key];
                if (jval.Type == JTokenType.Float)
                {
                    return (float)jval;
                }
                else
                {
                    logger.LogWarning($"JSON float \"{key}\" is a {jval.Type} when it should be a float");
                }
            }
            else
            {
                logger.LogWarning($"JSON float \"{key}\" is a {jobj[key].GetType()} when it should be a float");
            }
        }
        return num;
    }

    public bool InitCustomBool(JObject jobj, string key, bool val)
    {
        if (jobj.ContainsKey(key))
        {
            if (jobj[key].GetType() == typeof(JValue))
            {
                JValue jval = (JValue)jobj[key];
                if (jval.Type == JTokenType.Boolean)
                {
                    return (bool)jval;
                }
                else
                {
                    logger.LogWarning($"JSON float \"{key}\" is a {jval.Type} when it should be a boolean");
                }
            }
            else
            {
                logger.LogWarning($"JSON float \"{key}\" is a {jobj[key].GetType()} when it should be a boolean");
            }
        }
        return val;
    }


    // UTILITY // 

    public static IEnumerable<GameObject> FindGameObjectsInChildren(GameObject obj, string path)
    {
        string[] names = Regex.Split(path, @"[\\/]");
        return FindGameObjectsInChildren(obj, names);
    }

    public static IEnumerable<GameObject> FindGameObjectsInChildren(GameObject rootObj, string[] names, int i = 0)
    {
        for (var j = 0; j < rootObj.transform.childCount; j++)
        {
            var obj = rootObj.transform.GetChild(j).gameObject;
            if (Regex.IsMatch(obj.name, WildCardToRegex(names[i])))
            {
                if (i == names.Length - 1)
                {
                    yield return obj;
                }
                else
                {
                    foreach (var childObj in FindGameObjectsInChildren(obj, names, i + 1))
                    {
                        yield return childObj;
                    }
                }
            }
        }
    }

    private static string WildCardToRegex(string value)
    {
        return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
    }
}
