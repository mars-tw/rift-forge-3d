# Rift Forge 3D

正式入口為 `Assets/_Project/Scenes/Main.unity`，Build Settings 只保留此場景。專案使用 Unity 6000.5.4f1、URP 及 Input System。R6 已整合 CC0 角色模型與骨架動畫；競技場仍是既有幾何場景，原生實機與正式商店畫面尚未驗收，不能視為可送審。

## R6（v1.3.0／build 4）

完美閃避會在原地留下最多三枚相位鍛印；鍛造爆發消耗鍛印，另對舊落點周圍的敵人造成一次遠端傷害。標題的「初鍛引導」以練習護盾及耐久對手引導完美閃避、命中反擊、鍛印爆發。完成紀錄本機保存，可重練；練習不給印記、成就或最佳分數，也不覆蓋巡行檢查點。

「波間續玩」保存每波起點與波後鍛造／購買狀態。戰鬥中重開從本波起點重來；背景自動暫停，回前景需按繼續。檢查點不保留戰鬥中的位置、連擊與鍛印。

角色由 Quaternius 的 Knight／Zombie／Viking（CC0）經 Blender 背景重設材質後輸出，使用真正骨架曲線。銅綠角色、剪角鍛牌 UI 與六角鍛印建立本作識別。既有配樂授權待補，已移出 Resources，不隨遊戲打包。原生匯出套用 RiftForge 品牌；Editor 公司／產品欄位保留原值，以免改動既有開發存檔命名空間。

只以 `-batchmode -nographics` 驗證。Blender 模型三視圖不是真實遊戲截圖；本輪仍缺兩種商店尺寸的 R6 遊戲 PNG、實機與外部 beta。最新結論見 `docs/CODEX_RESPONSE_R6_STORE.md`。

## R5 手機內容沿革

R5（v1.2.0／build 3）提供繁體中文介面及隨包附帶的 Noto Sans TC 字型。390×844 直向與 844×390 橫向使用各自的版面。左側搖桿從起觸點移動；右下長按「斬擊」，另一指按下「閃避」立即出手，冷卻結束前 0.15 秒可短暫預輸入，放開或移出可取消。旋轉與暫停會清掉既有觸控。桌面鍵盤／手把維持可用。

標題箭頭可選四波「裂隙巡行」或三波試煉：「回聲」追擊者有兩次紅圈落點；「獵陣」衝鋒者交錯進攻；「熔核」守門者帶護衛。回聲從一開始可選，獵陣／熔核分別需 1／3 枚封印印記。通關獲 1 枚印記；離線每日目標是在兩個鍛造階段各購買一次升級，完成再獲 1 枚，同一鍛造階段多次購買不重複計數。印記、每日進度與三項成就存於本機 PlayerPrefs。每日依裝置日期刷新，沒有伺服器時間保證。驗證與缺件見 `docs/CODEX_RESPONSE_R5_MOBILE.md`。

## 玩法

主畫面點選 「進入裂隙」 開始。前三個裂隙依序出現 3、4、5 名敵人；最後一關為 Rift Warden。玩家可以移動、近距離斬擊、閃避敵人預告的紅色攻擊範圍。敵人死亡後會掉落金色素材，素材自動往玩家移動，全部收集後才進入鍛造畫面。每波之間可用素材強化武器傷害、生命上限或閃避冷卻，也可以保留素材直接前進。擊敗並收集最後 Boss 的掉落物才算勝利；生命歸零會進入失敗畫面。勝負兩種結局均可重新開始。

R4（v1.1.0）新增招牌玩法「相位反擊 → 鍛造爆發」：斬擊有蓄力、命中、收招三段，0.75 秒內連按三下會打出範圍更大的 Forge Finisher；敵人紅圈將落下前 0.4 秒內在圈中閃避，算完美閃避（慢動作＋下一擊 COUNTER 雙倍傷害）；命中與完美閃避會累積鍛造能量，滿格後下一擊變成環形 Forge Burst。第 2 波起出現直線衝鋒的 Rift Lancer，Warden 半血後狂暴三連砸。每局結束依時間、擊殺、連擊、完美閃避與受傷算出 S～D 評等，本機保存最佳紀錄。音效全部在執行期合成；戰鬥配樂放在 `Resources/Audio/rift_battle_loop.ogg`，來源見 `docs/evidence/r4/audio-manifest.json`。詳見 `docs/R4_FUN_AND_POLISH_REPORT.md`。

- 桌面：WASD／左搖桿移動；Space 或 J／手把南鍵斬擊；左 Shift 或 K／手把東鍵閃避；Esc／Start 暫停。
- 手機／平板：左側浮動移動區拖曳；右側「斬擊」、「閃避」按鈕；右上角暫停。uGUI 使用 Input System 的多指事件，UI 依 `Screen.safeArea` 配置。
- 離線單機：無網路、帳號、廣告、內購或使用者資料傳輸。

## 程式邊界

- `Scripts/Runtime/RiftRunState.cs`：四波、生命、素材與三條鍛造線的純邏輯；不依賴 Unity API。
- `Scripts/Runtime/RiftGame.cs`：場景合成、玩家戰鬥、戰利品、波次與短暫視覺效果。
- `Scripts/Runtime/RiftEnemy.cs`：追擊、攻擊預告、出手、收招與死亡。
- `Scripts/Runtime/RiftHud.cs`：主畫面、戰鬥 HUD、鍛造、暫停和結局 UI。
- 原有 `PlayerController`、`TouchMoveArea`、`TouchPlayerInputSource` 和 `CameraRig` 繼續使用；原觸控設計細節見 `Scripts/Runtime/TOUCH_INPUT.md`。

## 早期驗證紀錄與上架狀態（當前結果見 R5 報告）

已用 Unity 隨附 Roslyn 與目前專案的 Unity／Input System／uGUI DLL 對 Runtime、Editor 與 PlayMode 測試原始碼做獨立 C# 編譯。純邏輯的暫存 .NET harness 已執行並通過主畫面、四波推進、掉落收集、鍛造成本、Boss 勝利、重開與失敗檢查。這些檢查無法替代 Unity 匯入、Test Runner、實際遊戲操作或手機效能測試。

使用者已於 Unity Hub 自行完成授權條款及授權啟用；Unity 6000.5.4f1 的正式匯入已成功。Unity Test Runner 的 **EditMode 81／81** 與 **PlayMode 4／4** 全數通過，PlayMode 包含四波與 Boss 勝利、鍛造、失敗重開、閃避無敵，以及觸控元件的暫停／焦點／停用生命週期。原始碼提交前重新執行，仍分別通過 81／81 與 4／4；結果 XML 位於 `Logs/rift-editmode-source-commit-20261013.xml`、`Logs/rift-playmode-source-commit-20261013.xml`（`Logs/` 為本機忽略目錄）。

另以 Unity PlayMode 的 URP 離屏渲染，在 1280 × 720 畫面檢查八個實際狀態：主畫面、第一波戰鬥、暫停、第一波鍛造、購買武器升級、第二波失敗、Boss 戰鬥及勝利。八張圖與測試收據留在本機 `Builds/Validation/VisualQA/`，擷取測試通過 1／1。初次檢查發現暫停畫面的「RESTART RUN」按鈕對比不足；改成金色按鈕後，重新擷取的暫停畫面測試通過 1／1，原有 PlayMode 測試也再次通過 4／4。這是 Editor 的離屏渲染檢查，尚未驗證現有 Windows EXE 的實際畫面或手機實機顯示。

尚無 Android／iOS 原生建置與實機畫面、操控、安全區、效能驗證。因此 `native/build-config.json` 保持 `sourceStage: gameplay-unverified`、`readyForStore: false`，release gate 仍會阻擋正式匯出。完成原生建置與目標裝置的完整回合 QA 後，再評估調整 gate 與產出可送審版本。

本機 Windows Development Player 已產生在 `Builds/Validation/RiftForge-dev.exe`。Android Development Player 要從 ASCII 專案路徑建置；目前 `C:/Users/digimkt/Desktop/RiftForgeBuild` 是指向本專案的 junction。以此入口重試後，Unity 越過路徑檢查，但因 Editor 內缺少 OpenJDK、Android SDK 與 NDK 而停止。後續需在 Unity Hub 的 **Installs → 6000.5.4f1 → Manage → Add modules** 確認完整安裝 **Android Build Support**、**Android SDK & NDK Tools**、**OpenJDK**，並處理由安裝器實際顯示的條款。詳見 `plan/feature-rift-combat-forging-1-validation.md`。
