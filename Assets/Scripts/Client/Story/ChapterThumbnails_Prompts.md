# Story Chapter Assets — AI Image Generation Prompts

## 1. Popup Background Panel (Bảng thông tin chi tiết)

**Mục đích:** Background cho popup hiển thị chi tiết chapter (size 700×520)  
**Yêu cầu:** Bảng màu đen nhạt với khung viền medieval, không có nội dung text

```
2D pixel art medieval UI panel frame,
ornate decorative border with carved stone or wooden frame edges,
intricate corner embellishments with fantasy motifs (leaves, scrollwork, or heraldic patterns),
semi-transparent dark background inside the frame (dark grey or soft black, 70% opacity),
subtle inner shadow or bevel effect to suggest depth,
no text, no buttons, no icons, just the frame and background,
16:9 or 4:3 aspect ratio, pixel art style, 32x32 tile aesthetic, RPG UI design
```

**Kích thước:** 700×520 hoặc 1024×768 (để có viền rõ hơn rồi resize)  
**Import Unity:**
- Texture Type = Sprite (2D and UI)
- Filter Mode = Point (no filter)
- Compression = None (để giữ chi tiết viền)

---

## 2. Lock Icon (Biểu tượng khóa)

**Mục đích:** Hiển thị trong LockOverlay trên các chapter bị locked  
**Yêu cầu:** Icon khóa đơn giản, rõ ràng, pixel art style

```
2D pixel art padlock icon,
medieval fantasy style iron padlock,
simple closed lock with keyhole visible,
dark iron grey with subtle highlight to show metallic surface,
transparent background,
clear silhouette recognizable at small size,
64x64 or 128x128 pixels, high contrast, pixel art RPG style
```

**Kích thước:** 128×128 (scale xuống 64×64 trong Unity nếu cần)  
**Import Unity:**
- Texture Type = Sprite (2D and UI)
- Filter Mode = Point (no filter)

---

## 3. Chapter 0 Thumbnail — "Khởi đầu" (The Beginning)

**Chủ đề:** Mở đầu câu chuyện, khám phá, thiết lập thế giới  
**Vibe:** Bình minh, hy vọng, hành trình mới bắt đầu

```
2D pixel art chapter cover illustration for medieval fantasy story chapter 0,
sunrise over a peaceful medieval village with thatched roof cottages,
young adventurer standing at village gate looking towards distant misty mountains,
dirt path leading into unknown wilderness,
warm golden sunrise lighting, soft morning mist,
peaceful atmosphere with hint of adventure ahead,
detailed pixel art landscape, 32x32 tile style,
vertical portrait orientation 280x320 pixels or similar ratio,
fantasy RPG aesthetic, no text overlay
```

**Kích thước:** 280×400 (hoặc 512×728 rồi resize)  
**Mood:** Peaceful, hopeful, beginning

---

## 4. Chapter 1 Thumbnail — "Cao trào" (Rising Action)

**Chủ đề:** Hành động, thử thách, xung đột  
**Vibe:** Căng thẳng, chiến đấu, khẩn cấp

```
2D pixel art chapter cover illustration for medieval fantasy story chapter 1,
intense battle scene with hero facing dark shadowy enemies,
dramatic storm clouds gathering overhead with lightning strikes,
castle or fortress under siege in background, fires burning,
warrior in mid-combat stance wielding sword against silhouetted foes,
dynamic diagonal composition suggesting movement and danger,
dark dramatic lighting with red and orange highlights from flames,
tense atmosphere full of conflict and stakes,
detailed pixel art action scene, 32x32 tile style,
vertical portrait orientation 280x320 pixels or similar ratio,
fantasy RPG aesthetic, no text overlay
```

**Kích thước:** 280×400 (hoặc 512×728 rồi resize)  
**Mood:** Intense, dramatic, conflict

---

## 5. Chapter 2 Thumbnail — "Hồi kết" (Resolution / Climax)

**Chủ đề:** Kết thúc hồi này, chiến thắng hoặc hy sinh  
**Vibe:** Trang trọng, cảm động, hoàn thành

```
2D pixel art chapter cover illustration for medieval fantasy story chapter 2,
triumphant hero standing atop mountain peak or ruined throne,
sunset or twilight sky with rays of light breaking through clouds,
broken enemy banner or shattered dark artifact in foreground,
peaceful landscape stretching into distance showing peace restored,
hero silhouette against dramatic sky with cape or cloak flowing,
mix of melancholy and victory in the atmosphere,
warm golden and deep blue twilight colors,
sense of conclusion and reflection,
detailed pixel art dramatic scene, 32x32 tile style,
vertical portrait orientation 280x320 pixels or similar ratio,
fantasy RPG aesthetic, no text overlay
```

**Kích thước:** 280×400 (hoặc 512×728 rồi resize)  
**Mood:** Triumphant, bittersweet, conclusive

---

## 6. Chapter 3 Thumbnail — "Coming Soon" (Placeholder)

**Chủ đề:** Tương lai chưa được tiết lộ  
**Vibe:** Bí ẩn, mơ hồ, chờ đợi

```
2D pixel art chapter cover illustration for coming soon chapter,
mysterious silhouetted landscape shrouded in fog and mist,
faint outline of unknown structure or portal in distance,
question mark or enigmatic symbol subtly integrated into scene,
monochromatic or desaturated color palette (greys, muted blues),
sense of mystery and anticipation,
no clear details visible, everything obscured by mist,
vertical portrait orientation 280x320 pixels or similar ratio,
detailed pixel art atmospheric scene, 32x32 tile style,
fantasy RPG aesthetic, no text overlay
```

**Kích thước:** 280×400 (hoặc 512×728 rồi resize)  
**Mood:** Mysterious, unknown, teaser

---

## Hướng dẫn import vào Unity

1. **Generate images** từ các prompt trên (sử dụng DALL·E, Midjourney, Stable Diffusion, hoặc công cụ khác)

2. **Import vào Unity:**
   - Kéo các file ảnh vào `Assets/Sprites/Story/` (tạo folder nếu chưa có)
   - Chọn tất cả sprites → Inspector:
     - **Texture Type:** Sprite (2D and UI)
     - **Pixels Per Unit:** 100 (default) hoặc 32 (nếu muốn giữ pixel art sharp)
     - **Filter Mode:** Point (no filter) — giữ pixel art không bị blur
     - **Compression:** None hoặc Low Quality
     - **Max Size:** 2048 (đủ cho 512×728)
     - Apply

3. **Gán vào StoryProgressManager:**
   - Chọn GameObject `StoryProgressManager` trong scene `10_StoryProgress`
   - Inspector → **Thumbnail Sprites:**
     - **Size = 4** (hoặc số chapter bạn muốn hiển thị)
     - **Element 0:** Chapter0_Thumbnail
     - **Element 1:** Chapter1_Thumbnail
     - **Element 2:** Chapter2_Thumbnail
     - **Element 3:** Chapter3_ComingSoon (hoặc để trống)

4. **Gán Lock Icon vào ChapterCard prefab:**
   - Mở prefab `Assets/Prefabs/ChapterCard`
   - Trong `LockOverlay` → tìm `LockIcon` (Image hoặc child object)
   - Gán sprite khóa vừa import vào `Source Image`

5. **Gán Popup Background:**
   - Scene `10_StoryProgress` → `PopupPanel` → `PopupBackground` (Image)
   - Gán sprite bảng viền vào `Source Image`
   - **Image Type:** Sliced (nếu muốn dùng 9-slice để scale mà giữ viền)
   - Hoặc **Simple** nếu sprite đã đúng kích thước 700×520

---

## Lưu ý kỹ thuật

- **Vertical orientation:** Chapters là portrait card (280×400) nên prompt cần aspect ratio gần 2:3 hoặc 9:16
- **Pixel art style:** Nhấn mạnh "32x32 tile aesthetic" và "pixel art" để tool AI tạo đúng phong cách
- **No text:** Đảm bảo prompt ghi rõ "no text overlay" vì Unity sẽ render text riêng
- **Transparent background:** Lock icon cần background trong suốt, PNG format
- **Consistent palette:** Giữ màu sắc nhất quán với style game (medieval, warm tones)

---

## Nếu không có AI tool

Có thể dùng:
- **Placeholder sprites** — tạo hình chữ nhật màu solid với số chapter ở giữa (Photoshop, GIMP)
- **Free assets** — tìm trên itch.io, OpenGameArt.org (keyword: "pixel art medieval chapter")
- **Commission artist** — thuê pixel artist vẽ theo brief trên

---

**File này:** Lưu trong `Assets/Scripts/Client/Story/ChapterThumbnails_Prompts.md`
