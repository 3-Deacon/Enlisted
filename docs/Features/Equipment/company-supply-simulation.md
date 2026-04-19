# Company Supply Simulation System

**Summary:** The company supply system tracks logistical health of the military unit including rations, equipment maintenance materials, ammunition/consumables, and fodder/animal care supplies. Supply levels are consumed through time and combat, and replenished through quartermaster purchases and settlement resupply, creating realistic military logistics gameplay.

**Status:** ⚠️ Mixed (Core implemented, pressure arc events implemented, enhancements planned)  
**Last Updated:** 2026-01-06 (Added supply pressure arc events)
**Related Docs:** [Quartermaster System](quartermaster-system.md), [Provisions & Rations](provisions-rations-system.md), [Company Events](../../Core/company-events.md#pressure-arc-events)

---

## Overview

**Company Supply** represents the overall logistical health of your military unit, tracking:
- Rations and food supplies (provided directly as part of enlisted service)
- Equipment maintenance materials (repair supplies, oil, leather, whetstone)
- Ammunition and consumables (arrows, bolts, bandages, medicine)
- Fodder and animal care supplies (for horses and pack animals)
- Camp supplies (tents, rope, tools, firewood)

**Tracked Value:** `CompanyNeedsState.Supplies` (0-100%) — backed by `CompanySupplyManager`

**Current Impact (Implemented):**
- < 30% = Cannot access equipment changes (quartermaster menu blocked)
- < 50% = Warning logged to mod console
- Warning thresholds trigger log messages only (no in-game popups yet)

**Planned Impact (Not Yet Implemented):**
- < 50% = UI warning messages, morale penalties
- < 20% = Critical - Scrutiny increases, troops desert
- < 10% = Automatic desertion events

---

## CRITICAL: Company Scope

Company Supply is scoped to the **Company you are enlisted in** (i.e., the **enlisted lord's party**), not to the full army.

- The player receives rations and supplies as part of their enlisted service directly
- Supply tracking uses party size and activity from the enlisted lord's party for consumption calculations
- **Battle casualties** are counted ONLY from the lord's party, not from all army parties
- If the enlisted lord is attached to an army, supply calculations still use the enlisted lord's party metrics (not army-wide values)

**Bug Fix (Jan 2026):** Previously, battle casualties were incorrectly counted from all army parties, causing supply to drop from 86% to 0% in large battles with 1000+ army-wide casualties. Now correctly scoped to only the lord's party.

---

## Supply Consumption (Daily)

The supply system simulates consumption of all logistics including:
- Rations and food supplies
- Ammunition (arrows, bolts)
- Maintenance supplies (oil, whetstone, leather)
- Medical supplies (bandages, medicine)
- Camp gear (rope, tools, tents)
- Animal supplies (horseshoes, fodder supplements)

**Base Consumption Rate:** 1.5% per day

**Company Size Modifier:**
```csharp
int companySize = GetLordPartySize(); // Lord's total party size
float sizeMultiplier = companySize / 100.0f;

// Example:
// 50 troops = 0.5x consumption (0.75% per day)
// 100 troops = 1.0x consumption (1.5% per day)
// 200 troops = 2.0x consumption (3% per day)
```

**Activity Multiplier:**

| Activity | Multiplier | Daily Consumption (100 troops) |
|----------|------------|--------------------------------|
| **Resting in settlement** | 0.3x | 0.45% per day |
| **Traveling (normal)** | 1.0x | 1.5% per day |
| **Patrolling** | 1.2x | 1.8% per day |
| **Raiding** | 1.5x | 2.25% per day |
| **Besieging** | 2.5x | 3.75% per day *(siege equipment use)* |
| **Being besieged** | 1.8x | 2.7% per day |
| **Battle** | Special | See "Combat Losses" section |

**Terrain Multiplier:**

| Terrain | Multiplier | Reason |
|---------|------------|--------|
| **Plains/Forest** | 1.0x | Standard wear |
| **Desert** | 1.2x | Sand damages equipment |
| **Mountains** | 1.3x | Rough terrain, rockslides |
| **Snow** | 1.4x | Fuel for fires, equipment brittleness |
| **Sea (naval)** | 1.1x | Salt corrosion |

**Total Supply Formula:**
```
Total Supply % (0-100%)

Supply reduces by:
  (Base Rate 1.5% × Size Mult × Activity Mult × Terrain Mult) per day
```

---

## Supply Losses (Events)

### **Combat Losses:**

**After Battle:**
```csharp
public int CalculateBattleSupplyLoss(int troopsLost, bool playerVictory)
{
    // IMPORTANT: troopsLost is ONLY casualties from the lord's party, not army-wide
    // Bug fix (Jan 2026): Previously counted all army casualties, causing excessive losses
    
    // Base loss from casualties
    int casualtyLoss = troopsLost / 10; // 10 casualties = -1% supply
    
    // Defeat penalty (abandoned supplies)
    int defeatPenalty = playerVictory ? 0 : 5;
    
    // Siege equipment usage
    int siegeLoss = IsSiegeBattle() ? 3 : 0;
    
    return casualtyLoss + defeatPenalty + siegeLoss;
}
```

**Examples:**
- Victory with 10 casualties (lord's party): -1% supply
- Victory with 30 casualties (lord's party): -3% supply
- Defeat with 20 casualties (lord's party): -7% supply (2% + 5% abandoned)
- Siege assault victory: -8% supply (5% casualties + 3% siege equipment)

**Note:** Casualties are counted ONLY from the enlisted lord's party, not from the entire army. This ensures supply losses are scoped to the Company you're serving in, not the broader military coalition.

### **Special Events (Planned - Not Yet Implemented):**

These events are designed but not yet created in the content system:

| Event | Supply Loss | Description |
|-------|-------------|-------------|
| **Supply wagon raided** | -15% | Enemy raiders hit your supply train |
| **Spoilage (summer)** | -5% | Heat spoils/ruins stored non-food supplies (bandages, medicines, oils, etc.) |
| **Flooded supplies** | -10% | River crossing gone wrong |
| **Fire in camp** | -8% | Supplies burned |
| **Deserters steal supplies** | -3% per deserter | Internal theft |
| **Lord requisitions supplies** | -10% | Lord takes supplies for main army |

---

## Supply Gains (Replenishment)

### **1. Automatic Resupply**

When the player is with the enlisted lord and the Company is in a settlement:

**Hourly Resupply (New):**
- Resupply now happens **every in-game hour** instead of once per day
- Allows partial-day settlement visits to provide meaningful supply gains (lords rarely stay 24 hours)
- Base rate: ~2.08% per hour (50% / 24 hours) in towns/castles
- With bonuses: ~2.92% per hour (70% / 24 hours) maximum

**Player Messaging:**
- When the lord **leaves** a settlement after a meaningful resupply (≥5% gained), a flavor message appears
- Messages are RP-appropriate with no percentages shown:
  - Minor (5-15%): "Took on some supplies in {settlement}."
  - Good (15-30%): "Supplies replenished in {settlement}."
  - Major (30%+): "The company restocked well in {settlement}. Stores are replenished."
- Quartermaster dialogue also mentions active resupply when asked about supplies while in a settlement

```csharp
public void HourlyResupply()
{
    // Resupply is computed for the Company (enlisted lord's party),
    // regardless of whether the lord is currently attached to an army.
    if (IsWithEnlistedLordPartyInSettlement())
    {
        // Settlement resupply: +50% per day = ~2.08% per hour in town/castle
        float dailyResupply = 50.0f;
        
        // Wealthy settlements give more (better workshops/smiths)
        if (CurrentSettlement.Prosperity > 5000)
            dailyResupply += 10.0f;  // +10% per day = ~0.42% per hour
        
        // Modifier based on settlement relation
        if (CurrentSettlement.OwnerClan == PlayerLord.Clan)
            dailyResupply += 10.0f;  // +10% per day = ~0.42% per hour (friendly territory)
        
        // Convert to hourly rate
        float hourlyResupply = dailyResupply / 24.0f;
        
        // Replenish supplies (max 100%)
        AddSupplies(hourlyResupply);
    }
}
```

**Settlement Resupply Rates:**

| Settlement Type | Per Hour | Per Full Day (24h) | Notes |
|-----------------|----------|-------------------|-------|
| **Standard town/castle** | ~2.08% | 50% | Base resupply rate |
| **Wealthy settlement** (Prosperity > 5000) | ~2.50% | 60% | Better workshops/smiths |
| **Owned settlement** (Your lord's clan) | ~2.50% | 60% | Friendly territory access |
| **Wealthy + Owned** | ~2.92% | 70% | Maximum resupply rate |

**Examples:**
- **3 hours in town:** ~6.2% supply gained (50%/24 × 3 = 6.25%)
- **6 hours in wealthy town:** ~15% supply gained (60%/24 × 6 = 15%)
- **12 hours in owned castle:** ~30% supply gained (60%/24 × 12 = 30%)
- **24 hours (full day) in standard town:** 50% supply gained
- **24 hours (full day) in wealthy owned town:** 70% supply gained

### **2. Quartermaster Duties (Planned - Not Yet Connected)**

The `AddSupplies()` method exists but no duties currently call it. When connected:

| Duty/Activity | Supply Gain | Notes |
|---------------|-------------|-------|
| **Foraging Duty** | +2 to +5% | Gather rations and supplies from the field |
| **Inventory Management** | +2% | Organize existing supplies, reduce waste |
| **Equipment Maintenance** | +3% | Repair/salvage gear, reclaim materials |
| **Negotiate with Merchants** | +5 to +10% | Buy supplies and rations (costs gold) |
| **Hunt Wildlife** | +1 to +3% | Supplement rations with game meat |

**Implementation Note:** The API is ready (`CompanySupplyManager.Instance.AddSupplies(amount, "source")`) but duty handlers need to be updated to call it.

### **3. Supply Wagon Events (Planned - Not Yet Implemented)**

**Supply Caravan Arrival (Design):**
```
Every 7-14 days (random), if lord is with army:
  Supply wagon arrives: +20% supply
  
  BUT: 10% chance wagon is raided en route
    If raided: +0% supply
```

### **4. Post-Battle Looting**

```csharp
public int CalculateSupplyLoot(int enemyTroopsKilled, string enemyFaction)
{
    // Base loot: rations, arrows, damaged gear, materials
    int baseLoot = enemyTroopsKilled / 25; // 25 kills = +1% supply
    
    // Faction modifier (equipment quality affects salvage)
    float factionMultiplier = enemyFaction switch
    {
        "empire" => 1.3f,     // Professional army, good equipment
        "vlandia" => 1.2f,    // Knights, quality gear
        "sturgia" => 1.0f,    // Standard equipment
        "battania" => 0.9f,   // Woodland gear, less metal
        "khuzait" => 0.7f,    // Light cavalry, minimal armor
        "aserai" => 1.0f,     // Desert troops, standard gear
        "looters" => 0.5f,    // Peasants, junk
        "bandits" => 0.6f,    // Poorly equipped
        _ => 1.0f
    };
    
    return (int)(baseLoot * factionMultiplier);
}
```

**Examples:**
- Defeat 100 Empire troops: +5% supply (4% base × 1.3)
- Defeat 75 Khuzait troops: +2% supply (3% base × 0.7)
- Defeat bandit hideout (50 bandits): +1% supply (2% base × 0.6)

### **5. Player Purchases (Partial - Needs Completion)**

**Current State:** A basic supply purchase stub exists in `QuartermasterManager.OnSupplyPurchaseSelected()` that costs 50g and adds grain + tools to the player's roster. However, it does NOT call `AddSupplies()` to update the supply percentage.

**Planned Full System:**
```
Visit town merchant:
  [Buy Supplies] → Opens purchase menu
  
  Supply Packages:
    Small Pack (+ 5% supply): 150g
    Medium Pack (+10% supply): 280g
    Large Pack (+20% supply): 500g
    
  Price varies by:
    - Town prosperity (higher = cheaper)
    - Player Trade skill (higher = cheaper)
    - War/peace (war = 50% markup)
```

**To Complete:** Update `OnSupplyPurchaseSelected()` to call `CompanySupplyManager.Instance?.AddSupplies(amount, "purchase")` after adding items.

---

## Supply Management Strategies

### **Peacetime (Easy):**
```
Daily consumption: -2%
Settlement resupply: +5%
Net: +3% per day (stable, plenty of supply)

Player rarely needs to worry about supply.
```

### **Active Campaign (Moderate):**
```
Daily consumption: -2.4% (patrolling)
Settlement resupply: +5% (every 3-4 days in town)
Battle losses: -2% per battle
Net: Slowly declining, need occasional foraging

Player must do foraging duties every 10-15 days.
```

### **Siege/Long Campaign (Hard):**
```
Daily consumption: -4% (siege)
No settlement resupply (in field)
Battle losses: -8% (siege assault)
Net: Rapidly declining

Player must:
  - Do foraging duties frequently (+8%)
  - Buy supplies from nearby towns (+20% for 500g)
  - Win battles to loot enemy supplies (+3-6%)
  - Risk starvation if siege lasts >20 days
```

---

## Supply Pressure Arc Events

**Status:** ✅ Implemented

**File:** `ModuleData/Enlisted/Events/pressure_arc_events.json`

### Pressure Tracking

The `CompanySimulationBehavior` tracks consecutive days of low supplies (< 50%) and fires narrative events at specific thresholds:

```csharp
// In CompanySimulationBehavior.cs
private CompanyPressure _companyPressure;

// Daily tracking
if (_supplies < 50)
    _companyPressure.DaysLowSupplies++;
else
    _companyPressure.DaysLowSupplies = 0;

// Event triggering
CheckPressureArcEvents();
```

### Event Thresholds

| Days Low | Stage | Event IDs | Description |
|----------|-------|-----------|-------------|
| Day 3 | Stage 1 | `supply_pressure_stage_1_*` | Thin rations, grumbling, warnings |
| Day 5 | Stage 2 | `supply_pressure_stage_2_*` | Fights, discipline breakdown |
| Day 7 | Crisis | `supply_crisis_*` | Whispers of desertion, actual desertions |

**Tier Variants:** Each stage has 3 events (`_grunt`, `_nco`, `_cmd`) scaled to player tier (T1-T4, T5-T6, T7+).

### Equipment Gating

**30% Supply Threshold:**
- Equipment menu blocked in Quartermaster
- "Master at Arms isn't seeing anyone right now" message
- Forces player to address supply crisis before upgrading gear

**See Also:** [Company Events - Pressure Arcs](../../Core/company-events.md#pressure-arc-events)

---

## Technical Implementation: Supply System

### **Core Architecture:**

```csharp
public class CompanySupplyManager
{
    // Total supply level (0-100%)
    private float _totalSupply = 100.0f;
    
    // Public property returns total (0-100%)
    public int TotalSupply
    {
        get
        {
            return (int)Math.Clamp(_totalSupply, 0f, 100f);
        }
    }
    
    /// <summary>
    /// Updates supplies based on activity
    /// Called on daily tick
    /// </summary>
    public void DailyUpdate()
    {
        float consumption = CalculateSupplyConsumption();
        float resupply = CalculateSupplyResupply();
        
        _totalSupply = Math.Clamp(_totalSupply - consumption + resupply, 0f, 100f);
    }
    
    /// <summary>
    /// Simulates supply consumption (rations, ammo, equipment maintenance, etc.)
    /// </summary>
    private float CalculateSupplyConsumption()
    {
        MobileParty lordParty = GetPlayerLordParty();
        if (lordParty == null) return 0;
        
        // Base rate: 1.5% per day
        float baseRate = 1.5f;
        
        // Size modifier
        float sizeMultiplier = lordParty.Party.NumberOfAllMembers / 100.0f;
        
        // Activity modifier
        float activityMult = GetActivityMultiplier(lordParty);
        
        // Terrain modifier
        float terrainMult = GetTerrainMultiplier(lordParty);
        
        return baseRate * sizeMultiplier * activityMult * terrainMult;
    }
    
    /// <summary>
    /// Calculates resupply (settlement only)
    /// </summary>
    private float CalculateSupplyResupply()
    {
        MobileParty lordParty = GetPlayerLordParty();
        if (lordParty == null || lordParty.CurrentSettlement == null) return 0;
        
        Settlement settlement = lordParty.CurrentSettlement;
        if (!settlement.IsTown && !settlement.IsCastle) return 0;
        
        float resupply = 3.0f; // Base rate
        
        if (settlement.Prosperity > 5000) resupply += 1.0f;
        if (settlement.OwnerClan == lordParty.LeaderHero?.Clan) resupply += 1.0f;
        
        return resupply;
    }
    
    /// <summary>
    /// Player action: Manual supply gain from duties, purchases, etc.
    /// </summary>
    public void AddSupplies(float amount, string source)
    {
        _totalSupply = Math.Clamp(_totalSupply + amount, 0f, 100f);
    }
}
```

### **Key Principles:**

1. **Single Supply Metric:** All logistics (rations, ammo, repairs, camp supplies) tracked as one value
2. **Player Receives Rations:** Food/rations are part of enlisted service, not shared from lord's party
3. **Activity-Based Consumption:** Supply drains faster during combat, sieges, harsh terrain
4. **Settlement Resupply:** Automatic +3-5% per day when in friendly towns/castles

---

## Integration with Existing Systems

### **1. Company Needs System (Implemented)**

Supply is tracked via `CompanyNeedsState.Supplies` which delegates to `CompanySupplyManager`:

```csharp
// Actual implementation in CompanyNeedsState.cs
public class CompanyNeedsState
{
    public int Supplies
    {
        get => CompanySupplyManager.Instance?.TotalSupply ?? _suppliesFallback;
        set => _suppliesFallback = (int)MathF.Clamp(value, 0, 100);
    }
    
    private int _suppliesFallback = 60;
}

// Daily tick is called from EnlistmentBehavior.OnDailyTick():
CompanySupplyManager.Instance?.DailyUpdate();

// Equipment blocking is in QuartermasterManager:
var supplyLevel = enlistment.CompanyNeeds?.Supplies ?? 100;
if (supplyLevel < 30)
{
    // Block equipment menu with message
}
```

**Not Yet Connected:** Foraging duties don't call `AddNonFoodSupplies()` - this is the main gap.

### **2. Schedule Activities (Planned - Not Yet Connected)**

These integrations are designed but not implemented:

| Activity | Supply Effect |
|----------|---------------|
| **Foraging Duty** | +5% on success |
| **Quartermaster Inventory** | Prevents -1% waste |
| **Work Detail** | +1% (organize supplies) |
| **Rest** | -0.5% (lower consumption) |

### **3. Quartermaster Relationship (Planned - Not Yet Connected)**

QM relationship bonuses are designed but not integrated with `CompanySupplyManager`:

| QM Rep | Supply Bonus |
|--------|--------------|
| **65+** | -10% consumption (efficient management) |
| **35-65** | -5% consumption |
| **0-35** | Normal consumption |
| **<0** | +10% consumption (wasteful, sabotage) |

### **4. Duty System (Planned - Not Yet Connected)**

Quartermaster duty bonuses are designed but not implemented:

```
If player has "quartermaster" duty:
  - Daily consumption: -20%
  - Foraging gains: +50%
  - Can negotiate better supply prices (-20% cost)
```

---

## Implementation Checklist

### **Core System (Implemented Dec 2025)**

- [x] `CompanySupplyManager` class with unified supply model (`src/Features/Logistics/CompanySupplyManager.cs`)
- [x] `TotalSupply` property (0-100%) tracking all logistics including rations
- [x] `CalculateSupplyConsumption()` with 1.5% base rate
- [x] Size multiplier (partySize / 100)
- [x] Activity multiplier (siege=2.5x, settlement=0.3x, patrol=1.2x, etc.)
- [x] Terrain multiplier (desert=1.2x, mountain=1.3x, snow=1.4x, water=1.1x)
- [x] `CalculateSupplyResupply()` - settlement-based resupply (+3-5% per day)
- [x] `DailyUpdate()` called from `EnlistmentBehavior.OnDailyTick`
- [x] `ProcessBattleSupplyChanges()` - casualties, defeat penalty, siege penalty, loot
- [x] Warning logging at 50% and 30% thresholds via `ModLogger`
- [x] Equipment menu blocked at <30% supply (in `QuartermasterManager`)
- [x] Save/load supply value (serialization in EnlistmentBehavior)
- [x] Initialize/Shutdown lifecycle (on enlist/discharge)
- [x] `AddSupplies(float, string)` API ready for duty integration
- [x] Edge cases: lord captured, on leave, grace period re-enlistment

### **Partial/Stub Implementations**

- [~] Supply purchase option exists (`OnSupplyPurchaseSelected`) but doesn't call `AddSupplies()`

### **Future Enhancements (Not Yet Implemented)**

- [ ] Display supply % in main menu UI
- [ ] Connect foraging duty to `AddSupplies()` (+3-8%)
- [ ] Complete supply purchase system (call `AddSupplies()`)
- [ ] Add supply crisis events to JSON (`evt_supply_*`)
- [ ] Add special loss events (raid, fire, spoilage)
- [ ] Track deserter supply theft
- [ ] Add automatic desertion at <10%
- [ ] Connect to QM rep system (efficiency bonus)
- [ ] Connect to quartermaster duty (consumption reduction)
- [ ] Add supply caravan events (every 7-14 days)
- [ ] UI warning popups (not just logging)

---

## Example Supply Lifecycle

### **Week 1: Peacetime**
```
Day 1: Start at 100% supply
  - Full rations and supplies from enlistment
  
Day 2: In settlement
  - Supply: 100% - 0.45% consumption + 3% resupply = 100% (stays capped)
  - Total: 100%
  
Day 3-7: Stay at 100% (resupply > consumption)

Result: No supply concerns in peacetime
```

### **Week 2: Campaign Begins**
```
Day 8: Leave settlement, begin patrol
  - Supply: 100% - 1.8% (patrol rate) = 98.2%
  - Total: 98.2%
  
Day 9: Battle against 80 Empire troops
  - Supply: 98.2% - 1.8% travel - 4% battle loss + 4% loot = 96.4%
  - Total: 96.4%
  
Day 10-11: Travel (no resupply opportunities)
  - Supply: 96.4% - 3% = 93.4%
  - Total: 93.4%
  
Day 12: Foraging duty
  - Supply: 93.4% - 1.5% travel + 4% foraging = 95.9%
  - Total: 95.9%
  
Day 13-14: Return to settlement
  - Supply: 95.9% - 0.9% (two days at 0.45%) + 6% resupply = 100% (capped)
  - Total: 100%

Result: Campaign pressure manageable with occasional foraging
```

### **Week 3: Siege (Hard Mode)**
```
Day 15: Begin siege
  - Supply: 100% - 3.75% (siege rate, 100 troops) = 96.25%
  - Total: 96.25%
  
Day 16-18: Siege continues (no resupply)
  - Supply: 96.25% - 11.25% (3 days) = 85%
  - Total: 85%
  
Day 19: Assault battle (casualties)
  - Supply: 85% - 3.75% daily - 6% battle loss = 75.25%
  - Total: 75.25%
  
Day 20: Player does equipment maintenance duty
  - Supply: 75.25% - 3.75% + 3% maintenance = 74.5%
  - Total: 74.5%
  
Day 21-23: Continue siege (CRITICAL)
  - Supply: 74.5% - 11.25% = 63.25%
  - Total: 63.25%
  
Day 24: Player negotiates with nearby town merchant (+500g spent)
  - Supply: 63.25% - 3.75% + 10% purchase = 69.5%
  - Total: 69.5%
  
Day 25: Siege victory! Loot supplies, enter town
  - Supply: 69.5% + 5% loot + 5% town resupply = 79.5%
  - Total: 79.5%
  
Day 26-30: Recover in town
  - Supply: 79.5% + 15% (3 days × 5%) - 2.25% consumption = 92.25%
  - Total: 92.25%

Result: Intense siege pressure, realistic logistics, required active management
```

---

## Summary: Why This Approach Works

### **Integrated Supply Model (Implemented):**
✅ Single unified supply metric (0-100%) tracks all logistics including rations  
✅ Player receives rations as part of enlisted service (no food sharing from lord)  
✅ Equipment gate is based on overall supply health  
⏳ Player agency via duties (API ready, needs connection)  

### **Mod Compatibility (Implemented):**
✅ No modifications to native `MobileParty` food properties  
✅ No interference with other mods' food/party systems  
✅ All changes are additive (our own systems)  
✅ Player's food needs handled independently from vanilla  

### **Gameplay Benefits (Partial):**
✅ Realistic campaign pressure (supplies deplete during extended operations)  
✅ Meaningful equipment gate (tied to overall logistics)  
✅ Settlement resupply provides strategic value  
⏳ Player agency via duties (planned, not connected)  

### **Implementation Simplicity (Implemented):**
✅ One manager class (`CompanySupplyManager`)  
✅ Single supply metric (0-100%) for all logistics  
✅ Clean separation of concerns  
✅ Easy to debug and balance  

---

**Status**: Core implementation complete (December 2025). Player-facing features need connection.

**What Works Now:**
1. ✅ `CompanySupplyManager` class with unified supply model
2. ✅ Daily consumption based on activity, terrain, party size
3. ✅ Settlement auto-resupply (+3-5% per day)
4. ✅ Battle supply changes (losses and loot)
5. ✅ Equipment menu blocked at <30% supply
6. ✅ Save/load persistence
7. ✅ Warning logging at thresholds

**Main Gaps:**
1. ⏳ No duties call `AddSupplies()` - foraging duty needs connection
2. ⏳ Supply purchase stub exists but doesn't update supply %
3. ⏳ No supply crisis events in JSON (event IDs are placeholders)
4. ⏳ No UI display of supply percentage

**Next Steps:**
1. Connect foraging/quartermaster duties to `AddSupplies()`
2. Fix `OnSupplyPurchaseSelected()` to call `AddSupplies()`
3. Create supply crisis events (`evt_supply_*`) in JSON
4. Add supply % to Camp Hub status display
5. Balance consumption/resupply rates based on playtesting

