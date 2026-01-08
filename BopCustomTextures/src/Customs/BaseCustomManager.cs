using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using ILogger = BopCustomTextures.Logging.ILogger;

namespace BopCustomTextures.Customs;
public class BaseCustomManager(ILogger logger)
{
    public ILogger logger = logger;

    public static readonly AccessTools.FieldRef<MixtapeLoaderCustom, Dictionary<SceneKey, GameObject>> rootObjectsRef =
        AccessTools.FieldRefAccess<MixtapeLoaderCustom, Dictionary<SceneKey, GameObject>>("rootObjects");

    protected static SceneKey ToSceneKeyOrInvalid(string name)
    {
        string[] namesAffixed =
        [
        name,
        name + "Custom",
        name + "Mixtape"
        ];
        foreach (string name2 in namesAffixed)
        {
            SceneKey[] sceneKeys = MixtapeLoaderCustom.allSceneKeys;
            for (int j = 0; j < sceneKeys.Length; j++)
            {
                SceneKey result = sceneKeys[j];
                string keyName = result.ToString();
                if (string.Equals(name2, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    return result;
                }
            }
        }
        return SceneKey.Invalid;
    }
}
