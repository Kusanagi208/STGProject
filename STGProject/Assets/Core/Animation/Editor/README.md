# AnimationPack Editor

適用版本：Unity 6000.3.11f1，使用專案現有 Built-in Render Pipeline。

## 開啟與保存

- 選單：`Tools > Animation > Animation Pack Editor`，或雙擊 AnimationPack 資產。
- 左欄 New 建立 `.asset`；Open 或 Object 欄位載入現有 Pack。
- Clips 清單的 `+` 建立內嵌 Clip，`-` 確認後刪除，拖曳左側把手排序。
- Save、Ctrl/Cmd+S、切換 Pack 或關閉視窗都會保存該 Pack 的修改；不保存其他無關資產。
- 外部 Clip 標示 `[External]`，只能預覽；不自動搬移、修改或刪除。

## 播放與 Timeline

- Play：一律從 tick 0 開始。Pause/Resume：保留播放位置。Stop：停止並回到 tick 0。
- Ticks/s 預設 60；Speed 為播放倍率；Size 為視圖倍率，不修改動畫或 Box 資料。
- Timeline 上方刻度可定位 tick，下方區段可選取 Element；拖曳播放頭會暫停。
- `<` / `>` 單步前後移動一個 tick；數值欄可直接跳轉；Zoom 與下方捲軸控制 Timeline 視圖。
- 非循環 Clip 播放完成後停在最後有效 tick；Loop 沿用 Clip 的設定。

## Element 與 Box

- 右欄編輯 Id、Name、Loop、Sprite、Duration；Duration 是整數 tick，至少 1。
- Element 可新增、刪除、複製、排序；同一個 Element 的所有 ticks 共用其 Box 清單。
- 若需不同 tick 使用不同 Box，新增 Element 並重用相同 Sprite。
- Preview 選 Hit Box 或 Hurt Box 後拖曳建立矩形：Hit 為紅色半透明，Hurt 為藍色半透明。
- Select 可移動矩形及拖曳八個控制點縮放。Escape 取消目前拖曳，Preview 取得焦點後 Delete 刪除選取框。
- 放開滑鼠才提交拖曳，一次拖曳一筆 Undo；右欄可新增、刪除或編輯 Box 數值。
- Box 使用 Sprite pivot 為原點的本地 Unity 單位，右正上正；X/Y 為左下角，Width/Height 必須為正。
- Sprite 的 pixelsPerUnit 決定圖片在本地座標中的大小；Box 可超出 Sprite 範圍。
- 重疊時可從 Inspector 精確選取被遮住的 Box，再於 Preview 移動或縮放。

## 驗證

在 `Window > General > Test Runner > EditMode` 執行 `GenjitsuLAB.Animation.Editor.Tests`。
測試會建立唯一名稱的暫存資產資料夾，完成後自行清理；互動測試會短暫開啟測試視窗。

測試涵蓋子資產保存與 Undo/Redo、外部資產保護、Element 深複製、Runtime tick 對照、播放控制、座標換算、實際 Canvas 互動、GPU Sprite 邊界及預覽資源清理。
配置測試只驗證快取後的 tick 時脈；IMGUI、序列化介面及繪圖仍可能產生 Editor GC，不代表整個視窗零配置。

## 官方參考

- [EditorWindow](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorWindow.html)
- [AssetDatabase.AddObjectToAsset](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.AddObjectToAsset.html)
- [AssetDatabase.SaveAssetIfDirty](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.SaveAssetIfDirty.html)
- [SerializedObject](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SerializedObject.html)
- [Undo](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Undo.html)
- [Sprite](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Sprite.html)

此實作固定使用專案的 Unity 6.3 API，未加入舊版 Unity 相容層。
