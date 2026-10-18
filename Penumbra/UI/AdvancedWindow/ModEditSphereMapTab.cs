using Dalamud.Interface.DragDrop;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using ImSharp;
using Luna;
using Penumbra.GameData.Gui;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

/// <summary> Advanced editor tab: mod-level sphere map slots, independent of options and file redirections. </summary>
public sealed unsafe class ModEditSphereMapTab(
    ModEditor editor,
    ModSphereMapManager manager,
    TextureArraySlicePickers pickers,
    FileDialogService fileDialog,
    IDragDropManager dragDropManager,
    Configuration config)
{
    private const string DragDropLabel = "SphereMapDragDrop";
    private const int    SlotCount     = 32;
    private const float  ImageSize     = 96f;

    public void Draw()
    {
        using var tab = Im.TabBar.BeginItem("球面贴图"u8);
        if (!tab || editor.Mod is not { } mod)
            return;

        using var id = Im.Id.Push(mod.Identifier);
        dragDropManager.CreateImGuiSource(DragDropLabel, m => m.Extensions.Any(IsPng), m =>
        {
            if (!GetPng(m.Files, out var file))
                return false;

            Im.Text($"拖放以导入球面贴图: {Path.GetFileName(file)}");
            return true;
        });

        Im.TextWrapped(
            $"槽位 {ModSphereMaps.FirstSlot}-{ModSphereMaps.LastSlot} 可放置本模组专用的球面贴图。点击槽位通过文件选择器或拖放 PNG 图片到槽位进行导入或替换。");
        var busy = manager.IsBusy(mod);
        if (busy)
            ImEx.TextFramed("处理中..."u8, Im.ContentRegion.Available with { Y = 0 }, Colors.PressEnterWarningBg);
        if (manager.GetError(mod) is { } error)
            Im.Text(error, Colors.RegexWarningBorder);
        LunaStyle.DrawSeparator();

        using var child = Im.Child.Begin("##slots"u8, Im.ContentRegion.Available);
        if (!child)
            return;

        var slots     = manager.GetSlots(mod);
        var modArray  = slots is not null ? manager.GetPreview(mod) : null;
        var vanilla   = manager.VanillaHandle;
        var size      = new Vector2(ImageSize * Im.Style.GlobalScale);
        var spacing   = Im.Style.ItemSpacing.X;
        var columns   = Math.Max(1, (int)((Im.ContentRegion.Available.X + spacing) / (size.X + spacing)));
        for (var slot = 0; slot < SlotCount; ++slot)
        {
            if (slot % columns is not 0)
                Im.Line.Same();

            var declared = slots?.ContainsKey(slot) ?? false;
            DrawSlot(mod, slot, size, declared ? modArray : vanilla, declared, busy);
        }
    }

    private void DrawSlot(Mod mod, int slot, Vector2 size, TextureResourceHandle* texture, bool declared, bool busy)
    {
        using var id       = Im.Id.Push(slot);
        using var group    = Im.Group();
        var       editable = ModSphereMaps.IsEditableSlot(slot);
        var       position = Im.Cursor.ScreenPosition;
        if (Im.InvisibleButton("##image"u8, size) && editable && !busy)
            OpenFilePicker(mod, slot);

        if (texture is not null)
            pickers.DrawTextureSlices(position, size, 0, [texture], (byte)slot);
        else if (declared)
            Im.Window.DrawList.Text(position, ImGuiColor.Text.Get(), "加载中..."u8);

        if (editable && !busy && dragDropManager.CreateImGuiTarget(DragDropLabel, out var files, out _) && GetPng(files, out var file))
            manager.Import(mod, slot, file);

        Im.Tooltip.OnHover(editable
            ? declared ? "点击或拖放 PNG 以替换此槽位的球面贴图。"u8 : "点击或拖放 PNG 以导入此槽位的球面贴图。"u8
            : "原版槽位，只读。"u8);

        var label = editable ? declared ? "自定义"u8 : "空"u8 : "原版（只读）"u8;
        Im.Text($"{slot,2} {label}");
        if (!declared)
            return;

        Im.Line.Same();
        var modifierActive = LunaStyle.Modifier.Destructive.Active;
        if (ImEx.Icon.Button(LunaStyle.DeleteIcon,
                $"删除此槽位的球面贴图，恢复为原版。\n按住 {LunaStyle.Modifier.Destructive} 以删除。", !modifierActive || busy))
            manager.Remove(mod, slot);
    }

    private void OpenFilePicker(Mod mod, int slot)
    {
        var startPath = config.Io.DefaultModImportPath.Length > 0 ? config.Io.DefaultModImportPath : mod.ModPath.FullName;
        fileDialog.OpenFilePicker("导入球面贴图...", "PNG{.png}", (success, paths) =>
        {
            if (success && paths.Count > 0)
                manager.Import(mod, slot, paths[0]);
        }, 1, startPath, false);
    }

    private static bool IsPng(string extension)
        => extension.Equals(".png", StringComparison.OrdinalIgnoreCase);

    private static bool GetPng(IEnumerable<string> files, [NotNullWhen(true)] out string? file)
    {
        file = files.FirstOrDefault(f => IsPng(Path.GetExtension(f)));
        return file is not null;
    }
}
