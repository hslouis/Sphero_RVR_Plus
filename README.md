# Sphero RVR+ Control Center (.NET 8)

Suite .NET 8 (C#) permettant de piloter et diagnostiquer un Sphero RVR+ via Bluetooth LE sous Windows. Le projet expose une API de haut niveau (`Core/`), un gestionnaire de capteurs couleurs (`Sensors/`), ainsi qu'un ensemble de demos et d'exemples interactifs (menu console, scripts de synchronisation LED/couleur, controle moteur, etc.).

## Fonctionnalites principales
- Connexion BLE fiabilisee grace a `RvrBleConnection` (scan par nom, configuration indications/notifs, reconnexion et inspection GATT).
- Controle complet du robot via `RvrController` : moteurs, stabilisation, leds principales, capteurs internes, sequences temporisees.
- Gestion du capteur couleur externe (`ColorSensorManager`) avec activation active, lecture RGB multiniveau, mapping vers noms de couleurs francises et synchronisation directe avec les LEDs.
- Demos pretes a l'emploi (`Demo/`, `Examples/`) couvrant les scenarios cles : synchronisation LED-couleur, tests moteur, calibration, mouvement chronometre, sequences lumineuses avancees.
- Utilitaires NodeJS (`Utils/`) pour analyser les journaux BLE ou valider les trames officielles Sphero lorsqu'on doit diagnostiquer une communication.

## Architecture rapide
- `Program.cs` : point d'entree console avec menu principal, boucle de connexions et orchestration des demos.
- `Core/` : abstractions coeur (`RvrController`, `RvrBleConnection`, `SensorData`) gerant la pile protocolaire, la file de commandes et la traduction haut niveau.
- `Sensors/` : interfaces et implementations concretes pour le capteur couleur, le controleur de LEDs secondaires et la logique d'activation.
- `Demo/` et `Examples/` : modules independants exposes sous forme de methodes `RunAsync`/`Main` pour etre reutilises en labo ou en classe.
- `Utils/` : scripts d'analyse (Node 18+) et dossiers de captures (`Utils/LOG`) utiles pour debugger les echanges BLE.
- `Sphero_RVR+_11Novembre_Final/` : export Visual Studio historique conserve a titre d'archive. Il est exclu automatiquement de la compilation par `Sphero_RVR_Plus_CS.csproj`.

## Prerequis materiel et logiciel
- Windows 10 2004 (build 19041) ou plus recent (x64) avec la pile Bluetooth LE disponible. La cible .NET est `net8.0` (Windows obligatoire).
- .NET SDK 8.0 (ou plus recent) pour compiler/exec. Verifiez avec `dotnet --info`.
- Sphero RVR+ a jour cote firmware et deja appaire dans Parametres Windows > Appareils Bluetooth.
- (Optionnel) Node.js 18+ si vous souhaitez lancer les scripts d'analyse `Utils/*.js`.

## Installation et build
```bash
git clone <repo>
cd Sphero
dotnet restore
dotnet build
```
Le build produit `bin/Debug/net8.0-windows10.0.19041.0/` pret a etre lance. Utilisez `dotnet publish -c Release -r win-x64` pour distribuer un binaire optimisee.

## Utilisation
1. Appairez le RVR+ sous Windows et notez son nom BLE (ex. `RV-A380`).  
2. Ajustez `new RvrController("RV-A380")` dans `Program.cs` si votre robot diffuse un autre identifiant.  
3. Branchez/alimentez le capteur couleur sur le port UART requis (selon votre montage).  
4. Lancez le menu interactif :  
   ```bash
   dotnet run
   ```
5. Suivez les instructions a l'ecran pour executer une demo ou un test.

### Menu console (par defaut)
| Touche | Module | Description |
| ------ | ------ | ----------- |
| `6` | Diagnostic capteur | Active le capteur couleur, allume les LEDs, puis lit 10 echantillons en affichant le nom de couleur et en synchronisant la LED interne. |
| `9` | Demo complete | Enchaine tests LED (`TestLedFunctions`), mouvements (`TestMovementFunctions`), capteurs (`TestSensorFunctions`) puis une sequence integree. |
| `F` | QuickColorTest | Lance `Examples/QuickColorTest` pour verifier rapidement les valeurs RGB et les noms detectes. |
| `R` | Lecture ponctuelle | Effectue une lecture unique via `ColorSensorManager.ReadColorAsync` et journalise le resultat. |
| `Y` | Color/LED Sync | Execute `Demo/ColorLedSyncDemo` pour mapper dynamiquement la couleur detectee aux LEDs principales. |
| `L` | Controle LEDs | Mode 1 = demo automatique complete, Mode 2 = menu interactif (`MainLedControlDemo`) pour saisir vos propres couleurs/sequences. |
| `X` | Manette moteurs | Active `RunInteractiveControl` (WASD + Space/Q) pour piloter vitesses, marches avant/arriere et rotations. |

Les autres exemples dans `Examples/` peuvent etre invoques directement via `dotnet run --project Sphero_RVR_Plus_CS.csproj -- <args>` si vous souhaitez automatiser un scenario specifique.

## Structure du depot
```
.
|-- Core/                 # Abstraction BLE + commandes robot
|-- Sensors/              # Capteur couleur, LED controller, interfaces
|-- Demo/                 # Demos haute valeur pedagogique
|-- Examples/             # Cas d'usage ponctuels (calibration, mouvement, etc.)
|-- Utils/                # Scripts d'analyse BLE + captures
|-- Program.cs            # Menu console principal
|-- Sphero_RVR_Plus_CS.csproj
`--  Sphero_RVR_Plus_CS.sln

## Depannage rapide
- **Impossible de se connecter** : assurez-vous que le nom BLE fourni au `RvrController` correspond bien au nom public du robot. Utilisez l'appli mobile Sphero ou `BluetoothLEDevice.GetDeviceSelectorFromFriendlyName` pour le confirmer.
- **Erreurs de build Windows SDK** : installez le workload `dotnet workload install windows` ou ouvrez une invite "Developer PowerShell" Visual Studio qui embarque les WinRT contracts requis (`Windows.Devices.*`). 
- **Capteur couleur silencieux** : verifiez l'alimentation, puis relancez l'option `6` pour rebooter le capteur (la LED integree doit clignoter en blanc). Ajustez les delais dans `ColorSensorManager.ActivateAsync` si votre montage a besoin de plus de temps.
- **Logs BLE** : les scripts `Utils/analyze_*.js` consomment les captures `Utils/LOG/*.json`. Lancez-les avec `node analyze_led_illumination.js <fichier>` pour debugger une trame.

## Licence
Projet distribue sous licence [MIT](LICENSE). Vous pouvez reutiliser/modifier le code pour vos besoins pedagogiques ou projets internes tant que la notice de licence est conservee.
