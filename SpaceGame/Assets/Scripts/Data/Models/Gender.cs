/// <summary>
/// Character gender — stored as a tinyint in the Characters table.
/// Used by Character.GetPronoun() and NameGenerator.Generate().
///
/// None is reserved for the player captain, who is always addressed as "you".
/// </summary>
public enum Gender : byte
{
    Male      = 0,
    Female    = 1,
    NonBinary = 2,
    None      = 255,  // player captain — second-person pronouns only
}
