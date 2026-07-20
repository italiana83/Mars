# Mars MOLA Viewer

Interactive 3D viewer of Martian topography based on **MGS MOLA** (Mars Orbiter Laser Altimeter) data in the **MEGDR** format (128 px/°).

---

## English

### About

**Mars MOLA Viewer** is a **C# / .NET 8** desktop application built with **OpenTK 4**. It reconstructs Martian terrain in 3D from PDS MEGDR tiles (`.lbl` metadata + `.img` elevation grids), applies MOLA-style height coloring, and lets you browse the planet through an interactive global minimap.

### Screenshots

![Filled terrain view](docs/images/mars-filled.png)
*Filled terrain view with MOLA height coloring.*

![Wireframe terrain view](docs/images/mars-wireframe.png)
*Wireframe terrain view.*

MEGDR data archive:  
https://pds-geosciences.wustl.edu/missions/mgs/megdr.html

### Features

- Load **MEG128** topography tiles (`megt*.lbl` / `megt*.img`)
- Triangulated heightmap mesh with **chunk-based frustum culling**
- Height-based color ramp in GLSL
- **Global minimap** with click-to-load tile selection
- Sidebar UI: minimap toggle and settings panel
- Settings: interface language, **sampling step** (1–32), **vertical mesh scale**, and movement speed
- Coordinate axes, scene bounding box, FPS counter

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- OpenGL 3.3 Core capable GPU/driver
- Local MOLA dataset (not bundled with the repo)

### Data setup

Create a local `data/` folder (gitignored):

```
data/
├── Mars_topography_(MOLA_dataset)_HiRes.png   # minimap image (.jpg also works)
└── mola/
    └── meg128/
        ├── megt00n000hb.lbl
        ├── megt00n000hb.img
        └── …                                   # additional MEG128 tiles
```

The app searches for `Mars_topography_*` images under `data/`. Download MEG128 tiles from the PDS Geosciences Node (MEGDR collection).

### Build and run

From the repository root:

```bash
dotnet build Mars.sln
dotnet run --project src/Mars.csproj
```

Debug configurations are available in `.vscode/` and `src/.vscode/`.

### Controls

| Action | Input |
|--------|-------|
| Move | W / A / S / D |
| Rotate camera | LMB + drag |
| Move along the view direction | Mouse wheel |
| Close menu | Esc |
| Open menu | Sidebar Menu |
| Minimap | Minimap → click a tile region |
| Settings | Settings → language, sampling step, height scale, movement speed |

### Repository layout

```
Mars/
├── Mars.sln              # solution (root)
├── src/                  # C# OpenTK application
├── data/                 # local datasets (not in git)
└── Utils/
    └── mars_mola_project/  # optional Python tooling (matplotlib)
```

---

## Русский

### О проекте

Приложение на **C# / .NET 8** с **OpenTK 4** строит трёхмерную модель рельефа из бинарных тайлов PDS (`.lbl` + `.img`), раскрашивает поверхность по высоте в палитре MOLA и позволяет исследовать отдельные участки планеты через интерактивную мини-карту.

### Скриншоты

![Вид рельефа с заливкой](docs/images/mars-filled.png)
*Вид рельефа с раскраской высот в палитре MOLA.*

![Каркасный вид рельефа](docs/images/mars-wireframe.png)
*Каркасный вид рельефа.*

Данные MEGDR публикуются NASA PDS:  
https://pds-geosciences.wustl.edu/missions/mgs/megdr.html

### Возможности

- Загрузка топографических тайлов **MEG128** (`megt*.lbl` / `megt*.img`)
- Построение tri-mesh из heightmap с **frustum culling** по чанкам
- Цветовая шкала высот (градиент MOLA) в fragment shader
- **Мини-карта** глобальной топографии с выбором тайла кликом
- Боковое меню: мини-карта и панель настроек
- Настройки: язык интерфейса, **шаг выборки** (1–32), **масштаб высоты** меша и скорость движения
- Оси координат, ограничивающий параллелепипед (AABB), счётчик FPS

### Требования

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Windows / Linux / macOS с поддержкой OpenGL 3.3 Core
- Файлы данных MOLA (см. ниже) — в репозиторий не входят

### Подготовка данных

Каталог `data/` указан в `.gitignore` и создаётся локально:

```
data/
├── Mars_topography_(MOLA_dataset)_HiRes.png   # мини-карта (или .jpg)
└── mola/
    └── meg128/
        ├── megt00n000hb.lbl
        ├── megt00n000hb.img
        ├── megt44n180hb.lbl
        └── …                                   # остальные тайлы MEG128
```

Мини-карту можно положить в `data/` под именем `Mars_topography_(MOLA_dataset)_HiRes.png` (предпочтительно) или `.jpg`. Приложение также ищет другие файлы `Mars_topography_*`.

Тайлы MEG128 скачиваются с PDS Geosciences Node (архив MEGDR).

### Сборка и запуск

Из корня репозитория:

```bash
dotnet build Mars.sln
dotnet run --project src/Mars.csproj
```

Или через Visual Studio / VS Code (конфигурации в `.vscode/` и `src/.vscode/`).

### Управление

| Действие | Управление |
|----------|------------|
| Перемещение | W / A / S / D |
| Поворот камеры | ЛКМ + перетаскивание |
| Перемещение вдоль направления взгляда | Колёсико мыши |
| Закрыть меню | Esc |
| Открыть меню | Боковая панель «Меню» |
| Мини-карта | «Мини-карта» → клик по региону |
| Настройки | «Настройки» → язык, шаг выборки, масштаб высоты, скорость движения |

### Структура репозитория

```
Mars/
├── Mars.sln              # решение (корень)
├── src/                  # C# приложение (OpenTK)
│   ├── HeightmapGame.cs  # главное окно
│   ├── MolaDataReader.cs # чтение PDS .lbl / .img
│   ├── MeshRender.cs     # рендер heightmap
│   ├── Minimap.cs        # UI мини-карты
│   └── …
├── data/                 # локальные данные (не в git)
└── Utils/
    └── mars_mola_project/  # вспомогательный Python-проект (matplotlib)
```

---

## License

See [LICENSE](LICENSE).
