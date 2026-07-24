# Bluetooth Bridge - Windows Bluetooth to WebSocket

Puente de comunicación que recibe datos de dos ESP32 por Bluetooth y los reenvía al WebSocket del sistema de estabilometría canina.

## 📋 Descripción

Este programa Windows actúa como intermediario entre:
- **Entrada:** 2 ESP32 conectadas por Bluetooth (puertos COM virtuales)
- **Salida:** WebSocket que envía datos al Frontend

Recibe datos JSON de ambas ESP32, los combina y los envía en tiempo real.

## 🔧 Requisitos

- Windows 10/11
- .NET 6.0 Runtime o SDK
- 2 puertos COM virtuales (emulados por Bluetooth)
- Conexión a WebSocket local (puerto 8081)

## 📦 Instalación

### 1. Instalar .NET 6.0

Descarga de: https://dotnet.microsoft.com/download/dotnet/6.0

Verifica la instalación:
```bash
dotnet --version
```

### 2. Compilar el Proyecto

```bash
# En la carpeta BluetoothBridge
dotnet build --configuration Release
```

### 3. Configurar Puertos COM

Edita `Program.cs` líneas 30-31:

```csharp
private const string COM_PORT_WEIGHTS = "COM4";    // Cambiar a tu puerto
private const string COM_PORT_ROTATION = "COM5";   // Cambiar a tu puerto
```

**¿Cómo encontrar los puertos COM?**
1. Empareja las ESP32 en Configuración → Dispositivos → Bluetooth
2. Abre Administrador de dispositivos
3. Busca "Puertos COM y LPT"
4. Identifica qué puerto corresponde a cada ESP32

## 🚀 Ejecución

```bash
# Modo desarrollo
dotnet run

# Modo Release (recomendado)
dotnet run --configuration Release
```

**Salida esperada:**
```
========================================
 BLUETOOTH BRIDGE - VET SYSTEM
========================================

[*] Conectando a ESP32 #1 (Pesos) en COM4...
[✓] ESP32 #1 conectado
[*] Conectando a ESP32 #2 (Rotación) en COM5...
[✓] ESP32 #2 conectado
[*] Conectando a WebSocket: ws://localhost:8081/ws/frontend...
[✓] WebSocket conectado

[DATA] Datos Bluetooth recibidos...
```

## 📊 Flujo de Datos

```
ESP32 #1 (Pesos)  ──Bluetooth──┐
                                 ├─→ COM4/COM5 (Puertos Virtuales)
ESP32 #2 (Rotación)──Bluetooth──┘
                                 ↓
                         Bluetooth Bridge
                                 ↓
                    WebSocket (ws://localhost:8081)
                                 ↓
                            Frontend React
```

## 🔌 Formato de Datos

### Entrada (desde ESP32 #1 - Pesos):
```json
{
  "weightDistributionLF": 12.34,
  "weightDistributionRF": 11.45,
  "weightDistributionLB": 13.22,
  "weightDistributionRB": 12.99,
  "totalWeight": 50.00,
  "cop": { "x": -0.045, "y": 0.123 },
  "timestamp": 1703079600000,
  "espId": 1
}
```

### Entrada (desde ESP32 #2 - Rotación):
```json
{
  "accelerometer": { "x": 0.125, "y": -0.034, "z": 9.81 },
  "gyroscope": { "x": 1.2, "y": -0.5, "z": 0.3 },
  "angles": { "roll": 2.5, "pitch": -1.2, "yaw": 0.0 },
  "temperature": 28.5,
  "timestamp": 1703079600000,
  "espId": 2
}
```

### Salida (combinada al WebSocket):
```json
{
  "weightDistributionLF": 12.34,
  "weightDistributionRF": 11.45,
  "weightDistributionLB": 13.22,
  "weightDistributionRB": 12.99,
  "totalWeight": 50.00,
  "cop": { "x": -0.045, "y": 0.123 },
  "gyroscope": { "x": 1.2, "y": -0.5, "z": 0.3 },
  "accelerometer": { "x": 0.125, "y": -0.034, "z": 9.81 },
  "angles": { "roll": 2.5, "pitch": -1.2, "yaw": 0.0 },
  "temperature": 28.5,
  "timestamp": 1703079600000
}
```

## ⚙️ Configuración Avanzada

### Cambiar URLs/Puertos

En `Program.cs`:

```csharp
private const int BAUD_RATE = 115200;           // Velocidad serial
private const string WS_URL = "ws://localhost:8081/ws/frontend";  // WebSocket
private const int RECONNECT_DELAY_MS = 5000;    // Reintentos
```

### Frecuencia de Envío

```csharp
int sendInterval = 50;  // 50ms = 20 Hz (línea ~145)
```

## 🐛 Troubleshooting

| Problema | Solución |
|----------|----------|
| "Puerto COM no encontrado" | Verifica emparejamiento Bluetooth, revisa números de puerto |
| "No se puede conectar a WebSocket" | Comprueba que WebSocket esté corriendo en puerto 8081 |
| "Datos no se reciben" | Verifica que ESP32 están enviando (revisar Serial Monitor) |
| "Error de compilación" | Instala .NET 6.0, ejecuta `dotnet restore` |

## 📝 Estructura de Archivos

```
BluetoothBridge/
├── Program.cs                  # Código principal
├── BluetoothBridge.csproj     # Configuración proyecto
├── README.md                   # Este archivo
├── INICIAR_REPO.md            # Instrucciones Git
├── .gitignore                 # Configuración Git
└── bin/Release/               # Binario compilado
```

## 🔄 Ciclo de Vida

1. **Conexión a ESP32:** Lee datos cada 50ms de ambas ESP32
2. **Fusión de datos:** Combina pesos + rotación
3. **Envío WebSocket:** Reenvía datos al frontend cada 50ms
4. **Reconexión:** Si se pierde conexión, reintentos automáticos

## 📚 Documentación Relacionada

- [Guía de Instalación Completa](../INSTALACION_Y_USO.md)
- [ESP32 Firmware](../ESP32_Bluetooth_Firmware/README.md)
- [Resumen de Cambios](../RESUMEN_CAMBIOS.md)

## ⚠️ Advertencias

1. **Puertos COM:** Deben estar emparejados antes de ejecutar
2. **WebSocket:** Debe estar corriendo en puerto 8081
3. **Firewall:** Permite conexiones localhost si es necesario
4. **Performance:** 20 Hz es estable, no aumentes sin probar

## 📞 Soporte

- Revisa los logs en consola para diagnóstico
- Verifica que los puertos COM sean correctos
- Asegúrate de que ESP32 están emparejadas

---

**Versión:** 2.0 (Bluetooth)  
**Última actualización:** 2024-12-19  
**Estado:** Listo para producción ✅
