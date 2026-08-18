using ImSharp;
using ImSharp.Table;
using Luna;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class UnusedModsTab(
    ModConfigUpdater modConfigUpdater,
    ModManager manager,
    ModExportManager exports,
    UiNavigator navigator) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "未使用模组"u8;

    public ManagementTabType Identifier
        => ManagementTabType.UnusedMods;

    private readonly Table _table       = new(modConfigUpdater, manager, exports, navigator);
    private          int   _defaultDays = 30;

    public void PostTabButton()
    {
        if (!Im.Item.Hovered())
            return;

        using var tt = Im.Tooltip.Begin();
        ImEx.TextMultiColored("此处显示了当前未启用，或在"u8)
            .Then("任意"u8, ColorId.NewMod.Value).Then("合集中存在临时设置的模组。"u8).End();
        Im.Text(
            "其他调用 Penumbra API 的插件可以将模组标记为“使用中”以隐藏它们，或者在保持显示的同时为其添加自定义备注。"u8);
    }

    public void DrawContent()
    {
        using var child = Im.Child.Begin("c"u8, Im.ContentRegion.Available);
        if (!child)
            return;

        if (Im.Checkbox("隐藏所有具有备注的模组"u8, _table.HideNodes))
            _table.HideNodes ^= true;
        Im.Line.Same();
        if (Im.Button("显示所有未使用的模组"u8))
            _table.UnusedCap = TimeSpan.Zero;

        Im.Line.Same();
        if (Im.Button("显示未启用的模组，且未配置天数超过"u8))
            _table.UnusedCap = TimeSpan.FromDays(_defaultDays);
        Im.Line.SameInner();
        Im.Item.SetNextWidthScaled(40);
        Im.Drag("天"u8, ref _defaultDays, 0, null, 0.1f, SliderFlags.AlwaysClamp);

        _table.Draw();
    }

    private sealed class Table(
        ModConfigUpdater modConfigUpdater,
        ModManager manager,
        ModExportManager exports,
        UiNavigator navigator) : TableBase<CacheItem, Table.Cache>(new StringU8("unused"u8),
        new ButtonColumn(manager, exports),
        new NameColumn(navigator), new LastEditColumn(), new ModSizeColumn(), new PathColumn(), new NotesColumn())
    {
        public bool HideNodes
        {
            get;
            set
            {
                if (field == value)
                    return;

                field        = value;
                _filterDirty = true;
            }
        }

        public TimeSpan UnusedCap
        {
            get;
            set
            {
                if (field == value)
                    return;

                field      = value;
                _spanDirty = true;
            }
        } = TimeSpan.MaxValue;


        private bool _filterDirty;
        private bool _spanDirty;

        public override IEnumerable<CacheItem> GetItems()
        {
            var now = DateTime.UtcNow;
            return modConfigUpdater.ListUnusedMods(UnusedCap).Select(m => new CacheItem(m.Item1, m.Item2, now));
        }

        public override Vector2 GetSize()
        {
            var size = Im.ContentRegion.Available;
            size.Y -= Im.Style.TextHeightWithSpacing;
            return size;
        }

        protected override void PreDraw(in Cache cache)
        {
            var disabled = !LunaStyle.Modifier.Destructive.Active;
            Im.Line.Same();
            if (ImEx.Button("更新视图"u8,
                    "列表不会自动刷新。点击此处可以更新当前显示的模组列表，且不会更改时间限制设置。"u8))
                cache.Dirty |= IManagedCache.DirtyFlags.Custom;

            Im.Line.Same();
            if (ImEx.Button("删除所有可见模组"u8, default, "删除当前列表显示的所有模组。此操作不可逆，请谨慎操作！"u8,
                    disabled))
                foreach (var (mod, globalIndex) in cache.GetItemsWithIndices().ToList())
                {
                    manager.DeleteMod(mod.Mod);
                    cache.DeleteSingleItem(globalIndex);
                }

            if (disabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\n按住 {LunaStyle.Modifier.Destructive} 键以进行删除。");
        }

        protected override void PostDraw(in Cache cache)
        {
            base.PostDraw(in cache);
            var buttons = (ButtonColumn)Columns[0];
            while (buttons.DeleteList.TryDequeue(out var index))
                cache.DeleteSingleItem(index);

            if (cache.Loading)
                return;

            Im.Text($"{cache.Count}/{cache.AllItems.Count} 个可见模组，共 {manager.Count} 个模组。");
            if (buttons.Exporting is { } mod)
            {
                Im.Line.Same();
                Im.Text($"正在导出并删除模组：{mod.Name}");
                Im.Line.Same();
                ImEx.Spinner("s"u8, Im.Style.TextHeight / 3, 2, Im.Color.Get(ImGuiColor.Text));
            }
        }

        protected override Cache CreateCache()
            => new(this);

        public sealed class Cache : TableCache<CacheItem>
        {
            private readonly Table _parent;

            public Cache(Table parent)
                : base(parent)
            {
                KeepAliveDuration = TimeSpan.FromMinutes(5);
                _parent           = parent;
            }

            protected override bool WouldBeVisible(in CacheItem value, int globalIndex)
                => (!_parent.HideNodes || value.Notes.Length == 0) && base.WouldBeVisible(value, globalIndex);

            private CancellationTokenSource? _cancel;

            public override void Update()
            {
                if (_parent._filterDirty)
                {
                    FilterDirty          = true;
                    _parent._filterDirty = false;
                }

                if (_parent._spanDirty)
                {
                    Dirty              |= IManagedCache.DirtyFlags.Custom;
                    _parent._spanDirty =  false;
                }

                base.Update();
            }

            protected override void UpdateSort()
            {
                if (Loading)
                    return;

                base.UpdateSort();
            }

            protected override void UpdateFilter()
            {
                if (Loading)
                    return;

                base.UpdateFilter();
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                _cancel?.Cancel();
            }

            protected override void OnDataUpdate()
            {
                _cancel?.Cancel();
                base.OnDataUpdate();

                if (UnfilteredItems.Count < 50)
                    return;

                Loading = true;
                _cancel = new CancellationTokenSource();
                var token = _cancel.Token;
                Task.Run(() =>
                {
                    foreach (var mod in UnfilteredItems)
                    {
                        if (token.IsCancellationRequested)
                        {
                            Loading = false;
                            return;
                        }

                        _ = mod.ModSizeString;
                    }

                    Loading = false;
                }, token);
            }
        }
    }

    private sealed class ButtonColumn : BasicColumn<CacheItem>
    {
        public readonly ConcurrentQueue<int> DeleteList = [];

        private readonly ModManager       _manager;
        private readonly ModExportManager _exports;

        public Mod? Exporting { get; private set; }

        public ButtonColumn(ModManager manager, ModExportManager exports)
        {
            _manager =  manager;
            _exports =  exports;
            Label    =  StringU8.Empty;
            Flags    |= TableColumnFlags.NoSort;
        }

        public override void DrawColumn(in CacheItem item, int globalIndex)
        {
            var inactive      = !LunaStyle.Modifier.Destructive.Active;
            var exportingThis = Exporting == item.Mod;
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "从 Penumbra 和本地驱动器中删除此模组。此操作不可逆，请谨慎操作！"u8,
                    inactive || exportingThis))
            {
                _manager.DeleteMod(item.Mod);
                DeleteList.Enqueue(globalIndex);
            }

            if (inactive)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\n按住 {LunaStyle.Modifier.Destructive} 删除。");
            if (exportingThis)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "\n正在导出并删除此模组，请稍候。"u8);

            Im.Line.SameInner();

            var exporting = Exporting is not null;
            if (ImEx.Icon.Button(LunaStyle.BackupDeleteIcon,
                    "将此模组导出到导出目录，压缩后删除。"u8, inactive || exporting))
            {
                Exporting = item.Mod;
                _exports.CreateAsync(Exporting).ContinueWith(_ =>
                {
                    _manager.DeleteMod(Exporting);
                    DeleteList.Enqueue(globalIndex);
                    Exporting = null;
                });
            }

            if (inactive)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\n按住 {LunaStyle.Modifier.Destructive} 删除。");
            if (exporting)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "正在导出并删除模组，请稍候。"u8);

            Im.Line.SameInner();
            if (ImEx.Icon.Button(LunaStyle.FolderIcon, "在文件资源管理器中打开此模组的目录。"u8))
                Process.Start(new ProcessStartInfo(item.Mod.ModPath.FullName) { UseShellExecute = true });
        }

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => Im.Style.FrameHeight * 3 + 2 * Im.Style.ItemInnerSpacing.X;
    }

    private sealed class NameColumn : TextColumn<CacheItem>
    {
        private readonly UiNavigator _navigator;

        public NameColumn(UiNavigator navigator)
        {
            _navigator =  navigator;
            Label      =  new StringU8("模组名称"u8);
            Flags      |= TableColumnFlags.WidthStretch;
        }

        protected override string ComparisonText(in CacheItem item, int globalIndex)
            => item.Mod.Name;

        protected override StringU8 DisplayText(in CacheItem item, int globalIndex)
            => item.ModName;

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => 0.3f;

        public override void DrawColumn(in CacheItem item, int globalIndex)
        {
            var content = Im.ContentRegion.Available.X;
            Im.Cursor.FrameAlign();
            var clicked = Im.Selectable(item.ModName);
            Im.Tooltip.OnHover(item.DirectoryName);
            if (Im.Font.CalculateSize(item.ModName).X >= content)
                Im.Tooltip.OnHover(item.ModName);
            Im.Tooltip.OnHover("\n点击以移动到模组。"u8);
            if (clicked)
                _navigator.MoveTo(item.Mod);
        }
    }

    private sealed class PathColumn : TextColumn<CacheItem>
    {
        public PathColumn()
        {
            Label =  new StringU8("模组路径"u8);
            Flags |= TableColumnFlags.WidthStretch;
        }

        protected override string ComparisonText(in CacheItem item, int globalIndex)
            => item.Mod.Path.CurrentPath;

        protected override StringU8 DisplayText(in CacheItem item, int globalIndex)
            => item.ModPath;

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => 0.7f;

        public override void DrawColumn(in CacheItem item, int globalIndex)
        {
            var content = Im.ContentRegion.Available.X;
            Im.Cursor.FrameAlign();
            base.DrawColumn(in item, globalIndex);
            if (Im.Item.Size.X >= content)
                Im.Tooltip.OnHover(item.ModPath);
        }
    }


    private sealed class LastEditColumn : NumberColumn<long, CacheItem>
    {
        public LastEditColumn()
        {
            Label = new StringU8("最后配置编辑"u8);
        }

        public override long ToValue(in CacheItem item, int globalIndex)
            => item.Mod.LastConfigEdit;

        protected override StringU8 DisplayNumber(in CacheItem item, int globalIndex)
            => item.Duration;

        protected override string ComparisonText(in CacheItem item, int globalIndex)
            => item.Duration.Utf16;

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => Im.Font.CalculateSize("最后配置编辑"u8).X + ImEx.Table.ArrowWidth + Im.Style.CellPadding.X * 2;

        public override void DrawColumn(in CacheItem item, int globalIndex)
        {
            Im.Cursor.FrameAlign();
            base.DrawColumn(in item, globalIndex);
            Im.Tooltip.OnHover($"点击复制时间戳：{item.Mod.LastConfigEdit}");
            if (Im.Item.Clicked())
                Im.Clipboard.Set($"{item.Mod.LastConfigEdit}");
        }
    }

    private sealed class ModSizeColumn : NumberColumn<long, CacheItem>
    {
        public ModSizeColumn()
        {
            Label = new StringU8("磁盘大小"u8);
        }

        public override long ToValue(in CacheItem item, int globalIndex)
            => item.ModSize;

        protected override StringU8 DisplayNumber(in CacheItem item, int globalIndex)
            => item.ModSizeString;

        protected override string ComparisonText(in CacheItem item, int globalIndex)
            => item.ModSizeString.Utf16;

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => Im.Font.CalculateSize("磁盘大小"u8).X + ImEx.Table.ArrowWidth + Im.Style.CellPadding.X * 2;

        public override void DrawColumn(in CacheItem item, int globalIndex)
        {
            Im.Cursor.FrameAlign();
            base.DrawColumn(in item, globalIndex);
        }
    }

    private sealed class NotesColumn : TextColumn<CacheItem>
    {
        private static readonly StringU8 Notes = new("备注"u8);

        public NotesColumn()
        {
            Label = new StringU8("备注"u8);
        }

        public override bool WouldBeVisible(in CacheItem item, int globalIndex)
        {
            if (item.Notes.Length is 0)
                return Filter.WouldBeVisible(string.Empty);
            if (Filter.WouldBeVisible("备注"))
                return true;

            return item.Notes.Any(n => Filter.WouldBeVisible(n.Item1.Utf16) || Filter.WouldBeVisible(n.Item2.Utf16));
        }

        public override int Compare(in CacheItem lhs, int lhsGlobalIndex, in CacheItem rhs, int rhsGlobalIndex)
        {
            var first = lhs.Notes.Length.CompareTo(rhs.Notes.Length);
            if (first is not 0)
                return first;

            foreach (var (lhsNote, rhsNote) in lhs.Notes.Zip(rhs.Notes))
            {
                var second = lhsNote.Item1.Utf16.CompareTo(rhsNote.Item1.Utf16, StringComparison.Ordinal);
                if (second is not 0)
                    return second;
            }

            foreach (var (lhsNote, rhsNote) in lhs.Notes.Zip(rhs.Notes))
            {
                var third = lhsNote.Item2.Utf16.Length.CompareTo(rhsNote.Item2.Utf16.Length);
                if (third is not 0)
                    return third;
            }

            return 0;
        }

        protected override string ComparisonText(in CacheItem item, int globalIndex)
            => item.Notes.Length is 0 ? string.Empty : "备注";

        protected override StringU8 DisplayText(in CacheItem item, int globalIndex)
            => item.Notes.Length is 0 ? StringU8.Empty : Notes;

        public override void DrawColumn(in CacheItem item, int globalIndex)
            => DrawNotes(item.Notes);

        public override float ComputeWidth(IEnumerable<CacheItem> _)
            => Im.Style.FrameHeightWithSpacing + Im.Font.CalculateSize(Notes).X;
    }

    private record CacheItem(
        Mod Mod,
        StringU8 ModName,
        StringU8 ModPath,
        StringU8 DirectoryName,
        long ModSize,
        StringPair ModSizeString,
        StringPair Duration,
        (StringPair, StringPair)[] Notes)
    {
        public long ModSize
        {
            get => field < 0 ? field = WindowsFunctions.GetDirectorySize(Mod.ModPath.FullName) : field;
        } = ModSize;

        private StringPair _modSizeString = ModSizeString;

        public StringPair ModSizeString
        {
            get => _modSizeString.IsEmpty
                ? _modSizeString = new StringPair(FormattingFunctions.HumanReadableSize(ModSize))
                : _modSizeString;
        }

        public CacheItem(Mod mod, (string, string)[] notes, DateTime now)
            : this(mod, new StringU8(mod.Name), new StringU8(mod.Path.CurrentPath), new StringU8($"目录名称：{mod.Identifier}"),
                -1, StringPair.Empty, new StringPair(FormattingFunctions.DurationString(mod.LastConfigEdit, now)),
                notes.Select(n => (new StringPair(n.Item1), new StringPair(n.Item2))).ToArray())
        { }
    }

    public static void DrawNotes((StringPair, StringPair)[] notes)
    {
        if (notes.Length is 0)
            return;

        ImEx.Icon.DrawAligned(LunaStyle.InfoIcon, LunaStyle.FavoriteColor);
        var hovered = Im.Item.Hovered();
        Im.Line.SameInner();
        Im.Text("备注"u8);
        if (!hovered && !Im.Item.Hovered())
            return;

        using var tt = Im.Tooltip.Begin();
        DrawNote(notes[0]);
        foreach (var note in notes.Skip(1))
        {
            Im.Separator();
            DrawNote(note);
        }

        return;

        static void DrawNote((StringPair, StringPair) note)
        {
            using (Im.Group())
            {
                Im.Text(note.Item1.Utf8);
                Im.Line.Same();
                using (Im.Group())
                {
                    Im.Text(note.Item2.Utf8);
                }
            }
        }
    }
}
