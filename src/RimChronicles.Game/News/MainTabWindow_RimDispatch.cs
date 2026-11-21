using RimChronicles.Core.News;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimChronicles.Game.News;

/// <summary>The Dispatch tab: every delivered bulletin, newest first. UI code, so Unity stub types are allowed here.</summary>
public sealed class MainTabWindow_RimDispatch : MainTabWindow
{
    private const float Gap = 14f;
    private Vector2 _scroll;

    public override Vector2 RequestedTabSize => new(720f, 640f);

    public override void DoWindowContents(Rect inRect)
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var desk = component.Desk;
        var reporter = desk.Reporter;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), reporter.Outlet.CapitalizeFirst());
        Text.Font = GameFont.Small;
        var byline = reporter.Faction.Length > 0 ? $"Correspondent: {reporter.Name} of {reporter.Faction}" : $"Correspondent: {reporter.Name}";
        // No hint when the news has stopped: the player has to work out why (Silent Press).
        var next = desk.NextDueDay >= 0 && reporter.Status == ReporterStatus.Active ? $"   ·   Next bulletin: day {desk.NextDueDay}" : "";
        Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 24f), byline + next);

        var outRect = new Rect(inRect.x, inRect.y + 68f, inRect.width, inRect.height - 68f);
        var bulletins = desk.Delivered.AsEnumerable().Reverse().ToList();
        if (bulletins.Count == 0)
        {
            Widgets.Label(outRect, "No bulletins yet.");
            return;
        }

        var width = outRect.width - 20f;
        var heights = bulletins.Select(b => Text.CalcHeight(b.Text, width)).ToList();
        var viewRect = new Rect(0f, 0f, width, heights.Sum() + bulletins.Count * (24f + Gap));

        Widgets.BeginScrollView(outRect, ref _scroll, viewRect);
        var y = 0f;
        for (var i = 0; i < bulletins.Count; i++)
        {
            DrawBulletin(bulletins[i], heights[i], width, ref y);
        }

        Widgets.EndScrollView();
    }

    private static void DrawBulletin(Bulletin bulletin, float height, float width, ref float y)
    {
        GUI.color = new Color(0.75f, 0.75f, 0.75f);
        Widgets.Label(new Rect(0f, y, width, 24f), $"Day {bulletin.DeliveredDay}");
        GUI.color = Color.white;
        y += 24f;
        Widgets.Label(new Rect(0f, y, width, height), bulletin.Text);
        y += height + Gap;
        Widgets.DrawLineHorizontal(0f, y - Gap / 2f, width);
    }
}
