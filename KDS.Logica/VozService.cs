using System;
using System.Speech.Synthesis;
using System.Text;
using KDS.Entidades;

namespace KDS.Logica
{
    public class VozService
    {
        private static readonly SpeechSynthesizer _sintetizador = new SpeechSynthesizer();
        private static readonly object _candado = new object();

        public void CantarPedido(Pedido p)
        {
            if (p == null) return;

            try
            {
                string mensaje = ConstruirMensaje(p);

                lock (_candado)
                {
                    if (_sintetizador.State == SynthesizerState.Ready)
                    {
                        _sintetizador.SetOutputToDefaultAudioDevice();
                    }

                    _sintetizador.Rate = 0;
                    _sintetizador.Volume = 100;
                    _sintetizador.SpeakAsync(mensaje);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("VozService: no se pudo reproducir el pedido. " + ex.Message);
            }
        }

        private string ConstruirMensaje(Pedido p)
        {
            StringBuilder frase = new StringBuilder();
            frase.Append($"Atención cocina. Nuevo pedido para la mesa {p.NumeroMesa}. ");

            if (p.Detalles != null)
            {
                foreach (var det in p.Detalles)
                {
                    frase.Append($"{det.Cantidad} {det.NombrePlato}. ");

                    if (!string.IsNullOrWhiteSpace(det.NotasEspeciales))
                    {
                        frase.Append($"Con nota especial: {det.NotasEspeciales}. ");
                    }
                }
            }

            return frase.ToString();
        }
    }
}
