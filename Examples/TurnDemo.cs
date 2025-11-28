using System;
using System.Threading.Tasks;
using Sphero_RVR_Plus_CS.Core;

namespace Sphero_RVR_Plus_CS.Examples
{
    /// <summary>
    /// Démonstration des fonctions de rotation du Sphero RVR+
    /// </summary>
    public class TurnDemo
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("=== Démonstration des rotations Sphero RVR+ ===");

            var rvr = new RvrController("RV-829B");

            try
            {
                // Test des rotations vers la droite
                Console.WriteLine("🧪 Test des rotations vers la DROITE");
                await TestRightTurns(rvr);

                await Task.Delay(2000); // Pause entre les tests

                // Test des rotations vers la gauche
                Console.WriteLine("\n🧪 Test des rotations vers la GAUCHE");
                await TestLeftTurns(rvr);

                await Task.Delay(2000); // Pause entre les tests

                // Test combiné
                Console.WriteLine("\n🧪 Test combiné - séquence de rotation");
                await TestCombinedTurns(rvr);

                Console.WriteLine("\n✅ Tous les tests de rotation terminés!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur: {ex.Message}");
            }
            finally
            {
                await rvr.DisconnectAsync();
                Console.WriteLine("Déconnecté du RVR+");
            }
        }

        private static async Task TestRightTurns(RvrController rvr)
        {
            byte[] testDegrees = { 45, 90, 180, 30 };
            
            foreach (byte degrees in testDegrees)
            {
                Console.WriteLine($"   Rotation à droite de {degrees}°...");
                await rvr.TurnRightAsync(degrees);
                await Task.Delay(1000); // Pause entre les rotations
            }
        }

        private static async Task TestLeftTurns(RvrController rvr)
        {
            byte[] testDegrees = { 45, 90, 180, 60 };
            
            foreach (byte degrees in testDegrees)
            {
                Console.WriteLine($"   Rotation à gauche de {degrees}°...");
                await rvr.TurnLeftAsync(degrees);
                await Task.Delay(1000); // Pause entre les rotations
            }
        }

        private static async Task TestCombinedTurns(RvrController rvr)
        {
            Console.WriteLine("   Séquence: Droite 90° -> Gauche 180° -> Droite 90°");
            
            await rvr.TurnRightAsync(90, 80);  // Rotation droite à vitesse 80
            await Task.Delay(500);
            
            await rvr.TurnLeftAsync(180, 120); // Rotation gauche à vitesse 120
            await Task.Delay(500);
            
            await rvr.TurnRightAsync(90, 60);  // Rotation droite à vitesse 60
            
            Console.WriteLine("   Séquence terminée - le robot devrait être orienté vers la gauche");
        }

        /// <summary>
        /// Test avec des paramètres personnalisés
        /// </summary>
        /// <param name="rvr">Instance du contrôleur RVR</param>
        /// <param name="degrees">Degrés de rotation</param>
        /// <param name="speed">Vitesse de rotation</param>
        public static async Task CustomTurnTest(RvrController rvr, byte degrees, int speed = 100)
        {
            Console.WriteLine($"🎯 Test personnalisé: Rotation droite de {degrees}° à vitesse {speed}");
            await rvr.TurnRightAsync(degrees, speed);
            Console.WriteLine("✅ Test personnalisé terminé");
        }
    }
}