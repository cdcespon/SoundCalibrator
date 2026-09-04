# SoundCalibrator 🎛️⚡

> **Software de calibración acústica y medición en tiempo real de doble canal (FFT Dual-Channel Transfer Function).**  
> Inspirado en la arquitectura y capacidades de **Open Sound Meter**, desarrollado íntegramente bajo la disciplina de **SwarmForge** (Uncle Bob Software Craftsmanship) sobre **.NET 10** y **Avalonia UI**.

---

## 🌟 Características Principales

### 🔬 Motor Matemático DSP (Clean Architecture / Cero Alocaciones)
* **Función de Transferencia Dual-Channel (H1 Estimator):** Cálculo simultáneo entre canal de Referencia y Medición:
  * **Magnitud en dB:** $20 \log_{10}(|H(f)|)$ con escala de $+18\text{ dB}$ a $-36\text{ dB}$.
  * **Fase en grados:** $\text{atan2}$ de $+180^\circ$ a $-180^\circ$.
  * **Desenrollado de Fase (Phase Unwrap):** Eliminación continua de saltos de $360^\circ$ para análisis de retardo acústico.
  * **Coherencia ($\gamma^2$):** Medición de causalidad y relación señal/ruido acotada estrictamente en $[0.0, 1.0]$.
* **FFT Radix-2 & IFFT ($O(N \log N)$):** Transformadas directa e inversa con tablas precomputadas de inversión de bits y factores twiddle.
* **Ventaneado Temporal:** Hann y Blackman-Harris de 4 términos optimizados sobre `Span<float>`.
* **Cero Asignaciones en el Heap (0 Bytes Heap Allocation):** Bucle de procesamiento de audio en tiempo real con cero pausas de Garbage Collection.

### ⏱️ Buscador Automático de Retardo (Auto Delay Finder via IFFT)
* Cálculo de la **Respuesta al Impulso ($h(t)$)** mediante FFT Inversa.
* Detección milimétrica del arribo acústico directo en milisegundos ($\text{ms}$) y distancia equivalente en metros ($m = \text{ms} \times 0.343$).
* **Alineación de Fase en 1 Clic (`ALIGN PHASE`):** Compensa el retardo rotando la fase de vuelta a $0^\circ$ plano.

### 📊 Suavizado y Promediado Espectral
* **Promediado de Espectros:** Modos *Fast (Exp)*, *Slow (Exp)*, *Lineal (16)* e *Infinito* sobre auto-espectros ($G_{xx}, G_{yy}$) y espectros cruzados ($G_{xy}$).
* **Suavizado por Fracción de Octava:** Filtro logarítmico de bandas acústicas ($1/1, 1/3, 1/6, 1/12, 1/24, 1/48$).
* **Coherence Blanking:** Supresión configurable de datos de fase en zonas de baja coherencia ($30\%, 50\%, 70\%$).

### 🎚️ Fuentes de Audio & Generadores Sintéticos
* **Captura de Hardware Real (WASAPI):** Entrada estéreo de baja latencia para tarjetas de sonido y micrófonos de medición en Windows.
* **Generador Acústico de Ruido Rosa (Pink Noise):** Filtro Paul Kellet de $-3\text{ dB/octava}$.
* **Generador de Barrido Senoidal Logarítmico (Sine Sweep / Farina):** Barrido continuo de $20\text{ Hz}$ a $20\text{ kHz}$ en 3 segundos.
* **Simulador de Retardo y Ganancia:** Sliders interactivos para modelar desfases y ganancias físicas en tiempo real.

### 💾 Gestión y Exportación de Trazas
* **Captura de Trazas en Caliente (`CAPTURE`):** Congela mediciones para superponer y comparar ajustes de EQ o alineación de altavoces.
* **Exportación / Importación CSV:** Totalmente compatible con **Open Sound Meter**, **REW** y **Smaart**.
* **Calibración de Micrófonos (`.cal` / `.txt`):** Lector de curvas de compensación de micrófonos con interpolación logarítmica inteligente.

---

## 🏗️ Arquitectura de la Solución

```text
SoundCalibrator/
├── src/
│   ├── SoundCalibrator.Core/         --> [Dominio Matemático DSP Puro]
│   │   ├── Averaging/                (Promediado espectral y modos de integración)
│   │   ├── Calibration/              (Lector de curvas de micrófono e interpolación)
│   │   ├── DSP/                      (FFT, IFFT, Windowing, TransferFunction, IR)
│   │   ├── Models/                   (AcousticTrace, TransferFunctionResult, WindowType)
│   │   ├── Serialization/            (Exportación/Importación CSV compatible con OSM/REW)
│   │   └── Smoothing/                (Filtro de bandas fraccionarias de octava)
│   │
│   ├── SoundCalibrator.Audio/        --> [Adaptador de Hardware y Audio]
│   │   ├── Buffers/                  (AudioFifoBuffer circular lock-free multihilo)
│   │   ├── Devices/                  (WasapiAudioCaptureDevice para Windows)
│   │   ├── Engine/                   (AcousticMeasurementEngine con worker thread)
│   │   └── Generators/               (SyntheticAudioGenerator: Pink Noise, Sweep, Sine)
│   │
│   └── SoundCalibrator.App/          --> [Presentación: Avalonia UI + SkiaSharp]
│       ├── Controls/                 (AcousticGraphControl: Dibujo vectorial GPU a 60 FPS)
│       └── MainWindow.axaml          (Barra de herramientas, controles y panel)
│
└── tests/
    └── SoundCalibrator.Core.Tests/   --> [Suite TDD & Pruebas de Estrés xUnit]
```

---

## 🚀 Requisitos y Ejecución

* **SDK:** .NET 10 (`10.0.100` o superior)
* **Plataforma:** Windows (WASAPI / Avalonia), Linux y macOS (Avalonia / Core)

### Compilar y Ejecutar Pruebas:
```powershell
dotnet test
```
*(23 pruebas automatizadas pasando al 100% en menos de 200 ms).*

### Lanzar la Aplicación:
```powershell
dotnet run --project src/SoundCalibrator.App/SoundCalibrator.App.csproj
```

---

## 📜 Licencia & Constitución
Desarrollado bajo las normas éticas y de calidad de software de la **Constitución de Ingeniería SoundCalibrator** inspirada en el framework multi-agente **SwarmForge** de Robert C. Martin (*Uncle Bob*).
