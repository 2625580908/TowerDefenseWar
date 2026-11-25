using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("UI/Image Matrix Generator")]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UIImageMatrixGenerator : MonoBehaviour
{
    private const string GridRootName = "Generated Image Grid";

    [Header("矩阵数量")]
    [Tooltip("横向 Image 数量。")]
    [Min(1)] public int widthCount = 10;
    [Tooltip("纵向 Image 数量。")]
    [Min(1)] public int heightCount = 10;

    [Header("间距")]
    [Tooltip("矩阵距离父 UI 左边的像素间距，支持负数。")]
    public float leftSpacing;
    [Tooltip("矩阵距离父 UI 右边的像素间距，支持负数。")]
    public float rightSpacing;
    [Tooltip("矩阵距离父 UI 上边的像素间距，支持负数。")]
    public float topSpacing;
    [Tooltip("矩阵距离父 UI 下边的像素间距，支持负数。")]
    public float bottomSpacing;
    [Tooltip("相邻 Image 之间的水平像素间距，负数会产生重叠。")]
    public float horizontalSpacing;
    [Tooltip("相邻 Image 之间的垂直像素间距，负数会产生重叠。")]
    public float verticalSpacing;

    [Header("Image 设置")]
    [Tooltip("所有生成 Image 默认使用的图片。")]
    public Sprite defaultSprite;
    public Color imageColor = Color.white;
    public bool preserveAspect;
    public bool raycastTarget;

    [Header("单元格尺寸")]
    [Tooltip("启用后，每个 Image 使用固定正方形尺寸，矩阵不会随父 UI 拉伸变形。")]
    public bool useFixedSquareCells = true;
    [Tooltip("启用后，根据父 UI 宽度自动计算正方形边长，横向始终完整显示。")]
    public bool fitCellSizeToParentWidth = true;
    [Tooltip("宽度适配时预留的右侧像素空间，例如纵向滑动条宽度。")]
    [Min(0f)] public float fitWidthReservedSpace;
    [Tooltip("未启用宽度适配时使用的正方形像素边长。")]
    [Min(1f)] public float cellSize = 80f;

    [Header("生成设置")]
    [Tooltip("运行时启用组件时自动生成矩阵。")]
    public bool generateOnStart = true;
    [Tooltip("在 Inspector 修改数量、间距或图片时自动重新生成。")]
    public bool autoRefresh = true;

    [SerializeField, HideInInspector] private RectTransform gridRoot;
    private readonly List<Image> generatedImages = new();
    private bool runtimeRefreshRequested;
    private float resolvedCellSize = 80f;

#if UNITY_EDITOR
    private bool editorRefreshQueued;
#endif

    public int ImageCount => Mathf.Max(1, widthCount) * Mathf.Max(1, heightCount);
    public IReadOnlyList<Image> GeneratedImages => generatedImages;
    public RectTransform GridRoot => gridRoot;
    public float ResolvedCellSize => resolvedCellSize;
    public event Action<int, Image> ImageClicked;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateGrid();
        }
    }

    private void LateUpdate()
    {
        if (!runtimeRefreshRequested)
        {
            return;
        }

        runtimeRefreshRequested = false;
        GenerateGrid();
    }

    [ContextMenu("Generate Image Grid")]
    public void GenerateGrid()
    {
        runtimeRefreshRequested = false;
        widthCount = Mathf.Max(1, widthCount);
        heightCount = Mathf.Max(1, heightCount);

        EnsureGridRoot();
        ClearGeneratedImages();

        for (var row = 0; row < heightCount; row++)
        {
            for (var column = 0; column < widthCount; column++)
            {
                CreateImage(row, column);
            }
        }
    }

    [ContextMenu("Clear Image Grid")]
    public void ClearGrid()
    {
        if (gridRoot == null)
        {
            gridRoot = transform.Find(GridRootName) as RectTransform;
        }

        ClearGeneratedImages();
    }

    private void EnsureGridRoot()
    {
        if (gridRoot == null)
        {
            gridRoot = transform.Find(GridRootName) as RectTransform;
        }

        if (gridRoot == null)
        {
            var rootObject = new GameObject(GridRootName, typeof(RectTransform));
            gridRoot = rootObject.GetComponent<RectTransform>();
            gridRoot.SetParent(transform, false);
        }

        if (useFixedSquareCells)
        {
            resolvedCellSize = ResolveCellSize();
            var contentWidth = leftSpacing
                + rightSpacing
                + fitWidthReservedSpace
                + widthCount * resolvedCellSize
                + (widthCount - 1) * horizontalSpacing;
            var contentHeight = topSpacing
                + bottomSpacing
                + heightCount * resolvedCellSize
                + (heightCount - 1) * verticalSpacing;

            gridRoot.anchorMin = new Vector2(0f, 1f);
            gridRoot.anchorMax = new Vector2(0f, 1f);
            gridRoot.pivot = new Vector2(0f, 1f);
            gridRoot.anchoredPosition = Vector2.zero;
            gridRoot.sizeDelta = new Vector2(
                Mathf.Max(1f, contentWidth),
                Mathf.Max(1f, contentHeight));
        }
        else
        {
            gridRoot.anchorMin = Vector2.zero;
            gridRoot.anchorMax = Vector2.one;
            gridRoot.pivot = new Vector2(0.5f, 0.5f);
            gridRoot.offsetMin = Vector2.zero;
            gridRoot.offsetMax = Vector2.zero;
        }

        gridRoot.localScale = Vector3.one;
        gridRoot.SetAsFirstSibling();
    }

    private void CreateImage(int row, int column)
    {
        var cellIndex = row * widthCount + column;
        var imageObject = new GameObject(
            $"Image {cellIndex + 1:D3}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        var imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(gridRoot, false);

        if (useFixedSquareCells)
        {
            imageRect.anchorMin = new Vector2(0f, 1f);
            imageRect.anchorMax = new Vector2(0f, 1f);
            imageRect.pivot = new Vector2(0f, 1f);
            imageRect.anchoredPosition = new Vector2(
                leftSpacing + column * (resolvedCellSize + horizontalSpacing),
                -topSpacing - row * (resolvedCellSize + verticalSpacing));
            imageRect.sizeDelta = new Vector2(resolvedCellSize, resolvedCellSize);
        }
        else
        {
            var left = (float)column / widthCount;
            var right = (float)(column + 1) / widthCount;
            var top = 1f - (float)row / heightCount;
            var bottom = 1f - (float)(row + 1) / heightCount;
            var totalHorizontalSpacing = leftSpacing
                + rightSpacing
                + horizontalSpacing * (widthCount - 1);
            var totalVerticalSpacing = topSpacing
                + bottomSpacing
                + verticalSpacing * (heightCount - 1);

            imageRect.anchorMin = new Vector2(left, bottom);
            imageRect.anchorMax = new Vector2(right, top);
            imageRect.offsetMin = new Vector2(
                leftSpacing + column * (horizontalSpacing - totalHorizontalSpacing / widthCount),
                -topSpacing
                    + (row + 1) * totalVerticalSpacing / heightCount
                    - row * verticalSpacing);
            imageRect.offsetMax = new Vector2(
                leftSpacing
                    + column * horizontalSpacing
                    - (column + 1) * totalHorizontalSpacing / widthCount,
                -topSpacing + row * (totalVerticalSpacing / heightCount - verticalSpacing));
        }

        imageRect.localScale = Vector3.one;

        var image = imageObject.GetComponent<Image>();
        image.sprite = defaultSprite;
        image.color = imageColor;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = raycastTarget;
        generatedImages.Add(image);

        if (raycastTarget)
        {
            var button = imageObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => ImageClicked?.Invoke(cellIndex, image));
            UIClickAudioPlayer.Register(button);
        }
    }

    private void ClearGeneratedImages()
    {
        generatedImages.Clear();
        if (gridRoot == null)
        {
            return;
        }

        for (var index = gridRoot.childCount - 1; index >= 0; index--)
        {
            var child = gridRoot.GetChild(index).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private void OnValidate()
    {
        widthCount = Mathf.Max(1, widthCount);
        heightCount = Mathf.Max(1, heightCount);
        cellSize = Mathf.Max(1f, cellSize);
        fitWidthReservedSpace = Mathf.Max(0f, fitWidthReservedSpace);

        if (!autoRefresh || !isActiveAndEnabled)
        {
            return;
        }

        if (Application.isPlaying)
        {
            runtimeRefreshRequested = true;
            return;
        }

#if UNITY_EDITOR
        QueueEditorRefresh();
#endif
    }

    private float ResolveCellSize()
    {
        if (!fitCellSizeToParentWidth
            || transform is not RectTransform parentRect
            || parentRect.rect.width <= 0f)
        {
            return Mathf.Max(1f, cellSize);
        }

        var availableWidth = parentRect.rect.width
            - leftSpacing
            - rightSpacing
            - fitWidthReservedSpace
            - horizontalSpacing * (widthCount - 1);
        return Mathf.Max(1f, availableWidth / widthCount);
    }

#if UNITY_EDITOR
    private void QueueEditorRefresh()
    {
        if (editorRefreshQueued)
        {
            return;
        }

        editorRefreshQueued = true;
        EditorApplication.delayCall += RefreshGridInEditor;
    }

    private void RefreshGridInEditor()
    {
        editorRefreshQueued = false;
        if (this == null || Application.isPlaying || !autoRefresh || !isActiveAndEnabled)
        {
            return;
        }

        GenerateGrid();
        EditorUtility.SetDirty(this);
    }
#endif
}
