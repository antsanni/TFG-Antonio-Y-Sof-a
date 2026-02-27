# Herramienta de Simulación 3D para Patrulla Autónoma con Dron en Operaciones de Salvamento

Este repositorio contiene el material asociado al Trabajo de Fin de Grado de 
(Grado en Desarrollo de Videojuegos · Facultad de Informática · UCM).

El repositorio fue concebido inicialmente como **referencia técnica aislada** de scripts y componentes clave.
Actualmente incluye también la **aplicación completa en Unity**, permitiendo su análisis, ejecución y reutilización.

---

## 📂 Estructura del repositorio

+---App/ ← ✅ Proyecto completo de Unity 

+---App_code/ ← 📎 Scripts principales extraídos (versión compacta para consulta)
| BatteryManager.cs
| CameraController.cs
| Drone.cs
| ...

+---Server_code/ ← 🛰️ Servidor WebSocket (Python)
| server.py
| connection_test.py
| execute_server.txt

---Simulator_code/ ← 🧪 Integración con DroneKit-SITL / ArduPilot
setup_commands.txt
launch_dronekit.ps1

---

## 🔧 Componentes principales

### ✅ `App/` – Aplicación Unity completa

Contiene el proyecto íntegro de Unity listo para abrir con:

- **Unity 2022.3 LTS** o superior  
- **Plugin Mapbox para Unity**

Incluye escenas, prefabs, scripts y materiales originales del simulador 3D.

---

### 📎 `App_code/` – Scripts C# en versión simplificada

Incluye únicamente los scripts clave del proyecto para facilitar su consulta sin necesidad de abrir Unity.

Ejemplos:

| Categoría | Scripts |
|-----------|------------------------------|
| 🛰️ Control del dron | `Drone.cs`, `DroneWSClient.cs`, `RouteManager.cs` |
| 🔋 Energía y batería | `BatteryManager.cs` |
| 📍 Generación de rutas | `ZoneSelector.cs`, `ZoneProgressManager.cs` |
| 🎮 Interfaz y cámara | `CameraController.cs`, `IconsLayer.cs`, etc. |

---

### 🛰️ `Server_code/` – Servidor WebSocket en Python

| Archivo | Descripción |
|---------|-------------|
| `server.py` | Núcleo del servidor: gestiona telemetría y comandos |
| `connection_test.py` | Prueba rápida de comunicación |
| `execute_server.txt` | Instrucciones para ejecución en entorno virtual (equivalente a `.bat`) |

---

### 🧪 `Simulator_code/` – Integración con DroneKit-SITL / ArduPilot

| Archivo | Uso |
|---------|-----|
| `setup_commands.txt` | Script `.sh` para Linux (WSL recomendado) |
| `launch_dronekit.ps1` | Lanzador PowerShell para Windows |

---

## 🛠️ Requisitos técnicos

| Componente | Requisito |
|------------|----------|
| **Aplicación Unity** | Unity 2022.3 LTS + Plugin Mapbox |
| **Servidor Python** | Python 3.10+ con `dronekit`, `websockets` |
| **Simulador** | DroneKit-SITL + ArduPilot (Linux / WSL en Windows) |

---

## 📜 Licencia / Uso

Este repositorio se distribuye exclusivamente con fines **académicos y de investigación**.  
Para consultas, mejoras o reutilización en otros proyectos, contactar con el autor.
