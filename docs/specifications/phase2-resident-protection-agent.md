# Phase 2 – Resident Protection Agent

## 1. Contexto

La fase MVP validó exitosamente el uso del Work Area de Windows 8.1 para reservar una franja inferior de pantalla.

Resultado validado:

* Las ventanas maximizadas respetan el área reservada.
* El usuario puede recuperar espacio utilizable en pantallas con defectos físicos localizados.

Durante las pruebas se identificaron limitaciones que requieren una nueva fase de desarrollo.

---

## 2. Hallazgos Confirmados

### H-001

La reserva de área se pierde cuando Windows recalcula el Work Area.

Ejemplos observados:

* Cambio de posición de la barra de tareas.
* Reinicio de Explorer.
* Cambios de configuración de pantalla.

---

### H-002

Las aplicaciones en modo fullscreen o immersive no respetan el Work Area.

Ejemplos observados:

* Cámara.
* Microsoft Store.

---

## 3. Objetivo

Evolucionar la solución desde un comando puntual hacia un agente residente capaz de mantener automáticamente la reserva de pantalla.

---

## 4. Alcance

Incluido:

* Monitorización continua.
* Reaplicación automática.
* Inicio automático con Windows.
* Notificación de estado.
* Registro de eventos.

Excluido:

* Modificación de aplicaciones fullscreen.
* Intercepción de ventanas.
* Hooking.
* Inyección de código.
* Manipulación de GPU o drivers.

---

## 5. Requerimientos Funcionales

### RF-101

El sistema deberá ejecutarse en segundo plano.

---

### RF-102

El sistema deberá detectar modificaciones del Work Area.

---

### RF-103

Cuando el Work Area difiera de la configuración deseada, el sistema deberá reaplicar automáticamente la reserva.

---

### RF-104

El sistema deberá detectar cambios de resolución.

---

### RF-105

El sistema deberá recalcular automáticamente el área reservada tras cambios de resolución.

---

### RF-106

El sistema deberá iniciar automáticamente con Windows.

---

### RF-107

El sistema deberá exponer su estado actual.

---

### RF-108

El sistema deberá registrar eventos relevantes.

---

## 6. Requerimientos No Funcionales

### RNF-101

Uso de CPU inferior al 1%.

---

### RNF-102

Uso de memoria inferior a 50 MB.

---

### RNF-103

Tiempo de recuperación inferior a 2 segundos después de un cambio detectado.

---

## 7. Eventos a Investigar

El diseño deberá evaluar:

* WM_SETTINGCHANGE
* WM_DISPLAYCHANGE
* TaskbarCreated
* Explorer restart detection

No asumir implementación hasta completar investigación.

---

## 8. Restricciones

El sistema no garantizará compatibilidad con:

* Aplicaciones fullscreen.
* Juegos.
* Aplicaciones immersive.
* Aplicaciones que ignoren deliberadamente el Work Area.

---

## 9. Criterio de Éxito

Se considerará exitosa la fase cuando:

1. La reserva permanezca activa tras mover la barra de tareas.
2. La reserva permanezca activa tras reiniciar Explorer.
3. La reserva permanezca activa tras cambios de resolución.
4. No sea necesaria intervención manual del usuario.
