using System;
using System.Collections.Generic;

/// <summary>
/// Simple publish/subscribe event bus for decoupling game systems.
/// Usage:
///   Subscribe:   EventBus.Subscribe<MyEvent>(OnMyEvent);
///   Publish:     EventBus.Publish(new MyEvent(...));
///   Unsubscribe: EventBus.Unsubscribe<MyEvent>(OnMyEvent);
/// </summary>
public static class EventBus
{
    private static readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    public static void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_subscribers.ContainsKey(type))
            _subscribers[type] = new List<Delegate>();
        _subscribers[type].Add(handler);
    }

    public static void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (_subscribers.ContainsKey(type))
            _subscribers[type].Remove(handler);
    }

    public static void Publish<T>(T eventData)
    {
        var type = typeof(T);
        if (!_subscribers.TryGetValue(type, out var handlers)) return;

        // Copy list before iterating in case handlers modify subscriptions
        var copy = new List<Delegate>(handlers);
        foreach (var handler in copy)
            (handler as Action<T>)?.Invoke(eventData);
    }

    /// <summary>Call on scene unload to prevent stale references.</summary>
    public static void Clear() => _subscribers.Clear();
}

// ---------------------------------------------------------------------------
// Event definitions — add new events here as game systems grow
// ---------------------------------------------------------------------------

public class GameStateChangedEvent
{
    public GameManager.GameState NewState { get; }
    public GameStateChangedEvent(GameManager.GameState state) => NewState = state;
}

public class SystemEnteredEvent
{
    public StarSystem System { get; }
    public SystemEnteredEvent(StarSystem system) => System = system;
}

public class POISelectedEvent
{
    public PointOfInterest POI { get; }
    public POISelectedEvent(PointOfInterest poi) => POI = poi;
}

public class CrewRosterChangedEvent { }

public class ShipModifiedEvent { }

public class InventoryChangedEvent { }

public class CreditsChangedEvent
{
    public int NewTotal { get; }
    public CreditsChangedEvent(int total) => NewTotal = total;
}

public class SalvageChangedEvent
{
    public int NewTotal { get; }
    public SalvageChangedEvent(int total) => NewTotal = total;
}
