using System;
using System.Threading.Tasks;
using Sphero_RVR_Plus_CS.Core;
using Sphero_RVR_Plus_CS.Sensors;

namespace Sphero_RVR_Plus_CS.Examples
{
    /// <summary>
    /// Démonstration du contrôle des LEDs principales du robot Sphero RVR+
    /// </summary>
    public static class MainLedControlDemo
    {
        //FONCTIONNE HUGO!!!
        public static async Task RunAsync()
        {
            Console.WriteLine("=== CONTRÔLE DES LEDs PRINCIPALES ===");
            Console.WriteLine("Démonstration des LEDs du robot (pas du capteur)");
            Console.WriteLine();

            var rvr = new RvrController("RV-829B");
            
            try
            {
                Console.WriteLine("🔗 Connexion au Sphero RVR+...");
                if (!await rvr.ConnectAsync())
                {
                    Console.WriteLine("❌ Impossible de se connecter au RVR+");
                    return;
                }

                Console.WriteLine("✅ Connecté au RVR+!");
                Console.WriteLine();
                
                // Test 1: Couleurs de base
                Console.WriteLine("🎨 Test 1: Couleurs de base");
                var basicColors = new[]
                {
                    (LedColor.Red, "Rouge"),
                    (LedColor.Green, "Vert"),
                    (LedColor.Blue, "Bleu"),
                    (LedColor.Yellow, "Jaune"),
                    (LedColor.BlueCyan, "Cyan"),
                    (LedColor.Magenta, "Magenta"),
                    (LedColor.White, "Blanc")
                };

                foreach (var (color, name) in basicColors)
                {
                    Console.WriteLine($"   {name}...");
                    await rvr.SetMainLedsAsync(color);
                    await Task.Delay(1500);
                }

                await Task.Delay(1000);

                // Test 2: RGB personnalisé
                Console.WriteLine("🌈 Test 2: Couleurs RGB personnalisées");
                var customColors = new[]
                {
                    (255, 100, 0, "Orange vif"),
                    (128, 0, 128, "Violet"),
                    (255, 20, 147, "Rose vif"),
                    (0, 255, 127, "Vert printemps"),
                    (255, 215, 0, "Or")
                };

                foreach (var (r, g, b, name) in customColors)
                {
                    Console.WriteLine($"   {name} (RGB: {r},{g},{b})...");
                    await rvr.SetMainLedsAsync((byte)r, (byte)g, (byte)b);
                    await Task.Delay(1500);
                }

                await Task.Delay(1000);

                // Test 3: Clignotement
                Console.WriteLine("✨ Test 3: Clignotement");
                Console.WriteLine("   Clignotement rouge (5 cycles)...");
                await rvr.BlinkMainLedsAsync(255, 0, 0, 5, 300, 300);
                
                await Task.Delay(1000);
                
                Console.WriteLine("   Clignotement bleu rapide (3 cycles)...");
                await rvr.BlinkMainLedsAsync(0, 0, 255, 3, 150, 150);

                await Task.Delay(2000);

                // Test 4: Animation arc-en-ciel
                Console.WriteLine("🌈 Test 4: Animation arc-en-ciel (7 secondes)");
                await rvr.RainbowMainLedsAsync(7000);

                await Task.Delay(1000);

                // Test 5: Transitions douces
                Console.WriteLine("🔄 Test 5: Transitions douces");
                Console.WriteLine("   Rouge → Bleu...");
                await rvr.FadeMainLedsAsync(LedColor.Red, LedColor.Blue, 3000, 30);
                
                await Task.Delay(500);
                
                Console.WriteLine("   Bleu → Vert...");
                await rvr.FadeMainLedsAsync(LedColor.Blue, LedColor.Green, 2500, 25);

                await Task.Delay(500);

                Console.WriteLine("   Vert → Éteint...");
                await rvr.FadeMainLedsAsync(LedColor.Green, LedColor.Off, 2000, 20);

                await Task.Delay(2000);

                // Test 6: Coordination mouvement + LED
                Console.WriteLine("🚗 Test 6: Coordination mouvement + LEDs");
                Console.WriteLine("   Avancer en vert...");
                await rvr.SetMainLedsAsync(LedColor.Green);
                await rvr.DriveForwardAsync(100, 2000);
                
                Console.WriteLine("   Tourner en jaune...");
                await rvr.SetMainLedsAsync(LedColor.Yellow);
                await rvr.TurnRightAsync(90, 100);
                
                Console.WriteLine("   Reculer en rouge...");
                await rvr.SetMainLedsAsync(LedColor.Red);
                await rvr.DriveBackwardAsync(80, 1500);

                await Task.Delay(1000);

                // Finir avec une séquence festive
                Console.WriteLine("🎉 Séquence finale festive!");
                for (int i = 0; i < 3; i++)
                {
                    await rvr.RainbowMainLedsAsync(2000);
                    await rvr.BlinkMainLedsAsync(255, 255, 255, 2, 200, 200);
                }

                // Éteindre les LEDs
                Console.WriteLine("🔌 Extinction des LEDs");
                await rvr.TurnOffMainLedsAsync();
                
                Console.WriteLine();
                Console.WriteLine("🎯 DÉMONSTRATION TERMINÉE!");
                Console.WriteLine("✨ Toutes les fonctions LED testées avec succès");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur: {ex.Message}");
            }
            finally
            {
                await rvr.TurnOffMainLedsAsync(); // S'assurer que les LEDs sont éteintes
                await rvr.DisconnectAsync();
                Console.WriteLine("🔌 Déconnecté du RVR+");
            }
        }
        
        /// <summary>
        /// Menu interactif pour tester les LEDs manuellement
        /// </summary>
        public static async Task ShowInteractiveMenu()
        {
            Console.WriteLine("=== MENU INTERACTIF LEDs PRINCIPALES ===");
            
            var rvr = new RvrController("RV-829B");
            
            try
            {
                if (!await rvr.ConnectAsync())
                {
                    Console.WriteLine("❌ Impossible de se connecter au RVR+");
                    return;
                }

                while (true)
                {
                    Console.WriteLine();
                    Console.WriteLine("Choisissez une option:");
                    Console.WriteLine("1. 🔴 Rouge");
                    Console.WriteLine("2. 🟢 Vert");  
                    Console.WriteLine("3. 🔵 Bleu");
                    Console.WriteLine("4. 🟡 Jaune");
                    Console.WriteLine("5. 🟣 Violet");
                    Console.WriteLine("6. ⚪ Blanc");
                    Console.WriteLine("7. 🌈 Arc-en-ciel");
                    Console.WriteLine("8. ✨ Clignotement");
                    Console.WriteLine("9. 🎨 RGB personnalisé");
                    Console.WriteLine("0. 🔌 Éteindre et quitter");
                    Console.Write("Votre choix: ");
                    
                    var choice = Console.ReadKey();
                    Console.WriteLine();
                    Console.WriteLine();
                    
                    switch (choice.KeyChar)
                    {
                        case '1':
                            await rvr.SetMainLedsAsync(LedColor.Red);
                            Console.WriteLine("✅ Rouge activé");
                            break;
                        case '2':
                            await rvr.SetMainLedsAsync(LedColor.Green);
                            Console.WriteLine("✅ Vert activé");
                            break;
                        case '3':
                            await rvr.SetMainLedsAsync(LedColor.Blue);
                            Console.WriteLine("✅ Bleu activé");
                            break;
                        case '4':
                            await rvr.SetMainLedsAsync(LedColor.Yellow);
                            Console.WriteLine("✅ Jaune activé");
                            break;
                        case '5':
                            await rvr.SetMainLedsAsync(LedColor.Purple);
                            Console.WriteLine("✅ Violet activé");
                            break;
                        case '6':
                            await rvr.SetMainLedsAsync(LedColor.White);
                            Console.WriteLine("✅ Blanc activé");
                            break;
                        case '7':
                            Console.WriteLine("🌈 Animation arc-en-ciel...");
                            await rvr.RainbowMainLedsAsync(5000);
                            Console.WriteLine("✅ Arc-en-ciel terminé");
                            break;
                        case '8':
                            Console.WriteLine("✨ Clignotement blanc...");
                            await rvr.BlinkMainLedsAsync(255, 255, 255, 5, 400, 400);
                            Console.WriteLine("✅ Clignotement terminé");
                            break;
                        case '9':
                            await TestCustomRgb(rvr);
                            break;
                        case '0':
                            await rvr.TurnOffMainLedsAsync();
                            Console.WriteLine("🔌 LEDs éteintes");
                            return;
                        default:
                            Console.WriteLine("❌ Choix invalide");
                            break;
                    }
                }
            }
            finally
            {
                await rvr.TurnOffMainLedsAsync();
                await rvr.DisconnectAsync();
            }
        }
        
        private static async Task TestCustomRgb(RvrController rvr)
        {
            Console.Write("Valeur Rouge (0-255): ");
            if (int.TryParse(Console.ReadLine(), out int red) && red >= 0 && red <= 255)
            {
                Console.Write("Valeur Verte (0-255): ");
                if (int.TryParse(Console.ReadLine(), out int green) && green >= 0 && green <= 255)
                {
                    Console.Write("Valeur Bleue (0-255): ");
                    if (int.TryParse(Console.ReadLine(), out int blue) && blue >= 0 && blue <= 255)
                    {
                        await rvr.SetMainLedsAsync((byte)red, (byte)green, (byte)blue);
                        Console.WriteLine($"✅ RGB({red},{green},{blue}) activé");
                        return;
                    }
                }
            }
            Console.WriteLine("❌ Valeurs invalides");
        }
    }
}