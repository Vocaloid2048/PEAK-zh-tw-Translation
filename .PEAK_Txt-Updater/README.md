# PEAK Txt Updater

這是一個用於 PEAK 專案的簡易 BepInEx 外掛（C#），可以在遊戲啟動時比對遠端 manifest 並自動下載/更新 `BepInEx/config/zh-tw-voc/Text/` 下的 .txt 檔案。

主要功能
- 從 `manifest.json` 讀取要同步的 txt 檔案清單與 SHA256
- 比對本機檔案 SHA256，若不同或缺失則下載並替換

如何使用
1. 將此專案放在 `./.PEAK_Txt-Updater`。
2. 使用 Visual Studio（或其他）開啟 `PEAK.TxtUpdater.csproj`。編譯時需要參考 BepInEx 與 UnityEngine 的 DLL（來自你的 BepInEx/game 安裝目錄）。
3. 編譯後把產生的 DLL 放到遊戲的 `BepInEx/plugins/` 下。

GitHub Action
倉庫中含有一個工作流程（`.github/workflows/update-text-manifest.yml`），會在 `BepInEx/config/zh-tw-voc/Text/` 有變更時更新 `manifest.json`，並把檔案 SHA256 一併記錄。Plugin 預設會嘗試從 raw.githubusercontent.com 上抓取 manifest（指向 `main` 分支），你可以修改 `DefaultManifestUrl` 來指向其他位置或分支。

注意事項 / 建議
- 請在 build 階段加入對 BepInEx 的參考：`BepInEx.dll`、`UnityEngine.dll`（視遊戲/環境而定）。
- 若要更保險，請加入簽名檢查或使用 HTTPS 並把 manifest 放在固定的 release 或 tag 上。
