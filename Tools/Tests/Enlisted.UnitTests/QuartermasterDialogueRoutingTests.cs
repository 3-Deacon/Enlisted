using System;
using System.IO;
using Xunit;

namespace Enlisted.UnitTests;

public sealed class QuartermasterDialogueRoutingTests
{
    [Fact]
    public void QuartermasterBrowseRoutingUsesPublicBrowsingApis()
    {
        var dialogManager = ReadRepoFile("src", "Features", "Conversations", "Behaviors", "EnlistedDialogManager.cs");

        Assert.DoesNotContain("BuildWeaponOptionsFromCurrentTroop", dialogManager, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildArmorOptionsFromCurrentTroop", dialogManager, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildShieldOptionsFromWeapons", dialogManager, StringComparison.Ordinal);
        Assert.Contains("GetWeaponVariantsForBrowsing()", dialogManager, StringComparison.Ordinal);
        Assert.Contains("GetArmorVariantsForBrowsing()", dialogManager, StringComparison.Ordinal);
        Assert.Contains("GetAccessoryVariantsForBrowsing()", dialogManager, StringComparison.Ordinal);
    }

    [Fact]
    public void QuartermasterManagerExposesBrowseCategoryApis()
    {
        var quartermasterManager = ReadRepoFile("src", "Features", "Equipment", "Behaviors", "QuartermasterManager.cs");

        Assert.Contains("public List<EquipmentVariantOption> GetWeaponVariantsForBrowsing()", quartermasterManager, StringComparison.Ordinal);
        Assert.Contains("public List<EquipmentVariantOption> GetArmorVariantsForBrowsing()", quartermasterManager, StringComparison.Ordinal);
        Assert.Contains("public List<EquipmentVariantOption> GetAccessoryVariantsForBrowsing()", quartermasterManager, StringComparison.Ordinal);
    }

    [Fact]
    public void QuartermasterEquipmentGridBindingsHaveViewModelProperties()
    {
        var equipmentSelectorVm = ReadRepoFile("src", "Features", "Equipment", "UI", "QuartermasterEquipmentSelectorVM.cs");

        Assert.Contains("public string HeaderText { get; private set; }", equipmentSelectorVm, StringComparison.Ordinal);
        Assert.Contains("public string CurrentEquipmentText { get; private set; }", equipmentSelectorVm, StringComparison.Ordinal);
        Assert.Contains("public string PlayerGoldText { get; private set; }", equipmentSelectorVm, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, Path.Combine(relativeParts));
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(relativeParts));
    }
}
