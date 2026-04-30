Logitech Steering Wheel SDK — instrucciones de instalación
============================================================

El archivo `LogitechSteeringWheelEnginesWrapper.dll` (Windows x64) NO está incluido
en el repo porque es propietario de Logitech y su redistribución requiere licencia.

Para habilitar el force feedback en colisiones:

1. Ir a https://www.logitechg.com/en-us/innovation/developer-lab.html
   (o buscar "Logitech G Steering Wheel SDK" — registro gratuito requerido)

2. Bajar el SDK ZIP. Adentro buscar:
   `<sdk>/Lib/x64/LogitechSteeringWheelEnginesWrapper.dll`

3. Copiar SOLO esa DLL a este folder:
   `Assets/Plugins/x86_64/LogitechSteeringWheelEnginesWrapper.dll`

4. Abrir Unity. El meta ya está pre-configurado para Windows-only x64.
   Verificar en Inspector:
   - Include Platforms → Standalone (Windows x86_64) ✓
   - Excluded de Editor en Mac (evita errores P/Invoke al abrir el editor en Mac).

5. Build y verificar en kiosko Windows con G923 conectado:
   - Console debe mostrar `[LogitechFFB] SDK inicializado.`
   - Chocar → wheel debe golpear + vibrar ~250ms.

Si el archivo NO está presente:
- LogitechFFB.TryInitialize() captura DllNotFoundException silenciosamente.
- Console muestra `[LogitechFFB] DLL no presente en Plugins/x86_64. FFB deshabilitado.`
- El resto del feedback (overlay/shake/flash/audio) sigue funcionando.
- HORI Truck no usa este SDK — siempre opera sin FFB.
