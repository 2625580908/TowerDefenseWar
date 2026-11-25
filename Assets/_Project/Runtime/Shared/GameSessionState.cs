using System;

namespace MRStrategyRTS
{
    /// <summary>
    /// Runtime-only state shared by systems that need to know whether a level is in progress.
    /// It intentionally has no scene or prefab references.
    /// </summary>
    public static class GameSessionState
    {
        public static bool IsLevelRunning { get; private set; }
        public static bool IsBattlePaused { get; private set; }
        public static event Action EconomyChanged;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsLevelRunning = false;
            IsBattlePaused = false;
            canAffordGold = null;
            trySpendGold = null;
            addGold = null;
            EconomyChanged = null;
        }

        private static Func<int, bool> canAffordGold;
        private static Func<int, bool> trySpendGold;
        private static Action<int> addGold;

        public static void SetLevelRunning(bool isRunning)
        {
            IsLevelRunning = isRunning;
        }

        public static void SetBattlePaused(bool paused)
        {
            if (IsBattlePaused == paused) return;
            IsBattlePaused = paused;
            EconomyChanged?.Invoke();
        }

        public static void RegisterEconomy(
            Func<int, bool> canAfford,
            Func<int, bool> spendGold,
            Action<int> addGoldAction)
        {
            canAffordGold = canAfford;
            trySpendGold = spendGold;
            addGold = addGoldAction;
            EconomyChanged?.Invoke();
        }

        public static void ClearEconomy()
        {
            canAffordGold = null;
            trySpendGold = null;
            addGold = null;
            EconomyChanged?.Invoke();
        }

        public static void NotifyGoldChanged(int gold) => EconomyChanged?.Invoke();

        public static bool CanAffordGold(int goldCost)
        {
            return !IsBattlePaused && goldCost >= 0 && canAffordGold != null && canAffordGold(goldCost);
        }

        public static bool TrySpendGold(int goldCost)
        {
            return !IsBattlePaused && (goldCost == 0 || trySpendGold != null && trySpendGold(goldCost));
        }

        public static void AddGold(int goldAmount)
        {
            if (goldAmount > 0)
            {
                addGold?.Invoke(goldAmount);
            }
        }
    }
}
