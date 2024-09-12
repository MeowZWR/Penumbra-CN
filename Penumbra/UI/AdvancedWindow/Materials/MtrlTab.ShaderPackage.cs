using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.ShaderStructs;
using Penumbra.String.Classes;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    // strings path/to/the.exe | grep --fixed-strings '.shpk' | sort -u | sed -e 's#^shader/sm5/shpk/##'
    // Apricot shader packages are unlisted because
    // 1. they cause severe performance/memory issues when calculating the effective shader set
    // 2. they probably aren't intended for use with materials anyway
    private static readonly IReadOnlyList<string> StandardShaderPackages =
    [
        "3dui.shpk",
        // "apricot_decal_dummy.shpk",
        // "apricot_decal_ring.shpk",
        // "apricot_decal.shpk",
        // "apricot_fogModel.shpk",
        // "apricot_gbuffer_decal_dummy.shpk",
        // "apricot_gbuffer_decal_ring.shpk",
        // "apricot_gbuffer_decal.shpk",
        // "apricot_lightmodel.shpk",
        // "apricot_model_dummy.shpk",
        // "apricot_model_morph.shpk",
        // "apricot_model.shpk",
        // "apricot_powder_dummy.shpk",
        // "apricot_powder.shpk",
        // "apricot_shape_dummy.shpk",
        // "apricot_shape.shpk",
        "bgcolorchange.shpk",
        "bg_composite.shpk",
        "bgcrestchange.shpk",
        "bgdecal.shpk",
        "bgprop.shpk",
        "bg.shpk",
        "bguvscroll.shpk",
        "characterglass.shpk",
        "characterinc.shpk",
        "characterlegacy.shpk",
        "characterocclusion.shpk",
        "characterreflection.shpk",
        "characterscroll.shpk",
        "charactershadowoffset.shpk",
        "character.shpk",
        "characterstockings.shpk",
        "charactertattoo.shpk",
        "charactertransparency.shpk",
        "cloud.shpk",
        "createviewposition.shpk",
        "crystal.shpk",
        "directionallighting.shpk",
        "directionalshadow.shpk",
        "furblur.shpk",
        "grassdynamicwave.shpk",
        "grass.shpk",
        "hairmask.shpk",
        "hair.shpk",
        "iris.shpk",
        "lightshaft.shpk",
        "linelighting.shpk",
        "planelighting.shpk",
        "pointlighting.shpk",
        "river.shpk",
        "shadowmask.shpk",
        "skin.shpk",
        "spotlighting.shpk",
        "subsurfaceblur.shpk",
        "verticalfog.shpk",
        "water.shpk",
        "weather.shpk",
    ];

    private static readonly byte[] UnknownShadersString = "Vertex Shaders: ???\nPixel Shaders: ???"u8.ToArray();

    private string[]? _shpkNames;

    private string    _shaderHeader             = "Shader###Shader";
    private FullPath  _loadedShpkPath           = FullPath.Empty;
    private string    _loadedShpkPathName       = string.Empty;
    private string    _loadedShpkDevkitPathName = string.Empty;
    private string    _shaderComment            = string.Empty;
    private ShpkFile? _associatedShpk;
    private bool      _shpkLoading;
    private JObject?  _associatedShpkDevkit;

    private readonly string   _loadedBaseDevkitPathName;
    private readonly JObject? _associatedBaseDevkit;

    // Shader Key State
    private readonly
        List<(string Label, int Index, string Description, bool MonoFont, IReadOnlyList<(string Label, uint Value, string Description)>
            Values)> _shaderKeys = new(16);

    private readonly HashSet<int>         _vertexShaders = new(16);
    private readonly HashSet<int>         _pixelShaders  = new(16);
    private          bool                 _shadersKnown;
    private          ReadOnlyMemory<byte> _shadersString = UnknownShadersString;

    private string[] GetShpkNames()
    {
        if (null != _shpkNames)
            return _shpkNames;

        var names = new HashSet<string>(StandardShaderPackages);
        names.UnionWith(_edit.FindPathsStartingWith(ShpkPrefix).Select(path => path.ToString()[ShpkPrefixLength..]));

        _shpkNames = names.ToArray();
        Array.Sort(_shpkNames);

        return _shpkNames;
    }

    private FullPath FindAssociatedShpk(out string defaultPath, out Utf8GamePath defaultGamePath)
    {
        defaultPath = GamePaths.Shader.ShpkPath(Mtrl.ShaderPackage.Name);
        if (!Utf8GamePath.FromString(defaultPath, out defaultGamePath))
            return FullPath.Empty;

        return _edit.FindBestMatch(defaultGamePath);
    }

    private void LoadShpk(FullPath path)
        => Task.Run(() => DoLoadShpk(path));

    private async Task DoLoadShpk(FullPath path)
    {
        _shadersKnown = false;
        _shaderHeader = $"Shader ({Mtrl.ShaderPackage.Name})###Shader";
        _shpkLoading  = true;

        try
        {
            var data = path.IsRooted
                ? await File.ReadAllBytesAsync(path.FullName)
                : _gameData.GetFile(path.InternalName.ToString())?.Data;
            _loadedShpkPath     = path;
            _associatedShpk     = data?.Length > 0 ? new ShpkFile(data) : throw new Exception("无法加载文件数据。");
            _loadedShpkPathName = path.ToPath();
        }
        catch (Exception e)
        {
            _loadedShpkPath     = FullPath.Empty;
            _loadedShpkPathName = string.Empty;
            _associatedShpk     = null;
            Penumbra.Messager.NotificationMessage(e, $"无法加载 {_loadedShpkPath.ToPath()}.", NotificationType.Error, false);
        }
        finally
        {
            _shpkLoading = false;
        }

        if (_loadedShpkPath.InternalName.IsEmpty)
        {
            _associatedShpkDevkit     = null;
            _loadedShpkDevkitPathName = string.Empty;
        }
        else
        {
            _associatedShpkDevkit =
                TryLoadShpkDevkit(Path.GetFileNameWithoutExtension(Mtrl.ShaderPackage.Name), out _loadedShpkDevkitPathName);
        }

        UpdateShaderKeys();
        _updateOnNextFrame = true;
    }

    private void UpdateShaderKeys()
    {
        _shaderKeys.Clear();
        if (_associatedShpk != null)
            foreach (var key in _associatedShpk.MaterialKeys)
            {
                var keyName    = Names.KnownNames.TryResolve(key.Id);
                var dkData     = TryGetShpkDevkitData<DevkitShaderKey>("ShaderKeys", key.Id, false);
                var hasDkLabel = !string.IsNullOrEmpty(dkData?.Label);

                var valueSet = new HashSet<uint>(key.Values);
                if (dkData != null)
                    valueSet.UnionWith(dkData.Values.Keys);

                var valueKnownNames = keyName.WithKnownSuffixes();

                var mtrlKeyIndex = Mtrl.FindOrAddShaderKey(key.Id, key.DefaultValue);
                var values = valueSet.Select<uint, (string Label, uint Value, string Description)>(value =>
                {
                    var valueName = valueKnownNames.TryResolve(Names.KnownNames, value);
                    if (dkData != null && dkData.Values.TryGetValue(value, out var dkValue))
                        return (dkValue.Label.Length > 0 ? dkValue.Label : valueName.ToString(), value, dkValue.Description);

                    return (valueName.ToString(), value, string.Empty);
                }).ToArray();
                Array.Sort(values, (x, y) =>
                {
                    if (x.Value == key.DefaultValue)
                        return -1;
                    if (y.Value == key.DefaultValue)
                        return 1;

                    return string.Compare(x.Label, y.Label, StringComparison.Ordinal);
                });
                _shaderKeys.Add((hasDkLabel ? dkData!.Label : keyName.ToString(), mtrlKeyIndex, dkData?.Description ?? string.Empty,
                    !hasDkLabel, values));
            }
        else
            foreach (var (key, index) in Mtrl.ShaderPackage.ShaderKeys.WithIndex())
            {
                var keyName   = Names.KnownNames.TryResolve(key.Category);
                var valueName = keyName.WithKnownSuffixes().TryResolve(Names.KnownNames, key.Value);
                _shaderKeys.Add((keyName.ToString(), index, string.Empty, true, [(valueName.ToString(), key.Value, string.Empty)]));
            }
    }

    private void UpdateShaders()
    {
        static void AddShader(HashSet<int> globalSet, Dictionary<uint, HashSet<int>> byPassSets, uint passId, int shaderIndex)
        {
            globalSet.Add(shaderIndex);
            if (!byPassSets.TryGetValue(passId, out var passSet))
            {
                passSet = [];
                byPassSets.Add(passId, passSet);
            }

            passSet.Add(shaderIndex);
        }

        _vertexShaders.Clear();
        _pixelShaders.Clear();

        var vertexShadersByPass = new Dictionary<uint, HashSet<int>>();
        var pixelShadersByPass  = new Dictionary<uint, HashSet<int>>();

        if (_associatedShpk == null || !_associatedShpk.IsExhaustiveNodeAnalysisFeasible())
        {
            _shadersKnown = false;
        }
        else
        {
            _shadersKnown = true;
            var systemKeySelectors  = AllSelectors(_associatedShpk.SystemKeys).ToArray();
            var sceneKeySelectors   = AllSelectors(_associatedShpk.SceneKeys).ToArray();
            var subViewKeySelectors = AllSelectors(_associatedShpk.SubViewKeys).ToArray();
            var materialKeySelector =
                BuildSelector(_associatedShpk.MaterialKeys.Select(key => Mtrl.GetOrAddShaderKey(key.Id, key.DefaultValue).Value));

            foreach (var systemKeySelector in systemKeySelectors)
            {
                foreach (var sceneKeySelector in sceneKeySelectors)
                {
                    foreach (var subViewKeySelector in subViewKeySelectors)
                    {
                        var selector = BuildSelector(systemKeySelector, sceneKeySelector, materialKeySelector, subViewKeySelector);
                        var node     = _associatedShpk.GetNodeBySelector(selector);
                        if (node.HasValue)
                            foreach (var pass in node.Value.Passes)
                            {
                                AddShader(_vertexShaders, vertexShadersByPass, pass.Id, (int)pass.VertexShader);
                                AddShader(_pixelShaders,  pixelShadersByPass,  pass.Id, (int)pass.PixelShader);
                            }
                        else
                            _shadersKnown = false;
                    }
                }
            }
        }

        if (_shadersKnown)
        {
            var builder = new StringBuilder();
            foreach (var (passId, passVertexShader) in vertexShadersByPass)
            {
                if (builder.Length > 0)
                    builder.Append("\n\n");

                var passName = Names.KnownNames.TryResolve(passId);
                var shaders  = passVertexShader.OrderBy(i => i).Select(i => $"#{i}");
                builder.Append($"Vertex Shaders ({passName}): {string.Join(", ", shaders)}");
                if (pixelShadersByPass.TryGetValue(passId, out var passPixelShader))
                {
                    shaders = passPixelShader.OrderBy(i => i).Select(i => $"#{i}");
                    builder.Append($"\nPixel Shaders ({passName}): {string.Join(", ", shaders)}");
                }
            }

            foreach (var (passId, passPixelShader) in pixelShadersByPass)
            {
                if (vertexShadersByPass.ContainsKey(passId))
                    continue;

                if (builder.Length > 0)
                    builder.Append("\n\n");

                var passName = Names.KnownNames.TryResolve(passId);
                var shaders  = passPixelShader.OrderBy(i => i).Select(i => $"#{i}");
                builder.Append($"Pixel Shaders ({passName}): {string.Join(", ", shaders)}");
            }

            _shadersString = Encoding.UTF8.GetBytes(builder.ToString());
        }
        else
        {
            _shadersString = UnknownShadersString;
        }

        _shaderComment = TryGetShpkDevkitData<string>("Comment", null, true) ?? string.Empty;
    }

    private bool DrawShaderSection(bool disabled)
    {
        var ret = false;
        if (ImGui.CollapsingHeader(_shaderHeader))
        {
            ret |= DrawPackageNameInput(disabled);
            ret |= DrawShaderFlagsInput(disabled);
            DrawCustomAssociations();
            ret |= DrawMaterialShaderKeys(disabled);
            DrawMaterialShaders();
        }

        if (!_shpkLoading && (_associatedShpk == null || _associatedShpkDevkit == null))
        {
            ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));

            if (_associatedShpk == null)
                ImUtf8.Text("无法找到适合的着色器 (.shpk) 文件进行交叉引用。一些功能将会缺失。"u8,
                    ImGuiUtil.HalfBlendText(0x80u)); // Half red
            else
                ImUtf8.Text(
                    "未找到此材质着色器的开发工具包文件。请安装一个以获得最佳编辑体验，显示实际常量名称而不是十六进制标识符。"u8,
                    ImGuiUtil.HalfBlendText(0x8080u)); // Half yellow
        }

        return ret;
    }

    private bool DrawPackageNameInput(bool disabled)
    {
        if (disabled)
        {
            ImGui.TextUnformatted("Shader Package: " + Mtrl.ShaderPackage.Name);
            return false;
        }

        var ret = false;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 250.0f);
        using var c = ImRaii.Combo("Shader Package", Mtrl.ShaderPackage.Name);
        if (c)
            foreach (var value in GetShpkNames())
            {
                if (!ImGui.Selectable(value, value == Mtrl.ShaderPackage.Name))
                    continue;

                Mtrl.ShaderPackage.Name = value;
                ret                     = true;
                _associatedShpk         = null;
                _loadedShpkPath         = FullPath.Empty;
                LoadShpk(FindAssociatedShpk(out _, out _));
            }

        return ret;
    }

    private bool DrawShaderFlagsInput(bool disabled)
    {
        var shpkFlags = (int)Mtrl.ShaderPackage.Flags;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 250.0f);
        if (!ImGui.InputInt("Shader Flags", ref shpkFlags, 0, 0,
                ImGuiInputTextFlags.CharsHexadecimal | (disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)))
            return false;

        Mtrl.ShaderPackage.Flags = (uint)shpkFlags;
        SetShaderPackageFlags((uint)shpkFlags);
        return true;
    }

    /// <summary>
    /// Show the currently associated shpk file, if any, and the buttons to associate
    /// a specific shpk from your drive, the modded shpk by path or the default shpk.
    /// </summary>
    private void DrawCustomAssociations()
    {
        const string tooltip = "点击复制文件路径到剪贴板";
        var text = _associatedShpk == null
            ? "关联的 .shpk 文件：无"
            : $"关联的 .shpk 文件：{_loadedShpkPathName}";
        var devkitText = _associatedShpkDevkit == null
            ? "关联的开发工具包文件：无"
            : $"关联的开发工具包文件：{_loadedShpkDevkitPathName}";
        var baseDevkitText = _associatedBaseDevkit == null
            ? "基础开发工具包文件：无"
            : $"基础开发工具包文件：{_loadedBaseDevkitPathName}";

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));

        ImUtf8.CopyOnClickSelectable(text,           _loadedShpkPathName,       tooltip);
        ImUtf8.CopyOnClickSelectable(devkitText,     _loadedShpkDevkitPathName, tooltip);
        ImUtf8.CopyOnClickSelectable(baseDevkitText, _loadedBaseDevkitPathName, tooltip);

        if (ImUtf8.Button("关联自定义 .shpk 文件"u8))
            _fileDialog.OpenFilePicker("关联自定义 .shpk 文件...", ".shpk", (success, name) =>
            {
                if (success)
                    LoadShpk(new FullPath(name[0]));
            }, 1, _edit.Mod!.ModPath.FullName, false);

        var moddedPath = FindAssociatedShpk(out var defaultPath, out var gamePath);
        ImGui.SameLine();
        if (ImUtf8.ButtonEx("关联默认 .shpk 文件"u8, moddedPath.ToPath(), Vector2.Zero,
                moddedPath.Equals(_loadedShpkPath)))
            LoadShpk(moddedPath);

        if (!gamePath.Path.Equals(moddedPath.InternalName))
        {
            ImGui.SameLine();
            if (ImUtf8.ButtonEx("关联未修改的 .shpk 文件", defaultPath, Vector2.Zero,
                    gamePath.Path.Equals(_loadedShpkPath.InternalName)))
                LoadShpk(new FullPath(gamePath));
        }

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
    }

    private bool DrawMaterialShaderKeys(bool disabled)
    {
        if (_shaderKeys.Count == 0)
            return false;

        var ret = false;
        foreach (var (label, index, description, monoFont, values) in _shaderKeys)
        {
            using var font         = ImRaii.PushFont(UiBuilder.MonoFont, monoFont);
            ref var   key          = ref Mtrl.ShaderPackage.ShaderKeys[index];
            using var id           = ImUtf8.PushId((int)key.Category);
            var       shpkKey      = _associatedShpk?.GetMaterialKeyById(key.Category);
            var       currentValue = key.Value;
            var (currentLabel, _, currentDescription) =
                values.FirstOrNull(v => v.Value == currentValue) ?? ($"0x{currentValue:X8}", currentValue, string.Empty);
            if (!disabled && shpkKey.HasValue)
            {
                ImGui.SetNextItemWidth(UiHelpers.Scale * 250.0f);
                using (var c = ImUtf8.Combo(""u8, currentLabel))
                {
                    if (c)
                        foreach (var (valueLabel, value, valueDescription) in values)
                        {
                            if (ImGui.Selectable(valueLabel, value == currentValue))
                            {
                                key.Value = value;
                                ret       = true;
                                Update();
                            }

                            if (valueDescription.Length > 0)
                                ImGuiUtil.SelectableHelpMarker(valueDescription);
                        }
                }

                ImGui.SameLine();
                if (description.Length > 0)
                    ImGuiUtil.LabeledHelpMarker(label, description);
                else
                    ImUtf8.Text(label);
            }
            else if (description.Length > 0 || currentDescription.Length > 0)
            {
                ImUtf8.LabeledHelpMarker($"{label}: {currentLabel}",
                    description + (description.Length > 0 && currentDescription.Length > 0 ? "\n\n" : string.Empty) + currentDescription);
            }
            else
            {
                ImUtf8.Text($"{label}: {currentLabel}");
            }
        }

        return ret;
    }

    private void DrawMaterialShaders()
    {
        if (_associatedShpk == null)
            return;

        using (var node = ImUtf8.TreeNode("候选着色器"u8))
        {
            if (node)
                ImUtf8.Text(_shadersString.Span);
        }

        if (_shaderComment.Length > 0)
        {
            ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
            ImUtf8.Text(_shaderComment);
        }
    }
}
