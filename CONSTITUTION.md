# Constitución de Ingeniería de SoundCalibrator
Inspirada en Uncle Bob SwarmForge

## 1. Regla de Oro de Arquitectura (Clean Architecture)
- SoundCalibrator.Core es el corazón matemático y de dominio puro. No contiene dependencias de interfaces de usuario (Avalonia, XAML, SkiaSharp), ni librerías de hardware de audio (WASAPI, ASIO, PortAudio), ni operaciones de I/O bloqueantes.
- El Core es 100% determinista y testeable sin hardware físico.

## 2. Reglas para los Roles del Enjambre
1. Specifier:
   - Define las especificaciones de comportamiento (BDD/Gherkin y criterios matemáticos exactos).
   - No implementa código de producción.
2. Coder:
   - TDD estricto: Primero escribe los tests que fallan (rojo) basados en la especificación, luego la implementación mínima necesaria para pasar (verde).
   - Utiliza .NET 10 y C# 14 con foco en Span<float>, ReadOnlySpan<float>, vectorización SIMD y bajo consumo.
   - Prohibido hacer refactorizaciones arquitectónicas globales fuera del scope asignado.
3. Refactorer:
   - Mejora la estructura del código sin cambiar el comportamiento externo (los tests deben mantenerse en verde).
   - Elimina duplicación (DRY).
   - Garantiza CERO asignaciones en el Heap en el bucle caliente (hot loop) de DSP.
   - Mantiene la complejidad ciclomática por método acotada (CRAP <= 10).
4. Architect / Hardener:
   - Verifica el cumplimiento de las fronteras de dependencia.
   - Introduce pruebas de estrés para casos límite (buffers vacíos, división por cero, silencio digital, ráfagas de ruido blanco, clipping).
   - Valida la estabilidad y precisión numérica.
