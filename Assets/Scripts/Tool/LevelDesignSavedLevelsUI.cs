using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Builds a matching stone-and-gold library page in the existing world-space Canvas.</summary>
[DisallowMultipleComponent]
public sealed class LevelDesignSavedLevelsUI : MonoBehaviour
{
    private const int PageSize = 4;
    private static readonly Color Gold = new(0.96f, 0.87f, 0.65f, 1f);
    private static readonly Color Muted = new(0.65f, 0.63f, 0.57f, 1f);
    private const string LibraryDescription = "选择一张地图，进入试玩或继续编辑";
    private LevelDesignUIController owner;
    private TMP_Text fontSource;
    private RectTransform pageRoot;
    private RectTransform cardsRoot;
    private Button previousButton;
    private Button nextButton;
    private TMP_Text pageText;
    private TMP_Text countText;
    private TMP_Text statusText;
    private List<LevelDesignSaveData> saves = new();
    private Sprite libraryBackground;
    private Sprite cardSprite;
    private Sprite navigationSprite;
    private Sprite playSprite;
    private Sprite deleteSprite;
    private Sprite editSprite;
    private Coroutine statusCoroutine;
    private int page;
    private bool opening;
    private RectTransform deleteConfirmation;
    private TMP_Text deletePrompt;
    private string pendingDeleteId;

    public void Initialize(LevelDesignUIController controller, TMP_Text textStyle, Image background)
    {
        if (pageRoot != null)
            return;
        owner = controller;
        fontSource = textStyle;
        LoadStyleSprites();
        BuildPage();
        Hide();
    }

    public void Show()
    {
        if (pageRoot == null)
            return;
        page = 0;
        pageRoot.gameObject.SetActive(true);
        pageRoot.SetAsLastSibling();
        Refresh();
    }

    public void Hide()
    {
        CancelDelete();
        if (statusCoroutine != null)
        {
            StopCoroutine(statusCoroutine);
            statusCoroutine = null;
        }
        if (statusText != null)
            statusText.text = LibraryDescription;
        if (pageRoot != null)
            pageRoot.gameObject.SetActive(false);
    }

    public void RefreshIfVisible()
    {
        if (pageRoot != null && pageRoot.gameObject.activeSelf)
            Refresh();
    }

    private void Refresh()
    {
        try
        {
            saves = owner.SaveStore.GetAll();
            page = Mathf.Clamp(page, 0, Mathf.Max(0, (saves.Count - 1) / PageSize));
            RenderPage();
        }
        catch (Exception exception)
        {
            ShowStatus("列表读取失败：" + exception.Message);
        }
    }

    private void LoadStyleSprites()
    {
        libraryBackground = Resources.Load<Sprite>("LevelDesign/关卡库背景");
        cardSprite = Resources.Load<Sprite>("LevelDesign/关卡卡片");
        navigationSprite = Resources.Load<Sprite>("LevelDesign/关卡导航按钮");
        playSprite = Resources.Load<Sprite>("LevelDesign/关卡试玩按钮");
        deleteSprite = Resources.Load<Sprite>("LevelDesign/关卡删除按钮");
        editSprite = Resources.Load<Sprite>("LevelDesign/关卡编辑按钮");
    }

    private void BuildPage()
    {
        pageRoot = CreateRect(transform, "Saved Designed Levels Page", Vector2.zero, new Vector2(1024f, 1024f));
        var blocker = pageRoot.gameObject.AddComponent<Image>();
        blocker.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        blocker.raycastTarget = true;
        CreatePicture(pageRoot, "Library Background", libraryBackground, Vector2.zero,
            new Vector2(1024f, 1024f), false);
        var title = CreateText(pageRoot, "Title", "设计关卡库", new Vector2(-95f, 421f),
            new Vector2(500f, 64f), 36f, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        var subtitle = CreateText(pageRoot, "Subtitle", "已保存的设计关卡",
            new Vector2(-95f, 382f), new Vector2(500f, 34f), 19f, TextAlignmentOptions.Left);
        subtitle.color = Muted;
        CreateButton(pageRoot, "Back To Design", "返回",
            new Vector2(408f, 412f), new Vector2(96f, 50f), Hide, 22f);
        var heading = CreateText(pageRoot, "Maps Heading", "我的地图", new Vector2(-207f, 302f),
            new Vector2(500f, 48f), 25f, TextAlignmentOptions.Left);
        heading.fontStyle = FontStyles.Bold;
        countText = CreateText(pageRoot, "Save Count", string.Empty, new Vector2(307f, 302f),
            new Vector2(300f, 40f), 20f, TextAlignmentOptions.Right);
        countText.color = Muted;
        statusText = CreateText(pageRoot, "Library Hint", LibraryDescription, new Vector2(0f, 265f),
            new Vector2(914f, 40f), 19f, TextAlignmentOptions.Left);
        statusText.color = Muted;
        cardsRoot = CreateRect(pageRoot, "Saved Level Cards", Vector2.zero, new Vector2(940f, 740f));
        previousButton = CreateButton(pageRoot, "Previous Page", "上一页", new Vector2(-327f, -447f),
            new Vector2(220f, 60f), () => ChangePage(-1), 24f);
        nextButton = CreateButton(pageRoot, "Next Page", "下一页", new Vector2(327f, -447f),
            new Vector2(220f, 60f), () => ChangePage(1), 24f);
        pageText = CreateText(pageRoot, "Page Number", string.Empty, new Vector2(0f, -447f),
            new Vector2(280f, 55f), 23f, TextAlignmentOptions.Center);
        BuildDeleteConfirmation();
    }

    private void BuildDeleteConfirmation()
    {
        deleteConfirmation = CreateRect(pageRoot, "Delete Confirmation", Vector2.zero,
            new Vector2(1024f, 1024f));
        var shade = deleteConfirmation.gameObject.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.8f);
        var frame = CreatePicture(deleteConfirmation, "Confirmation Frame", cardSprite,
            Vector2.zero, new Vector2(436f, 296f), true);
        CreateText(frame, "Title", "删除设计关卡", new Vector2(0f, 104f),
            new Vector2(392f, 44f), 28f, TextAlignmentOptions.Center);
        deletePrompt = CreateText(frame, "Prompt", string.Empty, new Vector2(0f, 12f),
            new Vector2(382f, 120f), 22f, TextAlignmentOptions.Center);
        CreateButton(frame, "Cancel Delete", "取消", new Vector2(-106f, -96f),
            new Vector2(176f, 52f), CancelDelete, 22f);
        var confirm = CreateButton(frame, "Confirm Delete", "确认删除", new Vector2(106f, -96f),
            new Vector2(176f, 52f), ConfirmDelete, 22f);
        confirm.GetComponent<Image>().color = new Color(0.82f, 0.56f, 0.49f, 1f);
        CancelDelete();
    }

    private void RequestDelete(string id, string title)
    {
        pendingDeleteId = id;
        deletePrompt.text = $"确定删除「{title}」？\n删除后无法恢复";
        deleteConfirmation.gameObject.SetActive(true);
        deleteConfirmation.SetAsLastSibling();
    }

    private void CancelDelete()
    {
        pendingDeleteId = null;
        if (deleteConfirmation != null)
            deleteConfirmation.gameObject.SetActive(false);
    }

    private void ConfirmDelete()
    {
        var id = pendingDeleteId;
        CancelDelete();
        if (string.IsNullOrEmpty(id))
            return;
        try
        {
            var deleted = owner.DeleteSavedLevel(id);
            Refresh();
            ShowStatus(deleted ? "关卡已删除" : "该关卡已不存在");
        }
        catch (Exception exception)
        {
            ShowStatus("删除失败：" + exception.Message);
        }
    }

    private void ChangePage(int direction)
    {
        page = Mathf.Clamp(page + direction, 0, Mathf.Max(0, (saves.Count - 1) / PageSize));
        RenderPage();
    }

    private void RenderPage()
    {
        for (var index = cardsRoot.childCount - 1; index >= 0; index--)
        {
            var child = cardsRoot.GetChild(index).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        countText.text = $"共 {saves.Count} 个关卡";
        var pages = Mathf.Max(1, (saves.Count + PageSize - 1) / PageSize);
        pageText.text = $"{page + 1} / {pages}";
        previousButton.interactable = page > 0;
        nextButton.interactable = page + 1 < pages;
        if (saves.Count == 0)
        {
            CreateText(cardsRoot, "Empty Library", "还没有保存的关卡\n返回设计页面，绘制路线后点击保存",
                new Vector2(0f, -40f), new Vector2(820f, 170f), 30f, TextAlignmentOptions.Center);
            return;
        }
        for (var slot = 0; slot < PageSize && page * PageSize + slot < saves.Count; slot++)
        {
            var save = saves[page * PageSize + slot];
            var position = new Vector2(slot % 2 == 0 ? -231f : 231f, slot < 2 ? 90f : -228f);
            BuildCard(save, position, saves.Count - (page * PageSize + slot));
        }
    }

    private void BuildCard(LevelDesignSaveData save, Vector2 position, int number)
    {
        var card = CreatePicture(cardsRoot, "Level " + save.id, cardSprite,
            position, new Vector2(436f, 296f), true);
        var button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();
        ConfigureButtonColors(button);
        button.onClick.AddListener(() => Play(save.id));
        UIClickAudioPlayer.Register(button);
        var title = string.IsNullOrEmpty(save.displayName) ? $"设计关卡 {number:D2}" : save.displayName;
        if (title.StartsWith("设计关卡 ") && DateTime.TryParse(title.Substring(5), out _))
            title = $"设计关卡 {number:D2}";
        var nameText = CreateText(card, "Level Name", title, new Vector2(-48f, 121f),
            new Vector2(296f, 40f), 22f, TextAlignmentOptions.Left);
        nameText.fontStyle = FontStyles.Bold;
        var deleteRect = CreatePicture(card, "Delete Level", deleteSprite,
            new Vector2(155f, 121f), new Vector2(88f, 32f), true);
        var deleteButton = deleteRect.gameObject.AddComponent<Button>();
        deleteButton.targetGraphic = deleteRect.GetComponent<Image>();
        ConfigureButtonColors(deleteButton);
        var deleteColors = deleteButton.colors;
        deleteColors.highlightedColor = new Color(1f, 0.82f, 0.72f, 1f);
        deleteColors.pressedColor = new Color(0.75f, 0.54f, 0.45f, 1f);
        deleteButton.colors = deleteColors;
        deleteButton.onClick.AddListener(() => RequestDelete(save.id, title));
        UIClickAudioPlayer.Register(deleteButton);
        var deleteLabel = CreateText(deleteRect, "Delete Label", "删除", new Vector2(10f, 0f),
            new Vector2(46f, 28f), 17f, TextAlignmentOptions.Center);
        deleteLabel.color = new Color(0.87f, 0.72f, 0.59f, 1f);
        var savedTime = save.savedUtcTicks > 0 && save.savedUtcTicks <= DateTime.MaxValue.Ticks
            ? new DateTime(save.savedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm")
            : string.Empty;
        var dimensions = CreateText(card, "Map Dimensions", $"{save.width} × {save.height}",
            new Vector2(106f, 47f), new Vector2(190f, 52f), 32f, TextAlignmentOptions.Left);
        dimensions.fontStyle = FontStyles.Bold;
        var caption = CreateText(card, "Map Caption", "地图尺寸", new Vector2(106f, 13f),
            new Vector2(190f, 32f), 16f, TextAlignmentOptions.Left);
        caption.color = Muted;
        var timeText = CreateText(card, "Saved Time", savedTime, new Vector2(106f, -22f),
            new Vector2(190f, 34f), 18f, TextAlignmentOptions.Left);
        timeText.color = new Color(0.75f, 0.72f, 0.65f, 1f);
        var playPanel = CreatePicture(card, "Play Label Frame", playSprite,
            new Vector2(100f, -77f), new Vector2(196f, 52f), false);
        var playText = CreateText(playPanel, "Play Label", "进入试玩", Vector2.zero,
            new Vector2(182f, 48f), 23f, TextAlignmentOptions.Center);
        playText.color = new Color(0.21f, 0.15f, 0.08f, 1f);
        playText.fontStyle = FontStyles.Bold;
        var editRect = CreatePicture(card, "Edit Level", editSprite,
            new Vector2(-112f, -119f), new Vector2(166f, 38f), true);
        var editButton = editRect.gameObject.AddComponent<Button>();
        editButton.targetGraphic = editRect.GetComponent<Image>();
        ConfigureButtonColors(editButton);
        editButton.onClick.AddListener(() => Edit(save.id));
        UIClickAudioPlayer.Register(editButton);
        var editLabel = CreateText(editRect, "Edit Label", "编辑关卡", new Vector2(14f, 0f),
            new Vector2(110f, 32f), 19f, TextAlignmentOptions.Center);
        editLabel.color = new Color(0.89f, 0.81f, 0.63f, 1f);
        var preview = CreateRect(card, "Map Preview", new Vector2(-112f, -5f), new Vector2(166f, 166f));
        var cellSize = 166f / Mathf.Max(save.width, save.height);
        var startX = -save.width * cellSize * 0.5f + cellSize * 0.5f;
        var startY = save.height * cellSize * 0.5f - cellSize * 0.5f;
        for (var row = 0; row < save.height; row++)
        for (var column = 0; column < save.width; column++)
        {
            var rect = CreateRect(preview, $"Cell {row} {column}",
                new Vector2(startX + column * cellSize, startY - row * cellSize),
                new Vector2(cellSize - 0.4f, cellSize - 0.4f));
            var tile = rect.gameObject.AddComponent<Image>();
            tile.sprite = owner.GetSavedBrushSprite(save, save.brushIndices[row * save.width + column]);
            tile.color = tile.sprite != null ? Color.white : Color.clear;
            tile.preserveAspect = true;
            tile.raycastTarget = false;
        }
    }

    private void Edit(string id)
    {
        if (opening)
            return;
        opening = true;
        try
        {
            if (!owner.EditSavedLevel(id))
                ShowStatus(owner.LastStatusMessage);
        }
        finally
        {
            opening = false;
        }
    }

    private void Play(string id)
    {
        if (opening)
            return;
        opening = true;
        try
        {
            if (owner.PlaySavedLevel(id))
                Hide();
            else
                ShowStatus(owner.LastStatusMessage);
        }
        finally
        {
            opening = false;
        }
    }

    private void ShowStatus(string message)
    {
        if (statusCoroutine != null)
            StopCoroutine(statusCoroutine);
        statusText.text = message;
        statusText.color = Gold;
        statusCoroutine = StartCoroutine(ClearStatusLater());
    }

    private IEnumerator ClearStatusLater()
    {
        yield return new WaitForSecondsRealtime(5f);
        statusText.text = LibraryDescription;
        statusText.color = Muted;
        statusCoroutine = null;
    }

    private RectTransform CreatePicture(Transform parent, string name, Sprite sprite,
        Vector2 position, Vector2 size, bool raycast)
    {
        var panel = CreateRect(parent, name, position, size);
        var image = panel.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = raycast;
        return panel;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 position,
        Vector2 size, UnityAction action, float fontSize)
    {
        var rect = CreatePicture(parent, name, navigationSprite, position, size, true);
        rect.GetComponent<Image>().type = Image.Type.Sliced;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        ConfigureButtonColors(button);
        button.onClick.AddListener(action);
        UIClickAudioPlayer.Register(button);
        CreateText(rect, "Label", label, Vector2.zero, size - new Vector2(14f, 6f),
            fontSize, TextAlignmentOptions.Center);
        return button;
    }

    private static void ConfigureButtonColors(Button button)
    {
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.94f, 0.8f, 1f);
        colors.pressedColor = new Color(0.78f, 0.74f, 0.65f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private TMP_Text CreateText(Transform parent, string name, string text, Vector2 position,
        Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        var rect = CreateRect(parent, name, position, size);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (fontSource != null)
        {
            label.font = fontSource.font;
            label.fontSharedMaterial = fontSource.fontSharedMaterial;
        }
        label.text = text;
        label.color = Gold;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * 0.75f;
        label.fontSizeMax = fontSize;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        if (pageRoot != null)
            Destroy(pageRoot.gameObject);
    }
}
