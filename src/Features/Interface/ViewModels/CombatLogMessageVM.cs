using System;
using TaleWorlds.Library;

namespace Enlisted.Features.Interface.ViewModels
{
    /// <summary>
    /// ViewModel for individual combat log messages.
    /// Tracks message content, color, age, and fade effects.
    /// </summary>
    public class CombatLogMessageVM : ViewModel
    {
        private string _text;
        private Color _messageColor;
        private float _alphaFactor;

        public CombatLogMessageVM(string text, Color color)
        {
            Text = text;
            MessageColor = color;
            CreatedAt = DateTime.UtcNow;
            AlphaFactor = 1.0f;
        }

        /// <summary>
        /// Message text content.
        /// </summary>
        [DataSourceProperty]
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChangedWithValue(value, nameof(Text));
                }
            }
        }

        /// <summary>
        /// Message color based on type (success, warning, failure, etc.).
        /// </summary>
        [DataSourceProperty]
        public Color MessageColor
        {
            get => _messageColor;
            set
            {
                if (_messageColor != value)
                {
                    _messageColor = value;
                    OnPropertyChangedWithValue(value, nameof(MessageColor));
                }
            }
        }

        /// <summary>
        /// Alpha factor for fade effect (1.0 = fully visible, 0.0 = invisible).
        /// </summary>
        [DataSourceProperty]
        public float AlphaFactor
        {
            get => _alphaFactor;
            set
            {
                if (_alphaFactor != value)
                {
                    _alphaFactor = value;
                    OnPropertyChangedWithValue(value, nameof(AlphaFactor));
                }
            }
        }

        /// <summary>
        /// Real-time when message was created (UTC).
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets the age of this message in seconds (real-time).
        /// </summary>
        public float GetAgeInSeconds()
        {
            return (float)(DateTime.UtcNow - CreatedAt).TotalSeconds;
        }
    }
}
