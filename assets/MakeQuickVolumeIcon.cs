// ============================================================================
// MakeQuickVolumeIcon.cs
// Vector Icon & Multi-Resolution ICO Generator for QuickVolume
// 
// Motif: Modern Speaker / Audio Vector Glyph
// - Crisp warm white / platinum speaker body (#FFFFFF -> #F4DBD8 -> #EDEDED)
// - Vibrant electric turquoise concentric sound waves (#00D4FF)
// - Dark contour outline & soft drop shadow for dual-theme contrast (dark & light taskbars)
// - Pixel-aligned dedicated rendering for 16x16 and multi-resolution pyramid
// - Generates: quickvolume.ico (16, 20, 24, 32, 48, 64, 128, 256 pure 32-bit PNG frames)
//              quickvolume.png (256x256 high-resolution preview)
// ============================================================================

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

namespace QuickVolume.Assets
{
    class MakeQuickVolumeIcon
    {
        static int Main(string[] args)
        {
            try
            {
                string outputDir = AppDomain.CurrentDomain.BaseDirectory;
                if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                {
                    outputDir = args[0];
                }

                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                Console.WriteLine("==================================================");
                Console.WriteLine("QuickVolume Vector Icon Generator");
                Console.WriteLine("Output Directory: {0}", outputDir);
                Console.WriteLine("==================================================");

                int[] sizes = new int[] { 16, 20, 24, 32, 48, 64, 128, 256 };
                List<byte[]> pngFrames = new List<byte[]>();
                Bitmap masterBmp = null;

                for (int i = 0; i < sizes.Length; i++)
                {
                    int sz = sizes[i];
                    Console.Write("Rendering frame {0}x{0} ... ", sz);
                    using (Bitmap bmp = RenderSpeakerGlyph(sz))
                    {
                        if (sz == 256)
                        {
                            masterBmp = new Bitmap(bmp);
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Png);
                            pngFrames.Add(ms.ToArray());
                        }
                    }
                    Console.WriteLine("OK ({0} bytes PNG)", pngFrames[i].Length);
                }

                // 1. Save High-Res Preview PNG (256x256)
                string pngFilePath = Path.Combine(outputDir, "quickvolume.png");
                if (masterBmp != null)
                {
                    masterBmp.Save(pngFilePath, ImageFormat.Png);
                    masterBmp.Dispose();
                    Console.WriteLine("[Created] {0} ({1} bytes)", pngFilePath, new FileInfo(pngFilePath).Length);
                }

                // 2. Save Multi-Resolution ICO with PNG frames
                string icoFilePath = Path.Combine(outputDir, "quickvolume.ico");
                WriteIcoWithPngFrames(icoFilePath, sizes, pngFrames);
                Console.WriteLine("[Created] {0} ({1} bytes, {2} frames)", icoFilePath, new FileInfo(icoFilePath).Length, sizes.Length);

                Console.WriteLine("==================================================");
                Console.WriteLine("Icon generation completed successfully!");
                Console.WriteLine("==================================================");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error generating icon: {0}", ex);
                return 1;
            }
        }

        /// <summary>
        /// Renders the modern audio speaker glyph tuned for the specified icon size.
        /// </summary>
        static Bitmap RenderSpeakerGlyph(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                // --- Color Palette ---
                // Warm white / platinum gradient for speaker body
                Color whiteTop = Color.FromArgb(255, 255, 255, 255);
                Color warmPlatinum = Color.FromArgb(255, 244, 237, 235); // #F4DBD8 subtle warm tone
                Color neutralMid = Color.FromArgb(255, 237, 237, 237);   // #EDEDED crisp platinum
                Color bodyBase = Color.FromArgb(255, 212, 215, 222);     // Soft shadow metallic base

                // High-contrast dark outline & shadow for light & dark taskbars
                Color darkOutline = Color.FromArgb(235, 14, 18, 24);      // Deep charcoal outline (#0E1218)
                Color shadowColor = Color.FromArgb(85, 10, 12, 18);       // Soft drop shadow

                // Windows 11 Warm Silver / Monochrome Platinum for sound arcs (matching Windows 11 flyout palette)
                Color cyanHighlight = Color.FromArgb(255, 255, 255, 255); // Pure white highlight (#FFFFFF)
                Color cyanMain = Color.FromArgb(255, 230, 230, 230);       // Crisp platinum (#E6E6E6)
                Color cyanBase = Color.FromArgb(255, 166, 165, 161);       // Windows 11 Warm Silver Accent (#A6A5A1)

                // --- Resolution-Specific Metrics & Geometry ---
                float bx0, bx1, by0, by1;
                float hx1, hy0, hy1;
                float arcCx, arcCy;
                float[] arcRadii;
                float[] arcSpans;
                float cornerRadius;
                float outlinePenWidth;
                float arcStrokeWidth;
                float arcDarkWidth;
                float shadowOffsetY;

                if (size == 16)
                {
                    // Dedicated pixel-aligned tuning for 16x16:
                    // 1 bold sound arc, sharp corners, zero sub-pixel micro-strokes, no blurry drop shadow
                    bx0 = 3.0f; bx1 = 5.5f; by0 = 5.0f; by1 = 10.0f;
                    hx1 = 8.5f; hy0 = 3.0f; hy1 = 12.0f;
                    arcCx = 5.5f; arcCy = 7.5f;
                    arcRadii = new float[] { 6.3f };
                    arcSpans = new float[] { 76f };
                    cornerRadius = 0f;
                    outlinePenWidth = 2.0f;
                    arcStrokeWidth = 1.8f;
                    arcDarkWidth = 3.0f;
                    shadowOffsetY = 0f;
                }
                else if (size == 20)
                {
                    // 20x20: 1 arc, crisp alignment
                    bx0 = 4.0f; bx1 = 7.0f; by0 = 6.5f; by1 = 13.5f;
                    hx1 = 11.0f; hy0 = 4.0f; hy1 = 16.0f;
                    arcCx = 7.0f; arcCy = 10.0f;
                    arcRadii = new float[] { 8.0f };
                    arcSpans = new float[] { 76f };
                    cornerRadius = 0f;
                    outlinePenWidth = 2.2f;
                    arcStrokeWidth = 2.0f;
                    arcDarkWidth = 3.4f;
                    shadowOffsetY = 0f;
                }
                else if (size == 24)
                {
                    // 24x24: 2 concentric arcs
                    bx0 = 4.0f; bx1 = 8.0f; by0 = 7.5f; by1 = 15.5f;
                    hx1 = 13.0f; hy0 = 4.5f; hy1 = 18.5f;
                    arcCx = 8.5f; arcCy = 11.5f;
                    arcRadii = new float[] { 7.0f, 10.5f };
                    arcSpans = new float[] { 66f, 78f };
                    cornerRadius = 1.0f;
                    outlinePenWidth = 2.2f;
                    arcStrokeWidth = 1.8f;
                    arcDarkWidth = 3.0f;
                    shadowOffsetY = 0.6f;
                }
                else if (size == 32)
                {
                    // 32x32: standard taskbar & tray size, 2 concentric arcs, subtle drop shadow
                    bx0 = 5.5f; bx1 = 11.0f; by0 = 10.0f; by1 = 21.0f;
                    hx1 = 17.5f; hy0 = 6.0f; hy1 = 25.0f;
                    arcCx = 11.5f; arcCy = 15.5f;
                    arcRadii = new float[] { 9.5f, 14.2f };
                    arcSpans = new float[] { 66f, 80f };
                    cornerRadius = 1.6f;
                    outlinePenWidth = 2.5f;
                    arcStrokeWidth = 2.2f;
                    arcDarkWidth = 3.8f;
                    shadowOffsetY = 1.0f;
                }
                else if (size == 48)
                {
                    // 48x48: high-DPI tray / menu size, 2 concentric arcs, refined gradients
                    bx0 = 8.5f; bx1 = 16.5f; by0 = 15.0f; by1 = 31.5f;
                    hx1 = 26.5f; hy0 = 9.0f; hy1 = 37.5f;
                    arcCx = 17.5f; arcCy = 23.25f;
                    arcRadii = new float[] { 14.5f, 21.5f };
                    arcSpans = new float[] { 66f, 80f };
                    cornerRadius = 2.5f;
                    outlinePenWidth = 3.2f;
                    arcStrokeWidth = 3.2f;
                    arcDarkWidth = 5.0f;
                    shadowOffsetY = 1.5f;
                }
                else if (size == 64)
                {
                    // 64x64: 2 concentric arcs
                    bx0 = 11.0f; bx1 = 22.0f; by0 = 20.0f; by1 = 42.0f;
                    hx1 = 35.5f; hy0 = 12.0f; hy1 = 50.0f;
                    arcCx = 23.5f; arcCy = 31.0f;
                    arcRadii = new float[] { 19.0f, 28.5f };
                    arcSpans = new float[] { 66f, 80f };
                    cornerRadius = 3.5f;
                    outlinePenWidth = 3.8f;
                    arcStrokeWidth = 4.0f;
                    arcDarkWidth = 6.4f;
                    shadowOffsetY = 2.0f;
                }
                else if (size == 128)
                {
                    // 128x128: 3 concentric sound arcs
                    bx0 = 22.0f; bx1 = 44.0f; by0 = 40.0f; by1 = 84.0f;
                    hx1 = 71.0f; hy0 = 24.0f; hy1 = 100.0f;
                    arcCx = 47.0f; arcCy = 62.0f;
                    arcRadii = new float[] { 28.0f, 43.0f, 58.0f };
                    arcSpans = new float[] { 62f, 74f, 86f };
                    cornerRadius = 6.0f;
                    outlinePenWidth = 5.5f;
                    arcStrokeWidth = 6.0f;
                    arcDarkWidth = 9.5f;
                    shadowOffsetY = 3.0f;
                }
                else // 256
                {
                    // 256x256: Full master resolution, 3 concentric arcs, rich gradients, specular highlights
                    bx0 = 44.0f; bx1 = 88.0f; by0 = 80.0f; by1 = 168.0f;
                    hx1 = 142.0f; hy0 = 48.0f; hy1 = 200.0f;
                    arcCx = 94.0f; arcCy = 124.0f;
                    arcRadii = new float[] { 56.0f, 86.0f, 116.0f };
                    arcSpans = new float[] { 62f, 74f, 86f };
                    cornerRadius = 12.0f;
                    outlinePenWidth = 9.0f;
                    arcStrokeWidth = 11.5f;
                    arcDarkWidth = 17.5f;
                    shadowOffsetY = 5.0f;
                }

                // --- Construct Speaker Silhouette Path ---
                GraphicsPath speakerPath = new GraphicsPath();
                if (cornerRadius > 0.8f)
                {
                    speakerPath.AddArc(bx0, by0, cornerRadius * 2, cornerRadius * 2, 180, 90);
                    speakerPath.AddLine(bx0 + cornerRadius, by0, bx1, by0);
                    speakerPath.AddLine(bx1, by0, hx1, hy0);
                    speakerPath.AddLine(hx1, hy0, hx1, hy1);
                    speakerPath.AddLine(hx1, hy1, bx1, by1);
                    speakerPath.AddLine(bx1, by1, bx0 + cornerRadius, by1);
                    speakerPath.AddArc(bx0, by1 - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 90, 90);
                    speakerPath.CloseFigure();
                }
                else
                {
                    PointF[] pts = new PointF[] {
                        new PointF(bx0, by0),
                        new PointF(bx1, by0),
                        new PointF(hx1, hy0),
                        new PointF(hx1, hy1),
                        new PointF(bx1, by1),
                        new PointF(bx0, by1)
                    };
                    speakerPath.AddPolygon(pts);
                }

                // --- 1. Soft Ambient / Key Drop Shadow (for sizes >= 24) ---
                if (shadowOffsetY > 0.1f)
                {
                    using (Matrix sm = new Matrix())
                    {
                        sm.Translate(0, shadowOffsetY);
                        using (GraphicsPath sPath = (GraphicsPath)speakerPath.Clone())
                        {
                            sPath.Transform(sm);
                            using (Pen sPen = new Pen(shadowColor, outlinePenWidth + shadowOffsetY * 0.5f))
                            {
                                sPen.LineJoin = LineJoin.Round;
                                g.DrawPath(sPen, sPath);
                            }
                            using (SolidBrush sBrush = new SolidBrush(shadowColor))
                            {
                                g.FillPath(sBrush, sPath);
                            }
                        }
                    }

                    // Shadow for arcs
                    for (int i = 0; i < arcRadii.Length; i++)
                    {
                        float r = arcRadii[i];
                        float span = arcSpans[i];
                        float startA = -span / 2.0f;
                        RectangleF shadowArcRect = new RectangleF(arcCx - r, arcCy - r + shadowOffsetY, r * 2, r * 2);
                        using (Pen arcSPen = new Pen(shadowColor, arcDarkWidth))
                        {
                            arcSPen.StartCap = LineCap.Round;
                            arcSPen.EndCap = LineCap.Round;
                            g.DrawArc(arcSPen, shadowArcRect, startA, span);
                        }
                    }
                }

                // --- 2. Dark Protective Outlines (Ensures visibility on white/light taskbars) ---
                using (Pen outPen = new Pen(darkOutline, outlinePenWidth))
                {
                    outPen.LineJoin = LineJoin.Round;
                    g.DrawPath(outPen, speakerPath);
                }

                for (int i = 0; i < arcRadii.Length; i++)
                {
                    float r = arcRadii[i];
                    float span = arcSpans[i];
                    float startA = -span / 2.0f;
                    RectangleF arcRect = new RectangleF(arcCx - r, arcCy - r, r * 2, r * 2);
                    using (Pen arcDarkPen = new Pen(darkOutline, arcDarkWidth))
                    {
                        arcDarkPen.StartCap = LineCap.Round;
                        arcDarkPen.EndCap = LineCap.Round;
                        g.DrawArc(arcDarkPen, arcRect, startA, span);
                    }
                }

                // --- 3. Fill Speaker Body with Satin Platinum Gradient ---
                using (LinearGradientBrush lgb = new LinearGradientBrush(
                    new PointF(bx0, by0),
                    new PointF(hx1, hy1),
                    whiteTop,
                    bodyBase))
                {
                    ColorBlend cb = new ColorBlend(4);
                    cb.Colors = new Color[] { whiteTop, warmPlatinum, neutralMid, bodyBase };
                    cb.Positions = new float[] { 0.0f, 0.35f, 0.70f, 1.0f };
                    lgb.InterpolationColors = cb;
                    g.FillPath(lgb, speakerPath);
                }

                // --- 4. Specular Inner Rim Highlight on Horn Mouth (for sizes >= 48) ---
                if (size >= 48)
                {
                    float rimInset = (size >= 128) ? 3.0f : 1.5f;
                    using (Pen rimPen = new Pen(Color.FromArgb(170, 255, 255, 255), rimInset))
                    {
                        rimPen.StartCap = LineCap.Round;
                        rimPen.EndCap = LineCap.Round;
                        g.DrawLine(rimPen, hx1 - rimInset, hy0 + rimInset * 2, hx1 - rimInset, hy1 - rimInset * 2);
                    }
                }

                // --- 5. Draw Vibrant Sound Arcs ---
                for (int i = 0; i < arcRadii.Length; i++)
                {
                    float r = arcRadii[i];
                    float span = arcSpans[i];
                    float startA = -span / 2.0f;
                    RectangleF arcRect = new RectangleF(arcCx - r, arcCy - r, r * 2, r * 2);

                    if (size >= 48)
                    {
                        using (LinearGradientBrush arcGrad = new LinearGradientBrush(
                            new PointF(arcCx, arcCy - r),
                            new PointF(arcCx, arcCy + r),
                            cyanHighlight,
                            cyanBase))
                        {
                            ColorBlend acb = new ColorBlend(3);
                            acb.Colors = new Color[] { cyanHighlight, cyanMain, cyanBase };
                            acb.Positions = new float[] { 0.0f, 0.5f, 1.0f };
                            arcGrad.InterpolationColors = acb;
                            using (Pen arcPen = new Pen(arcGrad, arcStrokeWidth))
                            {
                                arcPen.StartCap = LineCap.Round;
                                arcPen.EndCap = LineCap.Round;
                                g.DrawArc(arcPen, arcRect, startA, span);
                            }
                        }
                    }
                    else
                    {
                        using (Pen arcPen = new Pen(cyanMain, arcStrokeWidth))
                        {
                            arcPen.StartCap = LineCap.Round;
                            arcPen.EndCap = LineCap.Round;
                            g.DrawArc(arcPen, arcRect, startA, span);
                        }
                    }
                }

                speakerPath.Dispose();
            }
            return bmp;
        }

        /// <summary>
        /// Writes an ICO file containing PNG-compressed 32-bit ARGB frames.
        /// Fully compatible with Windows Vista/7/8/10/11 Shell, Qt (QIcon), and modern asset pipelines.
        /// </summary>
        static void WriteIcoWithPngFrames(string filePath, int[] sizes, List<byte[]> pngFrames)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    // --- ICONDIR (6 bytes) ---
                    bw.Write((ushort)0);                 // Reserved (must be 0)
                    bw.Write((ushort)1);                 // Resource Type: 1 = Icon
                    bw.Write((ushort)sizes.Length);      // Number of image frames

                    // --- ICONDIRENTRY array (16 bytes per image) ---
                    int offset = 6 + (sizes.Length * 16);
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        int sz = sizes[i];
                        byte bWidth = (byte)(sz >= 256 ? 0 : sz);
                        byte bHeight = (byte)(sz >= 256 ? 0 : sz);

                        bw.Write(bWidth);                     // Image width (0 = 256)
                        bw.Write(bHeight);                    // Image height (0 = 256)
                        bw.Write((byte)0);                    // Color count (0 = >=8bpp)
                        bw.Write((byte)0);                    // Reserved
                        bw.Write((ushort)1);                  // Color planes
                        bw.Write((ushort)32);                 // Bits per pixel
                        bw.Write((uint)pngFrames[i].Length);  // Resource size in bytes
                        bw.Write((uint)offset);               // Resource data offset

                        offset += pngFrames[i].Length;
                    }

                    // --- Image Data (PNG streams) ---
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        bw.Write(pngFrames[i]);
                    }
                }
            }
        }
    }
}
