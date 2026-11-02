# TÀI LIỆU BÀN GIAO & ĐỐI CHIẾU KIẾN TRÚC (HANDOFF NOTES)
*Dự án: Pactkeeper - 2D Grid Real-Time Tactical SRPG (Unity 6 + Dedicated Server + ASP.NET Core Backend)*
*Thời điểm ghi nhận: 2026-10-26*

---

## 1. CHI TIẾT KIẾN TRÚC & QUY TẮC ĐÃ CHỐT

### 1.1. Mô hình Scene & Luồng Dữ liệu PvE Story (Additive Overlay Pattern)
- **Kiến trúc Scene**: Các scene Story Battle (`C0_S1.unity` .. `C3_S2.unity`) được thiết kế là các lightweight overlay scene. Toàn bộ thực thể chiến trường, hệ thống mạng (NetworkManager), Camera chiến đấu, và UI trận đấu nằm trong `5_ClientMatch.unity`.
- **Luồng nạp Scene**: Khi một scene PvE được mở, `BattleTutorialStarter.cs` sẽ nạp `5_ClientMatch` theo chế độ cộng dồn (`LoadSceneMode.Additive`). Sau đó, `BattleTutorialStarter` tiến hành nối dây (`WireBattleReferences`) từ `ClientScene` sang `PlotSequencer`, `TutorialTrigger` và đăng ký các `TutorialTarget`.
- **Tutorial Canvas Hierarchy**:
  - `TutorialCanvas` (Screen Space - Overlay, Sorting Order = 50).
  - `BlockerPanel` (Sorting Order = 1000, bắt toàn bộ Raycast khi đang khóa thao tác).
  - `Highlighter` (Sorting Order = 1001, viền phát sáng đối tượng).
  - `HighlightButton` (Sorting Order = 1002, nút proxy hứng tương tác click từ người chơi).
  - `TextBoxPanel` / `Panel` (Sorting Order = 100, khung thoại người dẫn truyện).
  - `NextButton` (Sorting Order = 100, nút bấm chuyển câu thoại).
  - `ReplayBarRoot` (Sorting Order = 1020, thanh chứa nút Skip Scene và Exit to Map).

### 1.2. Quy tắc Định danh & Kết nối Target ID (`TutorialTargetRegistry`)
- **Canonical Target ID (Đã chuẩn hóa 100%)**:
  Toàn bộ 10 file `PlotConfig` (`Plot_C0_S1.asset` .. `Plot_C3_S2.asset`) đã được loại bỏ tiền tố dạng `C0S1_`, `C1S1_` để chuyển thành danh xưng chuẩn:
  - `Unit1`, `Unit2`, `Unit3`, `Unit4`, `Unit5`: Các ô tướng trên Ready Bar (`T1` .. `T5`).
  - `OwnedReadyBar`: Thanh hàng đợi phe ta (`TeamQueue`).
  - `YourEye`, `EnemyEye`: Trạng thái Instant phe ta / phe địch (`TeamInstantStatus`, `EnemyInstantStatus`).
  - `ConfirmButton`, `CancelButton`, `WaitButton`: Các nút điều khiển lượt.
  - `WSKILL`, `CSKILL`, `TSKILL`, `PSKILL`: 4 ô kỹ năng (`Icon_Weapon_Skill`, `Icon_Class_Skill`, `Icon_Trinket_Skill`, `Icon_Passive`).
  - `InspectPanel`, `STYPE`: Khung thông số chiêu và loại chiêu thức.
  - `Skills`, `CharStat`, `X`, `1`, `3`: Cụm Action Menu, bảng chỉ số tướng, nút cuộn Command Log.
- **Serialization trong `5_ClientMatch.unity`**:
  Đã serialize trực tiếp 22 component `TutorialTarget` với các Target ID chuẩn trên vào đúng 22 GameObject của scene `5_ClientMatch.unity`, đảm bảo xuất hiện cố định trong Inspector.

### 1.3. Quy tắc Nghiệp vụ & Dữ liệu Backend (ASP.NET Core & MongoDB)
- **`autoResume = false`**: Bắt buộc áp dụng cho mọi thao tác chọn Chapter/Scene thủ công từ `10_StoryProgress.unity` và trong toàn bộ cấu hình `ChapterConfigs` của Chapter 1, Chapter 2, Chapter 3 trong `SeedData.cs`.
- **Ràng buộc Ngôn ngữ**:
  - Toàn bộ text hiển thị ingame (nội dung thoại `PlotNode.text`, tiêu đề Chapter, mô tả Scene, nhãn UI nút bấm) viết bằng **100% TIẾNG ANH**.
  - Toàn bộ tài liệu kỹ thuật, báo cáo phân tích và hướng dẫn Unity Editor viết bằng **TIẾNG VIỆT**.
- **Quy tắc Dấu câu**: Tuyệt đối không dùng ký tự gạch ngang dài `"—"`, luôn dùng `" - "`.

---

## 2. CÁC PHƯƠNG ÁN ĐÃ LOẠI BỎ (TRÁNH LẶP LẠI)

1. **Không sao chép UI trận đấu vào từng Scene Story**: Loại bỏ hoàn toàn phương án duplicate các GameObject UI trận đấu (`CanvasGroup`, `ActionMenuUI`, `ReadyBar`) vào từng scene `C0_S1`..`C3_S2`. Mọi thành phần trận đấu phải dùng chung từ `5_ClientMatch.unity` theo mô hình Additive Scene.
2. **Không dùng `target.GetComponent<Image>().sprite` để check hiển thị Unit**: Trên thanh Ready Bar, GameObject `T1`/`T2` là container `Mask`, component `Image` của chính nó luôn có sprite bằng null. Phải duyệt tìm sprite ở GameObject con (`GetComponentsInChildren<Image>(true)`).
3. **Không hardcode gán `sizeDelta = targetRect.rect.size`**: `MatchSessionCanvas` và `TutorialCanvas` có `CanvasScaler` khác nhau (`0.5` vs `0`) và các nút chiêu thức có local scale khác 1 (`1.22`, `0.85`). Không được gán thô kích thước mà phải tính toán qua tọa độ thế giới (`GetWorldCorners`).
4. **Không refactor lại cấu trúc code ngoài phạm vi yêu cầu**: Không tự ý sửa đổi code logic của hệ thống khi user chỉ yêu cầu chỉnh sửa dữ liệu asset/scene.
5. **Không dùng auto-timeout cắt ngang lời thoại của người dẫn truyện**: Người chơi phải được tự do đọc toàn bộ lời thoại kết bài của Reinhardt và chủ động click Next để chuyển màn, không tự ngắt sau một số giây gây cảm giác crash game.

---

## 3. ĐIỂM DỪNG HIỆN TẠI (BREAKPOINT)

- **Các phần đã hoàn thành 100%**:
  1. Backend ASP.NET Core & MongoDB (`D:\Backend_Pactkeeper`): Build succeeded, 0 lỗi, 0 cảnh báo. Đã seed đầy đủ dữ liệu 4 Chapter và cấu hình `autoResume = false`.
  2. Data Assets Unity: Đã tạo và cấu hình đủ 7 Grid Maps (`Map_C1S1`..`Map_C3S2`), 7 Encounter Configs (`Encounter_C1_S1`..`Encounter_C3_S2`), 7 Plot Configs (`Plot_C1_S1`..`Plot_C3_S2`).
  3. Client & Server Scenes: Đã tạo đủ 7 Client Scenes (`C1_S1.unity`..`C3_S2.unity`) và 7 Server Scenes (`C1_S1_Server.unity`..`C3_S2_Server.unity`).
  4. Khớp nối Target ID: 10/10 file `PlotConfig` đã được chuẩn hóa Target ID và 22/22 GameObject trong `5_ClientMatch.unity` đã được gắn `TutorialTarget` cố định.
  5. Sửa lỗi `CompleteAndExit` và luồng Chuyển cảnh Liền mạch: Đã áp dụng chuẩn hóa vào `PlotSequencer.cs`, `StoryClient.cs`, và `BattleTutorialStarter.cs`. Build Unity Client thành công với 0 lỗi.

---

## 4. PHÂN TÍCH NGUYÊN NHÂN LỖI `CompleteAndExit` & GIẢI PHÁP ĐÃ TRIỂN KHAI

### 4.1. Ba nguyên nhân gốc rễ khiến `CompleteAndExit` bị kẹt

#### Nguyên nhân 1: Xung đột Raycast & CanvasGroup tại `PlotSequencer.RunCompleteAndExit`
- Trong node cuối cùng của cả 10 file `PlotConfig` (`type: PlotNodeType.CompleteAndExit`), trường `tutorialTargetId` luôn để trống (`""`) và `delay = 0`.
- Khi thực thi nhánh này, `PlotSequencer.cs` kích hoạt `blockerPanel.SetActive(true)` (Sorting Order = 1000) đè lên `nextButton` (Sorting Order = 100, `CanvasGroup.alpha = 0`).
- Người chơi không thấy nút Next và click bị blocker chặn, dẫn đến coroutine bị treo vĩnh viễn ở `WaitUntil(() => !_waitingForInput)`.

#### Nguyên nhân 2: Treo vô hạn tại `NetworkManager.Singleton.Shutdown()` trong `StoryClient.CompleteAndAdvance`
- `WaitUntil(() => !IsListening)` không có timeout bảo vệ khiến quá trình giải phóng socket UDP bị kẹt frame làm chết Coroutine.

#### Nguyên nhân 3: Lỗi định tuyến cắt đứt luồng chuyển cảnh liền mạch trong `StoryClient.RouteToStoryScene`
- Khi hoàn thành 1 màn Battle, code cũ tự động đẩy về `3_MainMenu`, làm đứt gãy luồng chuyển cảnh liền mạch giữa các Scene con trong cùng 1 Chapter (`S1 -> S2 -> S3`).

---

### 4.2. Giải pháp Đã Triển khai (Chuẩn hóa 100% Kiến trúc Pactkeeper)

1. **Khắc phục triệt để tại `PlotSequencer.cs` (`RunCompleteAndExit`)**:
   - **Khi `node.text` có nội dung**: Bắt buộc kích hoạt `nextButton`, set `CanvasGroup.alpha = 1f`, và nâng `Canvas.sortingOrder = 1002` (nổi lên trên `blockerPanel` ở order 1000). Người chơi chủ động đọc toàn bộ lời thoại tổng kết của Reinhardt và click nút Next để hoàn tất màn chơi. **Tuyệt đối không dùng auto-timeout cắt ngang trải nghiệm đọc thoại của người chơi**.
   - **Khi không có cả text lẫn `tutorialTargetId`**: Tự động gọi `_onComplete?.Invoke()` sau `node.delay` (mặc định 1.0s).
2. **Khắc phục timeout tại `StoryClient.cs` (`CompleteAndAdvance`)**:
   - Thay thế `WaitUntil(() => !IsListening)` không giới hạn bằng vòng lặp while có timeout an toàn `waitLimit < 2.0f`, loại bỏ hoàn toàn nguy cơ đóng băng Coroutine khi giải phóng socket UDP.
3. **Định tuyến Chuyển cảnh Liền mạch tại `StoryClient.cs` (`RouteToStoryScene`)**:
   - **Khi Chapter chưa hoàn thành (`!progress.isChapterCompleted`)**: Không đẩy người chơi ra `10_StoryProgress` hay `3_MainMenu`. Hệ thống tự động kích hoạt `StartStoryMatch` để tạo Dedicated Server mới và nạp thẳng sang scene Battle tiếp theo (`C{chapterId}_S{sceneId}`), đảm bảo tính liền mạch cốt truyện `S1 -> S2 -> S3`.
   - **Chỉ khi hoàn thành Scene cuối cùng của Chapter (`progress.isChapterCompleted == true`) HOẶC người chơi chủ động bấm nút Thoát**: Lúc này mới điều hướng về `10_StoryProgress` (hoặc fallback `3_MainMenu`).
