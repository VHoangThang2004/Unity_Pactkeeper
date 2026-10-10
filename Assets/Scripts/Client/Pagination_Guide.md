# Pagination Guide — Chuyển từ ScrollView sang dạng trang sách

**Áp dụng cho:** `ShopTabManager` (item grid) và `InventoryManager` (item list)  
**Nguyên tắc:** Không xóa ScrollView hay GridLayoutGroup — chỉ thêm logic pagination vào code C# và thêm nút Prev/Next vào UI.

---

## Tại sao không xóa ScrollView?

- GridLayoutGroup và Content Size Fitter vẫn còn đó để tự động sắp xếp item.
- Chỉ cần **tắt scroll bằng tay** (Scroll Rect → không scroll được) và **giới hạn số item hiển thị mỗi trang**.
- Item thừa không Instantiate ra — chỉ items của trang hiện tại được spawn.

---

## PHẦN 1 — Thay đổi trong Inspector (làm trước)

### 1.1. Tắt khả năng kéo của ScrollView

Với cả **ShopPanel → ScrollView** lẫn **InventoryPanel → ScrollView**:

1. Chọn **ScrollView** GameObject
2. Component **Scroll Rect**:
   - **Horizontal**: ✗ tắt
   - **Vertical**: ✗ tắt  
   *(Giữ nguyên mọi thứ khác — Viewport, Content vẫn như cũ)*

> Bây giờ user không kéo được nữa. Item vẫn layout trong grid/list bình thường.

---

### 1.2. Thêm Pagination Bar vào UI

Thêm vào dưới (hoặc trên) vùng item grid của mỗi panel:

```
PaginationBar         (UI → Panel, anchor bottom-stretch, height 40)
├── PrevButton        (Button, anchor left, width 80)   → "< Prev"
├── PageLabel         (TMP_Text, anchor center)          → "Page 1 / 3"
└── NextButton        (Button, anchor right, width 80)  → "Next >"
```

**Cho Shop** — đặt PaginationBar ngay dưới ScrollView trong CenterPanel.  
**Cho Inventory** — đặt PaginationBar ngay dưới ScrollView trong LeftPanel.

---

## PHẦN 2 — Thay đổi code C#

### 2.1. ShopTabManager.cs

#### Bước A — Thêm fields vào Inspector section

Tìm `[Header("Center — Item Grid")]` và thêm sau dòng `shopItemPrefab`:

```csharp
[Header("Pagination — Item Grid")]
[SerializeField] private int      itemsPerPage     = 8;   // chỉnh theo số slot trong background
[SerializeField] private Button   itemPrevButton;
[SerializeField] private Button   itemNextButton;
[SerializeField] private TMP_Text itemPageLabel;
```

#### Bước B — Thêm biến runtime

Tìm khối `// ── Runtime ──` và thêm:

```csharp
private int currentItemPage = 0;
```

#### Bước C — Wire button trong Awake()

Tìm `void Awake()` và thêm vào trong:

```csharp
if (itemPrevButton != null) itemPrevButton.onClick.AddListener(ItemPrevPage);
if (itemNextButton != null) itemNextButton.onClick.AddListener(ItemNextPage);
```

Tìm `void OnDestroy()` và thêm:

```csharp
if (itemPrevButton != null) itemPrevButton.onClick.RemoveListener(ItemPrevPage);
if (itemNextButton != null) itemNextButton.onClick.RemoveListener(ItemNextPage);
```

#### Bước D — Reset page khi đổi shop

Tìm hàm `SelectShop(...)` — dòng gọi `RenderItemGrid(shop)`, thêm **trước** nó:

```csharp
currentItemPage = 0;
```

#### Bước E — Sửa `RenderItemGrid`

Thay toàn bộ hàm `RenderItemGrid` hiện tại bằng:

```csharp
void RenderItemGrid(PlayerShopDto shop)
{
    ClearItemGrid();

    if (shop.items == null || shop.items.Length == 0)
    {
        if (itemEmptyState != null) itemEmptyState.SetActive(true);
        UpdateItemPageLabel(0, 0);
        return;
    }

    if (itemEmptyState != null) itemEmptyState.SetActive(false);

    int totalPages = Mathf.CeilToInt((float)shop.items.Length / itemsPerPage);
    currentItemPage = Mathf.Clamp(currentItemPage, 0, Mathf.Max(0, totalPages - 1));

    int startIndex = currentItemPage * itemsPerPage;
    int endIndex   = Mathf.Min(startIndex + itemsPerPage, shop.items.Length);

    for (int i = startIndex; i < endIndex; i++)
    {
        var obj   = Instantiate(shopItemPrefab, itemGridContainer);
        var entry = obj.GetComponent<ShopItemEntry>();
        entry?.Setup(shop.items[i], this);
    }

    UpdateItemPageLabel(currentItemPage + 1, totalPages);
}
```

#### Bước F — Thêm các hàm pagination

Thêm vào cuối class, trước dấu `}` cuối cùng:

```csharp
void ItemPrevPage()
{
    if (selectedShop == null) return;
    int totalPages = Mathf.CeilToInt((float)selectedShop.items.Length / itemsPerPage);
    if (currentItemPage <= 0) return;
    currentItemPage--;
    RenderItemGrid(selectedShop);
}

void ItemNextPage()
{
    if (selectedShop == null) return;
    int totalPages = Mathf.CeilToInt((float)selectedShop.items.Length / itemsPerPage);
    if (currentItemPage >= totalPages - 1) return;
    currentItemPage++;
    RenderItemGrid(selectedShop);
}

void UpdateItemPageLabel(int current, int total)
{
    if (itemPageLabel  != null) itemPageLabel.text    = total > 0 ? $"Page {current} / {total}" : string.Empty;
    if (itemPrevButton != null) itemPrevButton.interactable = current > 1;
    if (itemNextButton != null) itemNextButton.interactable = current < total;
}
```

---

### 2.2. InventoryManager.cs

#### Bước A — Thêm fields

Tìm `[Header("Left Panel - Item List")]` và thêm sau `inventoryItemPrefab`:

```csharp
[Header("Pagination")]
[SerializeField] private int      itemsPerPage   = 6;   // chỉnh theo số slot trong background
[SerializeField] private Button   prevButton;
[SerializeField] private Button   nextButton;
[SerializeField] private TMP_Text pageLabel;
```

#### Bước B — Thêm biến runtime

Tìm `private List<InventoryItemData> filteredItems` và thêm sau:

```csharp
private int currentPage = 0;
```

#### Bước C — Wire button trong Awake()

Tìm `void Awake()` và thêm vào trong:

```csharp
if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
if (nextButton != null) nextButton.onClick.AddListener(NextPage);
```

Tìm `void OnDestroy()` và thêm:

```csharp
if (prevButton != null) prevButton.onClick.RemoveListener(PrevPage);
if (nextButton != null) nextButton.onClick.RemoveListener(NextPage);
```

#### Bước D — Reset page khi đổi filter

Tìm hàm `SetFilter(...)` và thêm `currentPage = 0;` **trước** dòng `ApplyFilter()`:

```csharp
void SetFilter(ItemFilter filter)
{
    currentFilter = filter;
    currentPage   = 0;       // ← thêm dòng này
    ApplyFilter();
}
```

Tương tự trong `Open()`, thêm `currentPage = 0;` trước `LoadInventory()`.

#### Bước E — Sửa `RenderItemList`

Thay toàn bộ hàm `RenderItemList` hiện tại bằng:

```csharp
void RenderItemList()
{
    ClearItemList();

    if (filteredItems.Count == 0)
    {
        ShowEmptyState();
        UpdatePageLabel(0, 0);
        return;
    }

    HideEmptyState();

    int totalPages = Mathf.CeilToInt((float)filteredItems.Count / itemsPerPage);
    currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, totalPages - 1));

    int startIndex = currentPage * itemsPerPage;
    int endIndex   = Mathf.Min(startIndex + itemsPerPage, filteredItems.Count);

    for (int i = startIndex; i < endIndex; i++)
    {
        var itemObj       = Instantiate(inventoryItemPrefab, itemListContainer);
        var itemComponent = itemObj.GetComponent<InventoryItem>();
        if (itemComponent != null)
            itemComponent.Setup(filteredItems[i], this);
    }

    UpdatePageLabel(currentPage + 1, totalPages);
}
```

#### Bước F — Thêm các hàm pagination

```csharp
void PrevPage()
{
    if (currentPage <= 0) return;
    currentPage--;
    RenderItemList();
}

void NextPage()
{
    int totalPages = Mathf.CeilToInt((float)filteredItems.Count / itemsPerPage);
    if (currentPage >= totalPages - 1) return;
    currentPage++;
    RenderItemList();
}

void UpdatePageLabel(int current, int total)
{
    if (pageLabel  != null) pageLabel.text    = total > 0 ? $"Page {current} / {total}" : string.Empty;
    if (prevButton != null) prevButton.interactable = current > 1;
    if (nextButton != null) nextButton.interactable = current < total;
}
```

---

## PHẦN 3 — Wire Inspector sau khi sửa code

### ShopTabManager — QuestPanel → ShopTabManager component:

| Field mới | Gán |
|---|---|
| **Items Per Page** | Số slot item trong background image (ví dụ: 4 cột × 2 hàng = **8**) |
| **Item Prev Button** | `PrevButton` trong PaginationBar của CenterPanel |
| **Item Next Button** | `NextButton` trong PaginationBar của CenterPanel |
| **Item Page Label** | `PageLabel` (TMP_Text) trong PaginationBar |

### InventoryManager — InventoryPanel → InventoryManager component:

| Field mới | Gán |
|---|---|
| **Items Per Page** | Số slot trong background (ví dụ: 1 cột × 6 hàng = **6**) |
| **Prev Button** | `PrevButton` trong PaginationBar của LeftPanel |
| **Next Button** | `NextButton` trong PaginationBar của LeftPanel |
| **Page Label** | `PageLabel` (TMP_Text) trong PaginationBar |

---

## PHẦN 4 — Cách xác định đúng `itemsPerPage`

Đây là bước quan trọng nhất để item khớp với khung background:

1. Mở background image trong image editor, đếm số slot item trong khung:
   - Ví dụ Shop background: 4 cột × 2 hàng → `itemsPerPage = 8`
   - Ví dụ Inventory background: 1 cột × 6 hàng → `itemsPerPage = 6`
2. Set **Cell Size** trong **Grid Layout Group** khớp đúng với kích thước slot trong ảnh.
3. Set **Constraint = Fixed Column Count** trong Grid Layout Group bằng số cột thực tế.
4. Chỉnh **itemsPerPage** trong Inspector cho đến khi item lấp đúng các ô background.

---

## PHẦN 5 — Checklist

- [ ] ScrollView → Scroll Rect → Horizontal: ✗, Vertical: ✗ (cả Shop lẫn Inventory)
- [ ] Thêm PaginationBar vào UI theo hướng dẫn Bước 1.2
- [ ] Sửa code theo Phần 2 (ShopTabManager + InventoryManager)
- [ ] Wire 4 fields mới trong Inspector (cả 2 manager)
- [ ] Đặt `itemsPerPage` đúng với số slot trong background
- [ ] Grid Layout Group: **Constraint = Fixed Column Count** = số cột background
- [ ] Test: đủ item → nút Next active, ít item → nút Prev/Next disabled đúng
- [ ] Test: đổi filter (Inventory) → quay về page 1 ✓
- [ ] Test: đổi shop tab → quay về page 1 ✓
