# Enlisted Camp Hub - Custom Gauntlet Implementation

**Summary:** Implementation specification for replacing the main camp hub (`enlisted_status`) with a custom Gauntlet UI screen. Provides horizontal button layout, dynamic order cards, and settlement access while all submenus remain native GameMenu.

**Status:** 📋 Specification  
**Last Updated:** 2025-12-24  
**Related Docs:** [ui-systems-master.md](ui-systems-master.md), [quartermaster-system.md](../Equipment/quartermaster-system.md), [order-progression-system.md](../Core/order-progression-system.md)

---

## Index

1. [Getting Started](#getting-started)
2. [Scope](#scope)
3. [Overview](#overview)
4. [Purpose](#purpose)
5. [Visual Layout](#visual-layout)
6. [Technical Architecture](#technical-architecture)
7. [Background System Implementation](#background-system-implementation)
8. [ViewModel Implementation](#viewmodel-implementation)
9. [Behavior Integration](#behavior-integration)
10. [File Structure](#file-structure)
11. [Integration with Existing Systems](#integration-with-existing-systems)
12. [Testing & Validation](#testing--validation)
13. [Performance Considerations](#performance-considerations)
14. [Migration Path](#migration-path)
15. [Acceptance Criteria](#acceptance-criteria)
16. [Future Enhancements](#future-enhancements)
17. [Edge Cases & Compatibility Verification](#edge-cases--compatibility-verification)
18. [Critical Implementation Notes](#critical-implementation-notes)
19. [References](#references)
20. [Implementation Estimate](#implementation-estimate)

---

## Getting Started

### Read These First

1. **`src/Features/Equipment/UI/QuartermasterEquipmentSelectorBehavior.cs`** - Working Gauntlet implementation to copy patterns from (layer creation, movie loading, cleanup)
2. **`src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs`** - Current GameMenu registration, understand what `enlisted_status` does today
3. **`C:\Dev\Enlisted\Decompile\SandBox.GauntletUI\Menu\GauntletMenuBaseView.cs`** - Native menu view pattern for reference

### Implementation Order

1. **Create XML prefab first** (`GUI/Prefabs/CampHub/EnlistedCampHub.xml`) - Start with minimal layout, verify it loads
2. **Create ViewModel** (`EnlistedCampHubVM.cs`) - Add properties one section at a time
3. **Create Behavior** (`EnlistedCampHubBehavior.cs`) - Copy Quartermaster pattern, wire up hotkey
4. **Add to .csproj** - Add `<Compile Include="..."/>` entries for all new .cs files
5. **Test incrementally** - Verify each section works before adding the next

### Key Pattern to Copy

From `QuartermasterEquipmentSelectorBehavior.cs`:

```csharp
// This exact pattern works - copy it
_gauntletLayer = new GauntletLayer("EnlistedCampHub", 150);
_gauntletMovie = _gauntletLayer.LoadMovie("EnlistedCampHub", _viewModel);
_gauntletLayer.InputRestrictions.SetInputRestrictions();
ScreenManager.TopScreen.AddLayer(_gauntletLayer);
_gauntletLayer.IsFocusLayer = true;
ScreenManager.TrySetFocus(_gauntletLayer);
```

---

## Scope

### What Gets Replaced

| Menu | Implementation | Notes |
|------|----------------|-------|
| `enlisted_status` (main hub) | **Custom Gauntlet** | Horizontal buttons, dynamic sections, custom layout |

### What Stays Native

| Menu | Implementation | Notes |
|------|----------------|-------|
| `enlisted_camp_hub` | Native GameMenu | Keep as-is |
| `enlisted_orders` | Native GameMenu | Keep as-is |
| `enlisted_decisions` | Native GameMenu | Keep as-is |
| `enlisted_reports` | Native GameMenu | Keep as-is |
| `enlisted_status_view` | Native GameMenu | Keep as-is |
| `enlisted_quartermaster` | Native GameMenu | Opens Quartermaster Gauntlet screen |
| All town/castle/village menus | Native GameMenu | Untouched |

The custom Gauntlet hub navigates to native submenus via `GameMenu.SwitchToMenu()` - no changes needed to those menus.

---

## Overview

Replace ONLY the `enlisted_status` GameMenu with a custom Gauntlet screen. The custom hub maintains native GameMenu visual style (sliding panel, location backgrounds, semi-transparent overlay) but enables custom button layouts including horizontal button rows and dynamic content sections.

All submenus (Orders, Decisions, Reports, etc.) remain as native GameMenu implementations. This allows incremental editing of the main hub while keeping the rest fully functional.

---

## Purpose

### Goals

1. Enable custom button layouts for MAIN HUB ONLY (horizontal rows, grids, custom positioning)
2. Support complex content sections with mixed layouts (status panels, news feeds, action logs)
3. **Dynamic content sections** that appear/disappear based on game state (pending orders, settlement access)
4. Maintain native GameMenu visual consistency (backgrounds, animations, styling)
5. Preserve expected behaviors (slide animation, hotkey access, time control)
6. Navigate to existing native submenus seamlessly

### Non-Goals

- Replacing submenus (Orders, Decisions, Reports stay as native GameMenu)
- Changing the visual style or player-facing behavior
- Building a generic menu framework for other mods
- Rewriting any native town/castle menus

---

## Visual Layout

### Design Note

This document describes the **Camp Hub** - the deep interaction menu accessed via [CAMP] from the Main Menu. For the camp simulation system, see [Camp Simulation System](../Campaign/camp-simulation-system.md).

### Complete Hub Structure

```
╔══════════════════════════════════════════════════════════╗
║  CAMP HUB                                                 ║
║  {RANK_NAME} ({TIER}) • Day {X} of 12 • {LOCATION}       ║
╠══════════════════════════════════════════════════════════╣
║                                                           ║
║  _____ CAMP STATUS _____                                  ║
║  ⚙️ {RHYTHM} - {ACTIVITY_LEVEL}                          ║
║                                                           ║
║  {Camp situation narrative. What's happening here.}      ║
║  {Supply state, morale, company condition.}              ║
║  {Recent camp events. What just happened.}               ║
║                                                           ║
║  _____ RECENT ACTIONS _____                               ║
║  • {Event/order outcome 1}                                ║
║  • {Event/order outcome 2}                                ║
║                                                           ║
╠══════════════════════════════════════════════════════════╣
║                                                           ║
║  [Service Records]                                        ║
║  [Quartermaster]               ← Supply ≥ 15%             ║
║  [Personal Retinue]            ← T7+ only                 ║
║  [Companion Assignments]       ← If companions            ║
║  [Medical Attention]           ← If injured/ill           ║
║  [Talk to Lords]               ← If lords nearby          ║
║  [Access Baggage Train]                                   ║
║                                                           ║
╠══════════════════════════════════════════════════════════╣
║            [Back]                                         ║
╚══════════════════════════════════════════════════════════╝
```

**Visit Settlement** appears in DECISIONS menu (not Camp Hub) when at a settlement - it's a contextual opportunity.

**Camp Hub focuses on CAMP, not kingdom.**
- CAMP STATUS: Military rhythm, activity level, camp-specific situation
- RECENT ACTIONS: **Full detail** - order outcomes, event results, reputation changes (last 5 days)
- No kingdom news (that's on Main Menu)
- No Leave Service (only accessible from Muster menu)
- No Reports menu (CAMP STATUS + RECENT ACTIONS replaces it)

**RECENT ACTIONS format:**
```
_____ RECENT ACTIONS _____
• Guard Duty (yesterday)
  Completed without incident. +15 discipline XP.
• Soldier reputation +5 (2 days ago)
  Helped veteran repair armor.
```

Uses existing `GetRecentOrderOutcomes()` and `GetRecentReputationChanges()` from `EnlistedNewsBehavior.cs`.

**This is the deep menu** for browsing, equipment, records, companions. Rank, tier, and day info shown here.

### Settlement Access (Dynamic)

When at a settlement, an additional button appears:

```
╔══════════════════════════════════════════════════════════╗
║  🏠 Visit {SETTLEMENT_NAME}                               ║
║  "Enter the town/castle to trade, recruit, or visit."    ║
╚══════════════════════════════════════════════════════════╝
```

### Dynamic Menu Options

| Option | Visibility Condition | Binding |
|--------|---------------------|---------|
| **Personal Retinue** | Tier 7+ | `@IsT7Plus` |
| **Companion Assignments** | Has companions | `@HasCompanions` |
| **Medical Attention** | Injured or ill | `@NeedsMedical` |
| **Talk to Lords** | Lords in party | `@LordsNearby` |

Note: **Visit Settlement** moved to DECISIONS menu as contextual opportunity (not a Camp Hub button).

---

## Culture-Aware Text

**Critical:** All displayed text must use culture-appropriate rank names.

### Rank Name Resolution

Use `RankHelper.GetRankTitle(culture, tier)` for all rank references:

| Generic Term | Variable | Resolution |
|--------------|----------|------------|
| Sergeant | `{NCO_TITLE}` | Empire: "Principalis", Vlandia: "Sergeant", Sturgia: "Drengr" |
| Captain | `{OFFICER_TITLE}` | Empire: "Centurion", Vlandia: "Knight", Sturgia: "Huskarl" |
| Your Rank | `{RANK_NAME}` | Player's current rank in lord's culture |

### Implementation

All text uses placeholder variables resolved at display time:
- `{NCO_TITLE}` - Resolved via `RankHelper.GetNCOTitle(culture)`
- `{OFFICER_TITLE}` - Resolved via `RankHelper.GetOfficerTitle(culture)`
- `{LORD_NAME}` - Resolved via `EnlistmentBehavior.EnlistedLord.Name`
- `{RANK_NAME}` - Resolved via `RankHelper.GetRankTitle(culture, tier)`

---

## Technical Architecture

### Components

**1. ViewModel Layer**
- `EnlistedCampHubVM` - Main data source and command handler
- `CampStatusVM` - Company status section data
- `RecentActionVM` - Individual action log entry
- Binds to game data, handles button commands, manages state

**2. View Layer (Gauntlet XML)**
- `EnlistedCampHub.xml` - Layout definition with custom widget placement
- Uses native brushes and styles for visual consistency
- Implements VisualDefinition for slide animation
- Custom button grid layouts

**3. Widget Layer (C# Custom Widgets)**
- `EnlistedCampHubWidget` - Root widget handling animations and lifecycle
- Extends base `Widget` class
- Manages slide state transitions
- Optional: custom button widgets if needed

**4. Behavior Integration**
- `EnlistedCampHubBehavior` - Campaign behavior managing hub lifecycle
- Opens/closes hub on hotkey
- Manages screen layer stack
- Handles time control
- Integrates with existing enlisted state

---

## Background System Implementation

### Native GameMenu Background Approach

The native system uses location-aware dynamic backgrounds with a semi-transparent overlay for readability. This provides:
- Settlement/culture-specific visuals
- Day/night tinting (blue at night)
- Smooth transitions between locations
- Professional, polished appearance

### Background Sprite Resolution

Bannerlord automatically selects backgrounds based on context:

**Settlement Types:**
- Towns: Culture-specific town interiors
- Castles: Culture-specific castle halls
- Villages: Village scenes with culture variations

**Field Locations:**
- Default: `wait_guards_stop` (generic outdoor scene)
- Encounter: Context-specific encounter backgrounds

**Culture Variations:**
Each culture has unique background meshes for settlements (Vlandian Gothic architecture vs Aserai Desert aesthetics).

### Implementation Details

#### ViewModel Background Property

```csharp
public class EnlistedCampHubVM : ViewModel
{
    private string _background;
    private bool _isNight;
    
    [DataSourceProperty]
    public string Background
    {
        get => _background;
        set
        {
            if (_background != value)
            {
                _background = value;
                OnPropertyChangedWithValue(value, nameof(Background));
            }
        }
    }
    
    [DataSourceProperty]
    public bool IsNight
    {
        get => _isNight;
        set
        {
            if (_isNight != value)
            {
                _isNight = value;
                OnPropertyChangedWithValue(value, nameof(IsNight));
            }
        }
    }
    
    public void RefreshBackground()
    {
        // Determine current location context
        Settlement currentSettlement = Settlement.CurrentSettlement 
            ?? MobileParty.MainParty.CurrentSettlement;
        
        if (currentSettlement != null)
        {
            // Use settlement's background mesh
            Background = currentSettlement.SettlementComponent.BackgroundMeshName;
        }
        else
        {
            // Field/map default
            Background = "wait_guards_stop";
        }
        
        // Update night status for tinting
        IsNight = Campaign.Current.IsNight;
    }
}
```

#### XML Layout with Background

```xml
<Prefab>
  <Constants>
    <Constant Name="Menu.Width" Value="493" />
    <Constant Name="Menu.Height" Value="891" />
  </Constants>
  
  <VisualDefinitions>
    <VisualDefinition Name="CampHubSlide" EaseType="EaseOut" EaseFunction="Quint" TransitionDuration="0.65">
      <VisualState PositionXOffset="0" State="Extended" />
      <VisualState PositionXOffset="-530" State="Hidden" />
    </VisualDefinition>
  </VisualDefinitions>
  
  <Window>
    <Widget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
      <Children>
        
        <!-- Main sliding panel container -->
        <Widget Id="OverlayContainer" VisualDefinition="CampHubSlide" 
                WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" 
                VerticalAlignment="Center" 
                PositionXOffset="-550">
          <Children>
            
            <!-- Extend/collapse button (optional) -->
            <ButtonWidget Id="ExtendButton" 
                          WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
                          SuggestedHeight="122" SuggestedWidth="58" 
                          Brush="GameMenu.Extend.Button" 
                          VerticalAlignment="Top" 
                          MarginLeft="522">
              <Children>
                <BrushWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
                             SuggestedWidth="30" SuggestedHeight="30" 
                             VerticalAlignment="Center" 
                             MarginBottom="15" MarginLeft="8" 
                             Brush="GameMenu.Extend.Button.Arrow" />
              </Children>
            </ButtonWidget>
            
            <!-- Decorative hinges (native style) -->
            <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
                    SuggestedWidth="35" SuggestedHeight="608" 
                    VerticalAlignment="Center" 
                    PositionXOffset="-10" 
                    Sprite="StdAssets\game_menu_hinges" />
            
            <!-- Main content panel -->
            <EnlistedCampHubWidget Id="MainPanel" 
                                    WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
                                    SuggestedWidth="!Menu.Width" SuggestedHeight="!Menu.Height" 
                                    HorizontalAlignment="Left" 
                                    MarginLeft="27" 
                                    Sprite="@Background"
                                    IsNight="@IsNight"
                                    OverlayContainer="..\..\OverlayContainer"
                                    ExtendButton="..\ExtendButton">
              <Children>
                
                <!-- Semi-transparent dark overlay for readability -->
                <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                        Sprite="SettlementLeftPanelFilter_9" 
                        Color="#00213BFF" 
                        AlphaFactor="0.65" />
                
                <!-- Content container with scrolling -->
                <Widget Id="ContentContainer" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
                  <Children>
                    
                    <ScrollablePanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" 
                                     AutoHideScrollBars="true" 
                                     ClipRect="ClipRect" 
                                     InnerPanel="ClipRect\ContentList">
                      <Children>
                        <Widget Id="ClipRect" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" ClipContents="true">
                          <Children>
                            
                            <ListPanel Id="ContentList" 
                                       WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" 
                                       StackLayout.LayoutMethod="VerticalBottomToTop">
                              <Children>
                                
                                <!-- Title -->
                                <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" 
                                            HorizontalAlignment="Center" 
                                            MarginTop="20" MarginBottom="15"
                                            Text="ENLISTED CAMP" 
                                            Brush="GameMenu.Items.Text.Default" 
                                            Brush.FontSize="30" 
                                            Brush.TextHorizontalAlignment="Center" />
                                
                                <!-- ═══════════════════════════════════════════════════════ -->
                                <!-- PENDING ORDER SECTION - Only visible when order exists -->
                                <!-- ═══════════════════════════════════════════════════════ -->
                                <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                        MarginTop="10" MarginBottom="10" MarginLeft="15" MarginRight="15"
                                        IsVisible="@HasPendingOrder">
                                  <Children>
                                    
                                    <!-- Alert header with NEW badge -->
                                    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                               StackLayout.LayoutMethod="HorizontalLeftToRight"
                                               MarginBottom="5">
                                      <Children>
                                        <TextWidget Text="⚠ PENDING ORDER" 
                                                    Brush="GameMenu.Items.Text.Default" 
                                                    Brush.FontSize="18" />
                                        <TextWidget Text="[NEW]" 
                                                    Brush="GameMenu.Items.Text.Hovered" 
                                                    Brush.FontSize="16"
                                                    MarginLeft="10"
                                                    IsVisible="@IsNewOrder" />
                                      </Children>
                                    </ListPanel>
                                    
                                    <!-- Order card with background -->
                                    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                            Sprite="SettlementLeftPanelFilter_9" 
                                            Color="#1a3d5cFF" AlphaFactor="0.6">
                                      <Children>
                                        <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                                   StackLayout.LayoutMethod="VerticalBottomToTop"
                                                   MarginLeft="10" MarginRight="10" MarginTop="8" MarginBottom="8">
                                          <Children>
                                            
                                            <!-- Issuer + Title -->
                                            <RichTextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                                            Text="@OrderHeaderText"
                                                            Brush="GameMenu.Items.Text.Default" 
                                                            Brush.FontSize="18" />
                                            
                                            <!-- Description snippet -->
                                            <RichTextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                                            Text="@OrderDescription"
                                                            Brush="GameMenu.Items.Text.Default" 
                                                            Brush.FontSize="14"
                                                            MarginTop="5" />
                                            
                                            <!-- Accept/Decline buttons inline -->
                                            <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                                       StackLayout.LayoutMethod="HorizontalCentered"
                                                       MarginTop="10">
                                              <Children>
                                                <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
                                                              SuggestedWidth="100" SuggestedHeight="32"
                                                              MarginRight="10"
                                                              Brush="GameMenu.Button"
                                                              Command.Click="ExecuteAcceptOrder">
                                                  <Children>
                                                    <TextWidget Text="Accept" 
                                                                WidthSizePolicy="StretchToParent"
                                                                HeightSizePolicy="StretchToParent"
                                                                Brush="GameMenu.Items.Text.Default"
                                                                Brush.TextHorizontalAlignment="Center"
                                                                Brush.TextVerticalAlignment="Center" />
                                                  </Children>
                                                </ButtonWidget>
                                                
                                                <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
                                                              SuggestedWidth="100" SuggestedHeight="32"
                                                              Brush="GameMenu.Button"
                                                              Command.Click="ExecuteDeclineOrder">
                                                  <Children>
                                                    <TextWidget Text="Decline" 
                                                                WidthSizePolicy="StretchToParent"
                                                                HeightSizePolicy="StretchToParent"
                                                                Brush="GameMenu.Items.Text.Default"
                                                                Brush.TextHorizontalAlignment="Center"
                                                                Brush.TextVerticalAlignment="Center" />
                                                  </Children>
                                                </ButtonWidget>
                                              </Children>
                                            </ListPanel>
                                            
                                          </Children>
                                        </ListPanel>
                                      </Children>
                                    </Widget>
                                    
                                  </Children>
                                </Widget>
                                
                                <!-- ═══════════════════════════════════════════════════════ -->
                                <!-- VISIT SETTLEMENT - Only when lord is at town/castle    -->
                                <!-- ═══════════════════════════════════════════════════════ -->
                                <ButtonWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                              MarginTop="5" MarginBottom="10" MarginLeft="15" MarginRight="15"
                                              Brush="GameMenu.Button"
                                              IsVisible="@CanVisitSettlement"
                                              Command.Click="ExecuteVisitSettlement">
                                  <Children>
                                    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                               StackLayout.LayoutMethod="HorizontalLeftToRight"
                                               MarginLeft="10" MarginTop="8" MarginBottom="8">
                                      <Children>
                                        <TextWidget Text="🏠" 
                                                    WidthSizePolicy="Fixed" SuggestedWidth="25"
                                                    HeightSizePolicy="CoverChildren"
                                                    Brush="GameMenu.Items.Text.Default" 
                                                    Brush.FontSize="18" />
                                        <TextWidget Text="@VisitSettlementText" 
                                                    WidthSizePolicy="StretchToParent"
                                                    HeightSizePolicy="CoverChildren"
                                                    Brush="GameMenu.Items.Text.Default" 
                                                    Brush.FontSize="18" />
                                      </Children>
                                    </ListPanel>
                                  </Children>
                                </ButtonWidget>
                                
                                <!-- ═══════════════════════════════════════════════════════ -->
                                <!-- HORIZONTAL BUTTON ROW - Main Navigation                -->
                                <!-- ═══════════════════════════════════════════════════════ -->
                                <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" 
                                           HorizontalAlignment="Center"
                                           MarginTop="10" MarginBottom="20"
                                           StackLayout.LayoutMethod="HorizontalLeftToRight">
                                  <Children>
                                    
                                    <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
                                                  SuggestedWidth="110" SuggestedHeight="40"
                                                  MarginLeft="5" MarginRight="5"
                                                  Brush="GameMenuItem.Button"
                                                  Command.Click="ExecuteOrders">
                                      <Children>
                                        <TextWidget Text="[Orders]" 
                                                    WidthSizePolicy="StretchToParent" 
                                                    HeightSizePolicy="StretchToParent"
                                                    Brush="GameMenuItem.Text" />
                                      </Children>
                                    </ButtonWidget>
                                    
                                    <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
                                                  SuggestedWidth="110" SuggestedHeight="40"
                                                  MarginLeft="5" MarginRight="5"
                                                  Brush="GameMenuItem.Button"
                                                  Command.Click="ExecuteDecisions">
                                      <Children>
                                        <TextWidget Text="[Decisions]" 
                                                    WidthSizePolicy="StretchToParent" 
                                                    HeightSizePolicy="StretchToParent"
                                                    Brush="GameMenuItem.Text" />
                                      </Children>
                                    </ButtonWidget>
                                    
                                    <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
                                                  SuggestedWidth="110" SuggestedHeight="40"
                                                  MarginLeft="5" MarginRight="5"
                                                  Brush="GameMenuItem.Button"
                                                  Command.Click="ExecuteReports">
                                      <Children>
                                        <TextWidget Text="[Reports]" 
                                                    WidthSizePolicy="StretchToParent" 
                                                    HeightSizePolicy="StretchToParent"
                                                    Brush="GameMenuItem.Text" />
                                      </Children>
                                    </ButtonWidget>
                                    
                                    <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
                                                  SuggestedWidth="130" SuggestedHeight="40"
                                                  MarginLeft="5" MarginRight="5"
                                                  Brush="GameMenuItem.Button"
                                                  Command.Click="ExecuteQuartermaster">
                                      <Children>
                                        <TextWidget Text="[Quartermaster]" 
                                                    WidthSizePolicy="StretchToParent" 
                                                    HeightSizePolicy="StretchToParent"
                                                    Brush="GameMenuItem.Text" />
                                      </Children>
                                    </ButtonWidget>
                                    
                                  </Children>
                                </ListPanel>
                                
                                <!-- COMPANY REPORT SECTION -->
                                <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                        MarginLeft="20" MarginRight="20" MarginTop="10" MarginBottom="5">
                                  <Children>
                                    <TextWidget Text="COMPANY REPORT" 
                                                WidthSizePolicy="StretchToParent" 
                                                HeightSizePolicy="CoverChildren"
                                                Brush="GameMenu.InfoText" 
                                                Brush.FontSize="22"
                                                Brush.TextHorizontalAlignment="Left" />
                                  </Children>
                                </Widget>
                                
                                <!-- CAMP STATUS -->
                                <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                        MarginLeft="20" MarginRight="20" MarginTop="5" MarginBottom="15">
                                  <Children>
                                    <AutoHideRichTextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                                            Text="@CampStatusText"
                                                            Brush="GameMenu.InfoText" 
                                                            Brush.FontSize="20"
                                                            Brush.TextHorizontalAlignment="Left" />
                                  </Children>
                                </Widget>
                                
                                <!-- DAILY NEWS SECTION -->
                                <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                        MarginLeft="20" MarginRight="20" MarginTop="10" MarginBottom="5">
                                  <Children>
                                    <TextWidget Text="DAILY NEWS" 
                                                WidthSizePolicy="StretchToParent" 
                                                HeightSizePolicy="CoverChildren"
                                                Brush="GameMenu.InfoText" 
                                                Brush.FontSize="22"
                                                Brush.TextHorizontalAlignment="Left" />
                                  </Children>
                                </Widget>
                                
                                <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                        MarginLeft="20" MarginRight="20" MarginTop="5" MarginBottom="15">
                                  <Children>
                                    <AutoHideRichTextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                                            Text="@DailyNewsText"
                                                            Brush="GameMenu.InfoText" 
                                                            Brush.FontSize="20"
                                                            Brush.TextHorizontalAlignment="Left" />
                                  </Children>
                                </Widget>
                                
                                <!-- RECENT ACTIONS SECTION -->
                                <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                        MarginLeft="20" MarginRight="20" MarginTop="10" MarginBottom="5">
                                  <Children>
                                    <TextWidget Text="RECENT ACTIONS" 
                                                WidthSizePolicy="StretchToParent" 
                                                HeightSizePolicy="CoverChildren"
                                                Brush="GameMenu.InfoText" 
                                                Brush.FontSize="22"
                                                Brush.TextHorizontalAlignment="Left" />
                                  </Children>
                                </Widget>
                                
                                <ListPanel DataSource="{RecentActions}" 
                                           WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                           MarginLeft="30" MarginRight="20" MarginBottom="15"
                                           StackLayout.LayoutMethod="VerticalBottomToTop">
                                  <ItemTemplate>
                                    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" MarginTop="3" MarginBottom="3">
                                      <Children>
                                        <AutoHideRichTextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                                                                Text="@ActionText"
                                                                Brush="GameMenu.InfoText" 
                                                                Brush.FontSize="18"
                                                                Brush.TextHorizontalAlignment="Left" />
                                      </Children>
                                    </Widget>
                                  </ItemTemplate>
                                </ListPanel>
                                
                                <!-- VIEW ALL ACTIONS BUTTON -->
                                <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
                                              SuggestedWidth="200" SuggestedHeight="35"
                                              HorizontalAlignment="Center"
                                              MarginBottom="20"
                                              Brush="GameMenuItem.Button"
                                              Command.Click="ExecuteViewAllActions">
                                  <Children>
                                    <TextWidget Text="[View All Actions]"
                                                WidthSizePolicy="StretchToParent" 
                                                HeightSizePolicy="StretchToParent"
                                                Brush="GameMenuItem.Text" />
                                  </Children>
                                </ButtonWidget>
                                
                              </Children>
                            </ListPanel>
                            
                          </Children>
                        </Widget>
                      </Children>
                    </ScrollablePanel>
                    
                  </Children>
                </Widget>
                
                <!-- Decorative frame border -->
                <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" 
                             Brush="Frame1.Border" 
                             IsEnabled="false" />
                
              </Children>
            </EnlistedCampHubWidget>
            
          </Children>
        </Widget>
        
      </Children>
    </Widget>
  </Window>
</Prefab>
```

#### Custom Widget for Animation Control

```csharp
public class EnlistedCampHubWidget : Widget
{
    private bool _isExtended = true;
    private bool _isNight;
    private Widget _overlayContainer;
    private ButtonWidget _extendButton;
    
    public EnlistedCampHubWidget(UIContext context) : base(context)
    {
    }
    
    [Editor(false)]
    public bool IsNight
    {
        get => _isNight;
        set
        {
            if (_isNight != value)
            {
                _isNight = value;
                OnPropertyChanged(value, nameof(IsNight));
            }
        }
    }
    
    [Editor(false)]
    public Widget OverlayContainer
    {
        get => _overlayContainer;
        set
        {
            if (_overlayContainer != value)
            {
                _overlayContainer = value;
                OnPropertyChanged(value, nameof(OverlayContainer));
            }
        }
    }
    
    [Editor(false)]
    public ButtonWidget ExtendButton
    {
        get => _extendButton;
        set
        {
            if (_extendButton != value)
            {
                _extendButton = value;
                OnPropertyChanged(value, nameof(ExtendButton));
                if (_extendButton != null)
                {
                    _extendButton.ClickEventHandlers.Add(OnExtendButtonClick);
                }
            }
        }
    }
    
    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);
        
        // Apply night tinting (same as native GameMenu)
        if (IsNight)
        {
            Color = Color.Lerp(Color, new Color(0.239215687f, 0.4509804f, 0.8f), dt);
        }
        else
        {
            Color = Color.Lerp(Color, Color.White, dt);
        }
    }
    
    private void OnExtendButtonClick(Widget widget)
    {
        _isExtended = !_isExtended;
        UpdateExtendedState();
    }
    
    public void SetExtended(bool extended)
    {
        _isExtended = extended;
        UpdateExtendedState();
    }
    
    private void UpdateExtendedState()
    {
        if (OverlayContainer != null)
        {
            OverlayContainer.SetState(_isExtended ? "Extended" : "Hidden");
        }
        
        // Flip arrow direction if present
        if (ExtendButton != null)
        {
            // Arrow flips to indicate direction
            foreach (Widget child in ExtendButton.Children)
            {
                if (child is BrushWidget brushWidget)
                {
                    foreach (Style style in brushWidget.Brush.Styles)
                    {
                        foreach (StyleLayer layer in style.GetLayers())
                        {
                            layer.HorizontalFlip = !_isExtended;
                        }
                    }
                }
            }
        }
    }
}
```

---

## ViewModel Implementation

### Main Hub ViewModel

```csharp
public class EnlistedCampHubVM : ViewModel
{
    private readonly Action _onClose;
    private string _background;
    private bool _isNight;
    private string _campStatusText;
    private string _dailyNewsText;
    private MBBindingList<RecentActionVM> _recentActions;
    
    // Order state
    private bool _hasPendingOrder;
    private bool _isNewOrder;
    private string _orderHeaderText;
    private string _orderDescription;
    private string _lastSeenOrderId = "";
    
    // Settlement access
    private bool _canVisitSettlement;
    private string _visitSettlementText;
    
    public EnlistedCampHubVM(Action onClose)
    {
        _onClose = onClose;
        _recentActions = new MBBindingList<RecentActionVM>();
        RefreshData();
    }
    
    public override void RefreshValues()
    {
        base.RefreshValues();
        RefreshData();
    }
    
    private void RefreshData()
    {
        RefreshBackground();
        RefreshPendingOrder();
        RefreshSettlementAccess();
        RefreshCampStatus();
        RefreshDailyNews();
        RefreshRecentActions();
    }
    
    #region Pending Order
    
    private void RefreshPendingOrder()
    {
        var currentOrder = OrderManager.Instance?.GetCurrentOrder();
        
        if (currentOrder == null)
        {
            HasPendingOrder = false;
            IsNewOrder = false;
            OrderHeaderText = "";
            OrderDescription = "";
            return;
        }
        
        HasPendingOrder = true;
        
        // Check if this is a new order (different from last seen)
        var currentId = currentOrder.Id ?? "";
        if (!string.Equals(_lastSeenOrderId, currentId, StringComparison.OrdinalIgnoreCase))
        {
            _lastSeenOrderId = currentId;
            IsNewOrder = true;
        }
        else
        {
            // Also mark as new if issued today
            var daysAgo = (int)(CampaignTime.Now - currentOrder.IssuedTime).ToDays;
            IsNewOrder = daysAgo == 0;
        }
        
        // Format header: "From Sergeant: Guard Duty"
        if (!string.IsNullOrEmpty(currentOrder.Issuer))
        {
            OrderHeaderText = $"From {currentOrder.Issuer}: {currentOrder.Title}";
        }
        else
        {
            OrderHeaderText = currentOrder.Title;
        }
        
        // Truncate description if too long
        var desc = currentOrder.Description ?? "";
        OrderDescription = desc.Length > 100 ? desc.Substring(0, 97) + "..." : desc;
    }
    
    public void ExecuteAcceptOrder()
    {
        OrderManager.Instance?.AcceptOrder();
        RefreshPendingOrder();
        RefreshCampStatus(); // Order outcome may affect status
    }
    
    public void ExecuteDeclineOrder()
    {
        OrderManager.Instance?.DeclineOrder();
        RefreshPendingOrder();
    }
    
    [DataSourceProperty]
    public bool HasPendingOrder
    {
        get => _hasPendingOrder;
        set
        {
            if (_hasPendingOrder != value)
            {
                _hasPendingOrder = value;
                OnPropertyChangedWithValue(value, nameof(HasPendingOrder));
            }
        }
    }
    
    [DataSourceProperty]
    public bool IsNewOrder
    {
        get => _isNewOrder;
        set
        {
            if (_isNewOrder != value)
            {
                _isNewOrder = value;
                OnPropertyChangedWithValue(value, nameof(IsNewOrder));
            }
        }
    }
    
    [DataSourceProperty]
    public string OrderHeaderText
    {
        get => _orderHeaderText;
        set
        {
            if (_orderHeaderText != value)
            {
                _orderHeaderText = value;
                OnPropertyChangedWithValue(value, nameof(OrderHeaderText));
            }
        }
    }
    
    [DataSourceProperty]
    public string OrderDescription
    {
        get => _orderDescription;
        set
        {
            if (_orderDescription != value)
            {
                _orderDescription = value;
                OnPropertyChangedWithValue(value, nameof(OrderDescription));
            }
        }
    }
    
    #endregion
    
    #region Settlement Access
    
    private void RefreshSettlementAccess()
    {
        var enlistment = EnlistmentBehavior.Instance;
        var lord = enlistment?.CurrentLord;
        var settlement = lord?.CurrentSettlement;
        
        // Only allow visiting towns and castles
        if (settlement == null || (!settlement.IsTown && !settlement.IsCastle))
        {
            CanVisitSettlement = false;
            VisitSettlementText = "";
            return;
        }
        
        CanVisitSettlement = true;
        VisitSettlementText = $"Visit {settlement.Name}";
    }
    
    public void ExecuteVisitSettlement()
    {
        // Reuse existing settlement visit logic from EnlistedMenuBehavior
        EnlistedMenuBehavior.Instance?.TriggerSettlementVisit();
        _onClose?.Invoke();
    }
    
    [DataSourceProperty]
    public bool CanVisitSettlement
    {
        get => _canVisitSettlement;
        set
        {
            if (_canVisitSettlement != value)
            {
                _canVisitSettlement = value;
                OnPropertyChangedWithValue(value, nameof(CanVisitSettlement));
            }
        }
    }
    
    [DataSourceProperty]
    public string VisitSettlementText
    {
        get => _visitSettlementText;
        set
        {
            if (_visitSettlementText != value)
            {
                _visitSettlementText = value;
                OnPropertyChangedWithValue(value, nameof(VisitSettlementText));
            }
        }
    }
    
    #endregion
    
    private void RefreshBackground()
    {
        Settlement currentSettlement = Settlement.CurrentSettlement 
            ?? MobileParty.MainParty.CurrentSettlement;
        
        if (currentSettlement != null)
        {
            Background = currentSettlement.SettlementComponent.BackgroundMeshName;
        }
        else
        {
            Background = "wait_guards_stop";
        }
        
        IsNight = Campaign.Current.IsNight;
    }
    
    private void RefreshCampStatus()
    {
        var enlistment = EnlistmentBehavior.Instance;
        if (enlistment?.IsEnlisted != true) return;
        
        var status = enlistment.GetCampStatus(); // Your existing status logic
        var settlement = MobileParty.MainParty.CurrentSettlement;
        var settlementName = settlement?.Name?.ToString() ?? "the field";
        
        CampStatusText = $"⚙ CAMP STATUS: {status}\n\n" +
                        $"Your lord holds at {settlementName} with no threats on the horizon. " +
                        $"The camp has settled into routine. Little disturbs the daily rhythm.\n\n" +
                        $"Company: {enlistment.GetCompanySize()} soldiers garrisoned at {settlementName}.\n" +
                        $"Supplies: {enlistment.GetSupplyStatus()} | " +
                        $"Morale: {enlistment.GetMoraleStatus()} | " +
                        $"Rest: {enlistment.GetRestStatus()}";
    }
    
    private void RefreshDailyNews()
    {
        var kingdom = Hero.MainHero.MapFaction?.Kingdom;
        if (kingdom == null)
        {
            DailyNewsText = "No news from the realm.";
            return;
        }
        
        // Generate contextual news based on campaign state
        var newsItems = new List<string>();
        
        // Peace/war status
        if (kingdom.IsAtWarAgainstFaction(/* any faction */))
        {
            newsItems.Add("The kingdom is at war.");
        }
        else
        {
            newsItems.Add($"{kingdom.Name} is at peace with all neighbors.");
        }
        
        // Trade status
        newsItems.Add("Merchant caravans travel the roads freely, and the harvest looks promising.");
        
        // Local lord activities
        var localLord = Settlement.CurrentSettlement?.OwnerClan?.Leader;
        if (localLord != null)
        {
            newsItems.Add($"Lord {localLord.Name} holds court at {Settlement.CurrentSettlement.Name}, " +
                         "settling disputes and hearing petitions.");
        }
        
        DailyNewsText = string.Join(" ", newsItems);
    }
    
    private void RefreshRecentActions()
    {
        RecentActions.Clear();
        
        // Pull from your existing action log system
        var recentLogs = ActionLogManager.GetRecentActions(5); // Last 5 actions
        
        foreach (var log in recentLogs)
        {
            RecentActions.Add(new RecentActionVM(log));
        }
    }
    
    // Command handlers
    public void ExecuteOrders()
    {
        // Navigate to Orders submenu
        GameMenu.SwitchToMenu("enlisted_orders");
        _onClose?.Invoke();
    }
    
    public void ExecuteDecisions()
    {
        // Navigate to Decisions menu
        GameMenu.SwitchToMenu("enlisted_decisions");
        _onClose?.Invoke();
    }
    
    public void ExecuteQuartermaster()
    {
        // Open Quartermaster Gauntlet screen
        QuartermasterManager.Instance?.OpenQuartermasterMenu();
        _onClose?.Invoke();
    }
    
    public void ExecuteViewAllActions()
    {
        // Open full action log
        ActionLogManager.OpenFullLog();
        _onClose?.Invoke();
    }
    
    // Data source properties
    
    [DataSourceProperty]
    public string Background
    {
        get => _background;
        set
        {
            if (_background != value)
            {
                _background = value;
                OnPropertyChangedWithValue(value, nameof(Background));
            }
        }
    }
    
    [DataSourceProperty]
    public bool IsNight
    {
        get => _isNight;
        set
        {
            if (_isNight != value)
            {
                _isNight = value;
                OnPropertyChangedWithValue(value, nameof(IsNight));
            }
        }
    }
    
    [DataSourceProperty]
    public string CampStatusText
    {
        get => _campStatusText;
        set
        {
            if (_campStatusText != value)
            {
                _campStatusText = value;
                OnPropertyChangedWithValue(value, nameof(CampStatusText));
            }
        }
    }
    
    [DataSourceProperty]
    public string DailyNewsText
    {
        get => _dailyNewsText;
        set
        {
            if (_dailyNewsText != value)
            {
                _dailyNewsText = value;
                OnPropertyChangedWithValue(value, nameof(DailyNewsText));
            }
        }
    }
    
    [DataSourceProperty]
    public MBBindingList<RecentActionVM> RecentActions
    {
        get => _recentActions;
        set
        {
            if (_recentActions != value)
            {
                _recentActions = value;
                OnPropertyChangedWithValue(value, nameof(RecentActions));
            }
        }
    }
}
```

### Recent Action ViewModel

```csharp
public class RecentActionVM : ViewModel
{
    private string _actionText;
    
    public RecentActionVM(ActionLogEntry logEntry)
    {
        // Format the log entry for display
        string prefix = logEntry.IsCompleted ? "✓" : "•";
        ActionText = $"{prefix} {logEntry.ActionName}: {logEntry.Result}";
    }
    
    [DataSourceProperty]
    public string ActionText
    {
        get => _actionText;
        set
        {
            if (_actionText != value)
            {
                _actionText = value;
                OnPropertyChangedWithValue(value, nameof(ActionText));
            }
        }
    }
}
```

---

## Behavior Integration

### Campaign Behavior Manager

```csharp
public class EnlistedCampHubBehavior : CampaignBehaviorBase
{
    private GauntletLayer _gauntletLayer;
    private EnlistedCampHubVM _viewModel;
    private IGauntletMovie _movie;
    private bool _isOpen;
    
    public override void RegisterEvents()
    {
        // Register for hourly tick to refresh data while open
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }
    
    public override void SyncData(IDataStore dataStore)
    {
        // No persistent data for UI
    }
    
    private void OnHourlyTick()
    {
        if (_isOpen && _viewModel != null)
        {
            _viewModel.RefreshValues();
        }
    }
    
    public void OpenCampHub()
    {
        if (_isOpen) return;
        
        try
        {
            // Ensure we're in map state
            if (!(Game.Current.GameStateManager.ActiveState is MapState))
            {
                ModLogger.Warning("CampHub", "Cannot open camp hub - not in map state");
                return;
            }
            
            // Create view model
            _viewModel = new EnlistedCampHubVM(CloseCampHub);
            
            // Create Gauntlet layer (same priority as native GameMenu)
            _gauntletLayer = new GauntletLayer(100, "GauntletLayer");
            _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            
            // Load the movie
            _movie = _gauntletLayer.LoadMovie("EnlistedCampHub", _viewModel);
            
            // Add to screen stack
            var mapScreen = ScreenManager.TopScreen;
            if (mapScreen != null)
            {
                mapScreen.AddLayer(_gauntletLayer);
                _gauntletLayer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_gauntletLayer);
            }
            
            // Pause game time
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            
            _isOpen = true;
            ModLogger.Info("CampHub", "Camp hub opened successfully");
        }
        catch (Exception ex)
        {
            ModLogger.Caught("CampHub", "Failed to open camp hub", ex);
            CleanupHub();
        }
    }
    
    public void CloseCampHub()
    {
        if (!_isOpen) return;
        
        try
        {
            // Slide out animation happens automatically via VisualDefinition state change
            // We just need to wait for animation to complete before cleanup
            
            CleanupHub();
            ModLogger.Info("CampHub", "Camp hub closed");
        }
        catch (Exception ex)
        {
            ModLogger.Caught("CampHub", "Error closing camp hub", ex);
            CleanupHub();
        }
    }
    
    private void CleanupHub()
    {
        if (_gauntletLayer != null)
        {
            var mapScreen = ScreenManager.TopScreen;
            if (mapScreen != null)
            {
                _gauntletLayer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_gauntletLayer);
                
                if (_movie != null)
                {
                    _gauntletLayer.ReleaseMovie(_movie);
                    _movie = null;
                }
                
                mapScreen.RemoveLayer(_gauntletLayer);
            }
            
            _gauntletLayer = null;
        }
        
        _viewModel?.OnFinalize();
        _viewModel = null;
        _isOpen = false;
    }
    
    public bool IsOpen => _isOpen;
}
```

### Hotkey Integration

Integrate with existing enlisted menu hotkey system:

```csharp
// In EnlistedMenuBehavior or similar
private void OnMapHotkey()
{
    if (!EnlistmentBehavior.Instance?.IsEnlisted == true) return;
    
    var hubBehavior = Campaign.Current.GetCampaignBehavior<EnlistedCampHubBehavior>();
    if (hubBehavior != null)
    {
        if (hubBehavior.IsOpen)
        {
            hubBehavior.CloseCampHub();
        }
        else
        {
            hubBehavior.OpenCampHub();
        }
    }
}
```

---

## File Structure

### New Files (Custom Hub Only)

```
Enlisted/
├── src/Features/Interface/
│   ├── ViewModels/
│   │   ├── EnlistedCampHubVM.cs          (Main hub view model)
│   │   └── RecentActionVM.cs             (Action log entry)
│   ├── Widgets/
│   │   └── EnlistedCampHubWidget.cs      (Custom widget - optional, see notes)
│   └── Behaviors/
│       └── EnlistedCampHubBehavior.cs    (Lifecycle management)
│
├── GUI/Prefabs/CampHub/
│   └── EnlistedCampHub.xml               (Gauntlet layout)
│
└── Enlisted.csproj                        (Add new files here)
```

### Existing Files (No Changes Needed)

```
src/Features/Interface/Behaviors/
└── EnlistedMenuBehavior.cs               (KEEP AS-IS - registers all native submenus)

ModuleData/Enlisted/Orders/               (KEEP AS-IS - native orders config)
ModuleData/Enlisted/Decisions/            (KEEP AS-IS - native decisions config)
```

The custom hub is isolated to new files only. Existing menu behavior stays untouched.

---

## Integration with Existing Systems

### Architecture: Custom Hub + Native Submenus

```
┌─────────────────────────────────────────────────────────────┐
│                    CUSTOM GAUNTLET                          │
│                    enlisted_status (main hub)               │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  [Orders]  [Decisions]  [Reports]  [Quartermaster]  │   │
│  └─────────────────────────────────────────────────────┘   │
└──────────┬────────────┬────────────┬────────────┬──────────┘
           │            │            │            │
           ▼            ▼            ▼            ▼
    ┌──────────┐ ┌────────────┐ ┌─────────┐ ┌──────────────┐
    │ NATIVE   │ │   NATIVE   │ │ NATIVE  │ │   EXISTING   │
    │ GameMenu │ │  GameMenu  │ │GameMenu │ │   Gauntlet   │
    │ orders   │ │ decisions  │ │ reports │ │ Quartermaster│
    └──────────┘ └────────────┘ └─────────┘ └──────────────┘
```

The custom hub is the ONLY new Gauntlet screen. All submenus use existing native GameMenu code.

### Orders System (Hub Only)

**Current GameMenu Approach (EnlistedMenuBehavior.cs):**
- Accordion header "Orders ▼/▲" that toggles visibility
- Separate row for active order that appears when expanded
- Click navigates to order details menu for Accept/Decline
- Tracks `_ordersCollapsed` and `_ordersLastSeenOrderId` state

**New Gauntlet Hub Approach (main hub only):**
- Order card always visible when order exists (no accordion toggle needed)
- Accept/Decline buttons inline - no menu navigation required
- [NEW] badge shown when order is fresh (same logic as current)
- Cleaner UX with fewer clicks to respond

**What Stays Native:**
- `enlisted_orders` GameMenu - completely unchanged
- All order registration in `EnlistedMenuBehavior` - unchanged

**Migration Notes:**
- `OrderManager.Instance.GetCurrentOrder()` - Same API, no changes
- `OrderManager.Instance.AcceptOrder()` / `DeclineOrder()` - Same API
- The hub displays order summary; clicking "View Details" could still open native orders menu

### Settlement Access (Hub Only)

**Current GameMenu Approach:**
- "Visit {Settlement}" option in menu list
- Condition checks `lord?.CurrentSettlement` for town/castle
- Hidden when not at eligible settlement

**New Gauntlet Hub Approach (main hub only):**
- Same visibility logic via `@CanVisitSettlement` binding
- Prominent button placement above navigation row
- Same underlying `TriggerSettlementVisit()` call
- Opens native town/castle menu - no custom Gauntlet for settlements

### Replace Current GameMenu Call (Entry Point Only)

**Before (current):**
```csharp
GameMenu.ActivateGameMenu("enlisted_status");
```

**After (custom Gauntlet hub):**
```csharp
var hubBehavior = Campaign.Current.GetCampaignBehavior<EnlistedCampHubBehavior>();
hubBehavior?.OpenCampHub();
```

**Submenus still use native calls:**
```csharp
GameMenu.ActivateGameMenu("enlisted_orders");    // No change
GameMenu.ActivateGameMenu("enlisted_decisions"); // No change
GameMenu.ActivateGameMenu("enlisted_reports");   // No change
```

### Navigation to Submenus (Native GameMenu)

From the custom hub, clicking buttons navigates to existing NATIVE GameMenu submenus:

```csharp
// Orders button → Opens native enlisted_orders GameMenu
public void ExecuteOrders()
{
    _onClose?.Invoke(); // Close the Gauntlet hub first
    GameMenu.ActivateGameMenu("enlisted_orders"); // Opens native menu
}

// Decisions button → Opens native enlisted_decisions GameMenu
public void ExecuteDecisions()
{
    _onClose?.Invoke();
    GameMenu.ActivateGameMenu("enlisted_decisions");
}

// Reports button → Opens native enlisted_reports GameMenu  
public void ExecuteReports()
{
    _onClose?.Invoke();
    GameMenu.ActivateGameMenu("enlisted_reports");
}
```

**The existing Orders, Decisions, Reports GameMenu systems remain 100% unchanged.** Only the main hub entry point is replaced with custom Gauntlet. This allows:

1. Incremental development - edit the main hub without breaking submenus
2. Safe fallback - if custom hub has issues, submenus still work
3. Focused scope - only customize what needs customization

### Quartermaster Integration

The Quartermaster button opens your existing Gauntlet equipment screen:

```csharp
public void ExecuteQuartermaster()
{
    QuartermasterManager.Instance?.OpenQuartermasterMenu();
    _onClose?.Invoke();
}
```

---

## Testing & Validation

### Visual Testing

1. **Background Rendering**
   - Verify backgrounds load correctly for all settlement types
   - Test in towns, castles, villages, and field locations
   - Check culture variation (Vlandian, Aserai, Battanian, etc.)
   - Confirm night tinting applies correctly

2. **Layout & Positioning**
   - Horizontal button row displays properly
   - All content sections visible and readable
   - Scrolling works when content exceeds panel height
   - Frame borders and decorations align correctly

3. **Animation**
   - Slide in/out animation smooth and matches native speed
   - Extend button toggles properly
   - No visual glitches or flicker

### Functional Testing

1. **Navigation**
   - Each button opens correct submenu/screen
   - Hotkey opens/closes hub
   - Back navigation from submenus works
   - Quartermaster integration functional

2. **Data Refresh**
   - Status updates when game state changes
   - Daily news reflects current campaign context
   - Recent actions log populates correctly
   - Hourly tick updates data while hub open

3. **State Management**
   - Hub closes properly on all exit paths
   - No memory leaks from ViewModel/layer
   - Time control behaves correctly
   - Multiple open/close cycles work without issues

### Edge Cases

1. **Missing Background**: If settlement has no background mesh, fallback to default
2. **Empty Action Log**: Display placeholder text if no recent actions
3. **Rapid Toggle**: Handle rapid hotkey presses without crashing
4. **Save/Load**: Hub closes cleanly on save, doesn't persist state incorrectly
5. **Game State Changes**: Hub closes gracefully if player enters battle/conversation

---

## Performance Considerations

### Memory Management

- ViewModel cleaned up properly on close (call `OnFinalize()`)
- GauntletLayer released from screen stack
- Movie released from layer
- No lingering event subscriptions

### Refresh Strategy

- Don't refresh every frame - use hourly tick for most data
- Cache background name to avoid repeated lookups
- Only rebuild action log when it actually changes

### Gauntlet Optimization

- Use `AutoHideRichTextWidget` for long text to avoid rendering when not visible
- Limit action log to 5 most recent entries (full log in separate screen)
- Use `ClipContents="true"` on scroll container to prevent overdraw

---

## Migration Path

### Phase 1: Build Custom Hub (Parallel)
- Implement ViewModel, Widget, XML layout
- Add behavior to campaign
- Wire up to separate test hotkey
- Test thoroughly without affecting existing system
- **Submenus remain completely untouched**

### Phase 2: Replace Main Hub Entry Point
- Change 'C' hotkey to open custom Gauntlet hub instead of `enlisted_status` GameMenu
- **All submenus stay as native GameMenu** - no changes needed
- Test navigation from custom hub → native submenus → back
- Verify Orders, Decisions, Reports, Quartermaster all work as before

### Phase 3: Iterate on Main Hub
- Edit custom hub layout, sections, buttons as needed
- Add new features (daily news, recent actions, etc.)
- **Submenus remain stable while we customize the hub**

### Phase 4: Cleanup (Optional)
- Remove old `enlisted_status` GameMenu definition (only after hub is stable)
- Keep all submenu GameMenu registrations exactly as they are

### Why This Approach

The main hub is the only menu that needs custom layout (horizontal buttons, dynamic order cards). The submenus work fine as native GameMenu:

| Menu | Why Native Works |
|------|------------------|
| Orders | Simple vertical list of order options |
| Decisions | Slot-based menu with standard buttons |
| Reports | Text display with back button |
| Status View | Info display with back button |

No reason to rewrite what already works. Custom Gauntlet is only for the main hub where we need layout control.

---

## Acceptance Criteria

### Main Hub (Custom Gauntlet) - Must Have

- [ ] Hub opens with 'C' hotkey when enlisted
- [ ] Horizontal button row displays all 4 main sections
- [ ] Background matches current location (settlement/field)
- [ ] Night tinting applies correctly
- [ ] Slide in/out animation smooth and native-like
- [ ] All 4 buttons navigate to correct native submenus
- [ ] Camp status displays accurate data
- [ ] Daily news shows contextual campaign information
- [ ] Recent actions log shows last 5 actions
- [ ] Hub closes cleanly without memory leaks
- [ ] Time pauses while hub open
- [ ] **Pending order section visible when order exists**
- [ ] **[NEW] badge shows for fresh orders**
- [ ] **Accept/Decline buttons work inline**
- [ ] **Order section hidden when no pending order**

### Submenus (Native GameMenu) - Must Have

- [ ] `enlisted_orders` opens correctly from hub and works as before
- [ ] `enlisted_decisions` opens correctly from hub and works as before
- [ ] `enlisted_reports` opens correctly from hub and works as before
- [ ] Quartermaster opens correctly from hub
- [ ] Back navigation from submenus returns to map (not hub)
- [ ] **No changes to any submenu code or registration**

### Main Hub - Should Have

- [ ] Extend/collapse button functional (optional feature)
- [ ] Decorative hinges and frame borders match native style
- [ ] Scrolling works when content exceeds panel height
- [ ] Hourly updates refresh data while hub open
- [ ] ESC key closes hub
- [ ] Order card has visual distinction (background color)

### Nice to Have

- [ ] Fade-in animation for content sections
- [ ] Hover effects on buttons
- [ ] Sound effects for button clicks
- [ ] Mini-icons next to action log entries
- [ ] Tooltips on main buttons
- [ ] Order description expands on hover
- [ ] Confirmation dialog for Decline action

---

## Future Enhancements

Once the main hub foundation is in place, these become possible (all within the hub only):

1. **Embedded Mini-Grids**: Show companion portraits, equipment overview without leaving hub
2. **Live Stats**: Real-time morale/supply bars that update visually
3. **Quick Actions**: Perform simple actions directly from hub (rest, patrol, etc.)
4. **Notification Badges**: Show counts on buttons (3 new orders, 12 available decisions)
5. **Customizable Layout**: Player preferences for which sections to show/hide
6. **Animations**: Section-specific entrance animations, stat number counters

**Submenus stay native.** If we later need custom layout in a submenu, we'd create a new Gauntlet screen for that specific menu - but current design keeps all submenus as native GameMenu.

---

## Edge Cases & Compatibility Verification

### ✅ VERIFIED: Horizontal Layout Works

Native Bannerlord uses `HorizontalLeftToRight` layout in multiple places:
- `SPConversation.xml` - Conversation button rows
- `InitialScreen.xml` - Main menu buttons
- `PowerLevelComparer.xml` - Power bars
- `OrderTroopItem.xml` - Order UI

**Your horizontal button row will work** - this is a proven pattern.

### ✅ VERIFIED: VisualDefinition Animation System

From `VisualDefinition.cs` (decompiled):
```csharp
public class VisualDefinition
{
    public float TransitionDuration { get; private set; }
    public AnimationInterpolation.Type EaseType { get; private set; }
    public AnimationInterpolation.Function EaseFunction { get; private set; }
    public Dictionary<string, VisualState> VisualStates { get; private set; }
}
```

The animation system is built into core Gauntlet - not GameMenu-specific. Your `SetState()` calls will animate correctly.

### ✅ VERIFIED: Your Existing Gauntlet Patterns Work

Your `QuartermasterEquipmentSelectorBehavior.cs` uses the exact pattern needed:

```csharp
// Layer creation
_gauntletLayer = new GauntletLayer("QuartermasterEquipmentGrid", 1001);

// Movie loading  
_gauntletMovie = _gauntletLayer.LoadMovie("QuartermasterEquipmentGrid", _selectorViewModel);

// Input setup
_gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
_gauntletLayer.InputRestrictions.SetInputRestrictions();

// Layer management
ScreenManager.TopScreen.AddLayer(_gauntletLayer);
_gauntletLayer.IsFocusLayer = true;
ScreenManager.TrySetFocus(_gauntletLayer);
```

**This is identical to what you need for the camp hub.**

### ✅ VERIFIED: Native Brushes Available

From `Modules/Native/GUI/Brushes/GameMenu.xml`:

| Brush Name | Purpose | Status |
|------------|---------|--------|
| `GameMenu.Button` | Button background | ✅ Available |
| `GameMenu.Items.Text.Default` | Button text | ✅ Available |
| `GameMenu.Items.Text.Hovered` | Hover state | ✅ Available |
| `GameMenu.Items.Text.Pressed` | Click state | ✅ Available |
| `GameMenu.Items.Text.Disabled` | Disabled state | ✅ Available |

### ⚠️ EDGE CASE: Custom Widget Registration

**Issue**: If you create a custom widget class (like `EnlistedCampHubWidget`), you MUST register it with the WidgetFactory.

**Your Current Code**: You don't have any custom widgets - all your Quartermaster UI uses native widgets only.

**Solutions**:

1. **Option A (Recommended)**: Don't use a custom widget. Use native `Widget` with manual state management in your behavior:

```csharp
// In your behavior, not a custom widget
public void SetExtended(bool extended)
{
    // Find the overlay container widget and call SetState directly
    // This avoids custom widget registration entirely
}
```

2. **Option B**: Register custom widget in SubModule:

```csharp
protected override void OnSubModuleLoad()
{
    base.OnSubModuleLoad();
    
    // After Gauntlet initializes, register custom widget
    var widgetFactory = UIResourceManager.WidgetFactory;
    widgetFactory.Register(typeof(EnlistedCampHubWidget));
}
```

**Recommendation**: Use Option A - avoid custom widgets entirely. Your Quartermaster system works without them.

### ⚠️ EDGE CASE: State Names Must Match

The VisualDefinition states in XML must match the state names used in `SetState()` calls:

**In XML:**
```xml
<VisualState PositionXOffset="0" State="Extended" />
<VisualState PositionXOffset="-530" State="Hidden" />
```

**In Code:**
```csharp
widget.SetState("Extended");  // Must match exactly
widget.SetState("Hidden");    // Must match exactly
```

If states don't match, the animation won't play and the widget stays in its current position.

### ⚠️ EDGE CASE: Layer Order Conflicts

**Issue**: If your camp hub layer has the same or lower priority than the GameMenu layer, they may conflict.

**Native GameMenu Layer**: Priority `100` (from `GauntletMenuBaseView.cs`)

**Your Quartermaster Layer**: Priority `1001` (higher, so it overlays)

**Recommendation**: Use priority `150-200` for your camp hub - higher than GameMenu (100) but lower than overlay popups.

```csharp
_gauntletLayer = new GauntletLayer("EnlistedCampHub", 150);
```

### ⚠️ EDGE CASE: Force Close on Interruptions

Your Quartermaster behavior correctly handles interruptions:

```csharp
CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnBattleEnd);
CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
```

**Copy this pattern** for your camp hub behavior - critical for preventing stale UI issues.

### ⚠️ EDGE CASE: MapScreen Check

Native menu views check if `ScreenManager.TopScreen is MapScreen` before setting state:

```csharp
if (ScreenManager.TopScreen is MapScreen topScreen)
{
    topScreen.SetIsInRecruitment(true);
}
```

**Your camp hub should also check** before adding layers:

```csharp
public void OpenCampHub()
{
    if (!(ScreenManager.TopScreen is MapScreen))
    {
        ModLogger.Warning("CampHub", "Cannot open - not on map screen");
        return;
    }
    // ... continue with layer creation
}
```

### ✅ VERIFIED: Existing Menu System Can Coexist

Your `EnlistedMenuBehavior` uses `GameMenu.SwitchToMenu()` for submenus. The custom Gauntlet hub can:
1. Open as a Gauntlet layer on top
2. Call `GameMenu.ActivateGameMenu()` or `SwitchToMenu()` to navigate to existing submenus
3. Close the Gauntlet layer before switching

This is the hybrid approach - works correctly.

### ✅ VERIFIED: No Custom Assets Needed

All brush references in the implementation exist in native files:
- `GameMenu.Button` ✅
- `GameMenu.Text` ✅ (use `GameMenu.Items.Text.Default`)
- `Frame1.Border` ✅
- `SettlementLeftPanelFilter_9` sprite ✅
- `StdAssets\game_menu_hinges` sprite ✅

---

## Critical Implementation Notes

### 1. Skip Custom Widget (Simplify)

Instead of creating `EnlistedCampHubWidget`, use the ViewModel to manage state:

```csharp
public class EnlistedCampHubVM : ViewModel
{
    private Widget _overlayWidget; // Set after movie loads
    
    public void SetOverlayWidget(Widget widget)
    {
        _overlayWidget = widget;
    }
    
    public void ExecuteToggleExtended()
    {
        _isExtended = !_isExtended;
        _overlayWidget?.SetState(_isExtended ? "Extended" : "Hidden");
    }
}
```

Get the widget reference after loading the movie:
```csharp
_movie = _gauntletLayer.LoadMovie("EnlistedCampHub", _viewModel);

// Find the overlay widget and pass to viewmodel
var rootWidget = _gauntletLayer.UIContext.Root;
var overlayWidget = rootWidget.FindChild("OverlayContainer");
_viewModel.SetOverlayWidget(overlayWidget);
```

### 2. Brush Name Correction

The implementation uses `GameMenu.Text` but the actual brush name is `GameMenu.Items.Text.Default`:

```xml
<!-- WRONG -->
<TextWidget Brush="GameMenu.Text" />

<!-- CORRECT -->
<TextWidget Brush="GameMenu.Items.Text.Default" />
```

Or for simpler text, use the standard brushes from Standard.xml:
```xml
<RichTextWidget Brush="SPGeneral.MediumText" />
```

### 3. XML File Location

Place the prefab in the correct directory structure:

```
Enlisted/
├── GUI/
│   └── Prefabs/
│       └── CampHub/                    ← New folder
│           └── EnlistedCampHub.xml     ← Your prefab
```

The movie load call must match:
```csharp
_gauntletLayer.LoadMovie("EnlistedCampHub", _viewModel);
// This looks for: GUI/Prefabs/CampHub/EnlistedCampHub.xml
// OR: GUI/Prefabs/EnlistedCampHub.xml
```

---

## References

### Native Files Referenced

- `Modules/Native/GUI/Prefabs/GameMenu/GameMenu.xml` - Layout structure
- `Modules/Native/GUI/Brushes/GameMenu.xml` - Brush definitions
- `TaleWorlds.CampaignSystem.ViewModelCollection/GameMenu/GameMenuVM.cs` - ViewModel patterns
- `TaleWorlds.MountAndBlade.GauntletUI.Widgets/GameMenu/GameMenuWidget.cs` - Widget behavior
- `SandBox.GauntletUI/Menu/GauntletMenuBaseView.cs` - View integration

### Related Documentation

- `docs/Features/UI/ui-systems-master.md` - Overall UI architecture
- `docs/Features/Equipment/quartermaster-system.md` - Example Gauntlet implementation
- `docs/Features/Core/muster-menu-revamp.md` - GameMenu vs Gauntlet comparison

---

## Implementation Estimate

**Total Effort**: 1-2 days

| Task | Effort | Notes |
|------|--------|-------|
| ViewModel (`EnlistedCampHubVM.cs`) | 3-4 hours | Data binding, commands, refresh logic |
| XML Layout (`EnlistedCampHub.xml`) | 4-5 hours | Prefab with all sections, bindings |
| Behavior (`EnlistedCampHubBehavior.cs`) | 2-3 hours | Layer management, lifecycle, hotkey |
| .csproj updates | 15 min | Add `<Compile Include="..."/>` for new files |
| Testing & polish | 3-4 hours | All settlement types, edge cases |

### Prerequisites

- Review [quartermaster-system.md](../Equipment/quartermaster-system.md) for existing Gauntlet patterns
- Review [ui-systems-master.md](ui-systems-master.md) for overall UI architecture
- Verify API against decompile at `C:\Dev\Enlisted\Decompile\` per [BLUEPRINT.md](../../BLUEPRINT.md)

### Files to Create

| File | Path | Add to .csproj |
|------|------|----------------|
| `EnlistedCampHubVM.cs` | `src/Features/Interface/ViewModels/` | Yes |
| `RecentActionVM.cs` | `src/Features/Interface/ViewModels/` | Yes |
| `EnlistedCampHubBehavior.cs` | `src/Features/Interface/Behaviors/` | Yes |
| `EnlistedCampHub.xml` | `GUI/Prefabs/CampHub/` | No (auto-discovered) |

