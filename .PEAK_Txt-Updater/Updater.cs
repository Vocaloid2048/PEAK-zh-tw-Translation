using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;

namespace PEAK.TxtUpdater
{
    public static class TxtUpdater
    {
        // Default manifest URL: this can be overridden by editing the source or building with a different config
        private const string DefaultManifestUrl = "https://raw.githubusercontent.com/Vocaloid2048/PEAK-zh-tw-Translation/main/BepInEx/config/zh-tw-voc/Text/manifest.json";

        private const string TestMainifestUrl = "https://raw.githubusercontent.com/Vocaloid2048/PEAK-zh-tw-Translation/test-text-updater/BepInEx/config/zh-tw-voc/Text/manifest.json";
        // (No-op) keep only the RunUpdateAsync(Logger) implementation below.

        public static async Task RunUpdateAsync(Logger logger)
        {
            var log = logger ?? throw new ArgumentNullException(nameof(logger));

            string manifestUrl = TestMainifestUrl;

            try
            {
                log.LogInfo($"Fetching manifest from {manifestUrl}");
                using var http = new HttpClient();
                var manifestJson = await http.GetStringAsync(manifestUrl);
                var manifest = JsonSerializer.Deserialize<Manifest>(manifestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (manifest == null)
                {
                    log.LogWarning("Manifest was empty or could not be deserialized.");
                    return;
                }

                string localTextDir = Path.Combine(BepInEx.Paths.ConfigPath, "zh-tw-voc", "Text");
                Directory.CreateDirectory(localTextDir);

                foreach (var file in manifest.files ?? new List<ManifestFile>())
                {
                    try
                    {
                        string localPath = Path.Combine(localTextDir, file.name);
                        bool needDownload = true;
                        if (File.Exists(localPath))
                        {
                            var localSha = ComputeFileSha256(localPath);
                            if (string.Equals(localSha, file.sha256, StringComparison.OrdinalIgnoreCase))
                            {
                                needDownload = false;
                                log.LogInfo($"Up-to-date: {file.name}");
                            }
                            else
                            {
                                log.LogInfo($"Outdated: {file.name} (local {localSha} vs remote {file.sha256})");
                            }
                        }

                        if (needDownload)
                        {
                            var fileUrl = manifest.base_raw_url?.TrimEnd('/') + "/" + file.path;
                            log.LogInfo($"Downloading {file.name} from {fileUrl}");
                            using var http = new HttpClient();
                            var bytes = await http.GetByteArrayAsync(fileUrl);
                            // verify
                            var sha = ComputeSha256(bytes);
                            if (!string.Equals(sha, file.sha256, StringComparison.OrdinalIgnoreCase))
                            {
                                log.LogWarning($"SHA mismatch for {file.name}: expected {file.sha256} got {sha}. Skipping write.");
                                continue;
                            }
                            var tmp = localPath + ".tmp";
                            File.WriteAllBytes(tmp, bytes);
                            File.Copy(tmp, localPath, true);
                            File.Delete(tmp);
                            log.LogInfo($"Wrote {localPath}");
                        }
                    }
                    catch (Exception exFile)
                    {
                        log.LogError($"Failed to update {file.name}: {exFile}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"TxtUpdater failed: {ex}");
            }
        }

        private static string ComputeFileSha256(string path)
        {
            using var fs = File.OpenRead(path);
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(fs);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(data);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
