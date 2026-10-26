# R7 Windows native exit：non-Development 對照準備

狀態：`NONDEVELOPMENT_BUILD_VERIFIED_EXIT_CRASH_UNRESOLVED`。Windows non-Development 建置已實際完成；本文件只收斂既有證據，不授權新建置、Player 或圖形工作。

## 已知缺陷與目的

四個 owned WARP UI case──互動 Development、baseline title、`-force-gfx-direct` title 與 non-Development release title──都在正常 shutdown cleanup 後以 `0xC0000005` 結束。SDK PDB 將 fault offset 對到 `ExternalGPUProfiler::GetGameViewWindowHandle+9`；raw stack 亦出現 `PlatformAccessibilityManager::StaticDestroy+59`，但沒有可靠 unwind／dump 根因證明，不能宣稱 GPU profiler、accessibility、GfxJobs 或 R7 gameplay 是真正根因。根因仍為 `UNKNOWN`，也沒有 crash fix。

標準 Windows `BuildOptions.None` 入口已由 Unity 6000.5.4f1 實際建出 `Builds/Release/RiftForge.exe`：Unity actual exit 0、版本 `1.3.1`／build `5`、manifest `development=false`，輸出 `UnityPlayer.dll` 37,139,888 bytes、SHA-256 `03c2c29cf777f14e7512b491f14af1af570e59cd3fd1f23dffc19bfbe791a248`，與安裝的 x64 Mono non-Development SDK runtime 完全一致。建置後五個 Unity serializer 寫入已保存，並以 CAS 還原至建置前精確 bytes；runtime 25 個 C# 檔與本頁所列四個 prepared source 仍保持驗證值。

Non-Development release title 對照仍以 `0xC0000005` 結束，因此 non-Development runtime 沒有修復 native exit。它不是用移除 Development watermark 掩蓋失敗；原 crash、完整 PlayMode 3 failures／3 skips 與實機缺口仍須保留。本 Windows 變更尚未重新建置或檢查 Android，不能從 Windows 結果推定 Android 狀態。

## 八面向不擴張

- 美術、人物、場景、材質、shader：不變；不產生新圖或模型。
- 按鈕、選單與技能：沿用已驗證 R7 runtime；不改 UI、戰鬥、傷害或委託規則。
- 動作與角色外觀：不變；不重新匯入 FBX 或修改 metadata。
- 原生範圍：僅 Windows x64 Mono、`BuildOptions.None`、正式 Main scene、fresh output。

## 建置契約

1. 只允許 batch mode，Unity 必須精確為 `6000.5.4f1`。
2. Scene 必須是可由 `AssetDatabase` 讀取的 `Assets/_Project/Scenes/Main.unity`；不得使用 SampleScene。
3. `ProjectSettings`、`native/build-config.json`、`NativeExport` 與入口常數須一致為 `1.3.1`／build `5`。
4. 輸出固定為 fresh `Builds/Release/RiftForge.exe`；EXE、`RiftForge_Data` 或 `UnityPlayer.dll` 已存在即拒絕覆寫。
5. `BuildOptions.None`，不得含 `Development`；不變更任何全域 PlayerSettings。
6. Windows 不繞過 mobile release gate：`RequiresReleaseValidation(StandaloneWindows64, None)` 本來即為 false；Android／iOS 的 `readyForStore=false` gate 完全不變。
7. 建置前後均要求 installed SDK `win64_player_nondevelopment_mono/UnityPlayer.dll` 的實際 SHA-256 為 `03c2c29cf777f14e7512b491f14af1af570e59cd3fd1f23dffc19bfbe791a248`，不能只相信 flags。
8. 成功 marker 必須記錄實際 Unity version、BuildOptions、版本、SDK runtime SHA 與 output runtime SHA。

## 後續驗證邊界

已完成的 CPU／Null Windows build 與 WARP release-title 對照只證明 non-Development artifact 可建置、但同一 native exit 仍會發生。後續若要繼續，只能另立有界 native root-cause 診斷；不得混入 development DLL、關閉 accessibility、patch profiler、啟動本機 AI／GPU、替換 Unity runtime，或把 Android 視為已驗證。

Non-Development 建置成功不等於 native exit 成功，更不等於完整 UI、連續全程、真人／實機、Android、p95、正式上架、畫面 9 分或 crash 根因已修復。本輪產品驗收未完成。
