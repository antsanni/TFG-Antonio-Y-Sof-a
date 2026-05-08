echo "--- LANZADOR DE DRONES ---"

while true; do
    echo "Introduce el numero de drones que quieres utilizar:"
    read NUM_DRONES

    # Comprobamos que sea un número (sin letras) y que sea mayor que 0
    if [[ "$NUM_DRONES" =~ ^[0-9]+$ ]] && [ "$NUM_DRONES" -gt 0 ]; then
        break # ¡El número es válido! Salimos del bucle y continuamos
    else
        echo "❌ Error: Por favor, introduce un número entero mayor que 0."
        echo "----------------------------------------"
    fi
done

# Limpiamos si habia algo
pkill -f sim_vehicle.py
pkill -f arducopter

cd ~/ardupilot || exit 1

for (( i=0; i<NUM_DRONES; i++ ))
do
    puerto=$((5760 + 10 * i))

    echo " -> Dron $i listo (En puerto $puerto)"

    # Comando que pone todo en marcha en las coordenadas exactas que quieres
    DISPLAY="" nohup python3 Tools/autotest/sim_vehicle.py -v ArduCopter -f quad --no-mavproxy --instance $i --sysid $(($i+1)) --custom-location=28.760784,-17.747523,0,0 > /dev/null 2>&1 &

    sleep 5
done

echo "----------------------------------------"
echo "¡Drones listos!"
echo "----------------------------------------"