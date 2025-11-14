// Script d'analyse des logs officiels pour trouver la commande LED capteur

// Recherche dans les logs officiels :
// 1. Séquence exacte APRÈS frame 43 (LED matrix)
// 2. Commandes avant première frame couleur 0x38 18 3D
// 3. Différences vs notre séquence

function analyzeOfficialLogs() {
    console.log("=== ANALYSE LOGS OFFICIELS SPHERO ===");
    
    // Patterns recherchés :
    const LED_MATRIX_FRAME = "8D3A11011A457400FFFFFFE3D8"; // Frame 43 connue
    const COLOR_DATA_FRAME = "8D381101183D"; // Début frame couleur officielle
    
    // Chercher :
    // 1. Toutes frames ENTRE LED_matrix et COLOR_data
    // 2. Nouvelles commandes DID=0x18 ou DID=0x1A
    // 3. Payloads différents de notre séquence
    
    console.log("Patterns à chercher :");
    console.log("- Frame LED Matrix (notre frame 43):", LED_MATRIX_FRAME);
    console.log("- Frame couleur officielle:", COLOR_DATA_FRAME);
    console.log("- Nouvelles commandes entre ces deux");
}

// Fonction pour extraire séquence post-activation
function extractPostActivationSequence(log) {
    const frames = log.bleFrames || [];
    let foundLedMatrix = false;
    let postActivationFrames = [];
    
    for (let frame of frames) {
        if (frame.data && frame.data.includes("8D3A11011A457400FFFFFF")) {
            foundLedMatrix = true;
            console.log("✅ Trouvé frame LED Matrix à:", frame.timestamp);
            continue;
        }
        
        if (foundLedMatrix) {
            if (frame.data && frame.data.startsWith("8D38") && frame.data.includes("183D")) {
                console.log("🎯 Première frame couleur trouvée à:", frame.timestamp);
                break;
            }
            postActivationFrames.push(frame);
        }
    }
    
    return postActivationFrames;
}

module.exports = { analyzeOfficialLogs, extractPostActivationSequence };