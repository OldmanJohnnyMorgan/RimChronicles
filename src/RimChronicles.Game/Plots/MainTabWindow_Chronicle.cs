using RimChronicles.Core.Plots;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>The Chronicle tab: the colony's stories, each with the letters it sent and the colony's answers, oldest
/// first. A plot shows only once the player has had a letter from it, so a story that starts in secret (the Silent
/// Press) gives nothing away. UI code, so Unity stub types are allowed here.</summary>
public sealed class MainTabWindow_Chronicle : MainTabWindow
{
    private const float ListWidth = 250f;
    private const float RowHeight = 46f;
    private const float Gap = 14f;

    private string _selected = "";
    private Vector2 _listScroll;
    private Vector2 _storyScroll;

    public override Vector2 RequestedTabSize => new(960f, 680f);

    public override void DoWindowContents(Rect inRect)
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "The colony's chronicle");
        Text.Font = GameFont.Small;
        var body = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);

        // Running stories first, oldest first; then finished ones, the latest ending first.
        var book = component.Plots;
        var plots = book.Active.OrderBy(p => p.StartDay)
            .Concat(book.Finished.AsEnumerable().Reverse())
            .Where(p => p.Story.Count > 0)
            .ToList();
        if (plots.Count == 0)
        {
            Widgets.Label(body, "No stories yet. When something in the colony's past comes back to it, the letters it sends are kept here.");
            return;
        }

        var selected = plots.FirstOrDefault(p => p.Id == _selected) ?? plots[0];
        _selected = selected.Id;

        DrawList(new Rect(body.x, body.y, ListWidth, body.height), plots, selected);
        DrawStory(new Rect(body.x + ListWidth + Gap, body.y, body.width - ListWidth - Gap, body.height), selected);
    }

    private void DrawList(Rect rect, List<Plot> plots, Plot selected)
    {
        var view = new Rect(0f, 0f, rect.width - 16f, plots.Count * RowHeight);
        Widgets.BeginScrollView(rect, ref _listScroll, view);
        for (var i = 0; i < plots.Count; i++)
        {
            var plot = plots[i];
            var row = new Rect(0f, i * RowHeight, view.width, RowHeight - 4f);
            if (plot == selected)
            {
                Widgets.DrawHighlightSelected(row);
            }
            else if (Mouse.IsOver(row))
            {
                Widgets.DrawHighlight(row);
            }

            var inner = row.ContractedBy(4f);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), PlotChronicle.Title(plot).Truncate(inner.width));
            GUI.color = plot.Ended ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.75f, 0.85f, 0.75f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, inner.y + 20f, inner.width, 18f), PlotChronicle.Status(plot).Truncate(inner.width));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(row))
            {
                _selected = plot.Id;
                _storyScroll = Vector2.zero;
            }
        }

        Widgets.EndScrollView();
    }

    private void DrawStory(Rect rect, Plot plot)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), PlotChronicle.Title(plot));
        Text.Font = GameFont.Small;
        var who = plot.Protagonist.Name.Length > 0 && plot.Protagonist.Kind != ProtagonistKind.Colony
            ? $"   ·   Carried by {plot.Protagonist.Name}"
            : "";
        GUI.color = new Color(0.75f, 0.75f, 0.75f);
        Widgets.Label(new Rect(rect.x, rect.y + 32f, rect.width, 24f), PlotChronicle.Status(plot) + who);
        GUI.color = Color.white;

        var outRect = new Rect(rect.x, rect.y + 62f, rect.width, rect.height - 62f);
        var width = outRect.width - 20f;
        var heights = plot.Story.Select(e => Text.CalcHeight(e.Text, e.Answer ? width - 16f : width)).ToList();
        var total = plot.Story.Select((e, i) => heights[i] + (e.Answer ? 0f : 24f) + Gap).Sum();
        var view = new Rect(0f, 0f, width, total);

        Widgets.BeginScrollView(outRect, ref _storyScroll, view);
        var y = 0f;
        for (var i = 0; i < plot.Story.Count; i++)
        {
            var entry = plot.Story[i];
            if (entry.Answer)
            {
                // The colony's answer: indented and tinted, under the letter it answers.
                GUI.color = new Color(0.85f, 0.8f, 0.55f);
                Widgets.Label(new Rect(16f, y, width - 16f, heights[i]), entry.Text);
                GUI.color = Color.white;
                y += heights[i] + Gap;
                continue;
            }

            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            Widgets.Label(new Rect(0f, y, width, 24f), $"Day {entry.Day}   ·   {entry.Label}");
            GUI.color = Color.white;
            y += 24f;
            Widgets.Label(new Rect(0f, y, width, heights[i]), entry.Text);
            y += heights[i] + Gap;
            Widgets.DrawLineHorizontal(0f, y - Gap / 2f, width);
        }

        Widgets.EndScrollView();
    }
}
