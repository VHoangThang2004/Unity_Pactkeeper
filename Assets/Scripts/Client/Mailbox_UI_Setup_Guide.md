# Mailbox UI Setup Guide

## Tổng quan
Hướng dẫn thiết lập UI cho Mailbox trong Unity Inspector với layout 2 panel (trái - phải) theo phong cách cuốn sách mở/cuộn giấy trung cổ.

---

## 1. Tạo Mailbox Prefab

### Bước 1: Tạo Panel chính
1. Trong scene MainMenu, tạo GameObject mới:
   - Right-click Hierarchy → UI → Panel
   - Đặt tên: `MailboxPanel`
   - RectTransform: Anchor stretch (0,0,1,1), Size: Full screen
   - Add component **MailboxManager**

2. Thêm background image (sau khi tạo asset từ AI):
   - Trong MailboxPanel → Image component
   - Set sprite: Asset cuốn sách mở/cuộn giấy
   - Type: Simple
   - Color: White

### Bước 2: Tạo Header
1. Tạo GameObject con của MailboxPanel:
   - Right-click MailboxPanel → UI → Panel
   - Đặt tên: `Header`
   - RectTransform: Anchor top stretch (0,1,1,1), Height: 80

2. Trong Header, tạo các UI elements:
   - **TitleText** (TMP_Text): "Mailbox", Anchor left-center, Font size: 24
   - **CloseButton** (Button): Anchor right-center, Size: 40x40

### Bước 3: Tạo Left Panel (Danh sách thông báo)
1. Tạo GameObject con của MailboxPanel:
   - Right-click MailboxPanel → UI → Panel
   - Đặt tên: `LeftPanel`
   - RectTransform: Anchor stretch (0,0,0.5,1), Top: 80, Bottom: 0
   - Add component **Vertical Layout Group**
   - Spacing: 10
   - Padding: Top 10, Bottom 10, Left 10, Right 10
   - Child Alignment: Upper Center
   - Child Force Expand: Width = checked, Height = unchecked

2. Trong LeftPanel, tạo:
   - **NotificationListContainer** (Panel với Vertical Layout Group)
     - RectTransform: Anchor stretch (0,0,1,1)
     - Add component **Vertical Layout Group**
     - Spacing: 5
     - Child Alignment: Upper Center
     - Child Force Expand: Width = checked, Height = unchecked

   - **ReadAllButton** (Button)
     - RectTransform: Anchor bottom stretch (0,1,0,1), Height: 50
     - Text: "Read All"
     - Position: Bottom of LeftPanel

### Bước 4: Tạo Right Panel (Chi tiết thông báo)
1. Tạo GameObject con của MailboxPanel:
   - Right-click MailboxPanel → UI → Panel
   - Đặt tên: `RightPanel`
   - RectTransform: Anchor stretch (0.5,0,1,1), Top: 80, Bottom: 0

2. Trong RightPanel, tạo:
   - **DetailPanel** (Panel)
     - RectTransform: Anchor stretch (0,0,1,1)
     - Set Active = false (ban đầu ẩn)

   - Trong DetailPanel:
     - **DetailTypeIcon** (Image): Anchor top-center, Size: 80x80, Position Y: -20
     - **DetailTitleText** (TMP_Text): Anchor top-center, Position Y: -120, Font size: 20
     - **DetailMessageText** (TMP_Text): Anchor stretch (0,0,1,1), Top: -150, Bottom: 50, Font size: 16
     - **DetailTimeText** (TMP_Text): Anchor bottom-center, Position Y: 20, Font size: 12

   - **DetailEmptyState** (Panel)
     - RectTransform: Anchor stretch (0,0,1,1)
     - Set Active = true (ban đầu hiện)
     - Trong đó: **Text** (TMP_Text): "Select a notification to view details", Anchor center

### Bước 5: Tạo EmptyState (cho toàn bộ mailbox)
1. Tạo GameObject con của MailboxPanel:
   - Right-click MailboxPanel → UI → Panel
   - Đặt tên: `EmptyState`
   - RectTransform: Anchor stretch (0,0,1,1), Top: 80, Bottom: 0
   - Set Active = false (ban đầu ẩn)

2. Trong EmptyState, tạo:
   - **Text** (TMP_Text): "No notifications", Anchor center, Font size: 18

---

## 2. Tạo NotificationItem Prefab

### Bước 1: Tạo Prefab
1. Trong Hierarchy, tạo GameObject mới trong LeftPanel:
   - Right-click LeftPanel → UI → Button
   - Đặt tên: `NotificationItem`
   - RectTransform: Anchor horizontal stretch (0,1), Height: 80

2. Drag NotificationItem vào folder Assets/Prefabs/UI/ để tạo prefab
3. Xóa NotificationItem khỏi Hierarchy

### Bước 2: Setup NotificationItem UI
1. Mở NotificationItem prefab trong Project window
2. Trong NotificationItem (Button):
   - Add component **NotificationItem** script

3. Tạo các UI elements con:
   - **SelectedBackground** (Image): Anchor stretch (0,0,1,1), Color: Highlight color (ví dụ: vàng nhạt), Set Active = false
   - **TypeIcon** (Image): Anchor left, Size: 50x50, Position X: 10
   - **TitleText** (TMP_Text): Anchor left, Position X: 70, Y: 15, Font size: 14
   - **MessageText** (TMP_Text): Anchor left, Position X: 70, Y: -10, Font size: 12
   - **TimeText** (TMP_Text): Anchor right, Position X: -10, Font size: 10
   - **UnreadIndicator** (Image): Anchor right, Size: 8x8, Position X: -40, Color: Red

---

## 3. Wire MailboxManager

### Bước 1: Wire các trường trong Inspector

**Config:**
- **Config**: Drag & Drop `ClientBackendConfig` asset vào đây

**Panel:**
- **Panel**: Drag & Drop chính MailboxPanel vào đây
- **Close Button**: Drag & Drop CloseButton vào đây

**Left Panel - List:**
- **Notification List Container**: Drag & Drop NotificationListContainer vào đây
- **Notification Item Prefab**: Drag & Drop NotificationItem prefab vào đây
- **Read All Button**: Drag & Drop ReadAllButton vào đây

**Right Panel - Detail:**
- **Detail Panel**: Drag & Drop DetailPanel vào đây
- **Detail Title Text**: Drag & Drop DetailTitleText vào đây
- **Detail Message Text**: Drag & Drop DetailMessageText vào đây
- **Detail Time Text**: Drag & Drop DetailTimeText vào đây
- **Detail Type Icon**: Drag & Drop DetailTypeIcon vào đây
- **Detail Empty State**: Drag & Drop DetailEmptyState vào đây

**Empty State:**
- **Empty State Panel**: Drag & Drop EmptyState GameObject vào đây

---

## 4. Wire NotificationItem

### Bước 2: Wire các trường trong Inspector

**UI Elements:**
- **Type Icon**: Drag & Drop TypeIcon Image vào đây
- **Title Text**: Drag & Drop TitleText vào đây
- **Message Text**: Drag & Drop MessageText vào đây
- **Time Text**: Drag & Drop TimeText vào đây
- **Unread Indicator**: Drag & Drop UnreadIndicator Image vào đây
- **Selected Background**: Drag & Drop SelectedBackground Image vào đây

### Bước 3: Thêm Button onClick
1. Chọn NotificationItem (Button)
2. Trong Button component → On Click ():
   - Click **+** để thêm event
   - Drag & Drop chính NotificationItem vào object field
   - Chọn function: `NotificationItem → OnClick`

---

## 4. Tích hợp vào MainMenu

### Trong scene 3_MainMenu.unity:

**Bước 1: Wire Mailbox vào MainMenuManager**
1. Tìm GameObject có `MainMenuManager` component
2. Trong Inspector của MainMenuManager:
   - Thêm field: `[SerializeField] private MailboxManager mailboxManager;`
   - **Mailbox Manager**: Drag & Drop MailboxPanel vào đây

**Bước 2: Tạo nút mở Mailbox**
1. Tạo hoặc chọn GameObject button trong MainMenu (ví dụ: MailboxButton trong Left_UI)
2. Thêm component **Button** (nếu chưa có)
3. Trong Button component → On Click ():
   - Click **+** để thêm event
   - Drag & Drop GameObject có MainMenuManager vào object field
   - Chọn function: `MainMenuManager → OpenMailbox`

**Bước 3: Thêm method vào MainMenuManager**
```csharp
public void OpenMailbox()
{
    if (mailboxManager != null)
        mailboxManager.Open();
}
```

---

## 5. Test

1. Chạy game
2. Click MailboxButton
3. MailboxPanel sẽ mở và load notifications từ backend
4. Nếu không có notification, sẽ hiện EmptyState
5. Click notification item bên trái để xem chi tiết bên phải
6. Notification sẽ được mark as read khi click
7. Click "Read All" để mark tất cả là đã đọc

---

## Lưu ý

- Đảm bảo backend đang chạy và endpoint `/api/Notification` hoạt động
- Đảm bảo endpoint `/api/Notification/read-all` tồn tại cho nút Read All
- Đảm bảo `PlayerSession.Token` có giá trị (đã login)
- Có thể tùy chỉnh icon cho TypeIcon dựa trên notification.type
- Có thể format thời gian trong MailboxManager.FormatTime()
- Background asset cuốn sách/cuộn giấy cần tạo từ AI theo prompt đã cung cấp
