using System.IO;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Corvax.ShuttleSerialize.Serializers;
using Content.Server.Decals;
using Content.Shared.Atmos;
using Content.Shared.Containers;
using Content.Shared.Decals;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Corvax.ShuttleSerialize.EntitySystems;

public sealed partial class GridSerializationSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tile = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DecalSystem _decalSystem = default!;
    public override void Initialize()
    {
        InitializeComponents();
    }

    public void Serialize(Stream stream, EntityUid grid)
    {
        var mapGrid = Comp<MapGridComponent>(grid);

        var tiles = _map.GetAllTiles(grid, mapGrid).ToList();

        UnmanagedSerializer.Serialize(stream, tiles.Count);

        foreach (var tile in tiles)
        {
            StringSerializer.Serialize(stream, _tile[tile.Tile.TypeId].ID);
            UnmanagedSerializer.Serialize(stream, new Vector2i(tile.X, tile.Y));
        }

        var atmosphere = Comp<GridAtmosphereComponent>(grid);

        foreach (var tile in tiles)
        {
            var gas = atmosphere.Tiles[new(tile.X, tile.Y)].Air;

            if (gas is null)
            {
                UnmanagedSerializer.Serialize(stream, float.NaN);
                continue;
            }

            UnmanagedSerializer.Serialize(stream, gas.Temperature);

            for (var i = 0; i < Atmospherics.AdjustedNumberOfGases; i++)
                UnmanagedSerializer.Serialize(stream, gas[i]);
        }

        var decalGridComponent = Comp<DecalGridComponent>(grid);
        var chunks = decalGridComponent.ChunkCollection.ChunkCollection;
        UnmanagedSerializer.Serialize(stream, chunks.Count);

        foreach (var (chunkPos, chunk) in chunks)
        {
            UnmanagedSerializer.Serialize(stream, chunkPos);
            UnmanagedSerializer.Serialize(stream, chunk.Decals.Count);

            foreach (var (uid, decal) in chunk.Decals)
            {
                Console.WriteLine("======SERIALIZATION=====");
                UnmanagedSerializer.Serialize(stream, uid);
                StringSerializer.Serialize(stream, decal.Id); // ID прототипа
                UnmanagedSerializer.Serialize(stream, decal.Coordinates);
                UnmanagedSerializer.Serialize(stream, decal.ZIndex);
                UnmanagedSerializer.Serialize(stream, decal.Angle);
                if (decal.Color.HasValue)
                {
                    UnmanagedSerializer.Serialize(stream, true);             // пишем флаг: цвет есть
                    UnmanagedSerializer.Serialize(stream, decal.Color.Value); // само Color (unmanaged)
                }
                else
                {
                    UnmanagedSerializer.Serialize(stream, false);            // пишем флаг: цвета нет
                }
                UnmanagedSerializer.Serialize(stream, decal.Cleanable);
                Console.WriteLine($"{decal.Id} - {decal.Coordinates}");
            }
        }

        List<(EntityUid Entity, BaseContainer Container)> containers = [];

        List<Entity<TransformComponent, MetaDataComponent>> entities = [];

        var query = AllEntityQuery<TransformComponent, MetaDataComponent>();

        while (query.MoveNext(out var entity, out var transform, out var meta))
            if (transform.GridUid == grid && meta.EntityPrototype is not null)
                entities.Add(new(entity, transform, meta));

        UnmanagedSerializer.Serialize(stream, entities.Count);

        foreach (var entity in entities)
        {
            UnmanagedSerializer.Serialize(stream, entity.Owner.Id);
            StringSerializer.Serialize(stream, entity.Comp2.EntityPrototype!.ID);
            UnmanagedSerializer.Serialize(stream, entity.Comp1.Coordinates.Position);
            UnmanagedSerializer.Serialize(stream, entity.Comp1.Anchored);
            UnmanagedSerializer.Serialize(stream, entity.Comp1.LocalRotation);

            SerializeComponents(stream, entity);

            var parent = Transform(entity).ParentUid;

            if (parent.IsValid() && _container.TryGetContainingContainer(parent, entity, out var container))
                containers.Add((entity, container));
        }

        UnmanagedSerializer.Serialize(stream, containers.Count);

        foreach ((var entity, var container) in containers)
        {
            UnmanagedSerializer.Serialize(stream, entity.Id);
            UnmanagedSerializer.Serialize(stream, container.Owner.Id);
            StringSerializer.Serialize(stream, container.ID);
        }
    }

    public EntityUid Deserialize(Stream stream, MapId id)
    {
        var grid = _mapManager.CreateGridEntity(id);

        List<(Vector2i, Tile)> tiles = new(UnmanagedSerializer.Deserialize<int>(stream));

        for (var i = 0; i < tiles.Capacity; i++)
        {
            var tile = StringSerializer.Deserialize(stream);
            var coordinates = UnmanagedSerializer.Deserialize<Vector2i>(stream);

            tiles.Add((coordinates, new(_tile[tile].TileId)));
        }

        _map.SetTiles(grid, tiles);

        var atmosphere = AddComp<GridAtmosphereComponent>(grid);

        foreach ((var tile, _) in tiles)
        {
            var temperature = UnmanagedSerializer.Deserialize<float>(stream);

            if (float.IsNaN(temperature))
                continue;

            var moles = new float[Atmospherics.AdjustedNumberOfGases];

            for (var i = 0; i < moles.Length; i++)
                moles[i] = UnmanagedSerializer.Deserialize<float>(stream);

            var atmosphereTiles = atmosphere.Tiles;

            atmosphereTiles.Add(tile, new(grid, tile, new(moles, temperature)));
        }

        //var component = Comp<DecalGridComponent>(grid);
        //component.ChunkCollection.ChunkCollection.Clear();
        //_decalSystem.RemoveAllDecals(grid);

        Console.WriteLine("Started decal deserialization");
        Console.WriteLine("=========DESERIALIZE==========");
        var chunkCount = UnmanagedSerializer.Deserialize<int>(stream);
        Console.WriteLine("chunkCount: " + chunkCount);

        for (var i = 0; i < chunkCount; i++)
        {
            var chunkPos = UnmanagedSerializer.Deserialize<Vector2i>(stream);
            Console.WriteLine($"chunkPos: {chunkPos}");
            var decalCount = UnmanagedSerializer.Deserialize<int>(stream);
            Console.WriteLine($"decalCount: {decalCount}");
            var chunk = new DecalGridComponent.DecalChunk();
            Console.WriteLine($"chunk: {decalCount}");
            for (var j = 0; j < decalCount; j++)
            {
                var uid = UnmanagedSerializer.Deserialize<uint>(stream);
                Console.WriteLine($"uid: {uid}");
                var mainId = StringSerializer.Deserialize(stream);
                Console.WriteLine($"mainId: {mainId}");
                var pos = UnmanagedSerializer.Deserialize<Vector2>(stream);
                Console.WriteLine($"pos: {pos}");
                var z = UnmanagedSerializer.Deserialize<int>(stream);
                Console.WriteLine($"z: {z}");
                var rot = UnmanagedSerializer.Deserialize<Angle>(stream);
                Console.WriteLine($"rot: {rot}");
                var hasColor = UnmanagedSerializer.Deserialize<bool>(stream);
                Color? color = hasColor
                    ? UnmanagedSerializer.Deserialize<Color>(stream)
                    : null;
                Console.WriteLine($"color: {color}");
                var clean = UnmanagedSerializer.Deserialize<bool>(stream);
                Console.WriteLine($"clean: {clean}");

                var decal = new Decal(pos, mainId, color, rot, z, clean);
                Console.WriteLine($"decal: {decal}");
                chunk.Decals[uid] = decal;
                _decalSystem.TryAddDecal(decal.Id,
                    new EntityCoordinates(grid, pos),
                    out var decalId,
                    color,
                    rot,
                    z,
                    clean);
            }
        }
        Console.WriteLine("Ended decal deserialization");

        Dictionary<int, EntityUid> entities = [];



        var count = UnmanagedSerializer.Deserialize<int>(stream);
        Console.WriteLine($"count: {count}");
        for (var i = 0; i < count; i++)
        {
            var entityUid = UnmanagedSerializer.Deserialize<int>(stream);
            Console.WriteLine($"entityUid: {entityUid}");
            var entity = EntityManager.CreateEntityUninitialized(StringSerializer.Deserialize(stream));
            Console.WriteLine($"entity: {entity}");
            var transform = Transform(entity);
            Console.WriteLine($"transform: {transform}");
            var coordinates = UnmanagedSerializer.Deserialize<Vector2>(stream);
            Console.WriteLine($"coordinates: {coordinates}");
            var anchored = UnmanagedSerializer.Deserialize<bool>(stream);
            Console.WriteLine($"anchored: {anchored}");
            _transform.SetCoordinates(entity, transform, new(grid, coordinates), unanchor: !anchored);

            transform.LocalRotation = UnmanagedSerializer.Deserialize<Angle>(stream);

            RemComp<ContainerFillComponent>(entity);

            DeserializeComponents(stream, entity);

            EntityManager.InitializeAndStartEntity(entity);

            if (EntityManager.TryGetComponent<ContainerManagerComponent>(entity, out var containerManager))
                foreach (var container in _container.GetAllContainers(entity, containerManager))
                    foreach (var containerEntity in _container.EmptyContainer(container, true, reparent: false))
                        Del(containerEntity);

            entities.Add(entityUid, entity);
        }
        Console.WriteLine($"entities: {entities.Count}");
        count = UnmanagedSerializer.Deserialize<int>(stream);

        for (var i = 0; i < count; i++)
        {
            var entity = UnmanagedSerializer.Deserialize<int>(stream);
            var container = UnmanagedSerializer.Deserialize<int>(stream);
            var containerId = StringSerializer.Deserialize(stream);

            if (_container.TryGetContainer(entities[container], containerId, out var containerEntity))
                _container.InsertOrDrop(entities[entity], containerEntity);
        }

        return grid;
    }
}
