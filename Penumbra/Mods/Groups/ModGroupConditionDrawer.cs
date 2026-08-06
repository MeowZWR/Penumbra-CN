using Luna;

namespace Penumbra.Mods.Groups;

public sealed class ModGroupConditionDrawer : ConditionDrawer<ModSettingContext>, IUiService
{
    protected override bool DrawCustom(ICondition<ModSettingContext>? condition, ModSettingContext context,
        out ICondition<ModSettingContext>? replace)
    {
        replace = null;
        switch (condition)
        {
            case SingleSettingCondition single:
            { }
                break;
            case MultiSettingAllCondition all:
            { }
                break;
            case MultiSettingAnyCondition any:
            { }
                break;
        }

        return false;
    }
}
