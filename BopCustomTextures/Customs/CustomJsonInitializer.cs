using BopCustomTextures.SceneMods;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using ILogger = BopCustomTextures.Logging.ILogger;
using System.Globalization;

namespace BopCustomTextures.Customs;

/// <summary>
/// Used to parse JSON-defined scene mods.
/// </summary>
/// <param name="logger">Plugin-specific logger.</param>
/// <param name="variantManager">Used for mapping custom texture variant external names to internal indices. Shared with CustomTextureManager.</param>
public class CustomJsonInitializer(ILogger logger, CustomVariantNameManager variantManager) : BaseCustomManager(logger)
{
    private readonly Dictionary<string, Material> Materials = [];
    private readonly Dictionary<string, Material> ShaderMaterials = [];
    private readonly CustomVariantNameManager VariantManager = variantManager;

    public MGameObject InitGameObject(JObject jobj, SceneKey scene, string name = "", bool isVolatile = false)
    {
        var mobj = new MGameObject(name);
        var components = new List<MComponent>();
        var childObjs = new List<MGameObject>();
        var childObjsVolatile = new List<MGameObject>();
        foreach (KeyValuePair<string, JToken> dict in jobj)
        {
            if (dict.Key.StartsWith("!"))
            {
                if (dict.Key == "!Active")
                {
                    if (dict.Value.Type != JTokenType.Boolean) {
                        logger.LogWarning($"JSON Active \"{dict.Key}\" is a {dict.Value.Type} when it should be a Boolean");
                        continue;
                    }
                    mobj.active = (bool)dict.Value;
                } 
                else
                {
                    if (dict.Value.Type != JTokenType.Object)
                    {
                        logger.LogWarning($"JSON Componnent \"{dict.Key}\" is a {dict.Value.Type} when it should be a Object");
                        continue;
                    }
                    var mcomponent = InitComponent((JObject)dict.Value, scene, dict.Key.Substring(1));
                    if (mcomponent != null)
                    {
                        components.Add(mcomponent);
                    }
                }
            }
            else
            {
                if (dict.Value.Type != JTokenType.Object)
                {
                    logger.LogWarning($"JSON GameObject \"{dict.Key}\" is a {dict.Value.Type} when it should be a Object");
                    continue;
                }

                string childName = dict.Key;
                bool isChildVolatile = isVolatile;
                if (childName.StartsWith("~")) {
                    isChildVolatile = true;
                    childName = childName.Substring(1);
                }

                var mchildObj = InitGameObject((JObject)dict.Value, scene, childName, isChildVolatile);
                if (isChildVolatile)
                {
                    childObjsVolatile.Add(mchildObj);
                } 
                else
                {
                    childObjs.Add(mchildObj);
                }
            }
        }
        mobj.components = components.ToArray();
        mobj.childObjs = childObjs.ToArray();
        mobj.childObjsVolatile = childObjsVolatile.ToArray();
        return mobj;
    }

    public MComponent InitComponent(JObject jcomponent, SceneKey scene, string name)
    {
        switch (name)
        {
            case "Transform":
                return InitTransform(jcomponent);
            case "SpriteRenderer":
                return InitSpriteRenderer(jcomponent);
            case "Image":
                return InitImage(jcomponent);
            case "ParallaxObjectScript":
                return InitParallaxObjectScript(jcomponent);
            case "CustomSpriteSwapper":
                return InitCustomSpriteSwapper(jcomponent, scene);
            default:
                logger.LogWarning($"JSON Componnent \"{name}\" is an unknown/unsupported component");
                return null;
        }
    }

    // UNITY COMPONENTS //
    public MTransform InitTransform(JObject jtransform)
    {
        var mtransform = new MTransform();
        if (TryGetJVector3(jtransform, "LocalPosition", out var vector3)) mtransform.localPosition = vector3;
        if (TryGetJQuaternion(jtransform, "LocalRotation", out var quaternion)) mtransform.localRotation = quaternion;
        if (TryGetJEulerAngles(jtransform, "LocalEulerAngles", out vector3)) mtransform.localEulerAngles = vector3;
        if (TryGetJVector3(jtransform, "LocalEulerAngles", out vector3)) mtransform.localEulerAngles = vector3;
        if (TryGetJVector3(jtransform, "LocalScale", out vector3)) mtransform.localScale = vector3;
        return mtransform;
    }

    public MSpriteRenderer InitSpriteRenderer(JObject jspriteRenderer)
    {
        var mspriteRenderer = new MSpriteRenderer();
        if (TryGetJColor(jspriteRenderer, "Color", out var color)) mspriteRenderer.color = color;
        if (TryGetJVector2(jspriteRenderer, "Size", out var vector2)) mspriteRenderer.size = vector2;
        JValue jval;
        if (TryGetJValue(jspriteRenderer, "FlipX", JTokenType.Boolean, out jval)) mspriteRenderer.flipX = (bool)jval;
        if (TryGetJValue(jspriteRenderer, "FlipY", JTokenType.Boolean, out jval)) mspriteRenderer.flipY = (bool)jval;
        Material mat;
        if (TryGetJMaterial(jspriteRenderer, "Material", out mat) || 
            TryGetJShader(jspriteRenderer, "Shader", out mat))
            mspriteRenderer.material = mat;
        return mspriteRenderer;
    }

    public MImage InitImage(JObject jimage)
    {
        var mimage = new MImage();
        Material mat;
        if (TryGetJMaterial(jimage, "Material", out mat) ||
            TryGetJShader(jimage, "Shader", out mat))
            mimage.material = mat;
        return mimage;
    }

    // BITS & BOPS SCRIPTS //
    public MParallaxObjectScript InitParallaxObjectScript(JObject jparallaxObjectScript)
    {
        var mparallaxObjectScript = new MParallaxObjectScript();
        float jfloat;
        if (TryGetJFloat(jparallaxObjectScript, "ParallaxScale", out jfloat)) mparallaxObjectScript.parallaxScale = jfloat;
        if (TryGetJFloat(jparallaxObjectScript, "LoopDistance", out jfloat)) mparallaxObjectScript.loopDistance = jfloat;
        return mparallaxObjectScript;
    }

    // THERE I AM GARY, THERE I AM! //

    public MCustomSpriteSwapper InitCustomSpriteSwapper(JObject jcustomSpriteSwapper, SceneKey scene)
    {
        var mcustomSpriteSwapper = new MCustomSpriteSwapper();
        if (jcustomSpriteSwapper.TryGetValue("Variants", out var jvariants))
        {
            switch (jvariants.Type)
            {
                case JTokenType.Array:
                    mcustomSpriteSwapper.variants = [];
                    foreach (var jel in (JArray)jvariants)
                    {
                        if (TryGetVariant(jel, scene, out var variant))
                        {
                            mcustomSpriteSwapper.variants.Add(variant);
                        }
                    }
                    break;
                case JTokenType.Object:
                    mcustomSpriteSwapper.variantsIndexed = [];
                    var jobj = (JObject)jvariants;
                    foreach (var pair in jobj)
                    {
                        if (!int.TryParse(pair.Key, out var index))
                        {
                            logger.LogWarning($"JSON variant \"{pair.Key}\" does not have an integer key");
                            continue;
                        }
                        if (TryGetVariant(pair.Value, scene, out var variant))
                        {
                            mcustomSpriteSwapper.variantsIndexed[index] = variant;
                        }
                    }
                    break;
                case JTokenType.String:
                case JTokenType.Integer:
                    if (TryGetVariant(jvariants, scene, out var variant2))
                    {
                        mcustomSpriteSwapper.variants = [variant2];
                    }
                    break;
                default:
                    logger.LogWarning($"JSON variants is a {jvariants.Type} when it should be an array, object, string, or integer");
                    break;
            }
        }
        return mcustomSpriteSwapper;
    }

    public bool TryGetVariant(JToken jtoken, SceneKey scene, out int variant)
    {
        switch (jtoken.Type)
        {
            case JTokenType.String:
                if (!VariantManager.TryGetVariant(scene, (string)jtoken, out variant))
                {
                    return false;
                }
                return true;
            case JTokenType.Integer:
                variant = (int)jtoken;
                return true;
        }
        logger.LogWarning($"JSON variant \"{jtoken}\" is a {jtoken.Type} when it should be a string or integer");
        variant = -1;
        return false;
    }

    // STRUCTS //
    public bool TryGetJVector2(JObject jobj, string key, out Vector2 vector2)
    {
        if (!jobj.TryGetValue(key, out var jvector2))
        {
            vector2 = default;
            return false;
        }
        switch (jvector2)
        {
            case JObject jobj2:
                vector2 = InitCustomVector2(jobj2);
                return true;
            case JArray jarray2:
                vector2 = InitCustomVector2(jarray2);
                return true;
        }
        logger.LogWarning($"JSON vector2 \"{key}\" is a {jvector2.Type} when it should be an object or array");
        vector2 = default;
        return false;
    }
    public Vector2 InitCustomVector2(JObject jvector2)
    {
        float jfloat;
        return new Vector2(
            TryGetJFloat(jvector2, "x", out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jvector2, "y", out jfloat) ? jfloat : float.NaN
        );
    }
    public Vector2 InitCustomVector2(JArray jvector2)
    {
        float jfloat;
        return new Vector2(
            TryGetJFloat(jvector2, 0, out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jvector2, 1, out jfloat) ? jfloat : float.NaN
        );
    }

    public bool TryGetJVector3(JObject jobj, string key, out Vector3 vector3)
    {
        if (!jobj.TryGetValue(key, out var jvector3))
        {
            vector3 = default;
            return false;
        }
        switch (jvector3)
        {
            case JObject jobj2:
                vector3 = InitCustomVector3(jobj2);
                return true;
            case JArray jarray2:
                vector3 = InitCustomVector3(jarray2);
                return true;
        }
        logger.LogWarning($"JSON vector3 \"{key}\" is a {jvector3.Type} when it should be an object or array");
        vector3 = default;
        return false;
    }
    public Vector3 InitCustomVector3(JObject jvector3)
    {
        float jfloat;
        return new Vector3(
            TryGetJFloat(jvector3, "x", out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jvector3, "y", out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jvector3, "z", out jfloat) ? jfloat : float.NaN
        );
    }
    public Vector3 InitCustomVector3(JArray jvector3)
    {
        float jfloat;
        return new Vector3(
            TryGetJFloat(jvector3, 0, out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jvector3, 1, out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jvector3, 2, out jfloat) ? jfloat : float.NaN
        );
    }

    public bool TryGetJEulerAngles(JObject jobj, string key, out Vector3 eulerAngles)
    {
        if (!jobj.TryGetValue(key, out var jvector3))
        {
            eulerAngles = default;
            return false;
        }
        switch (jvector3.Type)
        {
            case JTokenType.Object:
                eulerAngles = InitCustomVector3((JObject)jvector3);
                return true;
            case JTokenType.Array:
                eulerAngles = InitCustomVector3((JArray)jvector3);
                return true;
            case JTokenType.Float:
            case JTokenType.Integer:
                eulerAngles = new Vector3(float.NaN, (float)jvector3, float.NaN);
                return true;
        }
        logger.LogWarning($"JSON eulerAngles \"{key}\" is a {jvector3.Type} when it should be an object, array, float, or integer");
        eulerAngles = default;
        return false;
    }

    public bool TryGetJQuaternion(JObject jobj, string key, out Quaternion quaternion)
    {
        if (!jobj.TryGetValue(key, out var jquaternion))
        {
            quaternion = default;
            return false;
        }
        switch (jquaternion)
        {
            case JObject jobj2:
                quaternion = InitCustomQuaternion(jobj2);
                return true;
            case JArray jarray2:
                quaternion = InitCustomQuaternion(jarray2);
                return true;
        }
        logger.LogWarning($"JSON quaternion \"{key}\" is a {jquaternion.Type} when it should be an object or array");
        quaternion = default;
        return false;
    }
    public Quaternion InitCustomQuaternion(JObject jquaternion)
    {
        float jfloat;
        return new Quaternion(
            TryGetJFloat(jquaternion, "x", out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jquaternion, "y", out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jquaternion, "z", out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jquaternion, "w", out jfloat) ? jfloat : float.NaN
        );
    }
    public Quaternion InitCustomQuaternion(JArray jquaternion)
    {
        float jfloat;
        return new Quaternion(
            TryGetJFloat(jquaternion, 0, out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jquaternion, 1, out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jquaternion, 2, out jfloat) ? jfloat : float.NaN,
            TryGetJFloat(jquaternion, 3, out jfloat) ? jfloat : float.NaN
        );
    }

    public bool TryGetJColor(JObject jobj, string key, out Color color)
    {
        if (!jobj.TryGetValue(key, out var jcolor))
        {
            color = default;
            return false;
        }
        switch (jcolor.Type)
        {
            case JTokenType.Object:
                color = InitCustomColor((JObject)jcolor);
                return true;
            case JTokenType.Array:
                color = InitCustomColor((JArray)jcolor);
                return true;
            case JTokenType.String:
                color = InitCustomColor((string)jcolor);
                return true;
        }
        logger.LogWarning($"JSON color \"{key}\" is a {jcolor.Type} when it should be an object, array, or string");
        color = default;
        return false;
    }
    public Color InitCustomColor(JObject jcolor)
    {
        float jfloat;
        return new Color(
            TryGetJColorChannel(jcolor, "r", out jfloat) ? jfloat : float.NaN,
            TryGetJColorChannel(jcolor, "g", out jfloat) ? jfloat : float.NaN,
            TryGetJColorChannel(jcolor, "b", out jfloat) ? jfloat : float.NaN,
            TryGetJColorChannel(jcolor, "a", out jfloat) ? jfloat : float.NaN
        );
    }
    public Color InitCustomColor(JArray jcolor)
    {
        float jfloat;
        return new Color(
            TryGetJColorChannel(jcolor, 0, out jfloat) ? jfloat : float.NaN,
            TryGetJColorChannel(jcolor, 1, out jfloat) ? jfloat : float.NaN,
            TryGetJColorChannel(jcolor, 2, out jfloat) ? jfloat : float.NaN,
            TryGetJColorChannel(jcolor, 3, out jfloat) ? jfloat : float.NaN
        );
    }
    public Color InitCustomColor(string str)
    {
        str = str.TrimStart('#');
        Color jcolor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
        if (!int.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            logger.LogWarning($"JSON color string \"{str}\" couldn't be parsed as as color");
        }
        else
        {
            for (int i = 0; i < str.Length / 2 && i < 4; i++)
            {
                jcolor[i] = (rgb & 0xFF) / 255.0f;
                rgb >>= 8;
            }
        }
        return jcolor;
    }

    public bool TryGetJMaterial(JObject jobj, string key, out Material mat)
    {
        if (!TryGetJValue(jobj, key, JTokenType.String, out var jmatName))
        {
            mat = null;
            return false;
        }
        string matName = (string)jmatName;
        if (!Materials.ContainsKey(matName))
        {
            Material found = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(s => s.name == matName) ??
                Resources.Load<Material>($"Materials/{matName}");
            if (!found)
            {
                logger.LogWarning($"JSON material \"{matName}\" could not be found");
                Materials[matName] = null;
            }
            else
            {
                Materials[matName] = found;
            }
        }

        mat = Materials[matName];
        if (!mat)
        {
            return false;
        }
        return true;
    }

    public bool TryGetJShader(JObject jobj, string key, out Material mat)
    {
        if (!TryGetJValue(jobj, key, JTokenType.String, out var jshaderName))
        {
            mat = null;
            return false;
        }
        string shaderName = (string)jshaderName;
        if (!ShaderMaterials.ContainsKey(shaderName))
        {
            Shader found = Resources.FindObjectsOfTypeAll<Shader>().FirstOrDefault(s => s.name == shaderName) ??
                           Shader.Find(shaderName);
            if (!found)
            {
                logger.LogWarning($"JSON shader \"{shaderName}\" could not be found");
                ShaderMaterials[shaderName] = null;
            }
            else
            {
                ShaderMaterials[shaderName] = new Material(found);
            }
        }

        mat = ShaderMaterials[shaderName];
        if (!mat)
        {
            return false;
        }
        return true;
    }


    // UTILITY // 
    public bool TryGetJToken<T>(JObject jobj, string key, JTokenType type, out T jtoken) where T: JToken
    {
        if (!jobj.TryGetValue(key, out var jtoken2))
        {
            jtoken = null;
            return false;
        }
        if (jtoken2.Type != type)
        {
            logger.LogWarning($"JSON key \"{key}\" is a {jtoken2.Type} when it should be a {type}");
            jtoken = null;
            return false;
        }
        jtoken = (T)jtoken2;
        return true;
    }
    public bool TryGetJToken<T>(JArray jarray, int index, JTokenType type, out T jtoken) where T : JToken
    {
        if (jarray.Count <= index)
        {
            jtoken = null;
            return false;
        }
        var jtoken2 = jarray[index];
        if (jtoken2.Type != type)
        {
            logger.LogWarning($"JSON index \"{index}\" is a {jtoken2.Type} when it should be a {type}");
            jtoken = null;
            return false;
        }
        jtoken = (T)jtoken2;
        return true;
    }

    public bool TryGetJValue(JObject jobj, string key, JTokenType type, out JValue jvalue)
    {
        return TryGetJToken(jobj, key, type, out jvalue);
    }
    public bool TryGetJValue(JArray Jarray, int index, JTokenType type, out JValue jvalue)
    {
        return TryGetJToken(Jarray, index, type, out jvalue);
    }
    public bool TryGetJObject(JObject jobj, string key, out JObject jvalue)
    {
        return TryGetJToken(jobj, key, JTokenType.Object, out jvalue);
    }

    public bool TryGetJFloat(JObject jobj, string key, out float jfloat)
    {
        if (!jobj.TryGetValue(key, out var jtoken2))
        {
            jfloat = default;
            return false;
        }
        if (jtoken2.Type != JTokenType.Float && jtoken2.Type != JTokenType.Integer)
        {
            logger.LogWarning($"JSON key \"{key}\" is a {jtoken2.Type} when it should be a float or integer");
            jfloat = default;
            return false;
        }
        jfloat = (float)jtoken2;
        return true;
    }
    public bool TryGetJFloat(JArray jarray, int index, out float jfloat)
    {
        if (jarray.Count <= index)
        {
            jfloat = default;
            return false;
        }
        var jtoken2 = jarray[index];
        if (jtoken2.Type != JTokenType.Float && jtoken2.Type != JTokenType.Integer)
        {
            logger.LogWarning($"JSON index \"{index}\" is a {jtoken2.Type} when it should be a float or integer");
            jfloat = default;
            return false;
        }
        jfloat = (float)jtoken2;
        return true;
    }

    public bool TryGetJColorChannel(JObject jobj, string key, out float jfloat)
    {
        if (!jobj.TryGetValue(key, out var jtoken2))
        {
            jfloat = default;
            return false;
        }
        switch (jtoken2.Type)
        {
            case JTokenType.Float:
                jfloat = (float)jtoken2;
                return true;
            case JTokenType.Integer:
                jfloat = (float)jtoken2 / 255;
                return true;
        }
        logger.LogWarning($"JSON key \"{key}\" is a {jtoken2.Type} when it should be a float or integer");
        jfloat = default;
        return true;
    }
    public bool TryGetJColorChannel(JArray jarray, int index, out float jfloat)
    {
        if (jarray.Count <= index)
        {
            jfloat = default;
            return false;
        }
        var jtoken2 = jarray[index];
        switch (jtoken2.Type)
        {
            case JTokenType.Float:
                jfloat = (float)jtoken2;
                return true;
            case JTokenType.Integer:
                jfloat = (float)jtoken2 / 255;
                return true;
        }
        logger.LogWarning($"JSON index \"{index}\" is a {jtoken2.Type} when it should be a float or integer");
        jfloat = default;
        return true;
    }


}
