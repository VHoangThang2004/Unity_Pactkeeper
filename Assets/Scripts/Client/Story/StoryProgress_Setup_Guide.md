# Story Progress Scene — Setup Guide

**Scene:** `10_StoryProgress` (tạo mới)  
**Scripts:** `StoryProgressManager.cs`, `ChapterCard.cs`  
**Layout:** Horizontal scroll — mỗi chapter là 1 card, click mở popup chi tiết

---

## PROMPT — Background toàn màn hình

```
2D pixel art medieval fantasy story map / adventure hall background,
wide panoramic ancient stone hall with high vaulted ceilings,
large illuminated map of a fantasy kingdom spread on a central stone table,
glowing magical lanterns hanging from arched ceiling, candles flickering on walls,
worn tapestries depicting battle scenes and crests on stone walls,
bookshelves with old tomes and scroll racks on sides,
warm amber and deep navy color palette, dramatic directional lighting,
no characters, no text, no UI elements,
16:9 landscape format, high detail pixel art, 32x32 tile aesthetic, fantasy RPG style
```

> **Kích thước:** 1920×1080  
> **Import:** `Texture Type = Sprite (2D and UI)`, `Filter Mode = Point (no filter)`

---

## BƯỚC 1 — Tạo Scene mới

1. **File → New Scene** (Basic hoặc Empty)
2. **File → Save As** → đặt tên `10_StoryProgress`
3. **File → Build Settings** → Add Open Scenes → đảm bảo scene xuất hiện trong Build list
4. Cập nhật field **Story Progress Scene** trong `SceneConfig.asset`:
   - `Assets/Data/Shared/SceneConfig.asset` → Inspector → **Story Progress Scene** = `10_StoryProgress`

---

## BƯỚC 2 — Setup Camera & Canvas

1. **Main Camera** — giữ nguyên default
2. **Right-click Hierarchy → UI → Canvas**:
   - Canvas Scaler → **Scale With Screen Size**, Reference 1920×1080
   - Canvas Scaler → Match = **0.5** (cân bằng width/height)

---

## BƯỚC 3 — Background toàn màn hình

```
Canvas
└── Background   (UI → Image, anchor stretch-stretch, offset 0)
    → Source Image: background sprite đã generate
    → Image Type: Simple, Preserve Aspect: ✗
```

---

## BƯỚC 4 — Tạo StoryProgressManager GameObject

1. **Right-click Hierarchy → Create Empty**
2. Đặt tên: `StoryProgressManager`
3. **Add Component → StoryProgressManager**

---

## BƯỚC 5 — Header Bar

```
Canvas
└── HeaderBar   (UI → Panel, anchor top-stretch, height 70)
    ├── BackButton   (Button, anchor left, size 120×50, pos X 20)
    │   └── Text     (TMP_Text "← Back")
    └── TitleText    (TMP_Text "Story Progress", anchor center, font 24 bold)
```

---

## BƯỚC 6 — Horizontal Scroll Area (phần chính)

```
Canvas
└── ScrollArea   (UI → Scroll View, anchor stretch, Top=70, Bottom=0)
    ├── Scroll Rect:
    │   Horizontal: ✓  |  Vertical: ✗
    │   Movement Type: Elastic
    └── Viewport (Mask + Image)
        └── Content   ← CardContainer (gán vào StoryProgressManager)
            (Horizontal Layout Group)
            (Content Size Fitter: Horizontal = Preferred Size)
```

**Horizontal Layout Group trên Content:**
- Spacing: 40
- Child Alignment: Middle Left
- Control Child Size Width: ✗ / Height: ✗
- Child Force Expand: ✗ / ✗
- Padding Left: 80, Right: 80, Top: 0, Bottom: 0

**Content RectTransform:**
- Anchor Min/Max: (0, 0) → (0, 1) — left-stretch
- Pivot: (0, 0.5)
- Height: tự co theo Viewport height

---

## BƯỚC 7 — Loading & Empty State

```
Canvas
├── LoadingIndicator  (UI → Panel, anchor center, size 200×60)
│   └── LoadingText   (TMP_Text "Loading...", anchor center)
└── EmptyState        (UI → Panel, anchor center, size 400×100, SetActive=false)
    └── EmptyText     (TMP_Text "No chapters available", anchor center)
```

---

## BƯỚC 8 — Tạo ChapterCard Prefab

```
ChapterCardPrefab   (UI → Panel, size 280×400)
│
├── ThumbnailImage     (Image, anchor top-stretch, height 320)
│   → Placeholder: màu xám nhạt
│
├── StatusBadge        (Panel, anchor top-left, size 120×36, pos (10,-10))
│   ├── StatusBadgeBackground  (Image, color thay đổi theo status)
│   └── StatusBadgeText        (TMP_Text "COMPLETED", font 12 bold, color trắng)
│
├── ChapterTitleText   (TMP_Text, anchor bottom-stretch, height 60,
│                       font 15, padding Left/Right 10, WordWrap ✓)
│
└── LockOverlay        (Image, anchor stretch, color đen (0,0,0,0.6),
                        SetActive=false)
    └── LockIcon       (Image hoặc TMP_Text "🔒", anchor center)
```

**Add Component → ChapterCard** trên `ChapterCardPrefab`  
**Add Component → Button** trên `ChapterCardPrefab` → **On Click()** → `ChapterCard → OnClick()`

**Wire ChapterCard Inspector:**
| Field | Gán |
|---|---|
| Thumbnail Image | `ThumbnailImage` |
| Chapter Title Text | `ChapterTitleText` |
| Status Badge | `StatusBadge` (root) |
| Status Badge Text | `StatusBadgeText` |
| Status Badge Background | `StatusBadgeBackground` |
| Lock Overlay | `LockOverlay` |

Drag prefab vào **Assets/Prefabs/ChapterCard**, xóa instance trong scene.

---

## BƯỚC 9 — Detail Popup

```
Canvas
└── PopupPanel   (UI → Panel, anchor center, size 700×520, SetActive=false)
    ├── PopupBackground   (Image, dark semi-transparent)
    │
    ├── ── HEADER ──────────────────────────────────────────────────
    ├── PopupCloseButton  (Button, anchor top-right, size 40×40) → "✕"
    ├── PopupChapterTitle (TMP_Text, anchor top-stretch, height 50, font 22 bold)
    │
    ├── ── STATUS ──────────────────────────────────────────────────
    ├── StatusRow         (HorizontalLayoutGroup, anchor top-stretch, height 30)
    │   ├── PopupStatusIcon  (Image, size 20×20, color tự thay đổi)
    │   └── PopupStatusText  (TMP_Text "In Progress", font 16)
    │
    ├── ── PROGRESS ────────────────────────────────────────────────
    ├── PopupProgressText (TMP_Text "3 / 5 scenes", font 14, color xám)
    ├── PopupProgressBar  (UI → Slider, Interactable=✗, anchor stretch, height 20)
    │
    ├── ── CURRENT SCENE ───────────────────────────────────────────
    ├── CurrentSceneLabel (TMP_Text "Scene 2 — Tutorial battle",
    │                      font 14 italic, color amber)
    │
    └── ── SCENE LIST ──────────────────────────────────────────────
        └── SceneListScroll (UI → Scroll View, Horizontal ✗, Vertical ✓)
            └── Viewport → Content
                └── PopupSceneListText  (TMP_Text, font 13, anchor top-stretch)
```

---

## BƯỚC 10 — Wire StoryProgressManager Inspector

Chọn `StoryProgressManager` GameObject:

### Header: Config
| Field | Gán |
|---|---|
| **Config** | `Assets/Data/Client/ClientBackendConfig.asset` |
| **Scene Config** | `Assets/Data/Shared/SceneConfig.asset` |

### Header: Horizontal Scroll
| Field | Gán |
|---|---|
| **Card Container** | `Content` (trong ScrollView/Viewport/Content) |
| **Chapter Card Prefab** | `Assets/Prefabs/ChapterCard` |
| **Loading Indicator** | `LoadingIndicator` |
| **Empty State** | `EmptyState` |
| **Thumbnail Sprites** | Size = số chapter; kéo sprite vào từng index theo ChapterId |

### Header: Back Button
| Field | Gán |
|---|---|
| **Back Button** | `BackButton` |

### Header: Detail Popup
| Field | Gán |
|---|---|
| **Popup Panel** | `PopupPanel` |
| **Popup Close Button** | `PopupCloseButton` |
| **Popup Chapter Title** | `PopupChapterTitle` |
| **Popup Status Text** | `PopupStatusText` |
| **Popup Status Icon** | `PopupStatusIcon` |
| **Popup Progress Bar** | `PopupProgressBar` (Slider) |
| **Popup Progress Text** | `PopupProgressText` |
| **Popup Current Scene Label** | `CurrentSceneLabel` |
| **Popup Scene List Text** | `PopupSceneListText` |

---

## BƯỚC 11 — Nút mở Story Progress từ MainMenu

Trong scene `3_MainMenu`, tạo hoặc dùng button Story hiện có:
- **Button → On Click():**
  - Object: GameObject có `MainMenuManager`
  - Function: `MainMenuManager → GoToStoryProgress()`

---

## BƯỚC 12 — Checklist trước khi Play

- [ ] Scene `10_StoryProgress` đã add vào Build Settings
- [ ] `SceneConfig.asset` → **Story Progress Scene** = `10_StoryProgress`
- [ ] `PopupPanel` SetActive = **false**
- [ ] `LoadingIndicator` SetActive = **true**
- [ ] `EmptyState` SetActive = **false**
- [ ] `LockOverlay` trên ChapterCard prefab SetActive = **false**
- [ ] `PopupProgressBar` Interactable = **false**
- [ ] ChapterCard là file prefab trong Assets/Prefabs, không phải instance trong scene

---

## Luồng hoạt động

```
Player click "Story Progress" trên MainMenu
  → GoToStoryProgress() → LoadScene("10_StoryProgress")

Scene Start()
  → Fetch song song:
      GET /api/chapterconfig           (danh sách chapter + scenes)
      GET /api/story/progress/all      (tiến độ của player)
  → Join bằng chapterId
  → Xác định status: Completed / InProgress / NotStarted / Locked
  → Instantiate ChapterCard × n vào Content (kéo ngang)
  → LoadingIndicator ẩn

Player kéo ngang để xem chapters

Player click 1 ChapterCard
  → OnChapterCardClicked() → ShowPopup()
  → Popup hiển thị:
      - Tên chapter
      - Status (màu xanh/vàng/xám)
      - Progress bar: x / y scenes
      - Current scene: "Scene 2 — Tutorial battle"
      - Danh sách tất cả scenes với tick ✓ / ○

Player click ✕ hoặc outside popup
  → ClosePopup() → PopupPanel ẩn

Player click ← Back
  → LoadScene("3_MainMenu")
```

---

## Ghi chú về Thumbnail Sprites

`thumbnailSprites[]` trong Inspector được index theo **ChapterId**:
- Index 0 → Chapter 0 thumbnail
- Index 1 → Chapter 1 thumbnail
- ...

Nếu không gán thumbnail, card vẫn hiển thị bình thường với màu placeholder.  
Có thể dùng ảnh concept art, map screenshot, hoặc generate bằng AI prompt riêng cho từng chapter.
