using System;

namespace AssistenteInteligente.Services
{
    public static class CalculadoraVetorial
    {
        // Movendo o coração matemático para um serviço isolado
        public static float CalcularSimilaridadeCosseno(ReadOnlySpan<float> vetorA, ReadOnlySpan<float> vetorB)
        {
            float produtoEscalar = 0, magnitudeA = 0, magnitudeB = 0;
            for (int i = 0; i < vetorA.Length; i++)
            {
                produtoEscalar += vetorA[i] * vetorB[i];
                magnitudeA += vetorA[i] * vetorA[i];
                magnitudeB += vetorB[i] * vetorB[i];
            }
            return (float)(produtoEscalar / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB)));
        }
    }
}