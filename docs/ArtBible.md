# Art Bible - Vanguard: Astral Pit

Phong cách: **stylized neon / sci-fi void**. Nền tối, vật thể phát sáng, đọc được trong 0,2 giây.
Bảng màu và cường độ nằm trong `Assets/Data/ArtPalette.asset` (`ArtPaletteSO`). Không hardcode màu ở nơi khác.

## 1. Nguyên tắc chung
1. **Tối ở nền, sáng ở thông tin.** Chỉ thứ cần chú ý mới phát sáng; càng quan trọng càng sáng.
2. **Màu = phe/ý nghĩa**, không dùng làm trang trí. Một màu chỉ mang một nghĩa.
3. **Silhouette đọc được khi tắt màu.** Mỗi loại kẻ địch phải nhận ra được chỉ qua hình khối.
4. **Bloom là gia vị.** Ngưỡng Bloom 0,6: mọi thứ vượt 0,6 sẽ phát quầng sáng, nên đừng đặt emissive cao cho vật không quan trọng.

## 2. Bảng màu

| Vai trò | Màu | Ghi chú |
|---|---|---|
| Nền / Void | đen tím (0.02, 0.01, 0.06) và tím sẫm (0.12, 0.04, 0.25) | skybox, fog, sàn |
| Người chơi | **Cyan** (0, 0.8, 1.2) | duy nhất người chơi và đồng minh dùng cyan |
| Kẻ địch thường | **Đỏ** (1, 0.15, 0.12) | Crawler |
| Phe Hư Vô | **Tím** (0.6, 0.2, 1) | Void Caster, đạn Void |
| Tự sát/Nổ | **Cam** (1, 0.35, 0.05) | Volatile Husk, vụ nổ |
| Boss | **Magenta** (0.9, 0.2, 1.6) | Gargoyle, Void Warden |
| Nguy hiểm/Cảnh báo | **Đỏ sáng** (1, 0.1, 0.1) | telegraph, bẫy; luôn sáng nhất |
| Thẻ Common / Rare / Legendary | xám xanh / xanh dương / **vàng** | độ hiếm càng cao càng sáng |

Màu lưới sàn: cyan nhạt (vạch thường) và magenta (vạch đậm mỗi 5 ô). Đây là nền nên giữ cường độ thấp hơn mọi thứ khác.

## 3. Quy tắc emissive (nhân với màu gốc)

Từ thấp đến cao, thứ càng cao càng thu hút mắt:

| Bậc | Hệ số | Dùng cho |
|---|---|---|
| Nền | 1,5 | lưới sàn, trang trí |
| Kẻ địch thường | 2 | thân, mắt, rim |
| Người chơi | 3 | luôn nổi hơn kẻ địch thường |
| Nguy hiểm | 4 | telegraph, vùng sát thương, bẫy |
| FX chiêu cuối | 6 | Soul Burst, boss, thẻ Legendary |

Quy tắc phụ:
- Rim light dùng cùng tông với màu phe, không dùng màu khác phe.
- Hit flash luôn trắng sáng (`_FlashColor` ~4), kéo dài 0,08 giây.
- Không để hai vật khác nghĩa có cùng màu và cùng cường độ.

## 4. Silhouette từng loại kẻ địch

| Loại | Hình khối | Chuyển động | Màu |
|---|---|---|---|
| Crawler | thấp, rộng, nhiều chân | bò nhanh, sát đất | đỏ |
| Void Caster | cao, mảnh, có "mũ"/vòng phía trên | lơ lửng, giữ khoảng cách | tím |
| Volatile Husk | tròn, phình, có khe nứt phát sáng | lăn/lao thẳng, nhấp nháy tăng dần | cam |
| Gargoyle (mini-boss) | to, có cánh, vai rộng | nặng, lao xuống | magenta + mắt đỏ |
| Void Warden (final boss) | cao nhất, giáp nhiều tầng, vòng năng lượng | chậm, chia phase rõ | magenta + cyan lõi |
| Người chơi | cân đối, gọn, áo choàng/vệt cyan | nhanh, dash có vệt | cyan |

Kiểm tra: chụp màn hình, chuyển sang thang xám, mỗi loại vẫn phải phân biệt được.

## 5. Nên và không nên

**Nên**
- Dùng màu từ `ArtPaletteSO`.
- Giữ nền tối, tăng độ sáng ở thông tin chiến đấu.
- Telegraph luôn đọc được trên nền tối ngay cả khi có nhiều VFX.

**Không nên**
- Dùng cyan cho kẻ địch hoặc đỏ cho người chơi.
- Đặt emissive cao cho trang trí nền (cạnh tranh sự chú ý với telegraph).
- Dùng bloom cao đến mức HUD bị nhoè.
- Thêm màu mới khi chưa cập nhật bảng này.

## 6. Tham chiếu kỹ thuật
- Shader nhân vật/vật thể: `Vanguard/NeonLit`. Sàn: `Vanguard/NeonGrid`. Trời: `Vanguard/VoidSky`.
- Post-FX: `NeonPostFX` trên Global Volume (Bloom ngưỡng 0,6, Vignette, Chromatic Aberration).
- Tool thiết lập nằm ở menu **Vanguard > Graphics**.
