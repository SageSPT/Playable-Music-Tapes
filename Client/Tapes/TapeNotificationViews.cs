using System;
using System.Collections.Generic;
using System.IO;
using EFT;
using EFT.Communications;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableMusicTapes.Client;

internal static class TapeNotificationViews
{
    public static readonly Color TitleColor = new Color(1f, 0.95686275f, 0.8235294f, 1f);
    public static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.788f);

    private const string TextGroupName = "Text group";
    private const float FontSize = 16f;
    private const float TextGroupWidth = 440f;
    private const float LineSpacing = -5f;
    private const float IconColumnWidth = 60f;
    private const float FoundNotificationIndent = 10f;
    private const float ProgressIconOffset = -13.3f;

    private static readonly Vector2 IconSize = new Vector2(30f, 24f);
    private static readonly Color StorylineTint = new Color(1f, 1f, 1f, 0.7843137f);
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

    private static string UiFolder => Path.Combine(TapeCatalog.DataDir, "ui");

    public static BaseNotificationView Create(INotificationViewFactory factory)
    {
        if (!(factory is NotifierView notifier) || notifier._defaultNotificationTemplate == null)
        {
            return null;
        }

        BaseNotificationView view = UnityEngine.Object.Instantiate(notifier._defaultNotificationTemplate, notifier._container, false);
        notifier.SetupNotificationView(view);
        view.gameObject.AddComponent<TapeNotificationMarker>();
        return view;
    }

    public static List<TextMeshProUGUI> AddLines(BaseNotificationView view, Color color, TMP_Text insertBefore, params string[] texts)
    {
        var lines = new List<TextMeshProUGUI>();
        Transform group = TextGroup(view);
        int index = insertBefore != null ? insertBefore.transform.GetSiblingIndex() : view._text.transform.GetSiblingIndex() + 1;

        foreach (string text in texts)
        {
            GameObject copy = UnityEngine.Object.Instantiate(view._text.gameObject, group, false);
            copy.name = "TapeLine";
            copy.transform.SetSiblingIndex(index);
            index++;

            var line = copy.GetComponent<TextMeshProUGUI>();
            if (line == null)
            {
                UnityEngine.Object.Destroy(copy);
                continue;
            }

            line.text = text;
            line.color = color;
            ApplyTextStyle(line);
            lines.Add(line);
        }

        return lines;
    }

    public static void SetBackground(BaseNotificationView view, bool storyline)
    {
        if (view._background == null)
        {
            return;
        }

        view._background.color = BackgroundColor;
        if (!storyline)
        {
            return;
        }

        Sprite sprite = Load("EFT_Notification_Storylain_Background_1");
        if (sprite == null)
        {
            return;
        }

        var storylineObject = new GameObject("bg", typeof(RectTransform));
        var rect = (RectTransform)storylineObject.transform;
        rect.SetParent(view._background.transform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        storylineObject.AddComponent<LayoutElement>().ignoreLayout = true;

        var image = storylineObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = StorylineTint;
        image.raycastTarget = false;
    }

    public static void Finish(BaseNotificationView view, Notification notification)
    {
        view._text.fontSize = FontSize;
        view.Init(notification);
        view._text.color = Color.white;

        Sprite cassette = Load("Cassetes_Icon_Big_0");
        if (cassette != null && view._icon != null)
        {
            view._icon.sprite = cassette;
            view._icon.preserveAspect = true;
            view._icon.rectTransform.sizeDelta = IconSize;
        }
    }

    public static void ArrangeColumns(BaseNotificationView view, bool isProgress)
    {
        Transform group = view._text.transform.parent;
        HorizontalLayoutGroup row = group != null ? group.parent?.GetComponent<HorizontalLayoutGroup>() : null;
        if (row == null)
        {
            return;
        }

        float indent = isProgress ? 0f : FoundNotificationIndent;
        row.padding = new RectOffset((int)(indent + IconColumnWidth), 0, row.padding.top, row.padding.bottom);
        row.spacing = 0f;

        if (view._icon == null)
        {
            return;
        }

        RectTransform icon = view._icon.rectTransform;
        Vector2 anchor = isProgress ? new Vector2(0f, 1f) : new Vector2(0f, 0.5f);
        icon.anchorMin = anchor;
        icon.anchorMax = anchor;
        icon.pivot = new Vector2(0.5f, 0.5f);
        icon.anchoredPosition = new Vector2(indent + IconColumnWidth / 2f, isProgress ? ProgressIconOffset : 0f);
    }

    public static void Warn(string key, string fallback)
    {
        NotificationManager.DisplayWarningNotification(Localize(key, fallback));
    }

    public static void WarnNoRecorder()
    {
        Warn("Tapes/EquipRecorderRaid", "Equip recorder first");
    }

    public static void WarnNoTape()
    {
        Warn("Tapes/NoTapes", "No tapes");
    }

    public static string Localize(string key, string fallback)
    {
        try
        {
            string value = key.Localized(EStringCase.None);
            if (string.IsNullOrEmpty(value) || value == key)
            {
                return fallback;
            }

            return value;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public static Sprite Load(string name)
    {
        if (Sprites.TryGetValue(name, out Sprite cached))
        {
            return cached;
        }

        Sprite sprite = null;
        string path = Path.Combine(UiFolder, name + ".png");
        if (File.Exists(path))
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            texture.name = name;

            var area = new Rect(0, 0, texture.width, texture.height);
            sprite = Sprite.Create(texture, area, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero);
            sprite.name = name;
        }

        Sprites[name] = sprite;
        return sprite;
    }

    private static Transform TextGroup(BaseNotificationView view)
    {
        Transform parent = view._text.transform.parent;
        if (parent.name == TextGroupName)
        {
            return parent;
        }

        return CreateTextGroup(view, parent);
    }

    private static Transform CreateTextGroup(BaseNotificationView view, Transform row)
    {
        var groupObject = new GameObject(TextGroupName, typeof(RectTransform));
        var rect = (RectTransform)groupObject.transform;
        rect.SetParent(row, false);
        rect.SetSiblingIndex(view._text.transform.GetSiblingIndex());
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(TextGroupWidth, rect.sizeDelta.y);

        groupObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = groupObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = LineSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var size = groupObject.AddComponent<LayoutElement>();
        size.preferredWidth = TextGroupWidth;
        size.minWidth = TextGroupWidth;

        view._text.transform.SetParent(rect, false);
        ApplyTextStyle(view._text);
        return rect;
    }

    private static void ApplyTextStyle(TMP_Text text)
    {
        text.fontSize = FontSize;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.margin = new Vector4(0f, 4f, 0f, 5f);

        var layout = text.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = -1f;
            layout.flexibleWidth = -1f;
        }
    }
}
