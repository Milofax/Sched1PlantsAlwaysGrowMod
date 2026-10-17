// Managed test doubles. These exercise the production mod's update/lifecycle code,
// but cannot validate IL2CPP, Wine, Harmony, Unity or network RPC implementations.
namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class MelonInfoAttribute(Type type, string name, string version, string author) : Attribute;
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class MelonGameAttribute(string studio, string game) : Attribute;
    public class MelonMod
    {
        public TestLogger LoggerInstance { get; } = new();
        public virtual void OnInitializeMelon() { }
        public virtual void OnSceneWasLoaded(int index, string name) { }
        public virtual void OnSceneWasUnloaded(int index, string name) { }
        public virtual void OnUpdate() { }
    }
    public sealed class TestLogger
    {
        public List<string> Errors { get; } = [];
        public void Msg(string message) { }
        public void Error(string message) => Errors.Add(message);
    }
    public sealed class MelonPreferences_Entry<T>(T value) { public T Value = value; }
    public sealed class TestCategory
    {
        public MelonPreferences_Entry<T> CreateEntry<T>(string name, T value) => new(value);
    }
    public static class MelonPreferences
    {
        public static TestCategory CreateCategory(string name) => new();
    }
}
namespace UnityEngine
{
    public class Object
    {
        private static long sequence;
        public IntPtr Pointer { get; } = new(++sequence);
        public static List<Object> SceneObjects { get; } = [];
        public static int Scans;
        public static T[] FindObjectsOfType<T>() where T : Object
        {
            Scans++;
            // Like Unity, a scene search does not return deactivated objects.
            return SceneObjects.OfType<T>().Where((o) => o is not MonoBehaviour m || m.isActiveAndEnabled).ToArray();
        }
    }
    public class MonoBehaviour : Object { public bool isActiveAndEnabled = true; }
    public static class Time { public static float deltaTime = 1; public static float unscaledTime; }
}
namespace Il2CppFishNet
{
    public static class InstanceFinder { public static bool IsServer = true; }
}
namespace Il2CppScheduleOne.DevUtilities
{
    public static class Singleton<T> where T : class { public static T Instance = null!; }
    public static class NetworkSingleton<T> where T : class { public static T Instance = null!; }
    public static class GameManager { public static bool IS_TUTORIAL; }
}
namespace Il2CppScheduleOne.Persistence
{
    public sealed class LoadManager { public bool IsGameLoaded = true; public bool IsLoading; }
}
namespace Il2CppScheduleOne.GameTime
{
    public sealed class TimeManager : UnityEngine.Object
    {
        public bool IsEndOfDay = true;
        public int CurrentTime = 400;
        public float TimeSpeedMultiplier = 1;
        public static float MinuteDuration = 1;
    }
    public sealed class SleepController : UnityEngine.Object
    {
        public bool Removed;
        private bool sleeping;
        public bool IsSleepInProgress
        {
            get => Removed ? throw new MissingMethodException("simulated member removed by a game update") : sleeping;
            set => sleeping = value;
        }
    }
}
namespace Il2CppScheduleOne.ObjectScripts
{
    public sealed class Pot : UnityEngine.MonoBehaviour
    {
        public Il2CppScheduleOne.Growing.Plant? Plant;
        public float NormalizedMoistureAmount = 1;
        public float GrowSpeedMultiplier = 1;
        public float Exposure = 1;
        public float LightMultiplier = 1;
        public float TemperatureMultiplier = 1;
        public bool ThrowOnLight;
        public List<float> SentValues { get; } = [];
        public float GetAverageLightExposure(out float multiplier)
        {
            if (ThrowOnLight) throw new InvalidOperationException("simulated native wrapper failure");
            multiplier = LightMultiplier;
            return Exposure;
        }
        public float GetTemperatureGrowthMultiplier() => TemperatureMultiplier;
        public void SetGrowthProgress_Client(float value) => SentValues.Add(value);
    }
    public sealed class MushroomBed : UnityEngine.MonoBehaviour
    {
        public Il2CppScheduleOne.Growing.ShroomColony? CurrentColony;
    }
}
namespace Il2CppScheduleOne.Growing
{
    public sealed class Plant : UnityEngine.MonoBehaviour
    {
        public Il2CppScheduleOne.ObjectScripts.Pot? Pot;
        public float NormalizedGrowthProgress = 0.2f;
        public int GrowthTime = 1;
        public void SetNormalizedGrowthProgress(float value) => NormalizedGrowthProgress = value;
    }
    public sealed class ShroomColony : UnityEngine.MonoBehaviour
    {
        public Il2CppScheduleOne.ObjectScripts.MushroomBed? _parentBed = new();
        public int _growTime = 1;
        public float GrowthProgress = 0.2f;
        public bool IsFullyGrown => GrowthProgress >= 1;
        public float Rate = 1;
        public List<float> SentValues { get; } = [];
        public void CheckTemperature() { }
        public float GetCurrentGrowthRate() => Rate;
        public void SetGrowthPercentage_Local(object? connection, float value)
        {
            GrowthProgress = value;
            SentValues.Add(value);
        }
    }
}
