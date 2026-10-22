# Hướng dẫn dựng VFX Graph - Card 8.3

Code (VfxManager, pool, CombatVfxController) đã nối sẵn. Bạn chỉ cần dựng 4 graph và gán vào `Assets/Data/VfxLibrary`.
Khi chưa gán graph nào, game vẫn chạy bình thường (bỏ qua).

## Quy ước chung cho mọi graph
- Tạo: `Assets > Create > Visual Effects > Visual Effect Graph`, chọn template **Empty** hoặc **Simple Loop**. Lưu vào `Assets/VFX/`.
- **Exposed property** (Blackboard, bấm `+`, tick Exposed):
  - `Color`: kiểu **Color**, bật **HDR**. Dùng cho màu hạt.
  - `Scale`: kiểu **Float**, mặc định 1 (tuỳ chọn, nhân với kích thước).
- **Start Event** mặc định là `OnPlay`, **Stop Event** là `OnStop` (không đổi tên).
- **Output** dùng `Output Particle Quad` (Unlit) hoặc `Output Particle Unlit Quad`. Bật **Use Alpha Clipping** tắt, **Blend Mode = Additive** để có bloom.
- Màu: `Set Color over Life` hoặc `Set Color` = `Color` * hệ số HDR (nhân thêm 2-4 để bloom rõ).
- **Simulation Space** của System: **World** (để vệt lại phía sau khi nhân vật di chuyển).
- Particle Texture: dùng `Default-Particle` hoặc texture chấm tròn mềm.

## 1. DashTrail (gắn cố định lên Player, bật/tắt theo dash)
- Spawn: `Constant Spawn Rate` = **80**/giây. Gắn Start/Stop theo event (mặc định).
- Initialize Particle:
  - Lifetime: `Set Lifetime` = **0.35**.
  - Position: `Set Position (Shape: Sphere)` bán kính **0.25**, quanh gốc VFX (gốc VFX là con của Player, nên hạt sinh ra ngay tại Player).
  - Velocity: `Set Velocity Random` ngẫu nhiên nhỏ ±0.3.
  - Size: `Set Size` = **0.5**.
- Update Particle: `Set Size over Life` từ 1 về 0 (Curve). `Set Color over Life` fade Alpha 1 về 0.
- Output Quad, Additive, Billboard.
- Gán vào `VfxLibrary` ở entry **DashTrail**.

## 2. Slash (vệt chém, 3 nhịp khác màu)
- Spawn: `Single Burst` **30** hạt khi `OnPlay`.
- Initialize Particle:
  - Lifetime **0.18**.
  - Position: `Set Position (Shape: Arc Circle)`, bán kính **1.6**, Arc = **120°**, trục hướng về +Z của VFX. Xoay arc nằm ngang để quét theo mặt phẳng XZ.
  - Velocity: `Set Velocity from Direction & Speed (Tangent)` tốc độ **10** để hạt chạy dọc cung.
- Output Quad: **Orient = Along Velocity**, kích thước X (chiều dài) **0.9**, Y **0.08**, nhân `Scale`.
- Màu = `Color` * 3, Alpha giảm dần theo Life.
- Gán vào entry **Slash**. Màu từng nhịp lấy từ `CombatVfxController` (cyan, magenta, vàng); nhịp 3 có `Scale` = 1.4.

## 3. SoulBurst (sóng xung kích + hạt hút vào)
Hai System trong cùng một graph.
- System A "Shockwave": `Single Burst` **150** hạt, Lifetime **0.5**.
  - Position: `Arc Circle` 360°, bán kính **0.5**. Velocity: hướng **ra ngoài** (Radial) tốc độ **16**.
  - Output Quad **Orient Along Velocity**, X **1.2**, Y **0.15**, màu `Color` * 3, fade Alpha.
- System B "Suck-in": `Single Burst` **80** hạt, Lifetime **0.35**.
  - Position: `Sphere` bán kính **6** (bề mặt). Velocity: hướng **vào tâm** tốc độ **18**.
  - Size 0.2, màu `Color` * 2.
- Gán vào entry **SoulBurst**. Code đã tự gọi `NeonPostFX.Pulse` khi Soul Burst; không cần làm trong graph.

## 4. Impact (khi trúng đòn)
- Spawn: `Single Burst` **18** hạt.
- Initialize: Lifetime **0.22**, Position tại gốc VFX, Velocity `Sphere` ngẫu nhiên tốc độ **4 đến 9**, `Set Size` 0.1.
- Output Quad Orient Along Velocity: X 0.5, Y 0.06, màu `Color` * 3.
- Thêm 1 System nhỏ "Flash": `Single Burst` **1** hạt, Lifetime **0.08**, Size **1.0** nhân `Scale`, Output Additive, fade nhanh.
- **Update Mode** của System này: **Unscaled Delta Time** (để hiệu ứng vẫn chạy khi hit-stop làm timeScale rất nhỏ).
- Gán vào entry **Impact**.

## Gán vào game
1. Chạy `Vanguard > Graphics > Setup VFX Manager` (tạo `VfxLibrary` nếu chưa có).
2. Kéo từng VisualEffectAsset vào đúng entry trong `Assets/Data/VfxLibrary`.
3. Play: dash, đánh combo, đánh trúng quái, kích Soul Burst (phím T nạp nộ, phím Q xả).

## Kiểm tra pool
Trong Play, `VfxManager` chỉ có sẵn các `VFX_<Id>_<n>` ẩn/hiện; Hierarchy không được xuất hiện thêm object `VFX_*` mới khi đánh nhau.
