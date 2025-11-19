using System;
using System.Threading.Tasks;
using Sphero_RVR_Plus_CS.Core;
using Sphero_RVR_Plus_CS.Sensors;

namespace Sphero_RVR_Plus_CS.Examples
{
    /// <summary>
    /// Démonstration de la détection automatique de couleur avec l'API améliorée
    /// </summary>
    public static class ColorDetectionDemo
    {
        /// <summary>
        /// Démonstration principale : lecture et détection automatique des couleurs
        /// </summary>
        public static async Task RunDemo()
        {
            Console.WriteLine("=== Démonstration de Détection Automatique de Couleur ===");
            Console.WriteLine("Cette démo utilise la structure ColorReading améliorée avec détection automatique");
            Console.WriteLine();

            var controller = new RvrController("RV-829B");
            if (await controller.ConnectAsync())
            {
                var colorSensor = new ColorSensorManager(controller);
                var ledController = new LedController(controller);

                try
                {
                    // Activer le capteur
                    if (!await colorSensor.ActivateAsync())
                    {
                        Console.WriteLine("❌ Impossible d'activer le capteur");
                        return;
                    }

                    Console.WriteLine("✅ Capteur de couleur activé!");
                    Console.WriteLine("📖 Instructions:");
                    Console.WriteLine("  - Placez des objets colorés sous le capteur");
                    Console.WriteLine("  - La couleur sera automatiquement détectée et affichée");
                    Console.WriteLine("  - Les LEDs changeront pour refléter la couleur détectée");
                    Console.WriteLine("  - Appuyez sur 'q' puis ENTRÉE pour arrêter");
                    Console.WriteLine();

                    // Boucle de lecture continue
                    bool running = true;
                    while (running)
                    {
						// Lecture de couleur
						ColorReading color = await colorSensor.ReadColorAsync();
                        
                        if (color != null)
                        {
                           
                            
                            // Affichage des informations détaillées
                            Console.WriteLine($"🎨 Valeurs RGB: R={color.R}, G={color.G}, B={color.B}");
                            Console.WriteLine($"   → Couleur détectée: {color.DetectedColor}");
                            Console.WriteLine($"   → Nom en français: {color.GetColorNameFrench()}");
                            Console.WriteLine($"   → Timestamp: {color.Timestamp:HH:mm:ss.fff}");
                            
                            // Synchroniser la LED avec la couleur détectée
                            await ledController.SetColorAsync(color.DetectedColor);
                            Console.WriteLine($"   → LED synchronisée sur {color.GetColorNameFrench()}");
                            Console.WriteLine();
                        }
                        else
                        {
                            Console.WriteLine("⚠️ Aucune donnée de couleur disponible");
                        }

                        // Vérifier si l'utilisateur veut arrêter
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                            {
                                running = false;
                            }
                        }

                        await Task.Delay(1000); // Lecture toutes les secondes
                    }

                    // Éteindre la LED avant de partir
                    await ledController.TurnOffAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erreur: {ex.Message}");
                }
                finally
                {
                    await controller.DisconnectAsync();
                    Console.WriteLine("✅ Déconnexion terminée");
                }
            }
            else
            {
                Console.WriteLine("❌ Impossible de se connecter au RVR+");
            }
        }

        /// <summary>
        /// Test des différentes couleurs avec valeurs RGB connues
        /// </summary>
        public static void TestColorDetection()
        {
            Console.WriteLine("=== Test de Détection de Couleur avec Valeurs Connues ===");
            Console.WriteLine();

            // Test avec des valeurs RGB connues
            var testColors = new[]
            {
                (255, 0, 0, "Rouge"),
                (0, 255, 0, "Vert"),
                (0, 0, 255, "Bleu"),
                (255, 255, 0, "Jaune"),
                (255, 165, 0, "Orange"),
                (128, 0, 128, "Violet"),
                (255, 192, 203, "Rose"),
                (0, 255, 255, "Cyan"),
                (255, 0, 255, "Magenta"),
                (255, 255, 255, "Blanc"),
                (50, 205, 50, "Vert Lime"),
                (0, 0, 0, "Noir/Éteint")
            };

            foreach (var (r, g, b, expected) in testColors)
            {
                var colorReading = new ColorReading
                {
                    R = (byte)r,
                    G = (byte)g,
                    B = (byte)b,
                    Timestamp = DateTime.Now
                };

                Console.WriteLine($"RGB({r}, {g}, {b}) -> Attendu: {expected}");
                Console.WriteLine($"   Détecté: {colorReading.GetColorNameFrench()} ({colorReading.DetectedColor})");
                
                // Vérification basique
                bool matches = expected.ToLower().Contains(colorReading.GetColorNameFrench().ToLower()) ||
                              colorReading.GetColorNameFrench().ToLower().Contains(expected.ToLower());
                
                Console.WriteLine($"   Résultat: {(matches ? "✅ Correct" : "⚠️ Différent")}");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Point d'entrée principal pour les tests
        /// </summary>
        public static async Task Main()
        {
            Console.WriteLine("Choisissez un mode de test:");
            Console.WriteLine("1. Démonstration en temps réel avec RVR+");
            Console.WriteLine("2. Test des algorithmes de détection");
            Console.Write("Votre choix (1 ou 2): ");
            
            var choice = Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    await RunDemo();
                    break;
                case "2":
                    TestColorDetection();
                    break;
                default:
                    Console.WriteLine("Choix invalide, lancement de la démo par défaut...");
                    await RunDemo();
                    break;
            }
        }
    }
}