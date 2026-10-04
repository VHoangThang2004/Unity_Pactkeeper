# Inventory UI Setup Guide

## Tổng quan
Hướng dẫn thiết lập UI cho Inventory trong Unity Inspector với layout 2 panel (trái - phải) theo phong cách rương kho báu/túi đồ trung cổ.
- Bên trái (70%): Danh sách vật phẩm với filter buttons
- Bên phải (30%): Chi tiết vật phẩm khi được chọn
- Filter: All, Weapons, Trinkets

---

## 1. Tạo Inventory Prefab

### Bước 1: Tạo Panel chính
1. Trong scene MainMenu, tạo GameObject mới:
   - Right-click Hierarchy → UI → Panel
   - Đặt tên: `InventoryPanel`
   - RectTransform: Anchor stretch (0,0,1,1), Size: Full screen
   - Add component **InventoryManager**

2. Thêm background image (sau khi tạo asset từ AI):
   - Trong InventoryPanel → Image component
   - Set sprite: Asset rương kho báu/túi đồ
   - Type: Simple
   - Color: White

### Bước 2: Tạo Header
1. Tạo GameObject con của InventoryPanel:
   - Right-click InventoryPanel → UI → Panel
   - Đặt tên: `Header`
   - RectTransform: Anchor top stretch (0,1,1,1), Height: 80

2. Trong Header, tạo các UI elements:
   - **TitleText** (TMP_Text): "Inventory", Anchor left-center, Font size: 24
   - **CloseButton** (Button): Anchor right-center, Size: 40x40

### Bước 3: Tạo Left Panel (Item List + Filter)
1. Tạo GameObject con của InventoryPanel:
   - Right-click InventoryPanel → UI → Panel
   - Đặt tên: `LeftPanel`
   - RectTransform: Anchor stretch (0,0,0.7,1), Top: 80, Bottom: 60

2. Trong LeftPanel, tạo Filter Buttons:
   - Right-click LeftPanel → UI → Panel
   - Đặt tên: `FilterPanel`
   - RectTransform: Anchor top stretch (0,1,1,1), Height: 60
   - Add component **Horizontal Layout Group**
   - Spacing: 10
   - Padding: Top 10, Bottom 10, Left 20, Right 20

3. Trong FilterPanel, tạo các nút filter:
   - **FilterAllButton** (Button): Text "All", Height: 40
   - **FilterWeaponsButton** (Button): Text "Weapons", Height: 40
   - **FilterTrinketsButton** (Button): Text "Trinkets", Height: 40

4. Trong LeftPanel, tạo Scroll View cho item list:
   - Right-click LeftPanel → UI → Scroll View
   - Đặt tên: `ItemScrollView`
   - RectTransform: Anchor stretch (0,0,1,1), Top: 60

5. Trong Scroll View → Viewport → Content:
   - Add component **Grid Layout Group**
   - Cell Size: X=80, Y=80 (kích thước mỗi item)
   - Spacing: X=10, Y=10
   - Constraint: Fixed Column Count
   - Constraint Count: 7 (7 items mỗi hàng)
   - Padding: Top 20, Bottom 20, Left 20, Right 20
   - Start Corner: Upper Left
   - Start Axis: Horizontal
   - Child Alignment: Upper Center

### Bước 4: Tạo Right Panel (Item Detail)
1. Tạo GameObject con của InventoryPanel:
   - Right-click InventoryPanel → UI → Panel
   - Đặt tên: `RightPanel`
   - RectTransform: Anchor stretch (0.7,0,1,1), Top: 80, Bottom: 60

2. Trong RightPanel, tạo Detail Panel:
   - Right-click RightPanel → UI → Panel
   - Đặt tên: `DetailPanel`
   - RectTransform: Anchor stretch (0,0,1,1)
   - Add component **Vertical Layout Group**
   - Spacing: 20
   - Padding: Top 20, Bottom 20, Left 20, Right 20
   - Child Alignment: Upper Center

3. Trong DetailPanel, tạo:
   - **DetailIcon** (Image): Size: 100x100, Anchor center-top
   - **DetailNameText** (TMP_Text): Font size: 20, Anchor center
   - **DetailTypeText** (TMP_Text): Font size: 16, Anchor center
   - **DetailStatText** (TMP_Text): Font size: 14, Anchor center

4. Trong RightPanel, tạo Empty State:
   - Right-click RightPanel → UI → Panel
   - Đặt tên: `DetailEmptyState`
   - RectTransform: Anchor stretch (0,0,1,1)
   - Set Active = true (mặc định hiển thị khi chưa chọn item)
   - Trong đó tạo Text: "Select an item to view details"

### Bước 5: Tạo Empty State cho Item List
1. Tạo GameObject con của LeftPanel:
   - Right-click LeftPanel → UI → Panel
   - Đặt tên: `EmptyStatePanel`
   - RectTransform: Anchor stretch (0,0,1,1), Top: 60
   - Set Active = false (ban đầu ẩn)
   - Trong đó tạo Text: "No items found"

---

## 2. Tạo InventoryItem Prefab

### Bước 1: Tạo Prefab
1. Trong Hierarchy, tạo GameObject mới trong Content (của ItemScrollView):
   - Right-click Content → UI → Button
   - Đặt tên: `InventoryItem`
   - RectTransform: Width: 80, Height: 80 (không cần stretch vì dùng Grid Layout)

2. Drag InventoryItem vào folder Assets/Prefabs/UI/ để tạo prefab
3. Xóa InventoryItem khỏi Hierarchy

### Bước 2: Setup InventoryItem UI
1. Mở InventoryItem prefab trong Project window
2. Trong InventoryItem (Button):
   - Add component **InventoryItem** script
   - Xóa Image component mặc định của Button (hoặc dùng làm background)

3. Tạo các UI elements con:
   - **SelectedBackground** (Image): Anchor stretch (0,0,1,1), Color: Highlight color (ví dụ: vàng nhạt), Set Active = false
   - **IconImage** (Image): Anchor stretch (0,0,1,1), Set Aspect Ratio: 1:1 (để icon vuông)

### Bước 3: Thêm Button onClick
1. Chọn InventoryItem (Button)
2. Trong Button component → On Click ():
   - Click **+** để thêm event
   - Drag & Drop chính InventoryItem vào object field
   - Chọn function: `InventoryItem → OnClick`

---

## 3. Wire InventoryManager

### Bước 1: Wire các trường trong Inspector

**Panel:**
- **Panel**: Drag & Drop chính InventoryPanel vào đây
- **Close Button**: Drag & Drop CloseButton vào đây

**Left Panel - Item List:**
- **Item List Container**: Drag & Drop Content GameObject (trong ItemScrollView) vào đây
- **Inventory Item Prefab**: Drag & Drop InventoryItem prefab vào đây
- **Empty State Panel**: Drag & Drop EmptyStatePanel vào đây

**Filter Buttons:**
- **Filter All Button**: Drag & Drop FilterAllButton vào đây
- **Filter Weapons Button**: Drag & Drop FilterWeaponsButton vào đây
- **Filter Trinkets Button**: Drag & Drop FilterTrinketsButton vào đây

**Right Panel - Item Detail:**
- **Detail Panel**: Drag & Drop DetailPanel vào đây
- **Detail Empty State**: Drag & Drop DetailEmptyState vào đây
- **Detail Icon**: Drag & Drop DetailIcon vào đây
- **Detail Name Text**: Drag & Drop DetailNameText vào đây
- **Detail Type Text**: Drag & Drop DetailTypeText vào đây
- **Detail Stat Text**: Drag & Drop DetailStatText vào đây

**Config:**
- **Config**: Drag & Drop `BackendConfig` asset vào đây

**Registries:**
- **Weapon Registry**: Drag & Drop `WeaponDefinitionRegistry` asset vào đây
- **Trinket Registry**: Drag & Drop `TrinketDefinitionRegistry` asset vào đây

---

## 4. Wire InventoryItem

### Bước 1: Mở InventoryItem prefab
1. Double-click InventoryItem prefab trong Project window

### Bước 2: Wire các trường trong Inspector

**UI Elements:**
- **Icon Image**: Drag & Drop IconImage vào đây
- **Selected Background**: Drag & Drop SelectedBackground vào đây

---

## 5. Tích hợp vào MainMenu

### Trong scene 3_MainMenu.unity:

**Bước 1: Wire Inventory vào MainMenuManager**
1. Tìm GameObject có `MainMenuManager` component
2. Trong Inspector của MainMenuManager:
   - Thêm field: `[SerializeField] private InventoryManager inventoryManager;`
   - **Inventory Manager**: Drag & Drop InventoryPanel vào đây

**Bước 2: Tạo nút mở Inventory**
1. Tạo hoặc chọn GameObject button trong MainMenu (ví dụ: InventoryButton trong Left_UI)
2. Thêm component **Button** (nếu chưa có)
3. Trong Button component → On Click ():
   - Click **+** để thêm event
   - Drag & Drop GameObject có MainMenuManager vào object field
   - Chọn function: `MainMenuManager → OpenInventoryTab`

**Bước 3: Thêm method vào MainMenuManager**
```csharp
public void OpenInventoryTab()
{
    if (inventoryManager != null)
        inventoryManager.Open();
}

public void CloseInventoryTab()
{
    if (inventoryManager != null)
        inventoryManager.Close();
}
```

---

## 6. Test

1. Chạy game
2. Click InventoryButton
3. InventoryPanel sẽ mở và load inventory từ backend
4. Click filter buttons (All, Weapons, Trinkets) để lọc items
5. Click item trong danh sách bên trái để xem chi tiết bên phải
6. Nếu không có item, sẽ hiện EmptyState

---

## Lưu ý

- Inventory data được lấy từ endpoint `/api/PlayerProfile`
- Cần cập nhật WeaponDefinitionRegistry và TrinketDefinitionRegistry assets với đầy đủ thông tin (name, classId, skillId, statModifiers)
- Background asset rương kho báu/túi đồ cần tạo từ AI theo prompt đã cung cấp
- Filter buttons sẽ lọc items theo type (Weapon/Trinket)
- Detail panel hiển thị stats của item (HP, Speed, Skill Point, Damage, Defense)
