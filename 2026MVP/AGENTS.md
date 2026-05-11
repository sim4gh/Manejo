# Tlax2026MVP

Proyecto Unity del simulador de examen de manejo. Antes de editar, asumir que aquí conviven código propio y assets de terceros.

## No tocar

No modificar assets comprados salvo que el usuario lo pida explícitamente y el cambio esté bien justificado:

- `Assets/Realistic Car Controller Pro/`
- `Assets/Gley/`
- `Assets/EasyRoads3D/`
- `Assets/EasyRoads3Dv3/`
- `Assets/Fantasy Skybox FREE/`

## Stack y restricciones

- Unity 6 `6000.3.5f2`
- URP
- Input System nuevo solamente
- Build target Windows

No introducir APIs del input legacy si el código actual usa Input System.

## Áreas clave del código propio

- `Assets/Custom/`: gameplay, UI, diagnósticos, kiosko, logging.
- `Assets/Custom/Menu/`: flujo principal de verificación e inicio de examen.
- `Assets/pruebasQR/`: QR y sesiones de kiosko.
- `scripts/`: utilidades de build e instalación.

## Operación de builds

Hay dos flujos distintos heredados de Claude y conviene no mezclarlos:

- Empaquetado LAN local: `../../.claude/skills/package-unity-build/SKILL.md`
- Publicación al portal admin y deploy OTA: `../../.claude/skills/publish-unity-build/SKILL.md`

Diferencia crítica:

- El zip LAN lleva wrapper `Tlax2026MVP/` como raíz.
- El zip para el portal admin va con archivos al root del zip.

## Reglas prácticas

- Si el usuario pide publicar una versión, verificar primero si Unity está abierto antes de batch build.
- No matar Unity a la fuerza sin permiso explícito del usuario.
- Si trabajas en OTA, heartbeat o update status, preservar la lógica de confirmación honesta de instalaciones.
- Si tocas logging o diagnósticos, cuidar tanto `LogConsolePanel` como `LogUploader`; se usan para soporte en kioskos reales.

## Referencia extendida

Para arquitectura, overlays F7-F10, QR, NPCs, OTA y flujo del simulador, ver `Manejo/2026MVP/CLAUDE.md`.
