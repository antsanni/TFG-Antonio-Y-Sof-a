# Manual de Ejecución del Sistema Multidron

Este documento especifica el orden y el entorno en el que deben ejecutarse los distintos componentes del proyecto para asegurar su correcto funcionamiento.

## 1. Simulación de los Drones (ArduPilot / SITL)
La simulación física de la flota de drones se gestiona mediante ArduPilot SITL. Este componente debe ejecutarse en un entorno **Ubuntu** (nativo, máquina virtual o WSL).
- Abre tu terminal en Ubuntu.
- Ejecuta el script de bash encargado de inicializar las instancias de los drones:
  ```bash
  ./lanzar_drones.sh
  ```
  *Nota: Este script levantará los vehículos simulados y configurará sus respectivos puertos de conexión MAVLink.*

## 2. Servidor Central e IA (Backend)
Una vez que los drones simulados están en funcionamiento y transmitiendo telemetría, el siguiente paso es iniciar el servidor central que coordina la flota y procesa la Inteligencia Artificial.
- En un entorno con Python instalado y las dependencias necesarias configuradas (recomendado usar un entorno virtual), dirígete al directorio donde se encuentra el backend.
- Ejecuta el servidor central:
  ```bash
  python server.py
  ```
  *Nota: El servidor establecerá conexión con los drones, ejecutará el modelo YOLO para el procesamiento de imágenes e iniciará el servidor WebSocket esperando conexiones de clientes.*

## 3. Gemelo Digital y UI (Frontend - Unity)
El último paso es arrancar la interfaz gráfica y el Gemelo Digital desarrollado en Unity.
- Abre el proyecto de Unity (carpeta `App`).
- Dirígete a la escena principal de la aplicación.
- Pulsa el botón de **Play** en el editor de Unity.
  *Nota: El cliente de Unity se conectará automáticamente al servidor a través de WebSockets, y comenzará a representar en tiempo real la telemetría del enjambre, el estado de las baterías, las rutas de barrido y las detecciones (personas o animales) enviadas por la Inteligencia Artificial.*
