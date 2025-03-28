﻿using BepInEx;
using UnityEngine;
using ILogger = COM3D2.i18nEx.Core.ILogger;

namespace COM3D2.i18nEx.BepInEx
{
    [BepInPlugin("horse.coder.com3d2.i18nex", "i18nEx for 3.x", "1.6.1")]
    [BepInDependency("gravydevsupreme.xunity.autotranslator", BepInDependency.DependencyFlags.SoftDependency)]
    public class InternationalizationBepInEx : BaseUnityPlugin, ILogger
    {
        private GameObject managerObject;

        public void Awake()
        {
            DontDestroyOnLoad(this);
             
            managerObject = new GameObject("i18nExManager");
            DontDestroyOnLoad(managerObject);

            var core = managerObject.AddComponent<Core.Core>();
            core.Initialize(this, Paths.GameRootPath);
        }

        public void LogInfo(object data)
        {
            Logger.LogInfo(data);
        }

        public void LogWarning(object data)
        {
            Logger.LogWarning(data);
        }

        public void LogError(object data)
        {
            Logger.LogError(data);
        }
    }
}
