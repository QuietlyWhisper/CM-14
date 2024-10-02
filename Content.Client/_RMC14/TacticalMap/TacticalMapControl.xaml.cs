﻿using System.Numerics;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Input;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._RMC14.TacticalMap;

[GenerateTypedNameReferences]
public sealed partial class TacticalMapControl : TextureRect
{
    private TacticalMapBlip[]? _blips;
    private Vector2i _min;
    private Vector2i _delta;
    private bool _dragging;
    private Vector2i? _lastDrag;
    public readonly List<TacticalMapLine> Lines = new();
    public int LineLimit;
    public bool Drawing { get; set; }
    public Color Color;

    public TacticalMapControl()
    {
        RobustXamlLoader.Load(this);
    }

    public void UpdateTexture(Entity<AreaGridComponent> grid)
    {
        if (grid.Comp.Colors.Count == 0)
            return;

        _min = Vector2i.Zero;
        var max = Vector2i.Zero;
        foreach (var position in grid.Comp.Colors.Keys)
        {
            _min = Vector2i.ComponentMin(_min, position);
            max = Vector2i.ComponentMax(max, position);
        }

        var width = max.X - _min.X;
        var height = max.Y - _min.Y;
        if (width <= 0 || height <= 0)
            return;

        _delta = max - _min;
        var image = new Image<Rgba32>(_delta.X + 1, _delta.Y + 1);
        foreach (var (position, color) in grid.Comp.Colors)
        {
            var (x, y) = GetDrawPosition(position);
            image[x, y] = new Rgba32(color.R, color.G, color.B, color.A);
        }

        Texture = Texture.LoadFromImage(image);
    }

    public void UpdateBlips(TacticalMapBlip[]? blips)
    {
        _blips = blips;
    }

    private Vector2i GetDrawPosition(Vector2i pos)
    {
        return new Vector2i(pos.X - _min.X, _delta.Y - (pos.Y - _min.Y));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (Texture == null)
            return;

        var system = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
        var backgroundRsi = new SpriteSpecifier.Rsi(new ResPath("_RMC14/Interface/map_blips.rsi"), "background");
        var undefibbableRsi = new SpriteSpecifier.Rsi(new ResPath("_RMC14/Interface/map_blips.rsi"), "undefibbable");
        var background = system.Frame0(backgroundRsi);
        var draw = GetDrawDimensions(Texture);
        var offset = new Vector2(draw.Left, draw.Top);
        if (_blips != null)
        {
            foreach (var blip in _blips)
            {
                var position = GetDrawPosition(blip.Indices) * 3 * UIScale;
                var rect = UIBox2.FromDimensions(position, new Vector2(14, 14));
                handle.DrawTextureRect(background, rect, blip.Color);
                handle.DrawTextureRect(system.Frame0(blip.Image), rect);

                if (blip.Undefibbable)
                    handle.DrawTextureRect(system.Frame0(undefibbableRsi), rect);
            }
        }

        var lineVectors = new Vector2[6];
        foreach (var line in Lines)
        {
            var start = (line.Start + offset) * UIScale;
            var end = (line.End + offset) * UIScale;
            var diff = end - start;
            var box = Box2.CenteredAround(start + diff / 2, new Vector2(5, (int) diff.Length()));
            var boxRotated = new Box2Rotated(box, diff.ToWorldAngle(), start + diff / 2);
            lineVectors[0] = boxRotated.BottomLeft;
            lineVectors[1] = boxRotated.BottomRight;
            lineVectors[2] = boxRotated.TopRight;
            lineVectors[3] = boxRotated.BottomLeft;
            lineVectors[4] = boxRotated.TopLeft;
            lineVectors[5] = boxRotated.TopRight;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, lineVectors, line.Color);
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _dragging = true;
            _lastDrag = args.RelativePosition.Floored();
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _dragging = false;
            _lastDrag = null;
        }
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!Drawing || !_dragging)
            return;

        var relative = args.RelativePosition.Floored();
        if (_lastDrag == null)
        {
            _lastDrag = relative;
            return;
        }

        var diff = relative - _lastDrag.Value;
        if (diff == Vector2i.Zero)
            return;

        if (diff.Length < 10)
            return;

        if (Texture == null)
            return;

        Lines.Add(new TacticalMapLine(_lastDrag.Value, relative, Color));
        while (LineLimit >= 0 && Lines.Count > LineLimit)
        {
            Lines.RemoveAt(0);
        }

        _lastDrag = relative;
    }
}
