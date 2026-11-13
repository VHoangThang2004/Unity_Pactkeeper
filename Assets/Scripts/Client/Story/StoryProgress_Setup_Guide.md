# Story Progress & Battle Tutorial UI - Setup Guide (Verified Against Project)

**Scene:** `Assets/Scenes/10_StoryProgress.unity`  
**Scripts:** `StoryProgressManager.cs` + `ChapterCard.cs` + `SceneDropdownItemUI.cs` + `TutorialReplayUIController.cs` + `BattleTutorialStarter.cs`

---

## 1. Cấu trúc Prefab UI trong `Assets/Prefabs/UI/`

### 1.1. `SceneDropdownItemPrefab.prefab`
- **Root (`SceneDropdownItemPrefab`)**:
  - `RectTransform`: `Width = 640`, `Height = 64`
  - `Image` (`backgroundImage`): Màu nền hàng phân cảnh
  - `Button` (`selectButton`): Nút chọn vào màn chơi
  - `LayoutElement`: `Min Height = 60`, `Preferred Height = 64`, `Flexible Width = 1`
  - `SceneDropdownItemUI` (1 component duy nhất, đã gán đủ 5 trường):
    - `Select Button` -> `SceneDropdownItemPrefab (Button)`
    - `Scene Title Text` -> `SceneTitleText (TextMeshProUGUI)`
    - `Status Icon Text` -> `StatusIconText (TextMeshProUGUI)`
    - `Background Image` -> `SceneDropdownItemPrefab (Image)`
    - `Lock Icon Object` -> `LockIconObject (GameObject)`

### 1.2. `ChapterCardPrefab.prefab`
- **Root (`ChapterCardPrefab`)**:
  - `ChapterCard` component đã gán:
    - `Thumbnail Image` -> `ThumbnailImage`
    - `Chapter Title Text` -> `ChapterTitleText`
    - `Status Badge` -> `StatusBadge`
    - `Status Badge Text` -> `StatusBadgeText`
    - `Status Badge Background` -> `StatusBadgeBackground`
    - `Dropdown Container` -> `DropdownContainer`
    - `Scene Dropdown Item Prefab` -> `SceneDropdownItemPrefab`
    - `Lock Overlay` -> `LockOverlay`
  - Lưu ý: Khi `StoryProgressManager.HasPopupPanel == true` (mặc định trong `10_StoryProgress.unity`), bấm vào `ChapterCard` sẽ mở `PopupPanel` chứa danh sách các nút `SceneDropdownItemPrefab` bên trong `Scroll View` của Popup để không bị cắt (clip) bởi Mask của `Scroll View` ngang.

### 1.3. `ReplayBarPrefab.prefab` & Cụm `ReplayBarRoot` trong `C0_S1..C3_S2.unity`
- Trong toàn bộ 10 scene chiến đấu (`C0_S1.unity` .. `C3_S2.unity`):
  - `BattleTutorialManager` đã gắn sẵn:
    - `PlotSequencer`
    - `BattleTutorialStarter` (`tutorialTrigger` đã gán vào `BattleTutorialManager (TutorialTrigger)`)
    - `TutorialTrigger`
    - `TutorialReplayUIController` (đã gán sẵn `replayBarRoot`, `skipSceneButton`, `exitToMapButton`, `backendConfig`, `sequencer`)
  - Dưới `TutorialCanvas` đã có sẵn `ReplayBarRoot` (`m_IsActive: 0` mặc định, tự động bật khi `PlayerSession.CanSkipOrExitStory == true`), gồm 2 nút con:
    - `SkipSceneButton` (`"Skip Scene"`)
    - `ExitToMapButton` (`"Exit to Map"`)
