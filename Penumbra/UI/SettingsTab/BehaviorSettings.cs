using ImSharp;
using Luna;

namespace Penumbra.UI;

public sealed class BehaviorSettings(BehaviorConfig config) : IUiService
{
    public void Draw()
    {
        DrawGeneralBehavior();
        DrawCollectionAssociation();
    }

    private void DrawGeneralBehavior()
    {
        using var tree = Im.Tree.Node("常规"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("自动选择角色关联合集"u8,
                "每次登录时，自动选择与当前角色关联的合集作为当前编辑的合集。"u8,
                config.AutoSelectCollection))
            config.AutoSelectCollection ^= true;

        if (SettingsTab.Checkbox("允许其他插件的UI使用界面合集"u8,
                "允许其他卫月插件在调用UI材质时使用界面合集中的文件。"u8,
                config.UseDalamudUiTextureRedirection))
            config.UseDalamudUiTextureRedirection ^= true;

        LunaStyle.DrawSeparator();
    }

    private void DrawCollectionAssociation()
    {
        using var tree = Im.Tree.Node("合集关联"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("在登陆界面中使用合集"u8,
                "如果禁用此选项，则不会对登陆界面或美容师中的角色应用任何模组。"u8,
                config.ShowModsInLobby))
            config.ShowModsInLobby ^= true;
        if (SettingsTab.Checkbox("在角色窗口中使用合集"u8,
                "如果设置，则使用基于你的玩家名字命名的独立角色合集或你的角色组合集。"u8,
                config.UseCharacterCollectionInMainWindow))
            config.UseCharacterCollectionInMainWindow ^= true;
        if (SettingsTab.Checkbox("在冒险者铭牌中使用合集"u8,
                "根据冒险者的姓名，为其使用合适的合集。"u8,
                config.UseCharacterCollectionsInCards))
            config.UseCharacterCollectionsInCards ^= true;
        if (SettingsTab.Checkbox("在试穿窗口中使用合集"u8,
                "如果设置，则使用基于你的角色名字的独立合集。"u8,
                config.UseCharacterCollectionInTryOn))
            config.UseCharacterCollectionInTryOn ^= true;
        if (SettingsTab.Checkbox("在调查窗口中不使用模组"u8,
                "使用空合集来调查角色，不管是什么角色。\n"u8
              + "优先于下一个选项。"u8, config.UseNoModsInInspect))
            config.UseNoModsInInspect ^= true;
        if (SettingsTab.Checkbox("在调查窗口中使用合集"u8,
                "根据当前调查的角色的名称，为其使用符合角色名称的合集。"u8,
                config.UseCharacterCollectionInInspect))
            config.UseCharacterCollectionInInspect ^= true;
        if (SettingsTab.Checkbox("基于所有者使用合集"u8,
                "使用所有者的名字来决定其坐骑、宠物、时尚配饰、战斗伙伴使用适当的角色合集。"u8,
                config.UseOwnerNameForCharacterCollection))
            config.UseOwnerNameForCharacterCollection ^= true;
        if (config.UseOwnerNameForCharacterCollection)
            using (Im.Indent(Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X))
            {
                if (SettingsTab.Checkbox("包含敌对所有者角色"u8,
                        "包含任何由角色拥有的敌对角色，例如为单人任务生成的敌人。"u8,
                        config.UseOwnerForHostiles))
                    config.UseOwnerForHostiles ^= true;
            }
    }
}
