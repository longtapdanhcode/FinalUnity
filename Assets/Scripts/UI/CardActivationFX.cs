using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vanguard.Cards;

namespace Vanguard.UI
{
    /// <summary>
    /// Hiệu ứng kích hoạt khi chọn thẻ (Sprint 8 - Card 8.6): bùng tia lửa tại vị trí thẻ,
    /// với Legendary thêm flash toàn màn hình + sóng xung kích theo màu từng thẻ.
    /// Tự tạo một Canvas overlay tạm thời, tự huỷ sau khi chạy xong; chạy theo unscaledTime.
    /// </summary>
    public sealed class CardActivationFX : MonoBehaviour
    {
        private static Sprite ringSprite;

        private struct Item
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float StartSize;
            public float EndSize;
            public float StartAlpha;
        }

        private readonly List<Item> items = new List<Item>(96);
        private float elapsed;
        private float duration;

        /// <summary>Màu hiệu ứng: Legendary lấy theo từng thẻ, còn lại theo RarityColor.</summary>
        public static Color ResolveColor(UpgradeCardSO card)
        {
            string n = card.GetType().Name;
            if (n.Contains("Meteor")) return new Color32(0xFF, 0x6A, 0x00, 0xFF);
            if (n.Contains("Blood")) return new Color32(0xFF, 0x17, 0x44, 0xFF);
            if (n.Contains("VoidRage")) return new Color32(0xB0, 0x40, 0xFF, 0xFF);
            return card.RarityColor;
        }

        public static void Play(RectTransform cardRect, UpgradeCardSO card)
        {
            if (card == null || cardRect == null) return;

            Canvas source = cardRect.GetComponentInParent<Canvas>();
            Camera cam = source != null && source.renderMode != RenderMode.ScreenSpaceOverlay ? source.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, cardRect.TransformPoint(cardRect.rect.center));

            GameObject root = new GameObject("CardActivationFX", typeof(RectTransform), typeof(Canvas), typeof(CardActivationFX));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CardActivationFX fx = root.GetComponent<CardActivationFX>();
            fx.Build((RectTransform)root.transform, screen, card);
        }

        private void Build(RectTransform root, Vector2 center, UpgradeCardSO card)
        {
            bool legendary = card.Rarity == CardRarity.Legendary;
            Color color = ResolveColor(card);
            float k = Screen.height / 1080f;
            duration = legendary ? 1.5f : 0.8f;

            int count = legendary ? 64 : (card.Rarity == CardRarity.Rare ? 36 : 20);
            for (int i = 0; i < count; i++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float speed = Random.Range(180f, legendary ? 900f : 520f) * k;
                float life = Random.Range(0.35f, legendary ? 1.1f : 0.65f);
                float size = Random.Range(4f, legendary ? 14f : 9f) * k;
                Color c = Color.Lerp(color, Color.white, Random.Range(0.15f, 0.6f));
                AddItem(root, center, null, c, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed, life, size, size * 0.2f, 1f);
            }

            if (legendary)
            {
                EnsureRing();
                AddItem(root, center, ringSprite, color, Vector2.zero, 0.9f, 80f * k, 2200f * k, 0.9f);
                AddItem(root, center, ringSprite, Color.Lerp(color, Color.white, 0.5f), Vector2.zero, 0.65f, 40f * k, 1400f * k, 0.8f);

                // Flash toàn màn hình
                Color flash = Color.Lerp(color, Color.white, 0.35f);
                AddItem(root, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), null, flash, Vector2.zero, 0.45f,
                    Mathf.Max(Screen.width, Screen.height) * 2f, Mathf.Max(Screen.width, Screen.height) * 2f, 0.55f);
            }
        }

        private void AddItem(RectTransform root, Vector2 pos, Sprite sprite, Color color, Vector2 vel,
            float life, float startSize, float endSize, float alpha)
        {
            GameObject go = new GameObject("Fx", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(startSize, startSize);

            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, alpha);

            items.Add(new Item
            {
                Rect = rt, Image = img, Velocity = vel, Life = life, MaxLife = life,
                StartSize = startSize, EndSize = endSize, StartAlpha = alpha
            });
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                if (it.Life <= 0f || it.Rect == null) continue;

                it.Life -= dt;
                float t = 1f - Mathf.Clamp01(it.Life / it.MaxLife);
                float size = Mathf.Lerp(it.StartSize, it.EndSize, t);
                it.Rect.sizeDelta = new Vector2(size, size);
                it.Rect.anchoredPosition += it.Velocity * dt;
                it.Velocity *= Mathf.Exp(-3.5f * dt);

                Color c = it.Image.color;
                c.a = it.StartAlpha * (1f - t) * (1f - t);
                it.Image.color = c;
                items[i] = it;
            }

            if (elapsed >= duration) Destroy(gameObject);
        }

        private static void EnsureRing()
        {
            if (ringSprite != null) return;

            const int res = 128;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "CardFX_Ring"
            };
            Color[] px = new Color[res * res];
            float c = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) / 0.1f);
                    px[y * res + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
            ringSprite.name = "CardFX_Ring";
        }
    }
}
