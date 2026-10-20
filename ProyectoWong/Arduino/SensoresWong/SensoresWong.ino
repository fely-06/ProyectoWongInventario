#include <DHT.h>

#define Type DHT11
const int dhtPin = 2;
const int irPin = 3;            // Pin 3 = interrupcion externa (INT1 en Arduino Uno/Nano)
DHT HT(dhtPin, Type);

const bool usarRPM = true;
const int pulsosPorVuelta = 1;  // marcas reflectantes por cada vuelta del ventilador

const int ledRojo = 11;
const int ledVerde = 10;

const float tempMin = 18.0;
const float tempMax = 28.0;
const float rpmMin = 2000.0;

// ---- Medicion ----
const unsigned long intervaloMedicion = 1000;   // una lectura por segundo
unsigned long tiempoAnteriorMedicion = 0;

volatile unsigned long contadorPulsos = 0;      // se incrementa en la interrupcion
volatile unsigned long ultimoPulsoUs = 0;
const unsigned long antiReboteUs = 2000;        // ignora pulsos separados menos de 2 ms

float humedad = 0;
float tempC = 0;
float rpm = 0;

bool tempValida = false;
bool pruebaTempPasada = false;
bool rpmValida = false;
bool pruebaRpmPasada = false;

// ---- Estado de la prueba y de los LEDs ----
bool enPrueba = false;          // true mientras el PC pide medir
int resultado = 0;              // 0 = sin resultado, 1 = aprobado (verde fijo), 2 = rechazado (rojo fijo)

unsigned long ultimoParpadeo = 0;
bool estadoParpadeo = false;

// Se ejecuta en cada pulso del sensor infrarrojo (flanco de bajada)
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

  // Autoprueba de LEDs al encender: verde, rojo y apagados
  apagarLeds();
  digitalWrite(ledVerde, HIGH); delay(300); digitalWrite(ledVerde, LOW);
  digitalWrite(ledRojo, HIGH);  delay(300); digitalWrite(ledRojo, LOW);

  attachInterrupt(digitalPinToInterrupt(irPin), contarPulso, FALLING);
}

void loop() {
  leerComandos();

  if (enPrueba) {
    parpadearVerde();   // LED verde parpadeando = midiendo
    medir();
  }
}

// Comandos que envia el PC (una linea de texto cada uno):
//   START -> empieza a medir (LEDs apagados, verde parpadea)
//   STOP  -> deja de medir
//   OK    -> prueba aprobada: LED verde fijo
//   FAIL  -> prueba rechazada: LED rojo fijo
//   RESET -> apaga todo y vuelve a espera
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
    tempValida = false;
    rpmValida = false;
    tiempoAnteriorMedicion = millis();
    apagarLeds();
  }
  else if (comando == "STOP") {
    enPrueba = false;
    // Si todavia no hay resultado, apaga el parpadeo. Si ya hay resultado, no lo toca.
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

  Serial.print(",\"pruebaTemp\":");
  imprimirBool(tempValida, pruebaTempPasada);

  Serial.print(",\"pruebaRpm\":");
  imprimirBool(rpmValida, pruebaRpmPasada);

  Serial.println("}");
}

// Una medicion completa por segundo: RPM (por interrupcion) + DHT11, y se envia un solo JSON.
void medir() {
  unsigned long ahora = millis();
  unsigned long transcurrido = ahora - tiempoAnteriorMedicion;
  if (transcurrido < intervaloMedicion) return;
  tiempoAnteriorMedicion = ahora;

  // RPM = pulsos contados en la ventana * 60000 / (ms de la ventana * pulsos por vuelta)
  noInterrupts();
  unsigned long pulsos = contadorPulsos;
  contadorPulsos = 0;
  interrupts();

  if (usarRPM) {
    rpm = (pulsos * 60000.0) / ((float)transcurrido * pulsosPorVuelta);
    rpmValida = true;
    pruebaRpmPasada = (rpm >= rpmMin);
  }

  float h = HT.readHumidity();
  float t = HT.readTemperature();

  if (isnan(t) || isnan(h)) {
    tempValida = false;
    Serial.println("{\"error\":\"ERROR_SENSOR_DHT\"}");
  } else {
    tempValida = true;
    tempC = t;
    humedad = h;
    pruebaTempPasada = (tempC >= tempMin && tempC <= tempMax);
  }

  enviarDatos();
}
