using UnityEngine;

namespace TlaxSim.MotoCalibration
{
    public static class MotoMappingMigration
    {
        public interface IPlayerPrefsReader
        {
            string GetString(string key, string defaultValue);
            float GetFloat(string key, float defaultValue);
            int GetInt(string key, int defaultValue);
        }

        public static bool TryMigrateFromPlayerPrefs(out MotoMapping migrated)
        {
            migrated = null;
            return false;
        }
    }
}
