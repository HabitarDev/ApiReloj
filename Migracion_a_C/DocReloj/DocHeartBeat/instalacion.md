# Guía de Instalación - DeviceHeartbeatService

Esta guía explica paso a paso cómo instalar DeviceHeartbeatService como un servicio de Windows.

## 📋 Tabla de Contenidos

1. [Requisitos Previos](#requisitos-previos)
2. [Preparación del Proyecto](#preparación-del-proyecto)
3. [Configuración](#configuración)
4. [Publicación del Servicio](#publicación-del-servicio)
5. [Instalación como Servicio de Windows](#instalación-como-servicio-de-windows)
6. [Verificación](#verificación)
7. [Gestión del Servicio](#gestión-del-servicio)
8. [Desinstalación](#desinstalación)
9. [Solución de Problemas](#solución-de-problemas)

---

## Requisitos Previos

Antes de comenzar, asegúrate de tener:

- ✅ **Windows 10/11 o Windows Server 2016+**
- ✅ **.NET 8 SDK** instalado (solo necesario para compilar)
- ✅ **Permisos de Administrador** en el equipo donde se instalará
- ✅ **Acceso al backend** donde se enviarán los heartbeats
- ✅ **Clave secreta (SecretKey)** compartida con el backend

---

## Preparación del Proyecto

### Paso 1: Clonar o Descargar el Proyecto

Si tienes el código fuente:

```bash
cd C:\Projects
git clone <repository-url>
cd WindowsClockService
```

O descarga y extrae el proyecto en una ubicación accesible.

### Paso 2: Navegar al Directorio del Proyecto

```bash
cd DeviceHeartbeatService
```

---

## Configuración

### Paso 3: Configurar appsettings.json

Abre el archivo `appsettings.json` y modifica los siguientes valores:

```json
{
  "Device": {
    "SecretKey": "TU_CLAVE_SECRETA_AQUI",
    "DeviceId": "cm02abcdef1234567890xyz",
    "ResidentialId": "cm01abcdef1234567890xyz",
    "HeartbeatUrl": "http://localhost:5000/Residential/heartbeat",
    "IntervalSeconds": 30
  }
}
```

#### Valores que DEBES modificar:

| Parámetro           | Descripción                                                                                       | Ejemplo                                  |
| ------------------- | ------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| **SecretKey**       | ⚠️ **OBLIGATORIO** - Clave secreta para generar la firma HMAC. Debe coincidir con la del backend. | `"mi_clave_super_secreta_123456"`        |
| **DeviceId**        | Identificador único del dispositivo (**string**, mismo valor que en ApiReloj/HABITAR).           | `"cm02abcdef1234567890xyz"`             |
| **ResidentialId**   | Identificador del residencial (**string**, mismo valor que en ApiReloj/HABITAR).                  | `"cm01abcdef1234567890xyz"`             |
| **HeartbeatUrl**    | URL completa del endpoint que recibe los heartbeats (ApiReloj: **`POST /Residential/heartbeat`**). | `"https://api.miservidor.com/Residential/heartbeat"` |
| **IntervalSeconds** | Intervalo en segundos entre cada heartbeat (opcional, por defecto 30).                            | `30`                                     |

#### Ejemplo de configuración real:

```json
{
  "Device": {
    "SecretKey": "a8f5f167f44f4964e6c998dee827110c",
    "DeviceId": "cm02abcdef1234567890xyz",
    "ResidentialId": "cm01abcdef1234567890xyz",
    "HeartbeatUrl": "https://backend.midominio.com/Residential/heartbeat",
    "IntervalSeconds": 30
  }
}
```

**⚠️ IMPORTANTE**:

- **NUNCA** compartas tu `SecretKey` públicamente
- Asegúrate de que `SecretKey` coincida exactamente con la del backend
- Verifica que `HeartbeatUrl` sea accesible desde el servidor donde se instalará

---

## Publicación del Servicio

### Paso 4: Publicar el Servicio

Ejecuta el siguiente comando para generar un ejecutable autocontenido:

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

Este comando:

- Compila el proyecto en modo Release (optimizado)
- Genera un ejecutable para Windows x64
- Incluye todas las dependencias de .NET (no requiere .NET Runtime instalado)

### Paso 5: Localizar los Archivos Publicados

Los archivos se generarán en:

```
DeviceHeartbeatService\bin\Release\net8.0\win-x64\publish\
```

En esta carpeta encontrarás:

- `DeviceHeartbeatService.exe` - El ejecutable principal
- `appsettings.json` - Archivo de configuración (si está configurado para copiarse)
- Varios archivos `.dll` - Dependencias necesarias

**Nota**: Si `appsettings.json` no está en la carpeta `publish`, deberás copiarlo manualmente.

### Paso 6: Preparar Carpeta de Instalación

Crea una carpeta permanente para el servicio (no uses carpetas temporales):

```cmd
mkdir C:\Services\DeviceHeartbeatService
```

Copia **TODOS** los archivos de la carpeta `publish` a la carpeta de instalación:

```cmd
xcopy /E /I "DeviceHeartbeatService\bin\Release\net8.0\win-x64\publish\*" "C:\Services\DeviceHeartbeatService\"
```

O manualmente usando el Explorador de Windows:

1. Selecciona todos los archivos en `publish`
2. Cópialos a `C:\Services\DeviceHeartbeatService\`

### Paso 7: Verificar Archivos en Carpeta de Instalación

Asegúrate de que en `C:\Services\DeviceHeartbeatService\` estén:

```
C:\Services\DeviceHeartbeatService\
├── DeviceHeartbeatService.exe    ✅ DEBE ESTAR
├── appsettings.json              ✅ DEBE ESTAR (con tu configuración)
├── DeviceHeartbeatService.dll
├── Microsoft.Extensions.*.dll
└── ... (otros archivos .dll necesarios)
```

**⚠️ CRÍTICO**:

- `appsettings.json` **DEBE** estar en la misma carpeta que `DeviceHeartbeatService.exe`
- Verifica que `appsettings.json` tenga los valores correctos antes de continuar

---

## Instalación como Servicio de Windows

### Paso 8: Abrir Símbolo del Sistema como Administrador

1. Presiona `Windows + X`
2. Selecciona **"Terminal (Admin)"** o **"Símbolo del sistema (Administrador)"**
3. Si aparece UAC, confirma con **"Sí"**

### Paso 9: Instalar el Servicio

Ejecuta el siguiente comando (ajusta la ruta si usaste otra carpeta):

```cmd
sc create DeviceHeartbeatService binPath= "C:\Services\DeviceHeartbeatService\DeviceHeartbeatService.exe" start= auto
```

Este comando:

- Crea el servicio con nombre `DeviceHeartbeatService`
- Especifica la ruta al ejecutable
- Configura el inicio automático (`start= auto`)

**⚠️ IMPORTANTE**:

- Debe haber un **espacio después de `binPath=`** (antes de las comillas)
- La ruta debe estar entre comillas si contiene espacios
- Si la instalación es exitosa, verás: `[SC] CreateService SUCCESS`

### Paso 10: Configurar Descripción del Servicio (Opcional)

Para agregar una descripción descriptiva:

```cmd
sc description DeviceHeartbeatService "Servicio que envía heartbeats periódicos al backend con autenticación HMAC-SHA256"
```

### Paso 11: Iniciar el Servicio

```cmd
sc start DeviceHeartbeatService
```

Si todo está bien, verás:

```
SERVICE_NAME: DeviceHeartbeatService
        TYPE               : 10  WIN32_OWN_PROCESS
        STATE              : 2  START_PENDING
                                (NOT_STOPPABLE, NOT_PAUSABLE, IGNORES_SHUTDOWN)
        ...
```

Espera unos segundos y verifica el estado:

```cmd
sc query DeviceHeartbeatService
```

Deberías ver:

```
STATE              : 4  RUNNING
```

---

## Verificación

### Paso 12: Verificar que el Servicio Está Corriendo

#### Método 1: Usando sc query

```cmd
sc query DeviceHeartbeatService
```

El estado debe ser `RUNNING`.

#### Método 2: Usando Services.msc

1. Presiona `Windows + R`
2. Escribe `services.msc` y presiona Enter
3. Busca **"DeviceHeartbeatService"**
4. El estado debe ser **"En ejecución"**

#### Método 3: Usando PowerShell

```powershell
Get-Service DeviceHeartbeatService
```

### Paso 13: Verificar los Logs

#### Ver logs en archivo de texto:

```cmd
type C:\ProgramData\DeviceHeartbeatService\logs\service.log
```

O con PowerShell:

```powershell
Get-Content C:\ProgramData\DeviceHeartbeatService\logs\service.log -Tail 20
```

Deberías ver entradas como:

```
2025-01-10T10:30:00Z [INFO] Worker initialized. DeviceId: cm02abcdef1234567890xyz, ResidentialId: cm01abcdef1234567890xyz, Interval: 30s
2025-01-10T10:30:00Z [INFO] Heartbeat sent successfully
2025-01-10T10:30:30Z [INFO] Heartbeat sent successfully
```

#### Ver logs en Visor de Eventos:

1. Presiona `Windows + R`
2. Escribe `eventvwr.exe` y presiona Enter
3. Navega a **"Registros de Windows" → "Aplicación"**
4. Busca eventos de origen **"DeviceHeartbeatService"**

### Paso 14: Verificar Comunicación con el Backend

1. Revisa los logs del backend para confirmar que está recibiendo los heartbeats
2. Verifica que no haya errores de red en los logs del servicio
3. Si usas un proxy o firewall, asegúrate de que permita conexiones al `HeartbeatUrl`

---

## Gestión del Servicio

### Detener el Servicio

```cmd
sc stop DeviceHeartbeatService
```

### Iniciar el Servicio

```cmd
sc start DeviceHeartbeatService
```

### Reiniciar el Servicio

```cmd
sc stop DeviceHeartbeatService
timeout /t 5
sc start DeviceHeartbeatService
```

### Ver Estado del Servicio

```cmd
sc query DeviceHeartbeatService
```

### Cambiar Tipo de Inicio

#### Inicio Automático (recomendado):

```cmd
sc config DeviceHeartbeatService start= auto
```

#### Inicio Manual:

```cmd
sc config DeviceHeartbeatService start= demand
```

#### Deshabilitado:

```cmd
sc config DeviceHeartbeatService start= disabled
```

**⚠️ NOTA**: Debe haber un **espacio después de `start=`**

### Modificar Configuración (appsettings.json)

Si necesitas cambiar la configuración:

1. **Detener el servicio**:

   ```cmd
   sc stop DeviceHeartbeatService
   ```

2. **Editar** `C:\Services\DeviceHeartbeatService\appsettings.json`

3. **Iniciar el servicio**:
   ```cmd
   sc start DeviceHeartbeatService
   ```

**⚠️ IMPORTANTE**: Siempre detén el servicio antes de modificar `appsettings.json` para evitar problemas de lectura.

---

## Desinstalación

### Paso 1: Detener el Servicio

```cmd
sc stop DeviceHeartbeatService
```

Espera a que el estado cambie a `STOPPED`:

```cmd
sc query DeviceHeartbeatService
```

### Paso 2: Eliminar el Servicio

```cmd
sc delete DeviceHeartbeatService
```

Si es exitoso, verás: `[SC] DeleteService SUCCESS`

### Paso 3: Eliminar Archivos (Opcional)

Si deseas eliminar completamente todos los archivos:

```cmd
rmdir /S /Q "C:\Services\DeviceHeartbeatService"
rmdir /S /Q "C:\ProgramData\DeviceHeartbeatService"
```

**⚠️ ADVERTENCIA**: Esto eliminará:

- Todos los archivos del servicio
- Todos los logs históricos
- La configuración

---

## Solución de Problemas

### El servicio no inicia

**Síntoma**: `sc start` falla o el servicio se detiene inmediatamente.

**Soluciones**:

1. **Verificar que el ejecutable existe**:

   ```cmd
   dir "C:\Services\DeviceHeartbeatService\DeviceHeartbeatService.exe"
   ```

2. **Verificar que appsettings.json existe**:

   ```cmd
   dir "C:\Services\DeviceHeartbeatService\appsettings.json"
   ```

3. **Verificar logs del sistema**:

   - Abre `eventvwr.exe`
   - Revisa **"Registros de Windows" → "Sistema"** para errores

4. **Ejecutar manualmente el .exe**:

   ```cmd
   cd C:\Services\DeviceHeartbeatService
   DeviceHeartbeatService.exe
   ```

   Esto mostrará errores en la consola que pueden ayudar a diagnosticar.

5. **Verificar permisos**: Asegúrate de que el servicio tenga permisos de lectura en la carpeta.

### Error: "El servicio no pudo iniciarse"

**Causa común**: `appsettings.json` faltante o mal formateado.

**Solución**:

1. Verifica que `appsettings.json` está en la misma carpeta que el `.exe`
2. Valida el JSON usando un validador online
3. Verifica que no falten comillas o llaves

### Error: "El servicio se detiene automáticamente"

**Causa común**: Excepción no manejada al iniciar.

**Solución**:

1. Revisa el archivo de log: `C:\ProgramData\DeviceHeartbeatService\logs\service.log`
2. Verifica que todos los valores en `appsettings.json` son válidos
3. Verifica que `SecretKey` no esté vacío
4. Verifica que `HeartbeatUrl` sea una URL válida

### Error de conexión al backend

**Síntoma**: Los logs muestran errores de red repetidos.

**Soluciones**:

1. **Verificar conectividad**:

   ```cmd
   ping api.tuservidor.com
   ```

2. **Probar la URL manualmente**:

   ```powershell
   Invoke-WebRequest -Uri "https://api.tuservidor.com/Residential/heartbeat" -Method POST
   ```

3. **Verificar firewall**:

   - Abre el Firewall de Windows
   - Permite conexiones salientes para `DeviceHeartbeatService.exe`

4. **Verificar proxy**: Si estás detrás de un proxy corporativo, puede necesitar configuración adicional.

### Firma HMAC inválida

**Síntoma**: El backend rechaza los heartbeats con error de autenticación.

**Soluciones**:

1. **Verificar SecretKey**: Asegúrate de que `SecretKey` en `appsettings.json` coincide **exactamente** con la del backend
2. **Verificar formato**: No debe tener espacios extra al inicio o final
3. **Verificar encoding**: Asegúrate de que el archivo está guardado en UTF-8
4. **Verificar string firmado exacto**: debe ser `{timeStamp}|{deviceId}|{residentialId}`
5. **Verificar tipos JSON**: `deviceId` y `residentialId` como string, `timeStamp` como number

### El servicio no escribe logs

**Síntoma**: No existe el archivo `service.log`.

**Soluciones**:

1. **Verificar permisos en ProgramData**:

   ```cmd
   icacls "C:\ProgramData\DeviceHeartbeatService"
   ```

2. **Crear carpeta manualmente**:

   ```cmd
   mkdir "C:\ProgramData\DeviceHeartbeatService\logs"
   ```

3. **Verificar espacio en disco**: Asegúrate de que hay espacio disponible

### El servicio consume mucha CPU/Memoria

**Causa común**: Loop infinito o memory leak.

**Soluciones**:

1. **Verificar logs** para errores repetidos
2. **Verificar IntervalSeconds**: Asegúrate de que es un valor razonable (30-60 segundos)
3. **Reiniciar el servicio** periódicamente si es necesario

---

## Comandos de Referencia Rápida

```cmd
# Instalar
sc create DeviceHeartbeatService binPath= "C:\Services\DeviceHeartbeatService\DeviceHeartbeatService.exe" start= auto

# Iniciar
sc start DeviceHeartbeatService

# Detener
sc stop DeviceHeartbeatService

# Estado
sc query DeviceHeartbeatService

# Reiniciar
sc stop DeviceHeartbeatService && timeout /t 2 && sc start DeviceHeartbeatService

# Desinstalar
sc stop DeviceHeartbeatService
sc delete DeviceHeartbeatService

# Ver logs
type C:\ProgramData\DeviceHeartbeatService\logs\service.log

# Ver últimos 50 líneas de log
powershell "Get-Content C:\ProgramData\DeviceHeartbeatService\logs\service.log -Tail 50"
```

---

## Checklist de Instalación

Usa este checklist para asegurarte de que completaste todos los pasos:

- [ ] .NET 8 SDK instalado (para compilar)
- [ ] Proyecto clonado/descargado
- [ ] `appsettings.json` configurado con valores correctos
- [ ] `SecretKey` configurado y coincidiendo con el backend
- [ ] Proyecto publicado con `dotnet publish`
- [ ] Archivos copiados a carpeta permanente (ej: `C:\Services\DeviceHeartbeatService\`)
- [ ] `appsettings.json` está en la misma carpeta que el `.exe`
- [ ] Servicio instalado con `sc create`
- [ ] Servicio iniciado con `sc start`
- [ ] Estado verificado como `RUNNING`
- [ ] Logs verificados en `C:\ProgramData\DeviceHeartbeatService\logs\service.log`
- [ ] Backend recibiendo heartbeats correctamente
- [ ] Servicio configurado para inicio automático

---

## Soporte Adicional

Si después de seguir esta guía sigues teniendo problemas:

1. Revisa los logs detallados en `C:\ProgramData\DeviceHeartbeatService\logs\service.log`
2. Revisa el Visor de Eventos de Windows para errores del sistema
3. Verifica la documentación en `README.md` para detalles técnicos
4. Ejecuta el servicio manualmente para ver errores en consola

---

**Última actualización**: Mayo 2026
