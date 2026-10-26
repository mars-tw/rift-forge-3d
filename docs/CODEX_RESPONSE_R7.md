# R7 波間委託公開更新說明

## 功能範圍

R7 在既有戰鬥、收集與鍛造循環內增加四種波間選擇：不接委託、相位反擊、終結連擊、鍛印命中。三種委託分別要求一次真實反擊命中、兩次真實終結命中，或兩次造成生命下降的遠端鍛印命中；完成獎勵分別為 4、5、6 碎片。

- 空揮、錯誤攻擊種類、錯誤階段、初鍛流程與未造成生命下降的鍛印不計進度。
- 進度封頂；獎勵只在波次收束後支付一次，失敗或未完成不支付。
- 下一波選擇、目前委託、進度與已結算獎勵受 checkpoint 驗證；舊版 checkpoint 以無委託載入。
- 委託選擇不扣碎片，也不限制既有鍛造多次購買。
- 波間與戰鬥 HUD 顯示目前規則、進度與上一波結果。

## 公開 source 差異

- 6 個 runtime 檔：checkpoint、遊戲流程、實際命中接線、波間／戰鬥 HUD 與 run state。
- 2 個新測試檔及各自 `.meta`：規則、真實傷害路徑、checkpoint、一次性獎勵、鍛造多次購買及四種 viewport 控制可達性。
- `ProjectSettings/ProjectSettings.asset` 與 `native/build-config.json`：版本 1.3.1／build 5。
- 沒有新增或修改模型、材質、場景、貼圖或角色匯入 metadata。

## 有界驗證

| 項目 | 結果 | 邊界 |
|---|---:|---|
| EditMode | 119 passed，0 failed，0 skipped | Null graphics |
| 完整 PlayMode 原始執行 | 30 passed，3 failed，3 skipped | 不重分類為完整通過 |
| 聚焦重跑 | 原 3 個 UI failure 全部通過 | 僅修正橫向委託高度 48→72、升級卡 Y 150→176 |
| 明確未執行 | 3 個 visual／frame-time tests | 沒有新圖像或效能接受證據 |
| Android development build | exit 0；49,113,363 bytes；SHA-256 `ae7a54a28ab460f5e2c2b4199e219fac2ad390c37a5c822575605440d1635368` | Debug 簽章、未做實機驗收 |
| Windows development ZIP | exit 0；75,730,632 bytes；SHA-256 `885fe06506e7d89e337f13420d1fd10e0daa0029eda292da2f5f1cd6d70219ed` | 未做實際畫面或長時間操作驗收 |

PlayMode 證據的正確口徑是「30 項既有通過加 3 項修正後聚焦通過」。原完整 PlayMode 的 3 個 failure 與 3 個 explicit skip 都保留，不能改稱單一完整 PlayMode pass。

## 未主張事項

Null graphics 測試與 development build 不證明畫面品質、實機相容性、裝置 p95、production signing、商店接受或整款遊戲達到 9 分。R7 本次公開的是有界 source 更新與其測試／建置狀態，不是完整產品驗收。
