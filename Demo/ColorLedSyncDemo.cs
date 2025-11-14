using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sphero_RVR_Plus_CS.Core;
using Sphero_RVR_Plus_CS.Sensors;

namespace Sphero_RVR_Plus_CS.Demo
{
    /// <summary>
    /// Démonstration synchronisée : LED principale change selon la couleur détectée
    /// Version stabilisée avec filtrage des variations rapides
    /// </summary>
    public static class ColorLedSyncDemo
    {
        // Paramètres de stabilisation
        private static readonly int BUFFER_SIZE = 8; // 8 lectures pour analyser
        private static readonly int UPDATE_THRESHOLD = 40; // Seuil pour changement significatif
        private static readonly int UPDATE_INTERVAL_MS = 800; // 800ms entre mises à jour
        private static readonly int CONSISTENCY_THRESHOLD = 4; // Au moins 4/8 lectures doivent être similaires (50%)
        
        // Variables de filtrage
        private static List<(byte R, byte G, byte B)> colorBuffer = new List<(byte, byte, byte)>();
        private static (byte R, byte G, byte B) lastLedColor = (0, 0, 0);
        private static DateTime lastUpdate = DateTime.MinValue;
        
        public static async Task RunAsync(RvrController rvr)
        {
            Console.WriteLine("🌈 === SYNCHRONISATION LED-COULEUR ÉQUILIBRÉE ===");
            Console.WriteLine("La LED principale du RVR va changer de couleur selon ce que détecte le capteur!");
            Console.WriteLine("📍 Placez différents objets colorés devant le capteur blanc");
            Console.WriteLine("⏱️  Système équilibré - 4/8 lectures de même famille de couleur nécessaires");
            Console.WriteLine("🎯 Groupement par famille (Rouge, Vert, Bleu, etc.) pour une meilleure stabilité");
            Console.WriteLine("Pressez 'q' pour quitter...");
            Console.WriteLine();

            var colorSensor = new ColorSensorManager(rvr);
            
            // Réinitialiser les variables de filtrage
            colorBuffer.Clear();
            lastLedColor = (0, 0, 0);
            lastUpdate = DateTime.MinValue;
            
            try
            {
                // Activer le capteur de couleur
                Console.WriteLine("🔌 Activation du capteur de couleur...");
                await colorSensor.ActivateAsync();
                
                // Démarrer le streaming avec événements
                Console.WriteLine("🔄 Démarrage du streaming avec événements...");
                await colorSensor.StartStreamingWithEventsAsync();
                
                // Abonnement aux données couleur avec filtrage
                colorSensor.ColorDetected += async (colorData) =>
                {
                    // Récupérer les valeurs RGB du capteur
                    var color = colorData.Color;
                    var rawR = (byte)Math.Min(255, Math.Max(0, (int)color.Red));
                    var rawG = (byte)Math.Min(255, Math.Max(0, (int)color.Green));
                    var rawB = (byte)Math.Min(255, Math.Max(0, (int)color.Blue));
                    
                    // Ajouter au buffer de moyennage
                    colorBuffer.Add((rawR, rawG, rawB));
                    
                    // Garder seulement les N dernières lectures
                    if (colorBuffer.Count > BUFFER_SIZE)
                    {
                        colorBuffer.RemoveAt(0);
                    }
                    
                    // Attendre d'avoir assez de données et respecter l'intervalle de mise à jour
                    if (colorBuffer.Count >= BUFFER_SIZE && 
                        (DateTime.Now - lastUpdate).TotalMilliseconds >= UPDATE_INTERVAL_MS)
                    {
                        // Vérifier la consistance des couleurs dans le buffer
                        var consistentColor = FindConsistentColor(colorBuffer);
                        
                        if (consistentColor.HasValue)
                        {
                            var avgR = consistentColor.Value.R;
                            var avgG = consistentColor.Value.G;
                            var avgB = consistentColor.Value.B;
                            
                            // Vérifier si la couleur a suffisamment changé
                            var colorDifference = Math.Abs(avgR - lastLedColor.R) + 
                                                Math.Abs(avgG - lastLedColor.G) + 
                                                Math.Abs(avgB - lastLedColor.B);
                            
                            if (colorDifference >= UPDATE_THRESHOLD)
                            {
                                // Ajuster la luminosité pour un meilleur rendu LED
                                var brightness = 0.8f; // 80% de luminosité
                                var ledR = (byte)(avgR * brightness);
                                var ledG = (byte)(avgG * brightness);
                                var ledB = (byte)(avgB * brightness);
                                
                                // Changer la couleur de la LED principale
                                await rvr.SetMainLedsAsync(ledR, ledG, ledB);
                                
                                // Mémoriser la dernière couleur et le temps de mise à jour
                                lastLedColor = (ledR, ledG, ledB);
                                lastUpdate = DateTime.Now;
                                
                                // Afficher les informations
                                var colorName = GetColorName(ledR, ledG, ledB);
                                Console.WriteLine($"� Couleur CONFIRMÉE: R={avgR:D3} G={avgG:D3} B={avgB:D3} → LED: R={ledR:D3} G={ledG:D3} B={ledB:D3} ({colorName}) ✅");
                                
                                // Vider le buffer pour forcer une nouvelle validation
                                colorBuffer.Clear();
                            }
                        }
                        else
                        {
                            // Pas assez de consistance, on continue à collecter
                            Console.WriteLine($"⚠️ Couleurs incohérentes détectées - continue à analyser...");
                        }
                    }
                };

                Console.WriteLine("✅ Capteur activé! Synchronisation LED-couleur ÉQUILIBRÉE en cours...");
                Console.WriteLine("🔍 Essayez avec des objets rouge, vert, bleu, jaune, etc.");
                Console.WriteLine("📊 Le système groupe par famille de couleur (Rouge, Vert, Bleu...)");
                Console.WriteLine("⏳ Attente : 800ms et 4/8 lectures de même famille pour confirmer une couleur");

                // Attendre l'entrée utilisateur pour quitter
                ConsoleKeyInfo keyInfo;
                do
                {
                    keyInfo = Console.ReadKey(true);
                } while (keyInfo.KeyChar != 'q' && keyInfo.KeyChar != 'Q');

                Console.WriteLine("\n🛑 Arrêt de la synchronisation...");
                
                // Éteindre la LED principale
                await rvr.SetMainLedsAsync(0, 0, 0);
                Console.WriteLine("💡 LED principale éteinte");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur lors de la synchronisation: {ex.Message}");
            }
            finally
            {
                // Arrêter le streaming
                await colorSensor.StopStreamingWithEventsAsync();
                Console.WriteLine("🔄 Streaming arrêté");
                
                // Désactiver le capteur
                await colorSensor.DeactivateAsync();
                Console.WriteLine("🔌 Capteur de couleur désactivé");
            }
        }

        /// <summary>
        /// Trouve une couleur consistante dans le buffer si au moins 50% des lectures sont de la même famille de couleur
        /// </summary>
        private static (byte R, byte G, byte B)? FindConsistentColor(List<(byte R, byte G, byte B)> buffer)
        {
            if (buffer.Count < BUFFER_SIZE) return null;
            
            // Grouper par famille de couleur (Rouge, Vert, Bleu, etc.) au lieu de tolérance stricte
            var colorFamilies = new Dictionary<string, List<(byte R, byte G, byte B)>>();
            
            foreach (var color in buffer)
            {
                string family = GetColorFamily(color.R, color.G, color.B);
                
                if (!colorFamilies.ContainsKey(family))
                {
                    colorFamilies[family] = new List<(byte R, byte G, byte B)>();
                }
                colorFamilies[family].Add(color);
            }
            
            // Trouver la famille la plus importante
            var largestFamily = colorFamilies.Values.OrderByDescending(f => f.Count).First();
            
            // Vérifier si cette famille représente au moins 50% des lectures (4/8)
            if (largestFamily.Count >= CONSISTENCY_THRESHOLD)
            {
                // Retourner la moyenne de la famille la plus consistante
                var avgR = (byte)largestFamily.Average(c => c.R);
                var avgG = (byte)largestFamily.Average(c => c.G);
                var avgB = (byte)largestFamily.Average(c => c.B);
                
                return (avgR, avgG, avgB);
            }
            
            return null; // Pas assez de consistance
        }
        
        /// <summary>
        /// Détermine la famille de couleur pour regrouper les nuances similaires
        /// </summary>
        private static string GetColorFamily(byte r, byte g, byte b)
        {
            // Seuils pour déterminer la couleur dominante
            const int threshold = 50;
            
            // Déterminer quelle composante est dominante
            bool redDominant = r > g + threshold && r > b + threshold;
            bool greenDominant = g > r + threshold && g > b + threshold;
            bool blueDominant = b > r + threshold && b > g + threshold;
            
            if (redDominant) return "ROUGE";
            if (greenDominant) return "VERT";
            if (blueDominant) return "BLEU";
            
            // Couleurs secondaires
            if (r > threshold && g > threshold && b < threshold) return "JAUNE";
            if (r > threshold && b > threshold && g < threshold) return "MAGENTA";
            if (g > threshold && b > threshold && r < threshold) return "CYAN";
            
            // Couleurs neutres
            if (r < 30 && g < 30 && b < 30) return "NOIR";
            if (r > 200 && g > 200 && b > 200) return "BLANC";
            
            return "MÉLANGE";
        }

        /// <summary>
        /// Détermine le nom de la couleur en fonction des valeurs RGB
        /// </summary>
        private static string GetColorName(byte r, byte g, byte b)
        {
            // Seuils pour déterminer les couleurs principales
            const int threshold = 80;
            const int lowThreshold = 30;

            // Noir/sombre
            if (r < lowThreshold && g < lowThreshold && b < lowThreshold)
                return "NOIR";

            // Blanc/clair
            if (r > 200 && g > 200 && b > 200)
                return "BLANC";

            // Couleurs primaires et secondaires
            bool redHigh = r > threshold;
            bool greenHigh = g > threshold;
            bool blueHigh = b > threshold;

            if (redHigh && !greenHigh && !blueHigh)
                return "ROUGE";
            else if (!redHigh && greenHigh && !blueHigh)
                return "VERT";
            else if (!redHigh && !greenHigh && blueHigh)
                return "BLEU";
            else if (redHigh && greenHigh && !blueHigh)
                return "JAUNE";
            else if (redHigh && !greenHigh && blueHigh)
                return "MAGENTA";
            else if (!redHigh && greenHigh && blueHigh)
                return "CYAN";
            else if (redHigh && Math.Abs(g - r) < 50 && Math.Abs(b - r) < 50)
                return "ORANGE/ROSE";
            else
                return "MÉLANGE";
        }
    }
}