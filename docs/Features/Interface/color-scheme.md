# Enlisted Menu Color Scheme

> Professional military intelligence brief theme for GameMenu text displays

|| |
|---|---|
| **Implemented** | December 23, 2025 |
| **Status** | Active |
| **Brush File** | `GUI/Brushes/EnlistedColors.xml` |
| **Primary Usage** | GameMenu text displays + Combat log messages |

---

## Overview

The Enlisted mod uses a semantic color palette to improve readability and create visual hierarchy in text-based GameMenu displays. The color scheme follows a "military intelligence brief" theme where section headers, status labels, and severity indicators use distinct colors for quick scanning.

Players can instantly identify problems at a glance - critical supplies, low reputation, and exhausted troops jump out in red/gold while healthy stats stay calm and readable.

---

## Systems Using Color Scheme

### GameMenu Displays

| Menu | Colored Elements |
|------|------------------|
| **Main Menu (enlisted_status)** | KINGDOM REPORTS, COMPANY REPORTS, PLAYER STATUS headers + all status values |
| **Camp Hub** | RECENT ACTIVITY, STATUS headers + all status labels/values |
| **Reports (Legacy)** | DAILY BRIEF, RECENT ACTIVITY, COMPANY STATUS, CAMPAIGN CONTEXT headers (if implemented) |
| **Status Detail** | REPUTATION, ROLE & SPECIALIZATIONS, PERSONALITY TRAITS headers + reputation values |
| **Decisions Menu** | COMPANY REPORT header (if present) |
| **Muster Menu** | All 6 stages use full color scheme: section headers, status labels, reputation values, XP progress, rank titles |

### Combat Log Messages

| System | Message Color | Example |
|--------|---------------|---------|
| **Decision Effects** | Green | `+25 OneHanded XP, +5 Soldier Reputation, -30 gold` |
| **Decision Costs** | Yellow | `Cost: -30 gold, +2 fatigue` |
| **Decision Rewards** | Cyan | `+50 gold, -3 fatigue` |
| **Order Effects** | Green | `Order: +30 Leadership XP, +3 Officer Reputation` |
| **Warnings** | Red | `You are badly wounded!` |

---

## Color Palette

### Semantic Colors (Custom Styles)

| Style Name | Hex Color | RGB | Purpose | Usage |
|------------|-----------|-----|---------|-------|
| **Header** | `#00CCFFFF` | Cyan | Section headers | Major report sections (KINGDOM REPORTS, COMPANY REPORTS, PLAYER STATUS, etc.) |
| **Label** | `#8CDBC4FF` | Teal | Information labels | Status field names (READINESS:, MORALE:, SUPPLIES:, etc.) |
| **Success** | `#90EE90FF` | Light Green | Positive status | Good supplies (60+), high reputation, elite troops |
| **Warning** | `#FFD700FF` | Gold | Caution status | Fair conditions (40-59), [IMMINENT] tags, needs attention |
| **Alert** | `#FF6B6BFF` | Soft Red | Critical status | Poor conditions (<40), siege situations, immediate attention required |
| **Critical** | `#FF0000FF` | Bright Red | Extreme danger | Life-threatening injuries, catastrophic failures (with text outline for max visibility) |
| **Urgent** | `#FFA500FF` | Orange | Time-sensitive neutral | Imminent orders, scheduled events, deadlines (not good/bad, just "act now") |
| **Default** | `#FAF4DEFF` | Light Cream | Body text | Standard descriptive text, neutral information, troop counts |

### Native Styles (Used by Mod)

| Style Name | Purpose | Usage |
|------------|---------|-------|
| **Link** | Interactive elements, highlights | Accordion headers (ORDERS, DECISIONS), [NEW] tags, settlement/army names, contextual highlights |

### Integration with Native Styles

The custom colors work alongside Bannerlord's native GameMenu styles:

- **Custom styles** (Header, Label, Success, Warning, Alert) defined in `EnlistedColors.xml`
- **Native Link style** used for accordion headers, [NEW] tags, and interactive elements  
- **Header** color (cyan) is similar to native Link for visual consistency
- **Label** color (teal) provides distinct category labels
- **Default** uses a light cream tone matching Bannerlord's native text color

---

## Implementation

### Files

| File | Purpose |
|------|---------|
| `GUI/Brushes/EnlistedColors.xml` | Custom brush definition with color styles |
| `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` | Text builders using color spans |

### Code Patterns

**Section headers** use the Header style (cyan):
```csharp
sb.AppendLine("<span style=\"Header\">_____ COMPANY STATUS _____</span>");
```

**Status labels** use the Label style (teal):
```csharp
sb.AppendLine($"<span style=\"Label\">READINESS:</span> {description}");
```

**Status values** use conditional colors based on severity:
```csharp
var colorStyle = value >= 60 ? "Default" : value >= 40 ? "Warning" : "Alert";
var coloredDescription = $"<span style=\"{colorStyle}\">{description}</span>";
```

**Supplies** use Success (green) for good status since it's a critical resource:
```csharp
var suppliesColor = needs.Supplies >= 60 ? "Success" : needs.Supplies >= 40 ? "Warning" : "Alert";
```

**Reputation** values use Success/Warning/Alert based on standing:
```csharp
var lordColor = escalation.State.LordReputation >= 60 ? "Success" : escalation.State.LordReputation >= 40 ? "Warning" : "Alert";
```

---

## Visual Example

**Before (All White):**
```
=== COMPANY STATUS ===
READINESS: The company is prepared for action...
MORALE: The company's mood is steady...
SUPPLIES: Food is scarce. Men go hungry...
EQUIPMENT: Gear is serviceable...
REST: The company has had adequate rest...
```

**After (Color-Coded):**
```
_____ COMPANY STATUS _____              ← CYAN header (stands out)
READINESS: prepared for action...       ← TEAL label + cream text
MORALE: mood is steady...               ← TEAL label + cream text
SUPPLIES: Food is scarce...             ← TEAL label + RED text (Alert!)
EQUIPMENT: serviceable...               ← TEAL label + cream text
REST: adequate rest...                  ← TEAL label + cream text
```

---

## Coloring Rules

### Company Needs (Readiness, Morale, Equipment, Rest)

| Value Range | Color | Meaning |
|-------------|-------|---------|
| 60-100 | Default (Cream) | Satisfactory conditions |
| 40-59 | Warning (Gold) | Needs attention |
| 0-39 | Alert (Red) | Critical, immediate action required |

### Supplies

| Value Range | Color | Meaning |
|-------------|-------|---------|
| 60-100 | Success (Green) | Well-stocked |
| 40-59 | Warning (Gold) | Running low |
| 0-39 | Alert (Red) | Critical shortage |

Supplies uses green for good status because it's a critical resource players actively manage.

### Reputation

| Value Range | Color | Meaning |
|-------------|-------|---------|
| 60-100 | Success (Green) | Good standing |
| 40-59 | Warning (Gold) | Needs improvement |
| 0-39 | Alert (Red) | Dangerous territory |

### News Feed Severity Levels

Used by the news reporting system for event outcomes, routine activities, and order results:

| Severity | Color | Meaning | Examples |
|----------|-------|---------|----------|
| 0 | Default (Cream) | Normal events | Routine activities completed normally |
| 1 | Success (Green) | Positive outcomes | Excellent performance, successful orders, skill gains |
| 2 | Warning (Gold) | Needs attention | Poor performance, failed options, minor mishaps, sickness |
| 3 | Alert (Red) | Urgent issues | Severe conditions, critical failures, injuries |
| 4 | Critical (Bright Red) | Extreme danger | Life-threatening situations, catastrophic events |

**Routine Activity Outcome Mapping:**
- `Excellent` → Severity 1 (Green)
- `Good/Normal` → Severity 0 (Cream)
- `Poor/Mishap` → Severity 2 (Yellow)

---

## Design Rationale

### Why These Colors?

**Cyan Headers:** Stand out without overwhelming, creates clear section breaks for quick navigation.

**Teal Labels:** Distinct from body text, easy to scan vertically for specific status fields.

**Color-Coded Values:** Immediate visual feedback on what needs attention. Red text draws the eye to problems without reading every line.

**Warm Cream Body Text:** Softer than pure white, reduces eye strain during extended play sessions.

### Accessibility

- All colors have sufficient contrast against dark GameMenu backgrounds
- Color is supplementary to text content, not required for comprehension
- Status severity is also communicated through text descriptions

---

## Extension Points

### Adding New Colors

Edit `GUI/Brushes/EnlistedColors.xml`:

```xml
<Style Name="YourName" FontColor="#RRGGBBAA" TextGlowColor="#000000FF" FontSize="22" />
```

Use in code:
```csharp
sb.AppendLine($"<span style=\"YourName\">Your text here</span>");
```

### Changing Existing Colors

Modify the `FontColor` hex value in `EnlistedColors.xml`. Changes take effect on next game launch.

---

## Technical Notes

### Where Colors Work

Colors work in:
- GameMenu text that uses `RichTextWidget` (menu descriptions, option text)
- Combat log messages via `InformationManager.DisplayMessage()` with `Colors.*` constants
- Any UI element that supports Bannerlord's native color system

### Where Colors Don't Work

- Console output / debug logging
- Standard `TextObject` without RichText rendering
- Plain string messages without color parameters
- Some native Bannerlord UI elements

### Brush Loading

The brush XML is auto-loaded from `GUI/Brushes/` by the game engine. No explicit registration in SubModule.xml is required for brush files. The brush overrides `GameMenu.InfoText` to add the custom styles to the native brush.

---

## Version History

**January 3, 2026:**
- Added News Feed Severity Levels section documenting the 0-4 severity scale
- Documented routine activity outcome mapping to severity levels
- Clarified how severity levels translate to visual colors in news feed display

**December 31, 2025:**
- Updated menu references to match current implementation:
  - Main Menu now shows: KINGDOM REPORTS, COMPANY REPORTS, PLAYER STATUS
  - Camp Hub shows: RECENT ACTIVITY, STATUS
  - Removed reference to separate Reports Menu (was removed)
- Added documentation for native **Link** style usage (accordion headers, [NEW] tags, settlement names)
- Corrected Default color hex to match actual brush file: `#FAF4DEFF`
- Enhanced color usage examples with actual implementation details

**December 23, 2025:**
- Initial color scheme documentation
- Defined 8 semantic colors: Header, Label, Success, Warning, Alert, Critical, Urgent, Default
- Created `EnlistedColors.xml` brush file
