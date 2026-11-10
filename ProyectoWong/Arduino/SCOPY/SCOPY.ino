#include <DHT.h>
#include <Wire.h>

// ---- Configuración de Sensores ----
#define Type DHT11                  // Cambiar a DHT22 si usas ese modelo
const int dhtPin = 2;
const int irPin = 3;                // Interrupción externa (INT1 en Uno/Nano)
const int acs712Pin = A0;           // Pin analógico para sensor de corriente
const int MPU_ADDR = 0x68;          // Dirección I2C del HW-123 (MPU6050)
const int ruidoPin = A1;            // Salida analógica (AO) del sensor de sonido RQ-S001

DHT HT(dhtPin, Type);

// ---- Configuración de Límites de Prueba (Ajusta según tus especificaciones) ----
const float tempMin = 18.0;
const float tempMax = 28.0;
const float humMin = 60.0;          // % mínimo: el difusor de agua debe humedecer el aire
const float humMax = 90.0;          // % máximo (límite útil del DHT11)
const float rpmMin = 2000.0;
const float corrienteMax = 500.0;   // Ejemplo: 500 mA máximo permitido
const float vibracionMax = 1.30;    // Umbral de vibración (en g). 1.0 = quieto.
const float ruidoMax = 30.0;        // Nivel de ruido máximo permitido (dB aprox.)

// ---- Calibración del sensor de sonido RQ-S001 ----
// El módulo entrega la señal del micrófono montada en ~2.5 V. Se mide la amplitud pico a pico
// durante una ventana corta y se convierte a dB con: dB = dbReferencia + 20*log10(pp / ppReferencia).
// Para calibrar: con un sonómetro (o app del celular) mide el ruido de tu cuarto, pon ese valor
// en dbReferencia y en ppReferencia la amplitud cruda ("ruidoPP") que imprime el monitor serial.
// Gira el potenciómetro del módulo para que en silencio la amplitud sea baja pero no 0.
const unsigned long ventanaRuidoMs = 50;   // 50 ms = al menos 2-3 ciclos de las frecuencias del ventilador
const int muestrasRuido = 5;               // ventanas promediadas por lectura (~250 ms en total)
const float ppReferencia = 10.0;           // amplitud cruda medida en el ambiente de referencia
const float dbReferencia = 40.0;           // dB de ese ambiente de referencia

// ---- Pines de Salida ----
const int ledRojo = 11;
const int ledVerde = 10;

// ---- Variables de Medición ----
const unsigned long intervaloMedicion = 1000;   // 1 lectura por segundo
unsigned long tiempoAnteriorMedicion = 0;

volatile unsigned long contadorPulsos = 0;      
volatile unsigned long ultimoPulsoUs = 0;
const unsigned long antiReboteUs = 2000;        

float humedad = 0;
float tempC = 0;
float rpm = 0;
float corriente = 0;      // en mA
float vibracion = 0;      // índice de vibración (magnitud en g)
float ruido = 0;          // nivel de ruido (dB aprox.)
int ruidoPP = 0;          // amplitud cruda pico a pico (0-1023), útil para calibrar

bool tempValida = false, pruebaTempPasada = false, pruebaHumPasada = false;
bool rpmValida = false, pruebaRpmPasada = false;
bool corrienteValida = false, pruebaCorrientePasada = false;
bool vibracionValida = false, pruebaVibracionPasada = false;
bool ruidoValido = false, pruebaRuidoPasada = false;

// ---- Estado de la prueba ----
bool enPrueba = false;          
int resultado = 0;              // 0=sin resultado, 1=aprobado, 2=rechazado
unsigned long ultimoParpadeo = 0;
bool estadoParpadeo = false;

void contarPulso() {
  unsigned long ahora = micros();
  if (ahora - ultimoPulsoUs >= antiReboteUs) {
    contadorPulsos++;
    ultimoPulsoUs = ahora;
  }
}

void apagarLeds() {
  digitalWrite(ledVerde, LOW);
  digitalWrite(ledRojo, LOW);
}

void mostrarResultado(bool paso) {
  digitalWrite(ledVerde, paso ? HIGH : LOW);
  digitalWrite(ledRojo,  paso ? LOW  : HIGH);
}

void setup() {
  Serial.begin(9600);
  Serial.setTimeout(50);
  HT.begin();
  
  pinMode(ledRojo, OUTPUT);
  pinMode(ledVerde, OUTPUT);
  pinMode(irPin, INPUT);
  
  // Inicializar I2C para el HW-123 (MPU6050)
  Wire.begin();
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(0x6B);  // Registro PWR_MGMT_1
  Wire.write(0);     // Despertar el MPU6050
  Wire.endTransmission(true);

  // Autoprueba de LEDs al encender
  apagarLeds();
  digitalWrite(ledVerde, HIGH); delay(300); digitalWrite(ledVerde, LOW);
  digitalWrite(ledRojo, HIGH);  delay(300); digitalWrite(ledRojo, LOW);
  
  attachInterrupt(digitalPinToInterrupt(irPin), contarPulso, FALLING);
}

void loop() {
  leerComandos();
  if (enPrueba) {
    parpadearVerde();   
    medir();
  }
}

void leerComandos() {
  if (Serial.available() <= 0) return;
  String comando = Serial.readStringUntil('\n');
  comando.trim();
  comando.toUpperCase();
  
  if (comando == "START") {
    enPrueba = true;
    resultado = 0;
    noInterrupts();
    contadorPulsos = 0;
    interrupts();
    tempValida = rpmValida = corrienteValida = vibracionValida = ruidoValido = false;
    tiempoAnteriorMedicion = millis();
    apagarLeds();
  }
  else if (comando == "STOP") {
    enPrueba = false;
    if (resultado == 0) apagarLeds();
  }
  else if (comando == "OK") {
    enPrueba = false;
    resultado = 1;
    mostrarResultado(true);
  }
  else if (comando == "FAIL") {
    enPrueba = false;
    resultado = 2;
    mostrarResultado(false);
  }
  else if (comando == "RESET") {
    enPrueba = false;
    resultado = 0;
    apagarLeds();
  }
}

void parpadearVerde() {
  unsigned long ahora = millis();
  if (ahora - ultimoParpadeo >= 300) {
    ultimoParpadeo = ahora;
    estadoParpadeo = !estadoParpadeo;
    digitalWrite(ledRojo, LOW);
    digitalWrite(ledVerde, estadoParpadeo ? HIGH : LOW);
  }
}

void imprimirBool(bool valida, bool valor) {
  if (!valida) Serial.print("null");
  else Serial.print(valor ? "true" : "false");
}

void enviarDatos() {
  Serial.print("{\"temp\":");
  if (tempValida) Serial.print(tempC, 1); else Serial.print("null");
  
  Serial.print(",\"hum\":");
  if (tempValida) Serial.print(humedad, 1); else Serial.print("null");
  
  Serial.print(",\"rpm\":");
  if (rpmValida) Serial.print(rpm, 0); else Serial.print("null");
  
  Serial.print(",\"corriente\":");
  if (corrienteValida) Serial.print(corriente, 1); else Serial.print("null");
  
  Serial.print(",\"vibracion\":");
  if (vibracionValida) Serial.print(vibracion, 2); else Serial.print("null");
  
  Serial.print(",\"ruido\":");
  if (ruidoValido) Serial.print(ruido, 1); else Serial.print("null");

  Serial.print(",\"ruidoPP\":");
  if (ruidoValido) Serial.print(ruidoPP); else Serial.print("null");

  Serial.print(",\"pruebaTemp\":");
  imprimirBool(tempValida, pruebaTempPasada);

  Serial.print(",\"pruebaHum\":");
  imprimirBool(tempValida, pruebaHumPasada);
  
  Serial.print(",\"pruebaRpm\":");
  imprimirBool(rpmValida, pruebaRpmPasada);
  
  Serial.print(",\"pruebaCorriente\":");
  imprimirBool(corrienteValida, pruebaCorrientePasada);
  
  Serial.print(",\"pruebaVibracion\":");
  imprimirBool(vibracionValida, pruebaVibracionPasada);

  Serial.print(",\"pruebaRuido\":");
  imprimirBool(ruidoValido, pruebaRuidoPasada);
  
  Serial.println("}");
}

void medir() {
  unsigned long ahora = millis();
  unsigned long transcurrido = ahora - tiempoAnteriorMedicion;
  if (transcurrido < intervaloMedicion) return;
  tiempoAnteriorMedicion = ahora;

  // 1. RPM (por interrupción)
  noInterrupts();
  unsigned long pulsos = contadorPulsos;
  contadorPulsos = 0;
  interrupts();
  
  rpm = (pulsos * 60000.0) / ((float)transcurrido * 1.0); // 1 pulso por vuelta
  rpmValida = true;
  pruebaRpmPasada = (rpm >= rpmMin);

  // 2. Temperatura y Humedad (DHT)
  float h = HT.readHumidity();
  float t = HT.readTemperature();
  if (isnan(t) || isnan(h)) {
    tempValida = false;
  } else {
    tempValida = true;
    tempC = t;
    humedad = h;
    pruebaTempPasada = (tempC >= tempMin && tempC <= tempMax);
    pruebaHumPasada = (humedad >= humMin && humedad <= humMax);
  }

  // 3. Corriente (ACS712) - Promedio de 10 lecturas para filtrar ruido
  int lecturas = 10;
  int suma = 0;
  for (int i = 0; i < lecturas; i++) {
    suma += analogRead(acs712Pin);
    delay(2);
  }
  int promedio = suma / lecturas;
  
  // Fórmula para ACS712 de 5A: (promedio - 512) / 185.0 * 1000 = mA
  // NOTA: Si usas el módulo de 20A, cambia 185.0 por 100.0. Si es de 30A, por 66.0.
  corriente = ((promedio - 512) / 185.0) * 1000.0; 
  if (corriente < 0) corriente = 0; // Filtrar ruido negativo en reposo
  
  corrienteValida = true;
  pruebaCorrientePasada = (corriente <= corrienteMax);

  // 4. Vibración (HW-123 / MPU6050)
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(0x3B);  // Registro ACCEL_XOUT_H
  Wire.endTransmission(false);
  Wire.requestFrom(MPU_ADDR, 6, true); // Leer 6 bytes (X, Y, Z)
  
  if (Wire.available() == 6) {
    int16_t AcX = Wire.read() << 8 | Wire.read();
    int16_t AcY = Wire.read() << 8 | Wire.read();
    int16_t AcZ = Wire.read() << 8 | Wire.read();
    
    // Convertir a fuerza g (configuración por defecto ±2g: 1g = 16384)
    float gx = AcX / 16384.0;
    float gy = AcY / 16384.0;
    float gz = AcZ / 16384.0;
    
    // Calcular la magnitud total del vector de aceleración
    vibracion = sqrt(gx*gx + gy*gy + gz*gz);
    vibracionValida = true;
    pruebaVibracionPasada = (vibracion <= vibracionMax);
  } else {
    vibracionValida = false;
  }

  // 5. Ruido (RQ-S001)
  medirRuido();

  enviarDatos();
}

// Amplitud pico a pico de la señal del micrófono en una ventana de tiempo.
int amplitudRuido(unsigned long ventanaMs) {
  int maximo = 0;
  int minimo = 1023;
  unsigned long inicio = millis();
  while (millis() - inicio < ventanaMs) {
    int v = analogRead(ruidoPin);
    if (v > maximo) maximo = v;
    if (v < minimo) minimo = v;
  }
  return maximo - minimo;
}

void medirRuido() {
  long suma = 0;
  for (int i = 0; i < muestrasRuido; i++) {
    suma += amplitudRuido(ventanaRuidoMs);
  }
  ruidoPP = suma / muestrasRuido;

  // Amplitud 0 = sin señal (log10(0) no existe): se toma como 1 para dar el nivel mínimo medible
  float pp = ruidoPP < 1 ? 1.0 : (float)ruidoPP;
  ruido = dbReferencia + 20.0 * log10(pp / ppReferencia);
  if (ruido < 0) ruido = 0;

  ruidoValido = true;
  pruebaRuidoPasada = (ruido <= ruidoMax);
}