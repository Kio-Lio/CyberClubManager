public static class GameplayInputState
{
    public static bool IsBlocked
    {
        get
        {
            bool pauseBlocksInput =
                PauseMenuController.Instance != null &&
                PauseMenuController.Instance.BlocksGameplayInput;
            bool maintenanceBlocksInput =
                PCMaintenancePanel.Instance != null &&
                PCMaintenancePanel.Instance.IsOpen;
            bool pricingBlocksInput =
                PricingPanel.Instance != null &&
                PricingPanel.Instance.IsOpen;
            bool stockPanelBlocksInput =
                ConsumableStockPanel.Instance != null &&
                ConsumableStockPanel.Instance.IsOpen;
            bool reportBlocksInput =
                DailyFinancialReportPanel.Instance != null &&
                DailyFinancialReportPanel.Instance.IsOpen;
            bool marketingBlocksInput =
                MarketingPanel.Instance != null &&
                MarketingPanel.Instance.IsOpen;
            bool analyticsBlocksInput =
                DemandAnalyticsPanel.Instance != null &&
                DemandAnalyticsPanel.Instance.IsOpen;
            bool randomEventPanelBlocksInput =
                ClubRandomEventPanel.Instance != null &&
                ClubRandomEventPanel.Instance.IsOpen;
            bool internetPanelBlocksInput =
                InternetProviderPanel.Instance != null &&
                InternetProviderPanel.Instance.IsOpen;

            return pauseBlocksInput || maintenanceBlocksInput ||
                pricingBlocksInput || stockPanelBlocksInput || reportBlocksInput ||
                marketingBlocksInput || analyticsBlocksInput ||
                randomEventPanelBlocksInput || internetPanelBlocksInput;
        }
    }
}
