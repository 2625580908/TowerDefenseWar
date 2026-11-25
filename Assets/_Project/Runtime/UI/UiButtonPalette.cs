using UnityEngine;

namespace MRStrategyRTS.UI
{
    /// <summary>Shared immediate tint states for mouse and XR buttons.</summary>
    public static class UiButtonPalette
    {
        public static readonly Color Normal = Color.white;
        public static readonly Color Hover = new(1f, .97f, .82f, 1f);
        public static readonly Color Pressed = new(.68f, .66f, .58f, 1f);
        public static readonly Color Disabled = new(.58f, .58f, .58f, .65f);
    }
}
