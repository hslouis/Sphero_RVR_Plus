// ========== Enhanced BLE Sniffer pour Sphero RVR+ ==========
// Version améliorée pour capturer TOUTE la séquence d'activation
// Coller ce code dans la console Chrome sur edu.sphero.com

(() => {
  console.log('🔍 Enhanced BLE Sniffer - Capturing FULL activation sequence...');
  
  // ================= New configuration & state (added) =================
  const CONFIG = {
    logNonFrames: false,      // Set true to also log non Sphero frames in detail
    logServiceDiscovery: true,
    verifyChecksum: true,
    autoNumberNonFrames: false,
    keepBinary: true,
    maxStored: 5000
  };

  let g_connectionId = 0;
  let lastColorCandidate = null;
  let colorSession = { active:false, labels:[], samples:[] }; // For manual tagging of surfaces/colors

  // ================= Utility: checksum (Sphero 1's complement) =================
  const computeChecksum = (bytes) => {
    // For frames: 8D <FT> <FLAGS> <LEN or DID?> ... CHK D8
    // We apply generic Sphero rule: sum bytes index 1 .. n-2 -> checksum = (~sum) & 0xFF
    let sum = 0;
    for (let i = 1; i < bytes.length - 2; i++) sum = (sum + bytes[i]) & 0xFF;
    return (~sum) & 0xFF;
  };
  
  // Stockage des logs avec plus de détails
  window.BLE_LOGS = [];
  let frameCounter = 0;
  
  // Helper pour convertir en hex avec espaces
  const toHex = (data) => {
    let bytes;
    if (data instanceof ArrayBuffer) {
      bytes = new Uint8Array(data);
    } else if (data instanceof DataView) {
      bytes = new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
    } else if (data && data.buffer) {
      bytes = new Uint8Array(data.buffer, data.byteOffset || 0, data.byteLength || data.length);
    } else {
      bytes = new Uint8Array(data || []);
    }
    return Array.from(bytes, b => b.toString(16).padStart(2, '0')).join(' ').toUpperCase();
  };
  
  // Analyse plus détaillée des trames
  const analyzeSpheroFrame = (bytes) => {
    if (bytes.length < 8) return null;
    if (bytes[0] !== 0x8D || bytes[bytes.length - 1] !== 0xD8) return null;
    
    const frameType = bytes[1]; // 0x3A = official, 0x18 = raw, 0x0A = special
    const flags = bytes[2];
    const len = bytes[3];
    
    let analysis = '';
    if (frameType === 0x3A) {
      // Official frame: 8D 3A FLAGS LEN DID CID SEQ [payload] CHECKSUM D8
      if (bytes.length >= 7) {
        const did = bytes[4];
        const cid = bytes[5];
        const seq = bytes[6];
        analysis = `OFFICIAL DID:0x${did.toString(16).padStart(2,'0')} CID:0x${cid.toString(16).padStart(2,'0')} SEQ:${seq}`;
        
        // Detect key commands
        if (did === 0x1A && cid === 0x45) analysis += ' 🔥LED_RGB🔥';
        if (did === 0x1A && cid === 0x2F) analysis += ' 🔧CONFIG🔧';
        if (did === 0x18 && cid === 0x3B) analysis += ' 🔄STREAM🔄';
        if (did === 0x13 && cid === 0x10) analysis += ' 🌅WAKE🌅';
      }
    } else if (frameType === 0x18) {
      // Raw frame: 8D 18 FLAGS DID CID SEQ [payload] CHECKSUM D8
      if (bytes.length >= 7) {
        const did = bytes[3];
        const cid = bytes[4];
        const seq = bytes[5];
        analysis = `RAW DID:0x${did.toString(16).padStart(2,'0')} CID:0x${cid.toString(16).padStart(2,'0')} SEQ:${seq}`;
        
        // Detect sensor commands
        if (did === 0x18 && cid === 0x27) analysis += ' 🎨COLOR_CFG🎨';
        if (did === 0x18 && cid === 0x2B) analysis += ' 💡LED_CTRL💡';
        if (did === 0x18 && cid === 0x26) analysis += ' 🔗COLOR_NODE🔗';
        if (did === 0x18 && cid === 0x2C) analysis += ' 📡COLOR_NOTIFY📡';
      }
    }
    
    if (CONFIG.verifyChecksum) {
      const expected = computeChecksum(bytes);
      const actual = bytes[bytes.length - 2];
      if (expected !== actual) {
        analysis += ` ❌CHK exp:${expected.toString(16).padStart(2,'0')} got:${actual.toString(16).padStart(2,'0')}`;
      } else {
        analysis += ' ✅CHK';
      }
    }
    return analysis.trim();
  };
  
  // UUID court avec plus de détails
  const shortUuid = (uuid) => {
    if (!uuid) return 'null';
    const str = uuid.toString().toLowerCase();
    if (str.includes('00010002')) return 'CMD';
    if (str.includes('00010003')) return 'NOTIFY';
    if (str.includes('00010001')) return 'SERVICE';
    return str.substring(0, 8);
  };
  
  // Log enrichi avec analyse
  const logBLE = (direction, type, service, char, data, extra = '') => {
    const timestamp = new Date().toISOString().substring(11, 23);
    const hex = toHex(data);
    const bytes = new Uint8Array(data.buffer || data);
    const isFrame = bytes.length >= 2 && bytes[0] === 0x8D && bytes[bytes.length - 1] === 0xD8;
    
    let prefix = '📤';
    let analysis = '';
    
    if (isFrame) {
      frameCounter++;
      prefix = `🔴 FRAME #${frameCounter}`;
      analysis = analyzeSpheroFrame(bytes) || '';
    }
    
    if (isFrame || CONFIG.logNonFrames) {
      const msg = `${prefix} ${direction} [${timestamp}] ${type} ${shortUuid(service)}/${shortUuid(char)} : ${hex} ${analysis} ${extra}`;
      console.log(msg);
    }
    
    window.BLE_LOGS.push({
      timestamp,
      frameNumber: isFrame ? frameCounter : null,
      direction,
      type,
      service: shortUuid(service),
      characteristic: shortUuid(char),
      hex,
      analysis,
      isFrame,
      raw: Array.from(bytes),
      connectionId: g_connectionId
    });

    if (window.BLE_LOGS.length > CONFIG.maxStored) {
      window.BLE_LOGS.shift();
    }
  };
  
  // ================= Early lifecycle hooks (added) =================
  const originalNavigatorRequest = navigator.bluetooth.requestDevice.bind(navigator.bluetooth);
  navigator.bluetooth.requestDevice = async function(options) {
    console.log('🔎 requestDevice options:', JSON.stringify(options));
    const dev = await originalNavigatorRequest(options);
    console.log('✅ Device selected:', dev.name || '(no name)', dev.id);
    return dev;
  };

  const originalServerConnect = BluetoothRemoteGATTServer.prototype.connect;
  BluetoothRemoteGATTServer.prototype.connect = async function() {
    console.log('🔌 Connecting GATT server...');
    const res = await originalServerConnect.call(this);
    g_connectionId++;
    console.log(`✅ GATT Connected (session #${g_connectionId})`);
    return res;
  };

  const wrapService = (protoName, fnName) => {
    const originalFn = protoName.prototype[fnName];
    if (!originalFn) return;
    protoName.prototype[fnName] = async function(...args) {
      const started = performance.now();
      const result = await originalFn.apply(this, args);
      if (CONFIG.logServiceDiscovery) {
        console.log(`📦 ${fnName} -> ${Array.isArray(result)?result.length+' items':(result?.uuid||'')} (${(performance.now()-started).toFixed(1)}ms)`);
      }
      return result;
    };
  };
  wrapService(BluetoothRemoteGATTServer, 'getPrimaryService');
  wrapService(BluetoothRemoteGATTServer, 'getPrimaryServices');
  wrapService(BluetoothRemoteGATTService, 'getCharacteristic');
  wrapService(BluetoothRemoteGATTService, 'getCharacteristics');

  // Sauvegarder les fonctions originales (existing section)
  const original = {
    writeValue: BluetoothRemoteGATTCharacteristic.prototype.writeValue,
    writeValueWithResponse: BluetoothRemoteGATTCharacteristic.prototype.writeValueWithResponse,
    writeValueWithoutResponse: BluetoothRemoteGATTCharacteristic.prototype.writeValueWithoutResponse,
    readValue: BluetoothRemoteGATTCharacteristic.prototype.readValue,
    startNotifications: BluetoothRemoteGATTCharacteristic.prototype.startNotifications
  };
  
  // Hook writeValue
  BluetoothRemoteGATTCharacteristic.prototype.writeValue = async function(value) {
    logBLE('OUT', 'WRITE', this.service?.uuid, this.uuid, value);
    return await original.writeValue.call(this, value);
  };
  
  // Hook writeValueWithResponse
  if (original.writeValueWithResponse) {
    BluetoothRemoteGATTCharacteristic.prototype.writeValueWithResponse = async function(value) {
      logBLE('OUT', 'WRITE+RESP', this.service?.uuid, this.uuid, value);
      return await original.writeValueWithResponse.call(this, value);
    };
  }
  
  // Hook writeValueWithoutResponse
  if (original.writeValueWithoutResponse) {
    BluetoothRemoteGATTCharacteristic.prototype.writeValueWithoutResponse = async function(value) {
      logBLE('OUT', 'WRITE-RESP', this.service?.uuid, this.uuid, value);
      return await original.writeValueWithoutResponse.call(this, value);
    };
  }
  
  // Hook readValue
  BluetoothRemoteGATTCharacteristic.prototype.readValue = async function() {
    const result = await original.readValue.call(this);
    logBLE('IN', 'READ', this.service?.uuid, this.uuid, result);
    return result;
  };
  
  // Hook startNotifications avec détail
  BluetoothRemoteGATTCharacteristic.prototype.startNotifications = async function() {
    const result = await original.startNotifications.call(this);
    console.log(`🔔 Notifications ON → ${shortUuid(this.service?.uuid)}/${shortUuid(this.uuid)}`);
    
    const onNotification = (event) => {
      const value = event.target.value;
      logBLE('IN', 'NOTIFY', this.service?.uuid, this.uuid, value);
    };
    
    this.addEventListener('characteristicvaluechanged', onNotification);
    return result;
  };
  
  // Fonctions utilitaires améliorées
  window.BLE_SAVE = () => {
    const json = JSON.stringify(window.BLE_LOGS, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `ble-sphero-enhanced-${Date.now()}.json`;
    a.click();
    URL.revokeObjectURL(url);
    console.log(`💾 Sauvegardé ${window.BLE_LOGS.length} entrées`);
  };
  
  window.BLE_SEQUENCE = () => {
    const frames = window.BLE_LOGS.filter(log => log.isFrame);
    console.log(`📋 Séquence complète: ${frames.length} trames`);
    frames.forEach((frame, i) => {
      const timeRelative = i > 0 ? `(+${(new Date(`1970-01-01T${frame.timestamp}Z`) - new Date(`1970-01-01T${frames[i-1].timestamp}Z`))}ms)` : '';
      console.log(`${frame.frameNumber}. [${frame.timestamp}] ${timeRelative} ${frame.direction} : ${frame.hex}`);
      if (frame.analysis) console.log(`    → ${frame.analysis}`);
    });
    return frames;
  };
  
  window.BLE_LED_COMMANDS = () => {
    const ledFrames = window.BLE_LOGS.filter(log => 
      log.isFrame && (
        log.analysis.includes('LED') || 
        log.hex.includes('1A 45') || 
        log.hex.includes('18 2B')
      )
    );
    console.log(`💡 Commandes LED trouvées: ${ledFrames.length}`);
    ledFrames.forEach((frame, i) => {
      console.log(`${i+1}. [${frame.timestamp}] ${frame.hex}`);
      console.log(`    → ${frame.analysis}`);
    });
    return ledFrames;
  };
  
  window.BLE_CLEAR = () => {
    window.BLE_LOGS = [];
    frameCounter = 0;
    console.clear();
    console.log('🧹 Logs effacés, compteur remis à zéro');
  };
  
  window.BLE_UNHOOK = () => {
    BluetoothRemoteGATTCharacteristic.prototype.writeValue = original.writeValue;
    BluetoothRemoteGATTCharacteristic.prototype.writeValueWithResponse = original.writeValueWithResponse;
    BluetoothRemoteGATTCharacteristic.prototype.writeValueWithoutResponse = original.writeValueWithoutResponse;
    BluetoothRemoteGATTCharacteristic.prototype.readValue = original.readValue;
    BluetoothRemoteGATTCharacteristic.prototype.startNotifications = original.startNotifications;
    console.log('🔧 BLE hooks supprimés');
  };

  // Extraction des trames potentiellement liées à la couleur (heuristique)
  window.BLE_COLOR_CANDIDATES = () => {
    const frames = window.BLE_LOGS.filter(f => f.isFrame);
    const candidates = frames.filter(f => {
      const h = f.hex;
      return / 18 3D /i.test(' ' + h + ' ') || / 18 0F /i.test(' ' + h + ' ') || / 18 2C /i.test(' ' + h + ' ') || / 18 2D /i.test(' ' + h + ' ');
    });
    console.log(`🎨 Candidats couleur trouvés: ${candidates.length}`);
    candidates.forEach((c,i) => {
      console.log(`${i+1}. #${c.frameNumber} [${c.timestamp}] ${c.hex}`);
      if (c.analysis) console.log('    → ' + c.analysis);
    });
    // Détection simple de triplets RGB changeants: repère trois octets consécutifs qui varient entre candidats
    if (candidates.length > 1) {
      console.log('🧪 Analyse variation triplets potentiels (glissant)');
      for (let i=1;i<candidates.length;i++) {
        const prev = candidates[i-1].raw;
        const cur = candidates[i].raw;
        for (let off=5; off < Math.min(prev.length, cur.length)-4; off++) {
            const triplePrev = prev.slice(off, off+3).join('-');
            const tripleCur = cur.slice(off, off+3).join('-');
            if (triplePrev !== tripleCur) {
              // Heuristique: variation non triviale (pas toutes zeros, pas identique)
              const nums = tripleCur.split('-').map(n=>parseInt(n,10));
              const sum = nums[0]+nums[1]+nums[2];
              if (sum>0 && sum<765) {
                console.log(`   Δ frame#${candidates[i-1].frameNumber}->#${candidates[i].frameNumber} offset ${off}: ${triplePrev} -> ${tripleCur}`);
              }
            }
        }
      }
    }
    lastColorCandidate = candidates[candidates.length-1] || null;
    return candidates;
  };

  // ================= Color tagging session utilities (added) =================
  window.BLE_COLOR_SESSION_START = (label) => {
    colorSession = { active:true, labels: label ? [label]:[], samples:[] };
    console.log('🎬 Color session started', label?'with first label '+label:'');
  };
  window.BLE_COLOR_TAG = (label) => {
    if (!colorSession.active) {
      console.warn('⚠️ Start session first with BLE_COLOR_SESSION_START()');
      return;
    }
    const latest = lastColorCandidate || window.BLE_COLOR_CANDIDATES().slice(-1)[0];
    if (!latest) {
      console.warn('❌ No color candidate frame yet. Move the robot over a surface then retry.');
      return;
    }
    const entry = { label, frameNumber: latest.frameNumber, raw: latest.raw, hex: latest.hex, timestamp: latest.timestamp };
    colorSession.labels.push(label);
    colorSession.samples.push(entry);
    console.log(`🏷️ Tagged frame#${latest.frameNumber} as ${label}`);
  };
  window.BLE_COLOR_SESSION_STOP = () => {
    colorSession.active = false;
    console.log('🛑 Color session stopped. Samples:', colorSession.samples.length);
    const matrix = colorSession.samples.map(s => ({ label:s.label, frame:s.frameNumber, bytes:s.raw.slice(0,40) }));
    console.table(matrix);
    return colorSession;
  };
  window.BLE_COLOR_EXPORT = () => {
    const blob = new Blob([JSON.stringify({ session: colorSession, timestamp: Date.now() }, null, 2)], { type:'application/json'});
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = 'ble-color-session.json'; a.click();
    URL.revokeObjectURL(url);
    console.log('📤 Exported color session');
  };
  
  console.log('✅ Enhanced BLE Sniffer installé!');
  console.log('📖 Commandes disponibles:');
  console.log('   BLE_SEQUENCE() - Voir la séquence complète avec timing');
  console.log('   BLE_LED_COMMANDS() - Voir uniquement les commandes LED');
  console.log('   BLE_COLOR_CANDIDATES() - Lister trames susceptibles de contenir des données couleur');
  console.log('   BLE_COLOR_SESSION_START(label?) / BLE_COLOR_TAG(label) / BLE_COLOR_SESSION_STOP() / BLE_COLOR_EXPORT()');
  console.log('   BLE_SAVE() - Sauvegarder les logs détaillés');
  console.log('   BLE_CLEAR() - Effacer et remettre à zéro');
  console.log('');
  console.log('🎯 FOCUS: Capture la séquence COMPLETE depuis la connexion jusqu\'à l\'activation LED!');
  console.log('   1. Connecte le robot sur Sphero Edu');
  console.log('   2. Utilise un bloc "when color sensor detects white"');
  console.log('   3. Regarde les 🔥LED_RGB🔥 et commandes qui précèdent');
})();