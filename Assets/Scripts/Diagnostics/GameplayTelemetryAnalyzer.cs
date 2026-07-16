#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

public static class GameplayTelemetryAnalyzer
{
    public static GameplayTelemetrySummary BuildSummary(
        IReadOnlyList<GameplayDayTelemetry> days,
        int basePCCount = 5)
    {
        GameplayTelemetrySummary summary = new GameplayTelemetrySummary();
        if (days == null || days.Count == 0) return summary;

        summary.firstDay = days[0].day;
        summary.lastDay = days[^1].day;
        summary.daysAnalyzed = days.Count;
        summary.startingBalance = days[0].startBalance;
        summary.endingBalance = days[^1].endBalance;

        foreach (GameplayDayTelemetry day in days)
        {
            summary.totalRevenue += day.revenue;
            summary.totalBonuses += day.bonuses;
            summary.totalExpenses += day.expenses;
            summary.totalNetResult += day.netResult;
            summary.totalServedClients += day.servedClients;
            summary.totalLostClients += day.lostClients;
        }

        GameplayDayTelemetry last = days[^1];
        summary.finalClubLevel = last.clubLevel;
        summary.finalReputation = last.reputation;
        summary.finalPCCount = last.accessiblePCCount > 0
            ? last.accessiblePCCount
            : basePCCount + last.purchasedPCCount;
        summary.finalUnlockedRooms = last.unlockedRoomCount;
        summary.finalResearchLevels = last.researchLevels;
        return summary;
    }

    public static List<GameplayTelemetryWarning> BuildWarnings(
        IReadOnlyList<GameplayDayTelemetry> days)
    {
        List<GameplayTelemetryWarning> warnings = new();
        if (days == null || days.Count == 0) return warnings;

        AddConsecutiveThresholdWarning(
            days,
            day => day.endBalance < -300,
            3,
            "BALANCE_WARNING",
            "Balance stayed below -300 RUB for at least three days.",
            warnings
        );
        AddConsecutiveThresholdWarning(
            days,
            day => day.reputation < 10,
            3,
            "REPUTATION_LOCK",
            "Reputation stayed below 10 for at least three days.",
            warnings
        );

        for (int index = 0; index < days.Count; index++)
        {
            GameplayDayTelemetry day = days[index];
            int categorizedLosses = day.priceLostClients +
                day.capacityLostClients + day.queueOverflowClients;
            if (categorizedLosses > 0 &&
                (float)day.priceLostClients / categorizedLosses > 0.4f)
            {
                AddWarning(
                    warnings,
                    "PRICE_REJECTION_HIGH",
                    day.day,
                    day.day,
                    $"{day.priceLostClients} of {categorizedLosses} categorized losses were caused by pricing."
                );
            }

            float maximumUtilization = Mathf.Max(
                day.basicUtilization,
                day.gamingUtilization,
                day.premiumUtilization
            );
            bool repeatedCapacityLoss = day.capacityLostClients > 0 &&
                index > 0 && days[index - 1].capacityLostClients > 0;
            if (maximumUtilization > 90f && repeatedCapacityLoss)
            {
                AddWarning(
                    warnings,
                    "CAPACITY_LIMIT",
                    days[index - 1].day,
                    day.day,
                    $"Utilization reached {maximumUtilization:F0}% with repeated capacity losses."
                );
            }

            if (day.missedConsumableSales > 10)
            {
                AddWarning(
                    warnings,
                    "INVENTORY_LOSS",
                    day.day,
                    day.day,
                    $"Missed consumable sales: {day.missedConsumableSales}."
                );
            }

            if (day.staffExpenses > 0 &&
                day.staffExpenses > day.staffPreventedLossEstimate)
            {
                AddWarning(
                    warnings,
                    "STAFF_NOT_PROFITABLE",
                    day.day,
                    day.day,
                    $"Staff cost {day.staffExpenses} RUB exceeded estimated prevented losses {day.staffPreventedLossEstimate} RUB."
                );
            }
        }

        AddResearchGrowthWarning(days, warnings);
        return warnings;
    }

    private static void AddResearchGrowthWarning(
        IReadOnlyList<GameplayDayTelemetry> days,
        List<GameplayTelemetryWarning> warnings)
    {
        for (int index = 2; index < days.Count - 1; index++)
        {
            if (days[index].researchLevels <= days[index - 1].researchLevels)
                continue;

            float before = (days[index - 1].sessionRevenue +
                days[index - 2].sessionRevenue) / 2f;
            float after = (days[index].sessionRevenue +
                days[index + 1].sessionRevenue) / 2f;
            if (before <= 0f) continue;

            float increase = after / before - 1f;
            if (increase > 0.25f)
            {
                AddWarning(
                    warnings,
                    "RESEARCH_TOO_STRONG",
                    days[index].day,
                    days[index + 1].day,
                    $"Session revenue increased by {increase * 100f:F0}% around a research purchase."
                );
            }
        }
    }

    private static void AddConsecutiveThresholdWarning(
        IReadOnlyList<GameplayDayTelemetry> days,
        System.Func<GameplayDayTelemetry, bool> predicate,
        int requiredDays,
        string code,
        string details,
        List<GameplayTelemetryWarning> warnings)
    {
        int streakStart = -1;
        for (int index = 0; index <= days.Count; index++)
        {
            bool matches = index < days.Count && predicate(days[index]);
            if (matches && streakStart < 0) streakStart = index;
            if (matches) continue;

            if (streakStart >= 0 && index - streakStart >= requiredDays)
            {
                AddWarning(
                    warnings,
                    code,
                    days[streakStart].day,
                    days[index - 1].day,
                    details
                );
            }
            streakStart = -1;
        }
    }

    private static void AddWarning(
        List<GameplayTelemetryWarning> warnings,
        string code,
        int firstDay,
        int lastDay,
        string details)
    {
        warnings.Add(new GameplayTelemetryWarning
        {
            code = code,
            firstDay = firstDay,
            lastDay = lastDay,
            details = details
        });
    }
}
#endif
