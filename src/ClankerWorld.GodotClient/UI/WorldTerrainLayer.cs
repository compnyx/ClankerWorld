using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Draws only camera-visible terrain; it never creates a node per tile.</summary>
public partial class WorldTerrainLayer : Control
{
    private WorldTerrainMap? world;
    private Texture2D? paletteTexture;
    private Rect2 visibleTiles;
    private int tileSize;
    private int tileGap;
    private bool wrapsEastWest;
    private Vector2I? hoveredTile;
    private byte[] trees = [];

    public int VisibleTileCount { get; private set; }

    public WorldTerrainLayer()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
    }

    public void SetWorld(WorldTerrainMap map)
    {
        world = map;
        // At overview scale, thousands of individual draw commands are much
        // slower than one nearest-neighbor pixel per tile from the same map.
        // This is a render cache, not a separate regional art set.
        var image = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.Rgba8);
        for (var y = 0; y < map.Height; y++)
            for (var x = 0; x < map.Width; x++)
                image.SetPixel(x, y, WorldTerrainMap.ColorFor(map.At(x, y)));
        paletteTexture = ImageTexture.CreateFromImage(image);
        trees = new byte[checked(map.Width * map.Height)];
        QueueRedraw();
    }

    public void SetTrees(IReadOnlyList<OwnerWorldResource> resources)
    {
        if (world is null) return;
        var next = new byte[checked(world.Width * world.Height)];
        foreach (var resource in resources)
        {
            var x = resource.Position.X;
            var y = resource.Position.Y;
            if (x < 0 || x >= world.Width || y < 0 || y >= world.Height) continue;
            var kind = resource.TreeKind switch
            {
                "broadleaf" => (byte)1,
                "conifer" => (byte)2,
                "orchard" => (byte)7,
                _ => (byte)0,
            };
            if (kind == 0) continue;
            var index = y * world.Width + x;
            if (next[index] != 0)
                throw new InvalidDataException("Two trees occupy one visible tile.");
            next[index] = kind == 7 ? resource.TreeStage switch
            {
                "picked" => (byte)8,
                "growing" => (byte)9,
                _ => (byte)7,
            } : resource.IsPlanted ? (byte)(kind + 4) :
                resource.Quantity == 0 || resource.State != "available"
                    ? (byte)(kind + 2) : kind;
        }
        trees = next;
        QueueRedraw();
    }

    public void SetCamera(Rect2 visible, int size, int gap, bool wrap)
    {
        visibleTiles = visible;
        tileSize = size;
        tileGap = gap;
        wrapsEastWest = wrap;
        var bounds = VisibleBounds();
        VisibleTileCount = bounds.Width * bounds.Height;
        QueueRedraw();
    }

    public Vector2I? HoveredTile => hoveredTile;

    public string? TreeStageAt(int x, int y)
    {
        if (world is null || x < 0 || y < 0 || x >= world.Width || y >= world.Height) return null;
        return trees[y * world.Width + x] switch
        {
            1 or 2 => "mature",
            3 or 4 => "stump",
            5 or 6 => "sapling",
            7 => "fruiting",
            8 => "picked",
            9 => "growing",
            _ => null,
        };
    }

    public void SetHoveredTile(Vector2I? tile)
    {
        if (hoveredTile == tile) return;
        hoveredTile = tile;
        QueueRedraw();
    }

    private (int Left, int Top, int Width, int Height) VisibleBounds()
    {
        if (world is null || tileSize <= 0) return (0, 0, 0, 0);
        var left = Mathf.FloorToInt(visibleTiles.Position.X);
        var top = Math.Clamp(Mathf.FloorToInt(visibleTiles.Position.Y), 0, world.Height);
        var right = Mathf.CeilToInt(visibleTiles.End.X);
        if (!wrapsEastWest)
        {
            left = Math.Clamp(left, 0, world.Width);
            right = Math.Clamp(right, left, world.Width);
        }
        var bottom = Math.Clamp(Mathf.CeilToInt(visibleTiles.End.Y), top, world.Height);
        return (left, top, right - left, bottom - top);
    }

    public override void _Draw()
    {
        if (world is null) return;
        var bounds = VisibleBounds();
        var stride = tileSize + tileGap;
        if (tileSize < 28 && tileGap == 0 && paletteTexture is not null)
        {
            var end = bounds.Left + bounds.Width;
            for (var x = bounds.Left; x < end;)
            {
                var sourceX = wrapsEastWest ? Mod(x, world.Width) : x;
                var width = Math.Min(end - x, world.Width - sourceX);
                DrawTextureRectRegion(paletteTexture,
                    new Rect2(x * stride, bounds.Top * stride, width * stride, bounds.Height * stride),
                    new Rect2(sourceX, bounds.Top, width, bounds.Height));
                x += width;
            }
        }
        else for (var y = bounds.Top; y < bounds.Top + bounds.Height; y++)
        {
            for (var x = bounds.Left; x < bounds.Left + bounds.Width; x++)
            {
                var kind = world.At(wrapsEastWest ? Mod(x, world.Width) : x, y);
                var position = new Vector2(x * stride, y * stride);
                DrawRect(new Rect2(position, new Vector2(tileSize, tileSize)), WorldTerrainMap.ColorFor(kind));
                if (tileSize >= 28 && kind is 2 or 3 or 4 or 10)
                {
                    var marker = kind is 3 or 10 ? "▲" : "≈";
                    DrawString(ThemeDB.FallbackFont, position + new Vector2(tileSize * 0.4f, tileSize * 0.65f),
                        marker, fontSize: Math.Clamp(tileSize / 5, 12, 28), modulate: new Color("E6F0E8"));
                }
            }
        }
        // Trees are objects, not baked ground colors: keep them visible both
        // above full-size tiles and above the small-tile palette cache.
        for (var y = bounds.Top; y < bounds.Top + bounds.Height; y++)
        for (var x = bounds.Left; x < bounds.Left + bounds.Width; x++)
        {
            var tree = trees[y * world.Width + (wrapsEastWest ? Mod(x, world.Width) : x)];
            if (tree != 0) DrawTree(new Vector2(x * stride, y * stride), tree);
        }
        if (hoveredTile is { } hover && tileSize > 0 &&
            hover.Y >= bounds.Top && hover.Y < bounds.Top + bounds.Height)
        {
            var inset = tileSize >= 8 ? 1f : 0f;
            for (var x = bounds.Left; x < bounds.Left + bounds.Width; x++)
            {
                if ((wrapsEastWest ? Mod(x, world.Width) : x) != hover.X) continue;
                DrawRect(new Rect2(new Vector2(x * stride + inset, hover.Y * stride + inset),
                    new Vector2(tileSize - inset * 2, tileSize - inset * 2)),
                    new Color("FFF0B5"), filled: false, width: tileSize >= 8 ? 2 : 1);
            }
        }
    }

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;

    private void DrawTree(Vector2 position, byte tree)
    {
        var center = position + new Vector2(tileSize * 0.5f, tileSize * 0.5f);
        if (tree == 9)
        {
            DrawCircle(center, Math.Max(2f, tileSize * 0.12f), new Color("795539"));
            DrawCircle(center - new Vector2(0, tileSize * 0.08f), Math.Max(2f, tileSize * 0.19f),
                new Color("6F9749"));
            return;
        }
        if (tree is 3 or 4)
        {
            DrawCircle(center, Math.Max(2f, tileSize * 0.16f), new Color("735036"));
            DrawCircle(center, Math.Max(1f, tileSize * 0.08f), new Color("A77C4C"));
            return;
        }
        if (tree is 5 or 6)
        {
            DrawCircle(center, Math.Max(2f, tileSize * 0.11f), new Color("735036"));
            DrawCircle(center - new Vector2(0, tileSize * 0.09f), Math.Max(2f, tileSize * 0.17f),
                tree == 6 ? new Color("7BA88B") : new Color("94B465"));
            return;
        }
        DrawCircle(center, Math.Max(2f, tileSize * 0.32f), new Color("273F2E"));
        var canopy = tree == 2 ? new Color("3E705D") : new Color("5F8744");
        DrawCircle(center - new Vector2(tileSize * 0.04f, tileSize * 0.05f),
            Math.Max(2f, tileSize * 0.27f), canopy);
        if (tileSize >= 20)
            DrawCircle(center - new Vector2(tileSize * 0.1f, tileSize * 0.12f),
                tileSize * 0.09f, tree == 2 ? new Color("7BA88B") : new Color("94B465"));
        if (tree == 7 && tileSize >= 14)
        {
            var fruitColor = new Color("DE8B4E");
            DrawCircle(center + new Vector2(tileSize * 0.13f, tileSize * 0.04f),
                Math.Max(1f, tileSize * 0.045f), fruitColor);
            DrawCircle(center + new Vector2(-tileSize * 0.12f, tileSize * 0.11f),
                Math.Max(1f, tileSize * 0.045f), fruitColor);
            DrawCircle(center + new Vector2(tileSize * 0.01f, -tileSize * 0.13f),
                Math.Max(1f, tileSize * 0.045f), fruitColor);
        }
    }
}
