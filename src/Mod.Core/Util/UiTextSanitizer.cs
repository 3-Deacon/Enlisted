using System.Globalization;
using System.Text;

namespace Enlisted.Mod.Core.Util
{
    /// <summary>
    /// Normalizes player-facing text so it renders consistently in Bannerlord UI.
    ///
    /// Bannerlord fonts can display some unicode punctuation / emoji as tofu boxes.
    /// We keep the intent of the text, but normalize it to plain ASCII where practical.
    /// </summary>
    internal static class UiTextSanitizer
    {
        public static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            // Fast-path: if everything is basic ASCII, return as-is.
            var hasNonAscii = false;
            for (var i = 0; i < input.Length; i++)
            {
                if (input[i] > 0x7F)
                {
                    hasNonAscii = true;
                    break;
                }
            }

            if (!hasNonAscii)
            {
                return input;
            }

            // Normalize common punctuation that often renders poorly.
            // NOTE: do these replacements before the unicode-category filter below.
            var text = input
                .Replace('\u2014', '-')   // em dash —  -> -
                .Replace('\u2013', '-')   // en dash –  -> -
                .Replace('\u2212', '-')   // minus sign − -> -
                .Replace("\u2026", "...") // ellipsis … -> ...
                .Replace('\u2019', '\'')  // right single quote ’ -> '
                .Replace('\u2018', '\'')  // left single quote  ‘ -> '
                .Replace('\u201C', '"')   // left double quote  “ -> "
                .Replace('\u201D', '"')   // right double quote ” -> "
                .Replace('\u00A0', ' ')   // non-breaking space -> space
                .Replace('\u2022', '-')   // bullet • -> -
                .Replace("\uFE0F", string.Empty); // variation selector-16 (emoji presentation)

            var sb = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);

                // Strip emoji / symbol glyphs that typically render as squares in Bannerlord.
                if (cat == UnicodeCategory.Surrogate || cat == UnicodeCategory.OtherSymbol)
                {
                    continue;
                }

                _ = sb.Append(c);
            }

            return sb.ToString();
        }
    }
}


