#include <DHT.h>

#define Type DHT11

const int dhtPin = 2;
const int irPin = 3;
DHT HT(dhtPin, Type);

// Pon en true cuando el sensor infrarrojo (RPM) este conectado.
const bool usarRPM = true;

float humedad;
float tempC;

unsigned long tiempoAnteriorDHT = 0;
const unsigned long intervaloDHT = 5000;

const int ledRojo = 11;
const int ledVerde = 10;

const float tempMin = 18.0;
const float tempMax = 28.0;
const float rpmMin = 2000.0;

unsigned long contadorPulsos = 0;
unsigned long tiempoAnteriorRPM = 0;
const unsigned long intervaloRPM = 1000;
int estadoAnteriorIR = HIGH;
float rpm = 0;

// Ultimo resultado de cada prueba (para enviarlos juntos y manejar los LEDs)
bool tempValida = false;
bool pruebaTempPasada = false;
bool rpmValida = false;
bool pruebaRpmPasada = false;

void setup() {
  Serial.begin(9600);
  HT.begin();

  pinMode(ledRojo, OUTPUT);
  pinMode(ledVerde, OUTPUT);
  pinMode(irPin, INPUT);
}

void mostrarResultado(bool paso) {
  digitalWrite(ledVerde, paso ? HIGH : LOW);
  digitalWrite(ledRojo,  paso ? LOW  : HIGH);
}

// Verde solo si pasan todas las pruebas que estan activas.
void actualizarLeds() {
  bool paso = true;
  if (tempValida && !pruebaTempPasada) paso = false;
  if (usarRPM && rpmValida && !pruebaRpmPasada) paso = false;
  mostrarResultado(paso);
}

void imprimirBool(bool valida, bool valor) {
  if (!valida) Serial.print("null");
  else Serial.print(valor ? "true" : "false");
}

// Envia una linea JSON por el puerto serial. Visual Studio la lee y la muestra.
// Ejemplo: {"temp":24.5,"hum":52.0,"rpm":null,"pruebaTemp":true,"pruebaRpm":null,"ms":15000}
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

  Serial.print(",\"ms\":");
  Serial.print(millis());
  Serial.println("}");
}

void pruebaDHT() {
  unsigned long tiempoActual = millis();

  if (tiempoActual - tiempoAnteriorDHT >= intervaloDHT) {
    tiempoAnteriorDHT = tiempoActual;

    humedad = HT.readHumidity();
    tempC   = HT.readTemperature();

    if (isnan(tempC) || isnan(humedad)) {
      tempValida = false;
      Serial.println("{\"error\":\"ERROR_SENSOR_DHT\"}");
      return;
    }

    tempValida = true;
    pruebaTempPasada = (tempC >= tempMin && tempC <= tempMax);

    actualizarLeds();
    enviarDatos();
  }
}

void medirRPM() {
  int estadoIR = digitalRead(irPin);

  if (estadoIR == LOW && estadoAnteriorIR == HIGH) {
    contadorPulsos++;
  }
  estadoAnteriorIR = estadoIR;

  unsigned long tiempoActual = millis();
  if (tiempoActual - tiempoAnteriorRPM >= intervaloRPM) {
    tiempoAnteriorRPM = tiempoActual;

    rpm = contadorPulsos * 60.0;
    contadorPulsos = 0;

    rpmValida = true;
    pruebaRpmPasada = (rpm >= rpmMin);

    actualizarLeds();
    enviarDatos();
  }
}

void loop() {
  if (usarRPM) medirRPM();
  pruebaDHT();
}
