using System.Text.Json;
using Luna;
using Penumbra.Files;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.Services;

namespace Penumbra.UI.FileEditing.Materials;

/// <summary> User-defined color table row presets, stored in a separate file that upstream Penumbra does not read. </summary>
public sealed class ColorTableRowPresets : ConfigurationFile<FilenameService>
{
    private const int NumValues = ColorTableRow.NumVec4 * ColorTableRow.Halves;

    public sealed class Preset(string name, in ColorTableRow row, string shaderPackage)
    {
        public string        Name          = name;
        public ColorTableRow Row           = row;
        public string        ShaderPackage = shaderPackage;
    }

    private readonly List<Preset> _presets = [];

    public IReadOnlyList<Preset> Presets
        => _presets;

    public override int CurrentVersion
        => 1;

    public ColorTableRowPresets(SaveService saveService, PenumbraMessager messager)
        : base(saveService, messager)
        => Load();

    public int IndexOf(string name)
        => _presets.FindIndex(p => p.Name == name);

    /// <summary> Add a new preset or overwrite the values of an existing preset with the same name. </summary>
    public void Set(string name, in ColorTableRow row, string shaderPackage)
    {
        var idx = IndexOf(name);
        if (idx >= 0)
        {
            _presets[idx].Row           = row;
            _presets[idx].ShaderPackage = shaderPackage;
        }
        else
        {
            _presets.Add(new Preset(name, row, shaderPackage));
        }

        SaveService.QueueSave(this);
    }

    public bool Rename(int idx, string name)
    {
        name = name.Trim();
        if (name.Length is 0 || IndexOf(name) >= 0)
            return false;

        _presets[idx].Name = name;
        SaveService.QueueSave(this);
        return true;
    }

    public void Delete(int idx)
    {
        _presets.RemoveAt(idx);
        SaveService.QueueSave(this);
    }

    protected override void AddData(Utf8JsonWriter j)
    {
        j.WriteStartArray("Presets"u8);
        foreach (var preset in _presets)
        {
            j.WriteStartObject();
            j.WriteString("Name"u8, preset.Name);
            if (preset.ShaderPackage.Length > 0)
                j.WriteString("ShaderPackage"u8, preset.ShaderPackage);
            j.WriteStartArray("Values"u8);
            foreach (var half in (ReadOnlySpan<Half>)preset.Row)
            {
                var value = (float)half;
                j.WriteNumberValue(float.IsFinite(value) ? value : float.IsNaN(value) ? 0f : MathF.CopySign((float)Half.MaxValue, value));
            }

            j.WriteEndArray();
            j.WriteEndObject();
        }

        j.WriteEndArray();
    }

    protected override void LoadData(in JsonElement j)
    {
        if (!j.TryReadArray("Presets"u8, out var array))
            return;

        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind is not JsonValueKind.Object
             || !element.TryReadProperty("Name"u8, out string? name)
             || string.IsNullOrWhiteSpace(name)
             || IndexOf(name) >= 0
             || !element.TryReadArray("Values"u8, out var values)
             || values.GetArrayLength() is not NumValues)
                continue;

            var row = new ColorTableRow();
            var i   = 0;
            foreach (var value in values.EnumerateArray())
                row[i++] = (Half)value.GetSingle();
            _presets.Add(new Preset(name, row, element.PropertyOrDefault("ShaderPackage"u8, string.Empty)));
        }
    }

    public override string ToFilePath(FilenameService fileNames)
        => Path.Combine(fileNames.Config.Folder, "color_table_row_presets.json");
}
