using System;
using System.Threading.Tasks;
using RVR_CS.Core;

namespace RVR_CS.Examples
{
    /// <summary>
    /// Outil de calibrage pour améliorer la précision des rotations
    /// Permet de tester et ajuster les paramètres de rotation
    /// </summary>
    public static class TurnCalibrationTool
    {
        /// <summary>
        /// Teste différentes durées de rotation pour 90° afin de trouver la valeur optimale
        /// </summary>
        public static async Task CalibrateRotationAsync(RvrController rvr)
        {
            Console.WriteLine("=== CALIBRAGE DES ROTATIONS ===");
            Console.WriteLine("Ce test va effectuer plusieurs rotations de 90° avec différents timings");
            Console.WriteLine("Observez le robot et notez quelle rotation semble la plus précise");
            Console.WriteLine();
            
            // Test avec différents facteurs de temps
            var timeFactors = new[] { 8.0, 9.0, 10.0, 11.0, 12.0, 13.0, 14.0 };
            
            for (int i = 0; i < timeFactors.Length; i++)
            {
                double timeFactor = timeFactors[i];
                Console.WriteLine($"Test {i + 1}/{timeFactors.Length}: Facteur de temps = {timeFactor}ms/degré");
                Console.WriteLine("Appuyez sur une touche pour démarrer ce test...");
                Console.ReadKey();
                
                await TestRotationWithTiming(rvr, 90, 100, timeFactor);
                
                Console.WriteLine($"Rotation terminée avec facteur {timeFactor}ms/degré");
                Console.WriteLine("Le robot a-t-il tourné exactement 90°? (Noter votre observation)");
                Console.WriteLine();
                
                // Pause entre les tests
                await Task.Delay(2000);
            }
            
            Console.WriteLine("=== FIN DU CALIBRAGE ===");
            Console.WriteLine("Utilisez le facteur qui donne la rotation la plus précise");
            Console.WriteLine("Modifiez la constante TIME_PER_DEGREE_MS dans RvrController.cs");
        }
        
        /// <summary>
        /// Teste une rotation avec un facteur de temps spécifique
        /// </summary>
        private static async Task TestRotationWithTiming(RvrController rvr, byte degrees, int speed, double timeFactorMs)
        {
            // Calcul du temps avec le facteur testé
            double timePerDegreeMs = (timeFactorMs * 100.0) / speed;
            int rotationTimeMs = (int)(degrees * timePerDegreeMs);
            
            Console.WriteLine($"   Rotation: {degrees}° en {rotationTimeMs}ms");
            
            // Rotation gauche
            await rvr.SetMotorsAsync(-speed, speed);
            await Task.Delay(rotationTimeMs);
            await rvr.SetMotorsAsync(0, 0);
        }
        
        /// <summary>
        /// Test de précision avec rotation complète (360°)
        /// </summary>
        public static async Task TestFullRotationAsync(RvrController rvr)
        {
            Console.WriteLine("=== TEST ROTATION COMPLÈTE 360° ===");
            Console.WriteLine("Le robot va effectuer une rotation complète");
            Console.WriteLine("Vérifiez s'il revient exactement à sa position initiale");
            Console.WriteLine("Appuyez sur une touche pour commencer...");
            Console.ReadKey();
            
            // Marquer la position de départ visuellement
            Console.WriteLine("🔴 Position de départ - mémorisez l'orientation");
            await Task.Delay(2000);
            
            // Effectuer la rotation complète
            await rvr.TurnLeftAsync(255); // 255 est proche de 360°
            
            Console.WriteLine("🔴 Position finale - comparez avec le départ");
            Console.WriteLine("Le robot est-il revenu à sa position exacte?");
        }
        
        /// <summary>
        /// Test de précision pour petites rotations
        /// </summary>
        public static async Task TestSmallRotationsAsync(RvrController rvr)
        {
            Console.WriteLine("=== TEST PETITES ROTATIONS ===");
            Console.WriteLine("Test de précision pour rotations de 30°, 45°, 90°");
            Console.WriteLine();
            
            var testAngles = new byte[] { 30, 45, 90 };
            
            foreach (var angle in testAngles)
            {
                Console.WriteLine($"Test rotation {angle}°");
                Console.WriteLine("Appuyez sur une touche pour démarrer...");
                Console.ReadKey();
                
                await rvr.TurnLeftAsync(angle, 100);
                
                Console.WriteLine($"Rotation {angle}° terminée - vérifiez la précision");
                Console.WriteLine();
                await Task.Delay(2000);
            }
        }
    }
}