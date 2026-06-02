# Proyecto: ScreenSafe Area Manager (MVP)

## 1. Contexto

Se requiere desarrollar una utilidad para Windows que permita compensar una zona defectuosa de una pantalla LCD.

El caso de uso inicial corresponde a un equipo All-in-One con Windows 8 cuya pantalla presenta una distorsión permanente en la franja inferior. Debido a ello, elementos críticos como la barra de tareas y partes de ventanas maximizadas quedan parcialmente ocultos o inutilizables.

La solución NO debe intentar reparar el hardware ni modificar controladores gráficos.

La solución debe reservar una franja configurable de la pantalla para que Windows y las aplicaciones no utilicen dicha zona.

---

## 2. Objetivo del MVP

Desarrollar una aplicación mínima funcional capaz de:

* Detectar la resolución actual de la pantalla principal.
* Reservar una franja inferior configurable.
* Aplicar el cambio mediante APIs nativas de Windows.
* Restaurar el área de trabajo original.
* Persistir configuración local.
* Ejecutarse mediante línea de comandos.

No se requiere interfaz gráfica en esta fase.

---

## 3. Enfoque de Desarrollo

El proyecto debe seguir:

* Specification Driven Development (SDD)
* Test Driven Development (TDD)

Antes de implementar:

1. Elaborar especificación funcional detallada.
2. Elaborar diseño arquitectónico.
3. Identificar bounded contexts y componentes.
4. Definir estrategia de pruebas.
5. Presentar propuesta para aprobación.

La implementación solo debe comenzar después de la aprobación del diseño.

---

## 4. Plataforma Tecnológica

### Lenguaje

C#

### Runtime

.NET 8

### Tipo de aplicación

Console Application

### Sistema operativo objetivo

Windows

### Entorno de desarrollo

Linux (Ubuntu)

### Entorno de validación

Máquinas virtuales Windows 8.1 y Windows 10

---

## 5. Alcance del MVP

Incluido:

* Detección de resolución.
* Reserva de área inferior.
* Restauración.
* Persistencia JSON.
* Configuración por archivo.
* Logs básicos.
* Ejecución por CLI.

Excluido:

* GUI.
* Icono en bandeja.
* Multimonitor.
* Instalador.
* Actualizaciones automáticas.
* Servicio Windows.

---

## 6. Requerimientos Funcionales

### RF-001 Detectar resolución

El sistema deberá detectar:

* Ancho actual.
* Alto actual.

de la pantalla principal.

---

### RF-002 Reservar área inferior

El sistema deberá permitir reservar una franja inferior configurable.

Parámetro:

reservedBottomPixels

Ejemplo:

80

---

### RF-003 Aplicar área de trabajo

El sistema deberá modificar el Work Area del sistema operativo utilizando APIs nativas de Windows.

Resultado esperado:

Las ventanas maximizadas deberán respetar la nueva área disponible.

---

### RF-004 Restaurar configuración

El sistema deberá poder restaurar el Work Area original.

---

### RF-005 Persistencia

La configuración deberá almacenarse localmente.

Formato:

JSON

---

### RF-006 Activación mediante CLI

Comandos mínimos:

apply

restore

status

---

### RF-007 Estado

El sistema deberá informar:

* Resolución detectada.
* Área actual.
* Configuración activa.
* Valor reservado.

---

## 7. Requerimientos No Funcionales

### RNF-001

Consumo de memoria inferior a 50 MB.

---

### RNF-002

Tiempo de ejecución inferior a 3 segundos.

---

### RNF-003

No requerir acceso a Internet.

---

### RNF-004

No almacenar información personal.

---

### RNF-005

No requerir privilegios administrativos cuando sea técnicamente posible.

---

## 8. Casos de Uso

### CU-001 Aplicar protección

Dado:

Configuración válida.

Cuando:

El usuario ejecuta:

screensafe apply

Entonces:

El sistema aplica la reserva configurada.

---

### CU-002 Consultar estado

Cuando:

El usuario ejecuta:

screensafe status

Entonces:

Obtiene el estado actual.

---

### CU-003 Restaurar

Cuando:

El usuario ejecuta:

screensafe restore

Entonces:

Se restablece el Work Area original.

---

## 9. Arquitectura Deseada

Se espera que el agente proponga una arquitectura limpia.

Referencia sugerida:

Domain
Application
Infrastructure
Presentation

Dependencias dirigidas hacia el dominio.

---

## 10. Persistencia

Archivo:

appsettings.json

Ejemplo:

{
"enabled": true,
"reservedBottomPixels": 80
}

---

## 11. Adaptadores de Infraestructura Esperados

### WindowsWorkAreaAdapter

Responsable de:

* Obtener resolución.
* Obtener Work Area.
* Aplicar Work Area.
* Restaurar Work Area.

---

### JsonSettingsRepository

Responsable de:

* Leer configuración.
* Persistir configuración.

---

### Logger

Responsable de:

* Registro básico de eventos.

---

## 12. Estrategia TDD Esperada

El agente deberá identificar:

### Unit Tests

* Cálculo de área disponible.
* Validación de configuración.
* Casos límite.

### Integration Tests

* Persistencia JSON.
* Adaptadores de infraestructura.

### Acceptance Tests

* Aplicar configuración.
* Restaurar configuración.
* Consultar estado.

---

## 13. Riesgos Técnicos a Investigar

1. Compatibilidad .NET 8 con Windows 8.1.
2. Restricciones de SystemParametersInfo.
3. Permisos requeridos.
4. Persistencia del Work Area entre reinicios.
5. Comportamiento en sesiones RDP.
6. Diferencias entre Windows 8.1 y Windows 10.

---

## 14. Entregables Esperados del Agente

Fase 1:

* Especificación refinada.
* Diseño arquitectónico.
* Diagrama de componentes.
* Modelo de dominio.
* Estrategia TDD.
* Backlog técnico.

Fase 2 (solo tras aprobación):

* Implementación.
* Tests automatizados.
* Instrucciones de compilación.
* Instrucciones de ejecución.
* Evidencia de pruebas.

---

## 15. Criterio de Éxito del MVP

Se considerará exitoso cuando:

1. El usuario configure 80 píxeles reservados.
2. Ejecute el comando apply.
3. Las ventanas maximizadas respeten la nueva área.
4. El comando restore revierta el cambio.
5. Todos los tests definidos pasen satisfactoriamente.
