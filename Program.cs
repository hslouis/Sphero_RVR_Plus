using System;
using System.Threading;
using System.Threading.Tasks;
using Sphero_RVR_Plus_CS.Core;
using Sphero_RVR_Plus_CS.Demo;
using Sphero_RVR_Plus_CS.Examples;
using Sphero_RVR_Plus_CS.Sensors;
using System.Collections.Generic;

namespace Sphero_RVR_Plus_CS
{
    class Program
    {
        private static RvrController? _quickColorController;
        private static ColorSensorManager? _quickColorSensor;
        static async Task Main(string[] args)
        {

			ConsoleKeyInfo choice = new ConsoleKeyInfo();
			Console.WriteLine("=== Sphero RVR+ Control Center ===");
            Console.WriteLine("Version professionnelle avec capteurs et LEDs");
            Console.WriteLine();
			RvrController rvr = new RvrController("RV-A380");
			await rvr.ConnectAsync();
			Console.WriteLine("RVR - Connectée");
			// Création du capteur
			ColorSensorManager _colorSensor = new ColorSensorManager(rvr);
            await _colorSensor.ActivateAsync();
			// Test d'activation
			Console.WriteLine();
			Console.WriteLine("🔥 ACTIVATION DU CAPTEUR - Regardez si la LED s'allume sous le robot!");
			Console.WriteLine("La LED devrait devenir BLANCHE/BRILLANTE pendant quelques secondes...");
			Console.WriteLine();

			// Menu de s�lection
			while (choice.KeyChar != '0')
            {
                Console.WriteLine("Choisissez une d�monstration:");
                Console.WriteLine("6. ?? Test activation capteur simple (DIAGNOSTIC)");
				Console.WriteLine("X. ?? Manette");
				Console.WriteLine("L. ?? CONTR�LE LEDs PRINCIPALES Fonctionnnel Hugo novembre");
				Console.WriteLine("9. ?? DEMO LIBRAIRIE COMPLETE (NOUVEAU)");
                Console.WriteLine("F. ?? CAPTEUR COULEUR CORRIGE (NOUVEAU)");
                Console.WriteLine("S. ?? LECTURE COULEUR PONCTUELLE (NOUVEAU)");
                Console.WriteLine("Y. ?? SYNCHRONISATION LED-COULEUR (NOUVEAU)");
                Console.WriteLine("0. Quitter");
                Console.Write("Votre choix (0-10): ");

                choice = Console.ReadKey();
                Console.WriteLine();
                Console.WriteLine();

                if (choice.KeyChar == '0')
                {
                    Console.WriteLine("?? Au revoir!");
                    break;
                }
                

                try
                {
   

					
		
                    switch (choice.KeyChar)
                    {

                        case '6':


							try
							{
								// Test 4: Animation arc-en-ciel
								Console.WriteLine("🌈 Test 4: Animation arc-en-ciel (7 secondes)");
								await rvr.RainbowMainLedsAsync(7000);
								// Test d'activation
								Console.WriteLine();
								Console.WriteLine("🔥 ACTIVATION DU CAPTEUR - Regardez si la LED s'allume sous le robot!");
								Console.WriteLine("La LED devrait devenir BLANCHE/BRILLANTE pendant quelques secondes...");
								Console.WriteLine();

								bool activated = await _colorSensor.ActivateAsync();

								if (activated)
								{
									Console.WriteLine("✅ Activation réussie selon le code!");
									Console.WriteLine("🔍 La LED du capteur devrait maintenant être allumée.");
									Console.WriteLine();
									Console.WriteLine("Appuyez sur une touche pour continuer...");
									Console.ReadKey();

									// Test de lecture simple
									Console.WriteLine();
									Console.WriteLine("📊 Test de lecture des couleurs...");

									for (int i = 0; i < 10; i++)
									{
										var colorReading = await _colorSensor.ReadColorAsync();
										if (colorReading.HasValue)
										{
											var color = colorReading.Value;
											Console.WriteLine($"   Lecture {i + 1}: RGB({color.Red}, {color.Green}, {color.Blue})");
											Console.WriteLine($"      → Couleur détectée: {color.GetColorNameFrench()}");
                                            await rvr.SetMainLedsAsync(color.Red, color.Green, color.Blue);
                                            

										}
										else
										{
											Console.WriteLine($"   Lecture {i + 1}: Aucune donnée disponible");
										}
										await Task.Delay(1000);
									}
								}
								else
								{
									Console.WriteLine("❌ Échec de l'activation");
								}
							}
							catch (Exception ex)
							{
								Console.WriteLine($"❌ Erreur: {ex.Message}");
							}
							
							break;

					
                        case '9':
							// D�monstration compl�te de la librairie
							try
							{
								Console.WriteLine($"✅ Connecté! Status: {(rvr.IsConnected ? "Connecté" : "Déconnecté")}");
								await Task.Delay(1000);

								// === 2. LEDS ===
								Console.WriteLine("\n💡 2. Test des LEDs");
								await TestLedFunctions(rvr);
								await Task.Delay(2000);

								// === 3. MOUVEMENTS ===
								Console.WriteLine("\n🚗 3. Test des Mouvements");
								await TestMovementFunctions(rvr);
								await Task.Delay(2000);

								// === 4. CAPTEURS ===
								Console.WriteLine("\n📊 4. Test des Capteurs");
								await TestSensorFunctions(rvr);
								await Task.Delay(2000);

								// === 5. DÉMONSTRATION INTÉGRÉE ===
								Console.WriteLine("\n🎯 5. Démonstration Intégrée");
								await IntegratedDemo(rvr);

								Console.WriteLine("\n✅ Toutes les fonctions ont été testées avec succès!");
							}
							catch (Exception ex)
							{
								Console.WriteLine($"❌ Erreur: {ex.Message}");
							}
						
							break;
                            
                        case 'f':
                        case 'F':
                            // Option F - Test capteur couleur corrig�(Hugo: fonctionne de temps en temps)
                            Console.WriteLine("?? === CAPTEUR COULEUR CORRIG� ===");
                            await Sphero_RVR_Plus_CS.Examples.QuickColorTest.Main(new string[0]);
                            break;
                 
                        case 'r':
                        case 'R':
                            // Option R - Lecture synchrone via ReadColorAsync
                            Console.WriteLine("?? === TEST LECTURE COULEUR SYNCHRONE ===");
                            try
                            {
                                //if (!await EnsureQuickColorSensorReadyAsync())
                                //{
                                //    break;
                                //}

                                var colorReading = await _colorSensor.ReadColorAsync();
                                if (colorReading.HasValue)
                                {
                                    var reading = colorReading.Value;
                                    Console.WriteLine($"?? Lecture reussRie: R={reading.R:D3} G={reading.G:D3} B={reading.B:D3}");
                                    Console.WriteLine($"?? Couleur detectee: {reading.GetColorNameFrench()}");
                                }
                                else
                                {
                                    Console.WriteLine("? Aucune donnee recue dans le delai imparti");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"? Erreur pendant le test synchrone: {ex.Message}");
                            }
                            break;
                        case 'y':
                        case 'Y':
                            // Option Y - Synchronisation LED-couleur
                            Console.WriteLine("?? === SYNCHRONISATION LED-COULEUR ===");
                            await ColorLedSyncDemo.RunAsync(rvr);
                          
                            break;

                        case 'l':
                        case 'L':
                            // Option L - Contr�le des LEDs principales
                            Console.WriteLine("Choisissez le mode:");
                            Console.WriteLine("1. D�monstration automatique");
                            Console.WriteLine("2. Contr�le interactif");
                            Console.Write("Votre choix (1-2): ");
                            var ledChoice = Console.ReadKey();
                            Console.WriteLine();
                            Console.WriteLine();
                            
                            if (ledChoice.KeyChar == '1')
                            {
								try
								{
									

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
                            else if (ledChoice.KeyChar == '2')
                            {
                                await MainLedControlDemo.ShowInteractiveMenu();
                            }
                            else
                            {
                                Console.WriteLine("? Choix invalide");
                            }
                            break;
                        case 'x':
                        case 'X':
                            
                            RunInteractiveControl(rvr);
                            
                            break;

                        default:
                         
                            Console.WriteLine("? Choix invalide");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Erreur: {ex.Message}");
                }

                Console.WriteLine();
                Console.WriteLine("Appuyez sur une touche pour revenir au menu...");
                Console.ReadKey();
                Console.Clear();
            }

			await rvr.DisconnectAsync();
        }



        static async Task RunInteractiveControl(RvrController rvr)
        {
            Console.WriteLine("\n=== Interactive Motor Control ===");
            

            try
            {



                string key = "";
                while (key != "q")
                {
					Console.Clear();
					Console.WriteLine("Controls:");
					Console.WriteLine("  W - Forward");
					Console.WriteLine("  S - Backward");
					Console.WriteLine("  A - Turn Left");
					Console.WriteLine("  D - Turn Right");
					Console.WriteLine("  Space - Stop");
					Console.WriteLine("  Q - Quit");
					Console.WriteLine();
					Console.Write("Command: ");
                    key = Console.ReadLine();

                    
                    switch (key)
                    {
                        case "w":
						case "W":
							// Test 1: Avancement avec durée
							Console.WriteLine("📏 Test 1: Avancement 2 secondes à vitesse 100");
							await rvr.DriveForwardAsync(100, 2000); // 2 secondes
							await Task.Delay(1000);

							break;
                        case "s":
						case "S":
							// Test 2: Recul avec durée
							Console.WriteLine("📏 Test 2: Recul 1.5 secondes à vitesse 80");				

							await rvr.DriveBackwardAsync(80, 1500); // 1.5 secondes
							await Task.Delay(1000);
					
                            break;
                        case "a":
						case "A":
							// Test 3: Mouvement avec différentiel (virage léger)
							Console.WriteLine("📏 Left");
							await rvr.DriveAsync(0, 100, 1000); // 3 secondes
							await Task.Delay(1000);
							break;
                        case "d":
						case "D":
							Console.WriteLine("?? Right");
							await rvr.DriveAsync(100, 0, 1000); // 3 secondes
							break;
                        case "t":
						case "T":
							// Test 4: Virage progressif avec nouvelle fonction
							await rvr.DriveWithTurnAsync(120, 0.5, 2500); // 2.5 secondes
							await Task.Delay(1000);
                            break;
						case " ":

							Console.WriteLine("?? Brake...");
							await rvr.SetMotorsAsync(0, 0);


							break;
                        case "q":
						case "Q":
							Console.WriteLine("?? Quitting...");
                            await rvr.SetMotorsAsync(0, 0);
                            break;
                    }

                    await Task.Delay(10); // Small delay for smooth control
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
         
        }

		private static async Task TestLedFunctions(RvrController rvr)
		{
			Console.WriteLine("   Test des couleurs prédéfinies:");

			var colors = new[] { RvrLedColor.Red, RvrLedColor.Green, RvrLedColor.Blue, RvrLedColor.Yellow, RvrLedColor.White };
			foreach (var color in colors)
			{
				Console.WriteLine($"   • {color}");
				await rvr.SetLedColorAsync(color);
				await Task.Delay(800);
			}

			Console.WriteLine("   Test couleur personnalisée (Orange):");
			await rvr.SetLedColorAsync(255, 165, 0);
			await Task.Delay(1000);

			Console.WriteLine("   Extinction de la LED:");
			await rvr.TurnOffLedAsync();
			await Task.Delay(500);
		}

		private static async Task TestMovementFunctions(RvrController rvr)
		{
			Console.WriteLine("   Avancer en ligne droite:");
			await rvr.DriveForwardAsync(80, 1000);
			await Task.Delay(1500);
			await rvr.StopAsync();
			await Task.Delay(500);

			Console.WriteLine("   Reculer:");
			await rvr.DriveBackwardAsync(60,1000);
			await Task.Delay(1500);
			await rvr.StopAsync();
			await Task.Delay(500);

			Console.WriteLine("   Mouvements indépendants des moteurs:");
			await rvr.DriveAsync(100, 50); // Tourner légèrement à droite
			await Task.Delay(1500);
			await rvr.DriveAsync(50, 100); // Tourner légèrement à gauche
			await Task.Delay(1500);
			await rvr.StopAsync();
			await Task.Delay(500);

			Console.WriteLine("   Rotations précises:");
			await rvr.TurnRightAsync(90);
			await Task.Delay(1000);
			await rvr.TurnLeftAsync(180);
			await Task.Delay(1000);
			await rvr.TurnRightAsync(90);
			await Task.Delay(500);
		}

		private static async Task TestSensorFunctions(RvrController rvr)
		{
			Console.WriteLine("   Lecture de la couleur:");
			var color = await rvr.ReadColorAsync();
			Console.WriteLine($"   Couleur détectée: {color}");

			Console.WriteLine("   Réinitialisation de la distance:");
			await rvr.ResetDistanceAsync();

			Console.WriteLine("   Lecture de la distance (après déplacement):");
			await rvr.DriveForwardAsync(100);
			await Task.Delay(1000);
			await rvr.StopAsync();

			var distance = await rvr.ReadDistanceAsync();
			Console.WriteLine($"   Distance parcourue: {distance:F2} unités");

			Console.WriteLine("   Lecture de tous les capteurs:");
			var allSensors = await rvr.ReadAllSensorsAsync();
			Console.WriteLine($"   Données complètes: {allSensors}");

			Console.WriteLine("   Accès aux propriétés:");
			Console.WriteLine($"   • Dernière couleur: {rvr.LastDetectedColor}");
			Console.WriteLine($"   • Distance totale: {rvr.TotalDistance:F2}");
			Console.WriteLine($"   • Données capteurs: {rvr.CurrentSensorData}");
		}

		private static async Task IntegratedDemo(RvrController rvr)
		{
			Console.WriteLine("   Démonstration: Robot suit un carré avec changement de couleur LED");

			var ledColors = new[] { RvrLedColor.Red, RvrLedColor.Green, RvrLedColor.Blue, RvrLedColor.Yellow };

			for (int side = 0; side < 4; side++)
			{
				// Changer la couleur de la LED pour chaque côté
				await rvr.SetLedColorAsync(ledColors[side]);
				Console.WriteLine($"   Côté {side + 1}/4 - LED: {ledColors[side]}");

				// Avancer
				await rvr.DriveForwardAsync(100);
				await Task.Delay(2000);
				await rvr.StopAsync();

				// Lire les capteurs
				var color = await rvr.ReadColorAsync();
				var distance = await rvr.ReadDistanceAsync();
				Console.WriteLine($"     Couleur détectée: {color.ColorName}, Distance: {distance:F2}");

				// Tourner de 90° pour le prochain côté
				if (side < 3)
				{
					await rvr.TurnRightAsync(90);
					await Task.Delay(500);
				}
			}

			// Retourner à la position d'origine
			await rvr.SetLedColorAsync(RvrLedColor.White);
			Console.WriteLine("   Retour à la position d'origine");
			await rvr.DriveForwardAsync(100);
			await Task.Delay(1000);
			await rvr.StopAsync();

			// Éteindre la LED
			await rvr.TurnOffLedAsync();
			Console.WriteLine("   Démonstration terminée");
		}
	}
}
