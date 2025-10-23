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