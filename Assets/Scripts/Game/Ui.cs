using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ParcelSort.Game
{
    /// <summary>
    /// Small helpers for building the prototype's UI from code, so the project needs no
    /// prefabs or hand-made scenes yet. Positions are in the 1080x1920 reference resolution.
    /// </summary>
    public static class Ui
    {
        static Font font;
        static Sprite square;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var tex = Texture2D.whiteTexture;
                    square = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                return square;
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>A rect anchored to the parent's top-left; y grows downward.</summary>
        public static RectTransform Place(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A rect that fills its parent.</summary>
        public static RectTransform Fill(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Box(Transform parent, Color color, float x, float y, float w, float h, string name = "Box")
        {
            var image = Place(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = Square;
            image.color = color;
            return image;
        }

        public static Text Label(Transform parent, string text, int size, Color color, float x, float y, float w, float h,
            TextAnchor align = TextAnchor.MiddleCenter)
        {
            var label = Place("Label", parent, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            return label;
        }

        public static Button TextButton(Transform parent, string text, Color color, float x, float y, float w, float h, UnityAction onClick)
        {
            var image = Box(parent, color, x, y, w, h, "Button " + text);
            var button = image.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            Label(image.transform, text, 40, Color.white, 0, 0, w, h);
            return button;
        }

        /// <summary>A colored box that fills its parent.</summary>
        public static Image FillBox(Transform parent, Color color, string name)
        {
            var image = Fill(name, parent).gameObject.AddComponent<Image>();
            image.sprite = Square;
            image.color = color;
            return image;
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
