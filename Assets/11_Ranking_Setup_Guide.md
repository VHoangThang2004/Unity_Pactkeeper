# Scene 11_Ranking — Unity Setup Guide

Hướng dẫn từng bước để tạo scene Ranking trong Unity Editor.

---

## Tổng quan Scene

**Scene 11_Ranking** gồm 2 panel chính:
- **Left Panel:** Danh sách leaderboard (scroll vertical)
- **Right Panel:** Chi tiết rank của người chơi được chọn

**Mockup reference:** Xem hình người dùng cung cấp — left panel có list entries với header (rank position, username, level, rank badge, score), right panel có UI chi tiết rank với frame.

---

## Bước 1: Tạo Scene mới

1. **File → New Scene** → chọn **2D**
2. **Save As:** `Assets/Scenes/11_Ranking.unity`
3. **Build Settings (Ctrl+Shift+B):**
   - Add Open Scenes → kéo `11_Ranking` vào list
   - Đảm bảo scene index đúng thứ tự

---

## Bước 2: Canvas Setup

### 2.1. Tạo Canvas chính

1. **Hierarchy → Right-click → UI → Canvas**
2. Rename thành `RankingCanvas`
3. **Canvas component:**
   - Render Mode = **Screen Space - Overlay**
   - Pixel Perfect = **tích** (cho pixel art sharp)
   - Canvas Scaler:
     - UI Scale Mode = **Scale With Screen Size**
     - Reference Resolution = **1920 × 1080** (hoặc 1280×720)
     - Match = **0.5** (balance width/height)

### 2.2. Background Image

1. **RankingCanvas → Right-click → UI → Image**
2. Rename: `Background`
3. **RectTransform:** Stretch full (Anchor = stretch all)
4. **Image component:**
   - Source Image = gán sprite background từ `11_Ranking_UI_Prompts.md` (sau khi generate)
   - Image Type = **Simple**
   - Raycast Target = **bỏ tích** (không cần click)

---

## Bước 3: Title và Header

### 3.1. Title "Ranking"

1. **RankingCanvas → UI → Text - TextMeshPro**
2. Rename: `TitleText`
3. **RectTransform:**
   - Anchor = Top Center
   - Pos X = 0, Pos Y = -50
   - Width = 600, Height = 100
4. **TextMeshPro component:**
   - Text = "**Ranking**"
   - Font Size = 72
   - Alignment = Center, Middle
   - Color = trắng hoặc vàng (tùy background)

### 3.2. Subtitle "Mùa bao nhiêu?"

1. Duplicate `TitleText` → rename: `SubtitleText`
2. **RectTransform:** Pos Y = -120
3. **TextMeshPro:**
   - Text = "Mùa bao nhiêu ?" (hoặc "Season 1")
   - Font Size = 32
   - Color = xám nhạt

---

## Bước 4: Left Panel — Leaderboard List

### 4.1. Panel trái (container)

1. **RankingCanvas → UI → Panel**
2. Rename: `LeftPanel`
3. **RectTransform:**
   - Anchor = Left Stretch (left middle vertical stretch)
   - Pos X = 20, Pos Y = 0
   - Width = **700**, Height = **800** (adjust tùy design)
   - Pivot = (0, 0.5)
4. **Image component:**
   - Color = đen trong suốt `rgba(0, 0, 0, 0.6)` hoặc gán sprite khung panel
   - Raycast Target = **tích**

### 4.2. Header Row (Tên cột: rank, username, level, rank badge, score)

1. **LeftPanel → UI → Panel**
2. Rename: `HeaderRow`
3. **RectTransform:**
   - Anchor = Top Stretch
   - Pos Y = -10
   - Height = **60**
4. **Image:** Color = xám đậm `rgba(50, 50, 50, 0.8)`

**Tạo 5 TextMeshPro trong HeaderRow:**
- **RankHeaderText:** "#" — Pos X = -300 (trái)
- **UsernameHeaderText:** "Username" — Pos X = -150
- **LevelHeaderText:** "Level" — Pos X = 0
- **RankBadgeHeaderText:** "Rank Badge" — Pos X = 100
- **RankNameHeaderText:** "Rank Name" — Pos X = 200
- **ScoreHeaderText:** "Score" — Pos X = 300 (phải)

Font size = 24, color = trắng, alignment = center middle

### 4.3. Scroll View (danh sách entries)

1. **LeftPanel → UI → Scroll View**
2. Rename: `LeaderboardScrollView`
3. **RectTransform:**
   - Anchor = Stretch (fill panel trừ header)
   - Top = **-80** (dưới header)
   - Bottom = **10**
   - Left = **10**, Right = **10**
4. **Scroll Rect component:**
   - Horizontal = **bỏ tích**
   - Vertical = **tích**
   - Movement Type = Elastic
   - Scrollbar Visibility = Auto Hide
5. **Xóa `Viewport → Content → Scrollbar Horizontal`** (không cần)

### 4.4. Content (container cho entry cards)

1. Chọn `LeaderboardScrollView → Viewport → Content`
2. **RectTransform:**
   - Anchor = Top Stretch
   - Pivot = (0.5, 1) — top center
   - Pos Y = 0
   - Height = **2000** (sẽ tự động expand theo số entries)
3. **Add Component → Layout Group → Vertical Layout Group:**
   - Control Child Size Height = **tích**
   - Child Force Expand Height = **bỏ tích**
   - Spacing = **5** (khoảng cách giữa các entry)
4. **Add Component → Content Size Fitter:**
   - Vertical Fit = **Preferred Size** (auto resize height)

### 4.5. Button "Xem lại lịch sử đấu"

1. **LeftPanel → UI → Button - TextMeshPro**
2. Rename: `ViewHistoryButton`
3. **RectTransform:**
   - Anchor = Bottom Center
   - Pos Y = **-20** (phía dưới ScrollView)
   - Width = 300, Height = 50
4. **Button component:**
   - Interactable = **tích**
   - OnClick = sẽ wire sau (RankingManager.OnViewHistoryClick)
5. **Child Text:**
   - Text = "Xem lại lịch sử đấu"
   - Font Size = 20

---

## Bước 5: Right Panel — Player Detail

### 5.1. Panel phải (container)

1. **RankingCanvas → UI → Panel**
2. Rename: `PlayerDetailPanel`
3. **RectTransform:**
   - Anchor = Right Stretch
   - Pos X = -20, Pos Y = 0
   - Width = **600**, Height = **800**
   - Pivot = (1, 0.5)
4. **Image component:**
   - Color = đen trong suốt `rgba(0, 0, 0, 0.6)`

### 5.2. Title "Chi tiết rank của người đó"

1. **PlayerDetailPanel → UI → Text - TextMeshPro**
2. Rename: `DetailTitleText`
3. **RectTransform:**
   - Anchor = Top Center
   - Pos Y = -30
   - Width = 500, Height = 60
4. **TextMeshPro:**
   - Text = "Player Rank Detail"
   - Font Size = 28
   - Alignment = Center

### 5.3. Rank Badge Image (large, clickable)

1. **PlayerDetailPanel → UI → Image**
2. Rename: `DetailRankBadgeImage`
3. **RectTransform:**
   - Anchor = Top Center
   - Pos Y = -150
   - Width = **200**, Height = **200**
4. **Image component:**
   - Preserve Aspect = **tích**
   - Raycast Target = **TÍCH** (để clickable)
   - Source Image = sẽ gán runtime từ RankingManager
5. **Note:** Button component sẽ được add runtime bởi RankingManager để mở popup

### 5.4. Rank Name Text

1. **PlayerDetailPanel → UI → Text - TextMeshPro**
2. Rename: `DetailRankNameText`
3. **RectTransform:**
   - Anchor = Top Center
   - Pos Y = -380
   - Width = 400, Height = 60
4. **TextMeshPro:**
   - Text = "Pactkeeper" (placeholder)
   - Font Size = 32
   - Alignment = Center
   - Color = vàng hoặc tím (tùy theme)

### 5.5. Score Frame (khung điểm số)

1. **PlayerDetailPanel → UI → Image**
2. Rename: `ScoreFrameImage`
3. **RectTransform:**
   - Anchor = Center
   - Pos Y = -520
   - Width = **400**, Height = **200**
4. **Image component:**
   - Source Image = gán sprite ScoreFrame từ prompts
   - Image Type = **Sliced** (nếu dùng 9-slice) hoặc **Simple**
   - Raycast Target = **bỏ tích**

### 5.6. Score Text (trong frame)

1. **ScoreFrameImage → UI → Text - TextMeshPro** (child của frame)
2. Rename: `DetailScoreText`
3. **RectTransform:**
   - Anchor = Center
   - Stretch to fill parent (với padding)
4. **TextMeshPro:**
   - Text = "**3500**" (placeholder)
   - Font Size = 48
   - Alignment = Center, Middle
   - Color = vàng hoặc trắng
   - Font Style = Bold

### 5.7. Position Text

1. **PlayerDetailPanel → UI → Text - TextMeshPro**
2. Rename: `DetailPositionText`
3. **RectTransform:**
   - Anchor = Bottom Center
   - Pos Y = 30
   - Width = 200, Height = 40
4. **TextMeshPro:**
   - Text = "#1" (placeholder)
   - Font Size = 24
   - Alignment = Center

---

## Bước 6: Prefab — LeaderboardEntryCard

### 6.1. Tạo prefab mới

1. **Hierarchy → UI → Panel** (tạo tạm trong scene)
2. Rename: `LeaderboardEntryCard`
3. **RectTransform:**
   - Width = **700**, Height = **60**
4. **Image component:** Color = `rgba(30, 30, 30, 0.8)` (nền tối)

### 6.2. Thêm UI elements vào card

**Tạo 6 child elements:**

1. **RankPositionText** (TextMeshPro)
   - Pos X = -300 (trái), Width = 60, Height = 60
   - Text = "1", Font Size = 24, Center

2. **UsernameText** (TextMeshPro)
   - Pos X = -150, Width = 150, Height = 60
   - Text = "PlayerName", Font Size = 20, Left align

3. **LevelText** (TextMeshPro)
   - Pos X = 0, Width = 80, Height = 60
   - Text = "Lv 15", Font Size = 18, Center

4. **RankBadgeImage** (Image)
   - Pos X = 100, Width = 48, Height = 48
   - Preserve Aspect = tích

5. **RankNameText** (TextMeshPro)
   - Pos X = 200, Width = 120, Height = 60
   - Text = "Pactkeeper", Font Size = 18, Center

6. **ScoreText** (TextMeshPro)
   - Pos X = 300, Width = 100, Height = 60
   - Text = "3500", Font Size = 20, Right align

### 6.3. Add Button component

1. Chọn `LeaderboardEntryCard` root GameObject
2. **Add Component → Button**
3. **Button component:**
   - OnClick → sẽ wire runtime bởi LeaderboardEntryCard.cs

### 6.4. Add Script component

1. **Add Component → Script → LeaderboardEntryCard** (script đã tạo)
2. **Wire Inspector fields:**
   - Rank Position Text → kéo `RankPositionText`
   - Username Text → kéo `UsernameText`
   - Level Text → kéo `LevelText`
   - Rank Badge Image → kéo `RankBadgeImage`
   - Rank Name Text → kéo `RankNameText`
   - Score Text → kéo `ScoreText`
   - Background Image → kéo `LeaderboardEntryCard` (self Image)
   - Normal Color → `rgba(30, 30, 30, 0.8)`
   - Highlight Color → `rgba(50, 50, 70, 0.9)`
   - Top Three Color → `rgba(70, 50, 20, 0.9)` (gold tint)

### 6.5. Tạo Prefab

1. Kéo `LeaderboardEntryCard` GameObject từ Hierarchy vào folder:
   - `Assets/Prefabs/Ranking/LeaderboardEntryCard.prefab`
2. Xóa instance trong Hierarchy (prefab đã lưu)

---

## Bước 7: RankingManager GameObject

### 7.1. Tạo Manager

1. **Hierarchy → Create Empty**
2. Rename: `RankingManager`
3. **Transform:** Reset (0, 0, 0)
4. **Add Component → Script → RankingManager**

### 7.2. Wire Inspector fields

**Config:**
- **Backend Config:** kéo ScriptableObject `ClientBackendConfig` từ `Assets/Data/Client/`
- **Scene Config:** kéo `SceneConfig` từ `Assets/Data/Shared/`

**Left Panel — Leaderboard List:**
- **Leaderboard Container:** kéo `LeftPanel → LeaderboardScrollView → Viewport → Content`
- **Entry Card Prefab:** kéo prefab `LeaderboardEntryCard` từ `Assets/Prefabs/Ranking/`
- **Leaderboard Scroll Rect:** kéo `LeaderboardScrollView`
- **Loading Indicator:** tạo GameObject với spinner/loading text (optional)
- **Empty State:** tạo GameObject với text "No data" (optional)

**Right Panel — Player Detail:**
- **Player Detail Panel:** kéo `PlayerDetailPanel`
- **Detail Rank Badge Image:** kéo `DetailRankBadgeImage` (sẽ tự động add Button runtime)
- **Detail Rank Name Text:** kéo `DetailRankNameText`
- **Detail Score Text:** kéo `DetailScoreText`
- **Detail Position Text:** kéo `DetailPositionText`

**Rank Badge Sprites (7 tiers + Unranked):**
- **Size = 7**
- **Element 0:** RankBadge_0_Beginner
- **Element 1:** RankBadge_1_Master
- **Element 2:** RankBadge_2_Captain
- **Element 3:** RankBadge_3_Pactkeeper
- **Element 4:** RankBadge_4_Warlord
- **Element 5:** RankBadge_5_GrandCommander
- **Element 6:** RankBadge_6_Union
- **Unranked Badge Sprite:** RankBadge_Unranked

**Buttons:**
- **Back Button:** tạo Button ở top-left góc scene, wire `OnClick()` → `RankingManager.OnBackClick`
- **View History Button:** kéo `LeftPanel → ViewHistoryButton`, wire → `RankingManager.OnViewHistoryClick`

**Rank Tier Info Popup:**
- **Rank Tier Popup Panel:** kéo `RankTierPopupPanel`
- **Rank Tier Card Container:** kéo `RankTierPopupPanel → PopupContentBackground → RankTierScrollView → Viewport → Content`
- **Rank Tier Card Prefab:** kéo prefab `RankTierCard`
- **Rank Tier Popup Close Button:** kéo `PopupContentBackground → CloseButton`

**Story Completion Check:**
- **Has Completed Story:** bỏ tích (false) — sẽ check qua API sau

---

## Bước 8: Loading & Empty State (Optional)

### 8.1. Loading Indicator

1. **RankingCanvas → UI → Panel**
2. Rename: `LoadingIndicator`
3. Thêm child: **TextMeshPro** với text "Loading..."
4. Hoặc thêm **spinning icon** (Image với animation)
5. **Active = false** trong Inspector (RankingManager sẽ bật khi load)

### 8.2. Empty State

1. **RankingCanvas → UI → Panel**
2. Rename: `EmptyState`
3. Thêm child: **TextMeshPro** với text "No leaderboard data available"
4. **Active = false** trong Inspector

---

## Bước 9: Wire Main Menu Button

### 9.1. Thêm button vào MainMenu scene

1. Mở scene `3_MainMenu`
2. Tìm panel chứa các navigation buttons (Unit List, Inventory, Gacha, Story, etc.)
3. Duplicate một button có sẵn → Rename: `RankingButton`
4. **Button text:** "Ranking" hoặc icon rank
5. **Button OnClick():**
   - Target Object = `MainMenuManager`
   - Function = `MainMenuManager.GoToRanking()`

### 9.2. Test navigation

1. Play scene `3_MainMenu`
2. Click button Ranking
3. Scene 11_Ranking phải load thành công

---

## Bước 10: Test Scene 11_Ranking

### 10.1. Test flow

1. **Play scene 11_Ranking directly**
2. Kiểm tra:
   - Loading indicator hiển thị → biến mất sau khi fetch xong
   - Left panel hiển thị current player entry (Unranked) + 7 example entries cho mỗi tier
   - Current player entry được highlight (yellow outline)
   - Entry cards hiển thị: position, username, level, rank badge, **rank name**, score
   - Click vào entry → Right panel hiển thị chi tiết (badge, rank name, score, position)
   - **Click vào rank badge trong right panel** → Popup horizontal scroll mở
   - Popup hiển thị 8 cards: Unranked + 7 rank tiers (Beginner → Union)
   - Mỗi card hiển thị: badge, rank name, min score, lore description
   - **Lore bị locked** (overlay hiển thị "Complete Story to Unlock") vì hasCompletedStory = false
   - Close popup button hoạt động
   - Back button → về Main Menu
   - View History button → log "not implemented"

### 10.2. Debug checks

Nếu có lỗi:
- **Empty leaderboard:** Check console log, có thể API `/api/PlayerProfile` fail
- **No sprites on badges:** Check RankingManager Inspector, 7 sprites + Unranked sprite đã gán chưa
- **Layout broken:** Check HorizontalLayoutGroup (popup) và VerticalLayoutGroup (left panel) settings
- **Click không hoạt động:** Check Button component và Raycast Target
- **Popup không mở:** Check detailRankBadgeImage có Raycast Target = tích, Button component được add runtime
- **Lore không lock:** Check RankingManager Inspector → hasCompletedStory phải = false
- **Rank name không hiển thị:** Check LeaderboardEntryCard Inspector → rankNameText field đã wire chưa

---

## Bước 11: Polish (Optional)

### 11.1. Animations

- **Entry card hover:** Thêm EventTrigger component vào prefab, wire `OnPointerEnter`/`OnPointerExit` vào `LeaderboardEntryCard.cs`
- **Panel fade in:** Thêm Animator vào panels, tạo fade-in animation khi scene load

### 11.2. Audio

- **Button click sound:** Wire audio clip vào buttons
- **Scene BGM:** Thêm AudioSource vào scene với track "ranking hall theme"

### 11.3. Particle effects

- **Rank badge glow:** Thêm particle effect vào badge tier 6 (divine tier) cho cảm giác prestige

---

## Checklist Setup hoàn chỉnh

- [ ] Scene `11_Ranking.unity` đã tạo và add vào Build Settings
- [ ] Canvas với background sprite
- [ ] Left Panel với ScrollView + Header row (6 cột: #, Username, Level, Badge, Rank Name, Score) + ViewHistoryButton
- [ ] Right Panel với rank badge (clickable), rank name, score frame, position (NO username/level)
- [ ] Prefab `LeaderboardEntryCard` với 6 UI elements + script
- [ ] Prefab `RankTierCard` với badge, rank name, min score, lore description, locked overlay + script
- [ ] Popup `RankTierPopupPanel` với Horizontal ScrollView
- [ ] GameObject `RankingManager` với tất cả fields wired
- [ ] 7 rank badge sprites + 1 Unranked sprite imported và gán vào RankingManager
- [ ] Scene background sprite imported và gán vào Canvas
- [ ] Score frame sprite imported và gán vào Right Panel
- [ ] MainMenu button "Ranking" wire `GoToRanking()`
- [ ] Test: Play scene 11_Ranking → leaderboard render → click entry → detail hiển thị
- [ ] Test: Click rank badge → popup mở → 8 cards (Unranked + 7 tiers) → lore locked
- [ ] Test: Back button → về Main Menu

---

## Troubleshooting

### Lỗi: "NullReferenceException" khi load scene

**Nguyên nhân:** RankingManager thiếu reference  
**Fix:** Check Inspector, đảm bảo tất cả fields màu hồng (missing) được gán

### Lỗi: Scroll View không scroll được

**Nguyên nhân:** Content height quá nhỏ hoặc ScrollRect settings sai  
**Fix:**
- Content phải có `Content Size Fitter` với Vertical Fit = Preferred Size
- Content phải có `Vertical Layout Group`
- ScrollRect phải có Vertical = tích

### Lỗi: Rank badge không hiển thị

**Nguyên nhân:** Sprites chưa import đúng cách  
**Fix:**
- Import sprites với Texture Type = Sprite (2D and UI)
- Filter Mode = Point (no filter)
- Gán đúng thứ tự index 0-6 trong RankingManager Inspector

### Lỗi: API call fail

**Nguyên nhân:** Backend server chưa chạy hoặc PlayerSession.Token invalid  
**Fix:**
- Start backend server trước
- Login qua scene 1_Login để có valid token
- Check console log để xem error message từ UnityWebRequest

---

**File này:** Lưu tại `Assets/11_Ranking_Setup_Guide.md`

**Next steps sau khi setup xong:**
1. Generate assets từ `11_Ranking_UI_Prompts.md`
2. Import assets vào Unity
3. Follow guide này để wire scene
4. Test flow end-to-end
5. Khi backend team làm xong API leaderboard thật → replace mock data logic trong `RankingManager.cs`


---

## Bước 8: Rank Tier Info Popup (Horizontal Scroll)

### 8.1. Popup Panel

1. **RankingCanvas → UI → Panel**
2. Rename: `RankTierPopupPanel`
3. **RectTransform:** Stretch full (Anchor = stretch all)
4. **Image:** Color = `rgba(0, 0, 0, 0.8)` (dark overlay)
5. **Active = false** trong Inspector

### 8.2. Popup Content Background

1. **RankTierPopupPanel → UI → Panel**
2. Rename: `PopupContentBackground`
3. **RectTransform:**
   - Anchor = Center
   - Width = 1200, Height = 700
4. **Image:** Color = `rgba(20, 20, 20, 0.95)` hoặc gán popup background sprite

### 8.3. Close Button

1. **PopupContentBackground → UI → Button**
2. Rename: `CloseButton`
3. **RectTransform:**
   - Anchor = Top Right
   - Pos X = -20, Pos Y = -20
   - Width = 60, Height = 60
4. **Button:** OnClick → wire RankingManager.CloseRankTierPopup
5. **Child Text:** "X" (font size 32)

### 8.4. Title

1. **PopupContentBackground → UI → Text - TextMeshPro**
2. Rename: `TitleText`
3. Text = "All Rank Tiers", Font Size = 36, Top Center

### 8.5. Horizontal Scroll View

1. **PopupContentBackground → UI → Scroll View**
2. Rename: `RankTierScrollView`
3. **RectTransform:** Anchor = Center, Width = 1100, Height = 500
4. **Scroll Rect:**
   - Horizontal = **tích**
   - Vertical = **bỏ tích**
   - Movement Type = Elastic
5. **Xóa Scrollbar Vertical** (không cần)

### 8.6. Content (HorizontalLayoutGroup)

1. Chọn `RankTierScrollView → Viewport → Content`
2. **RectTransform:**
   - Anchor = Left Center
   - Pivot = (0, 0.5)
   - Width = **2400** (will expand with cards)
3. **Add Component → Horizontal Layout Group:**
   - Control Child Size Width/Height = **tích**
   - Child Force Expand = **bỏ tích**
   - Spacing = **20**
4. **Add Component → Content Size Fitter:**
   - Horizontal Fit = **Preferred Size**

---

## Bước 9: Prefab — RankTierCard

### 9.1. Tạo prefab mới

1. **Hierarchy → UI → Panel**
2. Rename: `RankTierCard`
3. **RectTransform:** Width = **280**, Height = **450**
4. **Image:** Color = `rgba(40, 40, 50, 0.9)`

### 9.2. Thêm UI elements

**Tạo 5 child elements:**

1. **RankBadgeImage** (Image)
   - Pos Y = 150 (top), Width = 150, Height = 150
   - Preserve Aspect = tích

2. **RankNameText** (TextMeshPro)
   - Pos Y = 50, Width = 250, Height = 50
   - Text = "Pactkeeper", Font Size = 28, Center, Bold

3. **MinScoreText** (TextMeshPro)
   - Pos Y = 0, Width = 250, Height = 40
   - Text = "Required: 3000+ points", Font Size = 18, Center

4. **LoreDescriptionText** (TextMeshPro)
   - Pos Y = -80, Width = 260, Height = 200
   - Text = "Story lore...", Font Size = 14, Center, Wrap

5. **LoreLockedOverlay** (Panel)
   - Stretch over LoreDescriptionText area
   - Image: Color = `rgba(0, 0, 0, 0.7)`
   - Child Text: "🔒 Complete Story to Unlock"
   - Active = true (RankTierCard script sẽ toggle)

### 9.3. Add Script

1. **Add Component → Script → RankTierCard**
2. **Wire fields:**
   - Rank Badge Image → kéo `RankBadgeImage`
   - Rank Name Text → kéo `RankNameText`
   - Min Score Text → kéo `MinScoreText`
   - Lore Description Text → kéo `LoreDescriptionText`
   - Lore Locked Overlay → kéo `LoreLockedOverlay`

### 9.4. Tạo Prefab

1. Kéo vào `Assets/Prefabs/Ranking/RankTierCard.prefab`
2. Xóa instance trong Hierarchy
