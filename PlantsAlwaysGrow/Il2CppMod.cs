using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Persistence;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(PlantsAlwaysGrow.PlantsAlwaysGrowMod), "PlantsAlwaysGrowMod", "1.2.3", "lasersquid")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PlantsAlwaysGrow;

// IL2CPP replacement for the 1.2.2 Harmony patches. No detours, delegate reflection,
// or moving game callbacks between onMinutePass/onUncappedMinutePass lists.
public sealed class PlantsAlwaysGrowMod : MelonMod
{
    private readonly MinuteClock clock = new();
    private bool mainScene;
    private bool faulted;
    private bool nightLogged;
    private IntPtr lastTimeManager;
    private MelonPreferences_Entry<bool> enabled = null!;
    private MelonPreferences_Entry<bool> mushrooms = null!;
    private MelonPreferences_Entry<bool> diagnostics = null!;
    private float nextDiagnosticTime;

    public override void OnInitializeMelon()
    {
        var category = MelonPreferences.CreateCategory("PlantsAlwaysGrow");
        enabled = category.CreateEntry("Enabled", true);
        mushrooms = category.CreateEntry("GrowMushrooms", true);
        diagnostics = category.CreateEntry("DiagnosticLogging", false);
        LoggerInstance.Msg("IL2CPP growth initialized. No Harmony patches; growth only at 04:00 on the host.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Main") return;
        mainScene = true;
        faulted = false;
        ResetClock();
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Main") return;
        mainScene = false;
        ResetClock();
    }

    private void ResetClock()
    {
        clock.Reset();
        nightLogged = false;
        lastTimeManager = IntPtr.Zero;
    }

    public override void OnUpdate()
    {
        if (!mainScene || faulted || !enabled.Value)
        {
            ResetClock();
            return;
        }

        try
        {
            var loader = Singleton<LoadManager>.Instance;
            if (loader == null || !loader.IsGameLoaded || loader.IsLoading || !InstanceFinder.IsServer)
            {
                ResetClock();
                return;
            }

            var time = NetworkSingleton<TimeManager>.Instance;
            if (time == null)
            {
                ResetClock();
                return;
            }
            if (time.Pointer != lastTimeManager)
            {
                ResetClock();
                lastTimeManager = time.Pointer;
            }

            bool allowed = GrowthPolicy.CanAdvance(true, true, time.IsEndOfDay,
                time.IsSleepInProgress, time.CurrentTime) && time.TimeSpeedMultiplier > 0;
            int minutes = clock.Advance(allowed, Time.deltaTime,
                TimeManager.MinuteDuration / time.TimeSpeedMultiplier);
            if (!allowed)
            {
                nightLogged = false;
                return;
            }
            if (minutes == 0) return;

            // Fresh objects on every growth step: no plant/pot wrappers survive scene loads,
            // harvesting, replacing pots, or placing new ones.
            int advanced = GrowPlants(minutes);
            if (mushrooms.Value) advanced += GrowMushrooms(minutes);

            if (!nightLogged)
            {
                LoggerInstance.Msg($"04:00 growth active; host advanced {advanced} plants/colonies. Clock remains {time.CurrentTime:0000}.");
                nightLogged = true;
            }
            if (diagnostics.Value && Time.unscaledTime >= nextDiagnosticTime)
            {
                LoggerInstance.Msg($"Growth tick: clock={time.CurrentTime:0000}, minutes={minutes}, advanced={advanced}");
                nextDiagnosticTime = Time.unscaledTime + 10;
            }
        }
        catch (Exception error)
        {
            // Managed failures disable growth for the current scene; do not spam,
            // retry a broken update forever, or swallow other mods' exceptions.
            faulted = true;
            ResetClock();
            LoggerInstance.Error($"Growth disabled for this scene after an error: {error}");
        }
    }

    private static int GrowPlants(int minutes)
    {
        int advanced = 0;
        foreach (var pot in UnityEngine.Object.FindObjectsOfType<Pot>())
        {
            if (pot == null || !pot.isActiveAndEnabled) continue;
            var plant = pot.Plant;
            if (plant == null || !plant.isActiveAndEnabled || plant.Pot == null ||
                plant.Pot.Pointer != pot.Pointer || pot.NormalizedMoistureAmount <= 0) continue;

            float before = plant.NormalizedGrowthProgress;
            if (before >= 1) continue;

            // This native method has a real body in the installed f13 build. Use the
            // game's lighting calculation instead of the original mod's private copy.
            float exposure = pot.GetAverageLightExposure(out float lightMultiplier);
            float multiplier = pot.GetTemperatureGrowthMultiplier() * exposure *
                pot.GrowSpeedMultiplier * lightMultiplier;
            if (GameManager.IS_TUTORIAL) multiplier *= 0.3f;

            float after = GrowthPolicy.NextProgress(before, minutes, plant.GrowthTime, multiplier);
            if (!(after > before)) continue;
            plant.SetNormalizedGrowthProgress(after);
            // Built-in observers RPC: clients receive the host's absolute value, never
            // another increment. The host remains authoritative with any lobby size.
            pot.SetGrowthProgress_Client(after);
            advanced++;
        }
        return advanced;
    }

    private static int GrowMushrooms(int minutes)
    {
        int advanced = 0;
        foreach (var colony in UnityEngine.Object.FindObjectsOfType<ShroomColony>())
        {
            if (colony == null || !colony.isActiveAndEnabled || colony._parentBed == null ||
                !colony._parentBed.isActiveAndEnabled || colony.IsFullyGrown) continue;

            colony.CheckTemperature();
            float before = colony.GrowthProgress;
            float after = GrowthPolicy.NextProgress(before, minutes, colony._growTime,
                colony.GetCurrentGrowthRate());
            if (!(after > before)) continue;
            // Existing observers RPC is RunLocally=true and handles both host and peers.
            colony.SetGrowthPercentage_Local(null, after);
            advanced++;
        }
        return advanced;
    }
}
