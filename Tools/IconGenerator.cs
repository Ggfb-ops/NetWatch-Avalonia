using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using SkiaSharp;

public class IconGenerator
{
    public static void Generate(string outputPath)
    {
        const int size = 512;
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var bgPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(size, size),
                [new SKColor(0x7c, 0x3a, 0xed), new SKColor(0x4f, 0x46, 0xe5)],
                SKShaderTileMode.Clamp),
            IsAntialias = true
        };

        var roundRect = new SKRoundRect(new SKRect(16, 16, size - 16, size - 16), 108);
        canvas.DrawRoundRect(roundRect, bgPaint);

        using var shieldPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(256, 72), new SKPoint(256, 456),
                [new SKColor(0xa7, 0x8b, 0xfa), new SKColor(0x6d, 0x28, 0xd9)],
                SKShaderTileMode.Clamp),
            IsAntialias = true
        };

        using var shieldPath = new SKPath();
        shieldPath.MoveTo(256, 72);
        shieldPath.LineTo(400, 136);
        shieldPath.LineTo(400, 280);
        shieldPath.CubicTo(400, 370, 340, 430, 256, 456);
        shieldPath.CubicTo(172, 430, 112, 370, 112, 280);
        shieldPath.LineTo(112, 136);
        shieldPath.Close();
        canvas.DrawPath(shieldPath, shieldPaint);

        using var shieldStroke = new SKPaint { Color = new SKColor(0xc4, 0xb5, 0xfd, 0x66), IsAntialias = true, IsStroke = true, StrokeWidth = 2 };
        canvas.DrawPath(shieldPath, shieldStroke);

        using var eyeWhite = new SKPaint { Color = new SKColor(0xe0, 0xe7, 0xff, 0xe6), IsAntialias = true };
        canvas.DrawOval(new SKRect(156, 204, 356, 316), eyeWhite);

        using var eyeGlow = new SKPaint { Color = new SKColor(0x8b, 0x5c, 0xf6, 0x40), IsAntialias = true };
        canvas.DrawOval(new SKRect(146, 198, 366, 322), eyeGlow);

        using var iris = new SKPaint { Color = new SKColor(0x4f, 0x46, 0xe5), IsAntialias = true };
        canvas.DrawCircle(256, 260, 38, iris);

        using var pupil = new SKPaint { Color = new SKColor(0x0f, 0x0f, 0x23), IsAntialias = true };
        canvas.DrawCircle(256, 260, 20, pupil);

        using var highlight = new SKPaint { Color = new SKColor(0xff, 0xff, 0xff, 0xcc), IsAntialias = true };
        canvas.DrawCircle(266, 250, 8, highlight);
        using var highlight2 = new SKPaint { Color = new SKColor(0xff, 0xff, 0xff, 0x66), IsAntialias = true };
        canvas.DrawCircle(248, 268, 3, highlight2);

        using var scanLine = new SKPaint { Color = new SKColor(0x8b, 0x5c, 0xf6, 0x4d), IsAntialias = true, StrokeWidth = 1.5f, IsStroke = true };
        canvas.DrawLine(156, 260, 356, 260, scanLine);

        using var logGreen = new SKPaint { Color = new SKColor(0x4a, 0xde, 0x80, 0x80), IsAntialias = true, StrokeWidth = 3, StrokeCap = SKStrokeCap.Round, IsStroke = true };
        canvas.DrawLine(170, 340, 342, 340, logGreen);

        using var logYellow = new SKPaint { Color = new SKColor(0xea, 0xb3, 0x08, 0x80), IsAntialias = true, StrokeWidth = 3, StrokeCap = SKStrokeCap.Round, IsStroke = true };
        canvas.DrawLine(185, 358, 310, 358, logYellow);

        using var logRed = new SKPaint { Color = new SKColor(0xef, 0x44, 0x44, 0x80), IsAntialias = true, StrokeWidth = 3, StrokeCap = SKStrokeCap.Round, IsStroke = true };
        canvas.DrawLine(200, 376, 280, 376, logRed);

        using var logGray = new SKPaint { Color = new SKColor(0x64, 0x74, 0x8b, 0x55), IsAntialias = true, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round, IsStroke = true };
        canvas.DrawLine(210, 394, 330, 394, logGray);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.OpenWrite(outputPath);
        data.SaveTo(fs);
    }

    public static void GenerateIco(string pngPath, string icoPath)
    {
        var sizes = new[] { 16, 32, 48, 256 };
        using var fs = new FileStream(icoPath, FileMode.Create);
        using var writer = new BinaryWriter(fs);

        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)sizes.Length);

        var pngData = File.ReadAllBytes(pngPath);

        var entries = new (int Size, int Offset)[sizes.Length];
        var offset = 6 + sizes.Length * 16;

        for (var i = 0; i < sizes.Length; i++)
        {
            var s = sizes[i];
            writer.Write((byte)(s >= 256 ? 0 : s));
            writer.Write((byte)(s >= 256 ? 0 : s));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write((int)pngData.Length);
            writer.Write((int)offset);
            entries[i] = (s, offset);
            offset += pngData.Length;
        }

        for (var i = 0; i < sizes.Length; i++)
        {
            writer.Write(pngData);
        }
    }
}
