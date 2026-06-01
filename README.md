# Sistema Multidron con IA y Gemelo Digital en 3D para Operaciones de Salvamento

Este repositorio contiene el material asociado al Trabajo de Fin de Grado de:
- **Antonio** (Grado en Ingeniería de Computadores · Facultad de Informática · UCM)
- **Sofía** (Grado en Desarrollo de Videojuegos · Facultad de Informática · UCM)

El repositorio incluye el **proyecto completo**, abarcando el gemelo digital en Unity, el servidor de Inteligencia Artificial (basado en YOLO para detección de personas y animales), y los scripts de simulación física del enjambre de drones.

---

## 🎥 Vídeo de Demostración

Puedes ver una demostración del sistema en funcionamiento en el siguiente enlace:
[**Ver Vídeo de Demostración**](https://youtu.be/pPDA59K-KQs)

---

## 📂 Estructura del repositorio

- **`App/`** ← ✅ **Proyecto completo de Unity** (Gemelo Digital y UI).
- **`App_code/`** ← 📎 Scripts principales de Unity extraídos (versión compacta para consulta rápida).
- **`Server_code/`** ← 🛰️ **Servidor Central e IA (Python)**. Coordina el enjambre por WebSocket y ejecuta los modelos YOLO.
- **`Simulator_code/`** ← 🧪 Integración con ArduPilot SITL / DroneKit (Scripts y utilidades).
- **`lanzar_drones.sh`** ← 🚀 Script en bash (para Ubuntu/WSL) que inicializa las instancias de los drones físicos simulados.
- **`Manual_Ejecucion.md`** ← 📖 **Manual paso a paso** para arrancar todos los componentes del sistema.

---

## 🔧 Componentes principales

### ✅ `App/` – Gemelo Digital en Unity (Frontend)
Contiene el proyecto íntegro de Unity listo para abrir con:
- **Unity 2022.3 LTS** o superior.
- **Plugin Mapbox para Unity**.
Representa en tiempo real la telemetría del enjambre, baterías, rutas de barrido y detecciones enviadas por la IA.

### 🛰️ `Server_code/` – Servidor Central e IA (Backend)
Núcleo desarrollado en Python. Se encarga de:
- Conectarse mediante MAVLink a los drones simulados.
- Procesar el feed de las cámaras simuladas con Inteligencia Artificial (**YOLO**) para identificar personas o animales extraviados.
- Levantar el servidor WebSocket (`server.py`) para comunicarse en tiempo real con Unity.

### 🧪 Simulación del Enjambre (ArduPilot / SITL)
Mediante el script `lanzar_drones.sh`, se despliegan de forma programática las instancias de vehículos aéreos simulados, que emiten su telemetría y escuchan comandos como si fueran vehículos físicos reales.

---

## 🚀 Ejecución del Sistema

Para levantar el sistema completo, los módulos deben arrancarse en el siguiente orden:

1. **Simulación de Drones (SITL):** Ejecutar `./lanzar_drones.sh` en un entorno Ubuntu/WSL.
2. **Servidor IA:** Ejecutar `python server.py` dentro de la carpeta `Server_code`.
3. **Gemelo Digital Unity:** Abrir el proyecto en la carpeta `App` y pulsar *Play* en el editor.

> Para más detalles y requisitos de configuración, consultar el archivo [**Manual_Ejecucion.md**](Manual_Ejecucion.md).

---

## 🛠️ Requisitos técnicos

| Componente | Requisito |
|------------|----------|
| **Gemelo Digital (App)** | Unity 2022.3 LTS + Plugin Mapbox |
| **Servidor / IA** | Python 3.10+ (dependencias como `dronekit`, `websockets`, modelo YOLO) |
| **Simulador** | Entorno Ubuntu (nativo o WSL en Windows) con ArduPilot instalado |

---

## 📜 Licencia / Uso

Este repositorio se distribuye exclusivamente con fines **académicos y de investigación**.  
Para consultas, mejoras o reutilización en otros proyectos, contactar con los autores.
