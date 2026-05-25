using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Substitutes <c>{Namespace.Target.Field}</c> tokens in story text with live
/// values from the current game session (<see cref="GameManager.Instance"/>).
///
/// Call <see cref="Resolve"/> on raw scene text before assigning it to the UI.
/// Tokens that cannot be resolved are left unchanged and logged as warnings.
///
/// ── Supported tokens ─────────────────────────────────────────────────────────
///
/// Character tokens   — replace Target with Captain, Pilot, or Engineer
///   {Character.Captain.FirstName}
///   {Character.Captain.LastName}
///   {Character.Captain.Name}            ← full display name
///   {Character.Captain.Role}
///   {Character.Captain.Level}
///   {Character.Captain.Pronoun.Subject}           → he / she / they / you
///   {Character.Captain.Pronoun.Object}            → him / her / them / you
///   {Character.Captain.Pronoun.PossessiveAdj}     → his / her / their / your
///   {Character.Captain.Pronoun.PossessivePronoun} → his / hers / theirs / yours
///   {Character.Captain.Pronoun.Reflexive}         → himself / herself / … / yourself
///
/// Ship tokens
///   {Ship.Name}
///   {Ship.ShipClass}
///
/// ── Example ──────────────────────────────────────────────────────────────────
///
///   "Along with {Character.Pilot.FirstName} and {Character.Engineer.FirstName},
///    you board your {Ship.ShipClass}, the {Ship.Name}."
/// </summary>
public static class StoryTextResolver
{
    private static readonly Regex TokenPattern = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Replaces all <c>{...}</c> tokens in <paramref name="text"/> with their
    /// resolved values. Returns the original string on null/empty input.
    /// </summary>
    public static string Resolve(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return TokenPattern.Replace(text, match =>
        {
            var token = match.Groups[1].Value;
            var resolved = ResolveToken(token);
            if (resolved == null)
            {
                Debug.LogWarning($"[StoryTextResolver] Unrecognised token: {{{token}}}");
                return match.Value; // leave original token in place
            }
            return resolved;
        });
    }

    // -----------------------------------------------------------------------
    // Internal dispatch
    // -----------------------------------------------------------------------

    private static string ResolveToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2) return null;

        return parts[0] switch
        {
            "Character" => ResolveCharacter(parts),
            "Ship"      => ResolveShip(parts),
            _           => null
        };
    }

    // -----------------------------------------------------------------------
    // Character resolver  — {Character.<Target>.<Field>}
    // -----------------------------------------------------------------------

    private static string ResolveCharacter(string[] parts)
    {
        // Minimum: Character.Target.Field  (3 parts)
        // Pronoun: Character.Target.Pronoun.Type  (4 parts)
        if (parts.Length < 3) return null;

        var character = parts[1] switch
        {
            "Captain"  => GameManager.Instance?.PlayerCaptain,
            "Pilot"    => GameManager.Instance?.PlayerPilot,
            "Engineer" => GameManager.Instance?.PlayerEngineer,
            _          => null
        };

        if (character == null)
        {
            Debug.LogWarning($"[StoryTextResolver] Character target '{parts[1]}' is null — game data may not be ready.");
            return null;
        }

        // Pronoun sub-token: Character.Target.Pronoun.Type
        if (parts[2] == "Pronoun")
        {
            if (parts.Length < 4) return null;
            var pronounType = parts[3] switch
            {
                "Subject"           => Character.PronounType.Subject,
                "Object"            => Character.PronounType.Object,
                "PossessiveAdj"     => Character.PronounType.PossessiveAdj,
                "PossessivePronoun" => Character.PronounType.PossessivePronoun,
                "Reflexive"         => Character.PronounType.Reflexive,
                _                   => (Character.PronounType?)null
            };
            if (pronounType == null) return null;
            return character.GetPronoun(pronounType.Value);
        }

        return parts[2] switch
        {
            "FirstName" => character.FirstName,
            "LastName"  => character.LastName,
            "Name"      => character.Name,
            "Role"      => character.Role,
            "Level"     => character.Level.ToString(),
            _           => null
        };
    }

    // -----------------------------------------------------------------------
    // Ship resolver  — {Ship.<Field>}
    // -----------------------------------------------------------------------

    private static string ResolveShip(string[] parts)
    {
        if (parts.Length < 2) return null;

        var ship = GameManager.Instance?.PlayerShip;
        if (ship == null)
        {
            Debug.LogWarning("[StoryTextResolver] PlayerShip is null — game data may not be ready.");
            return null;
        }

        return parts[1] switch
        {
            "Name"      => ship.Name,
            "ShipClass" => ship.ShipClass.ToString(),
            _           => null
        };
    }
}
