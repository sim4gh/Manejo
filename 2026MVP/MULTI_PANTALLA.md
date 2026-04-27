# Configuración de 3 Pantallas - Simulador de Manejo

## Estado actual

| Cámara | Target Display | Rotación Y | Estado |
|--------|---------------|------------|--------|
| `Camara_Centro` | Display 1 (0) | 0° | Correcto |
| `Camara_Izquierda` | Display 2 (1) | -44.6° | Correcto |
| `Camara_Derecha` | Display 1 (0) | +22.2° | 2 errores |

`MultiPantallaManager.cs` ya activa Display 2 y 3 — no necesita cambios.

## Instrucciones en Unity Editor

### Paso 1: Abrir la escena

Abrir `Assets/Gley/UrbanExample/Samples/UrbanExample.unity`

### Paso 2: Seleccionar Camara_Derecha

En la Hierarchy, buscar `Camara_Derecha` (está como hijo del vehículo Player).

### Paso 3: Cambiar Target Display

En el Inspector, componente **Camera**, campo **Target Display**:

- Cambiar de **"Display 1"** a **"Display 3"**

(Unity muestra Display 1/2/3 en la UI, que corresponden a índices 0/1/2 en código)

### Paso 4: Corregir rotación

En el Inspector, componente **Transform**, campo **Rotation**:

- Cambiar **Y** de `22.2` a `44.6`

(Simétrico con `Camara_Izquierda` que tiene -44.6°)

### Paso 5: Guardar escena

`Ctrl+S` para guardar.

## Verificación

1. Conectar 3 monitores al PC
2. En Unity: **Edit > Project Settings > Player** — verificar que "Use Exclusive Fullscreen" no esté marcado
3. Hacer Build o Play en Editor
4. Confirmar que cada monitor muestra una vista diferente:
   - Monitor 1: vista frontal (centro)
   - Monitor 2: vista izquierda (~45° izq)
   - Monitor 3: vista derecha (~45° der)

**Nota:** Si en Play Mode solo se ve un monitor, es normal — `Display.displays[]` solo reporta múltiples displays en un **Build standalone**, no en el Editor.
