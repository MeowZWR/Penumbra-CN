using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Interop.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public sealed class RedrawFooter(
    Configuration config,
    TutorialService tutorial,
    ObjectManager objects,
    ITargetManager targets,
    RedrawService redrawService) : IFooter
{
    public bool Collapsed
        => config.HideRedrawBar;

    public void PostCollapsed()
        => tutorial.SkipTutorial(BasicTutorialSteps.Redrawing);

    private void DrawTooltip()
    {
        var hovered = Im.Item.Hovered();
        tutorial.OpenTutorial(BasicTutorialSteps.Redrawing);
        if (!hovered)
            return;

        using var style = Im.Style.PushDefault();
        using var _     = Im.Tooltip.Begin();
        Im.Text("'/penumbra redraw' 支持的修饰符："u8);
        Im.BulletText("无, 重绘所有角色\n"u8);
        Im.BulletText("'self' 或 '<me>': 自己的角色\n"u8);
        Im.BulletText("'target' 或 '<t>': 目标\n"u8);
        Im.BulletText("'focus' 或 '<f>: 焦点目标\n"u8);
        Im.BulletText("'mouseover' 或 '<mo>': 当前悬停的角色\n"u8);
        Im.BulletText("'furniture': 大多数室内家具, 目前无法用于室外\n"u8);
        Im.BulletText("任何特定角色名称, 重绘所有匹配该名称的角色."u8);
    }

    private static void DrawInfo(Vector2 height)
    {
        var       frameColor = Im.Style[ImGuiColor.FrameBackground];
        using var group      = Im.Group();
        using (AwesomeIcon.Font.Push())
        {
            ImEx.TextFramed(LunaStyle.HelpMarker.Span, height, frameColor);
        }

        Im.Line.NoSpacing();
        ImEx.TextFramed("重绘：       "u8, height, frameColor);
    }

    public void Draw(Vector2 size)
    {
        using var style = Im.Style.PushDefault(ImStyleDouble.WindowPadding);
        DrawInfo(size with { X = 0 });
        DrawTooltip();

        using var id       = Im.Id.Push("Redraw"u8);
        using var disabled = Im.Disabled(!objects[0].Valid);
        Im.Line.NoSpacing();
        var buttonWidth = size with { X = Im.ContentRegion.Available.X / 5 };
        var tt = !objects[0].Valid
            ? "只能在登录且角色可用时使用。"u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "全部"u8, string.Empty, tt);
        Im.Line.NoSpacing();
        DrawButton(buttonWidth, "自己"u8, "self", tt);
        Im.Line.NoSpacing();

        tt = targets.Target is null && targets.GPoseTarget is null
            ? "只能在有目标时使用。"u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "目标"u8, "target", tt);
        Im.Line.NoSpacing();

        tt = targets.FocusTarget is null
            ? "只能在有焦点目标时使用。"u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "焦点"u8, "focus", tt);
        Im.Line.NoSpacing();

        tt = !IsIndoors()
            ? "目前只能用于室内家具。"u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "家具"u8, "furniture", tt);
    }

    private void DrawButton(Vector2 width, ReadOnlySpan<byte> label, string lower, ReadOnlySpan<byte> additionalTooltip)
    {
        using (Im.Disabled(additionalTooltip.Length > 0))
        {
            if (Im.Button(label, width))
            {
                if (lower.Length > 0)
                    redrawService.RedrawObject(lower, RedrawType.Redraw);
                else
                    redrawService.RedrawAll(RedrawType.Redraw);
            }
        }

        if (!Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
            return;

        using var style = Im.Style.PushDefault();
        using var _     = Im.Tooltip.Begin();
        if (lower.Length > 0)
            Im.Text($"执行 '/penumbra redraw {lower}'.");
        else
            Im.Text("执行 '/penumbra redraw'."u8);
        if (additionalTooltip.Length > 0)
            Im.Text(additionalTooltip);
    }

    private static unsafe bool IsIndoors()
        => HousingManager.Instance()->IsInside();
}
