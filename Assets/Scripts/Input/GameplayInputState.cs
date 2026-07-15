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

            return pauseBlocksInput || maintenanceBlocksInput;
        }
    }
}
