using System;
using System.Threading;
using System.Threading.Tasks;
using Sphero_RVR_Plus_CS.Core;
using Sphero_RVR_Plus_CS.Demo;
using Sphero_RVR_Plus_CS.Examples;
using Sphero_RVR_Plus_CS.Sensors;
using System.Collections.Generic;
using Windows.ApplicationModel.Background;

namespace Sphero_RVR_Plus_CS
{

	/// <summary>
	/// Auteur :      Hugo St-Louis
	/// Description : Expérimentation avec le robot Sphero RVR+.
	/// Date :        2025-11-19
	/// </summary>
	class Program
	{
		private static RvrController? _quickColorController;
		private static ColorSensorManager? _quickColorSensor;
		static async Task Main(string[] args)
		{
			char cLettre = ' ';
			byte R = 0;
			byte G = 0;
			byte B = 0;

			Console.WriteLine("=== Sphero RVR+ Control Center ===");
			//
			RvrController rvr = new RvrController("RV-829B");
			await rvr.ConnectAsync();
			Console.WriteLine("RVR - Connectée");
			// Création du capteur de couleur
			ColorSensorManager _colorSensor = new ColorSensorManager(rvr);
			await _colorSensor.ActivateAsync();
			do
			{
				Console.WriteLine(@" ");
				Console.WriteLine(@"         __ ");
				Console.WriteLine(@" _(\    |@@| ");
				Console.WriteLine(@"(__/\__ \--/ __  ");
				Console.WriteLine(@"   \___|----|  |   __ ");
				Console.WriteLine(@"       \ }{ /\ )_ / _\ ");
				Console.WriteLine(@"       /\__/\ \__O (__ ");
				Console.WriteLine(@"      (--/\--)    \__/ ");
				Console.WriteLine(@"      _)(  )(_   ");
				Console.WriteLine(@"     `---''---` ");
				Console.WriteLine("1-Appuyer sur W pour Avancer. ");
				Console.WriteLine("2-Appuyer sur S pour Reculer.");
				Console.WriteLine("3-Appuyer sur A pour Tourner à GAUCHE.");
				Console.WriteLine("4-Appuyer sur D pour Tourner à DROITE.");
				Console.WriteLine("5-Appuyer sur E pour Alterner la couleur des LED Rouge - Vert - Bleu..");
				Console.WriteLine("6-Appuyer sur ESPACE Lire la couleur sous le robot.");
				Console.WriteLine("7-CAppuyer sur M pour changer la couleur des leds du robot pour la lu par le senseur.");
				Console.WriteLine("7-CAppuyer sur Z pour faire avancer le robot jusqu'à ce qu'il y ait une couleur rouge vert ou bleu sous le robot .");
				Console.WriteLine("8-Appuyer sur Q pour quitter.");
				Console.Write(":");
				cLettre = Console.ReadKey().KeyChar;

				cLettre = char.ToUpper(cLettre);
				switch (cLettre)
				{
					case 'W':
						{
							// Test 1: Avancement avec durée
							Console.WriteLine("📏 Test 1: Avancement 2 secondes à vitesse 100");
							await rvr.DriveForwardAsync(100, 2000); // 2 secondes
							await Task.Delay(1000);


							break;
						}
					case 'S':
						{
							// Test 2: Recul avec durée
							Console.WriteLine("📏 Test 2: Recul 1.5 secondes à vitesse 80");
							await rvr.DriveBackwardAsync(80, 1500); // 1.5 secondes
							await Task.Delay(1000);
							break;
						}
					case 'A':
						{
							// Test 3: Mouvement avec différentiel (virage léger gauche)
							Console.WriteLine("📏 Gauche");
							await rvr.DriveAsync(0, 100, 1000); // 1 seconde
							await Task.Delay(1000);
							break;
						}
					case 'D':
						{
							// Test 3: Mouvement avec différentiel (virage léger droite)
							Console.WriteLine("📏 droit");
							await rvr.DriveAsync(100, 0, 1000); // 1 seconde
							await Task.Delay(1000);
							break;
						}

					case 'E':
						{
							if (R == 255)
							{
								R = 0;
								G = 255;
								B = 0;

								for (int i = 0; i < 256; i += 5)
								{
									G = (byte)i;
									await rvr.SetMainLedsAsync(R, G, B);
									
									await Task.Delay(1);
								}


							}

							else if (G == 255)
							{
								R = 0;
								G = 0;
								B = 255;

								for (int i = 0; i < 256; i += 5)
								{
									B = (byte)i;
									await rvr.SetMainLedsAsync(R, G, B);
									await Task.Delay(1);
								}


							}
							else
							{
								R = 255;
								G = 0;
								B = 0;

								for (int i = 0; i < 256; i+=5)
								{
									R = (byte)i;
									await rvr.SetMainLedsAsync(R, G, B);
									await Task.Delay(1);
								}

							}
							await rvr.SetMainLedsAsync(R, G, B);
							break;
						}
					case ' ':
						{
							// Test de lecture simple
							Console.WriteLine("📊 Test de lecture des couleurs...");
							ColorReading color = null;
							color = await _colorSensor.ReadColorAsync();
							if (color != null)
							{
								LedColor Ledcolor = color.GetDetectedColor();

								Console.WriteLine(Ledcolor.ToString());
								Console.WriteLine(color.GetColorNameFrench());
								Console.WriteLine($"   Lecture initiale: RGB({ color.Red}, { color.Green}, { color.Blue})");
							}
							Console.ReadKey();
							break;
						}
					case 'M':
						{
							break;
						}
					case 'Z':
						{

							break;
						}



				}
				Console.Clear();
			}
			while (cLettre != 'Q');

		}
	}

}
