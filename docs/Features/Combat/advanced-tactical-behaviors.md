# Advanced Tactical Behaviors

**Purpose:** This document describes advanced tactical enhancements to Phase 3 (Formation Intelligence), Phase 9 (Cavalry Cycle Manager), and Phase 11 (Agent Combat AI) that improve upon the base implementation specification.

**Status:** Enhancement specification, ready for implementation after core phases.

---

## Table of Contents

1. [Enhanced Agent Combat AI](#1-enhanced-agent-combat-ai)
2. [Advanced Cavalry Cycling](#2-advanced-cavalry-cycling)
3. [Formation Micro-Positioning](#3-formation-micro-positioning)
4. [Implementation Notes](#4-implementation-notes)

---

## 1. Enhanced Agent Combat AI

### 1.1 Context-Aware Combat Properties

Agents should fight differently based on their formation's objective and the battle phase.

```csharp
[HarmonyPatch(typeof(AgentStatCalculateModel), "SetAiRelatedProperties")]
public class EnlistedAgentAiPatch
{
    private static void Postfix(
        Agent agent,
        ref AgentDrivenProperties agentDrivenProperties,
        WeaponComponentData equippedItem,
        WeaponComponentData secondaryItem)
    {
        // 1. Apply skill-based tuning
        ApplySkillBasedProperties(agent, ref agentDrivenProperties, equippedItem);
        
        // 2. Apply formation objective modifiers
        ApplyObjectiveModifiers(agent, ref agentDrivenProperties);
        
        // 3. Apply battle phase modifiers
        ApplyBattlePhaseModifiers(agent, ref agentDrivenProperties);
    }
    
    private static void ApplySkillBasedProperties(
        Agent agent, 
        ref AgentDrivenProperties props,
        WeaponComponentData equippedItem)
    {
        int relevantSkill = GetRelevantSkillLevel(agent, equippedItem);
        float aiLevel = CalculateAILevel(agent, relevantSkill);
        
        bool hasShield = agent.HasShieldCached;
        
        props.AIBlockOnDecideAbility = MBMath.ClampFloat(aiLevel * 2f, 0.3f, 1f);
        props.AIParryOnDecideAbility = MBMath.ClampFloat(aiLevel * 2.2f, 0.05f, 0.95f);
        props.AiAttackOnDecideChance = MBMath.ClampFloat(aiLevel * 1.2f, 0.1f, 0.8f);
        props.AiDefendWithShieldDecisionChanceValue = hasShield ? 0.25f : 0.01f;
        
        // Ranged tuning
        if (agent.IsRangedCached)
        {
            bool isBow = equippedItem?.WeaponClass == WeaponClass.Bow;
            bool isCrossbow = equippedItem?.WeaponClass == WeaponClass.Crossbow;
            
            float baseError = isBow ? 0.003f : (isCrossbow ? 0.001f : 0.002f);
            props.AiShooterError = baseError * (1f - aiLevel * 0.7f);
            props.AiRangerLeadErrorMin = -0.35f + aiLevel * 0.2f;
            props.AiRangerLeadErrorMax = 0.2f - aiLevel * 0.1f;
        }
    }
    
    private static void ApplyObjectiveModifiers(
        Agent agent, 
        ref AgentDrivenProperties props)
    {
        if (agent.Formation == null) return;
        
        var orchestrator = Mission.Current.GetMissionBehavior<EnlistedBattleBehavior>()?.Orchestrator;
        if (orchestrator == null) return;
        
        var context = orchestrator.GetFormationContext(agent.Formation);
        if (context == null) return;
        
        // Modify behavior based on formation objective
        switch (context.CurrentObjective)
        {
            case FormationObjective.Attack:
            case FormationObjective.Breakthrough:
                // Attacking: more aggressive
                props.AiAttackOnDecideChance *= 1.2f;
                props.AIBlockOnDecideAbility *= 0.9f;
                break;
                
            case FormationObjective.Hold:
                // Holding: more defensive
                props.AiAttackOnDecideChance *= 0.7f;
                props.AIBlockOnDecideAbility *= 1.2f;
                props.AiDefendWithShieldDecisionChanceValue *= 1.3f;
                break;
                
            case FormationObjective.Screen:
                // Screening: cautious
                props.AiAttackOnDecideChance *= 0.8f;
                props.AIBlockOnDecideAbility *= 1.1f;
                break;
                
            case FormationObjective.FightingRetreat:
                // Retreat: very defensive
                props.AiAttackOnDecideChance *= 0.5f;
                props.AIBlockOnDecideAbility *= 1.4f;
                break;
        }
        
        // Main effort gets performance boost
        if (context.Priority == FormationPriority.MainEffort)
        {
            props.AIBlockOnDecideAbility *= 1.1f;
            props.AIParryOnDecideAbility *= 1.1f;
        }
    }
    
    private static void ApplyBattlePhaseModifiers(
        Agent agent,
        ref AgentDrivenProperties props)
    {
        var orchestrator = Mission.Current.GetMissionBehavior<EnlistedBattleBehavior>()?.Orchestrator;
        if (orchestrator == null) return;
        
        var phase = orchestrator.CurrentPhase;
        
        switch (phase)
        {
            case BattlePhase.Crisis:
                // Crisis: desperation improves focus
                props.AiAttackOnDecideChance *= 1.15f;
                props.AIBlockOnDecideAbility *= 1.1f;
                break;
                
            case BattlePhase.Rout:
                // Rout: panic degrades performance
                props.AiAttackOnDecideChance *= 0.6f;
                props.AIBlockOnDecideAbility *= 0.7f;
                props.AIParryOnDecideAbility *= 0.7f;
                break;
                
            case BattlePhase.Pursuit:
                // Pursuit: confidence increases aggression
                props.AiAttackOnDecideChance *= 1.3f;
                props.AIBlockOnDecideAbility *= 0.85f;
                break;
        }
    }
}
```

### 1.2 Bounded Agent Autonomy

Infantry in melee can make micro-tactical decisions while staying within formation bounds.

```csharp
public class AgentPositioningIntelligence
{
    private Agent _agent;
    private Formation _formation;
    private const float MAX_DEVIATION_FROM_FORMATION = 5f;
    
    public WorldPosition CalculatePosition(
        WorldPosition formationOrderPosition,
        FormationContext context)
    {
        // Check if we have tactical autonomy
        if (!HasTacticalAutonomy(context))
        {
            return formationOrderPosition; // Follow formation orders exactly
        }
        
        // Make tactical decision but constrain to formation area
        WorldPosition desired = MakeTacticalDecision();
        return ConstrainToFormationArea(desired, formationOrderPosition);
    }
    
    private bool HasTacticalAutonomy(FormationContext context)
    {
        // Autonomy only for infantry in melee during attack/pin objectives
        if (!_formation.QuerySystem.IsInfantryFormation) return false;
        if (context.Priority == FormationPriority.MainEffort) return false; // Need cohesion
        if (context.CurrentObjective != FormationObjective.Attack &&
            context.CurrentObjective != FormationObjective.Pin) return false;
        if (!IsInMeleeRange(_agent, 15f)) return false;
        
        return true;
    }
    
    private WorldPosition ConstrainToFormationArea(
        WorldPosition desired, 
        WorldPosition formationCenter)
    {
        Vec2 deviation = desired.AsVec2 - formationCenter.AsVec2;
        float distance = deviation.Length;
        
        if (distance <= MAX_DEVIATION_FROM_FORMATION)
            return desired;
        
        // Clamp to boundary
        Vec2 clampedOffset = deviation.Normalized() * MAX_DEVIATION_FROM_FORMATION;
        WorldPosition constrained = formationCenter;
        constrained.SetVec2(formationCenter.AsVec2 + clampedOffset);
        
        return constrained;
    }
}
```

---

## 2. Advanced Cavalry Cycling

### 2.1 Enhanced State Machine

Cavalry should cycle through reserve, positioning, charging, impact, melee, disengagement, rallying, and reforming states.

```csharp
public enum CavalryState
{
    Reserve,        // Held back, waiting for opportunity
    Positioning,    // Moving to charge position
    Charging,       // Accelerating toward enemy
    ChargingPast,   // Passing through enemy formation
    Impact,         // Moment of impact
    Melee,          // Brief melee after impact
    Disengaging,    // Pulling out of melee
    Rallying,       // Gathering scattered units
    Reforming,      // Restoring formation
    Bracing         // Holding position, ready to counter-charge
}

public class EnhancedCavalryBehavior : FormationTacticalBehavior
{
    private CavalryState _state = CavalryState.Reserve;
    private Formation _targetFormation;
    private Timer _chargeTimer;
    private Timer _meleeTimer;
    private Timer _reformTimer;
    
    private const float CHARGE_DURATION = 15f;
    private const float MELEE_DURATION = 5f;
    private const float REFORM_DURATION = 12f;
    
    private void CheckAndChangeState()
    {
        var context = _orchestrator.GetFormationContext(_formation);
        var plan = _orchestrator.CurrentPlan;
        
        switch (_state)
        {
            case CavalryState.Reserve:
                // Orchestrator decides when to release
                if (context.CurrentObjective == FormationObjective.Attack ||
                    context.CurrentObjective == FormationObjective.Flank)
                {
                    _state = CavalryState.Positioning;
                }
                break;
                
            case CavalryState.Charging:
                // Check cohesion
                float deviation = _formation.CachedFormationIntegrityData.DeviationOfPositionsExcludeFarAgents;
                
                if (deviation < 5f) // Tight formation = punch through
                {
                    _state = CavalryState.ChargingPast;
                }
                else if (_chargeTimer != null && _chargeTimer.Check())
                {
                    _state = CavalryState.Impact;
                    _meleeTimer = new Timer(Mission.Current.CurrentTime, MELEE_DURATION, false);
                }
                break;
                
            case CavalryState.Melee:
                if (ShouldDisengage())
                {
                    _state = CavalryState.Disengaging;
                }
                break;
                
            case CavalryState.Reforming:
                float reformDeviation = _formation.CachedFormationIntegrityData.DeviationOfPositionsExcludeFarAgents;
                
                if (_reformTimer.Check() || reformDeviation < 12f)
                {
                    // Reformed, select new target
                    _targetFormation = SelectChargeTarget();
                    _state = _targetFormation != null ? CavalryState.Positioning : CavalryState.Reserve;
                }
                break;
        }
    }
    
    private bool ShouldDisengage()
    {
        // Disengage if scattered, taking casualties, or bogged down
        float deviation = _formation.CachedFormationIntegrityData.DeviationOfPositionsExcludeFarAgents;
        if (deviation > 25f) return true;
        
        float speed = _formation.CachedMovementSpeed;
        if (speed < 2f) return true; // Cavalry needs speed
        
        return false;
    }
}
```

### 2.2 Orchestrator-Controlled Deployment

Cavalry release timing should be synchronized with battle plan.

```csharp
public class CavalryDeploymentDirector
{
    private bool ShouldReleaseCavalry(
        Formation cavalry, 
        BattlePlan plan, 
        BattlePhase phase)
    {
        switch (plan.Type)
        {
            case BattlePlanType.HammerAnvil:
                // Wait for infantry to engage (anvil set)
                var infantry = FindMainInfantryFormation();
                return infantry != null && FormationFightingInMelee(infantry, 0.4f);
                
            case BattlePlanType.LeftHook:
            case BattlePlanType.RightHook:
                // Release when main effort is 30-50m from enemy
                var mainEffort = plan.MainEffortFormation;
                if (mainEffort == null) return false;
                float distance = GetDistanceToClosestEnemy(mainEffort);
                return distance >= 30f && distance <= 50f;
                
            case BattlePlanType.Delay:
                // Only charge if enemy cavalry threatens our infantry
                return IsEnemyCavalryThreatening();
                
            default:
                // Default: charge when battle joined
                return phase == BattlePhase.Engagement;
        }
    }
}
```

### 2.3 Intelligent Target Selection

```csharp
public class CavalryTargetSelector
{
    private float ScoreTarget(Formation target, FormationContext context, BattlePlan plan)
    {
        float score = 0f;
        
        // Formation class priority
        if (target.QuerySystem.IsRangedFormation)
            score += 100f; // Archers are prime targets
        else if (target.QuerySystem.IsInfantryFormation)
            score += 60f;
        else if (target.QuerySystem.IsCavalryFormation)
            score += 30f;
        
        // Distance consideration
        float distance = (_cavalry.CachedAveragePosition - target.CachedAveragePosition).Length;
        if (distance >= 50f && distance <= 80f)
            score += 40f; // Ideal charge distance
        else if (distance < 30f)
            score -= 30f; // Too close
        
        // Plan relevance
        if (plan.Type == BattlePlanType.HammerAnvil && IsEngagedWithInfantry(target))
            score += 70f; // High priority for hammer targets
        
        // Vulnerability
        if (target.QuerySystem.UnderRangedAttackRatio > 0.3f)
            score += 30f; // Already suppressed
        if (target.ArrangementOrder.OrderEnum == ArrangementOrder.ArrangementOrderEnum.Loose)
            score += 25f; // Loose formations more vulnerable
        
        return score;
    }
}
```

---

## 3. Formation Micro-Positioning

### 3.1 Adaptive Timers

Timer durations should adapt to situation rather than being fixed.

```csharp
public class AdaptiveTimingManager
{
    public static float GetChargeDuration(Formation cavalry, Formation target)
    {
        float distance = (cavalry.CachedAveragePosition - target.CachedAveragePosition).Length;
        float speed = cavalry.CachedMovementSpeed;
        float timeToTarget = distance / (speed + 0.001f);
        
        // Duration = time to reach + time to pass through
        return MBMath.ClampFloat(timeToTarget + 5f, 10f, 20f);
    }
    
    public static float GetMeleeDuration(Formation cavalry)
    {
        float duration = 5f;
        
        // If performing well, stay longer
        float killRatio = GetRecentKillRatio(cavalry);
        if (killRatio > 2f)
            duration += 3f;
        else if (killRatio < 0.5f)
            duration -= 2f; // Taking losses, disengage faster
        
        return MBMath.ClampFloat(duration, 3f, 8f);
    }
    
    public static float GetReformDuration(Formation cavalry)
    {
        float duration = 12f;
        float deviation = cavalry.CachedFormationIntegrityData.DeviationOfPositionsExcludeFarAgents;
        
        if (deviation < 10f)
            duration = 8f;  // Already formed
        else if (deviation > 30f)
            duration = 16f; // Very scattered
        
        // Under fire = reform faster
        if (cavalry.QuerySystem.UnderRangedAttackRatio > 0.2f)
            duration *= 0.75f;
        
        return MBMath.ClampFloat(duration, 8f, 16f);
    }
}
```

### 3.2 Formation Cohesion Management

```csharp
public class FormationCohesionManager
{
    public float GetAutonomyRadius(FormationContext context)
    {
        if (context.Priority == FormationPriority.MainEffort)
            return 2f; // Very tight
        
        switch (context.CurrentObjective)
        {
            case FormationObjective.Hold:
            case FormationObjective.Screen:
                return 3f; // Tight
                
            case FormationObjective.Attack:
            case FormationObjective.Pin:
                return 5f; // Moderate
                
            case FormationObjective.FightingRetreat:
                return 8f; // Loose (survival)
                
            default:
                return 4f;
        }
    }
    
    public bool ShouldAllowAgentAutonomy(Agent agent, FormationContext context)
    {
        // Infantry in melee: yes
        if (_formation.QuerySystem.IsInfantryFormation && IsInMeleeRange(agent, 10f))
            return true;
        
        // Cavalry/archers: no (need cohesion)
        return false;
    }
}
```

---

## 4. Implementation Notes

### 4.1 Key APIs

**Formation Integrity:**
```csharp
float deviation = formation.CachedFormationIntegrityData.DeviationOfPositionsExcludeFarAgents;
// < 5f = very tight
// < 12f = good cohesion
// < 20f = acceptable
// > 25f = scattered
```

**Formation Activity Detection:**
```csharp
public static bool IsFormationShooting(Formation formation, float threshold = 0.3f)
{
    int shootingCount = 0;
    float totalMissionTime = MBCommon.GetTotalMissionTime();
    
    formation.ApplyActionOnEachUnitViaBackupList(agent =>
    {
        if (agent.LastRangedAttackTime > 0 && 
            totalMissionTime - agent.LastRangedAttackTime < 10f)
            shootingCount++;
    });
    
    return (float)shootingCount / formation.CountOfUnits >= threshold;
}

public static bool FormationFightingInMelee(Formation formation, float threshold = 0.5f)
{
    int meleeCount = 0;
    float currentTime = MBCommon.GetTotalMissionTime();
    
    formation.ApplyActionOnEachUnitViaBackupList(agent =>
    {
        if (currentTime - agent.LastMeleeAttackTime < 6f ||
            currentTime - agent.LastMeleeHitTime < 6f)
            meleeCount++;
    });
    
    return (float)meleeCount / formation.CountOfUnits >= threshold;
}
```

**Battle Joined Detection (with Hysteresis):**
```csharp
public static bool HasBattleBeenJoined(
    Formation mainInfantry, 
    bool currentlyJoined,
    float joinDistance = 75f)
{
    if (mainInfantry == null) return true;
    if (FormationFightingInMelee(mainInfantry, 0.35f)) return true;
    
    var enemy = FindSignificantEnemy(mainInfantry, true, true, false);
    if (enemy == null) return true;
    
    float distance = mainInfantry.CachedAveragePosition.Distance(enemy.CachedAveragePosition);
    
    // Hysteresis prevents flip-flopping: once joined, need +5m to un-join
    float threshold = joinDistance + (currentlyJoined ? 5f : 0f);
    
    return distance <= threshold;
}
```

### 4.2 Files to Create

**Agent AI:**
- `src/Features/Combat/AgentCombatIntelligence.cs`
- `src/Features/Combat/AgentPositioningIntelligence.cs`
- `src/Features/Combat/FormationCohesionManager.cs`
- `src/Features/Combat/Patches/EnlistedAgentAiPatch.cs`

**Cavalry:**
- `src/Features/Combat/CavalryCycleManager.cs`
- `src/Features/Combat/CavalryDeploymentDirector.cs`
- `src/Features/Combat/CavalryTargetSelector.cs`
- `src/Features/Combat/AdaptiveTimingManager.cs`

**Formation Behaviors:**
- `src/Features/Combat/FormationTacticalBehavior.cs`
- `src/Features/Combat/FormationPositionValidator.cs`
- `src/Features/Combat/TacticalUtilities.cs`

### 4.3 Integration Priority

1. **Phase 1:** Utilities and detection functions (HasBattleBeenJoined, IsFormationShooting, etc.)
2. **Phase 2:** Agent AI patches (objective-aware combat properties)
3. **Phase 3:** Formation tactical behaviors (position validation, threat response)
4. **Phase 4:** Cavalry enhancements (state machine, deployment director)

---

## 5. Edge Cases

### 5.1 Agent Combat AI Edge Cases

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Agent is dead | Skip entirely, check `Agent.State` |
| Agent has no weapon | Use baseline properties, don't error |
| Formation is null | Skip objective modifiers, use skill-based only |
| Orchestrator is null (not enlisted) | Skip all modifiers, native properties remain |
| Context is null | Skip objective modifiers |
| Battle phase unknown | Treat as Engagement (no modifier) |
| Skill level is 0 | Use minimum AI level (0.1) |
| Harmony patch throws exception | Catch, log, continue with native properties |

### 5.2 Cavalry Cycling Edge Cases

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Timer is null when checked | Create new timer with default duration |
| Target formation destroyed mid-charge | Abort charge, transition to Reforming |
| Formation has 0 units mid-cycle | Cancel cycle, formation considered destroyed |
| `DeviationOfPositionsExcludeFarAgents` returns NaN | Treat as very high (50f), trigger reform |
| All cavalry killed during impact | End cycle, no further transitions |
| Target width is 0 | Use default cavalry width |
| Reform timer expires but still scattered | Force transition, accept imperfect formation |
| Enemy cavalry charges while reforming | Interrupt reform, counter-charge |
| No valid targets available | Return null, cavalry enters Reserve |
| Plan is null | Use default cavalry behavior (charge nearest archer) |

### 5.3 Agent Autonomy Edge Cases

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Agent is dead | Skip entirely |
| Agent.Formation is null | Skip (agent not in formation) |
| FormationOrderPosition is invalid | Use agent's current position |
| No enemies nearby | Return to formation position |
| No allies nearby for FindAlly | Fallback to BackStep or Attack |
| Agent is in main effort formation | Early exit, follow formation strictly |
| All decision scores are 0 | Default to Attack |
| Deviation calculation produces negative | Use absolute value |
| Autonomy disabled mid-fight | Smoothly return to formation position |
| Context is null | Deny autonomy (safer) |

### 5.4 Adaptive Timer Edge Cases

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Speed is 0 (not moving) | Use maximum duration (20s for charge) |
| Kill ratio undefined (no kills yet) | Use base duration |
| Deviation is NaN | Use default reform duration (12s) |
| Duration calculation produces negative | Clamp to minimum (3s) |
| Duration calculation produces infinity | Clamp to maximum (20s) |
| Suppression ratio unavailable | Don't apply suppression multiplier |

### 5.5 General Cross-Cutting Edge Cases

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Mission.Current is null | Early exit from all systems |
| Mission ends during tick | Check `Mission.IsEnding` at tick start |
| Formation destroyed mid-tick | Null check before every formation access |
| Team is null | Skip orchestrator operations |
| Float precision issues | Use epsilon comparisons for float equality |
| Performance spike | Cap iterations, defer complex calculations |

---

**Related Documents:**
- `BATTLE-AI-IMPLEMENTATION-SPEC.md` - Main implementation plan
- `tactical-formation-behavior-enhancement.md` - Detailed formation behaviors
- `battle-ai-plan.md` - Design foundation
