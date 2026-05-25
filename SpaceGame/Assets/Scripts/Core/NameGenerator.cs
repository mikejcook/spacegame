using UnityEngine;

/// <summary>
/// Generates random character names from the names.json data file in Resources/Data.
///
/// Usage:
///   var result = NameGenerator.Generate();
///   var result = NameGenerator.Generate(Gender.Female);
///
///   result.firstName  → "Elena"
///   result.lastName   → "Vasquez"
///   result.gender     → Gender.Female
/// </summary>
public static class NameGenerator
{
    // -----------------------------------------------------------------------
    // Types
    // -----------------------------------------------------------------------

    public struct GeneratedName
    {
        public string firstName;
        public string lastName;
        public Gender gender;

        public override string ToString() => $"{firstName} {lastName} ({gender})";
    }

    // -----------------------------------------------------------------------
    // Internal data model — matches Resources/Data/names.json
    // -----------------------------------------------------------------------

    [System.Serializable]
    private class NamesData
    {
        public FirstNameLists firstNames;
        public string[] lastNames;

        [System.Serializable]
        public class FirstNameLists
        {
            public string[] male;
            public string[] female;
            public string[] neutral;
        }
    }

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private static NamesData _data;
    private static bool _loaded;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generate a random name. Gender is chosen at random if not specified.
    /// </summary>
    public static GeneratedName Generate(Gender? gender = null)
    {
        EnsureLoaded();

        var resolvedGender = gender ?? RandomGender();
        var firstName      = PickFirstName(resolvedGender);
        var lastName       = Pick(_data.lastNames);

        return new GeneratedName
        {
            firstName = firstName,
            lastName  = lastName,
            gender    = resolvedGender,
        };
    }

    // -----------------------------------------------------------------------
    // Internals
    // -----------------------------------------------------------------------

    public static Gender RandomGender()
    {
        // 45% male, 45% female, 10% non-binary.
        var roll = Random.value;
        if (roll < 0.45f) return Gender.Male;
        if (roll < 0.90f) return Gender.Female;
        return Gender.NonBinary;
    }

    private static string PickFirstName(Gender gender)
    {
        return gender switch
        {
            Gender.Male     => Pick(_data.firstNames.male),
            Gender.Female   => Pick(_data.firstNames.female),
            Gender.NonBinary => Pick(_data.firstNames.neutral),
            _               => Pick(_data.firstNames.neutral),
        };
    }

    private static string Pick(string[] pool)
    {
        if (pool == null || pool.Length == 0)
        {
            Debug.LogError("[NameGenerator] Name pool is empty.");
            return "Unknown";
        }
        return pool[Random.Range(0, pool.Length)];
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;

        var asset = Resources.Load<TextAsset>("Data/names");
        if (asset == null)
        {
            Debug.LogError("[NameGenerator] Could not load Resources/Data/names.json.");
            _data = new NamesData
            {
                firstNames = new NamesData.FirstNameLists
                {
                    male    = new[] { "Alex" },
                    female  = new[] { "Alex" },
                    neutral = new[] { "Alex" },
                },
                lastNames = new[] { "Unknown" },
            };
            _loaded = true;
            return;
        }

        _data   = JsonUtility.FromJson<NamesData>(asset.text);
        _loaded = true;

        Debug.Log($"[NameGenerator] Loaded {_data.firstNames.male.Length} male, " +
                  $"{_data.firstNames.female.Length} female, " +
                  $"{_data.firstNames.neutral.Length} non-binary first names, " +
                  $"{_data.lastNames.Length} last names.");
    }
}
