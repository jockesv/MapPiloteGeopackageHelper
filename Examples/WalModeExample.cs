using MapPiloteGeopackageHelper;

namespace WalModeExample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("WAL Mode Example for MapPiloteGeopackageHelper");
            Console.WriteLine("==============================================");

            // Example 1: Create GeoPackage with WAL mode enabled
            string walGpkg = "example_with_wal.gpkg";
            Console.WriteLine("\n1. Creating GeoPackage with WAL mode enabled:");
            
            CMPGeopackageCreateHelper.CreateGeoPackage(
                walGpkg, 
                srid: 3006,
                walMode: true, 
                onStatus: Console.WriteLine);

            // Example 2: Create GeoPackage without WAL mode (backward compatible)
            string normalGpkg = "example_normal.gpkg";
            Console.WriteLine("\n2. Creating GeoPackage with normal mode (backward compatible):");
            
            CMPGeopackageCreateHelper.CreateGeoPackage(
                normalGpkg, 
                srid: 3006,
                onStatus: Console.WriteLine);

            // Example 3: WAL mode with default SRID
            string walDefaultGpkg = "example_wal_default.gpkg";
            Console.WriteLine("\n3. Creating GeoPackage with WAL mode and default SRID:");
            
            CMPGeopackageCreateHelper.CreateGeoPackage(
                walDefaultGpkg, 
                walMode: true,
                onStatus: Console.WriteLine);

            Console.WriteLine("\nAll examples completed successfully!");
            Console.WriteLine("\nBenefits of WAL mode:");
            Console.WriteLine("- Better concurrency (multiple readers while writing)");
            Console.WriteLine("- Improved performance for write-heavy workloads");
            Console.WriteLine("- Atomic commits and better crash recovery");
            Console.WriteLine("- No need to manually execute PRAGMA journal_mode = WAL");

            // Clean up example files
            CleanupFile(walGpkg);
            CleanupFile(normalGpkg);
            CleanupFile(walDefaultGpkg);
        }

        private static void CleanupFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    // Also clean up WAL auxiliary files
                    var walFile = path + "-wal";
                    var shmFile = path + "-shm";
                    if (File.Exists(walFile)) File.Delete(walFile);
                    if (File.Exists(shmFile)) File.Delete(shmFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}