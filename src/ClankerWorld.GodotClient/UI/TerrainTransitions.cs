using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>
/// Soft land-to-land edges. Where two ground surfaces meet, the higher one in
/// <see cref="Order"/> reaches a short, wandering way into its lower
/// neighbor: grass into sand, forest into grass, snow into rock. The edge is
/// organic rather than a line; a few bumps and gaps merge the two surfaces,
/// and its rim pixels are half-transparent so they blend.
/// <para>
/// Every tile corner on the map has its own reach (shallow, middle or deep),
/// and each edge piece runs from the reach at one corner to the reach at the
/// other. Neighboring pieces therefore meet exactly, and long boundaries
/// wander without repeating once per tile. Corner pieces keep diagonals round.
/// Water keeps its separate shoreline pieces.
/// </para>
/// </summary>
public static class TerrainTransitions
{
    public const int Levels = 3;
    public const int EdgeVariants = 2;
    private const int PiecesPerSide = Levels * Levels * EdgeVariants;
    private const int OuterCorner = 4 * PiecesPerSide;
    private const int InnerCorner = OuterCorner + 4 * Levels;
    public const int PieceCount = InnerCorner + 4 * Levels;
    private static readonly int StyleCount = Enum.GetValues<TerrainStyle>().Length;
    private static readonly Dictionary<int, Image> Images = [];
    private static readonly Dictionary<int, ImageTexture> Textures = [];
    private static readonly (int X, int Y)[] Offsets = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];
    // Reused per call: the terrain layer collects pieces tile by tile on the main thread.
    private static readonly int[] Around = new int[8];
    private static readonly int[] Candidates = new int[8];

    // Low to high: each surface reaches into the ones before it. Mountain and
    // peak relief reach into their neighbors but never take an edge themselves.
    private static readonly TerrainStyle[] Order =
    [
        TerrainStyle.Sand,
        TerrainStyle.ScrubSand,
        TerrainStyle.DesertBrush,
        TerrainStyle.DryBrush,
        TerrainStyle.DryScrub,
        TerrainStyle.FertileSoil,
        TerrainStyle.ScrubGrass,
        TerrainStyle.Grass,
        TerrainStyle.Tundra,
        TerrainStyle.ForestGrass,
        TerrainStyle.ForestFloor,
        TerrainStyle.DenseForestFloor,
        TerrainStyle.Rock,
        TerrainStyle.TundraSnow,
        TerrainStyle.Snow,
        TerrainStyle.Mountain,
        TerrainStyle.Peak,
    ];

    /// <summary>Overlap rank, or −1 for water and unknown ground, which take no land edges.</summary>
    public static int Rank(TerrainStyle style) => Array.IndexOf(Order, style);

    /// <summary>Whether <paramref name="over"/> reaches into a neighboring <paramref name="under"/> tile.</summary>
    public static bool Overlaps(TerrainStyle over, TerrainStyle under)
    {
        var high = Rank(over);
        var low = Rank(under);
        return high > low && low >= 0 &&
            under is not (TerrainStyle.Mountain or TerrainStyle.Peak) &&
            TerrainTextures.BaseColor(over) != TerrainTextures.BaseColor(under);
    }

    /// <summary>
    /// How far an edge reaches into its tile at a corner of the given level.
    /// The blend stays in a narrow band so every tile still reads as a square
    /// of its own ground; building and movement follow whole tiles.
    /// </summary>
    public static int Reach(int level, int atlasTileSize) => atlasTileSize >= 32
        ? level switch { 0 => 2, 1 => 4, _ => 6 }
        : level switch { 0 => 1, 1 => 2, _ => 3 };

    /// <summary>The deepest an edge may wander into a tile, a quarter of its width.</summary>
    public static int MaximumReach(int atlasTileSize) => atlasTileSize / 4;

    /// <summary>The reach level shared by every edge that meets at one map corner.</summary>
    public static int CornerLevel(int cornerX, int cornerY, int mapWidth, bool wrapsEastWest) =>
        (int)(PixelArt.Hash(wrapsEastWest ? (cornerX % mapWidth + mapWidth) % mapWidth : cornerX, cornerY, 133) % Levels);

    public static ImageTexture Atlas(int atlasTileSize)
    {
        if (Textures.TryGetValue(atlasTileSize, out var cached)) return cached;
        var texture = ImageTexture.CreateFromImage(AtlasImage(atlasTileSize));
        Textures[atlasTileSize] = texture;
        return texture;
    }

    public static Rect2 Region(TerrainStyle style, int piece, int atlasTileSize) =>
        new(piece * atlasTileSize, (int)style * atlasTileSize, atlasTileSize, atlasTileSize);

    /// <summary>One generated piece, for inspection and tests.</summary>
    public static Image Piece(TerrainStyle style, int piece, int atlasTileSize) =>
        AtlasImage(atlasTileSize).GetRegion(new Rect2I(piece * atlasTileSize, (int)style * atlasTileSize,
            atlasTileSize, atlasTileSize));

    /// <summary>
    /// Edge piece for a side (0 north, 1 east, 2 south, 3 west) running from
    /// its start corner (west or north end) to its end corner.
    /// </summary>
    public static int EdgePiece(int side, int startLevel, int endLevel, int variant) =>
        side * PiecesPerSide + (startLevel * Levels + endLevel) * EdgeVariants + variant;

    /// <summary>Outer corner piece (0 north-east, 1 south-east, 2 south-west, 3 north-west).</summary>
    public static int OuterCornerPiece(int corner, int level) => OuterCorner + corner * Levels + level;

    /// <summary>Inner corner piece that rounds two edges of the same surface.</summary>
    public static int InnerCornerPiece(int corner, int level) => InnerCorner + corner * Levels + level;

    /// <summary>
    /// The pieces to draw over one tile, lowest surface first so a higher one
    /// ends on top where two different neighbors meet at a corner.
    /// </summary>
    public static void Collect(WorldTerrainMap map, int x, int y, bool wrapsEastWest,
        List<(TerrainStyle Style, int Piece)> pieces)
    {
        pieces.Clear();
        var here = map.StyleAt(x, y);
        if (Rank(here) < 0 || here is TerrainStyle.Mountain or TerrainStyle.Peak) return;
        // Neighbors clockwise from north; a missing neighbor (map edge) is −1.
        var around = Around;
        for (var index = 0; index < 8; index++)
        {
            var nx = x + Offsets[index].X;
            var ny = y + Offsets[index].Y;
            if (wrapsEastWest) nx = (nx % map.Width + map.Width) % map.Width;
            around[index] = ny < 0 || ny >= map.Height || nx < 0 || nx >= map.Width ? -1 : (int)map.StyleAt(nx, ny);
        }

        // Distinct overlapping neighbors, lowest rank first.
        var candidates = Candidates;
        var count = 0;
        foreach (var neighbor in around)
        {
            if (neighbor < 0 || !Overlaps((TerrainStyle)neighbor, here) || Array.IndexOf(candidates, neighbor, 0, count) >= 0)
                continue;
            var slot = count++;
            while (slot > 0 && Rank((TerrainStyle)candidates[slot - 1]) > Rank((TerrainStyle)neighbor))
            {
                candidates[slot] = candidates[slot - 1];
                slot--;
            }
            candidates[slot] = neighbor;
        }
        if (count == 0) return;

        // Corner reach levels: north-east, south-east, south-west, north-west.
        var northEast = CornerLevel(x + 1, y, map.Width, wrapsEastWest);
        var southEast = CornerLevel(x + 1, y + 1, map.Width, wrapsEastWest);
        var southWest = CornerLevel(x, y + 1, map.Width, wrapsEastWest);
        var northWest = CornerLevel(x, y, map.Width, wrapsEastWest);
        Span<int> cornerLevels = [northEast, southEast, southWest, northWest];
        Span<int> sideStarts = [northWest, northEast, southWest, northWest];
        Span<int> sideEnds = [northEast, southEast, southEast, southWest];

        for (var entry = 0; entry < count; entry++)
        {
            var candidate = candidates[entry];
            var style = (TerrainStyle)candidate;
            // Sides: north, east, south, west sit at even neighbor indexes.
            for (var side = 0; side < 4; side++)
                if (around[side * 2] == candidate)
                    pieces.Add((style, EdgePiece(side, sideStarts[side], sideEnds[side],
                        (int)(PixelArt.Hash(x, y, 91 + side) % EdgeVariants))));
            // Corners: north-east, south-east, south-west, north-west.
            for (var corner = 0; corner < 4; corner++)
            {
                var first = around[corner * 2] == candidate;
                var second = around[(corner * 2 + 2) % 8] == candidate;
                if (first && second) pieces.Add((style, InnerCornerPiece(corner, cornerLevels[corner])));
                else if (!first && !second && around[corner * 2 + 1] == candidate)
                    pieces.Add((style, OuterCornerPiece(corner, cornerLevels[corner])));
            }
        }
    }

    private static Image AtlasImage(int size)
    {
        if (Images.TryGetValue(size, out var cached)) return cached;
        var width = size * PieceCount;
        var data = new byte[width * size * StyleCount * 4];
        foreach (var style in Order)
            for (var piece = 0; piece < PieceCount; piece++)
                PaintPiece(data, width, piece * size, (int)style * size, size, style, piece);
        var image = Image.CreateFromData(width, size * StyleCount, false, Image.Format.Rgba8, data);
        Images[size] = image;
        return image;
    }

    private static void PaintPiece(byte[] data, int stride, int left, int top, int size, TerrainStyle style, int piece)
    {
        var unit = size / 32f;
        var mask = new bool[size * size];
        var random = new PixelArt.Stream(PixelArt.Hash((int)style + 1, piece + 1, size));

        if (piece < OuterCorner)
        {
            var side = piece / PiecesPerSide;
            var levels = piece % PiecesPerSide / EdgeVariants;
            var start = Reach(levels / Levels, size);
            var end = Reach(levels % Levels, size);
            // Local coordinates: u runs along the edge, v is depth into the tile.
            void Mark(int u, int v, bool value)
            {
                if (u < 0 || v < 0 || u >= size || v >= size) return;
                var (px, py) = side switch
                {
                    0 => (u, v),
                    1 => (size - 1 - v, u),
                    2 => (u, size - 1 - v),
                    _ => (v, u),
                };
                mask[py * size + px] = value;
            }

            // Whole half-waves vanish at both ends, so the wandering never
            // moves the corners away from their shared reach.
            var wave1 = (random.Range(0, 41) - 20) / 10f;
            var wave2 = (random.Range(0, 31) - 15) / 10f;
            var wave3 = (random.Range(0, 21) - 10) / 10f;
            var wave5 = (random.Range(0, 11) - 5) / 10f;
            var profile = new int[size];
            for (var u = 0; u < size; u++)
            {
                var t = u / (float)(size - 1);
                var wobble = wave1 * Mathf.Sin(Mathf.Pi * t) + wave2 * Mathf.Sin(2 * Mathf.Pi * t) +
                    wave3 * Mathf.Sin(3 * Mathf.Pi * t) + wave5 * Mathf.Sin(5 * Mathf.Pi * t);
                profile[u] = Math.Clamp((int)MathF.Round(Mathf.Lerp(start, end, t) + unit * wobble),
                    1, MaximumReach(size) - (size >= 32 ? 2 : 1));
            }
            profile[0] = start;
            profile[size - 1] = end;
            for (var u = 0; u < size; u++)
                for (var v = 0; v < profile[u]; v++) Mark(u, v, true);

            // A few rounded bumps on the edge and small gaps just inside it
            // merge the two surfaces instead of drawing one clean line.
            var bumpWidth = size >= 32 ? 3 : 2;
            for (var bump = 0; bump < 2; bump++)
            {
                var u = random.Range(3, size - 3 - bumpWidth);
                var v = profile[u];
                for (var du = 0; du < bumpWidth; du++) Mark(u + du, v, true);
                if (size >= 32)
                    for (var du = 1; du < bumpWidth - 1; du++) Mark(u + du, v + 1, true);
            }
            if (size >= 32)
            {
                var u = random.Range(3, size - 5);
                var v = profile[u] - 2;
                if (v >= 2) Mark(u, v, false);
            }
        }
        else
        {
            var inner = piece >= InnerCorner;
            var index = piece - (inner ? InnerCorner : OuterCorner);
            var corner = index / Levels;
            var reach = Reach(index % Levels, size);
            // Local coordinates: u and v count away from the corner.
            var bend = random.Range(0, 21) / 10f;
            for (var v = 0; v < size; v++)
                for (var u = 0; u < size; u++)
                {
                    var du = u + 0.5f;
                    var dv = v + 0.5f;
                    var radius = inner
                        ? reach * 1.5f + unit + unit * bend * 0.5f
                        : reach + unit * bend * 0.5f * Mathf.Sin(2 * Mathf.Atan2(dv, du));
                    if (du * du + dv * dv >= radius * radius) continue;
                    var (px, py) = corner switch
                    {
                        0 => (size - 1 - u, v),
                        1 => (size - 1 - u, size - 1 - v),
                        2 => (u, size - 1 - v),
                        _ => (u, v),
                    };
                    mask[py * size + px] = true;
                }
            if (!inner)
            {
                // Meet the neighboring tiles' edges exactly at the corner's reach.
                var (cornerX, cornerY) = corner switch { 0 => (size - 1, 0), 1 => (size - 1, size - 1), 2 => (0, size - 1), _ => (0, 0) };
                var stepX = cornerX == 0 ? 1 : -1;
                var stepY = cornerY == 0 ? 1 : -1;
                for (var along = 0; along < size; along++)
                {
                    mask[cornerY * size + cornerX + along * stepX] = along < reach;
                    mask[(cornerY + along * stepY) * size + cornerX] = along < reach;
                }
            }
        }

        var baseColor = TerrainTextures.BaseColor(style);
        bool Covered(int px, int py) => px < 0 || py < 0 || px >= size || py >= size || mask[py * size + px];
        for (var py = 0; py < size; py++)
            for (var px = 0; px < size; px++)
            {
                if (!mask[py * size + px]) continue;
                // Rim pixels are half-transparent, blending the two surfaces.
                var rim = !Covered(px - 1, py) || !Covered(px + 1, py) || !Covered(px, py - 1) || !Covered(px, py + 1);
                var offset = ((top + py) * stride + left + px) * 4;
                data[offset] = (byte)MathF.Round(baseColor.R * 255);
                data[offset + 1] = (byte)MathF.Round(baseColor.G * 255);
                data[offset + 2] = (byte)MathF.Round(baseColor.B * 255);
                data[offset + 3] = (byte)(rim ? 166 : 255);
            }
    }
}
