using System;
using System.Threading.Tasks;
using Sphero_RVR_Plus_CS.Core;

namespace Sphero_RVR_Plus_CS.Examples
{
    /// <summary>
    /// Test de connexion simple pour diagnostiquer les problèmes
    /// </summary>
    public class ConnectionTest
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🔍 === TEST DE CONNEXION RVR+ ===");
            Console.WriteLine();
            
            // Instructions pré-connexion
            Console.WriteLine("📋 Vérifications avant connexion :");
            Console.WriteLine("   ✅ RVR+ est allumé (bouton power enfoncé)");
            Console.WriteLine("   ✅ LED clignote (mode découvrable)");
            Console.WriteLine("   ✅ Application Sphero officielle fermée");
            Console.WriteLine("   ✅ Bluetooth activé sur l'ordinateur");
            Console.WriteLine();
            
            Console.WriteLine("Appuyez sur une touche pour commencer le test...");
            Console.ReadKey();
            Console.WriteLine();
            
            try
            {
                Console.WriteLine("🔗 Tentative de connexion à 'RV-829B'...");
                
                var controller = new RvrController("RV-829B");
                
                // Tentative de connexion avec timeout
                var connectionTask = controller.ConnectAsync();
                var timeoutTask = Task.Delay(15000); // 15 secondes timeout
                
                var completedTask = await Task.WhenAny(connectionTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("❌ TIMEOUT: Impossible de se connecter en 15 secondes");
                    Console.WriteLine();
                    Console.WriteLine("🔧 Suggestions de dépannage :");
                    Console.WriteLine("   1. Vérifiez que le RVR+ est allumé et visible");
                    Console.WriteLine("   2. Rapprochez-vous du robot");
                    Console.WriteLine("   3. Redémarrez le RVR+ (bouton power)");
                    Console.WriteLine("   4. Vérifiez que le nom est bien 'RV-829B'");
                    return;
                }
                
                bool connected = await connectionTask;
                
                if (connected)
                {
                    Console.WriteLine("✅ CONNEXION RÉUSSIE !");
                    Console.WriteLine("🎯 Le RVR+ est maintenant connecté");
                    
                    // Test basique
                    Console.WriteLine();
                    Console.WriteLine("🔧 Test basique - Allumer LED rouge...");
                    await controller.SetLedColorAsync(255, 0, 0); // Rouge
                    await Task.Delay(2000);
                    
                    Console.WriteLine("💡 LED verte...");
                    await controller.SetLedColorAsync(0, 255, 0); // Vert
                    await Task.Delay(2000);
                    
                    Console.WriteLine("🔵 LED bleue...");
                    await controller.SetLedColorAsync(0, 0, 255); // Bleu
                    await Task.Delay(2000);
                    
                    Console.WriteLine("🔌 Extinction LED...");
                    await controller.SetLedColorAsync(0, 0, 0); // Éteint
                    
                    Console.WriteLine();
                    Console.WriteLine("✅ Test basique terminé avec succès !");
                    
                    await controller.DisconnectAsync();
                    Console.WriteLine("👋 Déconnecté");
                }
                else
                {
                    Console.WriteLine("❌ ÉCHEC DE CONNEXION");
                    Console.WriteLine();
                    Console.WriteLine("🔧 Diagnostics possibles :");
                    Console.WriteLine("   1. Nom de dispositif incorrect");
                    Console.WriteLine("   2. RVR+ non découvrable");
                    Console.WriteLine("   3. Problème Bluetooth");
                    Console.WriteLine("   4. Dispositif déjà connecté ailleurs");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERREUR LORS DE LA CONNEXION: {ex.Message}");
                Console.WriteLine($"💡 Détails: {ex.StackTrace}");
            }
            
            Console.WriteLine();
            Console.WriteLine("Appuyez sur une touche pour quitter...");
            Console.ReadKey();
        }
    }
}