using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RVR_CS.Core;
using RVR_CS.Sensors;

namespace RVR_CS.Examples
{
    /// <summary>
    /// Test rapide du capteur de couleur corrigé
    /// </summary>
    public class QuickColorTest
    {
        private const int SAMPLE_WINDOW = 12;
        private const int STABLE_THRESHOLD = 6;
        private const int PROTOTYPE_DISTANCE_THRESHOLD = 11000;

        private static readonly object _sampleLock = new();
        private static readonly Queue<ColorSample> _recentSamples = new();
        private static ColorCategory _stableCategory = ColorCategory.Unknown;
        private static DateTime _lastStablePrint = DateTime.MinValue;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 === TEST RAPIDE CAPTEUR COULEUR ===");
            Console.WriteLine("Basé sur l'analyse des logs BLE officiels Sphero");
            
            try
            {
                // Connexion
                var controller = new RvrController("RV-829B");
                if (!await controller.ConnectAsync())
                {
                    Console.WriteLine("❌ Impossible de se connecter au RVR+");
                    return;
                }
                
                Console.WriteLine("✅ Connecté au RVR+");
                
                // S'abonner aux données de couleur
                controller.ColorDataReceived += (colorData) =>
                {
                    var (r, g, b, index, confidence) = colorData;
                    if (LooksLikeLedNoise(r, g, b))
                    {
                        return;
                    }

                 

                    var category = CategorizeColor(r, g, b);
                    lock (_sampleLock)
                    {
                        _recentSamples.Enqueue(new ColorSample(r, g, b, category));
                        if (_recentSamples.Count > SAMPLE_WINDOW)
                        {
                            _recentSamples.Dequeue();
                        }

                        var stable = DetermineStableCategory();
                        if (stable != ColorCategory.Unknown &&
                            (stable != _stableCategory ||
                             (DateTime.Now - _lastStablePrint).TotalSeconds > 3))
                        {
                            _stableCategory = stable;
                            _lastStablePrint = DateTime.Now;
                            var (avgR, avgG, avgB) = AverageSamplesFor(stable);
                            var friendlyName = Describe(stable);
                            var consoleColor = GetConsoleColor(stable);
							Console.WriteLine($"🎨 COULEUR REÇUE: R={r} G={g} B={b} Confidence={confidence}");
							var previousColor = Console.ForegroundColor;
                            Console.ForegroundColor = consoleColor;
                            Console.WriteLine($"   ✅ Couleur confirmée: {friendlyName} (moyenne R={avgR} G={avgG} B={avgB})");
                            Console.ForegroundColor = previousColor;
                        }
                    }
                };
                
                // Activation
                var colorSensor = new ColorSensorManager(controller);
                Console.WriteLine("\n🔧 Activation du capteur...");
                if (await colorSensor.ActivateAsync())
                {
                    Console.WriteLine("✅ Capteur activé !");
                    Console.WriteLine("\n🎯 Placez des objets colorés devant le capteur...");
                    Console.WriteLine("📊 Les données devraient apparaître automatiquement !");
                    
                    // Attendre et observer
                    for (int i = 0; i < 30; i++)
                    {
                        Console.WriteLine($"⏱️ Écoute... {i+1}/30s");
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    Console.WriteLine("❌ Échec d'activation");
                }
                
                await controller.DisconnectAsync();
                Console.WriteLine("👋 Déconnecté");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur: {ex.Message}");
            }
            
            Console.WriteLine("\nAppuyez sur une touche pour quitter...");
            Console.ReadKey();
        }

        private enum ColorCategory
        {
            Unknown,
            Black,
            White,
            Gray,
            Red,
            Orange,
            Yellow,
            Green,
            Cyan,
            Blue,
            Violet,
            Magenta,
            Brown
        }

        private readonly struct ColorSample
        {
            public ColorSample(byte r, byte g, byte b, ColorCategory category)
            {
                R = r;
                G = g;
                B = b;
                Category = category;
            }

            public byte R { get; }
            public byte G { get; }
            public byte B { get; }
            public ColorCategory Category { get; }
        }

        private static bool LooksLikeLedNoise(byte r, byte g, byte b)
        {
            // Pulses emitted by the color sensor LED when the official sequence primes the sensor
            if (r >= 120 && r <= 136 && g <= 7 && b <= 7) return true;

            // Deep-blue sync bursts that appear between real samples
            if (r <= 6 && g <= 6 && b >= 80 && b <= 120) return true;
            if (r <= 6 && g <= 6 && b >= 160 && b <= 215) return true;

            // Dark frames right before the LED lights up
            if (r <= 6 && g <= 6 && b <= 40) return true;

            // Startup frames: slight green component with high blue
            if (r <= 4 && g <= 22 && b >= 200 && b <= 225) return true;

            return false;
        }
        private static readonly Dictionary<ColorCategory, (int R, int G, int B)> Prototypes = new()
        {
            { ColorCategory.Black,   (15, 15, 15) },
            { ColorCategory.White,   (240, 240, 240) },
            { ColorCategory.Gray,    (128, 128, 128) },
            { ColorCategory.Red,     (255, 60, 60) },
            { ColorCategory.Orange,  (210, 120, 40) },
            { ColorCategory.Yellow,  (245, 210, 60) },
            { ColorCategory.Green,   (40, 200, 120) },
            { ColorCategory.Cyan,    (0, 230, 200) },
            { ColorCategory.Blue,    (0, 220, 255) },
            { ColorCategory.Violet,  (130, 60, 200) },
            { ColorCategory.Magenta, (200, 80, 200) },
            { ColorCategory.Brown,   (150, 90, 45) }
        };

        private static ColorCategory CategorizeColor(byte r, byte g, byte b)
        {
            var candidate = Prototypes
                .Select(kvp => (Category: kvp.Key, Distance: ColorDistance(r, g, b, kvp.Value.R, kvp.Value.G, kvp.Value.B)))
                .OrderBy(x => x.Distance)
                .First();

            if (candidate.Distance <= PROTOTYPE_DISTANCE_THRESHOLD)
            {
                return candidate.Category;
            }

            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var range = max - min;

            if (max < 25) return ColorCategory.Black;
            if (max > 235 && min > 210) return ColorCategory.White;
            if (range < 18) return ColorCategory.Gray;

            if (max == r)
            {
                if (g > 160 && b < 100) return ColorCategory.Yellow;
                if (g > 120 && b < 80) return ColorCategory.Orange;
                if (b > 140 && g < 100) return ColorCategory.Magenta;
                if (g > 80 && b < 80) return ColorCategory.Brown;
                return ColorCategory.Red;
            }

            if (max == g)
            {
                if (b > 150) return ColorCategory.Cyan;
                if (r > 150) return ColorCategory.Yellow;
                return ColorCategory.Green;
            }

            if (r > 110 && g < 100) return ColorCategory.Violet;
            if (g > 150 && r < 80) return ColorCategory.Blue;
            if (g > 150 && r > 80) return ColorCategory.Cyan;
            return ColorCategory.Blue;
        }

        private static int ColorDistance(byte r1, byte g1, byte b1, int r2, int g2, int b2)
        {
            var dr = r1 - r2;
            var dg = g1 - g2;
            var db = b1 - b2;
            return dr * dr + dg * dg + db * db;
        }

        private static ColorCategory DetermineStableCategory()
        {
            if (_recentSamples.Count == 0) return ColorCategory.Unknown;

            var counts = new Dictionary<ColorCategory, int>();
            foreach (var sample in _recentSamples)
            {
                if (sample.Category == ColorCategory.Unknown) continue;
                counts.TryGetValue(sample.Category, out var current);
                counts[sample.Category] = current + 1;
            }

            if (counts.Count == 0) return ColorCategory.Unknown;

            var winner = counts.OrderByDescending(c => c.Value).First();
            return winner.Value >= STABLE_THRESHOLD ? winner.Key : ColorCategory.Unknown;
        }

        private static (byte r, byte g, byte b) AverageSamplesFor(ColorCategory category)
        {
            var filtered = _recentSamples.Where(s => s.Category == category).ToList();
            if (filtered.Count == 0) return (0, 0, 0);

            int sumR = 0, sumG = 0, sumB = 0;
            foreach (var sample in filtered)
            {
                sumR += sample.R;
                sumG += sample.G;
                sumB += sample.B;
            }

            return ((byte)(sumR / filtered.Count), (byte)(sumG / filtered.Count), (byte)(sumB / filtered.Count));
        }

        private static string Describe(ColorCategory category)
        {
            return category switch
            {
                ColorCategory.Black => "⚫️ NOIR",
                ColorCategory.White => "⚪️ BLANC",
                ColorCategory.Gray => "⚙️ GRIS",
                ColorCategory.Red => "🔴 ROUGE",
                ColorCategory.Orange => "🟠 ORANGE",
                ColorCategory.Yellow => "🟡 JAUNE",
                ColorCategory.Green => "🟢 VERT",
                ColorCategory.Cyan => "🟦 CYAN",
                ColorCategory.Blue => "🔵 BLEU",
                ColorCategory.Violet => "🟣 VIOLET",
                ColorCategory.Magenta => "💜 MAGENTA",
                ColorCategory.Brown => "🟤 BRUN",
                _ => "🌈 AUTRE"
            };
        }

        private static ConsoleColor GetConsoleColor(ColorCategory category)
        {
            return category switch
            {
                ColorCategory.Black => ConsoleColor.Black,
                ColorCategory.White => ConsoleColor.White,
                ColorCategory.Gray => ConsoleColor.Gray,
                ColorCategory.Red => ConsoleColor.Red,
                ColorCategory.Orange => ConsoleColor.DarkYellow,
                ColorCategory.Yellow => ConsoleColor.Yellow,
                ColorCategory.Green => ConsoleColor.Green,
                ColorCategory.Cyan => ConsoleColor.Cyan,
                ColorCategory.Blue => ConsoleColor.Blue,
                ColorCategory.Violet => ConsoleColor.Magenta,
                ColorCategory.Magenta => ConsoleColor.Magenta,
                ColorCategory.Brown => ConsoleColor.DarkYellow,
                _ => ConsoleColor.White
            };
        }
    }
}
