using System;
using System.Collections.Generic;
using Bindito.Core;
using Newtonsoft.Json;
using Timberborn.Automation;
using Timberborn.BaseComponentSystem;
using Timberborn.Beavers;
using Timberborn.BlockingSystem;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Characters;
using Timberborn.DwellingSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Modding;
using Timberborn.ModManagerScene;
using Timberborn.Navigation;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Timberborn.WorldPersistence;
using Timberborn.WorkSystem;
using UnityEngine;

namespace OptimizedLocalHousing;

[Context("Game")]
public sealed class HousingConfigurator : Configurator
{
    protected override void Configure() => Bind<HousingService>().AsSingleton();
}

public sealed class ModStarter : IModStarter
{
    public void StartMod(IModEnvironment environment) => Debug.Log("[OptimizedLocalHousing] 1.1.1 loaded.");
}

// Thin bridge between the game and PassEngine: it enumerates beavers, answers route queries, and applies moves.
// It keeps no state of its own, so nothing here can drift between multiplayer peers.
public sealed class HousingService : ILoadableSingleton, IUnloadableSingleton, ISaveableSingleton, ITickableSingleton, IPassWorld
{
    private static readonly SingletonKey SaveKey = new SingletonKey("OptimizedLocalHousing");
    private static readonly PropertyKey<string> StateKey = new PropertyKey<string>("State");
    private static readonly string[] Conflicts = { "BobHousingOptimize", "BobCommuteBalancer", "housingoptimize" };
    private readonly EventBus _events;
    private readonly DistrictCenterRegistry _districts;
    private readonly EntityRegistry _entities;
    private readonly ISingletonLoader _loader;
    private readonly ModRepository _mods;
    private PassEngine _engine;
    private bool _disabled;

    public HousingService(EventBus events, DistrictCenterRegistry districts, EntityRegistry entities, ISingletonLoader loader, ModRepository mods)
    { _events = events; _districts = districts; _entities = entities; _loader = loader; _mods = mods; }

    public void Load()
    {
        foreach (var mod in _mods.EnabledMods)
            foreach (var conflict in Conflicts)
                if (mod.Manifest.Id == conflict)
                { _disabled = true; Debug.LogWarning("[OptimizedLocalHousing] Disabled because another housing assignment mod is enabled: " + conflict); }
        PassState state = null;
        try
        {
            if (_loader.TryGetSingleton(SaveKey, out var saved) && saved.Has(StateKey))
                state = JsonConvert.DeserializeObject<PassState>(saved.Get(StateKey));
        }
        catch (Exception exception) { Debug.LogWarning("[OptimizedLocalHousing] Saved state ignored: " + exception.Message); }
        _engine = new PassEngine(this, state);
        _engine.Reported += Report;
        _engine.Faulted += exception => Debug.LogError("[OptimizedLocalHousing] Pass abandoned until the next day: " + exception);
        if (!_disabled) _events.Register(this);
    }
    public void Unload() { if (!_disabled) _events.Unregister(this); }
    public void Save(ISingletonSaver saver) => saver.GetSingleton(SaveKey).Set(StateKey, JsonConvert.SerializeObject(_engine.State));

    [OnEvent] public void OnDaytimeStart(DaytimeStartEvent ev) => _engine.RequestPass();
    public void Tick() { if (!_disabled) _engine.Tick(); }

    private void Report(PassReport r) => Debug.Log($"[OptimizedLocalHousing] Pass {_engine.State.Passes}: {r.Adults} adults, {r.Homes} homes, " +
        $"{r.Works} workplaces; {r.Queries} route queries over {r.Ticks} ticks; {r.Applied} move cycles applied " +
        $"({r.Rejected} rejected, {r.Stale} stale), {r.Recovered} disconnected commutes repaired, route cost saved {r.RouteCostSaved:F0}.");

    public Snapshot Capture()
    {
        var builder = new SnapshotBuilder();
        foreach (var district in _districts.FinishedDistrictCenters)
        {
            foreach (var beaver in district.DistrictPopulation.Beavers) AddAdult(builder, beaver);
            // Every usable home in the district, so an adult can move into an empty bed.
            var registry = district.GetComponent<DistrictBuildingRegistry>();
            if (registry) foreach (var home in registry.GetEnabledBuildingsInstant<Dwelling>()) AddHome(builder, home, Id(district));
        }
        return builder.Build();
    }

    private static bool AddHome(SnapshotBuilder builder, Dwelling home, Guid districtId)
    {
        if (!UsableHome(home) || Id(home.GetComponent<DistrictBuilding>()?.District) != districtId) return false;
        var block = home.GetComponent<BlockObject>(); if (!block) return false;
        var at = block.Coordinates;
        builder.AddHome(Id(home), districtId, at.x, at.y, at.z, home.MaxBeavers, home.NumberOfAdultDwellers, home.NumberOfChildDwellers);
        return true;
    }

    private void AddAdult(SnapshotBuilder builder, Beaver beaver)
    {
        var character = beaver.GetComponent<Character>(); var dweller = beaver.GetComponent<Dweller>();
        if (!character || !character.Alive || !dweller || beaver.GetComponent<Child>()) return;
        var home = dweller.Home; var district = beaver.GetComponent<Citizen>()?.AssignedDistrict;
        if (!home || !district || !district.Enabled) return;
        var districtId = Id(district);
        if (!AddHome(builder, home, districtId)) return;
        builder.AddAdult(Id(beaver), Id(home), JobOf(beaver, districtId, builder), districtId);
    }

    // The assigned workplace when it is usable for commute purposes, otherwise Guid.Empty (the beaver is then
    // indifferent to where it lives). A paused workplace still counts: its route matters when it reopens.
    private Guid JobOf(Beaver beaver, Guid districtId, SnapshotBuilder builder)
    {
        var worker = beaver.GetComponent<Worker>(); var work = worker ? worker.Workplace : null;
        if (!work || !work.Enabled || Id(work.GetComponent<DistrictBuilding>()?.District) != districtId) return Guid.Empty;
        var block = work.GetComponent<BlockObject>(); if (!block) return Guid.Empty;
        if (builder != null) { var at = block.Coordinates; builder.AddWork(Id(work), at.x, at.y, at.z); }
        return Id(work);
    }

    public bool TryRoute(Guid homeId, Guid workId, out float cost)
    {
        cost = 0;
        var home = Component<Dwelling>(homeId); var work = Component<Workplace>(workId);
        var start = home ? home.GetEnabledComponent<Accessible>() : null;
        var end = work ? work.GetEnabledComponent<Accessible>() : null;
        if (!start || !end || !BuildingUsable(home) || !work.Enabled ||
            !start.ValidAccessible || !end.ValidAccessible || !start.HasSingleAccess || end.Accesses.Count == 0) return false;
        var blocked = work.GetComponent<BlockableObject>();
        if (blocked && !blocked.IsUnblocked) return false;
        return start.FindRoadPath(end, out cost);
    }

    public bool ApplyCycle(Move[] cycle)
    {
        int n = cycle.Length;
        var dwellers = new Dweller[n]; var from = new Dwelling[n]; var to = new Dwelling[n];
        var change = new Dictionary<Guid, int>();
        for (int i = 0; i < n; i++)
        {
            var m = cycle[i];
            var beaver = Component<Beaver>(m.Adult);
            if (!beaver) return false;
            var character = beaver.GetComponent<Character>(); var dweller = beaver.GetComponent<Dweller>();
            if (!character || !character.Alive || !dweller || beaver.GetComponent<Child>() || !dweller.Home || Id(dweller.Home) != m.From) return false;
            var district = beaver.GetComponent<Citizen>()?.AssignedDistrict;
            if (!district || JobOf(beaver, Id(district), null) != m.Work) return false;
            var source = Component<Dwelling>(m.From); var target = Component<Dwelling>(m.To);
            if (!source || !target || !UsableHome(target) || Id(target.GetComponent<DistrictBuilding>()?.District) != Id(district)) return false;
            dwellers[i] = dweller; from[i] = source; to[i] = target;
            change[m.From] = (change.TryGetValue(m.From, out var out1) ? out1 : 0) - 1;
            change[m.To] = (change.TryGetValue(m.To, out var in1) ? in1 : 0) + 1;
        }
        // A cycle leaves every home's head count unchanged, and a chain ends in a bed that was empty at the snapshot; this
        // guards against a newborn or a homeless beaver taking that bed meanwhile, or an adult a child's bed (the game
        // would move an adult out of a home with more adults than adult beds).
        foreach (var pair in change)
        {
            var home = Component<Dwelling>(pair.Key);
            if (!home || home.NumberOfDwellers + pair.Value > home.MaxBeavers) return false;
            if (pair.Value > 0 && home.NumberOfAdultDwellers + pair.Value > home.AdultSlots) return false;
        }
        try
        {
            for (int i = 0; i < n; i++) dwellers[i].UnassignFromHome();
            for (int i = 0; i < n; i++) to[i].AssignDweller(dwellers[i]);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[OptimizedLocalHousing] A move cycle failed and was rolled back: " + exception.Message);
            for (int i = 0; i < n; i++) if (dwellers[i].Home) dwellers[i].UnassignFromHome();
            for (int i = 0; i < n; i++) { try { from[i].AssignDweller(dwellers[i]); } catch (Exception) { } }
            return false;
        }
    }

    private T Component<T>(Guid id) where T : BaseComponent
    {
        if (id == Guid.Empty) return null;
        var entity = _entities.GetEntity(id);
        return entity && !entity.Deleted ? entity.GetComponent<T>() : null;
    }
    private static Guid Id(BaseComponent component) => component ? component.GetComponent<EntityComponent>().EntityId : Guid.Empty;
    private static bool UsableHome(Dwelling home)
    {
        if (!BuildingUsable(home)) return false;
        var accessible = home.GetEnabledComponent<Accessible>();
        return accessible && accessible.ValidAccessible && accessible.HasSingleAccess;
    }
    private static bool BuildingUsable(BaseComponent building)
    {
        if (!building || !building.Enabled) return false;
        var pause = building.GetComponent<PausableBuilding>(); if (pause && pause.Paused) return false;
        var automation = building.GetComponent<Automatable>();
        if (automation && automation.IsAutomated && automation.State == ConnectionState.Off) return false;
        var blocked = building.GetComponent<BlockableObject>(); return !blocked || blocked.IsUnblocked;
    }
}
