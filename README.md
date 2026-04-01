# DirSnap

![App Icon](./Assets/app-icon.png)

## 📌 ¿Qué es y para qué sirve?

**DirSnap** es una herramienta de escritorio diseñada para ayudarte a **organizar, buscar y comparar colecciones gigantes de archivos**. 

¿Tienes discos duros o carpetas llenas de fotos, videos o documentos repetidos y no sabes exactamente qué hay en cada uno? Este programa te soluciona la vida permitiéndote:
- **Escanear un disco o carpeta completa** y guardar un "índice" (una base de datos) súper rápido con todo lo que contiene.
- **Comparar dos índices distintos** (por ejemplo, el índice de tu disco duro principal y el de tu disco de copias de seguridad) para ver qué archivos te faltan en un lado o cuáles están duplicados.
- **Previsualizar imágenes y videos** directamente desde la aplicación al seleccionar un archivo, para saber de un vistazo de qué archivo se trata.

---

## 🚀 ¿Cómo se usa? (Guía paso a paso)

El uso del programa es muy simple y consta de dos fases: **crear inventarios** de tus discos/carpetas y luego **compararlos** entre sí.

### Fase 1: Crear el inventario de tus archivos (Indexar)
Para poder comparar, primero necesitas crear un archivo `.db` (un inventario) por cada disco o carpeta que quieras analizar.
1. Abre el programa.
2. Haz clic en el botón superior derecho que dice **"Generar DB desde carpeta..."**. Esto abrirá la ventana del *Asistente de Indexación*.
3. En el paso **"1. Selecciona la carpeta a escanear:"**, haz clic en el botón con puntos suspensivos (`...`) y elige el disco duro o carpeta que quieres inventariar.
4. En el paso **"2. Selecciona dónde guardar la Base de Datos:"**, haz clic en su botón (`...`) y elige dónde se guardará el archivo `.db` (tu inventario con los resultados) y ponle un nombre.
5. Haz clic en el botón **"Iniciar"** abajo a la derecha. 
6. El programa rastreará todos tus archivos muy rápido. Al terminar, mostrará el mensaje *¡Indexación Completada!*. Haz clic en **"No cargar (Cerrar)"** o elige cargarla directamente en uno de los paneles.

> *Consejo: Repite este proceso para tu otro disco duro o carpeta, así tendrás dos archivos `.db` listos para comparar.*

### Fase 2: Comparar y Explorar  
Ya tienes tus dos inventarios (`.db`). Ahora vamos a cruzarlos para ver qué archivos faltan en uno de tus discos:
1. En la ventana principal, arriba a la izquierda, verás dos botones: **"Load DB 1"** y **"Load DB 2"**.
2. Haz clic en **"Load DB 1"** y carga el primer inventario (`.db`) que creaste (ej. tu pc principal).
3. Haz clic en **"Load DB 2"** y carga tu segundo inventario (ej. tu disco de respaldo). 
4. ¡Listo! Automáticamente verás todo tu contenido organizado en dos listas. Presta mucha atención a los colores:
   - 🟢 **Filas en color VERDE**: El archivo es idéntico y existe en AMBOS discos. (Lo tienes guardado en ambos lados).
   - 🔴 **Filas en color ROJO**: El archivo SOLO existe en esa lista. (¡Cuidado, si es un archivo rojo, significa que no lo tienes respaldado en el otro disco!).
   
   > **Nota importante:** El programa sabe que es el mismo archivo mirando su *tamaño* y su *contenido*, **sin importarle en qué carpeta esté guardado**. Incluso si lo moviste de carpeta, si es el exacto mismo archivo, te saldrá en verde.
5. Puedes hacer clic sobre cualquier archivo de la lista para ver abajo una vista previa interactiva (verás fotos y miniaturas de videos).

---

## 🛠 Instalación y Ejecución

El programa está construido con **Avalonia UI**, lo que lo hace muy rápido y compatible multiplataforma.

Si tienes el código fuente y quieres ejecutarlo, asegúrate de tener instalado el **SDK de .NET**. Luego abre una terminal y ejecuta:

```bash
# Entrar a la carpeta del proyecto
cd Viewer

# Ejecutar la aplicación
dotnet run
```

---

## 🤓 Detalles Técnicos y Arquitectura (Para Desarrolladores)

Si eres desarrollador y te interesa saber cómo funciona "bajo el capó":

- **FileIndexerHelper (Escaneo veloz)**: Recorre directorios sin consumir memoria Heap usando estructuras optimizadas en C#. Guarda en la base de datos de manera atómica transaccional (bloques Batch en modo WAL) garantizando miles de inserciones por segundo.
- **Cross-Reference Engine (Comparación)**: El motor cruza las colecciones en memoria principal evaluando características únicas (`Hash + Tamaño` temporalmente o `Nombre + Tamaño`) mediante diccionarios en C# (LINQ), resultando en comparaciones instantáneas a gran escala.
- **Componentes UI y Multimedia**: Desarrollado con el patrón MVVM Behind en Avalonia UI. Descarga bajo demanda `FFmpeg` (usando `Xabe.FFmpeg`) para generar *thumbnails* de video en tiempo real sin bloquear la interfaz.
- **Ecosistema**: `Avalonia 11.3+`, `Microsoft.Data.Sqlite`, `Xabe.FFmpeg`.
