using KDS.Entidades;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;

namespace KDS.Logica
{
    /// <summary>
    /// Asistente auditivo de la cocina: lee en voz alta los pedidos nuevos
    /// usando el sintetizador de voz de Windows (System.Speech).
    /// </summary>
    public class VozService
    {
        // Instancia única compartida: evita solapamientos de audio y bloqueos de recursos.
        private static readonly SpeechSynthesizer _sintetizador = new SpeechSynthesizer();

        // Objeto de bloqueo para proteger la configuración del sintetizador.
        private static readonly object _candado = new object();

        /// <summary>
        /// Lee en voz alta un pedido de forma asíncrona (no congela la interfaz).
        /// Nunca lanza excepciones: si no hay audio, simplemente no suena.
        /// </summary>
        public void CantarPedido(Pedido p)
        {
            // Validación defensiva: no hay nada que leer.
            if (p == null) return;

            try
            {
                string mensaje = ConstruirMensaje(p);

                lock (_candado)
                {
                    // Solo se reconfigura la salida si el sintetizador está libre;
                    // cambiar el dispositivo mientras habla provocaría una excepción.
                    if (_sintetizador.State == SynthesizerState.Ready)
                    {
                        _sintetizador.SetOutputToDefaultAudioDevice();
                    }

                    _sintetizador.Rate = 0;      // Velocidad normal (rango -10 a 10)
                    _sintetizador.Volume = 100;  // Volumen máximo

                    // SpeakAsync encola el mensaje y reproduce en segundo plano.
                    _sintetizador.SpeakAsync(mensaje);
                }
            }
            catch (Exception ex)
            {
                // El equipo puede no tener dispositivo de audio o voz instalada.
                // Se registra en la ventana de depuración y la aplicación sigue funcionando.
                System.Diagnostics.Debug.WriteLine("VozService: no se pudo reproducir el pedido. " + ex.Message);
            }
        }

        /// <summary>
        /// Arma el texto que se va a leer a partir de los datos del pedido.
        /// </summary>
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
