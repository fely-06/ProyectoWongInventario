#include <DHT.h>

#define Type DHT11
const int dhtPin = 2;
const int irPin = 3;
DHT HT(dhtPin, Type);

const bool usarRPM = true;
float humedad;
float tempC;

unsigned long tiempoAnteriorDHT = 0;
const unsigned long intervaloDHT = 1000; // 1 segundo durante la prueba

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

bool tempValida = false;
bool pruebaTempPasada = false;
bool rpmValida = false;
bool pruebaRpmPasada = false;

// ✅ NUEVO: Controla si el Arduino debe medir o esperar
bool enPrueba = false; 

void setup() {
  Serial.begin(9600);
  HT.begin();
  pinMode(ledRojo, OUTPUT);
  pinMode(ledVerde, OUTPUT);
  pinMode(irPin, INPUT);
  
  digitalWrite(ledVerde, LOW);
  digitalWrite(ledRojo, LOW);
}

void loop() {
  // 1. ESCUCHAR COMANDOS DE LA PC (START o STOP)
  if (Serial.available() > 0) {
    String comando = Serial.readStringUntil('\n');
    comando.trim(); 
    
    if (comando == "START") {
      enPrueba = true;
      contadorPulsos = 0;
      tempValida = false;
      rpmValida = false;
      digitalWrite(ledVerde, HIGH); // LED verde = Midiendo
      digitalWrite(ledRojo, LOW);
    } 
    else if (comando == "STOP") {
      enPrueba = false;
      digitalWrite(ledVerde, LOW);
      digitalWrite(ledRojo, LOW);
    }
  }

  // 2. SOLO MEDIR Y ENVIAR SI ESTÁ EN MODO PRUEBA
  if (enPrueba) {
    if (usarRPM) medirRPM();
    pruebaDHT();
  }
}

void mostrarResultado(bool paso) {
  digitalWrite(ledVerde, paso ? HIGH : LOW);
  digitalWrite(ledRojo,  paso ? LOW  : HIGH);
}

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

void pruebaDHT() {
  unsigned long tiempoActual = millis();
  if (tiempoActual - tiempoAnteriorDHT >= intervaloDHT) {
    tiempoAnteriorDHT = tiempoActual;
    
    float h = HT.readHumidity();
    float t = HT.readTemperature();
    
    if (isnan(t) || isnan(h)) {
      tempValida = false;
      Serial.println("{\"error\":\"ERROR_SENSOR_DHT\"}");
      return;
    }
    
    tempValida = true;
    tempC = t;
    humedad = h;
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