using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

/// <summary>Versioned 64x64 terrain chunks in private-world checkpoints.</summary>
internal sealed class PrivateWorldTerrainChunkCodec : JsonConverter<SeededMap>
{
    private const int ChunkSize = 64;
    private const string Encoding = "terrain-chunks/v1";

    private sealed record Chunk(int X, int Y, int Width, int Height, byte[] Data, string Sha256);

    private sealed record PackedMap(
        int Width,
        int Height,
        int GenerationAttempt,
        IReadOnlyList<CampObject>? CampObjects,
        IReadOnlyList<MapResource>? Resources,
        string? ManifestDigest,
        string? TerrainEncoding,
        IReadOnlyList<Chunk>? TerrainChunks,
        byte[]? ClimateZones,
        bool WrapsEastWest);

    public override SeededMap Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var packed = JsonSerializer.Deserialize<PackedMap>(ref reader, options)
            ?? throw new InvalidDataException("The chunked map is missing.");
        if (packed.TerrainEncoding != Encoding || packed.Width is < 1 or > 4096 ||
            packed.Height is < 1 or > 2048 || packed.TerrainChunks is null ||
            packed.CampObjects is null || packed.Resources is null || packed.ManifestDigest is null)
            throw new InvalidDataException("The private-world terrain encoding or dimensions are unsupported.");

        var tiles = new TerrainTile[checked(packed.Width * packed.Height)];
        var occupied = new bool[tiles.Length];
        var expectedChunks = checked((packed.Width + ChunkSize - 1) / ChunkSize *
            ((packed.Height + ChunkSize - 1) / ChunkSize));
        if (packed.TerrainChunks.Count != expectedChunks)
            throw new InvalidDataException("The private-world terrain chunk count is invalid.");
        foreach (var chunk in packed.TerrainChunks)
        {
            if (chunk is null || chunk.X < 0 || chunk.Y < 0 ||
                chunk.X % ChunkSize != 0 || chunk.Y % ChunkSize != 0 ||
                chunk.X >= packed.Width || chunk.Y >= packed.Height ||
                chunk.Width != Math.Min(ChunkSize, packed.Width - chunk.X) ||
                chunk.Height != Math.Min(ChunkSize, packed.Height - chunk.Y) ||
                chunk.Data is null || chunk.Data.Length != chunk.Width * chunk.Height ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(chunk.Data)).ToLowerInvariant(),
                    chunk.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("The private-world terrain chunk is damaged.");
            for (var localY = 0; localY < chunk.Height; localY++)
            for (var localX = 0; localX < chunk.Width; localX++)
            {
                var x = chunk.X + localX;
                var y = chunk.Y + localY;
                var index = y * packed.Width + x;
                if (occupied[index] || !Enum.IsDefined((TerrainKind)chunk.Data[localY * chunk.Width + localX]))
                    throw new InvalidDataException("The private-world terrain chunk overlaps or contains an unknown terrain kind.");
                occupied[index] = true;
                tiles[index] = new TerrainTile(new GridPoint(x, y),
                    (TerrainKind)chunk.Data[localY * chunk.Width + localX]);
            }
        }
        if (occupied.Any(value => !value))
            throw new InvalidDataException("The private-world terrain chunks leave missing tiles.");
        return new SeededMap(packed.Width, packed.Height, packed.GenerationAttempt, tiles,
            packed.CampObjects, packed.Resources, packed.ManifestDigest)
        {
            ClimateZones = packed.ClimateZones,
            WrapsEastWest = packed.WrapsEastWest,
        };
    }

    public override void Write(Utf8JsonWriter writer, SeededMap value, JsonSerializerOptions options)
    {
        if (value.Width is < 1 or > 4096 || value.Height is < 1 or > 2048 ||
            value.Tiles.Count != checked(value.Width * value.Height))
            throw new InvalidDataException("The private-world terrain dimensions are invalid.");
        var terrain = new byte[value.Tiles.Count];
        var occupied = new bool[terrain.Length];
        foreach (var tile in value.Tiles)
        {
            if (!value.Contains(tile.Position) || !Enum.IsDefined(tile.Terrain))
                throw new InvalidDataException("The private-world terrain tile is invalid.");
            var index = tile.Position.Y * value.Width + tile.Position.X;
            if (occupied[index]) throw new InvalidDataException("The private-world terrain has duplicate tiles.");
            occupied[index] = true;
            terrain[index] = checked((byte)tile.Terrain);
        }
        if (occupied.Any(present => !present))
            throw new InvalidDataException("The private-world terrain has missing tiles.");

        var chunks = new List<Chunk>();
        for (var y = 0; y < value.Height; y += ChunkSize)
        for (var x = 0; x < value.Width; x += ChunkSize)
        {
            var width = Math.Min(ChunkSize, value.Width - x);
            var height = Math.Min(ChunkSize, value.Height - y);
            var data = new byte[width * height];
            for (var localY = 0; localY < height; localY++)
                Array.Copy(terrain, (y + localY) * value.Width + x, data, localY * width, width);
            chunks.Add(new Chunk(x, y, width, height, data,
                Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant()));
        }
        JsonSerializer.Serialize(writer, new PackedMap(value.Width, value.Height, value.GenerationAttempt,
            value.CampObjects, value.Resources, value.ManifestDigest, Encoding, chunks,
            value.ClimateZones, value.WrapsEastWest), options);
    }
}
