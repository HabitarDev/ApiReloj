# DeviceHeartbeatService

Windows Service desarrollado en .NET 8 que envía heartbeats periódicos a un backend con autenticación HMAC-SHA256.

## 📋 Descripción

DeviceHeartbeatService es un servicio de Windows que se ejecuta en segundo plano y envía señales de vida (heartbeats) cada 30 segundos (configurable) a un servidor backend. Cada mensaje incluye una firma HMAC-SHA256 para garantizar la autenticidad e integridad de los datos.

### Características principales

- ✅ Envío automático de heartbeats cada 30 segundos (configurable)
- ✅ Firma HMAC-SHA256 para seguridad
- ✅ Configuración mediante `appsettings.json`
- ✅ Manejo robusto de errores sin detener el servicio
- ✅ Reintentos automáticos en cada intervalo
- ✅ Logging completo a archivo y Visor de Eventos
- ✅ Sistema de logging thread-safe a archivo de texto
- ✅ Diseñado como Windows Service nativo

## 🔒 Seguridad HMAC-SHA256

El servicio utiliza HMAC-SHA256 para firmar cada heartbeat, garantizando que:

1. **Autenticidad**: El backend puede verificar que el mensaje proviene de un dispositivo autorizado
2. **Integridad**: Cualquier modificación del mensaje invalidará la firma
3. **No repudio**: La firma solo puede generarse con la clave secreta conocida

### Algoritmo de firma

```
signature = HMAC_SHA256(secretKey, "{timeStamp}|{deviceId}|{residentialId}")
```

Los valores `deviceId` y `residentialId` son **strings** (mismos que persisten ApiReloj / HABITAR). La cadena UTF-8 concatenada usa exactamente esos caracteres, sin conversion numerica intermedia.

La firma se genera combinando el timestamp (como numero en texto decimal), deviceId y residentialId con un pipe (`|`) como separador, y luego aplicando HMAC-SHA256 con la clave secreta configurada.

El endpoint en ApiReloj es **`POST /Residential/heartbeat`** (no `/heartbeat` solo).

## ⚙️ Configuración

Toda la configuración se realiza mediante el archivo `appsettings.json`:

```json
{
  "Device": {
    "SecretKey": "PONER_VALOR",
    "DeviceId": "cm02abcdef1234567890xyz",
    "ResidentialId": "cm01abcdef1234567890xyz",
    "HeartbeatUrl": "http://localhost:5000/Residential/heartbeat",
    "IntervalSeconds": 30
  }
}
```

### Parámetros de configuración

| Parámetro         | Descripción                              | Ejemplo                             |
| ----------------- | ---------------------------------------- | ----------------------------------- |
| `SecretKey`       | Clave secreta para generar la firma HMAC | `"mi_clave_secreta_12345"`          |
| `DeviceId`        | Identificador único del dispositivo (string, mismo que BD) | `"cm02abcdef1234567890xyz"` |
| `ResidentialId`   | Identificador del residencial (string, mismo que BD)       | `"cm01abcdef1234567890xyz"` |
| `HeartbeatUrl`    | URL completa del endpoint de heartbeat                     | `"http://localhost:5000/Residential/heartbeat"` |
| `IntervalSeconds` | Intervalo en segundos entre heartbeats   | `30`                                |

**⚠️ IMPORTANTE**: Antes de instalar el servicio, asegúrate de actualizar el valor de `SecretKey` en `appsettings.json` con una clave secreta segura.

## 📦 Formato del Heartbeat

Cada heartbeat se envía como una petición POST con el siguiente formato JSON:

```json
{
  "deviceId": "cm02abcdef1234567890xyz",
  "residentialId": "cm01abcdef1234567890xyz",
  "timeStamp": 1733791220,
  "signature": "a1b2c3d4e5f6..."
}
```

### Campos del mensaje

- **deviceId**: Identificador del dispositivo (desde configuración)
- **residentialId**: Identificador de la residencia (desde configuración)
- **timeStamp**: Timestamp Unix (segundos desde epoch UTC)
- **signature**: Firma HMAC-SHA256 en hexadecimal minúsculas

## 📝 Sistema de Logging

El servicio incluye un sistema de logging a archivo que registra todas las operaciones importantes del servicio en un archivo de texto plano.

### Ubicación del archivo de log

Los logs se escriben en:

```
C:\ProgramData\DeviceHeartbeatService\logs\service.log
```

Esta ubicación en `ProgramData` es ideal para servicios de Windows porque:

- **Permisos adecuados**: Los servicios de Windows tienen acceso de escritura a esta carpeta
- **Persistencia**: Los logs se mantienen incluso después de actualizaciones del servicio
- **Acceso centralizado**: Facilita la administración y monitoreo de logs

### Formato de los logs

Cada entrada en el archivo de log sigue este formato:

```
{timeStampIsoUtc} [{LEVEL}] {mensaje}
```

Donde:

- **timestamp**: Fecha y hora en formato ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`)
- **LEVEL**: Nivel de log (`INFO` o `ERROR`)
- **mensaje**: Mensaje descriptivo del evento

### Ejemplos de logs

#### Log de información (INFO)

```
2025-01-10T03:15:22Z [INFO] Worker initialized. DeviceId: cm02abcdef1234567890xyz, ResidentialId: cm01abcdef1234567890xyz, Interval: 30s
2025-01-10T03:15:22Z [INFO] Heartbeat sent successfully
2025-01-10T03:15:52Z [INFO] Heartbeat sent successfully
```

#### Log de error (ERROR)

```
2025-01-10T03:16:22Z [ERROR] Network error sending heartbeat: System.Net.Http.HttpRequestException - No se puede conectar al servidor remoto
   en System.Net.Http.HttpClientHandler.SendAsync(...)
   en DeviceHeartbeatService.Worker.ExecuteAsync(...)
2025-01-10T03:16:52Z [ERROR] Heartbeat failed. Status: 500, Response: Internal Server Error
```

### Eventos que se registran

El sistema de logging registra los siguientes eventos:

| Evento                         | Nivel | Descripción                                             |
| ------------------------------ | ----- | ------------------------------------------------------- |
| Inicialización del Worker      | INFO  | Cuando el servicio inicia y carga la configuración      |
| Heartbeat enviado exitosamente | INFO  | Cada vez que un heartbeat se envía correctamente        |
| Heartbeat fallido              | ERROR | Cuando el backend responde con error HTTP               |
| Error de red                   | ERROR | Cuando ocurre un error de conexión (timeout, DNS, etc.) |
| Error inesperado               | ERROR | Cualquier excepción no categorizada                     |
| Cancelación de request         | INFO  | Cuando se cancela un request de heartbeat               |

### Características del sistema de logging

- **Thread-safe**: Utiliza `SemaphoreSlim` para garantizar escritura segura desde múltiples threads
- **No bloqueante**: El logging se realiza de forma asíncrona para no afectar el rendimiento
- **Resiliente**: Si el logging falla, el servicio continúa funcionando normalmente
- **Auto-creación de directorios**: Crea automáticamente la carpeta de logs si no existe
- **Encoding UTF-8**: Los logs se escriben en UTF-8 para soportar caracteres especiales

### Ver los logs

#### Método 1: Explorador de Windows

1. Navega a `C:\ProgramData\DeviceHeartbeatService\logs\`
2. Abre `service.log` con el Bloc de notas o cualquier editor de texto

#### Método 2: PowerShell

```powershell
Get-Content C:\ProgramData\DeviceHeartbeatService\logs\service.log -Tail 50
```

#### Método 3: Símbolo del sistema

```cmd
type C:\ProgramData\DeviceHeartbeatService\logs\service.log
```

### Rotación de logs (futuro)

Actualmente, el servicio escribe todos los logs en un único archivo. Para evitar que el archivo crezca indefinidamente, se recomienda:

1. **Monitorear el tamaño del archivo** periódicamente
2. **Configurar un script de limpieza** o rotación manual
3. **Usar herramientas de Windows** como Task Scheduler para archivar logs antiguos

### Deshabilitar el logging a archivo

El sistema de logging a archivo está siempre habilitado y no puede deshabilitarse mediante configuración. Si deseas deshabilitarlo, deberías:

1. Modificar el código para remover las llamadas a `_fileLogger`
2. O comentar el registro del servicio en `Program.cs`

**Nota**: Se recomienda mantener el logging habilitado para facilitar el troubleshooting y auditoría.

### Permisos necesarios

El servicio requiere permisos de escritura en `C:\ProgramData\DeviceHeartbeatService\logs\`. Estos permisos se otorgan automáticamente cuando el servicio se ejecuta con la cuenta de servicio de Windows.

Si encuentras problemas de permisos:

1. Verifica que el servicio tenga permisos de escritura en `ProgramData`
2. Asegúrate de que la carpeta no esté bloqueada por antivirus u otro software
3. Verifica que el disco tenga espacio disponible

## 🏗️ Compilación del Proyecto

### Requisitos previos

- .NET 8 SDK instalado
- Windows (para compilar como Windows Service)

### Compilar el proyecto

```bash
cd DeviceHeartbeatService
dotnet build -c Release
```

## 📤 Publicación del Servicio

Para publicar el servicio como ejecutable autocontenido para Windows:

```bash
cd DeviceHeartbeatService
dotnet publish -c Release -r win-x64 --self-contained true
```

Esto generará el ejecutable en:

```
bin/Release/net8.0/win-x64/publish/DeviceHeartbeatService.exe
```

### Opciones de publicación

- `-c Release`: Compila en modo Release (optimizado)
- `-r win-x64`: Especifica el runtime de Windows x64
- `--self-contained true`: Incluye todas las dependencias de .NET en el ejecutable

## 🖥️ Instalación como Servicio de Windows

### Prerrequisitos

1. Ejecutar como **Administrador** (es necesario para instalar servicios)
2. Tener el ejecutable publicado en una ubicación permanente (ej: `C:\Services\DeviceHeartbeatService\`)
3. Asegurarse de que `appsettings.json` esté en la misma carpeta que el `.exe`

### Pasos de instalación

1. **Copiar archivos necesarios**:

   ```
   C:\Services\DeviceHeartbeatService\
   ├── DeviceHeartbeatService.exe
   └── appsettings.json
   ```

2. **Crear el servicio**:

   ```cmd
   sc create DeviceHeartbeatService binPath= "C:\Services\DeviceHeartbeatService\DeviceHeartbeatService.exe"
   ```

3. **Iniciar el servicio**:

   ```cmd
   sc start DeviceHeartbeatService
   ```

4. **Verificar el estado**:
   ```cmd
   sc query DeviceHeartbeatService
   ```

### Gestión del servicio

#### Detener el servicio

```cmd
sc stop DeviceHeartbeatService
```

#### Reiniciar el servicio

```cmd
sc stop DeviceHeartbeatService
sc start DeviceHeartbeatService
```

#### Desinstalar el servicio

```cmd
sc stop DeviceHeartbeatService
sc delete DeviceHeartbeatService
```

### Configurar inicio automático

Para que el servicio se inicie automáticamente al arrancar Windows:

```cmd
sc config DeviceHeartbeatService start= auto
```

Opciones de inicio:

- `auto`: Inicio automático
- `demand`: Inicio manual
- `disabled`: Deshabilitado

## 🧪 Pruebas y Depuración

### Ejecutar como aplicación de consola

Para probar el servicio sin instalarlo, puedes ejecutarlo directamente:

```bash
cd DeviceHeartbeatService
dotnet run
```

Esto ejecutará el servicio como una aplicación de consola, mostrando los logs en tiempo real. Presiona `Ctrl+C` para detenerlo.

### Verificar logs

El servicio escribe logs en dos ubicaciones:

1. **Archivo de texto**: `C:\ProgramData\DeviceHeartbeatService\logs\service.log`
2. **Visor de Eventos de Windows**:
   - Abre **Visor de eventos** (`eventvwr.exe`)
   - Navega a **Registros de Windows** → **Aplicación**
   - Busca eventos de origen `DeviceHeartbeatService`

### Verificar funcionamiento

Puedes verificar que el servicio está funcionando correctamente:

1. **Revisar logs en archivo de texto** (`C:\ProgramData\DeviceHeartbeatService\logs\service.log`)
2. **Revisar logs en Visor de Eventos**
3. **Monitorear tráfico de red** con herramientas como Wireshark o Fiddler
4. **Revisar logs del backend** para confirmar recepción de heartbeats

### Ejemplo de logs exitosos

**En el archivo de texto** (`service.log`):

```
2025-01-10T03:15:22Z [INFO] Worker initialized. DeviceId: cm02abcdef1234567890xyz, ResidentialId: cm01abcdef1234567890xyz, Interval: 30s
2025-01-10T03:15:22Z [INFO] Heartbeat sent successfully
2025-01-10T03:15:52Z [INFO] Heartbeat sent successfully
```

**En el Visor de Eventos**:

```
[Information] Worker initialized. DeviceId: cm02abcdef1234567890xyz, ResidentialId: cm01abcdef1234567890xyz, Interval: 30s
[Information] Sending heartbeat. TimeStamp: 1733791220, DeviceId: cm02abcdef1234567890xyz
[Information] Heartbeat sent successfully. Status: OK
```

## 🐛 Troubleshooting

### El servicio no inicia

**Problema**: El servicio no puede iniciarse.

**Soluciones**:

1. Verificar que el ejecutable existe en la ruta especificada
2. Verificar permisos de ejecución (ejecutar como Administrador)
3. Revisar el Visor de Eventos para errores específicos
4. Verificar que `appsettings.json` existe en la misma carpeta que el `.exe`

### Errores de conexión

**Problema**: El servicio no puede conectar al backend.

**Soluciones**:

1. Verificar que `HeartbeatUrl` es correcta y accesible
2. Verificar firewall de Windows no bloquea la conexión
3. Probar la URL manualmente con `curl` o Postman
4. Revisar logs del servicio para mensajes de error específicos

### Firma HMAC inválida

**Problema**: El backend rechaza los heartbeats por firma inválida.

**Soluciones**:

1. Verificar que `SecretKey` coincide con la del backend
2. Verificar formato de la firma (hexadecimal minúsculas)
3. Verificar que el orden de los campos es: `{timeStamp}|{deviceId}|{residentialId}`
4. Verificar encoding UTF-8 en ambos extremos

### El servicio se detiene inesperadamente

**Problema**: El servicio se detiene sin razón aparente.

**Soluciones**:

1. Revisar Visor de Eventos para excepciones no manejadas
2. Verificar que no hay problemas de memoria
3. Revisar logs para errores recurrentes
4. Verificar configuración de `IntervalSeconds` (valor válido > 0)

## 📁 Estructura del Proyecto

```
DeviceHeartbeatService/
├── DeviceHeartbeatService.csproj  # Archivo de proyecto
├── Program.cs                      # Punto de entrada y configuración del host
├── Worker.cs                       # Lógica principal del worker
├── HmacHelper.cs                   # Utilidad para generar firma HMAC
├── IFileLogger.cs                  # Interfaz para el logger de archivos
├── FileLogger.cs                   # Implementación del logger de archivos
├── appsettings.json                # Configuración del servicio
└── README.md                       # Esta documentación
```

## 🔍 Explicación Técnica

### Arquitectura

El servicio utiliza el patrón **Background Service** de .NET, implementado mediante:

- **Host Builder**: Configura el entorno de hosting con soporte para Windows Service
- **Worker Service**: Ejecuta la lógica de negocio en un bucle infinito
- **HTTP Client Factory**: Gestiona las conexiones HTTP de forma eficiente
- **Configuration**: Carga configuración desde `appsettings.json`
- **File Logger**: Sistema de logging thread-safe a archivo de texto

### Flujo de ejecución

1. El servicio inicia y carga la configuración
2. Inicia un bucle infinito que se ejecuta cada `IntervalSeconds`
3. En cada iteración:
   - Genera timestamp Unix actual
   - Construye string para firma: `{timeStamp}|{deviceId}|{residentialId}`
   - Genera firma HMAC-SHA256
   - Envía POST con el payload JSON
   - Registra resultado (éxito o error)
   - Espera el intervalo configurado

### Manejo de errores

El servicio implementa manejo robusto de errores:

- **Errores de red**: Capturados y logueados, el servicio continúa
- **Errores de serialización**: Capturados y logueados
- **Timeouts**: Configurados a 10 segundos, capturados y logueados
- **Cancelación**: Respeta el token de cancelación para cierre ordenado

Ningún error detendrá el servicio; siempre reintentará en el siguiente intervalo.

## 📝 Notas Adicionales

- El servicio requiere .NET 8 Runtime o puede publicarse como self-contained
- Los logs se generan automáticamente en dos ubicaciones:
  - **Archivo de texto**: `C:\ProgramData\DeviceHeartbeatService\logs\service.log`
  - **Visor de Eventos de Windows**: Registros de Windows → Aplicación
- El servicio es resiliente y puede recuperarse de errores temporales
- El sistema de logging a archivo es thread-safe y no bloquea las operaciones del servicio
- Si el logging a archivo falla, el servicio continúa funcionando normalmente
- La configuración puede modificarse en `appsettings.json` sin recompilar (requiere reinicio del servicio)
