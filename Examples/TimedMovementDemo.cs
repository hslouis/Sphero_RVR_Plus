using System;
using System.Threading.Tasks;
using Sphero_RVR_Plus_CS.Core;

namespace Sphero_RVR_Plus_CS.Examples
{
    /// <summary>
    /// Démonstration des nouvelles fonctions de mouvement avec durée en millisecondes
    /// </summary>
    public static class TimedMovementDemo
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("=== DÉMONSTRATION MOUVEMENTS AVEC DURÉE ===");
            Console.WriteLine("Test des nouvelles surcharges avec timing précis");
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
                
                // Test 1: Avancement avec durée
                Console.WriteLine("📏 Test 1: Avancement 2 secondes à vitesse 100");
                Console.WriteLine("Appuyez sur une touche pour démarrer...");
                Console.ReadKey();
                
                await rvr.DriveForwardAsync(100, 2000); // 2 secondes
                await Task.Delay(1000);
                
                // Test 2: Recul avec durée
                Console.WriteLine("📏 Test 2: Recul 1.5 secondes à vitesse 80");
                Console.WriteLine("Appuyez sur une touche pour démarrer...");
                Console.ReadKey();
                
                await rvr.DriveBackwardAsync(80, 1500); // 1.5 secondes
                await Task.Delay(1000);
                
                // Test 3: Mouvement avec différentiel (virage léger)
                Console.WriteLine("📏 Test 3: Virage léger gauche 3 secondes");
                Console.WriteLine("Moteur gauche=70, Moteur droit=100");
                Console.WriteLine("Appuyez sur une touche pour démarrer...");
                Console.ReadKey();
                
                await rvr.DriveAsync(70, 100, 3000); // 3 secondes
                await Task.Delay(1000);
                
                // Test 4: Virage progressif avec nouvelle fonction
                Console.WriteLine("📏 Test 4: Virage progressif droite 2.5 secondes");
                Console.WriteLine("Vitesse=120, Ratio de virage=0.5 (vers la droite)");
                Console.WriteLine("Appuyez sur une touche pour démarrer...");
                Console.ReadKey();
                
                await rvr.DriveWithTurnAsync(120, 0.5, 2500); // 2.5 secondes
                await Task.Delay(1000);
                
                // Test 5: Séquence de mouvements automatique
                Console.WriteLine("📏 Test 5: Séquence automatique");
                Console.WriteLine("Carré avec virages temporisés...");
                Console.WriteLine("Appuyez sur une touche pour démarrer...");
                Console.ReadKey();
                
                await ExecuteSquareSequence(rvr);
                
                Console.WriteLine();
                Console.WriteLine("🎯 NOUVELLES FONCTIONS DISPONIBLES:");
                Console.WriteLine("• DriveForwardAsync(speed, durationMs)  - Avancer avec durée");
                Console.WriteLine("• DriveBackwardAsync(speed, durationMs) - Reculer avec durée");
                Console.WriteLine("• DriveAsync(left, right, durationMs)   - Différentiel avec durée");
                Console.WriteLine("• DriveWithTurnAsync(speed, ratio, ms)  - Virage progressif");
                Console.WriteLine();
                Console.WriteLine("Toutes les fonctions s'arrêtent automatiquement après la durée spécifiée!");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur: {ex.Message}");
            }
            finally
            {
                await rvr.DisconnectAsync();
                Console.WriteLine("🔌 Déconnecté du RVR+");
            }
        }
        
        /// <summary>
        /// Exécute une séquence en forme de carré avec les nouvelles fonctions temporisées
        /// </summary>
        private static async Task ExecuteSquareSequence(RvrController rvr)
        {
            Console.WriteLine("🔲 Démarrage séquence carré...");
            
            for (int side = 1; side <= 4; side++)
            {
                Console.WriteLine($"  Côté {side}/4: Avancer 1.5s");
                await rvr.DriveForwardAsync(100, 1500);
                
                await Task.Delay(500); // Pause entre mouvement et rotation
                
                Console.WriteLine($"  Virage {side}/4: Tourner à droite");
                await rvr.TurnRightAsync(90, 100);
                
                await Task.Delay(500); // Pause avant le côté suivant
            }
            
            Console.WriteLine("✅ Séquence carré terminée!");
        }
        
        /// <summary>
        /// Menu interactif pour tester les fonctions temporisées
        /// </summary>
        public static async Task ShowInteractiveMenu()
        {
            Console.WriteLine("=== MENU MOUVEMENTS TEMPORISÉS ===");
            
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
                    Console.WriteLine("Choisissez un test:");
                    Console.WriteLine("1. ⬆️ Avancer (vitesse + durée personnalisées)");
                    Console.WriteLine("2. ⬇️ Reculer (vitesse + durée personnalisées)");
                    Console.WriteLine("3. 🔄 Différentiel (gauche/droite + durée)");
                    Console.WriteLine("4. 🌙 Virage progressif (arc de cercle)");
                    Console.WriteLine("5. 🔲 Séquence carré automatique");
                    Console.WriteLine("0. Retour");
                    Console.Write("Votre choix: ");
                    
                    var choice = Console.ReadKey();
                    Console.WriteLine();
                    Console.WriteLine();
                    
                    switch (choice.KeyChar)
                    {
                        case '1':
                            await TestCustomForward(rvr);
                            break;
                        case '2':
                            await TestCustomBackward(rvr);
                            break;
                        case '3':
                            await TestCustomDifferential(rvr);
                            break;
                        case '4':
                            await TestProgressiveTurn(rvr);
                            break;
                        case '5':
                            await ExecuteSquareSequence(rvr);
                            break;
                        case '0':
                            return;
                        default:
                            Console.WriteLine("❌ Choix invalide");
                            break;
                    }
                }
            }
            finally
            {
                await rvr.DisconnectAsync();
            }
        }
        
        private static async Task TestCustomForward(RvrController rvr)
        {
            Console.Write("Vitesse (0-255): ");
            if (int.TryParse(Console.ReadLine(), out int speed))
            {
                Console.Write("Durée en millisecondes: ");
                if (int.TryParse(Console.ReadLine(), out int duration))
                {
                    await rvr.DriveForwardAsync(speed, duration);
                }
            }
        }
        
        private static async Task TestCustomBackward(RvrController rvr)
        {
            Console.Write("Vitesse (0-255): ");
            if (int.TryParse(Console.ReadLine(), out int speed))
            {
                Console.Write("Durée en millisecondes: ");
                if (int.TryParse(Console.ReadLine(), out int duration))
                {
                    await rvr.DriveBackwardAsync(speed, duration);
                }
            }
        }
        
        private static async Task TestCustomDifferential(RvrController rvr)
        {
            Console.Write("Vitesse gauche (-255 à +255): ");
            if (int.TryParse(Console.ReadLine(), out int leftSpeed))
            {
                Console.Write("Vitesse droite (-255 à +255): ");
                if (int.TryParse(Console.ReadLine(), out int rightSpeed))
                {
                    Console.Write("Durée en millisecondes: ");
                    if (int.TryParse(Console.ReadLine(), out int duration))
                    {
                        await rvr.DriveAsync(leftSpeed, rightSpeed, duration);
                    }
                }
            }
        }
        
        private static async Task TestProgressiveTurn(RvrController rvr)
        {
            Console.Write("Vitesse de base (0-255): ");
            if (int.TryParse(Console.ReadLine(), out int speed))
            {
                Console.Write("Ratio de virage (-1.0=gauche max, 0=droit, +1.0=droite max): ");
                if (double.TryParse(Console.ReadLine(), out double turnRatio))
                {
                    Console.Write("Durée en millisecondes: ");
                    if (int.TryParse(Console.ReadLine(), out int duration))
                    {
                        await rvr.DriveWithTurnAsync(speed, turnRatio, duration);
                    }
                }
            }
        }
    }
}