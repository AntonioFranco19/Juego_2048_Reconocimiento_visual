using System;
using System.Collections.Generic;
using System.Text;

namespace Juego_2048.Models
{
    public enum Direccion { Arriba, Abajo, Izquierda, Derecha, Nada }
    public class Tablero
    {
        private readonly int[,] _celdas = new int[4, 4];
        private readonly Random _random = new Random();
        public int Puntuacion { get; private set; }
        public int this[int fila, int columna] => _celdas[fila, columna];

        public void Reiniciar()
        {
            for (int f = 0; f < 4; f++)
            {
                for (int c = 0; c < 4; c++)
                {
                    _celdas[f, c] = 0;
                }
            }
            Puntuacion = 0;
            ColocarFichaAleatoria();
            ColocarFichaAleatoria();
        }

        public void ColocarFichaAleatoria()
        {
            var vacias = new List<(int Fila, int Columna)>();
            for (int f = 0; f < 4; f++)
                for (int c = 0; c < 4; c++)
                    if (_celdas[f, c] == 0) vacias.Add((f, c));

            if (vacias.Count == 0) return;

            var (fila, columna) = vacias[_random.Next(vacias.Count)];
            _celdas[fila, columna] = _random.Next(10) == 0 ? 4 : 2; // 10% -> 4, 90% -> 2
        }

        public int ObtenerDato(int fila, int columna)
        {
            return _celdas[fila, columna];
        }

        public bool Mover(Direccion direccion)
        {
            int[,] antes = (int[,])_celdas.Clone();

            switch (direccion)
            {
                case Direccion.Izquierda:
                    FusionarFilas();
                    break;
                case Direccion.Derecha:
                    Invertir();
                    FusionarFilas();
                    Invertir();
                    break;
                case Direccion.Arriba:
                    Transponer();
                    FusionarFilas();
                    Transponer();
                    break;
                case Direccion.Abajo:
                    Transponer();
                    Invertir();
                    FusionarFilas();
                    Invertir();
                    Transponer();
                    break;
            }

            return !Iguales(antes, _celdas);
        }

        // Aplica FusionarFilaIzquierda (Fase 4) a las 4 filas del tablero
        // actual y suma los puntos ganados a Puntuacion.
        private void FusionarFilas()
        {
            for (int fila = 0; fila < 4; fila++)
            {
                int[] original = { _celdas[fila, 0], _celdas[fila, 1], _celdas[fila, 2], _celdas[fila, 3] };
                var (fusionada, puntos) = FusionarFilaIzquierda(original);
                Puntuacion += puntos;
                for (int columna = 0; columna < 4; columna++)
                    _celdas[fila, columna] = fusionada[columna];
            }
        }

        // Fase 4: compactar + fusionar + rellenar, para una sola fila hacia
        // la izquierda. Es una función pura -solo toma un array y devuelve
        // otro-, por eso es la parte más fácil de probar aislada.
        private static (int[] fila, int puntos) FusionarFilaIzquierda(int[] fila)
        {
            var compactada = new List<int>();
            foreach (int valor in fila)
                if (valor != 0) compactada.Add(valor);

            var resultado = new List<int>();
            int puntos = 0;
            int i = 0;
            while (i < compactada.Count)
            {
                if (i + 1 < compactada.Count && compactada[i] == compactada[i + 1])
                {
                    int fusion = compactada[i] * 2;
                    resultado.Add(fusion);
                    puntos += fusion;
                    i += 2; // los dos ya se han usado: saltamos ambos
                }
                else
                {
                    resultado.Add(compactada[i]);
                    i += 1;
                }
            }
            while (resultado.Count < 4) resultado.Add(0);
            return (resultado.ToArray(), puntos);
        }

        // Fase 5: invierte el orden de los valores dentro de cada fila.
        private void Invertir()
        {
            for (int fila = 0; fila < 4; fila++)
            {
                (_celdas[fila, 0], _celdas[fila, 3]) = (_celdas[fila, 3], _celdas[fila, 0]);
                (_celdas[fila, 1], _celdas[fila, 2]) = (_celdas[fila, 2], _celdas[fila, 1]);
            }
        }

        // Fase 5: la fila i, columna j pasa a fila j, columna i.
        private void Transponer()
        {
            for (int fila = 0; fila < 4; fila++)
                for (int columna = fila + 1; columna < 4; columna++)
                    (_celdas[fila, columna], _celdas[columna, fila]) = (_celdas[columna, fila], _celdas[fila, columna]);
        }

        private static bool Iguales(int[,] a, int[,] b)
        {
            for (int fila = 0; fila < 4; fila++)
                for (int columna = 0; columna < 4; columna++)
                    if (a[fila, columna] != b[fila, columna]) return false;
            return true;
        }

        public bool HayFichaGanadora()
        {
            for (int fila = 0; fila < 4; fila++)
                for (int columna = 0; columna < 4; columna++)
                    if (_celdas[fila, columna] == 2048) return true;
            return false;
        }

        // Fase 7: true si hay algún hueco vacío o algún par de vecinos
        // (derecha o abajo) con el mismo valor. Un tablero lleno no es lo
        // mismo que "sin jugada posible".
        public bool HayMovimientosPosibles()
        {
            for (int fila = 0; fila < 4; fila++)
                for (int columna = 0; columna < 4; columna++)
                    if (_celdas[fila, columna] == 0) return true;

            for (int fila = 0; fila < 4; fila++)
            {
                for (int columna = 0; columna < 4; columna++)
                {
                    int valor = _celdas[fila, columna];
                    if (columna + 1 < 4 && _celdas[fila, columna + 1] == valor) return true;
                    if (fila + 1 < 4 && _celdas[fila + 1, columna] == valor) return true;
                }
            }
            return false;
        }
    }
}
