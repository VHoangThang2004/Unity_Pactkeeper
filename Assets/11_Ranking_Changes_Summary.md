# Ranking Scene — Changes Summary

## Các thay đổi theo yêu cầu

### ✅ 1. Right Panel — Xóa Username và Level

**Trước:**
- DetailUsernameText
- DetailLevelText
- DetailRankBadgeImage
- DetailRankNameText
- DetailScoreText
- DetailPositionText

**Sau:**
- ~~DetailUsernameText~~ (đã xóa)
- ~~DetailLevelText~~ (đã xóa)
- DetailRankBadgeImage (**clickable** — click để mở popup)
- DetailRankNameText
- DetailScoreText
- DetailPositionText

---

### ✅ 2. Left Panel và Entry Card — Thêm Rank Name Field

**Header Row:**
- Đã thêm cột "**Rank Name**" giữa "Rank Badge" và "Score"
- Tổng 6 cột: #, Username, Level, Rank Badge, **Rank Name**, Score

**LeaderboardEntryCard.cs:**
- Thêm field `rankNameText` (TextMeshPro)
- Hiển thị rank name tiếng Anh: "Beginner", "Master", "Pactkeeper", etc.

**LeaderboardEntryData:**
- Thêm property `rankName` (string)

---

### ✅ 3. Rank Names — Tiếng Anh

Tất cả rank names hiển thị **tiếng Anh**:

| Tier | English Name | Score Range |
|------|--------------|-------------|
| -1 | **Unranked** | No rating |
| 0 | Beginner | 0-999 |
| 1 | Master | 1000-1999 |
| 2 | Captain | 2000-2999 |
| 3 | Pactkeeper | 3000-3999 |
| 4 | Warlord | 4000-4999 |
| 5 | Grand Commander | 5000-5999 |
| 6 | Union | 6000+ |

---

### ✅ 4. API Data — Fetch All Players

**API Endpoints sử dụng:**
1. `GET /api/PlayerProfile` — current player data
2. `GET /api/players/all` — **tất cả players** (backend cần implement)

**Logic:**
- Fetch tất cả players từ `/api/players/all`
- Dùng `gems` field làm score (tạm thời)
- Sort by score descending, then by username alphabetically (for ties)
- Assign positions sau khi sort

**Backend TODO:**
```javascript
GET /api/players/all
Response: {
  "items": [
    {
      "playerId": "abc123",
      "username": "PlayerName",
      "level": 25,
      "gems": 5500,
      "experience": 12000
    },
    // ... more players
  ]
}
```

**Fallback:**
- Nếu API call fail → hiển thị only current player
- Console warning: "Backend may not have implemented /api/players/all yet"

---

### ✅ 5. Unranked Badge Sprite

**Thêm field mới:**
- `unrankedBadgeSprite` (Sprite) — hiển thị cho players chưa có rating

**Inspector:**
- Rank Badge Sprites (Size = 7) — các tier 0-6
- **Unranked Badge Sprite** — sprite riêng cho Unranked

**Logic:**
- Nếu player chưa có rating/score → tier = -1 → hiển thị Unranked badge
- `GetRankBadgeSprite(-1)` trả về `unrankedBadgeSprite`

---

### ✅ 6. Rank Tier Info Popup (Click vào Badge)

**Tính năng mới:**
- Click vào `DetailRankBadgeImage` trong right panel → mở popup horizontal scroll
- Popup hiển thị **8 cards** (Unranked + 7 tiers)
- Mỗi card có:
  - Rank badge (large)
  - Rank name
  - Min score required
  - **Lore description** (story từ Pactkeeper lore)

**RankTierCard.cs** — script mới cho popup cards:
```csharp
public void Setup(RankTierDisplayData data, bool hasCompletedStory)
```

**Lore Lock System:**
- Nếu `hasCompletedStory = false` → lore description bị ẩn
- Hiển thị overlay: "??? Complete the story to unlock lore ???"
- Khi player hoàn thành story (check qua API) → unlock lore
- Unranked lore luôn visible (không cần unlock)

**Lore Content (từ Pactkeeper_Story_Lore.md):**

| Tier | Lore |
|------|------|
| 0 - Beginner | "Varek in his youth, growing up amidst war, with nothing in hand." |
| 1 - Master | "Varek gradually becomes battle-hardened, accumulating combat experience." |
| 2 - Captain | "At 22 years old, becomes the leader of a small warband." |
| 3 - Pactkeeper | "The moment of life and death — forging a pact with the gods, receiving the power of Time Sight." |
| 4 - Warlord | "Using Time Sight to win continuously, beginning to recruit legendary generals from defeated tribes." |
| 5 - Grand Commander | "The warband has gathered all 6 classes of champions, nearly invincible across East and West." |
| 6 - Union | "Unified the entire Thiên Nhãn Giới into one — fulfilling the divine pact." |

---

## Files đã sửa

### Scripts (4 files):
1. **RankingManager.cs** — major changes:
   - Xóa `detailUsernameText`, `detailLevelText` fields
   - Thêm `unrankedBadgeSprite`, popup fields
   - Thêm `allPlayerProfiles` array để store tất cả players
   - **Fetch `/api/players/all`** — backend cần implement endpoint này
   - **`BuildLeaderboardFromPlayers()`** — convert all players → leaderboard entries
   - **Sort logic:** score descending, then username alphabetically (ties)
   - **Score = gems** (temporary until rating system implemented)
   - `DetailRankBadgeImage` auto-add Button component runtime

2. **LeaderboardEntryCard.cs** — minor changes:
   - Thêm field `rankNameText` (TMP_Text)
   - Setup() gán `entryData.rankName` vào text

3. **RankTierCard.cs** — NEW file:
   - Script cho popup rank tier cards
   - Hiển thị badge, rank name, min score, lore description
   - Lock/unlock lore based on story completion

4. **LeaderboardEntryData** (trong RankingManager.cs):
   - Thêm property `rankName` (string)
   - `rankTier = -1` cho Unranked

### Documentation (2 files):
1. **11_Ranking_Setup_Guide.md** — updated:
   - Xóa DetailUsernameText/DetailLevelText steps
   - Thêm RankNameText vào entry card (6 fields thay vì 5)
   - Thêm Bước 8: Rank Tier Info Popup setup
   - Thêm Bước 9: RankTierCard prefab
   - Update Inspector wiring section

2. **11_Ranking_Changes_Summary.md** — NEW file (file này)

---

## Sprites cần tạo

**Từ `11_Ranking_UI_Prompts.md`:**

| Asset | Số lượng | Đã có? |
|-------|----------|--------|
| Scene background | 1 | ❓ |
| Rank badge sprites (tiers 0-6) | 7 | ❓ |
| **Unranked badge sprite** | 1 | ✅ (người dùng đã tạo) |
| Score frame panel | 1 | ❓ |

**Unranked badge prompt** (nếu cần regenerate):
```
2D pixel art rank badge icon for Unranked tier,
simple grey or bronze placeholder badge,
question mark symbol or empty shield,
faded or desaturated color scheme (grey, dark brown),
sense of "not yet ranked" or "beginner starting point",
128x128 pixels, transparent background,
medieval fantasy pixel art style
```

---

## Setup trong Unity (Quick Checklist)

### Left Panel Updates:
- [ ] Header row: thêm "Rank Name" column
- [ ] LeaderboardEntryCard prefab: thêm `RankNameText` (TextMeshPro)
- [ ] Wire `rankNameText` field trong Inspector

### Right Panel Updates:
- [ ] Xóa `DetailUsernameText` và `DetailLevelText` GameObjects
- [ ] `DetailRankBadgeImage`: set Raycast Target = **tích** (để clickable)

### Popup Setup:
- [ ] Tạo `RankTierPopupPanel` với Horizontal ScrollView
- [ ] Tạo `RankTierCard` prefab (280×450) với 5 UI elements
- [ ] Wire popup fields trong RankingManager Inspector

### RankingManager Inspector:
- [ ] Xóa references: DetailUsernameText, DetailLevelText
- [ ] Thêm: Unranked Badge Sprite
- [ ] Thêm: Rank Tier Popup fields (panel, container, prefab, close button)
- [ ] Set: Has Completed Story = **false** (default)

---

## Backend TODO (khi làm API)

### 1. Leaderboard API

**Endpoint:** `GET /api/leaderboard` hoặc `GET /api/ranking`

**Response:**
```json
{
  "items": [
    {
      "position": 1,
      "playerId": "abc123",
      "username": "PlayerName",
      "level": 25,
      "rating": 5500,  // hoặc "mmr", "score"
      "matchesPlayed": 150,
      "wins": 95,
      "losses": 55
    }
  ],
  "currentPlayer": {
    "position": 42,
    "rating": 2800
  }
}
```

**Pagination (optional):**
- Query params: `?limit=50&offset=0`

### 2. Player Rating System

**Add to PlayerProfile:**
```json
{
  "playerId": "abc123",
  "rating": 2800,
  "rankTier": 2,  // 0-6 calculated server-side
  "matchesPlayed": 150,
  "wins": 95,
  "losses": 55
}
```

**Rating calculation:**
- Use ELO, Glicko, or TrueSkill algorithm
- Update after each match
- Store in database

### 3. Story Completion Check

**Add to PlayerProfile hoặc StoryProgress API:**
```json
{
  "hasCompletedStory": false
}
```

**Logic:**
- Check if all chapters completed
- Set `hasCompletedStory = true`
- Frontend sẽ unlock lore descriptions trong popup

---

## Test Steps

1. **Play scene 11_Ranking**
2. Verify:
   - ✅ Current player entry: Unranked badge, score = 0
   - ✅ 7 example entries: các tier từ Beginner → Union
   - ✅ Entry cards có 6 fields (thêm Rank Name)
   - ✅ Click entry → right panel NO username/level
   - ✅ Right panel: badge, rank name, score, position
   - ✅ **Click badge** → popup mở
   - ✅ Popup: 8 cards horizontal scroll
   - ✅ Lore locked (overlay hiển thị)
   - ✅ Close popup hoạt động
3. **Toggle `hasCompletedStory = true` trong Inspector**
4. Play lại → verify lore unlocked

---

## Next Steps

1. **Generate sprites** (nếu chưa có):
   - 7 rank badges (Beginner → Union)
   - Score frame panel
   - Background (war memorial hall theme)

2. **Setup Unity scene** theo `11_Ranking_Setup_Guide.md`

3. **Khi backend làm xong API:**
   - Replace `GeneratePlaceholderLeaderboard()` bằng real API call
   - Map `rating` field từ API → calculate `rankTier` client-side hoặc nhận từ server
   - Fetch `hasCompletedStory` từ API

4. **Polish:**
   - Popup animation (fade in/out)
   - Card hover effects
   - Unlock lore animation khi story completed

---

**File này:** `Assets/11_Ranking_Changes_Summary.md`
