# Quest Panel — UI Setup Guide

**Scene:** `3_MainMenu`  
**Scripts:** `QuestManager.cs`, `QuestItem.cs`  
**Pattern:** Giống MailboxManager (left list + right detail) + InventoryItem (prefab item)

---

## BƯỚC 1 — Tạo QuestPanel (root)

1. Hierarchy → right-click **Canvas** → **UI → Panel**
2. Đặt tên: `QuestPanel`
3. RectTransform:
   - Anchor: **stretch-stretch** (min 0,0 / max 1,1) → full screen overlay
   - Left / Right / Top / Bottom: `0`
4. **Image** component → gán background sprite (pixel art quest board đã generate)
5. **Add Component → QuestManager**
6. **Set Active = false** (ẩn mặc định)

---

## BƯỚC 2 — Tạo CloseButton

```
QuestPanel
└── CloseButton   (UI → Button, size 50×50, anchor top-right, pos −30,−30)
    └── X_Text    (TMP_Text) → "✕"
```

---

## BƯỚC 3 — Tạo Layout 2 cột

```
QuestPanel
├── CloseButton
├── LeftPanel     (UI → Panel, anchor left, width ~38% màn hình)
└── RightPanel    (UI → Panel, anchor right, width ~58% màn hình)
```

**LeftPanel RectTransform:**
- Anchor Min: (0, 0) / Max: (0.38, 1)
- Left: 20 / Right: 10 / Top: 20 / Bottom: 20

**RightPanel RectTransform:**
- Anchor Min: (0.40, 0) / Max: (1, 1)
- Left: 10 / Right: 20 / Top: 20 / Bottom: 20

---

## BƯỚC 4 — Tạo LeftPanel (danh sách nhiệm vụ)

```
LeftPanel
├── HeaderText        (TMP_Text) → "Nhiệm Vụ", anchor top-stretch, height 40
├── EmptyStatePanel   (UI → Panel, anchor center)
│   └── EmptyText     (TMP_Text) → "Không có nhiệm vụ nào."
└── ScrollView        (UI → Scroll View, anchor stretch, Top: 50, Bottom: 0)
    └── Viewport
        └── Content   ← đây là QuestListContainer
            (Vertical Layout Group + Content Size Fitter)
```

**Content GameObject — thêm components:**
- **Vertical Layout Group**:
  - Spacing: 8
  - Child Force Expand Width: ✓ / Height: ✗
  - Child Control Size Width: ✓ / Height: ✗
  - Padding: Left 8, Right 8, Top 8, Bottom 8
- **Content Size Fitter**:
  - Vertical Fit: **Preferred Size**

---

## BƯỚC 5 — Tạo QuestItem Prefab

1. Hierarchy → right-click **LeftPanel/ScrollView/Viewport/Content** → **UI → Panel**
2. Đặt tên: `QuestItemPrefab`
3. RectTransform: Height = **80**, Width tự co theo container

```
QuestItemPrefab
├── SelectedBackground   (Image, color vàng nhạt ~(1,0.9,0.5,0.3), mặc định inactive)
├── QuestNameText        (TMP_Text) → tên nhiệm vụ, anchor left, font size 16
└── ProgressText         (TMP_Text) → "0 / 5", anchor right, font size 14, color xám
```

4. **Add Component → QuestItem** trên `QuestItemPrefab`
5. **Add Component → Button** trên `QuestItemPrefab`
   - **On Click():** object = `QuestItemPrefab` → function = `QuestItem → OnClick()`

**Wire QuestItem Inspector:**
| Field | Gán |
|---|---|
| Quest Name Text | `QuestNameText` |
| Progress Text | `ProgressText` |
| Selected Background | `SelectedBackground` |

6. Drag `QuestItemPrefab` từ Hierarchy → thư mục **Assets/Prefabs/** để tạo prefab thật
7. Xóa instance trong Hierarchy (chỉ giữ prefab trong Assets)

---

## BƯỚC 6 — Tạo RightPanel (chi tiết nhiệm vụ)

```
RightPanel
├── DetailEmptyState     (UI → Panel, anchor center, mặc định active)
│   └── HintText         (TMP_Text) → "Chọn một nhiệm vụ để xem chi tiết"
│
└── DetailPanel          (UI → Panel, anchor stretch, mặc định inactive)
    │
    ├── ── HEADER ─────────────────────────────────────────
    ├── DetailQuestNameText   (TMP_Text) → tên lớn, font size 22, bold
    ├── DetailActionTypeText  (TMP_Text) → subtitle nhỏ, font size 13, color xám
    │
    ├── ── PROGRESS ────────────────────────────────────────
    ├── DetailProgressText    (TMP_Text) → "2 / 5", font size 16
    ├── DetailProgressBar     (UI → Slider)
    │   ├── Background   (Image, color xám tối)
    │   └── Fill Area → Fill  (Image, color xanh lá / vàng)
    │       (tắt Handle Slide Area nếu không muốn kéo được)
    │
    ├── ── CONDITIONS (tuỳ chọn) ────────────────────────────
    ├── DetailConditionsText  (TMP_Text) → font size 13, color xám nhạt
    │
    ├── ── REWARD BOX (khung xanh như screenshot) ───────────
    ├── RewardBox             (UI → Panel, border màu xanh dương)
    │   ├── RewardTitle       (TMP_Text) → "Phần Thưởng:", font size 14, bold
    │   ├── DetailRewardIcon  (Image, size 48×48)
    │   ├── DetailRewardTypeText   (TMP_Text) → "💎 Gems", font size 16
    │   └── DetailRewardAmountText (TMP_Text) → "x500", font size 18, bold, color vàng
    │
    ├── ── STATUS ──────────────────────────────────────────
    ├── DetailStatusText      (TMP_Text) → font size 14, italic
    │
    └── ── CLAIM BUTTON ────────────────────────────────────
        └── ClaimButton       (UI → Button, anchor bottom-right, size 200×50)
            └── ClaimButtonText (TMP_Text) → "Nhận thưởng"
```

**Slider (DetailProgressBar) — tắt tương tác:**
- Interactable: ✗ (chỉ dùng để hiển thị, không kéo được)
- Transition: None

---

## BƯỚC 7 — Wire QuestManager Inspector

Chọn **QuestPanel** → Inspector → **QuestManager** component:

### Header: Config
| Field | Gán |
|---|---|
| **Config** | `ClientBackendConfig` asset (Assets/Data/Client/ClientBackendConfig.asset) |

### Header: Panel
| Field | Gán |
|---|---|
| **Panel** | `QuestPanel` (chính nó) |
| **Close Button** | `CloseButton` |

### Header: Left Panel — Quest List
| Field | Gán |
|---|---|
| **Quest List Container** | `Content` (trong ScrollView/Viewport/Content) |
| **Quest Item Prefab** | Prefab `QuestItemPrefab` từ Assets/Prefabs/ |
| **Empty State Panel** | `EmptyStatePanel` trong LeftPanel |

### Header: Right Panel — Quest Detail
| Field | Gán |
|---|---|
| **Detail Panel** | `DetailPanel` trong RightPanel |
| **Detail Empty State** | `DetailEmptyState` trong RightPanel |
| **Detail Quest Name Text** | `DetailQuestNameText` |
| **Detail Action Type Text** | `DetailActionTypeText` |
| **Detail Progress Text** | `DetailProgressText` |
| **Detail Progress Bar** | `DetailProgressBar` (Slider) |
| **Detail Conditions Text** | `DetailConditionsText` |
| **Detail Reward Type Text** | `DetailRewardTypeText` |
| **Detail Reward Amount Text** | `DetailRewardAmountText` |
| **Detail Reward Icon** | `DetailRewardIcon` (Image) |
| **Detail Status Text** | `DetailStatusText` |
| **Claim Button** | `ClaimButton` |
| **Claim Button Text** | `ClaimButtonText` (TMP_Text con của ClaimButton) |

---

## BƯỚC 8 — Wire vào MainMenuManager

1. Tìm GameObject có `MainMenuManager` trong scene
2. Inspector → field **Quest Manager** → drag `QuestPanel` vào

---

## BƯỚC 9 — Tạo nút mở Quest trong HUD

1. Tạo Button trong HUD navbar (cạnh các nút Mailbox, Shop, v.v.)
2. Đặt tên: `QuestButton`
3. **On Click():**
   - Object: GameObject có `MainMenuManager`
   - Function: `MainMenuManager → OpenQuestTab()`

---

## BƯỚC 10 — Kiểm tra lần cuối trước khi Play

- [ ] `QuestPanel` SetActive = **false** trong Inspector
- [ ] `DetailPanel` SetActive = **false** trong Inspector
- [ ] `DetailEmptyState` SetActive = **true** trong Inspector
- [ ] `EmptyStatePanel` SetActive = **false** trong Inspector
- [ ] `DetailProgressBar` Interactable = **false**
- [ ] `QuestItemPrefab` đã được tạo thành file prefab trong Assets, **không** để instance trong scene
- [ ] `ClientBackendConfig` asset đã được gán vào field **Config**

---

## Luồng hoạt động sau khi setup

```
Player click QuestButton
  → MainMenuManager.OpenQuestTab()
    → QuestManager.Open()
      → panel.SetActive(true)
      → LoadQuestsCoroutine()
          → GET /api/rewardrule/active     (danh sách quest definitions)
          → GET /api/rewardprogress        (progress của player)
          → Join 2 list bằng RuleId
          → Instantiate QuestItemPrefab × n vào Content
          → EmptyState ẩn đi nếu có data

Player click 1 QuestItem
  → QuestItem.OnClick()
    → QuestManager.OnQuestSelected()
      → Highlight item được chọn
      → ShowDetail() → hiển thị RightPanel với đầy đủ thông tin
      → ClaimButton: enabled nếu IsCompleted=true && IsReceived=false

Player click "Nhận thưởng"
  → QuestManager.OnClickClaim()
    → POST /api/rewardprogress/{id}/claim
    → Nếu thành công: IsReceived = true, refresh detail + re-render list
    → ClaimButton đổi text → "Đã nhận", disabled
```

---

## Ghi chú

- `ActionType` từ backend (vd: `MatchWon`, `MatchCompleted`) được tự động format thành `Match Won`, `Match Completed` khi hiển thị — không cần sửa code.
- `Conditions` là tuỳ chọn UI; nếu không muốn hiển thị thì bỏ trống field `Detail Conditions Text` trong Inspector, code sẽ tự bỏ qua.
- `DetailRewardIcon` hiện để trống vì phần thưởng có thể là Gems/Unit/Weapon/Trinket — bạn có thể gán sprite icon sau khi có asset đủ.
