using System;
using BepInEx;
using UnityEngine;

namespace PEAK.TxtUpdater
{
    [BepInPlugin("vocaloid2048.peaktxtupdater", "PEAK Txt Updater", "1.0.0")]
    public class TxtUpdaterPlugin : BaseUnityPlugin
    {
        private async void Awake()
        {
            Logger.LogInfo("PEAK Txt Updater starting...");
            try
            {
                await TxtUpdater.RunUpdateAsync(Logger);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Txt updater encountered an exception: {ex}");
            }
        }
    }
}
