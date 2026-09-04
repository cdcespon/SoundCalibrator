# Feature: Dual-Channel Transfer Function (H1 Estimator)
Como ingeniero de sonido usando SoundCalibrator
Quiero medir la función de transferencia entre una señal de referencia y una señal medida
Para obtener la respuesta en frecuencia (magnitud en dB, fase en grados) y la coherencia del sistema acústico.

## Criterio 1: Ventaneado Temporal
Given un buffer de audio de tamaño N (potencia de 2)
When se aplica una ventana (Hann, Blackman-Harris)
Then los extremos del buffer se atenúan suavemente hacia cero
And se preserva la energía normalizada según el factor de compensación de la ventana.

## Criterio 2: Identidad (Loopback perfecto)
Given una señal de referencia x(t) y una señal de medida y(t) idénticas (y = x)
When se calcula la función de transferencia H(f)
Then la Magnitud debe ser 0.0 dB (+/- 0.05 dB) en las frecuencias activas
And la Fase debe ser 0.0 grados (+/- 0.5 grados)
And la Coherencia debe ser 1.0 (100%).

## Criterio 3: Ganancia y Retardo conocido (Fase lineal)
Given una señal de prueba senoidal a frecuencia F0
And la señal de medida y(t) tiene el doble de amplitud (ganancia x2) y un desfase de 90 grados
When se calcula la función de transferencia
Then la Magnitud en F0 debe ser +6.02 dB (+/- 0.05 dB)
And la Fase en F0 debe ser exactamente -90.0 grados (+/- 1.0 grado)
And la Coherencia en F0 debe ser 1.0.

## Criterio 4: Robustez y Señal Nula (Casos Borde)
Given una señal de referencia en silencio total (amplitudes en cero)
When se ejecuta el cálculo de la función de transferencia
Then no debe ocurrir una excepción por división por cero
And la coherencia debe evaluarse como 0.0
And la magnitud no debe emitir valores NaN o Infinity.
