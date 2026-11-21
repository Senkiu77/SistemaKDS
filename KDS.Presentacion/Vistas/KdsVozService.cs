using System;
using System.Speech.Synthesis;
using System.Text;

namespace KDS.PRE.Vistas
{
    public static class KdsVozService
    {
        private static readonly SpeechSynthesizer _sintetizador = new();
        private static readonly object _candado = new();

        public static void CantarPedido(PedidoModel p)
        {
            if (p == null) return;

            try
            {
                lock (_candado)
                {
                    if (_sintetizador.State == SynthesizerState.Ready)
                    {
                        _sintetizador.SetOutputToDefaultAudioDevice();
                    }

                    _sintetizador.Rate = 0;
                    _sintetizador.Volume = 100;

                    var frase = new StringBuilder();
                    frase.Append($"Atención cocina. Nuevo pedido para la mesa {p.NumeroMesa}. ");

                    foreach (var det in p.Detalles)
                    {
                        frase.Append($"{det.Cantidad} {det.NombrePlato}. ");
                        if (!string.IsNullOrWhiteSpace(det.NotasEspeciales))
                        {
                            frase.Append($"Con nota especial: {det.NotasEspeciales}. ");
                        }
                    }

                    _sintetizador.SpeakAsync(frase.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error de síntesis de voz: {ex.Message}");
            }
        }
    }
}
