using UnityEngine;

/// <summary>Shared stone-and-gold design tokens for every tower information card.</summary>
public static class DefenseTowerInfoStyle
{
    public static readonly Vector2 CardSize = new(1100, 1040);
    public static readonly Color Gold = new(.9f, .75f, .39f);
    public static readonly Color Body = new(.95f, .89f, .73f);
    public static readonly Color Muted = new(.7f, .75f, .72f);
    public static readonly Color Available = new(.62f, .88f, .52f);
    public static readonly Color Unavailable = new(1f, .57f, .48f);
    public const float CanvasScale = .0013f;
    public const float OpenDuration = .18f;
    public const float CloseDuration = .12f;
    public const float ChangeDuration = .24f;
    public const float RefreshInterval = .15f;
    public const float ContentWidth = 940f;
}
