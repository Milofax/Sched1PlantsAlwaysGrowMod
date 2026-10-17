using PlantsAlwaysGrow;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Persistence;
using Il2CppFishNet;
using UnityEngine;

int passed = 0;
void Test(string name, Action check)
{
    check();
    Console.WriteLine("PASS " + name);
    passed++;
}
void Require(bool condition, string message = "assertion failed")
{
    if (!condition) throw new Exception(message);
}
void Near(float actual, float expected) => Require(Math.Abs(actual-expected) < 0.00001f, $"expected {expected}, got {actual}");
(PlantsAlwaysGrowMod mod, Pot pot, Plant plant, TimeManager time, LoadManager loader, SleepController sleep) Setup()
{
    UnityEngine.Object.SceneObjects.Clear();
    UnityEngine.Object.Scans = 0;
    Time.deltaTime = 1;
    TimeManager.MinuteDuration = 1;
    GameManager.IS_TUTORIAL = false;
    InstanceFinder.IsServer = true;
    var time = new TimeManager();
    var loader = new LoadManager();
    var sleep = new SleepController();
    NetworkSingleton<TimeManager>.Instance = time;
    NetworkSingleton<SleepController>.Instance = sleep;
    Singleton<LoadManager>.Instance = loader;
    var pot = new Pot();
    var plant = new Plant { Pot = pot };
    pot.Plant = plant;
    UnityEngine.Object.SceneObjects.Add(pot);
    var mod = new PlantsAlwaysGrowMod();
    mod.OnInitializeMelon();
    mod.OnSceneWasLoaded(1,"Main");
    return (mod,pot,plant,time,loader,sleep);
}

Test("Daytime updates never read or update plants", () => {
    var s = Setup(); s.time.CurrentTime = 2110; s.time.IsEndOfDay = false; s.pot.ThrowOnLight = true;
    for(int i=0;i<90;i++) s.mod.OnUpdate();
    Near(s.plant.NormalizedGrowthProgress,0.2f); Require(UnityEngine.Object.Scans==0); Require(s.mod.LoggerInstance.Errors.Count==0);
});
Test("At 04:00 one minute grows a one-hour plant by 1/60; clock does not change", () => {
    var s=Setup(); s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,0.2f+1f/60);
    Require(s.time.CurrentTime==400); Require(s.pot.SentValues.Count==1); Near(s.pot.SentValues[0],s.plant.NormalizedGrowthProgress);
});
Test("Multiplayer clients perform no growth and send no RPC", () => {
    var s=Setup(); InstanceFinder.IsServer=false;
    for(int i=0;i<90;i++) s.mod.OnUpdate();
    Near(s.plant.NormalizedGrowthProgress,0.2f); Require(s.pot.SentValues.Count==0); Require(UnityEngine.Object.Scans==0);
});
Test("Water, light and temperature each block growth when absent", () => {
    foreach(string factor in new[]{"water","light","temperature"}) {
        var s=Setup();
        if(factor=="water") s.pot.NormalizedMoistureAmount=0;
        if(factor=="light") s.pot.Exposure=0;
        if(factor=="temperature") s.pot.TemperatureMultiplier=0;
        s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,0.2f); Require(s.pot.SentValues.Count==0);
    }
});
Test("Environmental, additive and tutorial multipliers combine", () => {
    var s=Setup(); s.pot.GrowSpeedMultiplier=2; s.pot.Exposure=0.5f; s.pot.TemperatureMultiplier=0.8f;
    s.pot.LightMultiplier=1.5f; GameManager.IS_TUTORIAL=true; s.mod.OnUpdate();
    Near(s.plant.NormalizedGrowthProgress,0.2f+1f/60*2*0.5f*0.8f*1.5f*0.3f);
});
Test("Two elapsed minutes produce twice the growth, fixing the old inverse-minute formula", () => {
    var s=Setup(); s.time.TimeSpeedMultiplier=2; s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,0.2f+2f/60);
});
Test("A pause produces no growth and no resume catch-up", () => {
    var s=Setup(); Time.deltaTime=0;
    for(int i=0;i<120;i++) s.mod.OnUpdate();
    Near(s.plant.NormalizedGrowthProgress,0.2f); Time.deltaTime=1; s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,0.2f+1f/60);
});
Test("Sleeping and loading reset partial minute credit", () => {
    foreach(bool loading in new[]{false,true}) {
        var s=Setup(); Time.deltaTime=0.5f; s.mod.OnUpdate();
        if(loading) s.loader.IsLoading=true; else s.sleep.IsSleepInProgress=true;
        s.mod.OnUpdate(); s.loader.IsLoading=false; s.sleep.IsSleepInProgress=false; s.mod.OnUpdate();
        Near(s.plant.NormalizedGrowthProgress,0.2f); s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,0.2f+1f/60);
    }
});
Test("Scene reload resets timing and never visits an old removed pot", () => {
    var s=Setup(); Time.deltaTime=0.5f; s.mod.OnUpdate(); s.mod.OnSceneWasUnloaded(1,"Main");
    UnityEngine.Object.SceneObjects.Clear(); s.pot.ThrowOnLight=true;
    s.mod.OnSceneWasLoaded(1,"Main"); s.mod.OnUpdate(); s.mod.OnUpdate();
    Near(s.plant.NormalizedGrowthProgress,0.2f); Require(s.mod.LoggerInstance.Errors.Count==0);
});
Test("Newly placed pot is picked up on the next step; harvested pot is skipped", () => {
    var s=Setup(); UnityEngine.Object.SceneObjects.Clear(); s.mod.OnUpdate();
    UnityEngine.Object.SceneObjects.Add(s.pot); s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,0.2f+1f/60);
    s.pot.Plant=null; s.mod.OnUpdate(); Require(s.pot.SentValues.Count==1);
});
Test("Replaced plants and plants in inactive pots do not receive growth", () => {
    var s=Setup(); s.plant.Pot=new Pot(); s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,0.2f);
    s.plant.Pot=s.pot; s.pot.isActiveAndEnabled=false; s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,0.2f);
});
Test("A plant the game has hidden still grows in its active pot", () => {
    var s=Setup(); s.plant.isActiveAndEnabled=false; s.mod.OnUpdate();
    Near(s.plant.NormalizedGrowthProgress,0.2f+1f/60); Require(s.pot.SentValues.Count==1);
});
Test("Fully grown plants stop at 100 percent with no repeated RPC", () => {
    var s=Setup(); s.plant.NormalizedGrowthProgress=0.999f;
    s.mod.OnUpdate(); s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,1); Require(s.pot.SentValues.Count==1);
});
(MushroomBed bed, ShroomColony colony) Bed()
{
    var bed = new MushroomBed();
    var colony = new ShroomColony { _parentBed = bed };
    bed.CurrentColony = colony;
    UnityEngine.Object.SceneObjects.Add(bed);
    UnityEngine.Object.SceneObjects.Add(colony);
    return (bed, colony);
}
Test("Mushrooms use the host's absolute growth through their existing RPC", () => {
    var s=Setup(); var b=Bed(); s.mod.OnUpdate();
    Near(b.colony.GrowthProgress,0.2f+1f/60); Require(b.colony.SentValues.Count==1);
});
Test("A colony the game has hidden still grows in its active bed", () => {
    var s=Setup(); var b=Bed(); b.colony.isActiveAndEnabled=false; s.mod.OnUpdate();
    Near(b.colony.GrowthProgress,0.2f+1f/60); Require(b.colony.SentValues.Count==1);
});
Test("Colonies in inactive or other beds do not receive growth", () => {
    var s=Setup(); var b=Bed(); b.bed.isActiveAndEnabled=false; s.mod.OnUpdate(); Near(b.colony.GrowthProgress,0.2f);
    b.bed.isActiveAndEnabled=true; b.colony._parentBed=new MushroomBed(); s.mod.OnUpdate(); Near(b.colony.GrowthProgress,0.2f);
});
Test("Invalid growth inputs and zero speed never write growth", () => {
    Near(GrowthPolicy.NextProgress(0.2f,1,0,1),0.2f);
    Near(GrowthPolicy.NextProgress(0.2f,1,1,float.NaN),0.2f);
    Near(GrowthPolicy.NextProgress(0.2f,1,1,float.PositiveInfinity),0.2f);
    var s=Setup(); s.time.TimeSpeedMultiplier=0; s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,0.2f);
});
Test("Managed native-wrapper errors disable subsequent updates for this scene", () => {
    var s=Setup(); s.pot.ThrowOnLight=true; s.mod.OnUpdate(); s.pot.ThrowOnLight=false;
    for(int i=0;i<30;i++) s.mod.OnUpdate();
    Near(s.plant.NormalizedGrowthProgress,0.2f); Require(s.mod.LoggerInstance.Errors.Count==1);
});
Test("A missing sleep controller grows nothing and logs nothing", () => {
    var s=Setup(); NetworkSingleton<SleepController>.Instance=null!;
    for(int i=0;i<30;i++) s.mod.OnUpdate();
    Near(s.plant.NormalizedGrowthProgress,0.2f); Require(s.mod.LoggerInstance.Errors.Count==0);
});
Test("A game call that throws logs one error, not one per frame", () => {
    var s=Setup(); s.sleep.Removed=true;
    for(int i=0;i<90;i++) s.mod.OnUpdate();
    Near(s.plant.NormalizedGrowthProgress,0.2f); Require(s.mod.LoggerInstance.Errors.Count==1);
});
Test("Large suspend/resume gap is discarded", () => {
    var s=Setup(); Time.deltaTime=120; s.mod.OnUpdate(); Near(s.plant.NormalizedGrowthProgress,0.2f);
});
Console.WriteLine($"{passed} regression scenarios passed. Managed simulation only; native game test still required.");
