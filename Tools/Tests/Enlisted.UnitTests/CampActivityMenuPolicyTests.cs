using Enlisted.Features.Interface.Models;
using Xunit;

namespace Enlisted.UnitTests;

public sealed class CampActivityMenuPolicyTests
{
    [Fact]
    public void CampActivitiesUseDedicatedFiveSlotSubmenu()
    {
        Assert.Equal(5, CampActivityMenuPolicy.ActivitySlotCount);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("not_now")]
    [InlineData("nevermind")]
    [InlineData("decline_request")]
    [InlineData("back_to_camp")]
    [InlineData("skip_shift")]
    public void CancelLikeOptionsDoNotCommitDecisionCooldowns(string optionId)
    {
        Assert.True(CampActivityMenuPolicy.IsCancelOptionId(optionId));
    }

    [Theory]
    [InlineData("train_with_veterans")]
    [InlineData("seek_surgeon")]
    [InlineData("maintain_gear")]
    public void ActionOptionsCommitDecisionCooldowns(string optionId)
    {
        Assert.False(CampActivityMenuPolicy.IsCancelOptionId(optionId));
    }
}
