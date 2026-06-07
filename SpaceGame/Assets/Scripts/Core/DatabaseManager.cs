using UnityEngine;
using SQLite4Unity3d;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Manages the SQLite database connection and exposes typed repositories
/// for each game entity. Attach as a child of the GameManager GameObject.
///
/// DEPENDENCY: Requires sqlite-net-pcl. Install via NuGetForUnity:
///   Window -> NuGet -> Search "sqlite-net-pcl" -> Install
/// See the project setup guide (Docs/SetupGuide.md) for full instructions.
/// </summary>
public class DatabaseManager : MonoBehaviour
{
    private SQLiteConnection _db;

    // Typed repositories — one per DB table
    public TableRepository<SaveGame>    SaveGames   { get; private set; }
    public TableRepository<Character>   Characters  { get; private set; }
    public TableRepository<Ship>        Ships       { get; private set; }
    public TableRepository<EquipmentItem> Equipment { get; private set; }
    public TableRepository<StarSystem>  StarSystems { get; private set; }
    public TableRepository<PointOfInterest> POIs    { get; private set; }

    // -----------------------------------------------------------------------
    // Initialization
    // -----------------------------------------------------------------------

    public void Initialize()
    {
        string path = Path.Combine(Application.persistentDataPath, Constants.Database.FileName);
        Debug.Log($"[DatabaseManager] Opening database at: {path}");

#if UNITY_EDITOR
        // In the editor, always start with a fresh database so schema changes take effect.
        // Remove this block (or gate it behind a menu toggle) before shipping.
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("[DatabaseManager] Editor mode: existing database deleted for schema refresh.");
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android's linker namespace isolation prevents IL2CPP's dlopen("sqlite3") from
        // finding the app-private libsqlite3.so.  Calling System.loadLibrary from the Java
        // side resolves the app namespace correctly; once loaded the library is resident in
        // the process and IL2CPP's subsequent DllImport finds it via the linker cache.
        try
        {
            using (var preload = new AndroidJavaClass("com.mikecook.starcaptain.SQLitePreload"))
            {
                preload.CallStatic("load");
                Debug.Log("[DatabaseManager] sqlite3 native library preloaded via Java.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DatabaseManager] SQLite preload failed: {ex.Message}");
        }
#endif

        _db = new SQLiteConnection(path);

        CreateTables();
        InitializeRepositories();

        Debug.Log("[DatabaseManager] Database ready.");
    }

    private void CreateTables()
    {
        _db.CreateTable<SaveGame>();
        _db.CreateTable<Character>();
        _db.CreateTable<Ship>();
        _db.CreateTable<EquipmentItem>();
        _db.CreateTable<StarSystem>();
        _db.CreateTable<PointOfInterest>();
    }

    private void InitializeRepositories()
    {
        SaveGames  = new TableRepository<SaveGame>(_db);
        Characters = new TableRepository<Character>(_db);
        Ships      = new TableRepository<Ship>(_db);
        Equipment  = new TableRepository<EquipmentItem>(_db);
        StarSystems = new TableRepository<StarSystem>(_db);
        POIs       = new TableRepository<PointOfInterest>(_db);
    }

    // -----------------------------------------------------------------------
    // Convenience query helpers
    // -----------------------------------------------------------------------

    /// <summary>Returns all hired crew for a save (excludes pending station recruits).</summary>
    public List<Character> GetCrewForSave(int saveGameId)
        => Characters.Query().Where(c => c.SaveGameId == saveGameId && c.StationPoiId == 0).ToList();

    /// <summary>Returns the first hired crew member with the given role for a save, or null.</summary>
    public Character GetCrewByRole(int saveGameId, string role)
        => Characters.Query().Where(c => c.SaveGameId == saveGameId && c.StationPoiId == 0 && c.Role == role).FirstOrDefault();

    /// <summary>Returns all pending recruits stored for a given space station POI.</summary>
    public List<Character> GetStationRecruits(int poiId)
        => Characters.Query().Where(c => c.StationPoiId == poiId).ToList();

    /// <summary>
    /// Deletes all pending recruits for a station POI so a fresh pool can be generated.
    /// </summary>
    public void ClearStationRecruits(int poiId)
    {
        var old = GetStationRecruits(poiId);
        foreach (var c in old)
            Characters.Delete(c);
    }

    public List<PointOfInterest> GetPOIsForSystem(int starSystemId)
        => POIs.Query().Where(p => p.StarSystemId == starSystemId).ToList();

    public List<EquipmentItem> GetInventoryForSave(int saveGameId)
        => Equipment.Query().Where(e => e.SaveGameId == saveGameId && !e.IsInstalled).ToList();

    // -----------------------------------------------------------------------
    // Cleanup
    // -----------------------------------------------------------------------

    private void OnDestroy()
    {
        _db?.Close();
        Debug.Log("[DatabaseManager] Database connection closed.");
    }
}

// ---------------------------------------------------------------------------
// Generic repository — basic CRUD + query access for any table type
// ---------------------------------------------------------------------------

public class TableRepository<T> where T : new()
{
    private readonly SQLiteConnection _db;

    public TableRepository(SQLiteConnection db) => _db = db;

    public int Insert(T item)     => _db.Insert(item);
    public int Update(T item)     => _db.Update(item);
    public int Delete(T item)     => _db.Delete(item);
    public T   Get(int id)        => _db.Get<T>(id);
    public List<T> GetAll()       => _db.Table<T>().ToList();
    public TableQuery<T> Query()  => _db.Table<T>();
}