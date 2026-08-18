using ImSharp;
using Luna;
using Luna.Generators;
using Penumbra.Api.Enums;
using Penumbra.GameData.Files;
using Penumbra.Interop.ResourceTree;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.ManagementTab;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly ResourceTreeFactory                                       _resourceTreeFactory;
    private readonly ResourceTreeViewer                                        _quickImportViewer;
    private readonly Dictionary<(Utf8GamePath, IWritable?), QuickImportAction> _quickImportActions = new();

    public HashSet<string> GetPlayerResourcesOfType(ResourceType type)
    {
        var resources = ResourceTreeApiHelper
            .GetResourcesOfType(_resourceTreeFactory.FromObjectTable(ResourceTreeFactory.Flags.LocalPlayerRelatedOnly), type)
            .Values
            .SelectMany(r => r.Values)
            .Select(r => r.Item1);

        return new HashSet<string>(resources, StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<FileRegistry> PopulateIsOnPlayer(IReadOnlyList<FileRegistry> files, ResourceType type)
    {
        var playerResources = GetPlayerResourcesOfType(type);
        foreach (var file in files)
            file.IsOnPlayer = playerResources.Contains(file.File.ToPath());

        return files;
    }

    private void DrawQuickImportTab(bool optionChanged)
    {
        using var tab = Im.TabBar.BeginItem("从画面导入"u8);
        if (!tab)
        {
            _quickImportActions.Clear();
            return;
        }

        using var id = Im.Id.Push(Mod!.Identifier);
        if (optionChanged)
            _quickImportActions.Clear();
        _quickImportViewer.Draw();
    }

    private void OnQuickImportRefresh()
    {
        _quickImportActions.Clear();
    }

    private void DrawQuickImportActions(ResourceNode resourceNode, IWritable? writable, Vector2 buttonSize)
    {
        Im.Line.Same();
        if (!_quickImportActions.TryGetValue((resourceNode.GamePath, writable), out var quickImport))
        {
            quickImport = QuickImportAction.Prepare(this, resourceNode.GamePath, writable);
            _quickImportActions.Add((resourceNode.GamePath, writable), quickImport);
        }

        var canQuickImport     = quickImport.CanExecute;
        var quickImportEnabled = canQuickImport && (!resourceNode.Protected || LunaStyle.Modifier.Destructive);
        if (ImEx.Icon.Button(LunaStyle.ImportIcon,
                canQuickImport
                    ? $"添加此文件副本到 {quickImport.OptionName}.{(!quickImportEnabled ? $"\n按住 {LunaStyle.Modifier.Destructive} 同时点击来添加" : string.Empty)}"
                    : $"无法添加此文件副本到 {quickImport.OptionName}:\n{quickImport.NonExecutableReason.Tooltip()}",
                !quickImportEnabled))
        {
            quickImport.Execute();
            _quickImportActions.Remove((resourceNode.GamePath, writable));
        }
    }

    public class QuickImportAction
    {
        public const string FallbackOptionName = "当前选项";

        private readonly string                         _optionName;
        private readonly Utf8GamePath                   _gamePath;
        private readonly ModEditor                      _editor;
        private readonly IWritable?                     _file;
        private readonly string?                        _targetPath;
        private readonly int                            _subDirs;
        private readonly QuickImportNonExecutableReason _nonExecutableReason;

        public string OptionName
            => _optionName;

        public Utf8GamePath GamePath
            => _gamePath;

        public bool CanExecute
            => !_gamePath.IsEmpty && _editor.Mod != null && _file != null && _targetPath != null;

        public QuickImportNonExecutableReason NonExecutableReason
            => _nonExecutableReason;

        /// <summary>
        /// Creates a non-executable QuickImportAction.
        /// </summary>
        private QuickImportAction(ModEditor editor, string optionName, Utf8GamePath gamePath,
            QuickImportNonExecutableReason nonExecutableReason)
        {
            if (nonExecutableReason is QuickImportNonExecutableReason.None)
                throw new ArgumentException($"The reason why this {nameof(QuickImportAction)} is non-executable must be specified.",
                    nameof(nonExecutableReason));

            _optionName          = optionName;
            _gamePath            = gamePath;
            _editor              = editor;
            _file                = null;
            _targetPath          = null;
            _subDirs             = 0;
            _nonExecutableReason = nonExecutableReason;
        }

        /// <summary>
        /// Creates an executable QuickImportAction.
        /// </summary>
        private QuickImportAction(string optionName, Utf8GamePath gamePath, ModEditor editor, IWritable file, string targetPath, int subDirs)
        {
            _optionName          = optionName;
            _gamePath            = gamePath;
            _editor              = editor;
            _file                = file;
            _targetPath          = targetPath;
            _subDirs             = subDirs;
            _nonExecutableReason = QuickImportNonExecutableReason.None;
        }

        public static QuickImportAction Prepare(ModEditWindow owner, Utf8GamePath gamePath, IWritable? file)
        {
            var editor     = owner._editor;
            var subMod     = editor.Option!;
            var optionName = subMod is IModOption o ? o.FullName : FallbackOptionName;

            if (gamePath.IsEmpty)
                return new QuickImportAction(editor, optionName, gamePath, QuickImportNonExecutableReason.NoGamePath);

            if (file is null)
                return new QuickImportAction(editor, optionName, gamePath, QuickImportNonExecutableReason.NoFile);

            if (editor.FileEditor.Changes)
                return new QuickImportAction(editor, optionName, gamePath, QuickImportNonExecutableReason.EditorDirty);

            if (ReservedFiles.Files.ContainsKey(unchecked((uint)gamePath.Path.Crc32)))
                return new QuickImportAction(editor, optionName, gamePath, QuickImportNonExecutableReason.GamePathReserved);

            if (subMod.Files.ContainsKey(gamePath) || subMod.FileSwaps.ContainsKey(gamePath))
                return new QuickImportAction(editor, optionName, gamePath, QuickImportNonExecutableReason.GamePathAlreadyInUse);

            var mod = owner.Mod;
            if (mod is null)
                return new QuickImportAction(editor, optionName, gamePath, QuickImportNonExecutableReason.NoTargetMod);

            var (preferredPath, subDirs) = GetPreferredPath(mod, subMod as IModOption, owner._config.Io.ReplaceNonAsciiOnImport);
            var targetPath = new FullPath(Path.Combine(preferredPath.FullName, gamePath.ToString())).FullName;
            if (File.Exists(targetPath))
                return new QuickImportAction(editor, optionName, gamePath, QuickImportNonExecutableReason.FileAlreadyExists);

            return new QuickImportAction(optionName, gamePath, editor, file, targetPath, subDirs);
        }

        public FileRegistry Execute()
        {
            if (!CanExecute)
                throw new InvalidOperationException();

            var directory = Path.GetDirectoryName(_targetPath);
            if (directory is not null)
                Directory.CreateDirectory(directory);
            _editor.Compactor.WriteAllBytes(_targetPath!, _file!.Write());
            _editor.FileEditor.Revert(_editor.Mod!, _editor.Option!);
            var fileRegistry =
                _editor.Files.Available.First(file => file.File.FullName.Equals(_targetPath, StringComparison.OrdinalIgnoreCase));
            _editor.FileEditor.AddPathsToSelected(_editor.Option!, [fileRegistry], _subDirs);
            _editor.FileEditor.Apply(_editor.Mod!, _editor.Option!);

            return fileRegistry;
        }

        private static (DirectoryInfo, int) GetPreferredPath(Mod mod, IModOption? subMod, bool replaceNonAscii)
        {
            var path    = mod.ModPath;
            var subDirs = 0;
            if (subMod is null)
                return (path, subDirs);

            var name     = subMod.Name;
            var fullName = subMod.FullName;
            if (fullName.EndsWith(": " + name))
            {
                path    = ModCreator.NewOptionDirectory(path, fullName[..^(name.Length + 2)], replaceNonAscii);
                path    = ModCreator.NewOptionDirectory(path, name,                           replaceNonAscii);
                subDirs = 2;
            }
            else
            {
                path    = ModCreator.NewOptionDirectory(path, fullName, replaceNonAscii);
                subDirs = 1;
            }

            return (path, subDirs);
        }
    }

    [TooltipEnum]
    public enum QuickImportNonExecutableReason
    {
        [Tooltip(Omit: true)]
        None = 0,

        [Tooltip("文件重定向标签页中有未保存的更改。")]
        EditorDirty,

        [Tooltip("没有可复制此文件的目标模组。\n\n这不应该发生。若你看到此消息，请向 Penumbra 支持求助。")]
        NoTargetMod,

        [Tooltip("没有可复制的源文件。")]
        NoFile,

        [Tooltip("导入文件要重定向到的游戏路径未知。")]
        NoGamePath,

        [Tooltip(
            "此文件因使用过于通用而被保留，无法直接更改。\n\n请手动编辑父文件，为此槽位引用另一条路径，再将此文件导入到新路径。")]
        GamePathReserved,

        [Tooltip("当前选项已在此游戏路径安装了文件。")]
        GamePathAlreadyInUse,

        [Tooltip(
            "此文件将被复制到的位置已存在文件。\n\n若这是你想重做的先前导入，请先移动或删除该文件。\n否则请手动导入此文件。")]
        FileAlreadyExists,
    }
}
