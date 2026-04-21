using System;
using System.IO;
using Enlisted.Mod.Core.Logging;
using Newtonsoft.Json;

namespace Enlisted.Features.Interface.Utils
{
    public sealed class CombatLogUiState
    {
        public float ExpandedWidth { get; set; } = CombatLogUiStateStore.DefaultExpandedWidth;

        public float ExpandedHeight { get; set; } = CombatLogUiStateStore.DefaultExpandedHeight;

        public float OffsetX { get; set; }

        public float OffsetY { get; set; }
    }

    public static class CombatLogUiStateStore
    {
        private const string FileName = "combat_log_ui_state.json";
        private const float ScreenEdgePadding = 80f;
        private const float DefaultMargin = 30f;

        public const float DefaultExpandedWidth = 640f;
        public const float DefaultExpandedHeight = 420f;
        public const float MinExpandedWidth = 480f;
        public const float MinExpandedHeight = 320f;

        private static string StoreDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Enlisted");

        private static string StorePath => Path.Combine(StoreDirectory, FileName);

        public static CombatLogUiState Load()
        {
            try
            {
                if (!File.Exists(StorePath))
                {
                    return new CombatLogUiState();
                }

                var state = JsonConvert.DeserializeObject<CombatLogUiState>(File.ReadAllText(StorePath));
                if (state == null)
                {
                    return new CombatLogUiState();
                }

                if (state.ExpandedWidth <= 0f)
                {
                    state.ExpandedWidth = DefaultExpandedWidth;
                }

                if (state.ExpandedHeight <= 0f)
                {
                    state.ExpandedHeight = DefaultExpandedHeight;
                }

                return state;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Failed to load combat log UI state", ex);
                return new CombatLogUiState();
            }
        }

        public static void Save(float width, float height, float offsetX, float offsetY)
        {
            try
            {
                Directory.CreateDirectory(StoreDirectory);

                var state = new CombatLogUiState
                {
                    ExpandedWidth = width,
                    ExpandedHeight = height,
                    OffsetX = offsetX,
                    OffsetY = offsetY
                };

                File.WriteAllText(StorePath, JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Failed to save combat log UI state", ex);
            }
        }

        public static float ClampWidth(float width, float viewportWidth)
        {
            float maxWidth = viewportWidth > 0f
                ? Math.Max(MinExpandedWidth, viewportWidth - ScreenEdgePadding)
                : Math.Max(MinExpandedWidth, width);

            return Math.Max(MinExpandedWidth, Math.Min(width, maxWidth));
        }

        public static float ClampHeight(float height, float viewportHeight)
        {
            float maxHeight = viewportHeight > 0f
                ? Math.Max(MinExpandedHeight, viewportHeight - ScreenEdgePadding)
                : Math.Max(MinExpandedHeight, height);

            return Math.Max(MinExpandedHeight, Math.Min(height, maxHeight));
        }

        public static float ClampOffsetX(float offsetX, float panelWidth, float viewportWidth)
        {
            if (viewportWidth <= 0f || panelWidth <= 0f)
            {
                return offsetX;
            }

            float maxOffset = DefaultMargin;
            float minOffset = panelWidth + DefaultMargin - viewportWidth;
            return Math.Max(minOffset, Math.Min(offsetX, maxOffset));
        }

        public static float ClampOffsetY(float offsetY, float panelHeight, float viewportHeight)
        {
            if (viewportHeight <= 0f || panelHeight <= 0f)
            {
                return offsetY;
            }

            float minOffset = -DefaultMargin;
            float maxOffset = viewportHeight - panelHeight - DefaultMargin;
            return Math.Max(minOffset, Math.Min(offsetY, maxOffset));
        }
    }

}
