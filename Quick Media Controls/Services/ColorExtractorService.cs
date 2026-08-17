using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quick_Media_Controls.Services
{
    public sealed class FlyoutPalette
    {
        public Color DominantColor { get; init; }
        public Color AccentColor { get; init; }
        public Color SecondaryColor { get; init; }
        public Color HoverColor { get; init; }
        public Color PressedColor { get; init; }
        public Color IconColor { get; init; }

        public Brush AmbientBrush { get; init; } = Brushes.Transparent;
        public SolidColorBrush PlayButtonBrush { get; init; } = Brushes.Transparent;
        public SolidColorBrush PlayButtonBorderBrush { get; init; } = Brushes.Transparent;
        public SolidColorBrush PlayButtonHoverBrush { get; init; } = Brushes.Transparent;
        public SolidColorBrush PlayButtonHoverBorderBrush { get; init; } = Brushes.Transparent;
        public SolidColorBrush PlayButtonPressedBrush { get; init; } = Brushes.Transparent;
        public SolidColorBrush PlayButtonPressedBorderBrush { get; init; } = Brushes.Transparent;
        public SolidColorBrush PlayButtonForegroundBrush { get; init; } = Brushes.White;

        public static FlyoutPalette CreateFallback(bool isDarkMode)
        {
            Color defaultAccent = isDarkMode ? Color.FromRgb(0, 120, 215) : Color.FromRgb(0, 103, 192);
            Color secondaryAccent = isDarkMode ? Color.FromRgb(30, 80, 138) : Color.FromRgb(50, 130, 184);

            Color hoverAccent = AdjustLuminance(defaultAccent, isDarkMode ? 0.12f : -0.10f);
            Color pressedAccent = AdjustLuminance(defaultAccent, isDarkMode ? -0.10f : -0.18f);

            Color borderAccent = AdjustLuminance(defaultAccent, isDarkMode ? 0.10f : -0.08f);
            Color hoverBorderAccent = AdjustLuminance(hoverAccent, isDarkMode ? 0.10f : -0.08f);
            Color pressedBorderAccent = AdjustLuminance(pressedAccent, isDarkMode ? 0.10f : -0.08f);

            Color iconColor = Colors.White;

            var playBrush = CreateFrozenSolidBrush(defaultAccent);
            var playBorderBrush = CreateFrozenSolidBrush(borderAccent);
            var hoverBrush = CreateFrozenSolidBrush(hoverAccent);
            var hoverBorderBrush = CreateFrozenSolidBrush(hoverBorderAccent);
            var pressedBrush = CreateFrozenSolidBrush(pressedAccent);
            var pressedBorderBrush = CreateFrozenSolidBrush(pressedBorderAccent);
            var iconBrush = CreateFrozenSolidBrush(iconColor);

            Color stop1 = Color.FromArgb(isDarkMode ? (byte)100 : (byte)80, defaultAccent.R, defaultAccent.G, defaultAccent.B);
            Color midBlend = ColorExtractorService.BlendColors(defaultAccent, secondaryAccent, 0.5f);
            Color stop2 = Color.FromArgb(isDarkMode ? (byte)75 : (byte)60, midBlend.R, midBlend.G, midBlend.B);
            Color stop3 = Color.FromArgb(isDarkMode ? (byte)55 : (byte)45, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B);

            var ambientGradient = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0.5),
                EndPoint = new System.Windows.Point(1, 0.5),
                GradientStops = new GradientStopCollection
                {
                    new(stop1, 0.0),
                    new(stop2, 0.45),
                    new(stop3, 1.0)
                }
            };
            ambientGradient.Freeze();

            return new FlyoutPalette
            {
                DominantColor = defaultAccent,
                AccentColor = defaultAccent,
                SecondaryColor = secondaryAccent,
                HoverColor = hoverAccent,
                PressedColor = pressedAccent,
                IconColor = iconColor,
                AmbientBrush = ambientGradient,
                PlayButtonBrush = playBrush,
                PlayButtonBorderBrush = playBorderBrush,
                PlayButtonHoverBrush = hoverBrush,
                PlayButtonHoverBorderBrush = hoverBorderBrush,
                PlayButtonPressedBrush = pressedBrush,
                PlayButtonPressedBorderBrush = pressedBorderBrush,
                PlayButtonForegroundBrush = iconBrush
            };
        }

        public static SolidColorBrush CreateFrozenSolidBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public static Color AdjustLuminance(Color color, float factor)
        {
            if (factor > 0)
            {
                byte r = (byte)Math.Clamp(color.R + (255 - color.R) * factor, 0, 255);
                byte g = (byte)Math.Clamp(color.G + (255 - color.G) * factor, 0, 255);
                byte b = (byte)Math.Clamp(color.B + (255 - color.B) * factor, 0, 255);
                return Color.FromArgb(color.A, r, g, b);
            }
            else
            {
                float multiplier = 1.0f + factor;
                byte r = (byte)Math.Clamp(color.R * multiplier, 0, 255);
                byte g = (byte)Math.Clamp(color.G * multiplier, 0, 255);
                byte b = (byte)Math.Clamp(color.B * multiplier, 0, 255);
                return Color.FromArgb(color.A, r, g, b);
            }
        }
    }

    public static class ColorExtractorService
    {
        private sealed class ColorCluster
        {
            public int Count { get; set; }
            public long TotalR { get; set; }
            public long TotalG { get; set; }
            public long TotalB { get; set; }

            public Color AverageColor =>
                Color.FromRgb(
                    (byte)(TotalR / Count),
                    (byte)(TotalG / Count),
                    (byte)(TotalB / Count));
        }

        public static async Task<FlyoutPalette> ExtractPaletteAsync(BitmapSource? bitmap, bool isDarkMode)
        {
            if (bitmap == null)
            {
                return FlyoutPalette.CreateFallback(isDarkMode);
            }

            try
            {
                return await Task.Run(() => ExtractPaletteInternal(bitmap, isDarkMode));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Palette extraction failed: {ex.Message}");
                return FlyoutPalette.CreateFallback(isDarkMode);
            }
        }

        private static FlyoutPalette ExtractPaletteInternal(BitmapSource source, bool isDarkMode)
        {
            BitmapSource formattedSource = source;
            if (source.Format != PixelFormats.Bgra32)
            {
                var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                formattedSource = converted;
            }

            int width = formattedSource.PixelWidth;
            int height = formattedSource.PixelHeight;

            if (width <= 0 || height <= 0)
            {
                return FlyoutPalette.CreateFallback(isDarkMode);
            }

            int sampleStepX = Math.Max(1, width / 40);
            int sampleStepY = Math.Max(1, height / 40);

            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            formattedSource.CopyPixels(pixels, stride, 0);

            long totalR = 0, totalG = 0, totalB = 0;
            int validSampleCount = 0;

            var clusterMap = new Dictionary<int, ColorCluster>();

            for (int y = 0; y < height; y += sampleStepY)
            {
                for (int x = 0; x < width; x += sampleStepX)
                {
                    int index = y * stride + x * 4;
                    byte b = pixels[index];
                    byte g = pixels[index + 1];
                    byte r = pixels[index + 2];
                    byte a = pixels[index + 3];

                    if (a < 128) continue;

                    totalR += r;
                    totalG += g;
                    totalB += b;
                    validSampleCount++;

                    int binKey = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);

                    if (!clusterMap.TryGetValue(binKey, out var cluster))
                    {
                        cluster = new ColorCluster();
                        clusterMap[binKey] = cluster;
                    }

                    cluster.Count++;
                    cluster.TotalR += r;
                    cluster.TotalG += g;
                    cluster.TotalB += b;
                }
            }

            Color dominantColor;
            if (validSampleCount > 0)
            {
                dominantColor = Color.FromRgb(
                    (byte)(totalR / validSampleCount),
                    (byte)(totalG / validSampleCount),
                    (byte)(totalB / validSampleCount));
            }
            else
            {
                dominantColor = isDarkMode ? Color.FromRgb(30, 30, 35) : Color.FromRgb(240, 240, 245);
            }

            var candidates = new List<(Color color, int count, float score)>();

            foreach (var cluster in clusterMap.Values)
            {
                if (cluster.Count == 0) continue;

                Color avgColor = cluster.AverageColor;

                float rf = avgColor.R / 255f;
                float gf = avgColor.G / 255f;
                float bf = avgColor.B / 255f;

                float max = Math.Max(rf, Math.Max(gf, bf));
                float min = Math.Min(rf, Math.Min(gf, bf));
                float delta = max - min;

                float lum = (max + min) / 2f;
                float sat = (delta == 0) ? 0 : (delta / (1f - Math.Abs(2f * lum - 1f)));

                if (lum >= 0.12f && lum <= 0.90f && sat >= 0.15f)
                {
                    float populationRatio = (float)cluster.Count / Math.Max(1, validSampleCount);
                    float lumTarget = isDarkMode ? 0.50f : 0.45f;
                    float lumScore = 1f - Math.Abs(lum - lumTarget);

                    float score = (float)(Math.Pow(populationRatio, 0.6) * Math.Pow(sat, 1.3) * (0.5f + 0.5f * lumScore));
                    candidates.Add((avgColor, cluster.Count, score));
                }
            }

            Color primaryAccent;
            Color secondaryTone;

            if (candidates.Count > 0)
            {
                var rankedCandidates = candidates.OrderByDescending(c => c.score).ToList();
                primaryAccent = rankedCandidates[0].color;

                var secondaryCandidate = rankedCandidates
                    .Skip(1)
                    .FirstOrDefault(c => ColorDistance(c.color, primaryAccent) >= 48);

                if (secondaryCandidate != default && secondaryCandidate.count > 0)
                {
                    secondaryTone = secondaryCandidate.color;
                }
                else
                {
                    secondaryTone = DeriveHarmonicSecondary(primaryAccent, dominantColor, isDarkMode);
                }
            }
            else
            {
                float domLum = (0.299f * dominantColor.R + 0.587f * dominantColor.G + 0.114f * dominantColor.B) / 255f;
                if (domLum > 0.85f || domLum < 0.15f)
                {
                    primaryAccent = isDarkMode ? Color.FromRgb(0, 120, 215) : Color.FromRgb(0, 103, 192);
                    secondaryTone = isDarkMode ? Color.FromRgb(30, 80, 138) : Color.FromRgb(50, 130, 184);
                }
                else
                {
                    primaryAccent = dominantColor;
                    secondaryTone = DeriveHarmonicSecondary(primaryAccent, dominantColor, isDarkMode);
                }
            }

            primaryAccent = OptimizeAccentLuminance(primaryAccent, isDarkMode);
            secondaryTone = OptimizeSecondaryLuminance(secondaryTone, isDarkMode);

            Color hoverColor = FlyoutPalette.AdjustLuminance(primaryAccent, isDarkMode ? 0.18f : -0.14f);
            Color pressedColor = FlyoutPalette.AdjustLuminance(primaryAccent, isDarkMode ? -0.12f : -0.22f);

            Color borderAccent = FlyoutPalette.AdjustLuminance(primaryAccent, isDarkMode ? 0.08f : -0.06f);
            Color hoverBorderAccent = FlyoutPalette.AdjustLuminance(hoverColor, isDarkMode ? 0.08f : -0.06f);
            Color pressedBorderAccent = FlyoutPalette.AdjustLuminance(pressedColor, isDarkMode ? 0.08f : -0.06f);

            double perceivedBrightness = (0.299 * primaryAccent.R + 0.587 * primaryAccent.G + 0.114 * primaryAccent.B);
            Color iconColor = perceivedBrightness > 145 ? Color.FromRgb(24, 24, 27) : Colors.White;

            Color ambientStop1;
            Color ambientStop2;
            Color ambientStop3;

            Color midBlend = BlendColors(primaryAccent, secondaryTone, 0.5f);

            if (isDarkMode)
            {
                ambientStop1 = Color.FromArgb(120, primaryAccent.R, primaryAccent.G, primaryAccent.B);
                ambientStop2 = Color.FromArgb(85, midBlend.R, midBlend.G, midBlend.B);
                ambientStop3 = Color.FromArgb(65, secondaryTone.R, secondaryTone.G, secondaryTone.B);
            }
            else
            {
                ambientStop1 = Color.FromArgb(135, primaryAccent.R, primaryAccent.G, primaryAccent.B);
                ambientStop2 = Color.FromArgb(95, midBlend.R, midBlend.G, midBlend.B);
                ambientStop3 = Color.FromArgb(75, secondaryTone.R, secondaryTone.G, secondaryTone.B);
            }

            var ambientGradient = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0.5),
                EndPoint = new System.Windows.Point(1, 0.5),
                GradientStops = new GradientStopCollection
                {
                    new(ambientStop1, 0.0),
                    new(ambientStop2, 0.45),
                    new(ambientStop3, 1.0)
                }
            };
            ambientGradient.Freeze();

            var playBrush = FlyoutPalette.CreateFrozenSolidBrush(primaryAccent);
            var playBorderBrush = FlyoutPalette.CreateFrozenSolidBrush(borderAccent);
            var hoverBrush = FlyoutPalette.CreateFrozenSolidBrush(hoverColor);
            var hoverBorderBrush = FlyoutPalette.CreateFrozenSolidBrush(hoverBorderAccent);
            var pressedBrush = FlyoutPalette.CreateFrozenSolidBrush(pressedColor);
            var pressedBorderBrush = FlyoutPalette.CreateFrozenSolidBrush(pressedBorderAccent);
            var iconBrush = FlyoutPalette.CreateFrozenSolidBrush(iconColor);

            return new FlyoutPalette
            {
                DominantColor = dominantColor,
                AccentColor = primaryAccent,
                SecondaryColor = secondaryTone,
                HoverColor = hoverColor,
                PressedColor = pressedColor,
                IconColor = iconColor,
                AmbientBrush = ambientGradient,
                PlayButtonBrush = playBrush,
                PlayButtonBorderBrush = playBorderBrush,
                PlayButtonHoverBrush = hoverBrush,
                PlayButtonHoverBorderBrush = hoverBorderBrush,
                PlayButtonPressedBrush = pressedBrush,
                PlayButtonPressedBorderBrush = pressedBorderBrush,
                PlayButtonForegroundBrush = iconBrush
            };
        }

        public static double ColorDistance(Color c1, Color c2)
        {
            double dr = c1.R - c2.R;
            double dg = c1.G - c2.G;
            double db = c1.B - c2.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        public static Color BlendColors(Color c1, Color c2, float ratio)
        {
            float r2 = Math.Clamp(ratio, 0f, 1f);
            float r1 = 1f - r2;
            byte r = (byte)Math.Clamp(c1.R * r1 + c2.R * r2, 0, 255);
            byte g = (byte)Math.Clamp(c1.G * r1 + c2.G * r2, 0, 255);
            byte b = (byte)Math.Clamp(c1.B * r1 + c2.B * r2, 0, 255);
            return Color.FromRgb(r, g, b);
        }

        private static Color OptimizeAccentLuminance(Color color, bool isDarkMode)
        {
            float lum = (0.299f * color.R + 0.587f * color.G + 0.114f * color.B) / 255f;
            if (isDarkMode && lum < 0.45f)
            {
                return FlyoutPalette.AdjustLuminance(color, Math.Min(0.35f, 0.46f - lum));
            }
            else if (!isDarkMode)
            {
                if (lum < 0.35f)
                {
                    return FlyoutPalette.AdjustLuminance(color, 0.38f - lum);
                }
                else if (lum > 0.70f)
                {
                    return FlyoutPalette.AdjustLuminance(color, -(lum - 0.65f));
                }
            }
            return color;
        }

        private static Color OptimizeSecondaryLuminance(Color color, bool isDarkMode)
        {
            float lum = (0.299f * color.R + 0.587f * color.G + 0.114f * color.B) / 255f;
            if (isDarkMode && lum < 0.30f)
            {
                return FlyoutPalette.AdjustLuminance(color, 0.32f - lum);
            }
            else if (!isDarkMode && lum < 0.40f)
            {
                return FlyoutPalette.AdjustLuminance(color, 0.42f - lum);
            }
            return color;
        }

        private static Color DeriveHarmonicSecondary(Color primary, Color dominant, bool isDarkMode)
        {
            if (ColorDistance(primary, dominant) >= 35)
            {
                return BlendColors(primary, dominant, 0.6f);
            }

            return FlyoutPalette.AdjustLuminance(primary, isDarkMode ? -0.22f : 0.18f);
        }
    }
}
