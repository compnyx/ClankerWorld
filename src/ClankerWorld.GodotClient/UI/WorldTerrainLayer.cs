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
}
