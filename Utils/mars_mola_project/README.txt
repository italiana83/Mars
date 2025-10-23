Создам пошаговую инструкцию по созданию окружения в Anaconda для работы с проектом визуализации MOLA данных.

## 1. Создание нового окружения

```bash
# Откройте Anaconda Prompt или командную строку
# Создайте новое окружение с Python 3.9
conda create -n mola-visualization python=3.9

# Активируйте окружение
conda activate mola-visualization
```

## 2. Установка необходимых пакетов

```bash
# Установка основных пакетов через conda
conda install matplotlib pillow numpy jupyter

# Или через pip (если какие-то пакеты недоступны в conda)
pip install matplotlib pillow numpy jupyter
```

## 3. Альтернативный способ - создание через environment.yml файл

Создайте файл `environment.yml` с содержимым:

```yaml
name: mola-visualization
channels:
  - conda-forge
  - defaults
dependencies:
  - python=3.9
  - matplotlib
  - pillow
  - numpy
  - jupyter
  - pip
```

Затем выполните:
```bash
conda env create -f environment.yml
conda activate mola-visualization
```

## 4. Проверка установки

Создайте тестовый файл `test_environment.py`:

```python
import matplotlib
import PIL
import numpy
import sys

print("Python version:", sys.version)
print("Matplotlib version:", matplotlib.__version__)
print("Pillow version:", PIL.__version__)
print("NumPy version:", numpy.__version__)

# Простой тест отображения
import matplotlib.pyplot as plt
plt.plot([1, 2, 3, 4])
plt.title('Test - Environment Works!')
plt.show()
```

## 5. Структура проекта

Рекомендую создать такую структуру папок:
```
mars_mola_project/
│
├── environment.yml
├── mars_visualization.py
├── test_environment.py
└── data/
    ├── Mars_topography_(MOLA_dataset)_HiRes.jpg
    └── MEGDR_files/
        ├── megt00n180gb.img
        ├── megr00n180gb.img
        └── ...
```

## 6. Полный код для вашего проекта

Сохраните как `mars_mola_visualization.py`:

```python
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from PIL import Image
import numpy as np
import struct

class MOLADataProcessor:
    def __init__(self):
        self.regions_128 = [
            # Северное полушарие (88°N to 44°N)
            {"lat_range": (44, 88), "lon_range": (0, 90), "name": "88°N-44°N\n0°E-90°E", "color": "red"},
            {"lat_range": (44, 88), "lon_range": (90, 180), "name": "88°N-44°N\n90°E-180°E", "color": "blue"},
            {"lat_range": (44, 88), "lon_range": (180, 270), "name": "88°N-44°N\n180°E-270°E", "color": "green"},
            {"lat_range": (44, 88), "lon_range": (270, 360), "name": "88°N-44°N\n270°E-360°E", "color": "orange"},
            
            # Экваториальная зона (44°N to 0°)
            {"lat_range": (0, 44), "lon_range": (0, 90), "name": "44°N-0°\n0°E-90°E", "color": "red"},
            {"lat_range": (0, 44), "lon_range": (90, 180), "name": "44°N-0°\n90°E-180°E", "color": "blue"},
            {"lat_range": (0, 44), "lon_range": (180, 270), "name": "44°N-0°\n180°E-270°E", "color": "green"},
            {"lat_range": (0, 44), "lon_range": (270, 360), "name": "44°N-0°\n270°E-360°E", "color": "orange"},
            
            # Южное полушарие (0° to 44°S)
            {"lat_range": (-44, 0), "lon_range": (0, 90), "name": "0°-44°S\n0°E-90°E", "color": "red"},
            {"lat_range": (-44, 0), "lon_range": (90, 180), "name": "0°-44°S\n90°E-180°E", "color": "blue"},
            {"lat_range": (-44, 0), "lon_range": (180, 270), "name": "0°-44°S\n180°E-270°E", "color": "green"},
            {"lat_range": (-44, 0), "lon_range": (270, 360), "name": "0°-44°S\n270°E-360°E", "color": "orange"},
            
            # Южная полярная зона (44°S to 88°S)
            {"lat_range": (-88, -44), "lon_range": (0, 90), "name": "44°S-88°S\n0°E-90°E", "color": "red"},
            {"lat_range": (-88, -44), "lon_range": (90, 180), "name": "44°S-88°S\n90°E-180°E", "color": "blue"},
            {"lat_range": (-88, -44), "lon_range": (180, 270), "name": "44°S-88°S\n180°E-270°E", "color": "green"},
            {"lat_range": (-88, -44), "lon_range": (270, 360), "name": "44°S-88°S\n270°E-360°E", "color": "orange"},
        ]
    
    def convert_longitude(self, lon):
        """Преобразует долготы из 0-360 в -180-180"""
        return lon if lon <= 180 else lon - 360
    
    def plot_regions_on_map(self, image_path):
        """Отображает регионы на карте Марса"""
        try:
            # Загрузка изображения
            mars_image = Image.open(image_path)
            mars_array = np.array(mars_image)
            
            # Создаем фигуру
            fig, ax = plt.subplots(1, 1, figsize=(20, 10))
            
            # Отображаем карту Марса
            ax.imshow(mars_array, extent=[-180, 180, -90, 90], aspect='auto')
            
            # Рисуем прямоугольники регионов
            for region in self.regions_128:
                lon_min, lon_max = region["lon_range"]
                lat_min, lat_max = region["lat_range"]
                
                # Преобразуем долготы
                lon_min_conv = self.convert_longitude(lon_min)
                lon_max_conv = self.convert_longitude(lon_max)
                
                width = lon_max_conv - lon_min_conv
                height = lat_max - lat_min
                
                # Создаем прямоугольник
                rect = patches.Rectangle(
                    (lon_min_conv, lat_min), width, height,
                    linewidth=2, edgecolor='white', 
                    facecolor=region["color"], alpha=0.3
                )
                ax.add_patch(rect)
                
                # Добавляем подпись
                center_x = lon_min_conv + width / 2
                center_y = lat_min + height / 2
                ax.text(center_x, center_y, region["name"], 
                        ha='center', va='center', fontsize=6, fontweight='bold',
                        color='white', 
                        bbox=dict(boxstyle="round,pad=0.1", facecolor="black", alpha=0.7))
            
            # Настройка графика
            ax.set_xlim(-180, 180)
            ax.set_ylim(-90, 90)
            ax.set_xlabel('Longitude (degrees)')
            ax.set_ylabel('Latitude (degrees)')
            ax.set_title('MEGDR 128 Pixels/Degree Tiling Overlay on MOLA Topography Map', 
                        fontsize=14, fontweight='bold', color='white')
            ax.grid(True, color='white', alpha=0.3)
            
            plt.tight_layout()
            plt.show()
            
        except FileNotFoundError:
            print(f"Ошибка: Файл {image_path} не найден!")
        except Exception as e:
            print(f"Ошибка при обработке изображения: {e}")
    
    def generate_file_names(self):
        """Генерирует имена файлов для всех регионов"""
        print("MEGDR 128 Pixels per Degree - Complete File List")
        print("=" * 70)
        
        for i, region in enumerate(self.regions_128, 1):
            lat_min, lat_max = region["lat_range"]
            lon_min, lon_max = region["lon_range"]
            
            # Определяем код широты
            if lat_min >= 0:
                lat_code = f"{abs(lat_max):02d}n"
            else:
                lat_code = f"{abs(lat_min):02d}s"
            
            lon_code = f"{lon_min:03d}"
            
            print(f"\nRegion {i:2d}: {region['name']}")
            print(f"  Topography: MEGT{lat_code}{lon_code}HB.IMG")
            print(f"  Radius:     MEGR{lat_code}{lon_code}HB.IMG") 
            print(f"  Counts:     MEGC{lat_code}{lon_code}HB.IMG")
            print(f"  Areoid:     MEGA{lat_code}{lon_code}HB.IMG")

# Использование
if __name__ == "__main__":
    processor = MOLADataProcessor()
    
    # Укажите путь к вашему изображению
    image_path = "data/Mars_topography_(MOLA_dataset)_HiRes.jpg"
    
    # Визуализация
    processor.plot_regions_on_map(image_path)
    
    # Генерация имен файлов
    processor.generate_file_names()
```

## 7. Запуск проекта

```bash
# Активируйте окружение
conda activate mola-visualization

# Запустите скрипт
python mars_mola_visualization.py

# Или запустите Jupyter для интерактивной работы
jupyter notebook
```

## 8. Дополнительные пакеты (опционально)

Если планируете расширенную работу с данными:

```bash
conda install pandas scipy scikit-image
pip install opencv-python
```

Теперь у вас есть полностью настроенное окружение для работы с MOLA данными!