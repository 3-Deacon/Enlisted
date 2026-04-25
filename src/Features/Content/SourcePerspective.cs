namespace Enlisted.Features.Content
{
    /// <summary>
    /// Who is speaking / observing in the projected signal. Determines
    /// rendering voice in the news feed. Plan 3 signal-projection layer
    /// populates this; emit sites outside Plan 3 default to Narrator.
    /// </summary>
    public enum SourcePerspective : byte
    {
        Narrator,
        Sergeant,
        Rumor,
        OwnObservation,
        Courier,
        Surgeon,
        Quartermaster
    }
}
