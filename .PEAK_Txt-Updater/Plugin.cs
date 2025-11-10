using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace PeakTxtUpdater;

/**
 * PEAK Text Updater Plugin
 * 
 * This BepInEx plugin automatically updates text files for the PEAK game (Gale BepInEx folder)
 * from a specified GitHub repository manifest.
 * 
 * It fetches a JSON manifest that lists files, their paths, and SHA256 hashes,
 * then downloads and replaces local files if they are missing or outdated.
 * 
 * This plugin was developed by Vocaloid2048 with AI help.
 */

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin {

    // Default manifest URL: this can be overridden by editing the source or building with a different config
    private const string DefaultManifestUrl = "https://raw.githubusercontent.com/Vocaloid2048/PEAK-zh-tw-Translation/main/BepInEx/config/zh-tw-voc/Text/manifest.json";
    private const string TestMainifestUrl = "https://raw.githubusercontent.com/Vocaloid2048/PEAK-zh-tw-Translation/test-text-updater/BepInEx/config/zh-tw-voc/Text/manifest.json";
    private static ConfigEntry<bool> configIsEnablePlugin;

    // 取得 BepInEx 的 Logger
    internal static ManualLogSource Log;

    private void Awake() {
        Log = Logger;
        Log.LogInfo("PeakTxtUpdater init - reading config.");

        // Config: Enable Plugin
        configIsEnablePlugin = ((BaseUnityPlugin)this).Config.Bind<bool>("PeakTxtUpdater", "Is Enable PeakTxtUpdater", true, "若啟用，則允許插件在啟動遊戲時自動更新繁體中文翻譯。");

        // Run updater if enabled
        if (!configIsEnablePlugin.Value) { Log.LogInfo("PeakTxtUpdater is DISABLED in config.");  return;  }

        var task = RunUpdaterAsync(DefaultManifestUrl);
        task.ContinueWith(t => {
            if (t.IsFaulted) {
                Log.LogError($"RunUpdaterAsync failed: {t.Exception}");
            }
        }, TaskScheduler.Default);
    }
    private async Task RunUpdaterAsync(string manifestUrl) {
        Log.LogInfo("RunUpdaterAsync Task Started");
        if (Log == null) Log = Logger;
        Manifest manifest = null;

        try {
            // Fetch manifest from GitHub repo
            Log.LogInfo($"Fetching manifest from {manifestUrl}");
            using var http = new HttpClient();
            var manifestJson = await http.GetStringAsync(manifestUrl);

            if (string.IsNullOrEmpty(manifestJson)) {
                Log.LogWarning("Manifest JSON is empty.");
                return;
            }

            // Deserialize manifest
            try {
                var settings = new JsonSerializerSettings {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,
                    DateParseHandling = DateParseHandling.None
                };
                manifest = JsonConvert.DeserializeObject<Manifest>(manifestJson, settings);
                Log.LogInfo("Manifest deserialized");
            } catch (JsonException jex) {
                Log.LogError($"Failed to deserialize manifest: {jex}");
                
                if (!string.IsNullOrEmpty(manifestJson)) {
                    var snippet = manifestJson.Length > 1000 ? manifestJson.Substring(0, 1000) + "..." : manifestJson;
                    Log.LogError($"Manifest snippet: {snippet}");
                }
                return;
            }

            if (manifest == null) {
                Log.LogWarning("Manifest was empty or could not be deserialized.");
                return;
            }

            string localTextDir = Path.Combine(BepInEx.Paths.ConfigPath, "zh-tw-voc", "Text");
            Directory.CreateDirectory(localTextDir);

            foreach (var file in manifest.files ?? new List<ManifestFile>()) {
                try {
                    string localPath = Path.Combine(localTextDir, file.name);
                    bool needDownload = true;
                    if (File.Exists(localPath)) {
                        // Exclude Version.txt in this comparison
                        if (string.Equals(file.name, "Version.txt", StringComparison.OrdinalIgnoreCase)) { needDownload = false; continue; }

                        // Otherwise, compare SHA256
                        var localSha = ComputeFileSha256(localPath);
                        if (string.Equals(localSha, file.sha256, StringComparison.OrdinalIgnoreCase)) {
                            needDownload = false;
                        } else {
                            Log.LogInfo($"Outdated: {file.name} (local {localSha} vs remote {file.sha256})");
                        }
                    }

                    if (needDownload) {
                        var fileUrl = manifest.base_raw_url?.TrimEnd('/') + "/" + file.path;
                        Log.LogInfo($"Downloading {file.name} from {fileUrl}");
                        var bytes = await http.GetByteArrayAsync(fileUrl);

                        // verify
                        var sha = ComputeSha256(bytes);
                        if (!string.Equals(sha, file.sha256, StringComparison.OrdinalIgnoreCase)) {
                            Log.LogWarning($"SHA mismatch for {file.name}: expected {file.sha256} got {sha}. Skipping write.");
                            continue;
                        }
                        var tmp = localPath + ".tmp";
                        File.WriteAllBytes(tmp, bytes);
                        File.Copy(tmp, localPath, true);
                        File.Delete(tmp);
                        Log.LogInfo($"Wrote {localPath}");
                    }
                } catch (Exception exFile) {
                    Log.LogError($"Failed to update {file.name}: {exFile}");
                }
            }

            // Finally, write/update Version.txt
            try {
                string versionPath = Path.Combine(localTextDir, "Version.txt");
                string versionContent = $"##Ref: {manifest.@ref}##\n"
                    +$"##Commit: {manifest.commit}##\n"
                    +$"##Generated at: {manifest.generated_at}##\n"
                    +"\n"
                    +$"sr:\"^v.(\\d+).(\\d+).(.+)$\"=v.$1.$2.$3 (夜芷冰繁中翻譯 - #{manifest.commit.Substring(0,6)})\n"
                    +$"sr:\"^beta.(\\d+).(\\d+).(.+)$\"=beta.$1.$2.$3 (夜芷冰繁中翻譯 - #{manifest.commit.Substring(0, 6)})\n";
                File.WriteAllText(versionPath, versionContent, Encoding.UTF8);
                Log.LogInfo($"Wrote version info to {versionPath}");
            } catch (Exception exVersion) {
                Log.LogError($"Failed to write Version.txt: {exVersion}");
            }
        } catch (Exception ex) {
            Log.LogError($"TxtUpdater failed: {ex}");
        }
    }

    private static string ComputeFileSha256(string path) {
        using var fs = File.OpenRead(path);
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(fs);
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    private static string ComputeSha256(byte[] data) {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(data);
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    public class Manifest {
        public string generated_at { get; set; }
        public string @ref { get; set; }
        public string commit { get; set; }
        public string base_raw_url { get; set; }
        public List<ManifestFile> files { get; set; }
    }

    public class ManifestFile {
        public string name { get; set; }
        public string path { get; set; }
        public string sha256 { get; set; }
    }
}