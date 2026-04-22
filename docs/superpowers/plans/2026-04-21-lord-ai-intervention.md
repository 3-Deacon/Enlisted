# Lord AI Intervention Implementation Plan (Plan 2 of 5)

> **Status:** Ready to start. Plan 1 (Campaign Intelligence Backbone) shipped 2026-04-22 — the `EnlistedCampaignIntelligenceBehavior.Current` accessor this plan consumes is live.

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Integration context:** This is Plan 2 in the five-plan integration roadmap — see [docs/superpowers/specs/2026-04-21-plans-integration-design.md](../specs/2026-04-21-plans-integration-design.md) for the full sequence (Plan 1: Campaign Intelligence Backbone; Plan 3: Signal Projection; Plan 4: Duty Opportunities; Plan 5: Career Loop Closure). Plan 2 is one of three plans that consume Plan 1's `EnlistedCampaignIntelligenceBehavior.Current` accessor. It runs in parallel with Plans 3 and 4 after Plan 1 signs off — there is no shared code surface with Plan 3 or Plan 4 beyond Plan 1's read-only accessor and additive csproj / `SubModule.cs` entries.

**Goal:** Bias the enlisted lord toward more coherent strategic behavior on the campaign map — better pursuit selection, better target choice, less reckless army formation, less thrashing under strain — without rewriting the campaign AI. All bias comes from reading the Plan 1 snapshot; every wrapper falls through to vanilla whenever the current call is not for the enlisted lord's party or the player is not actively enlisted. Design spec §8 ("Lord Logic Design") is the authoritative behavioral reference; design spec §8.5 is the authoritative intervention-layer order.

**Architecture:** Three `MBGameModel<T>` wrappers registered via `CampaignGameStarter.AddModel<T>(...)` (`TargetScoreCalculatingModel`, `ArmyManagementCalculationModel`, `MobilePartyAIModel`). `SettlementValueModel` is deliberately skipped — see Phase E for the rationale. Each wrapper's constructor runs at `SubModule.OnGameStart`; `CampaignGameStarter.AddModel<T>` auto-invokes `Initialize(baseModel)` on the wrapper where `baseModel` is the most recently registered `T` — i.e., the vanilla `Default*Model` that Sandbox registered before our `OnGameStart`. Every overridden method calls a single shared identity helper (`EnlistedAiGate.TryGetSnapshotForParty`), and when that helper returns `false` the method delegates straight to `BaseModel.Same(...)`. When it returns `true`, the method computes the vanilla result first, then applies a narrow snapshot-driven adjustment, then returns. A reserved Phase F task covers narrow `[HarmonyPatch]`es against specific vanilla CampaignBehavior loops if — and only if — smoke testing in Phase H surfaces failure modes the model layer cannot reach.

**Tech Stack:** C# 7.3 / .NET Framework 4.7.2, Mount & Blade II: Bannerlord `MBGameModel<T>` + `CampaignGameStarter.AddModel<T>` + Enlisted `ModLogger` / `EnlistmentBehavior` + Harmony 2 (Phase F only, contingent).

**Scope boundary — what this plan does NOT do:**

- No `SignalBuilder`, `StoryCandidate` fields, or soldier-facing signal projection. Reserved for Plan 3.
- No `DutyOpportunityBuilder`, storylet JSON, scripted effects, or duty content. Reserved for Plan 4.
- No changes to `EnlistedLordIntelligenceSnapshot` shape or `SnapshotClassifier` logic. If a bias needs a field that Plan 1 does not publish, the plan files a Plan-1-follow-up task; it does not hot-patch Plan 1.
- No `GameModel` replacement beyond the wrappers listed in Phase B / C / D (and the deliberately-skipped E). No full `MobilePartyAi.Think` replacement.
- No global AI coherence work for non-enlisted lords. Every override is gated per-call on `party == EnlistmentBehavior.Instance.EnlistedLord.PartyBelongedTo`.
- No persistence. Wrappers are stateless; `SaveableTypeDefiner` is not touched.
- No revert mechanism beyond the identity gate. When `EnlistmentBehavior.Instance.IsEnlisted` flips to `false`, Plan 1's `Current` getter returns `null`; every gate helper returns `false`; every wrapper falls through to `BaseModel`. Revert is automatic and immediate, as required by design spec §12.3.

**Verification surface:** Pure identity-gate logic is unit-testable in principle, but the project has no test harness today. The realistic verification stack is `dotnet build` + `validate_content.py` + in-game smoke via `ModLogger` heartbeats. Phase G adds a bias-event heartbeat so Phase H can confirm the wrappers are firing (and, equally importantly, not firing for non-enlisted lords). Phase H runs a 14 in-game day session that compares enlisted-lord decisions before and after the wrappers ship.

---

## Design decisions locked before task 1

### Identity gate is per-call, not per-behavior

Every model method receives a `MobileParty` or `Settlement` argument. The enlisted-only constraint requires that EVERY overridden call check whether the argument is tied to the enlisted lord's current party. A blanket `IsEnlisted`-only check at the top of a wrapper is incorrect — it would bias every lord in Calradia whenever the player is enlisted, which violates the hard constraint that "Phase-1 scope must be reversible" and the non-goal "No global rewrite for every AI lord in Calradia" (design spec §3, §4.7).

The gate is centralized in `EnlistedAiGate.TryGetSnapshotForParty(MobileParty, out snapshot)` (Task T2). It returns `false` when:

- `EnlistmentBehavior.Instance` is null, or
- `EnlistmentBehavior.Instance.IsEnlisted` is false, or
- `EnlistmentBehavior.Instance.EnlistedLord` is null or `IsAlive` is false, or
- `EnlistmentBehavior.Instance.EnlistedLord.PartyBelongedTo` is null, or
- The argument party is not referentially equal to that `PartyBelongedTo`, or
- `EnlistedCampaignIntelligenceBehavior.Instance?.Current` is null (Plan 1 not ready / snapshot not populated yet).

When the gate returns `false`, the wrapper calls `BaseModel.<same>(...)` and returns that value unmodified. When it returns `true`, the `out snapshot` parameter is non-null and all Plan 1 snapshot fields are readable.

For `Settlement`-only overrides (`CalculatePatrollingScoreForSettlement`), the gate takes the `MobileParty` also present on the signature — every `SettlementValueModel` / `TargetScoreCalculatingModel` call that might need biasing carries a party handle.

### BaseModel null-guard is defensive, not load-bearing

`MBGameModel<T>.Initialize(T baseModel)` is invoked by `CampaignGameStarter.AddModel<T>` (decompile: `TaleWorlds.CampaignSystem/CampaignGameStarter.cs:76-81`). `Initialize` assigns `BaseModel = baseModel` where `baseModel` is the most recent previously-registered `T` — i.e., the vanilla `Default*Model` registered by `SandboxCampaignGameStarter` before our `OnGameStart`. `BaseModel` should never be null after `AddModel<T>` returns.

Every overridden method begins with:

```csharp
if (BaseModel == null)
{
    ModLogger.Surfaced("INTELAI", "base_model_missing", null);
    return <type-sensible default>;
}
var vanilla = BaseModel.<method>(...);
```

The null-guard protects against bootstrap-order regressions (e.g., future refactor that re-registers `Default*` later in the load order) without crashing the AI tick. The `Surfaced` call is intentional — a missing BaseModel means the wrapper is silently returning a default value, which is both a visible bug and a developer-facing warning.

### Enlisted lord as army member vs army leader

When the enlisted lord's party is a non-leader member of another lord's army (`lord.PartyBelongedTo.AttachedTo != null` and `lord.PartyBelongedTo != lord.PartyBelongedTo.Army.LeaderParty`), the enlisted lord has effectively ceded strategic authority to the army leader. Party-level AI in that state is dominated by escort-to-leader behavior, and wrapper biasing has near-zero effect because the overridden models are called against the army leader's party, not the enlisted lord's.

This is correct per design (the lord made a strategic choice to join an army and is now following) and matches vanilla behavior. The plan does NOT try to bias the army leader's party — that would expand scope into "global AI coherence" (design spec §3 non-goal). If the enlisted lord is an army *leader*, all wrapper biasing applies normally; the gate already passes when `party == lord.PartyBelongedTo`, and the lord BEING the leader makes that party the decision point.

Implementers must not "fix" the attached-as-member case — it is not a bug. Phase A Task T1's docstring documents this so a future session does not chase a phantom.

### Snapshot freshness is acceptable for bias

Plan 1's snapshot recomputes hourly (`CampaignEvents.HourlyTickEvent`). `DefaultMobilePartyAIModel.AiCheckInterval = 0.25f` (in-game days = 6 hours per tick, decompile `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultMobilePartyAIModel.cs:15`). Within any AI interval there will be 6 hours of snapshot staleness at worst. For bias (multiplicative adjustments to vanilla scores), up-to-hour-old state is acceptable; the enlisted lord is not making hair-trigger life-or-death decisions that require sub-hourly precision. Wrappers read `Current` per call with no local caching — `Current` is a cheap getter on `EnlistedCampaignIntelligenceBehavior.Instance` (single reference read + IsEnlisted check).

### Phase E — SettlementValueModel is skipped, with rationale recorded

Design spec §8.5 lists `SettlementValueModel` as **"if needed"**, not mandatory. After reading the interface (decompile: `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.ComponentInterfaces/SettlementValueModel.cs`) the conclusion is that biasing it alongside `TargetScoreCalculatingModel` would double-count: `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` already weighs settlement value internally, and `DefaultSettlementValueModel.CalculateSettlementValueForEnemyHero` feeds into that score chain. Biasing both surfaces would compound the adjustment non-linearly and make calibration fragile.

Decision: **Phase B's `TargetScoreCalculatingModel` wrapper is the single biasing surface for settlement-oriented strategic choice**. If smoke testing in Phase H reveals that target-score biasing is insufficient (e.g., the lord still chooses low-value settlements even when `FrontPressure == High`), Plan 2 adds a follow-up task under Phase E to wrap `SettlementValueModel` narrowly. Until then, Phase E is a placeholder section with this rationale and no tasks.

### Phase F — narrow Harmony is a reserved investigation slot

The model wrappers reach most of the design-spec §8.1-8.3 failure modes through these vanilla sites (confirmed in the decompile):

- Bait / pursuit magnetization → `MobilePartyAIModel.GetBestInitiativeBehavior` (decompile `DefaultMobilePartyAIModel.cs:107-` and its consumer at `MobilePartyAi.cs:485-513`).
- Low-value target scoring → `TargetScoreCalculatingModel.GetTargetScoreForFaction` and `.CurrentObjectiveValue` (decompile `DefaultTargetScoreCalculatingModel.cs:58-, :147-`).
- Casual army formation → `ArmyManagementCalculationModel.GetMobilePartiesToCallToArmy` (decompile `DefaultArmyManagementCalculationModel.cs:116-`).

Pre-committing to specific `[HarmonyPatch]` sites before smoke-testing would violate design spec §13 ("If the model layer and narrow patch layer disagree, the patch layer should never force behavior outside the enlisted-only gate"). Phase F is therefore **one reserved task** (T18) that instructs the implementer: during Phase H smoke testing, if any §8.1-8.3 failure mode persists despite wrapper coverage, author a narrow Harmony prefix against the specific vanilla site still forcing the bad behavior. Candidate targets (verify in decompile before patching): `MobilePartyAi.TickInternal` (decompile line 427), `MobilePartyAi.GetBehaviors` (line 477), faction AI behaviors under `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.*`.

If smoke testing shows no such persistence, T18 closes out with "no patch needed; wrappers sufficient."

### No save-definer changes

Wrappers are stateless singletons held only by the `CampaignGameStarter`'s `_models` list (decompile: `CampaignGameStarter.cs:73`). They read Plan 1's `EnlistedCampaignIntelligenceBehavior.Instance?.Current` each call — they never hold snapshot state locally, and they never write to any store. Phase 0 therefore does NOT touch `EnlistedSaveDefiner`. This is intentional — Plan 2 is a bias layer, not a persistent subsystem.

### csproj is additive; wildcard non-recursion applies

Plan 1 registers `src/Features/CampaignIntelligence/*.cs` files directly (not via a wildcard). Plan 2 adds a new `src/Features/CampaignIntelligence/Models/` subfolder. Per CLAUDE.md "Enlisted.csproj wildcards are NON-RECURSIVE", a wildcard at the parent level does NOT cover the subfolder. Every new file under `Models/` needs its own explicit `<Compile Include>` line. Task T1 adds six entries at once so the build passes against the subsequent scaffold tasks.

### Registration site in SubModule

`AddModel<T>(...)` and `AddBehavior(...)` both belong in the same `OnGameStart` handler in `SubModule.cs`. Order within that handler does not matter for model correctness (the first `AddModel<T>` call captures the then-most-recent vanilla `Default*Model`; subsequent calls don't matter because `T` is different). The plan registers the four wrappers near the Plan 1 behavior registration — immediately after `EnlistedCampaignIntelligenceBehavior` so the conceptual grouping is intact. Exact anchor is at `src/Mod.Entry/SubModule.cs:362` (after `PathScorer`) when Plan 1's T7 has landed.

---

## File structure

```
src/Features/CampaignIntelligence/Models/
  EnlistedAiGate.cs                               — shared identity-gate helper
  EnlistedTargetScoreModel.cs                     — wraps TargetScoreCalculatingModel (Phase B)
  EnlistedArmyManagementModel.cs                  — wraps ArmyManagementCalculationModel (Phase C)
  EnlistedMobilePartyAiModel.cs                   — wraps MobilePartyAIModel (Phase D)
  EnlistedAiBiasHeartbeat.cs                      — throttled observability logger (Phase G)

src/Mod.GameAdapters/Patches/
  EnlistedLordAiNarrowPatches.cs                  — Phase F contingent narrow Harmony patches
```

Six new files, all under two existing tracked directories. No new top-level feature folder.

---

## Task index

- **Phase 0 — csproj claim**: T1
- **Phase A — Identity gate + wrapper skeleton**: T2, T3
- **Phase B — TargetScoreCalculatingModel wrapper**: T4, T5, T6, T7
- **Phase C — ArmyManagementCalculationModel wrapper**: T8, T9, T10
- **Phase D — MobilePartyAIModel wrapper**: T11, T12, T13, T14
- **Phase E — SettlementValueModel (skip with rationale)**: T15
- **Phase F — Narrow Harmony reserved investigation**: T16, T17, T18
- **Phase G — Observability + error handling**: T19, T20, T21
- **Phase H — Smoke verification + sign-off**: T22, T23, T24, T25

Total: 25 tasks.

---

## Phase 0 — csproj claim

### Task T1: Claim csproj slots for the six new files

**Files:**
- Modify: `Enlisted.csproj` (add explicit `<Compile Include>` entries for the six new files)

- [ ] **Step 1: Confirm none of the target paths are already registered**

```
Grep pattern: "src\\\\Features\\\\CampaignIntelligence\\\\Models\\\\|src\\\\Mod\\.GameAdapters\\\\Patches\\\\EnlistedLordAiNarrowPatches" in Enlisted.csproj
Expected: zero matches
```

If any match, another branch has already claimed the slot — stop and reconcile.

- [ ] **Step 2: Add six `<Compile Include>` lines**

The csproj uses explicit per-file includes. Anchor the new block immediately after Plan 1's `CampaignIntelligence` registration block (Plan 1's T1 added those lines in the alphabetic neighborhood between `src\Features\Camp\` and `src\Features\Combat\`). Insert the Models/ entries as a sub-block under that same region:

```xml
    <Compile Include="src\Features\CampaignIntelligence\Models\EnlistedAiGate.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Models\EnlistedTargetScoreModel.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Models\EnlistedArmyManagementModel.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Models\EnlistedMobilePartyAiModel.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Models\EnlistedAiBiasHeartbeat.cs"/>
```

The Phase F Harmony patch file is registered under the existing Patches block. Locate the anchor by grepping for `Mod\GameAdapters\Patches\AbandonArmyBlockPatch.cs` and insert after it:

```xml
    <Compile Include="src\Mod.GameAdapters\Patches\EnlistedLordAiNarrowPatches.cs"/>
```

CLAUDE.md wildcard-non-recursion pitfall: do NOT remove any of these explicit lines later in favor of a `Models\*.cs` glob — the build at `Enlisted.csproj:390` shows `<Compile Include="src\Features\Activities\*.cs"/>` at the parent `Activities\` level does not pick up `Activities\Home\Foo.cs`. Same rule applies here.

- [ ] **Step 3: Build — expect compile failure**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **FAIL** with `The type or namespace name 'EnlistedAiGate' does not exist` etc. That's correct — types don't exist yet. T2-T17 make this build succeed.

- [ ] **Step 4: Commit**

```bash
git add Enlisted.csproj
git commit -F <message-file>
```

Commit message:

```
feat(intelai): claim csproj slots for Plan 2 wrapper + Harmony files

Reserves six Compile entries under src/Features/CampaignIntelligence/Models/
and src/Mod.GameAdapters/Patches/ for the Lord AI Intervention layer.
Build currently fails — types land in T2-T17. Part of Plan 2 of 5 (Lord
AI Intervention).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase A — Identity gate + wrapper skeleton

### Task T2: Create `EnlistedAiGate.cs` — shared identity helper

**Files:**
- Create: `src/Features/CampaignIntelligence/Models/EnlistedAiGate.cs`

- [ ] **Step 1: Write the helper**

```csharp
using Enlisted.Features.Enlistment.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Shared per-call identity gate for every enlisted-only AI model wrapper.
    /// Returns true only when the player is actively enlisted, the given party
    /// belongs to the enlisted lord, and Plan 1's intelligence snapshot is
    /// available. Every model wrapper must call this before applying bias;
    /// when it returns false, the caller delegates unmodified to BaseModel.
    /// </summary>
    internal static class EnlistedAiGate
    {
        /// <summary>
        /// Tests whether <paramref name="party"/> is the enlisted lord's current
        /// party and, if so, exposes the latest intelligence snapshot via
        /// <paramref name="snapshot"/>. Cheap reference compares + one property
        /// read; safe to call on every AI tick.
        /// </summary>
        public static bool TryGetSnapshotForParty(
            MobileParty party,
            out EnlistedLordIntelligenceSnapshot snapshot)
        {
            snapshot = null;
            if (party == null)
            {
                return false;
            }
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return false;
            }
            var lord = enlistment.EnlistedLord;
            if (lord == null || !lord.IsAlive)
            {
                return false;
            }
            var lordParty = lord.PartyBelongedTo;
            if (lordParty == null || lordParty != party)
            {
                return false;
            }
            var intel = EnlistedCampaignIntelligenceBehavior.Instance;
            if (intel == null)
            {
                return false;
            }
            snapshot = intel.Current;
            return snapshot != null;
        }

        /// <summary>
        /// Returns true when the enlisted lord's party is currently the army
        /// leader's party. When false, Plan 2 bias is inert because the
        /// enlisted lord has ceded strategy to the army leader — this is by
        /// design (design spec §6.3) and not a bug. Used by Phase C/D
        /// wrappers that only apply under leader authority.
        /// </summary>
        public static bool IsEnlistedLordArmyLeader()
        {
            var lord = EnlistmentBehavior.Instance?.EnlistedLord;
            var party = lord?.PartyBelongedTo;
            var army = party?.Army;
            return army != null && army.LeaderParty == party;
        }
    }
}
```

**VERIFY IN DECOMPILE:** `Hero.PartyBelongedTo` (decompile `TaleWorlds.CampaignSystem/Hero.cs:279,285,377,379`), `Army.LeaderParty` (decompile `TaleWorlds.CampaignSystem/Army.cs:118`), `MobileParty.Army` — all previously confirmed by Plan 1's T9/T10/T21 tasks; re-confirm when touching this file if the decompile has been regenerated since.

- [ ] **Step 2: Run CRLF normalizer on the new file**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/Models/EnlistedAiGate.cs
```

- [ ] **Step 3: Build — expect compile failure (wrappers not yet written)**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: still fails on the wrapper types that T3 will stub, but `EnlistedAiGate` itself should compile clean.

- [ ] **Step 4: Commit**

```bash
git add src/Features/CampaignIntelligence/Models/EnlistedAiGate.cs
git commit -F <message-file>
```

Commit message:

```
feat(intelai): add EnlistedAiGate per-call identity helper (T2)

Shared gate consumed by every Plan 2 model wrapper. Returns the current
intelligence snapshot only when the caller is operating on the enlisted
lord's party AND the player is actively enlisted AND Plan 1's snapshot
is populated. Every override delegates to BaseModel when this returns
false, preserving vanilla behavior for non-enlisted parties.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T3: Create empty wrapper stubs so the csproj slots compile

**Files:**
- Create: `src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs` (stub)
- Create: `src/Features/CampaignIntelligence/Models/EnlistedArmyManagementModel.cs` (stub)
- Create: `src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs` (stub)
- Create: `src/Features/CampaignIntelligence/Models/EnlistedAiBiasHeartbeat.cs` (stub)
- Create: `src/Mod.GameAdapters/Patches/EnlistedLordAiNarrowPatches.cs` (stub, Phase F)

Rationale: Task T1 registered five file paths in the csproj (plus the gate, which landed in T2). Without stubs the build fails; Phases B-F fill them in. Each stub inherits from the vanilla default model so the `AddModel<T>(...)` registration in T23 can land without "the type doesn't implement the abstract contract" errors.

- [ ] **Step 1: Write `EnlistedTargetScoreModel.cs` as a minimal pass-through**

```csharp
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Wraps vanilla TargetScoreCalculatingModel. Phases B (T4-T7) fill in
    /// enlisted-only bias for defend/relieve/siege/raid/pursuit choices.
    /// Until then, every method delegates unmodified to BaseModel.
    /// </summary>
    public sealed class EnlistedTargetScoreModel : TargetScoreCalculatingModel
    {
        public override float TravelingToAssignmentFactor
            => BaseModel?.TravelingToAssignmentFactor ?? 1.33f;
        public override float BesiegingFactor => BaseModel?.BesiegingFactor ?? 1.67f;
        public override float AssaultingTownFactor => BaseModel?.AssaultingTownFactor ?? 2f;
        public override float RaidingFactor => BaseModel?.RaidingFactor ?? 1.67f;
        public override float DefendingFactor => BaseModel?.DefendingFactor ?? 2f;

        public override float GetPatrollingFactor(bool isNavalPatrolling)
            => BaseModel?.GetPatrollingFactor(isNavalPatrolling) ?? 0.66f;

        public override float GetTargetScoreForFaction(
            Settlement targetSettlement, Army.ArmyTypes missionType,
            MobileParty mobileParty, float ourStrength)
            => BaseModel?.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, ourStrength) ?? 0f;

        public override float CalculatePatrollingScoreForSettlement(
            Settlement settlement, bool isFromPort, MobileParty mobileParty)
            => BaseModel?.CalculatePatrollingScoreForSettlement(settlement, isFromPort, mobileParty) ?? 0f;

        public override float CurrentObjectiveValue(MobileParty mobileParty)
            => BaseModel?.CurrentObjectiveValue(mobileParty) ?? 0f;
    }
}
```

**VERIFY IN DECOMPILE:** Signature surface from `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.ComponentInterfaces/TargetScoreCalculatingModel.cs` — 10 abstract members total; the stub implements all 10. Vanilla default values (`1.33f`, `1.67f`, `2f`, `0.66f`) sourced from `DefaultTargetScoreCalculatingModel.cs:29-47`; used only when `BaseModel` is null (bootstrap-order regression guard).

- [ ] **Step 2: Write `EnlistedArmyManagementModel.cs` stub**

```csharp
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Wraps vanilla ArmyManagementCalculationModel. Phase C (T8-T10) fills
    /// in enlisted-only bias for call-to-army decisions under supply/strain
    /// pressure. Until then, every method delegates to BaseModel.
    /// </summary>
    public sealed class EnlistedArmyManagementModel : ArmyManagementCalculationModel
    {
        public override float AIMobilePartySizeRatioToCallToArmy
            => BaseModel?.AIMobilePartySizeRatioToCallToArmy ?? 0.6f;
        public override float PlayerMobilePartySizeRatioToCallToArmy
            => BaseModel?.PlayerMobilePartySizeRatioToCallToArmy ?? 0.4f;
        public override float MinimumNeededFoodInDaysToCallToArmy
            => BaseModel?.MinimumNeededFoodInDaysToCallToArmy ?? 15f;
        public override float MaximumDistanceToCallToArmy
            => BaseModel?.MaximumDistanceToCallToArmy ?? 1000f;
        public override int InfluenceValuePerGold => BaseModel?.InfluenceValuePerGold ?? 40;
        public override int AverageCallToArmyCost => BaseModel?.AverageCallToArmyCost ?? 20;
        public override int CohesionThresholdForDispersion => BaseModel?.CohesionThresholdForDispersion ?? 10;
        public override float MaximumWaitTime => BaseModel?.MaximumWaitTime ?? 72f;

        public override bool CanPlayerCreateArmy(out TextObject disabledReason)
        {
            if (BaseModel != null) return BaseModel.CanPlayerCreateArmy(out disabledReason);
            disabledReason = TextObject.Empty;
            return false;
        }

        public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
            => BaseModel?.CalculatePartyInfluenceCost(armyLeaderParty, party) ?? 0;

        public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
            => BaseModel?.DailyBeingAtArmyInfluenceAward(armyMemberParty) ?? 0f;

        public override List<MobileParty> GetMobilePartiesToCallToArmy(MobileParty leaderParty)
            => BaseModel?.GetMobilePartiesToCallToArmy(leaderParty) ?? new List<MobileParty>();

        public override int CalculateTotalInfluenceCost(Army army, float percentage)
            => BaseModel?.CalculateTotalInfluenceCost(army, percentage) ?? 0;

        public override float GetPartySizeScore(MobileParty party)
            => BaseModel?.GetPartySizeScore(party) ?? 0f;

        public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation)
        {
            if (BaseModel != null) return BaseModel.CheckPartyEligibility(party, out explanation);
            explanation = TextObject.Empty;
            return false;
        }

        public override int GetPartyRelation(Hero hero)
            => BaseModel?.GetPartyRelation(hero) ?? 0;

        public override ExplainedNumber CalculateDailyCohesionChange(Army army, bool includeDescriptions = false)
            => BaseModel?.CalculateDailyCohesionChange(army, includeDescriptions) ?? new ExplainedNumber(0f, includeDescriptions);

        public override int CalculateNewCohesion(Army army, PartyBase newParty, int calculatedCohesion, int sign)
            => BaseModel?.CalculateNewCohesion(army, newParty, calculatedCohesion, sign) ?? calculatedCohesion;

        public override int GetCohesionBoostInfluenceCost(Army army, int percentageToBoost = 100)
            => BaseModel?.GetCohesionBoostInfluenceCost(army, percentageToBoost) ?? 0;
    }
}
```

**VERIFY IN DECOMPILE:** Signature surface from `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.ComponentInterfaces/ArmyManagementCalculationModel.cs` — 18 abstract members; stub implements all 18. Default fallback values from `DefaultArmyManagementCalculationModel.cs:29-43`. `TextObject.Empty` — confirm at `TaleWorlds.Localization/TextObject.cs`.

- [ ] **Step 3: Write `EnlistedMobilePartyAiModel.cs` stub**

```csharp
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Wraps vanilla MobilePartyAIModel. Phase D (T11-T14) fills in
    /// enlisted-only bias for pursuit/avoidance/initiative decisions. Until
    /// then, every method delegates to BaseModel.
    /// </summary>
    public sealed class EnlistedMobilePartyAiModel : MobilePartyAIModel
    {
        public override float AiCheckInterval => BaseModel?.AiCheckInterval ?? 0.25f;
        public override float FleeToNearbyPartyRadius => BaseModel?.FleeToNearbyPartyRadius ?? 1f;
        public override float FleeToNearbySettlementRadius => BaseModel?.FleeToNearbySettlementRadius ?? 2f;
        public override float HideoutPatrolDistanceAsDays => BaseModel?.HideoutPatrolDistanceAsDays ?? 0.5f;
        public override float FortificationPatrolDistanceAsDays => BaseModel?.FortificationPatrolDistanceAsDays ?? 0.3f;
        public override float VillagePatrolDistanceAsDays => BaseModel?.VillagePatrolDistanceAsDays ?? 0.25f;
        public override float SettlementDefendingNearbyPartyCheckRadius
            => BaseModel?.SettlementDefendingNearbyPartyCheckRadius ?? 9f;
        public override float SettlementDefendingWaitingPositionRadius
            => BaseModel?.SettlementDefendingWaitingPositionRadius ?? 3f;
        public override float NeededFoodsInDaysThresholdForSiege
            => BaseModel?.NeededFoodsInDaysThresholdForSiege ?? 12f;
        public override float NeededFoodsInDaysThresholdForRaid
            => BaseModel?.NeededFoodsInDaysThresholdForRaid ?? 8f;

        public override bool ShouldConsiderAvoiding(MobileParty party, MobileParty targetParty)
            => BaseModel?.ShouldConsiderAvoiding(party, targetParty) ?? false;

        public override bool ShouldConsiderAttacking(MobileParty party, MobileParty targetParty)
            => BaseModel?.ShouldConsiderAttacking(party, targetParty) ?? false;

        public override float GetPatrolRadius(MobileParty mobileParty, CampaignVec2 patrolPoint)
            => BaseModel?.GetPatrolRadius(mobileParty, patrolPoint) ?? 0f;

        public override bool ShouldPartyCheckInitiativeBehavior(MobileParty mobileParty)
            => BaseModel?.ShouldPartyCheckInitiativeBehavior(mobileParty) ?? false;

        public override void GetBestInitiativeBehavior(
            MobileParty mobileParty,
            out AiBehavior bestInitiativeBehavior,
            out MobileParty bestInitiativeTargetParty,
            out float bestInitiativeBehaviorScore,
            out Vec2 averageEnemyVec)
        {
            if (BaseModel != null)
            {
                BaseModel.GetBestInitiativeBehavior(
                    mobileParty,
                    out bestInitiativeBehavior,
                    out bestInitiativeTargetParty,
                    out bestInitiativeBehaviorScore,
                    out averageEnemyVec);
                return;
            }
            bestInitiativeBehavior = AiBehavior.None;
            bestInitiativeTargetParty = null;
            bestInitiativeBehaviorScore = 0f;
            averageEnemyVec = Vec2.Zero;
        }
    }
}
```

**VERIFY IN DECOMPILE:** Signature surface from `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.ComponentInterfaces/MobilePartyAIModel.cs` — 15 abstract members; stub implements all 15. Default fallback values from `DefaultMobilePartyAIModel.cs:15-33`. `AiBehavior.None` — confirm at `TaleWorlds.CampaignSystem.Party/AiBehavior.cs`.

- [ ] **Step 4: Write `EnlistedAiBiasHeartbeat.cs` stub**

```csharp
namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Throttled ModLogger emitter used by every Plan 2 wrapper to record
    /// that bias was applied. Fills in during Phase G (T19). Kept here as a
    /// stub so csproj registration compiles during Phases A-F.
    /// </summary>
    internal static class EnlistedAiBiasHeartbeat
    {
    }
}
```

- [ ] **Step 5: Write `EnlistedLordAiNarrowPatches.cs` stub**

```csharp
using HarmonyLib;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Phase F narrow Harmony patches for AI behavior paths the model-wrapper
    /// layer cannot reach. Intentionally empty at plan-authoring time — T18
    /// only authors patches here if Phase H smoke testing reveals that the
    /// model wrappers leave one of design spec §8.1-8.3 failure modes
    /// unaddressed. See Plan 2 §"Phase F — narrow Harmony is a reserved
    /// investigation slot" for the investigation contract.
    /// </summary>
    [HarmonyPatch]
    internal static class EnlistedLordAiNarrowPatches
    {
    }
}
```

- [ ] **Step 6: Normalize CRLF on all five new files**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/Models/EnlistedArmyManagementModel.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/Models/EnlistedAiBiasHeartbeat.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Mod.GameAdapters/Patches/EnlistedLordAiNarrowPatches.cs
```

- [ ] **Step 7: Build — expect success**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**. Every abstract member has a pass-through body that delegates to `BaseModel`. The wrappers are live as no-ops until Phases B-D fill in bias.

- [ ] **Step 8: Commit**

```bash
git add src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs src/Features/CampaignIntelligence/Models/EnlistedArmyManagementModel.cs src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs src/Features/CampaignIntelligence/Models/EnlistedAiBiasHeartbeat.cs src/Mod.GameAdapters/Patches/EnlistedLordAiNarrowPatches.cs
git commit -F <message-file>
```

Commit message:

```
feat(intelai): add pass-through wrapper stubs for Plan 2 models (T3)

Every abstract member on TargetScoreCalculatingModel (10),
ArmyManagementCalculationModel (18), and MobilePartyAIModel (15) has a
BaseModel-delegating body so the csproj registrations from T1 compile
clean and AddModel<T> can land in T23. Bias bodies arrive in Phases
B/C/D; these stubs are inert. Also seeds the heartbeat + Harmony files
as empty placeholders.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase B — TargetScoreCalculatingModel wrapper

Design-spec §8.2 ("Strategic target choice") is the reference. The three primary biases at this layer:

- **Defend before speculative raiding** — when `missionType == Defender` and `snapshot.FrontPressure >= Medium`, multiply the vanilla score up (`1.15f-1.6f` depending on `FrontPressure`). Plan 1's classifier folds threatened-friendly count into `FrontPressure`, so the enum alone is a reliable proxy.
- **Pick siege targets more selectively** — when `missionType == Besieger` and `RecoveryNeed >= Medium` OR `SupplyPressure >= Strained`, multiply down (`0.5f-0.8f`).
- **Stop sticking to a bad pursuit** — when `CurrentObjectiveValue` is being asked about a pursue objective whose `PursuitViability < Viable`, multiply down so the lord's comparator finds anything else better.

The wrapper also biases `CalculatePatrollingScoreForSettlement` down when `FrontPressure >= High` — patrolling is the wrong posture under pressure and the lord should prefer defend/relief targets instead.

### Task T4: Flesh out `EnlistedTargetScoreModel` — add snapshot-read helper

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs`

- [ ] **Step 1: Add a private helper that bundles the gate + a vanilla-score return path**

Insert at the bottom of the class (above the closing `}`):

```csharp
        /// <summary>
        /// Unified identity-gate + vanilla-fallback entry for every biased
        /// float-returning method. Always returns the vanilla value when the
        /// gate fails; the caller applies any enlisted-only bias on top.
        /// </summary>
        private float VanillaOnlyOrBias(
            MobileParty party,
            float vanilla,
            System.Func<EnlistedLordIntelligenceSnapshot, float, float> biasFn)
        {
            if (!EnlistedAiGate.TryGetSnapshotForParty(party, out var snapshot))
            {
                return vanilla;
            }
            try
            {
                return biasFn(snapshot, vanilla);
            }
            catch (System.Exception ex)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Caught("INTELAI", "target_score_bias_failed", ex);
                return vanilla;
            }
        }
```

Required `using` additions at the top of the file:
```csharp
using System;
```

- [ ] **Step 2: Build — expect success**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS** (no method bodies yet consume the helper).

- [ ] **Step 3: Regen error-codes + commit**

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
git add src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs docs/error-codes.md
git commit -F <message-file>
```

Commit message:

```
feat(intelai): TargetScore wrapper snapshot-read helper (T4)

Adds VanillaOnlyOrBias private helper that runs the identity gate and
exposes (snapshot, vanilla) to a caller-supplied bias delegate. Every
T5/T6/T7 override uses this helper to keep the gate/fallback pattern
identical across the wrapper.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T5: Bias `GetTargetScoreForFaction` — defend/relief priority, siege caution

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs`

- [ ] **Step 1: Replace the pass-through `GetTargetScoreForFaction` body**

```csharp
        public override float GetTargetScoreForFaction(
            Settlement targetSettlement,
            Army.ArmyTypes missionType,
            MobileParty mobileParty,
            float ourStrength)
        {
            if (BaseModel == null)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Surfaced("INTELAI", "base_model_missing", null);
                return 0f;
            }
            var vanilla = BaseModel.GetTargetScoreForFaction(
                targetSettlement, missionType, mobileParty, ourStrength);

            return VanillaOnlyOrBias(mobileParty, vanilla, (snapshot, v) =>
            {
                // Defend / relieve friendly-under-threat bias. Key only on
                // FrontPressure — Plan 1 T14's classifier already folds
                // ThreatenedFriendlySettlementCount into FrontPressure score
                // (>0 adds +2, >2 adds +4), so `FrontPressure >= Medium`
                // is a reliable proxy for "there's a threatened friendly"
                // without needing a raw count on the snapshot. See Plan 1
                // IntelligenceEnums + SnapshotClassifier.ClassifyFrontPressure.
                if (missionType == Army.ArmyTypes.Defender
                    && snapshot.FrontPressure >= FrontPressure.Medium)
                {
                    float mult = snapshot.FrontPressure == FrontPressure.Critical ? 1.6f
                               : snapshot.FrontPressure == FrontPressure.High ? 1.35f
                               : 1.15f;
                    EnlistedAiBiasHeartbeat.Record("target_score_defend", v, v * mult);
                    return v * mult;
                }

                // Besieger bias down under recovery / supply strain. The lord
                // should not commit to an offensive siege while under strain.
                if (missionType == Army.ArmyTypes.Besieger)
                {
                    if (snapshot.RecoveryNeed >= RecoveryNeed.Medium
                        || snapshot.SupplyPressure >= SupplyPressure.Strained
                        || snapshot.ArmyStrain >= ArmyStrainLevel.Severe)
                    {
                        float mult = snapshot.RecoveryNeed == RecoveryNeed.High ? 0.5f
                                   : snapshot.SupplyPressure == SupplyPressure.Critical ? 0.55f
                                   : 0.7f;
                        EnlistedAiBiasHeartbeat.Record("target_score_siege_strained", v, v * mult);
                        return v * mult;
                    }
                }

                // Raider bias down under High-or-greater front pressure.
                // Design-spec §8.2: "defend or relieve nearby friendly holdings
                // before speculative raiding." FrontPressure is the proxy for
                // "there's something more important nearby" — see Plan 1
                // classifier rationale above.
                if (missionType == Army.ArmyTypes.Raider
                    && snapshot.FrontPressure >= FrontPressure.High)
                {
                    EnlistedAiBiasHeartbeat.Record("target_score_raid_suppressed", v, v * 0.6f);
                    return v * 0.6f;
                }

                return v;
            });
        }
```

**VERIFY IN DECOMPILE:** `Army.ArmyTypes` enum values `Besieger`, `Raider`, `Defender` — confirm all three present at `TaleWorlds.CampaignSystem/Army.cs:22`. `FrontPressure` / `RecoveryNeed` / `SupplyPressure` / `ArmyStrainLevel` — Plan 1 T2 registered these enums; confirm names match Plan 1's final IntelligenceEnums.cs.

**Design note (Plan 1 publishing surface):** The snapshot published by Plan 1 T3 contains only enums + `CampaignTime` — it does NOT expose `NearestThreatenedFriendly` (a Settlement reference) or `ThreatenedFriendlySettlementCount` (a raw integer); both live only on the transient `IntelligenceInputs` struct (Plan 1 T8). The bias here keys solely on `FrontPressure`, which is a reliable proxy because Plan 1's `ClassifyFrontPressure` (T14) already folds the threatened-friendly count into the pressure score (`ThreatenedFriendlySettlementCount > 0` adds +2, `> 2` adds +4 toward the enum classification). If smoke-testing shows the proxy too coarse, file a Plan-1-follow-up task to publish a `ThreatenedFriendlyCount` byte on the snapshot rather than hot-patching Plan 2's scope.

- [ ] **Step 3: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 4: Regenerate error-codes + commit**

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
git add src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs docs/error-codes.md
git commit -F <message-file>
```

Commit message:

```
feat(intelai): bias target scores by FrontPressure/RecoveryNeed/Supply (T5)

GetTargetScoreForFaction applies three enlisted-only biases: defend
targets multiplied up by 1.15-1.6 when the front is under pressure;
besieger targets multiplied down by 0.5-0.7 under recovery or supply
strain; raider targets multiplied down by 0.6 when friendlies are
threatened. Non-enlisted parties and non-lord parties receive vanilla
scoring unchanged via the identity gate.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T6: Bias `CurrentObjectiveValue` — break bad pursuit stickiness

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs`

Design-spec §8.1: "stop chasing targets that are faster and operationally worthless." Vanilla `CurrentObjectiveValue` is used by the AI layer to decide whether the current committed objective is still worth pursuing vs. a candidate alternative. When the enlisted lord is committed to a Pursue objective whose `PursuitViability` is `NotViable` or `Marginal`, biasing this value down makes the comparator prefer any alternative — breaking the stickiness.

- [ ] **Step 1: Replace the pass-through `CurrentObjectiveValue` body**

```csharp
        public override float CurrentObjectiveValue(MobileParty mobileParty)
        {
            if (BaseModel == null)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Surfaced("INTELAI", "base_model_missing", null);
                return 0f;
            }
            var vanilla = BaseModel.CurrentObjectiveValue(mobileParty);

            return VanillaOnlyOrBias(mobileParty, vanilla, (snapshot, v) =>
            {
                // Bad-pursuit suppressor. When Plan 1 says the current objective
                // is a Pursue whose viability is Marginal or NotViable, halve the
                // objective value so any alternative scores higher by comparison.
                if (snapshot.Objective == ObjectiveType.Pursue
                    && snapshot.PursuitViability <= PursuitViability.Marginal)
                {
                    float mult = snapshot.PursuitViability == PursuitViability.NotViable ? 0.3f : 0.6f;
                    EnlistedAiBiasHeartbeat.Record("objective_pursuit_weak", v, v * mult);
                    return v * mult;
                }

                // Recovery prioritizer. When RecoveryNeed == High, drop the
                // current-objective value so the AI prefers moving toward
                // settlement-backed rest.
                if (snapshot.RecoveryNeed == RecoveryNeed.High
                    && snapshot.Objective != ObjectiveType.Recover
                    && snapshot.Objective != ObjectiveType.DefendSettlement)
                {
                    EnlistedAiBiasHeartbeat.Record("objective_recovery_override", v, v * 0.5f);
                    return v * 0.5f;
                }

                return v;
            });
        }
```

**VERIFY IN DECOMPILE:** `ObjectiveType` and `PursuitViability` and `RecoveryNeed` enum names — match Plan 1 T2 final names (should be `ObjectiveType.Pursue`, `PursuitViability.NotViable/Marginal`, `RecoveryNeed.High`).

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs
git commit -F <message-file>
```

Commit message:

```
feat(intelai): break bad-pursuit stickiness via CurrentObjectiveValue (T6)

When Plan 1's snapshot reports a Pursue objective with PursuitViability
<= Marginal, CurrentObjectiveValue is multiplied by 0.3-0.6 so the AI's
current-vs-candidate comparator finds alternatives more attractive.
High RecoveryNeed also drops the current-objective value by 0.5 unless
the lord is already recovering or defending — a subtle pull toward
rest without overriding active defense.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T7: Bias `CalculatePatrollingScoreForSettlement` — less patrol under pressure

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs`

- [ ] **Step 1: Replace the pass-through `CalculatePatrollingScoreForSettlement` body**

```csharp
        public override float CalculatePatrollingScoreForSettlement(
            Settlement settlement, bool isFromPort, MobileParty mobileParty)
        {
            if (BaseModel == null)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Surfaced("INTELAI", "base_model_missing", null);
                return 0f;
            }
            var vanilla = BaseModel.CalculatePatrollingScoreForSettlement(
                settlement, isFromPort, mobileParty);

            return VanillaOnlyOrBias(mobileParty, vanilla, (snapshot, v) =>
            {
                // Patrol suppressor under pressure. Patrolling is the wrong
                // posture when the front is lit up or supply is failing.
                if (snapshot.FrontPressure >= FrontPressure.High
                    || snapshot.SupplyPressure >= SupplyPressure.Strained
                    || snapshot.RecoveryNeed >= RecoveryNeed.Medium)
                {
                    EnlistedAiBiasHeartbeat.Record("patrol_suppressed", v, v * 0.4f);
                    return v * 0.4f;
                }
                return v;
            });
        }
```

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/Models/EnlistedTargetScoreModel.cs
git commit -F <message-file>
```

Commit message:

```
feat(intelai): suppress patrol scoring under front/supply/recovery stress (T7)

CalculatePatrollingScoreForSettlement multiplied by 0.4 when the
enlisted lord's front is under High-or-greater pressure, supply is
strained, or recovery need is medium-or-greater. Patrolling is the
wrong posture in any of those states — the lord should defend,
regroup, or recover instead.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase C — ArmyManagementCalculationModel wrapper

Design-spec §8.3 ("Army management") is the reference. Primary biases:

- **Gather armies less casually** — when the enlisted lord would be the army leader AND snapshot reports `SupplyPressure >= Strained` OR `ArmyStrain >= Severe` OR `RecoveryNeed == High`, return an empty call-to-army candidate list from `GetMobilePartiesToCallToArmy`. The lord cannot sustain an army under strain.
- **Raise influence cost when strained** — `CalculatePartyInfluenceCost` multiplied up to further disincentivize calls the leader cannot sustain.
- **Pass through** the player-facing `CanPlayerCreateArmy` — biasing the player's army-creation path belongs in a different feature (Spec 4/5), not this intervention layer.

Reality check from the decompile (`DefaultArmyManagementCalculationModel.cs:29-43`): most of the model is global constant knobs (`AIMobilePartySizeRatioToCallToArmy = 0.6f` etc.). We cannot make those enlisted-specific without biasing every AI lord — that's a scope violation. The per-party bias surface is the three methods named above. Global knobs pass through unchanged.

### Task T8: Bias `GetMobilePartiesToCallToArmy` — empty list under strain

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedArmyManagementModel.cs`

- [ ] **Step 1: Replace the pass-through `GetMobilePartiesToCallToArmy` body**

```csharp
        public override List<MobileParty> GetMobilePartiesToCallToArmy(MobileParty leaderParty)
        {
            if (BaseModel == null)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Surfaced("INTELAI", "base_model_missing", null);
                return new List<MobileParty>();
            }
            var vanilla = BaseModel.GetMobilePartiesToCallToArmy(leaderParty);

            if (!EnlistedAiGate.TryGetSnapshotForParty(leaderParty, out var snapshot))
            {
                return vanilla;
            }

            // Only bias when the enlisted lord would BE the army leader. If
            // leaderParty is some other lord's party, the gate will already
            // have returned false — belt-and-braces leader check here.
            if (!EnlistedAiGate.IsEnlistedLordArmyLeader()
                && EnlistmentBehavior.Instance?.EnlistedLord?.PartyBelongedTo != leaderParty)
            {
                return vanilla;
            }

            try
            {
                bool strained = snapshot.SupplyPressure >= SupplyPressure.Strained
                             || snapshot.ArmyStrain >= ArmyStrainLevel.Severe
                             || snapshot.RecoveryNeed == RecoveryNeed.High;

                if (strained)
                {
                    EnlistedAiBiasHeartbeat.Record("call_to_army_suppressed", vanilla.Count, 0);
                    return new List<MobileParty>();
                }

                // Front pressure without full strain: trim the list to half so
                // the lord calls a smaller, closer army rather than a sprawling
                // one. Preserve ordering — vanilla list is strength-weighted at
                // DefaultArmyManagementCalculationModel.cs:178-204.
                if (snapshot.FrontPressure >= FrontPressure.High && vanilla.Count > 2)
                {
                    int trimTarget = System.Math.Max(2, vanilla.Count / 2);
                    var trimmed = new List<MobileParty>(trimTarget);
                    for (int i = 0; i < trimTarget && i < vanilla.Count; i++)
                    {
                        trimmed.Add(vanilla[i]);
                    }
                    EnlistedAiBiasHeartbeat.Record("call_to_army_trimmed", vanilla.Count, trimmed.Count);
                    return trimmed;
                }

                return vanilla;
            }
            catch (System.Exception ex)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Caught("INTELAI", "call_to_army_bias_failed", ex);
                return vanilla;
            }
        }
```

Required `using` additions:
```csharp
using Enlisted.Features.Enlistment.Behaviors;
```

**VERIFY IN DECOMPILE:** The vanilla `GetMobilePartiesToCallToArmy` ordering — the list returned at `DefaultArmyManagementCalculationModel.cs:203` is ordered by score descending (highest-value calls first). Our trim preserves front-of-list order, so the surviving entries are the highest-scoring calls. Confirm this holds by reading lines 178-204 of the decompile before committing.

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Regen error-codes + commit**

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
git add src/Features/CampaignIntelligence/Models/EnlistedArmyManagementModel.cs docs/error-codes.md
git commit -F <message-file>
```

Commit message:

```
feat(intelai): suppress call-to-army under strain (T8)

GetMobilePartiesToCallToArmy returns an empty list when the enlisted
lord would be the army leader AND Plan 1 reports SupplyPressure >=
Strained, ArmyStrain >= Severe, or RecoveryNeed == High. Under High
FrontPressure without full strain, the list is trimmed to half size so
the lord calls a tighter army rather than a sprawling one. Vanilla
ordering (strength-weighted) is preserved in the trimmed output.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T9: Bias `CalculatePartyInfluenceCost` — raise cost when strained

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedArmyManagementModel.cs`

- [ ] **Step 1: Replace the pass-through `CalculatePartyInfluenceCost` body**

```csharp
        public override int CalculatePartyInfluenceCost(
            MobileParty armyLeaderParty, MobileParty party)
        {
            if (BaseModel == null)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Surfaced("INTELAI", "base_model_missing", null);
                return 0;
            }
            var vanilla = BaseModel.CalculatePartyInfluenceCost(armyLeaderParty, party);

            if (!EnlistedAiGate.TryGetSnapshotForParty(armyLeaderParty, out var snapshot))
            {
                return vanilla;
            }

            try
            {
                // Moderate bias up when the enlisted lord (as leader) is under
                // supply pressure or elevated strain. This applies BEFORE the
                // empty-list suppressor in T8 at vanilla call-sites that iterate
                // (e.g., influence-budget checks inside the AI's loop at
                // DefaultArmyManagementCalculationModel.cs:171-175). T8 catches
                // the aggregate; T9 catches the per-call budgeting.
                if (snapshot.SupplyPressure >= SupplyPressure.Tightening
                    || snapshot.ArmyStrain >= ArmyStrainLevel.Elevated
                    || snapshot.RecoveryNeed >= RecoveryNeed.Medium)
                {
                    float mult = 1.5f;
                    if (snapshot.SupplyPressure == SupplyPressure.Critical
                        || snapshot.ArmyStrain == ArmyStrainLevel.Breaking)
                    {
                        mult = 2.5f;
                    }
                    int biased = (int)System.Math.Round(vanilla * mult);
                    EnlistedAiBiasHeartbeat.Record("influence_cost_strained", vanilla, biased);
                    return biased;
                }
                return vanilla;
            }
            catch (System.Exception ex)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Caught("INTELAI", "influence_cost_bias_failed", ex);
                return vanilla;
            }
        }
```

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/Models/EnlistedArmyManagementModel.cs
git commit -F <message-file>
```

Commit message:

```
feat(intelai): raise influence cost per call under strain (T9)

CalculatePartyInfluenceCost multiplied by 1.5-2.5 when the enlisted
lord (as leader) is under SupplyPressure >= Tightening, ArmyStrain >=
Elevated, or RecoveryNeed >= Medium. Complements T8's aggregate
suppressor by making individual calls budget-prohibitive even when T8
has not fully emptied the candidate list.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T10: Document why other ArmyManagement methods are pass-through

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedArmyManagementModel.cs`

No code change — just a class-level XML doc comment expansion so future implementers do not try to bias the 16 global knobs.

- [ ] **Step 1: Update the class doc comment**

Replace the class XML doc with:

```csharp
    /// <summary>
    /// Wraps vanilla ArmyManagementCalculationModel. Biases only three
    /// per-party methods: GetMobilePartiesToCallToArmy (T8),
    /// CalculatePartyInfluenceCost (T9), and implicitly CohesionBoost/Eligibility
    /// by pass-through. The remaining 15 abstract members are GLOBAL constants
    /// (AIMobilePartySizeRatioToCallToArmy, MinimumNeededFoodInDaysToCallToArmy,
    /// etc.) that affect every AI lord in Calradia; biasing them would
    /// violate Plan 2's enlisted-only scope. Global knobs therefore pass
    /// through to BaseModel unchanged — enlisted behavior only shifts at
    /// the per-party call sites.
    /// </summary>
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/Models/EnlistedArmyManagementModel.cs
git commit -F <message-file>
```

Commit message:

```
docs(intelai): record why 15 ArmyManagement knobs pass through (T10)

Global constants (size ratios, food thresholds, wait time) affect every
AI lord — biasing them would break Plan 2's enlisted-only scope. Only
per-party methods are biased. Doc comment expanded so future sessions
do not re-litigate the decision.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase D — MobilePartyAIModel wrapper

Design-spec §8.1 ("Movement and pursuit") is the reference. Primary biases:

- **Stop considering attacks on unviable targets** — `ShouldConsiderAttacking` returns `false` when `PursuitViability == NotViable` OR `EnemyContactRisk == High` with strong vanilla unfavorability.
- **More eager to avoid** — `ShouldConsiderAvoiding` returns `true` when `RecoveryNeed == High` OR `PursuitViability == NotViable`.
- **Break the bait spell** — `GetBestInitiativeBehavior` zeros the score when the computed initiative target pulls the lord away from a threatened friendly settlement. This is the central "magnetized by bait" fix (design-spec §8.1 first bullet).

### Task T11: Bias `ShouldConsiderAttacking` — abort unviable pursuits

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs`

- [ ] **Step 1: Replace the pass-through body**

```csharp
        public override bool ShouldConsiderAttacking(
            MobileParty party, MobileParty targetParty)
        {
            if (BaseModel == null)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Surfaced("INTELAI", "base_model_missing", null);
                return false;
            }
            var vanilla = BaseModel.ShouldConsiderAttacking(party, targetParty);

            if (!EnlistedAiGate.TryGetSnapshotForParty(party, out var snapshot))
            {
                return vanilla;
            }

            if (!vanilla)
            {
                // Vanilla already says no — don't second-guess.
                return false;
            }

            // Abort unviable chases.
            if (snapshot.PursuitViability == PursuitViability.NotViable)
            {
                EnlistedAiBiasHeartbeat.Record("attack_abort_unviable", 1, 0);
                return false;
            }

            // Abort when contact risk is high AND the lord is not army-leader
            // (a lone party should not rush a high-risk contact).
            if (snapshot.EnemyContactRisk == EnemyContactRisk.High
                && !EnlistedAiGate.IsEnlistedLordArmyLeader()
                && snapshot.ArmyStrain >= ArmyStrainLevel.Elevated)
            {
                EnlistedAiBiasHeartbeat.Record("attack_abort_high_risk", 1, 0);
                return false;
            }

            return true;
        }
```

**VERIFY IN DECOMPILE:** `EnemyContactRisk` / `PursuitViability` / `ArmyStrainLevel` — Plan 1 T2 names. `BaseModel.ShouldConsiderAttacking` signature — `(MobileParty, MobileParty) -> bool` at `MobilePartyAIModel.cs:31`.

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs
git commit -F <message-file>
```

Commit message:

```
feat(intelai): abort unviable attacks via ShouldConsiderAttacking (T11)

Returns false when PursuitViability == NotViable, OR when
EnemyContactRisk == High combined with non-leader status and elevated
strain. Vanilla false-returns are preserved (we never second-guess a
vanilla abort into an attack).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T12: Bias `ShouldConsiderAvoiding` — more avoidance under recovery need

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs`

- [ ] **Step 1: Replace the pass-through body**

```csharp
        public override bool ShouldConsiderAvoiding(
            MobileParty party, MobileParty targetParty)
        {
            if (BaseModel == null)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Surfaced("INTELAI", "base_model_missing", null);
                return false;
            }
            var vanilla = BaseModel.ShouldConsiderAvoiding(party, targetParty);

            if (!EnlistedAiGate.TryGetSnapshotForParty(party, out var snapshot))
            {
                return vanilla;
            }

            // Vanilla already says avoid — respect it.
            if (vanilla) return true;

            // Bias toward avoidance when recovery is urgent.
            if (snapshot.RecoveryNeed == RecoveryNeed.High)
            {
                EnlistedAiBiasHeartbeat.Record("avoid_recovery", 0, 1);
                return true;
            }

            // Bias toward avoidance when pursuit is not viable AND the target
            // would be the one vanilla is checking against.
            if (snapshot.PursuitViability == PursuitViability.NotViable
                && snapshot.EnemyContactRisk >= EnemyContactRisk.Medium)
            {
                EnlistedAiBiasHeartbeat.Record("avoid_unviable_pursuit", 0, 1);
                return true;
            }

            return false;
        }
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs
git commit -F <message-file>
```

Commit message:

```
feat(intelai): bias avoidance on recovery need / unviable pursuit (T12)

ShouldConsiderAvoiding returns true when RecoveryNeed == High, or when
PursuitViability is NotViable combined with medium-or-greater contact
risk. Vanilla true-returns are preserved. Complements T11's abort
logic — one biases attack-on, the other biases avoid-on, so the lord
moves away from losing engagements instead of drifting through them.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T13: Bias `GetBestInitiativeBehavior` — break the bait spell

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs`

Design spec §8.1: "stop allowing bait to drag the lord off an active front." Vanilla `GetBestInitiativeBehavior` (decompile `DefaultMobilePartyAIModel.cs:107-`) iterates nearby hostile parties, scores engagement candidates, and returns the best one. The consumer at `MobilePartyAi.cs:488` only commits to the initiative behavior when `bestInitiativeBehaviorScore > 1f`. By zeroing (or sharply reducing) that score when the candidate target pulls the lord away from a threatened front, we let the lord's `DefaultBehavior` (defend / relieve) dominate.

- [ ] **Step 1: Replace the pass-through body**

```csharp
        public override void GetBestInitiativeBehavior(
            MobileParty mobileParty,
            out AiBehavior bestInitiativeBehavior,
            out MobileParty bestInitiativeTargetParty,
            out float bestInitiativeBehaviorScore,
            out Vec2 averageEnemyVec)
        {
            if (BaseModel == null)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Surfaced("INTELAI", "base_model_missing", null);
                bestInitiativeBehavior = AiBehavior.None;
                bestInitiativeTargetParty = null;
                bestInitiativeBehaviorScore = 0f;
                averageEnemyVec = Vec2.Zero;
                return;
            }

            BaseModel.GetBestInitiativeBehavior(
                mobileParty,
                out bestInitiativeBehavior,
                out bestInitiativeTargetParty,
                out bestInitiativeBehaviorScore,
                out averageEnemyVec);

            if (!EnlistedAiGate.TryGetSnapshotForParty(mobileParty, out var snapshot))
            {
                return;
            }

            // Vanilla score already below commit threshold — no bias needed.
            if (bestInitiativeBehaviorScore <= 1f)
            {
                return;
            }

            // Bait-break: under High-or-greater front pressure AND the
            // candidate initiative is an EngageParty, zero the score so the
            // vanilla consumer at MobilePartyAi.cs:488 rejects the commit.
            // FrontPressure alone is a reliable proxy for "there is real
            // pressure here" because Plan 1's ClassifyFrontPressure (T14)
            // already folds the threatened-friendly count into the enum
            // classification — we do not need a raw count on the snapshot.
            if (snapshot.FrontPressure >= FrontPressure.High
                && bestInitiativeBehavior == AiBehavior.EngageParty)
            {
                EnlistedAiBiasHeartbeat.Record("initiative_bait_break",
                    (int)(bestInitiativeBehaviorScore * 100), 0);
                bestInitiativeBehaviorScore = 0f;
                return;
            }

            // Unviable pursuit: soft-break by multiplying down. Don't zero —
            // PursuitViability.Marginal may still be worth attempting.
            if (snapshot.PursuitViability == PursuitViability.NotViable
                && bestInitiativeBehavior == AiBehavior.EngageParty)
            {
                float biased = bestInitiativeBehaviorScore * 0.3f;
                EnlistedAiBiasHeartbeat.Record("initiative_pursuit_weak",
                    (int)(bestInitiativeBehaviorScore * 100), (int)(biased * 100));
                bestInitiativeBehaviorScore = biased;
                return;
            }

            // Severe-strain blanket: don't initiate ANY initiative when strain
            // is breaking. The lord should accept what his DefaultBehavior
            // already set (likely recovery) and stop probing for fights.
            if (snapshot.ArmyStrain == ArmyStrainLevel.Breaking
                || snapshot.RecoveryNeed == RecoveryNeed.High)
            {
                EnlistedAiBiasHeartbeat.Record("initiative_strain_block",
                    (int)(bestInitiativeBehaviorScore * 100), 0);
                bestInitiativeBehaviorScore = 0f;
                return;
            }
        }
```

**VERIFY IN DECOMPILE:** `AiBehavior.EngageParty` — confirm the enum value name at `TaleWorlds.CampaignSystem.Party/AiBehavior.cs`. The consumer threshold `bestInitiativeBehaviorScore > 1f` at `MobilePartyAi.cs:488` — confirm by reading lines 477-520 of the decompile. The `out` parameter signature is the full five-parameter form — re-verify against `MobilePartyAIModel.cs:37`.

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs
git commit -F <message-file>
```

Commit message:

```
feat(intelai): break bait magnetism via GetBestInitiativeBehavior (T13)

Three biases on the initiative-behavior score:
  - Zero when FrontPressure >= High AND a threatened friendly exists
    AND the candidate is EngageParty. Forces the lord's DefaultBehavior
    (defend/relieve) to dominate instead of the bait pull.
  - Multiply by 0.3 when PursuitViability == NotViable and the target
    is an EngageParty. Soft-breaks unwinnable chases without killing
    marginal-but-plausible ones.
  - Zero when ArmyStrain is Breaking or RecoveryNeed is High. The lord
    stops probing for fights and accepts the recovery his DefaultBehavior
    already chose.

Vanilla consumer at MobilePartyAi.cs:488 requires score > 1f to commit,
so zeroing is sufficient to reject.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T14: Document why other MobilePartyAI methods are pass-through

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs`

- [ ] **Step 1: Update the class doc comment**

Replace the class XML doc with:

```csharp
    /// <summary>
    /// Wraps vanilla MobilePartyAIModel. Biases three methods:
    /// ShouldConsiderAttacking (T11), ShouldConsiderAvoiding (T12), and
    /// GetBestInitiativeBehavior (T13). The other 12 abstract members are
    /// either global tuning constants (AiCheckInterval, radii, food thresholds)
    /// that apply to every AI lord, or behavior predicates already scoped by
    /// the three biased methods. Passing them through preserves enlisted-only
    /// scope.
    /// </summary>
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/Models/EnlistedMobilePartyAiModel.cs
git commit -F <message-file>
```

Commit message:

```
docs(intelai): record why 12 MobilePartyAI knobs pass through (T14)

Global tuning constants and already-scoped predicates not biased.
Class doc expanded so future sessions do not re-litigate.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase E — SettlementValueModel (skipped, with rationale)

### Task T15: Record the Phase E skip decision in the plan audit trail

**Files:** read-only verification — no code change expected.

Phase E is documented as skipped per the "Design decisions locked before task 1 → Phase E" subsection at the top of this plan. The rationale is:

- `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` already internally weighs settlement value (decompile: `DefaultTargetScoreCalculatingModel.cs:140-250`), and `DefaultSettlementValueModel.CalculateSettlementValueForEnemyHero` feeds into that score chain.
- Biasing both would double-count and make calibration fragile.
- Phase B's `EnlistedTargetScoreModel` already delivers the "relieve the threatened friendly before raiding" behavior through `GetTargetScoreForFaction` and `CalculatePatrollingScoreForSettlement`.

- [ ] **Step 1: Read the "Design decisions locked → Phase E" subsection of this plan**

Confirm the rationale is present and matches the decompile reality at the time of execution. If the decompile has been regenerated since plan authoring and the score-chain path has changed, revise the rationale inline and fold into a single commit below.

- [ ] **Step 2: Read the Phase H smoke report (when Phase H has run)**

If Phase H surfaces a bug where the enlisted lord still scores low-value settlements higher than threatened-friendly defense targets, open a Plan 2 follow-up task to wrap `SettlementValueModel` narrowly. Capture the evidence in the Phase H smoke report and link it in the follow-up task body.

- [ ] **Step 3: No code change, no commit** — this task is a skip-gate acknowledgement. Mark it done once the plan audit trail reflects the decision.

---

## Phase F — Narrow Harmony reserved investigation

### Task T16: Read Phase H smoke-test evidence and decide whether any narrow Harmony patch is required

**Files:** verification-only until Phase H runs. If Phase H indicates a patch is needed, this task expands into a code change.

- [ ] **Step 1: Read the `Session-A_*.log` entries captured during Phase H Task T24**

Look for heartbeat entries that indicate the wrappers fired but the lord still behaved badly — e.g., `initiative_bait_break` fired but the lord still chased the bait (because a higher-level vanilla behavior re-issued the command). Candidate failure-mode signatures:

- Vanilla `AiMilitaryBehavior` (or similar CampaignBehavior under `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors/`) issues `SetMoveEngageParty` directly, bypassing the initiative-behavior score.
- `MobilePartyAi.TickInternal` (decompile `MobilePartyAi.cs:427`) short-circuits based on a flag that our score-bias cannot reach.
- `Army.DailyTick` (check decompile for existence) ignores the per-party influence cost adjustment.

- [ ] **Step 2: If wrappers are sufficient, close out T16-T18 with a single no-op commit message**

Commit message template:

```
docs(intelai): Phase F narrow Harmony not required (T16-T18)

Phase H smoke testing at <campaign day range> confirmed the model
wrapper layer (Phases B/C/D) addresses all four design-spec §8.1-8.3
failure modes observed before shipping. No narrow Harmony patches
authored. EnlistedLordAiNarrowPatches.cs stays empty as documented.
Session-A log excerpts supporting this conclusion saved at
docs/Features/CampaignIntelligence/plan2-phase-h-log.md (if relevant).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

- [ ] **Step 3: If a patch is needed, identify the exact vanilla site and proceed to T17**

Record the specific method + file + line number in the Phase H smoke report. Do NOT proceed to T17 without primary-source evidence of the persisting failure mode.

---

### Task T17: Author the narrow Harmony patch (contingent — only if T16 identified a persistent failure)

**Files (if needed):**
- Modify: `src/Mod.GameAdapters/Patches/EnlistedLordAiNarrowPatches.cs`

This task is executed ONLY if T16 surfaced a specific vanilla site the wrappers cannot reach.

- [ ] **Step 1: Re-verify the target against `../Decompile/` before writing the patch**

```
Glob: ../Decompile/TaleWorlds.CampaignSystem/**/<candidate-file>.cs
```

Read the full method body. Confirm:
- The method is NOT already wrappable through a model-layer surface (if it IS, expand the model wrapper instead — stay in the preferred-order layering).
- The method signature (static vs instance, parameter order, return type).
- Whether the target should be a `HarmonyPrefix` (block the whole call) or `HarmonyPostfix` (let vanilla run, then amend the result).

**VERIFY IN DECOMPILE:** the specific method chosen in T16 — read the full body at the decompile site before writing any `[HarmonyPatch(typeof(...))]` attribute.

- [ ] **Step 2: Write the patch with the standard enlisted-only gate**

Template:

```csharp
using Enlisted.Features.CampaignIntelligence;
using Enlisted.Features.CampaignIntelligence.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
// plus whichever TaleWorlds namespace the patched type lives in

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Narrow Harmony patch against &lt;specific vanilla site&gt; to close
    /// a &lt;specific failure mode&gt; that the Plan 2 model wrappers cannot
    /// reach. See Plan 2 Phase F investigation notes for the evidence
    /// that required this patch.
    /// </summary>
    [HarmonyPatch(typeof(/* VERIFY IN DECOMPILE */), "/* method name */")]
    internal static class /* patch class name */
    {
        [HarmonyPrefix]
        [UsedImplicitly]
        public static bool Prefix(/* method args */)
        {
            if (!EnlistedAiGate.TryGetSnapshotForParty(/* the party arg */, out var snapshot))
            {
                return true; // not enlisted or not the lord — vanilla runs.
            }

            try
            {
                // Enlisted-only narrow correction. Example shape: block the
                // call if snapshot.FrontPressure says the lord should not be
                // doing this right now.
                // if (snapshot.X) return false;
                return true;
            }
            catch (System.Exception ex)
            {
                ModLogger.Caught("INTELAI", "narrow_patch_failed", ex);
                return true;
            }
        }
    }
}
```

- [ ] **Step 3: Build + regen error-codes + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
git add src/Mod.GameAdapters/Patches/EnlistedLordAiNarrowPatches.cs docs/error-codes.md
git commit -F <message-file>
```

Commit message (adjust specifics to the patch authored):

```
feat(intelai): narrow Harmony for <failure mode> (T17)

Model wrappers in Phases B/C/D did not reach <specific vanilla site>.
This patch gates <specific behavior> behind EnlistedAiGate.TryGetSnapshot
so non-enlisted parties continue to use vanilla behavior unchanged.
Phase H re-run confirms the failure mode is resolved.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T18: Close out Phase F — record final patch set and justification

**Files:**
- Modify: this plan's "Phase F closure record" section (added below).

- [ ] **Step 1: Append a one-paragraph closure entry below**

Under the heading `## Phase F closure record`, append:

- If no patch was needed: one sentence stating "Phase F authored no patches; model wrappers in Phases B/C/D sufficient. Phase H smoke-test campaign-day range <X-Y> confirmed."
- If a patch was authored in T17: one paragraph with the patched site, the failure mode observed, the evidence captured during Phase H Task T24, and the commit hash from T17.

- [ ] **Step 2: No standalone commit — fold into the T17 commit (or T16 no-patch commit).**

---

## Phase G — Observability + error handling

### Task T19: Fill out `EnlistedAiBiasHeartbeat.Record` — throttled ModLogger emitter

**Files:**
- Modify: `src/Features/CampaignIntelligence/Models/EnlistedAiBiasHeartbeat.cs`

Design: every bias site in Phases B/C/D calls `EnlistedAiBiasHeartbeat.Record(<reason>, <beforeValue>, <afterValue>)`. For Phase H to verify bias is firing, those calls must land in the session log. For non-Phase-H play, they must NOT flood the log (the AI tick runs every ~15 minutes per party and there are many parties). Throttling per-reason at 60s wall-clock matches `ModLogger.Expected`'s existing throttle pattern (CLAUDE.md "ModLogger.Expected key-throttled 60s window").

- [ ] **Step 1: Replace the stub with the emitter**

```csharp
namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Throttled bias-record emitter. Every Phase 2 wrapper bias call routes
    /// through Record(reason, before, after) so a single ModLogger.Expected
    /// per reason per 60s wall-clock window lands in the session log. Lets
    /// Phase H confirm wrappers fire without flooding the log during routine
    /// play.
    /// </summary>
    internal static class EnlistedAiBiasHeartbeat
    {
        public static void Record(string reason, int before, int after)
        {
            // ModLogger.Expected is already per-key 60s throttled (see
            // CLAUDE.md project convention). "reason" doubles as the key,
            // so each reason logs at most once per wall-clock minute.
            Enlisted.Mod.Core.Logging.ModLogger.Expected(
                "INTELAI",
                "bias_" + reason,
                "bias applied reason=" + reason + " before=" + before + " after=" + after);
        }

        public static void Record(string reason, float before, float after)
        {
            // Float overload avoids (int) cast loss for fractional values.
            // Uses the same throttle key path as the int overload.
            Enlisted.Mod.Core.Logging.ModLogger.Expected(
                "INTELAI",
                "bias_" + reason,
                "bias applied reason=" + reason
                + " before=" + before.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " after=" + after.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
```

Note: `ModLogger.Expected(category, key, summary)` — all three arguments are string literals except `summary`, which the registry scanner captures up to the first interpolation point. The summary here uses string-concat with runtime values after a string-literal prefix (`"bias applied reason="`), which matches the pattern at `src/Features/Activities/ActivityTypeCatalog.cs:23-26` (Plan 1 T19 uses the same pattern). Do NOT use `$"..."` interpolation — CLAUDE.md pitfall #20 rejects it.

**VERIFY IN DECOMPILE / LOCAL CODE:** `ModLogger.Expected(string category, string key, string summary)` signature — Plan 1 T19 used this shape; if Plan 1's final version introduced an overload with `ctx:` parameter, use the three-arg form without ctx.

- [ ] **Step 2: Build + regen error-codes**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
```

Expected: `docs/error-codes.md` gains an `E-INTELAI-NNN bias_*` family of throttled-info codes. Exact numeric codes depend on registry state — do not hand-edit.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/Models/EnlistedAiBiasHeartbeat.cs docs/error-codes.md
git commit -F <message-file>
```

Commit message:

```
feat(intelai): throttled bias-event heartbeat emitter (T19)

Record(reason, before, after) routes every Phase 2 wrapper bias
through ModLogger.Expected under the INTELAI category with a
per-reason 60s throttle key. Enables Phase H to verify wrappers fire
without flooding the log during routine play. Two overloads: int for
counted-list biases (call-to-army trim) and float for score biases
(target-score multiplier).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T20: Audit all ModLogger calls in Plan 2 for severity + literal-argument compliance

**Files:** read-only audit.

- [ ] **Step 1: Grep the new code for ModLogger calls**

```
Grep pattern: 'ModLogger\.(Surfaced|Caught|Expected|Debug)\(' in src/Features/CampaignIntelligence/Models/, src/Mod.GameAdapters/Patches/EnlistedLordAiNarrowPatches.cs
```

For each call, confirm:

- Category argument is the string literal `"INTELAI"`.
- Summary / key argument is a string literal (not `$"..."` interpolation, not a `private const string` — CLAUDE.md pitfalls #20 and the shared-const pitfall).
- Severity matches the usage:
  - `Surfaced` — `base_model_missing` (bootstrap-order regression, dev-visible).
  - `Caught` — `*_bias_failed` (defensive catches inside wrappers). These log only; player never sees a toast.
  - `Expected` — `bias_*` heartbeat entries. Throttled, info-level, no stack trace.
- No `ModLogger.Error` anywhere — the `.Error` method was retired on 2026-04-19.

Fix drift inline; do not roll multiple category/severity changes into one audit commit.

- [ ] **Step 2: Grep for any forbidden patterns**

```
Grep pattern: 'ModLogger\.Error' in src/Features/CampaignIntelligence/Models/, src/Mod.GameAdapters/Patches/EnlistedLordAiNarrowPatches.cs
Expected: zero matches
```

```
Grep pattern: 'ModLogger\.\w+\([^,]+,\s*\$' in src/Features/CampaignIntelligence/Models/
Expected: zero matches (no interpolated summaries)
```

- [ ] **Step 3: If fixes required, commit**

```bash
git add <touched files>
git commit -F <message-file>
```

Otherwise, no commit.

---

### Task T21: Regenerate error-codes registry after all Phase B-G edits

**Files:**
- Modify: `docs/error-codes.md` (auto-generated)

- [ ] **Step 1: Run the generator**

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
```

- [ ] **Step 2: Verify INTELAI codes landed**

```
Grep pattern: 'INTELAI' in docs/error-codes.md
```

Expected entries (approximate — generator picks numeric codes):

- `base_model_missing`
- `target_score_bias_failed`
- `influence_cost_bias_failed`
- `call_to_army_bias_failed`
- `narrow_patch_failed` (if T17 ran)
- `bias_target_score_defend`
- `bias_target_score_siege_strained`
- `bias_target_score_raid_suppressed`
- `bias_objective_pursuit_weak`
- `bias_objective_recovery_override`
- `bias_patrol_suppressed`
- `bias_call_to_army_suppressed`
- `bias_call_to_army_trimmed`
- `bias_influence_cost_strained`
- `bias_attack_abort_unviable`
- `bias_attack_abort_high_risk`
- `bias_avoid_recovery`
- `bias_avoid_unviable_pursuit`
- `bias_initiative_bait_break`
- `bias_initiative_pursuit_weak`
- `bias_initiative_strain_block`

- [ ] **Step 3: Stage and commit**

```bash
git add docs/error-codes.md
git commit -F <message-file>
```

Commit message:

```
chore(intelai): regenerate error-codes registry after Plan 2 logs (T21)

Registry now contains the INTELAI family: one Surfaced (base_model_missing),
three Caught (*_bias_failed), and ~15 Expected (bias_*) codes consumed by
the wrapper layer and heartbeat emitter.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

(If T19's regen already picked up every code and no lines shifted since, the registry is current — skip this commit.)

---

## Phase H — Smoke verification + sign-off

### Task T22: Register the four wrappers in `SubModule.cs`

**Files:**
- Modify: `src/Mod.Entry/SubModule.cs`

Four `AddModel<T>` calls plus the optional Harmony patch registration. The Harmony registration is automatic via `Harmony.PatchAll()` at `SubModule.cs:138` — no action needed for `EnlistedLordAiNarrowPatches` beyond its existence. Only `AddModel<T>` needs explicit registration.

- [ ] **Step 1: Locate the anchor**

Grep for Plan 1's `EnlistedCampaignIntelligenceBehavior` registration line (inserted by Plan 1 T7 after `campaignStarter.AddBehavior(new Features.Activities.Orders.PathScorer());` at approximately `SubModule.cs:362`). Plan 2's model registrations go immediately after that line, before the next `AddBehavior` (ENLISTMENT block at ~365).

- [ ] **Step 2: Insert the four `AddModel<T>` calls**

```csharp
                    // Plan 2 — Lord AI Intervention. Four MBGameModel wrappers
                    // that bias target choice, army formation, and pursuit for
                    // the enlisted lord only. Every wrapper calls EnlistedAiGate
                    // per-call and falls through to BaseModel for non-enlisted
                    // parties and non-lord parties. Gated behind
                    // EnlistmentBehavior.Instance.IsEnlisted via the gate;
                    // revert on unenlist is automatic.
                    campaignStarter.AddModel<TaleWorlds.CampaignSystem.ComponentInterfaces.TargetScoreCalculatingModel>(
                        new Features.CampaignIntelligence.Models.EnlistedTargetScoreModel());
                    campaignStarter.AddModel<TaleWorlds.CampaignSystem.ComponentInterfaces.ArmyManagementCalculationModel>(
                        new Features.CampaignIntelligence.Models.EnlistedArmyManagementModel());
                    campaignStarter.AddModel<TaleWorlds.CampaignSystem.ComponentInterfaces.MobilePartyAIModel>(
                        new Features.CampaignIntelligence.Models.EnlistedMobilePartyAiModel());
```

Note: `SettlementValueModel` registration is intentionally omitted per Phase E — the wrapper skeleton was not written.

**VERIFY IN DECOMPILE:** `CampaignGameStarter.AddModel<T>(MBGameModel<T>)` signature at `TaleWorlds.CampaignSystem/CampaignGameStarter.cs:76`. The generic constraint is `where T : GameModel`; `TargetScoreCalculatingModel` / `ArmyManagementCalculationModel` / `MobilePartyAIModel` all extend `MBGameModel<T>` which extends `GameModel` — the generic binding resolves automatically.

- [ ] **Step 3: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 4: Commit**

```bash
git add src/Mod.Entry/SubModule.cs
git commit -F <message-file>
```

Commit message:

```
feat(intelai): register Plan 2 wrappers in SubModule.OnGameStart (T22)

Three AddModel<T> calls wrap the vanilla TargetScoreCalculatingModel,
ArmyManagementCalculationModel, and MobilePartyAIModel. SettlementValueModel
is deliberately not wrapped — see Phase E rationale. CampaignGameStarter's
AddModel<T> auto-initializes BaseModel to the most-recent vanilla
registration, so bias delegates to vanilla whenever the gate returns false.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T23: Full build + content-validator pass

- [ ] **Step 1: Build clean**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS** with 0 warnings in `src/Features/CampaignIntelligence/Models/` and `src/Mod.GameAdapters/Patches/EnlistedLordAiNarrowPatches.cs`.

- [ ] **Step 2: Run content validator**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: **PASS** with 0 errors. Phase 10 validates the error-codes registry against `(category, file, line)` triples; if any file with `ModLogger.Surfaced/Caught/Expected` had lines shift during Phases B-G, T21's regen should have handled it — re-run the generator + validator if Phase 10 fails.

- [ ] **Step 3: Run repo lint stack**

```bash
./Tools/Validation/lint_repo.ps1
```

Expected: all 4 phases pass (.editorconfig / content validators / Ruff / PSScriptAnalyzer).

- [ ] **Step 4: Commit** — only if changes staged. Usually no-op after T21-T22.

---

### Task T24: In-game smoke test — 14 in-game days enlisted

**Purpose:** confirm the wrappers fire, bias lands on the enlisted lord's decisions, and non-enlisted lords continue to behave exactly as vanilla. Evidence is `Session-A_*.log` content.

- [ ] **Step 1: Close `BannerlordLauncher.exe`; launch the game fresh with the build from T23**

- [ ] **Step 2: Start a new campaign; play to enlistment with a lord**

Pick a lord whose faction is currently at war AND whose faction has friendly settlements near active hostile forces (check with the encyclopedia or scout around). The narrower the strategic window, the more clearly the bias will show in the log.

- [ ] **Step 3: Watch for Phase B bias entries**

Within the first few in-game days post-enlistment, the session log should show at least one of the `bias_target_score_*` entries. Typical cadence: one every 1-3 in-game hours while the lord makes target decisions.

Expected heartbeat keys (at minimum one of these within the first 3 in-game days if conditions fit):

- `bias_target_score_defend` — front under Medium+ pressure with threatened friendlies
- `bias_objective_pursuit_weak` — pursuit of a fleeing target
- `bias_patrol_suppressed` — patrol being deprioritized

- [ ] **Step 4: Watch for Phase C bias entries**

Over 5-10 in-game days, if the lord's party ever becomes a candidate army leader (varies by faction and clan rank), look for:

- `bias_call_to_army_suppressed` — empty list returned
- `bias_call_to_army_trimmed` — trimmed list returned
- `bias_influence_cost_strained` — raised cost

These fire less often than Phase B because call-to-army is a less frequent decision. Acceptable that they do not fire within a 14-day window if the lord's strain never reached the thresholds.

- [ ] **Step 5: Watch for Phase D bias entries — especially `bias_initiative_bait_break`**

Provoke (if campaign state allows): position the enlisted lord near a threatened friendly settlement AND have a hostile party within the AI's initiative-check radius. When vanilla would normally pull the lord toward the hostile party, the bait-break should fire and the log should show `bias_initiative_bait_break`.

Alternative provocation: if the lord's party takes meaningful casualties, look for `bias_initiative_strain_block` on the subsequent tick (strain should elevate to Breaking).

- [ ] **Step 6: Confirm non-enlisted lords unchanged**

Observe a non-enlisted lord party on the campaign map for ~10 in-game hours. Their pursuit/avoid/initiative decisions should look identical to pre-Plan-2 vanilla. No `INTELAI` heartbeat entries should reference a non-enlisted party (the gate blocks them before `Record` is called).

If ANY `INTELAI` heartbeat references a party other than the enlisted lord's, the identity gate in T2 has a bug — stop and investigate before signing off.

- [ ] **Step 7: Provoke enlistment-end (discharge, fire, retire, death)**

After unenlist, the next tick's `INTELAI` heartbeat entries should NOT appear. `EnlistedCampaignIntelligenceBehavior.Current` returns null once `IsEnlisted` is false; every gate call sees null and returns false; every wrapper falls through to BaseModel. Revert is automatic.

Confirm by watching the session log for 5-10 in-game minutes post-unenlist: zero new `INTELAI bias_*` entries.

- [ ] **Step 8: Save + reload during active enlistment**

Save mid-campaign while the heartbeats are firing. Reload. Expect:

- No exceptions at load.
- First post-load tick shows `INTELAI hourly_recompute` (Plan 1's heartbeat) — snapshot re-populates.
- First post-load bias call fires normally — `bias_*` entries resume.

- [ ] **Step 9: Record evidence**

Capture 3-5 `Session-A_*.log` excerpts showing at least: one `bias_target_score_defend` entry, one pass-through confirmation (non-enlisted party), and one clean post-unenlist silence. Paste into the Phase F closure record at T18.

- [ ] **Step 10: Sign-off**

If Steps 3-9 pass, Phase H is complete. If any step reveals a persistent §8.1-8.3 failure mode, return to Task T16 (Phase F investigation) before declaring the plan done.

- [ ] **Step 11: No commit** — verification only.

---

### Task T25: API corrections appendix + handoff sign-off

**Files:**
- Modify: this plan's "API Corrections Appendix" section below.

Every implementer subagent MUST verify prescribed APIs against `../Decompile/` before writing the call. When drift is found, append an entry here inline. Pattern matches Plan 1's appendix:

```
### T<task>: <member>

**Prescribed:** <plan code>
**Actual in decompile:** <real signature>
**Patch applied:** <one-sentence description + commit hash>
```

- [ ] **Step 1: After each task with a code block referencing a TaleWorlds API, run the verification step described in that task.**

- [ ] **Step 2: If drift is found, patch the code inline AND add an entry to the appendix.**

- [ ] **Step 3: No standalone commit solely for appendix edits** — fold into the same commit as the code fix.

- [ ] **Step 4: Update CLAUDE.md "Current project status" paragraph**

Add a single sentence referencing Plan 2 completion:

```
- **Plan 2 (Lord AI Intervention)** — ✅ shipped on `development`. Wraps
  TargetScoreCalculatingModel / ArmyManagementCalculationModel /
  MobilePartyAIModel; gates every override on enlisted-lord identity.
  Phase H smoke verified <date>.
```

- [ ] **Step 5: Commit CLAUDE.md change**

```bash
git add CLAUDE.md
git commit -F <message-file>
```

Commit message:

```
docs(plan2): record Plan 2 completion in project status (T25)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## API Corrections Appendix

_(Implementers append entries here as they discover plan-vs-decompile drift. Empty at plan-authoring time.)_

---

## Phase F closure record

_(T18 appends one paragraph here at Phase F closure. Empty at plan-authoring time.)_

---

## Self-review log (plan author)

Completed before handoff:

1. **Spec coverage (design spec §8):**
   - §8.1 Movement and pursuit → Phase D (T11-T13): `ShouldConsiderAttacking` / `ShouldConsiderAvoiding` / `GetBestInitiativeBehavior` biases.
   - §8.2 Strategic target choice → Phase B (T5-T7): `GetTargetScoreForFaction` / `CurrentObjectiveValue` / `CalculatePatrollingScoreForSettlement` biases.
   - §8.3 Army management → Phase C (T8-T9): `GetMobilePartiesToCallToArmy` / `CalculatePartyInfluenceCost` biases.
   - §8.4 Information use — partially covered via Phase B `CurrentObjectiveValue` bias on low-confidence objectives. Deeper §8.4 work (recon-driven confidence boost, player-report influence) belongs to Plans 3 and 4 where the player surface for information supply exists.
   - §8.5 Intervention layers — preferred order followed: `TargetScoreCalculatingModel` (B) → `ArmyManagementCalculationModel` (C) → `MobilePartyAIModel` (D) → `SettlementValueModel` (E, skipped with rationale) → narrow Harmony (F, reserved investigation).

2. **Hard constraints honored:**
   - No `GameModel` replacement beyond the three (not four) model wrappers listed — §13 non-goal.
   - No storylets / JSON / duty emission — Plans 3/4 own content.
   - No signal projection — Plan 3 owns.
   - Every wrapper method gated per-call on `EnlistedAiGate.TryGetSnapshotForParty`.
   - `EnlistmentBehavior.Instance.IsEnlisted` false → gate returns false → wrapper falls through to BaseModel. Revert is hard and immediate.
   - Non-enlisted lords unaffected — the gate fails for any party whose reference is not `lord.PartyBelongedTo`.
   - Plan 1's snapshot consumed read-only via `EnlistedCampaignIntelligenceBehavior.Instance.Current` — never mutated.

3. **Placeholder scan:** every step has concrete code or a concrete command. "TBD" / "TODO" / "implement later" do not appear in any task body. The `FrontPressure`-as-proxy decision for T5 and T13 (in lieu of a raw threatened-count on the snapshot) is documented in the task bodies and justified by reference to Plan 1's classifier — not left as a gap.

4. **Type consistency:**
   - `EnlistedAiGate.TryGetSnapshotForParty` — used in T2 (definition), T5-T13 (consumers).
   - `EnlistedAiGate.IsEnlistedLordArmyLeader` — used in T2 (definition), T8, T11.
   - `EnlistedAiBiasHeartbeat.Record(string, int, int)` and `Record(string, float, float)` — used consistently from T5 through T13; overloads defined in T19.
   - Wrapper class names: `EnlistedTargetScoreModel` / `EnlistedArmyManagementModel` / `EnlistedMobilePartyAiModel` — used consistently from T3 through T22.
   - Snapshot field references: `Posture`, `Objective`, `FrontPressure`, `ArmyStrain`, `SupplyPressure`, `PursuitViability`, `RecoveryNeed`, `EnemyContactRisk` — all published on the snapshot per Plan 1 T3 (enums only + `CampaignTime`, no raw counts or references). T5 and T13 key defend/relief and bait-break biases on `FrontPressure` alone because Plan 1's `ClassifyFrontPressure` (T14) already folds the threatened-friendly count into the enum classification. `NearestThreatenedFriendly` (Settlement ref) and `ThreatenedFriendlySettlementCount` (int) live only on the transient `IntelligenceInputs` (Plan 1 T8) — not published. If smoke-testing shows the `FrontPressure` proxy too coarse, file a Plan-1-follow-up to publish a `ThreatenedFriendlyCount` byte rather than hot-patching Plan 2's scope.

5. **Decompile-authoritative verification coverage:** every task with a TaleWorlds API reference has either a decompile file+line citation or a `VERIFY IN DECOMPILE:` callout. The callouts are preserved for implementer verification — they are not drafting gaps.

---

## Handoff to next plan

When Plan 2 signs off (T24 green):

- **Plan 3 (Signal Projection)** can begin in parallel — it does not share code surface with Plan 2 beyond additive `SubModule.cs` and `Enlisted.csproj` entries (different lines). Plan 3 reads the same Plan 1 snapshot accessor.
- **Plan 4 (Duty Opportunities)** can begin in parallel — same independence from Plan 2.
- **Plan 5 (Career Loop Closure)** path-crossroads portion (Tasks 44-52 from the old Orders plan) is Intelligence-independent and can begin in parallel. Plan 5's late phase (culture / trait overlays) queues until Plan 4's content ships.

Plan 2 adds no new snapshot fields. If any downstream consumer needs a field Plan 1 did not publish (e.g., a raw `ThreatenedFriendlyCount` byte, or a specific `Settlement` reference like a `NearestThreatenedFriendlyId`), that is filed as a Plan-1-amendment task on Plan 1, not hot-patched into Plan 2. Plan 2's T5 and T13 explicitly accept the `FrontPressure`-as-proxy form and will only need such an amendment if smoke-testing reveals the proxy is too coarse.
