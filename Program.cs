using System;
using System.IO.Ports;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;

/*
 * ================================================================
 *  BLUETOOTH BRIDGE - Windows Bluetooth to WebSocket
 *
 *  Responsabilidades:
 *    - Conectar a 2 ESP32 por Bluetooth (puertos COM virtuales)
 *    - Recibir datos JSON de ambas ESP32
 *    - Combinar datos de pesos + rotación
 *    - Enviar datos unificados al API via WebSocket
 *
 *  Requisitos:
 *    - .NET 6.0+
 *    - Paquetes NuGet:
 *      - WebSocketClient
 *      - System.IO.Ports
 *
 *  Configuración:
 *    - Puerto COM ESP32 #1 (Pesos): editar COM_PORT_WEIGHTS
 *    - Puerto COM ESP32 #2 (Rotación): editar COM_PORT_ROTATION
 *    - URL WebSocket API: editar WS_URL
 * ================================================================
 */

namespace BluetoothBridge {
    class Program {
        // ================================================================
        //  CONFIGURACIÓN
        // ================================================================
        private const string COM_PORT_WEIGHTS = "COM4";      // Puerto Bluetooth ESP32 #1
        private const string COM_PORT_ROTATION = "COM5";     // Puerto Bluetooth ESP32 #2
        private const int BAUD_RATE = 115200;
        private const string WS_URL = "ws://localhost:8081/ws/frontend";
        private const int RECONNECT_DELAY_MS = 5000;

        // ================================================================
        //  VARIABLES GLOBALES
        // ================================================================
        private static SerialPort? weightsSensor;
        private static SerialPort? rotationSensor;
        private static ClientWebSocket? wsClient;
        private static CancellationTokenSource cts = new();

        private static SensorDataBuffer weightData = new();
        private static SensorDataBuffer rotationData = new();
        private static int debugCounter = 0;

        // ================================================================
        //  MAIN
        // ================================================================
        static async Task Main(string[] args) {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("========================================");
            Console.WriteLine(" BLUETOOTH BRIDGE - VET SYSTEM");
            Console.WriteLine("========================================");
            Console.WriteLine("");

            try {
                // Inicializar conexiones
                Console.WriteLine("[*] Inicializando conexiones...");
                InitializeSerialPorts();
                await InitializeWebSocket();

                // Iniciar tareas de lectura
                var weightsTask = ReadWeightsSensorAsync();
                var rotationTask = ReadRotationSensorAsync();
                var dataFusionTask = DataFusionLoopAsync();

                await Task.WhenAll(weightsTask, rotationTask, dataFusionTask);
            }
            catch (Exception ex) {
                Console.WriteLine($"[!] Error: {ex.Message}");
            }
            finally {
                Cleanup();
            }
        }

        // ================================================================
        //  INICIALIZAR PUERTOS SERIE
        // ================================================================
        static void InitializeSerialPorts() {
            try {
                Console.WriteLine($"[*] Conectando a ESP32 #1 (Pesos) en {COM_PORT_WEIGHTS}...");
                weightsSensor = new SerialPort(COM_PORT_WEIGHTS, BAUD_RATE);
                weightsSensor.Open();
                Console.WriteLine("[✓] ESP32 #1 conectado");
            }
            catch (Exception ex) {
                Console.WriteLine($"[!] Error conectando ESP32 #1: {ex.Message}");
                weightsSensor = null;
            }

            try {
                Console.WriteLine($"[*] Conectando a ESP32 #2 (Rotación) en {COM_PORT_ROTATION}...");
                rotationSensor = new SerialPort(COM_PORT_ROTATION, BAUD_RATE);
                rotationSensor.Open();
                Console.WriteLine("[✓] ESP32 #2 conectado");
            }
            catch (Exception ex) {
                Console.WriteLine($"[!] Error conectando ESP32 #2: {ex.Message}");
                rotationSensor = null;
            }

            if (weightsSensor == null && rotationSensor == null) {
                throw new Exception("No se pudo conectar a ningún ESP32");
            }

            Console.WriteLine("");
        }

        // ================================================================
        //  INICIALIZAR WEBSOCKET
        // ================================================================
        static async Task InitializeWebSocket() {
            try {
                Console.WriteLine($"[*] Conectando a WebSocket: {WS_URL}...");
                wsClient = new ClientWebSocket();
                await wsClient.ConnectAsync(new Uri(WS_URL), cts.Token);
                Console.WriteLine("[✓] WebSocket conectado");
                Console.WriteLine("");
            }
            catch (Exception ex) {
                Console.WriteLine($"[!] Error conectando WebSocket: {ex.Message}");
                wsClient = null;
            }
        }

        // ================================================================
        //  LEER DATOS DE PESOS
        // ================================================================
        static async Task ReadWeightsSensorAsync() {
            while (!cts.Token.IsCancellationRequested) {
                try {
                    if (weightsSensor == null || !weightsSensor.IsOpen) {
                        await Task.Delay(RECONNECT_DELAY_MS, cts.Token);
                        continue;
                    }

                    if (weightsSensor.BytesToRead > 0) {
                        string line = weightsSensor.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line)) {
                            lock (weightData) {
                                weightData.ParseJson(line);
                            }
                        }
                    }

                    await Task.Delay(10, cts.Token);
                }
                catch (Exception ex) {
                    Console.WriteLine($"[!] Error leyendo pesos: {ex.Message}");
                    await Task.Delay(RECONNECT_DELAY_MS, cts.Token);
                }
            }
        }

        // ================================================================
        //  LEER DATOS DE ROTACIÓN
        // ================================================================
        static async Task ReadRotationSensorAsync() {
            while (!cts.Token.IsCancellationRequested) {
                try {
                    if (rotationSensor == null || !rotationSensor.IsOpen) {
                        await Task.Delay(RECONNECT_DELAY_MS, cts.Token);
                        continue;
                    }

                    if (rotationSensor.BytesToRead > 0) {
                        string line = rotationSensor.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line)) {
                            lock (rotationData) {
                                rotationData.ParseJson(line);
                            }
                        }
                    }

                    await Task.Delay(10, cts.Token);
                }
                catch (Exception ex) {
                    Console.WriteLine($"[!] Error leyendo rotación: {ex.Message}");
                    await Task.Delay(RECONNECT_DELAY_MS, cts.Token);
                }
            }
        }

        // ================================================================
        //  FUSIÓN DE DATOS
        // ================================================================
        static async Task DataFusionLoopAsync() {
            int sendInterval = 50;  // Enviar cada 50ms (20 Hz)
            long lastSendTime = DateTime.UtcNow.Ticks / 10000;

            while (!cts.Token.IsCancellationRequested) {
                try {
                    long now = DateTime.UtcNow.Ticks / 10000;

                    if (now - lastSendTime >= sendInterval) {
                        lastSendTime = now;

                        // Combinar datos
                        var fusedData = FuseData();

                        // Enviar por WebSocket
                        if (wsClient != null && wsClient.State == WebSocketState.Open) {
                            await SendViaWebSocketAsync(fusedData);
                        }

                        // Debug
                        PrintDebugInfo(fusedData);
                    }

                    await Task.Delay(10, cts.Token);
                }
                catch (Exception ex) {
                    Console.WriteLine($"[!] Error en fusión de datos: {ex.Message}");
                }
            }
        }

        // ================================================================
        //  FUSIONAR DATOS DE AMBAS FUENTES
        // ================================================================
        static FusedSensorData FuseData() {
            lock (weightData) {
                lock (rotationData) {
                    return new FusedSensorData {
                        // Datos de pesos (ESP32 #1)
                        WeightDistributionLF = weightData.GetValue("weightDistributionLF"),
                        WeightDistributionRF = weightData.GetValue("weightDistributionRF"),
                        WeightDistributionLB = weightData.GetValue("weightDistributionLB"),
                        WeightDistributionRB = weightData.GetValue("weightDistributionRB"),
                        TotalWeight = weightData.GetValue("totalWeight"),
                        COP = weightData.GetObject("cop"),

                        // Datos de rotación (ESP32 #2)
                        Gyroscope = rotationData.GetObject("gyroscope"),
                        Accelerometer = rotationData.GetObject("accelerometer"),
                        Angles = rotationData.GetObject("angles"),
                        Temperature = rotationData.GetValue("temperature"),

                        // Metadatos
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
        }

        // ================================================================
        //  ENVIAR POR WEBSOCKET
        // ================================================================
        static async Task SendViaWebSocketAsync(FusedSensorData data) {
            try {
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions {
                    WriteIndented = false
                });

                var bytes = Encoding.UTF8.GetBytes(json);
                await wsClient.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cts.Token
                );
            }
            catch (Exception ex) {
                Console.WriteLine($"[!] Error enviando por WebSocket: {ex.Message}");
            }
        }

        // ================================================================
        //  DEBUG
        // ================================================================
        static void PrintDebugInfo(FusedSensorData data) {
            if (++debugCounter >= 10) {  // Imprimir cada 500ms
                debugCounter = 0;
                Console.Write($"[DATA] Pesos: LF={data.WeightDistributionLF:F2} RF={data.WeightDistributionRF:F2} | ");
                try {
                    var roll = data.Angles?.GetProperty("roll").GetDouble() ?? 0.0;
                    var pitch = data.Angles?.GetProperty("pitch").GetDouble() ?? 0.0;
                    Console.WriteLine($"Ángulos: R={roll:F1}° P={pitch:F1}°");
                }
                catch {
                    Console.WriteLine("");
                }
            }
        }

        // ================================================================
        //  LIMPIAR RECURSOS
        // ================================================================
        static void Cleanup() {
            Console.WriteLine("\n[*] Limpiando recursos...");

            if (weightsSensor != null) {
                if (weightsSensor.IsOpen) weightsSensor.Close();
                weightsSensor.Dispose();
            }

            if (rotationSensor != null) {
                if (rotationSensor.IsOpen) rotationSensor.Close();
                rotationSensor.Dispose();
            }

            if (wsClient != null) {
                if (wsClient.State == WebSocketState.Open) {
                    wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait();
                }
                wsClient.Dispose();
            }

            cts?.Dispose();
            Console.WriteLine("[✓] Limpieza completada. Bye!");
        }
    }

    // ================================================================
    //  CLASE: BUFFER DE DATOS DE SENSOR
    // ================================================================
    class SensorDataBuffer {
        private Dictionary<string, object> data = new();
        private object lockObj = new();

        public void ParseJson(string json) {
            try {
                lock (lockObj) {
                    using (JsonDocument doc = JsonDocument.Parse(json)) {
                        var root = doc.RootElement;

                        // Guardar todos los valores
                        foreach (var property in root.EnumerateObject()) {
                            if (property.Value.ValueKind == JsonValueKind.Number) {
                                data[property.Name] = property.Value.GetDouble();
                            }
                            else if (property.Value.ValueKind == JsonValueKind.Object) {
                                data[property.Name] = property.Value.Clone();
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"[!] Error parseando JSON: {ex.Message}");
            }
        }

        public double GetValue(string key) {
            lock (lockObj) {
                if (!data.ContainsKey(key)) return 0.0;
                var value = data[key];
                return value is double d ? d : 0.0;
            }
        }

        public JsonElement? GetObject(string key) {
            lock (lockObj) {
                if (!data.ContainsKey(key)) return null;
                var value = data[key];
                return value is JsonElement je ? (JsonElement?)je : null;
            }
        }
    }

    // ================================================================
    //  CLASE: DATOS FUSIONADOS
    // ================================================================
    class FusedSensorData {
        // Pesos
        public double WeightDistributionLF { get; set; }
        public double WeightDistributionRF { get; set; }
        public double WeightDistributionLB { get; set; }
        public double WeightDistributionRB { get; set; }
        public double TotalWeight { get; set; }
        public JsonElement? COP { get; set; }

        // Rotación
        public JsonElement? Gyroscope { get; set; }
        public JsonElement? Accelerometer { get; set; }
        public JsonElement? Angles { get; set; }
        public double Temperature { get; set; }

        // Metadatos
        public DateTime Timestamp { get; set; }
    }
}
