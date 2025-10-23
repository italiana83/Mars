import matplotlib.pyplot as plt
import matplotlib.patches as patches
from PIL import Image
import numpy as np

class MOLAGridProcessor:
    def __init__(self, grid_width=36, grid_height=14, x_offset_ratio=0.25):
        self.grid_width = grid_width
        self.grid_height = grid_height
        self.x_offset_ratio = x_offset_ratio  # смещение сетки вправо (25%)
        
        # Размеры ячеек (постоянные)
        self.cell_width = 1.0 / grid_width    # относительная ширина
        self.cell_height = 1.0 / grid_height  # относительная высота
        
        # Определяем регионы MEGDR 128 pix/deg
        self.regions_128 = self._define_regions()
    
    def _define_regions(self):
        """Определяем регионы MEGDR 128 pix/deg"""
        colors = ['red', 'blue', 'green', 'orange']
        
        regions = [
            # Северное полушарие (88°N to 44°N) - 4 региона по долготе
            {"lat_range": (44, 88), "lon_range": (0, 90), "name": "88°N-44°N\n0°E-90°E", "color": colors[0]},
            {"lat_range": (44, 88), "lon_range": (90, 180), "name": "88°N-44°N\n90°E-180°E", "color": colors[1]},
            {"lat_range": (44, 88), "lon_range": (180, 270), "name": "88°N-44°N\n180°E-270°E", "color": colors[2]},
            {"lat_range": (44, 88), "lon_range": (270, 360), "name": "88°N-44°N\n270°E-360°E", "color": colors[3]},
            
            # Средние широты (44°N to 0°) - 4 региона по долготе
            {"lat_range": (0, 44), "lon_range": (0, 90), "name": "44°N-0°\n0°E-90°E", "color": colors[0]},
            {"lat_range": (0, 44), "lon_range": (90, 180), "name": "44°N-0°\n90°E-180°E", "color": colors[1]},
            {"lat_range": (0, 44), "lon_range": (180, 270), "name": "44°N-0°\n180°E-270°E", "color": colors[2]},
            {"lat_range": (0, 44), "lon_range": (270, 360), "name": "44°N-0°\n270°E-360°E", "color": colors[3]},
            
            # Средние широты (0° to 44°S) - 4 региона по долготе
            {"lat_range": (-44, 0), "lon_range": (0, 90), "name": "0°-44°S\n0°E-90°E", "color": colors[0]},
            {"lat_range": (-44, 0), "lon_range": (90, 180), "name": "0°-44°S\n90°E-180°E", "color": colors[1]},
            {"lat_range": (-44, 0), "lon_range": (180, 270), "name": "0°-44°S\n180°E-270°E", "color": colors[2]},
            {"lat_range": (-44, 0), "lon_range": (270, 360), "name": "0°-44°S\n270°E-360°E", "color": colors[3]},
            
            # Южное полушарие (44°S to 88°S) - 4 региона по долготе
            {"lat_range": (-88, -44), "lon_range": (0, 90), "name": "44°S-88°S\n0°E-90°E", "color": colors[0]},
            {"lat_range": (-88, -44), "lon_range": (90, 180), "name": "44°S-88°S\n90°E-180°E", "color": colors[1]},
            {"lat_range": (-88, -44), "lon_range": (180, 270), "name": "44°S-88°S\n180°E-270°E", "color": colors[2]},
            {"lat_range": (-88, -44), "lon_range": (270, 360), "name": "44°S-88°S\n270°E-360°E", "color": colors[3]},
        ]
        
        return regions
    
    def _lat_to_y(self, lat, img_height):
        """Преобразует широту в координату Y на изображении"""
        # lat: -90 до +90, y: 0 до img_height (север вверху)
        return ((90 - lat) / 180) * img_height
    
    def _lon_to_x(self, lon, img_width):
        """Преобразует долготу в координату X на изображении с учетом смещения"""
        # lon: 0 до 360, x: 0 до img_width
        # Учитываем смещение сетки на 25% вправо
        x_ratio = (lon / 360) - self.x_offset_ratio
        if x_ratio < 0:
            x_ratio += 1.0  # переносим в правую часть если вышли за левый край
        return x_ratio * img_width
    
    def find_pixel_region(self, x, y, img_width, img_height):
        """Находит регион MEGDR для пикселя с координатами (x, y)"""
        # Преобразуем координаты пикселя в географические
        lon = ((x / img_width) + self.x_offset_ratio) * 360
        if lon >= 360:
            lon -= 360
        
        lat = 90 - (y / img_height) * 180
        
        # Находим соответствующий регион
        for region in self.regions_128:
            lat_min, lat_max = region["lat_range"]
            lon_min, lon_max = region["lon_range"]
            
            if (lat_min <= lat <= lat_max) and (lon_min <= lon <= lon_max):
                return region
        
        return None
    
    def plot_regions_on_map(self, image_path):
        """Отображает регионы MEGDR на карте с учетом смещения сетки"""
        try:
            # Загрузка изображения
            mars_image = Image.open(image_path)
            mars_array = np.array(mars_image)
            img_height, img_width = mars_array.shape[:2]
            
            # Создаем фигуру
            fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(20, 10))
            
            # Первый subplot: исходное изображение
            ax1.imshow(mars_array)
            ax1.set_title('Исходная карта MOLA', fontsize=14, fontweight='bold')
            ax1.axis('off')
            
            # Второй subplot: с наложенными регионами MEGDR
            ax2.imshow(mars_array)
            ax2.set_title('Регионы MEGDR 128 pix/deg\n(с учетом смещения сетки)', 
                         fontsize=14, fontweight='bold')
            ax2.axis('off')
            
            # Создаем маску для заливки регионов
            region_mask = np.zeros((img_height, img_width, 4), dtype=np.float32)
            
            # Заполняем регионы цветами
            for y in range(img_height):
                for x in range(img_width):
                    region = self.find_pixel_region(x, y, img_width, img_height)
                    if region:
                        color = region["color"]
                        # Преобразуем название цвета в RGB
                        if color == 'red':
                            rgb = (1, 0, 0, 0.3)
                        elif color == 'blue':
                            rgb = (0, 0, 1, 0.3)
                        elif color == 'green':
                            rgb = (0, 1, 0, 0.3)
                        elif color == 'orange':
                            rgb = (1, 0.5, 0, 0.3)
                        else:
                            rgb = (1, 1, 1, 0.3)
                        region_mask[y, x] = rgb
            
            # Накладываем маску регионов
            ax2.imshow(region_mask)
            
            # Добавляем подписи регионов
            for region in self.regions_128:
                # Находим приблизительный центр региона
                lat_center = (region["lat_range"][0] + region["lat_range"][1]) / 2
                lon_center = (region["lon_range"][0] + region["lon_range"][1]) / 2
                
                x_center = self._lon_to_x(lon_center, img_width)
                y_center = self._lat_to_y(lat_center, img_height)
                
                # Проверяем, чтобы подписи не выходили за границы изображения
                if (0 <= x_center <= img_width and 0 <= y_center <= img_height):
                    ax2.text(x_center, y_center, region["name"], 
                            ha='center', va='center', fontsize=6, fontweight='bold',
                            color='white', 
                            bbox=dict(boxstyle="round,pad=0.2", facecolor="black", alpha=0.8))
            
            plt.tight_layout()
            plt.show()
            
            # Выводим информацию о файлах
            self.print_file_mapping()
            
        except FileNotFoundError:
            print(f"Ошибка: Файл {image_path} не найден!")
        except Exception as e:
            print(f"Ошибка при обработке изображения: {e}")
    
    def print_file_mapping(self):
        """Выводит соответствие регионов MEGDR именам файлов"""
        print("MEGDR 128 Pixels per Degree - File Mapping")
        print("=" * 60)
        print(f"Параметры сетки: {self.grid_width}×{self.grid_height}, смещение: {self.x_offset_ratio*100}%")
        print()
        
        for i, region in enumerate(self.regions_128, 1):
            lat_min, lat_max = region["lat_range"]
            lon_min, lon_max = region["lon_range"]
            
            # Генерируем код региона для имени файла
            if lat_min >= 0:
                lat_code = f"{abs(lat_max):02d}n"
            else:
                lat_code = f"{abs(lat_min):02d}s"
            
            lon_code = f"{lon_min:03d}"
            
            print(f"Регион {i:2d}: {region['name']}")
            print(f"  Координаты: {lat_min}°-{lat_max}° lat, {lon_min}°-{lon_max}° lon")
            print(f"  Файлы:")
            print(f"    MEGT{lat_code}{lon_code}HB.IMG - Topography")
            print(f"    MEGR{lat_code}{lon_code}HB.IMG - Radius")
            print(f"    MEGC{lat_code}{lon_code}HB.IMG - Counts")
            print(f"    MEGA{lat_code}{lon_code}HB.IMG - Areoid")
            print()

# Использование
if __name__ == "__main__":
    # Создаем процессор с учетом смещения сетки на 25%
    processor = MOLAGridProcessor(grid_width=36, grid_height=14, x_offset_ratio=0.25)
    
    # Укажите путь к вашему изображению
    image_path = "data/Mars_topography_(MOLA_dataset)_HiRes_2.jpg"
    
    # Визуализация
    processor.plot_regions_on_map(image_path)