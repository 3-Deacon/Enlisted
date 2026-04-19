namespace Enlisted.Features.Content
{
    /// <summary>Named slot role binding. Tag is optional; when set the filler requires that flag on the candidate hero.</summary>
    public sealed class SlotDecl
    {
        public string Role { get; set; } = string.Empty;   // enlisted_lord | squadmate | named_veteran | chaplain | quartermaster | enemy_commander | settlement_notable | kingdom
        public string Tag { get; set; } = string.Empty;    // optional flag name
    }
}
