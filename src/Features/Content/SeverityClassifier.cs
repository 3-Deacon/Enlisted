using System.Collections.Generic;

namespace Enlisted.Features.Content
{
	/// <summary>
	/// Scores a StoryCandidate and caps its tier. Score = SeverityHint + max beat weight
	/// in the candidate's Beats set + player-stake bonuses (enlisted lord, kingdom, visited
	/// settlement). Final tier is max(candidate.ProposedTier, score-derived-tier), bounded
	/// by the strictest BeatMaxTier among the candidate's beats.
	/// </summary>
	public static class SeverityClassifier
	{
		private static readonly Dictionary<StoryBeat, float> BeatWeights = new Dictionary<StoryBeat, float>
		{
			{ StoryBeat.LordCaptured, 0.95f },
			{ StoryBeat.LordKilled, 0.95f },
			{ StoryBeat.PaydayBeat, 0.90f },
			{ StoryBeat.CompanyCrisisThreshold, 0.85f },
			{ StoryBeat.EscalationThreshold, 0.80f },
			{ StoryBeat.LordMajorBattleEnd, 0.70f },
			{ StoryBeat.SiegeBegin, 0.55f },
			{ StoryBeat.SiegeEnd, 0.55f },
			{ StoryBeat.OrderComplete, 0.50f },
			{ StoryBeat.WarDeclared, 0.45f },
			{ StoryBeat.PeaceSigned, 0.40f },
			{ StoryBeat.ArmyFormed, 0.35f },
			{ StoryBeat.ArmyDispersed, 0.35f },
			{ StoryBeat.LordMajorBattleStart, 0.35f },
			{ StoryBeat.PlayerBattleEnd, 0.30f },
			{ StoryBeat.CompanyPressureThreshold, 0.25f },
			{ StoryBeat.SettlementEntered, 0.15f },
			{ StoryBeat.SettlementLeft, 0.10f },
			{ StoryBeat.OrderPhaseTransition, 0.05f },
			{ StoryBeat.QuietStretchTimeout, 0.70f }
		};

		private static readonly Dictionary<StoryBeat, StoryTier> BeatMaxTier = new Dictionary<StoryBeat, StoryTier>
		{
			{ StoryBeat.LordCaptured, StoryTier.Modal },
			{ StoryBeat.LordKilled, StoryTier.Modal },
			{ StoryBeat.LordMajorBattleEnd, StoryTier.Modal },
			{ StoryBeat.PaydayBeat, StoryTier.Modal },
			{ StoryBeat.CompanyCrisisThreshold, StoryTier.Modal },
			{ StoryBeat.EscalationThreshold, StoryTier.Modal },
			{ StoryBeat.OrderComplete, StoryTier.Modal },
			{ StoryBeat.QuietStretchTimeout, StoryTier.Modal },
			{ StoryBeat.WarDeclared, StoryTier.Headline },
			{ StoryBeat.PeaceSigned, StoryTier.Headline },
			{ StoryBeat.ArmyFormed, StoryTier.Headline },
			{ StoryBeat.ArmyDispersed, StoryTier.Headline },
			{ StoryBeat.SiegeBegin, StoryTier.Headline },
			{ StoryBeat.SiegeEnd, StoryTier.Headline },
			{ StoryBeat.LordMajorBattleStart, StoryTier.Headline },
			{ StoryBeat.CompanyPressureThreshold, StoryTier.Pertinent },
			{ StoryBeat.SettlementEntered, StoryTier.Pertinent },
			{ StoryBeat.SettlementLeft, StoryTier.Pertinent },
			{ StoryBeat.PlayerBattleEnd, StoryTier.Pertinent },
			{ StoryBeat.OrderPhaseTransition, StoryTier.Log }
		};

		public static StoryTier Classify(StoryCandidate c, bool touchesEnlistedLord, bool touchesPlayerKingdom, bool recentlyVisitedSettlement)
		{
			float score = c.SeverityHint;

			float maxBeatWeight = 0f;
			StoryTier tierCap = StoryTier.Log;
			foreach (var beat in c.Beats)
			{
				if (BeatWeights.TryGetValue(beat, out var w) && w > maxBeatWeight)
				{
					maxBeatWeight = w;
				}
				if (BeatMaxTier.TryGetValue(beat, out var cap) && (int)cap > (int)tierCap)
				{
					tierCap = cap;
				}
			}
			score += maxBeatWeight;

			if (touchesEnlistedLord) score += 0.15f;
			if (touchesPlayerKingdom) score += 0.10f;
			if (recentlyVisitedSettlement) score += 0.05f;

			StoryTier byScore =
				score >= 0.70f ? StoryTier.Modal :
				score >= 0.35f ? StoryTier.Headline :
				score >= 0.10f ? StoryTier.Pertinent :
								 StoryTier.Log;

			StoryTier maxOfProposedAndScore = (int)c.ProposedTier > (int)byScore ? c.ProposedTier : byScore;
			return (int)maxOfProposedAndScore > (int)tierCap ? tierCap : maxOfProposedAndScore;
		}
	}
}
