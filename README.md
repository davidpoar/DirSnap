# SQLite DB Viewer & Indexer

Un visor y creador de bases de datos SQLite potente y rápido para buscar, localizar y comparar archivos locales con soporte avanzado de visualización multimedia.

![App Icon](./Assets/app-icon.png)

## 🚀 Características Principales

1. **Generador Integrado de DB (Asistente)**: Olvídate de usar scripts por consola. Cuentas con un asistente gráfico tipo "Wizard" para crear una base de datos local desde cualquier de tus carpetas. Escanea todos tus archivos recursivamente para extraer metadatos (tamaños y fechas) y genera una base `.db` eficiente con rutinas transaccionales optimizadas para máxima velocidad de guardado mediante transacciones en bloque (WAL mode).
2. **Visualización de Bases Cruzada**: Permite cargar dos bases de datos y analizarlas simultáneamente. Mantiene en memoria y compara visualmente archivos basándose en `Hash + Tamaño` y `Nombre + Tamaño`.
3. **Visor Multimedia Rápido**: Posee un panel inteligente inferior que te permite previsualizar instantáneamente imágenes (`.jpg`, `.png`, etc) y autogenera "thumbnails" en la primera carga si el archivo es un video, descargando invisiblemente componentes de sistema operativo como `FFmpeg`.
4. **Filtros Inmediatos**: Descubre qué archivos son únicos y cuáles están en ambas colecciones en pocos *clicks* usando el menú superior simple de los `DataGrid`, iluminando las líneas mediante un sistema visual condicional tipo tabla Excel.

## 🛠 Instalación y Despliegue

La solución corre sobre **Avalonia UI** para asegurar compatibilidad cross-platform directa.
Asegúrate de tener instalado el SDK de .NET:

```bash
# Entrar a la carpeta del proyecto
cd Viewer

# Instalar dependencias 
dotnet restore

# Compilar código
dotnet build

# Ejecutar aplicación localmente
dotnet run
```

## 📚 Arquitectura y ¿Cómo Funciona?

### 1. FileIndexerHelper
Es una biblioteca de ayuda que recorre carpetas recursivamente mediante el sistema interno (`Directory.GetFiles` y una pila LIFO sin consumir *Heap*) enviando *logs* asíncronos directamente al `IndexerWizardWindow` usando `IProgress`. La Base de datos queda optimizada con Índices SQL y operaciones batch (`INSERT OR REPLACE`).

### 2. MainWindow y Lógica de UI
- Implementa componentes MVVM primitivos (Código asíncrono puro Behind) sin recargas reactivas largas.
- **Cross-Reference Engine**: Lee el archivo local. Usando LINQ (`GroupBy` y Diccionarios `Dictionary<string, FileItem>`), cruza ambas colas a velocidad extrema y otorga booleanos a la propiedad virtual `IsMatched`.
- Avalonia se encarga de reestructurar virtualmente millones de componentes listándolos en un DataGrid. Las filas se iluminan verde `#33228822` si son coincidentes o rojo si no encuentran su correspondiente hermano en la DB opuesta. 

## 🤝 Ecosistema
El proyecto depende internamente de:
- `Avalonia 11.3.12+`
- `Microsoft.Data.Sqlite 10.0+`
- `Xabe.FFmpeg`
- `Xabe.FFmpeg.Downloader`
