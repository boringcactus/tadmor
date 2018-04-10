﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Humanizer;
using MoreLinq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Convolution;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.Processing.Drawing.Pens;
using SixLabors.ImageSharp.Processing.Text;
using SixLabors.ImageSharp.Processing.Transforms;
using SixLabors.Primitives;
using SixLabors.Shapes;
using Tadmor.Extensions;

namespace Tadmor.Services.Imaging
{
    public class ImagingService
    {
        private static readonly Font LargeBoldArial = SystemFonts.CreateFont("Arial", 35, FontStyle.Bold);
        private static readonly Font LargeSerif = SystemFonts.CreateFont("Times New Roman", 40);
        private static readonly Font SmallArial = new Font(LargeBoldArial, 20, FontStyle.Regular);

        public MemoryStream McDonalds(IEnumerable<(Random rng, byte[] avatarData)> rngAndAvatarDatas)
        {
            //constants
            const int s = 1280; //picture size
            const float extent = s * .5F;
            var color = Rgba32.Black;
            const double triangleRadius = s * 0.45;
            var trianglePosition = new PointF(extent, s * .62F);
            const string title = "CHILDREN YELLING: MCDONALDS! MCDONALDS! MCDONALDS!";
            const string opt1 = "\"We have food at home\"";
            const string opt2 = "*Pulls into the drive through as chilren cheer*\n" +
                                "*Orders a single black coffee and leaves*";
            const string opt3 = "\"MCDONALDS!\nMCDONALDS! MCDONALDS!\"";
            const float topTitleMargin = s * .02F;
            const float botMargin = s * 0.08F;
            const float topMargin = topTitleMargin + botMargin;

            //computed variables
            var rendererOptions = new RendererOptions(SmallArial);
            var opt2Extent = TextMeasurer.Measure(opt2, rendererOptions).Width / 2;
            var opt3Extent = TextMeasurer.Measure(opt3, rendererOptions).Width / 2;
            var triangle = new RegularPolygon(trianglePosition, 3, (int) triangleRadius, (float) Math.PI);
            var vertices = triangle.LineSegments.Single().Flatten();
            var output = new MemoryStream();
            using (var canvas = new Image<Rgba32>(s, s))
            {
                canvas.Mutate(c =>
                {
                    //background
                    c.Fill(Rgba32.White);
                    c.Draw(new Pen<Rgba32>(color, 5), triangle);
                    var t = new TextGraphicsOptions(true) {HorizontalAlignment = HorizontalAlignment.Center};
                    c.DrawText(t, title, LargeBoldArial, color, new PointF(extent, topTitleMargin));
                    c.DrawText(t, opt1, SmallArial, color, new PointF(extent, topMargin));
                    c.DrawText(t, opt2, SmallArial, color, new PointF(opt2Extent + s * .02F, s - botMargin));
                    c.DrawText(t, opt3, SmallArial, color, new PointF(s - (opt3Extent + s * .02F), s - botMargin));

                    //avatars
                    foreach (var (rng, avatarData) in rngAndAvatarDatas)
                        using (var avatar = CropCircle(avatarData))
                        {
                            var randomVertices = vertices.RandomSubset(2, rng).ToList();
                            var (a, b) = (randomVertices[0], randomVertices[1]);
                            //select a random point on any of the vertices of the path
                            //IPath.PointAlongPath seems to have a glitch so do it myself
                            var avatarPosition = a + (b - a) * (float) rng.NextDouble() - avatar.Size() / 2;
                            c.DrawImage(avatar, 1, new Point((int) avatarPosition.X, (int) avatarPosition.Y));
                        }
                });
                canvas.SaveAsPng(output);
            }

            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        public MemoryStream AlignmentChart(IEnumerable<(Random rng, byte[] avatarData)> rngAndAvatarDatas, string ea1,
            string ma, string ea2, string eb1, string mb, string eb2)
        {
            //constants
            const int w = 1480;
            const int h = 1024;
            const int horMargin = 30;
            const int verMargin = 20;
            const int textHeight = 80;
            const int textMargin = 10;
            var color = Rgba32.LightGray;

            //computed variables
            var cells = new[] {ea1, ma, ea2}
                .Cartesian(new[] {eb1, mb, eb2}, (s1, s2) => s1 == s2 ? $"true {s1}" : $"{s1} {s2}")
                .Batch(3)
                .SelectMany((col, x) => col.Select((cell, y) => (cell: cell.Humanize(LetterCasing.AllCaps), x, y)))
                .ToList();
            var alignmentString = string.Concat(cells.Select(t => t.cell));
            var rows = cells.Max(t => t.y) + 1;
            var cols = cells.Max(t => t.x) + 1;
            var cellW = w / cols;
            var cellH = h / rows;
            var avatarsByCell = rngAndAvatarDatas
                .Select(tuple => (rng: alignmentString.ToRandom(tuple.rng), tuple.avatarData))
                .ToLookup(
                    tuple => (tuple.rng.Next(cols), tuple.rng.Next(rows)),
                    tuple => CropCircle(tuple.avatarData));
            var textOptions = new TextGraphicsOptions(true) {HorizontalAlignment = HorizontalAlignment.Center};
            var output = new MemoryStream();
            using (var canvas = new Image<Rgba32>(w, h))
            {
                canvas.Mutate(c =>
                {
                    //background
                    c.Fill(Rgba32.Black);

                    foreach (var (text, x, y) in cells)
                    {
                        //alignment cell
                        var cellRect = new RectangleF(cellW * x, cellH * y, cellW, cellH - textHeight);
                        cellRect.Inflate(-horMargin, -verMargin);
                        var cellCenter = cellRect.Location + cellRect.Size / 2;

                        //alignment text
                        var textPosition = cellCenter + new PointF(0, cellRect.Height / 2 + textMargin);
                        c.DrawText(textOptions, text, LargeSerif, color, textPosition);

                        //avatars
                        var avatarsForCell = avatarsByCell[(x, y)].ToList();
                        if (avatarsForCell.Any())
                        {
                            //draw all the avatars on one image, then resize if necessary
                            var avatarWidth = avatarsForCell.First().Width;
                            var avatars = new Image<Rgba32>(avatarWidth * avatarsForCell.Count, avatarWidth);
                            avatars.Mutate(ac =>
                            {
                                for (var i = 0; i < avatarsForCell.Count; i++)
                                    ac.DrawImage(avatarsForCell[i], 1, new Point(i * avatarWidth, 0));
                                if (avatars.Width > cellRect.Width)
                                    ac.Resize((Size) (avatars.Size() * (cellRect.Width / avatars.Width)));
                            });
                            //use the average color from the avatars as cell background
                            var blurryAvatars = avatars.Clone();
                            blurryAvatars.Mutate(i => i.GaussianBlur(10));
                            var averageColor = blurryAvatars[blurryAvatars.Width / 2, blurryAvatars.Height / 2];
                            c.Fill(averageColor, cellRect);
                            var position = cellCenter - avatars.Size() / 2;
                            c.DrawImage(avatars, 1, (Point) position);
                        }
                        else
                        {
                            c.Fill(color, cellRect);
                        }
                    }
                });
                canvas.SaveAsPng(output);
            }

            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        private Image<Rgba32> CropCircle(byte[] imageData)
        {
            var image = Image.Load(imageData);
            var circle = new EllipsePolygon(new PointF(image.Size() / 2), image.Size());
            var rectangle = new RectangularPolygon(circle.Bounds);
            var exceptCircle = rectangle.Clip(circle);
            image.Mutate(i => i.Fill(
                new GraphicsOptions(true) {BlenderMode = PixelBlenderMode.Src}, //use overlay colors
                Rgba32.Transparent, //overlay with transparency
                exceptCircle)); //outside of the circle's bounds
            return image;
        }
    }
}