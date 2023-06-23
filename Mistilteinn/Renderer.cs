using SkiaSharp;

namespace Mistilteinn;

public static class Renderer
{
    [Flags]
    private enum Quadrant
    {
        First = 1,
        Second = 1 << 1,
        Third = 1 << 2,
        Fourth = 1 << 3
    }
    
    private const int LatticeSize = 60;
    private const float InnerBorder = 30f;
    private const float OuterBorder = InnerBorder - 5;
    private const float DecorationLength = 15;
    private const float DecorationMargin = 5;
    private const float LastMoveDecorationLength = 10;
    private const int Rows = 9;
    private const int Columns = 8;
    private const float BottomMarginExtra = 50f;
    private const float RightMarginExtra = 50f;
    private const float ImageWidth = LatticeSize * Columns + InnerBorder * 2;
    private const float ImageHeight = LatticeSize * Rows + InnerBorder * 2;

    private static readonly Lazy<Task<SKTypeface>> Typeface = new(async () =>
    {
        var asm = typeof(Renderer).Assembly;
        await using var stream = asm.GetManifestResourceStream("Mistilteinn.fonts.font.ttf");
        return SKTypeface.FromStream(stream);
    });

    public static async Task<MemoryStream> RenderAsync(Chessboard chessboard, int lastMoveX = -1, int lastMoveY = -1)
    {
        var info = new SKImageInfo((int) (ImageWidth + RightMarginExtra), (int) (ImageHeight + BottomMarginExtra));
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.DrawColor(SKColors.BurlyWood);
        await DrawEmptyChessboardAsync(canvas); 
        var taskList = new List<Task>();
        var tuples = from piece in Enum.GetValues<Pieces>()
            from side in Enum.GetValues<Side>()
            select (piece, side);
        foreach (var (piece, side) in tuples)
        {
            var positions = chessboard.PositionOf(piece, side);
            foreach (var (x, y) in positions)
                taskList.Add(DrawPieceAsync(canvas, piece, side, x, y));
        }
        
        if (lastMoveX != -1 && lastMoveY != -1)
        {
            DrawLastMoveDecoration(canvas, lastMoveX, lastMoveY);
        }

        await Task.WhenAll(taskList);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Seek(0L, SeekOrigin.Begin);
        return stream;
    }

    private static void DrawLastMoveDecoration(SKCanvas canvas, int x, int y)
    {
        var (coordX, coordY) = TranslateCoordinates(x, y);
        using var pen = PenOf(SKColors.Red);
        const int half = LatticeSize / 2;
        canvas.DrawLine(coordX - half, coordY - half, coordX - half + LastMoveDecorationLength, coordY - half, pen);
        canvas.DrawLine(coordX - half, coordY - half, coordX - half, coordY - half + LastMoveDecorationLength, pen);
        canvas.DrawLine(coordX + half, coordY - half, coordX + half - LastMoveDecorationLength, coordY - half, pen);
        canvas.DrawLine(coordX + half, coordY - half, coordX + half, coordY - half + LastMoveDecorationLength, pen);
        canvas.DrawLine(coordX - half, coordY + half, coordX - half + LastMoveDecorationLength, coordY + half, pen);
        canvas.DrawLine(coordX - half, coordY + half, coordX - half, coordY + half - LastMoveDecorationLength, pen);
        canvas.DrawLine(coordX + half, coordY + half, coordX + half - LastMoveDecorationLength, coordY + half, pen);
        canvas.DrawLine(coordX + half, coordY + half, coordX + half, coordY + half - LastMoveDecorationLength, pen);
    }

    private static async Task DrawPieceAsync(SKCanvas canvas, Pieces pieces, Side side, int coordX, int coordY)
    {
        using var borderPen = PenOf(SKColors.Black);
        using var backgroundPen = PenOf(SKColors.Bisque);
        using var foregroundPen = PenOf(side == Side.Red ? SKColors.Crimson : SKColors.Black);
        var (x, y) = TranslateCoordinates(coordX, coordY);
        canvas.DrawCircle(x, y, (LatticeSize + 4) / 2f - 5, borderPen);
        canvas.DrawCircle(x, y, LatticeSize / 2f - 5, backgroundPen);
        canvas.DrawText(pieces.GetPieceName(side).ToString(), x - 20, y + 10, new SKFont(await Typeface.Value, 40f), foregroundPen);
    }
    
    private static async Task DrawEmptyChessboardAsync(SKCanvas canvas)
    {
        DrawOuterBorder(canvas);
        DrawInnerBorder(canvas);
        DrawBlackLattices(canvas);
        DrawRedLattices(canvas);
        DrawBlackDecoration(canvas);
        DrawRedDecoration(canvas);
        DrawBlackPalace(canvas);
        DrawRedPalace(canvas);
        await DrawIndexesAsync(canvas);
        await DrawRiverAsync(canvas);
    }

    private static async Task DrawRiverAsync(SKCanvas canvas)
    {
        // rotate 90 degree to left
        canvas.RotateDegrees(-90, (ImageWidth + RightMarginExtra) / 2f, (ImageHeight + BottomMarginExtra) / 2f);
        using var pen = PenOf(SKColors.Black);
        var (leftX, leftY) = TranslateCoordinates(4f, 7f);
        canvas.DrawText("楚", leftX + 25, leftY, new SKFont(await Typeface.Value, 50f), pen);
        canvas.DrawText("河", leftX + 25, leftY + 100, new SKFont(await Typeface.Value, 50f), pen);
        canvas.RotateDegrees(180, (ImageWidth + RightMarginExtra) / 2f, (ImageHeight + BottomMarginExtra) / 2f);
        var (rightX, rightY) = TranslateCoordinates(3.5f, 7f);
        canvas.DrawText("汉", rightX + 5, rightY + 30, new SKFont(await Typeface.Value, 50f), pen);
        canvas.DrawText("界", rightX + 5, rightY + 130, new SKFont(await Typeface.Value, 50f), pen);
        canvas.RotateDegrees(-90, (ImageWidth + RightMarginExtra) / 2f, (ImageHeight + RightMarginExtra) / 2f);
    }

    private static async Task DrawIndexesAsync(SKCanvas canvas)
    {
        using var pen = PenOf(SKColors.Black);
        foreach (var i in Enumerable.Range(1, 9))
        {
            var coord = TranslateCoordinates(i - 1, 0);
            canvas.DrawText(i.ToChineseNumeral().ToString(), coord.x - 20f, coord.y + LatticeSize, new SKFont(await Typeface.Value, 40f), pen);
        }

        foreach (var i in Enumerable.Range(1, 10))
        {
            var coord = TranslateCoordinates(8, i - 1);
            canvas.DrawText(i.ToChineseNumeral().ToString(), coord.x + 25f, coord.y + 15f, new SKFont(await Typeface.Value, 40f), pen);
        }
    }
    
    private static void DrawRedPalace(SKCanvas canvas)
    {
        using var pen = PenOf(SKColors.Black);
        var (leftTopX, leftTopY) = TranslateCoordinates(3, 0);
        var (rightBottomX, rightBottomY) = TranslateCoordinates(5, 2);
        var (leftBottomX, leftBottomY) = TranslateCoordinates(3, 2);
        var (rightTopX, rightTopY) = TranslateCoordinates(5, 0);
        canvas.DrawLine(leftTopX, leftTopY, rightBottomX, rightBottomY, pen);
        canvas.DrawLine(leftBottomX, leftBottomY, rightTopX, rightTopY, pen);
    }
    
    private static void DrawBlackPalace(SKCanvas canvas)
    {
        using var pen = PenOf(SKColors.Black);
        var (leftTopX, leftTopY) = TranslateCoordinates(3, 9);
        var (rightBottomX, rightBottomY) = TranslateCoordinates(5, 7);
        var (leftBottomX, leftBottomY) = TranslateCoordinates(3, 7);
        var (rightTopX, rightTopY) = TranslateCoordinates(5, 9);
        canvas.DrawLine(leftTopX, leftTopY, rightBottomX, rightBottomY, pen);
        canvas.DrawLine(leftBottomX, leftBottomY, rightTopX, rightTopY, pen);
    }

    private static void DrawRedDecoration(SKCanvas canvas)
    {
        using var pen = PenOf(SKColors.Black);
        var (leftRedCannonX, leftRedCannonY) = TranslateCoordinates(1, 7);
        var (rightRedCannonX, rightRedCannonY) = TranslateCoordinates(7, 7);
        DrawDecorationAround(canvas, leftRedCannonX, leftRedCannonY);
        DrawDecorationAround(canvas, rightRedCannonX, rightRedCannonY);
        foreach (var i in Enumerates.Range(0, 10, 2))
        {
            var (pawnX, pawnY) = TranslateCoordinates(i, 6);
            DrawDecorationAround(canvas, pawnX, pawnY, i switch
            {
                0 => Quadrant.Second | Quadrant.Third,
                8 => Quadrant.First | Quadrant.Fourth,
                _ => Quadrant.First | Quadrant.Second | Quadrant.Third | Quadrant.Fourth
            });
        }
    }
    
    private static void DrawBlackDecoration(SKCanvas canvas)
    {
        using var pen = PenOf(SKColors.Black);
        var (leftRedCannonX, leftRedCannonY) = TranslateCoordinates(1, 2);
        var (rightRedCannonX, rightRedCannonY) = TranslateCoordinates(7, 2);
        DrawDecorationAround(canvas, leftRedCannonX, leftRedCannonY);
        DrawDecorationAround(canvas, rightRedCannonX, rightRedCannonY);
        foreach (var i in Enumerates.Range(0, 10, 2))
        {
            var (pawnX, pawnY) = TranslateCoordinates(i, 3);
            DrawDecorationAround(canvas, pawnX, pawnY, i switch
            {
                0 => Quadrant.Second | Quadrant.Third,
                8 => Quadrant.First | Quadrant.Fourth,
                _ => Quadrant.First | Quadrant.Second | Quadrant.Third | Quadrant.Fourth
            });
        }
    }

    private static void DrawDecorationAround(
        SKCanvas canvas, 
        float x,
        float y,
        Quadrant includes = Quadrant.First | Quadrant.Second | Quadrant.Third | Quadrant.Fourth)
    {
        using var pen = PenOf(SKColors.Black);
        if ((includes & Quadrant.First) == Quadrant.First)
        {
            var (firstQuadrantX, firstQuadrantY) = (x - DecorationMargin, y - DecorationMargin);
            canvas.DrawLine(firstQuadrantX, firstQuadrantY, firstQuadrantX - DecorationLength, firstQuadrantY, pen);
            canvas.DrawLine(firstQuadrantX, firstQuadrantY, firstQuadrantX, firstQuadrantY - DecorationLength, pen);
        }
        
        if ((includes & Quadrant.Second) == Quadrant.Second)
        {
            var (secondQuadrantX, secondQuadrantY) = (x + DecorationMargin, y - DecorationMargin);
            canvas.DrawLine(secondQuadrantX, secondQuadrantY, secondQuadrantX + DecorationLength, secondQuadrantY, pen); 
            canvas.DrawLine(secondQuadrantX, secondQuadrantY, secondQuadrantX, secondQuadrantY - DecorationLength, pen);
        }
        
        if ((includes & Quadrant.Third) == Quadrant.Third)
        {
            var (thirdQuadrantX, thirdQuadrantY) = (x + DecorationMargin, y + DecorationMargin);
            canvas.DrawLine(thirdQuadrantX, thirdQuadrantY, thirdQuadrantX + DecorationLength, thirdQuadrantY, pen);
            canvas.DrawLine(thirdQuadrantX, thirdQuadrantY, thirdQuadrantX, thirdQuadrantY + DecorationLength, pen);
        }
        
        if ((includes & Quadrant.Fourth) == Quadrant.Fourth)
        {
            var (fourthQuadrantX, fourthQuadrantY) = (x - DecorationMargin, y + DecorationMargin);
            canvas.DrawLine(fourthQuadrantX, fourthQuadrantY, fourthQuadrantX - DecorationLength, fourthQuadrantY, pen);
            canvas.DrawLine(fourthQuadrantX, fourthQuadrantY, fourthQuadrantX, fourthQuadrantY + DecorationLength, pen);
        }
    }

    private static void DrawRedLattices(SKCanvas canvas)
    {
        using var pen = PenOf(SKColors.Black);
        for (var i = 0; i < 5; i++)
        {
            var (startX, startY) = TranslateCoordinates(0, i);
            var (endX, endY) = TranslateCoordinates(8, i);
            canvas.DrawLine(startX, startY, endX, endY, pen);
        }

        for (var i = 0; i < 9; i++)
        {
            var (startX, startY) = TranslateCoordinates(i, 0);
            var (endX, endY) = TranslateCoordinates(i, 4);
            canvas.DrawLine(startX, startY, endX, endY, pen);
        }
    }

    private static void DrawBlackLattices(SKCanvas canvas)
    {
        using var pen = PenOf(SKColors.Black);
        for (var i = 5; i < 9; i++)
        {
            var (startX, startY) = TranslateCoordinates(0, i);
            var (endX, endY) = TranslateCoordinates(8, i);
            canvas.DrawLine(startX, startY, endX, endY, pen);
        }

        for (var i = 0; i < 9; i++)
        {
            var (startX, startY) = TranslateCoordinates(i, 5);
            var (endX, endY) = TranslateCoordinates(i, 9);
            canvas.DrawLine(startX, startY, endX, endY, pen);
        }
    }

    private static void DrawInnerBorder(SKCanvas canvas)
    {
        using var paint = PenOf(SKColors.Black);
        // top line
        canvas.DrawLine(InnerBorder, InnerBorder, ImageWidth - InnerBorder, InnerBorder, paint);
        // right line
        canvas.DrawLine(ImageWidth - InnerBorder, InnerBorder, ImageWidth - InnerBorder, ImageHeight - InnerBorder, paint);
        // bottom line
        canvas.DrawLine(ImageWidth - InnerBorder, ImageHeight - InnerBorder, InnerBorder, ImageHeight - InnerBorder, paint);
        // left line
        canvas.DrawLine(InnerBorder, ImageHeight - InnerBorder, InnerBorder, InnerBorder, paint);
    }
    
    private static void DrawOuterBorder(SKCanvas canvas)
    {
        using var paint = PenOf(SKColors.Black, 2.5f);
        // bottom line
        canvas.DrawLine(OuterBorder, OuterBorder, ImageWidth - OuterBorder, OuterBorder, paint);
        // right line
        canvas.DrawLine(ImageWidth - OuterBorder, OuterBorder, ImageWidth - OuterBorder, ImageHeight - OuterBorder, paint);
        // top line
        canvas.DrawLine(ImageWidth - OuterBorder, ImageHeight - OuterBorder, OuterBorder, ImageHeight - OuterBorder, paint);
        // left line
        canvas.DrawLine(OuterBorder, ImageHeight - OuterBorder, OuterBorder, OuterBorder, paint);
    }
    
    private static (float x, float y) TranslateCoordinates(float coordX, float coordY)
    {
        var yOffset = InnerBorder + LatticeSize * (9 - coordY);
        var xOffset = InnerBorder + LatticeSize * coordX;
        return (xOffset, yOffset);
    }

    private static SKPaint PenOf(SKColor color, float strokeWidth = 1.5f)
    {
        return new SKPaint
        {
            IsAntialias = true,
            Color = color,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = strokeWidth
        };
    }
}