# Rift Forge 3D

Unity 6000.5.4f1 的離線單機動作遊戲。R7 為 1.3.1／build 5。

操作循環：移動、讀取敵人紅圈、完美閃避接反擊、三段連擊與鍛造爆發；完美閃避留下最多三枚鍛印，爆發在舊落點造成遠端命中。波間可用碎片升級，或保留碎片直接前進。

R7 在波間鍛造加入可選委託：不接委託、相位反擊、終結連擊、鍛印命中。委託只計入造成實際生命下降的命中，完成後在波次收束時支付一次碎片；未完成不支付。委託選擇、進度與已結算獎勵有 checkpoint 邊界，舊 checkpoint 預設為不接委託。委託不取代既有鍛造，原本同一波間購買多個可負擔升級的行為仍保留。

波間 UI 同時顯示委託、三張升級卡、繼續與返回控制；橫向版委託控制高度為 72、升級卡起始 Y 為 176。

開啟專案後使用 `Assets/_Project/Scenes/Main.unity`。依賴版本在 `Packages/manifest.json`。開發建置入口為 `RiftForge.Editor.RiftDevelopmentBuild.Android`；需安裝 Unity 對應的 Android SDK／NDK／JDK。

有界驗證：EditMode 119 項通過；PlayMode 保留完整執行的 30 項通過與 3 項原始失敗，修正兩個 UI 常數後只重跑原 3 項失敗並通過。另有 3 項明確視覺／效能測試未執行。Android 與 Windows development build 均回報成功，但在 Null graphics 環境建置與測試不代表實際畫面品質、實機操作、p95、正式簽署、商店接受或整款遊戲 9 分驗收。

原創程式採 MIT。Quaternius 模型採 CC0、Noto Sans TC 採 OFL；Unity 及其套件依各自條款。詳見 `CREDITS.md`。
