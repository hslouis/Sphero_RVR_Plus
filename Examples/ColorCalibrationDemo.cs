using System;
using System.Threading.Tasks;
using Sphero_RVR_Plus_CS.Core;

namespace Sphero_RVR_Plus_CS.Examples
{
    /// <summary>
    /// Démonstration du calibrage et test de précision du capteur de couleur
    /// </summary>
    public class ColorCalibrationDemo
    {
        /// <summary>
        /// Test spécifique pour le problème de détection de couleur verte
        /// </summary>
        public static async Task RunGreenCardTestAsync()
        {
            var controller = new RvrController("RV-829B");
            
            try
            {
                Console.WriteLine("🟢 Test spécifique pour carte verte");
                Console.WriteLine("==================================");
                
                // Connexion
                Console.WriteLine("Connexion au Sphero RVR+...");
                bool connected = await controller.ConnectAsync();
                
                if (!connected)
                {
                    Console.WriteLine("❌ Impossible de se connecter au robot");
                    return;
                }
                
                Console.WriteLine("✅ Connecté avec succès!");
                await Task.Delay(2000);
                
                // Test avec votre carte verte
                Console.WriteLine("\n🔍 Test de votre carte verte problématique:");
                Console.WriteLine("Placez la carte verte sous le capteur et appuyez sur Entrée...");
                Console.ReadLine();
                
                // Calibrage pour la carte verte
                var greenReading = await controller.CalibrateColorSensorAsync("Vert");
                
                // Analysis des résultats
                Console.WriteLine("\n📊 Analyse détaillée:");
                Console.WriteLine($"RGB détecté: R={greenReading.Red}, G={greenReading.Green}, B={greenReading.Blue}");
                Console.WriteLine($"Couleur identifiée: {greenReading.ColorName}");
                
                // Test avec d'autres cartes de couleur si disponibles
                Console.WriteLine("\n🎨 Test comparatif avec d'autres couleurs (optionnel):");
                string[] otherColors = { "Rouge", "Bleu", "Jaune", "Blanc" };
                
                foreach (string color in otherColors)
                {
                    Console.WriteLine($"\nVoulez-vous tester une carte {color}? (o/n)");
                    var input = Console.ReadLine()?.ToLower();
                    
                    if (input == "o" || input == "oui")
                    {
                        Console.WriteLine($"Placez la carte {color} sous le capteur et appuyez sur Entrée...");
                        Console.ReadLine();
                        await controller.CalibrateColorSensorAsync(color);
                    }
                }
                
                // Test de l'éclairage du capteur
                Console.WriteLine("\n💡 Test de l'éclairage du capteur:");
                Console.WriteLine("Activation de la LED du capteur...");
                await controller.SetColorSensorLedAsync(true);
                await Task.Delay(1000);
                
                Console.WriteLine("LED activée. Remettez la carte verte et appuyez sur Entrée...");
                Console.ReadLine();
                
                var greenWithLed = await controller.ReadColorAsync();
                Console.WriteLine($"Avec LED: R={greenWithLed.Red}, G={greenWithLed.Green}, B={greenWithLed.Blue} -> {greenWithLed.ColorName}");
                
                // Test sans LED
                await controller.SetColorSensorLedAsync(false);
                await Task.Delay(1000);
                
                Console.WriteLine("LED désactivée. Gardez la carte et appuyez sur Entrée...");
                Console.ReadLine();
                
                var greenWithoutLed = await controller.ReadColorAsync();
                Console.WriteLine($"Sans LED: R={greenWithoutLed.Red}, G={greenWithoutLed.Green}, B={greenWithoutLed.Blue} -> {greenWithoutLed.ColorName}");
                
                // Recommandations
                Console.WriteLine("\n💡 Recommandations pour améliorer la détection:");
                Console.WriteLine("1. Utilisez la LED du capteur pour un éclairage constant");
                Console.WriteLine("2. Évitez les surfaces brillantes ou réfléchissantes");
                Console.WriteLine("3. Maintenez une distance constante entre le capteur et l'objet");
                Console.WriteLine("4. Testez dans différentes conditions d'éclairage");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur: {ex.Message}");
            }
            finally
            {
                await controller.DisconnectAsync();
                Console.WriteLine("Déconnexion terminée.");
            }
        }
        
        /// <summary>
        /// Test de calibrage complet avec toutes les couleurs
        /// </summary>
        public static async Task RunFullCalibrationAsync()
        {
            var controller = new RvrController("RV-829B");
            
            try
            {
                Console.WriteLine("🎯 Calibrage complet du capteur de couleur");
                Console.WriteLine("==========================================");
                
                // Connexion
                bool connected = await controller.ConnectAsync();
                if (!connected)
                {
                    Console.WriteLine("❌ Impossible de se connecter");
                    return;
                }
                
                Console.WriteLine("✅ Connecté!");
                await Task.Delay(2000);
                
                // Lancer le test complet
                await controller.TestColorSensorAccuracyAsync();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur: {ex.Message}");
            }
            finally
            {
                await controller.DisconnectAsync();
            }
        }
        
        /// <summary>
        /// Menu principal pour les tests de calibrage
        /// </summary>
        public static async Task ShowCalibrationMenuAsync()
        {
            Console.WriteLine("🎨 Menu de Calibrage du Capteur de Couleur");
            Console.WriteLine("==========================================");
            Console.WriteLine("1. Test spécifique carte verte");
            Console.WriteLine("2. Calibrage complet");
            Console.WriteLine("0. Retour");
            Console.Write("Votre choix: ");
            
            var choice = Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    await RunGreenCardTestAsync();
                    break;
                case "2":
                    await RunFullCalibrationAsync();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Choix invalide");
                    break;
            }
        }
    }
}