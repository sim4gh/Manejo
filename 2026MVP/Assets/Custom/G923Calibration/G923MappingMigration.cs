namespace TlaxSim.G923Calibration
{
    // Stub. Full implementation arrives in Phase 3. Returns false so
    // G923ControlMapping.LoadFromDisk() can compile and behave correctly
    // (no migration available yet).
    public static class G923MappingMigration
    {
        public static bool TryMigrateFromPlayerPrefs(out G923Mapping migrated)
        {
            migrated = null;
            return false;
        }
    }
}
