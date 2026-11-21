# SistemaKDS

Sistema de comandas para restaurante: el mesero toma el pedido, la cocina lo ve en su monitor (y lo escucha por voz) y el cliente puede pedir desde su celular escaneando el sticker NFC o el QR de la mesa.

## Qué hay en cada proyecto

| Proyecto | Contenido |
|---|---|
| `KDS.Entidades` | Clases `Pedido`, `DetallePedido`, `Plato`, `Usuario` |
| `KDS.Datos` | Acceso a PostgreSQL (`PedidoDAL`, `PlatoDAL`, `UsuarioDAL`) |
| `KDS.Logica` | Reglas de negocio (`PedidoBLL`, `PlatoBLL`, `VozService`) |
| `KDS.Presentacion` | App WPF (.NET 10): mesero, cocina, administrador de platos y el servidor del menú web |

## Puesta en marcha

1. En pgAdmin abre el Query Tool sobre `bd_kds` y ejecuta **solo** `BD/actualizacion_menu_web.sql` (no el script con el que se crearon las tablas: ese da *la relación «usuarios» ya existe* porque las tablas ya están). Agrega el estado `Solicitado`, el usuario Administrador (el dueño), la clave del dueño y la categoría de cada plato (a los platos que ya existían se les pone una según el nombre). Se puede correr varias veces sin problema.
2. El programa se conecta a `bd_kds` con `postgres` / `1234` en `localhost:5432` y al abrir aplica solo `BD/actualizacion_menu_web.sql`, así que las columnas nuevas se crean sin hacer nada. Si el servidor usa otro puerto, se cambia en `KDS.Presentacion/Vistas/KdsDatosService.cs`.
3. Abre `SistemaKDS.slnx` en Visual Studio y ejecuta con F5. Al arrancar se enciende el menú web en `http://localhost:8080`. En la parte de abajo de la ventana principal sale si quedó activo; si le das clic se abre en el navegador con la mesa 1.

## Dónde ver el menú web y el asistente

Al correr el proyecto, la barra de abajo de la ventana principal dice **MENÚ WEB ACTIVO: http://localhost:8080**. Al hacerle clic se abre el menú de la mesa 1 en el navegador. El asistente es el muñequito Plati (o el botón **💬 Plati**) del menú. Para verlo como en un celular, en el navegador se oprime F12 y luego el ícono del celular.

## Asistente del menú con Claude

El chat del menú web usa Claude para recomendar platos según el presupuesto o lo que pida el cliente ("tengo 25.000 y sin cebolla"). Solo recomienda platos que estén activos en la base de datos, y si el cliente pide quitar algo, esa nota viaja con el pedido a cocina.

Para activarlo hay que crear la variable de entorno `ANTHROPIC_API_KEY` con la clave de [console.anthropic.com](https://console.anthropic.com) antes de abrir Visual Studio:

```cmd
setx ANTHROPIC_API_KEY "sk-ant-..."
```

Después de `setx` hay que cerrar y volver a abrir Visual Studio. La clave nunca va en el código ni en el repositorio.

Sin la variable, el asistente sigue funcionando con reglas sencillas (lee el presupuesto, filtra por bebidas o hamburguesas y arma combinaciones que alcancen). La ventana principal dice cuál de los dos modos está usando.

### Plati, el muñequito del asistente

El asistente es un personaje animado, Plati, que aparece en la esquina del menú y en el chat:

- Mira hacia donde está el dedo o el mouse, parpadea y saluda de vez en cuando.
- Al tocarlo dice una frase con voz.
- Cuando el cliente le habla pone cara de escuchar, mientras busca en la carta pone cara de pensar, mueve la boca al responder y salta de felicidad cuando se agrega un plato.
- El botón **🔊 Voz activada** apaga o prende la voz.

### Mensajes de voz

En el chat está el botón **🎤**. El cliente lo toca, habla normal ("tengo veinte lucas y sin cebolla") y al callarse el mensaje se envía solo. Plati responde por escrito y en voz alta.

- Funciona en Chrome de Android y en Safari de iPhone. Necesita internet en el celular y el link `https` del túnel (o `localhost` en el computador).
- La primera vez el celular pide permiso para usar el micrófono.
- En navegadores que no entienden audio (por ejemplo Firefox), Plati le dice al cliente que use el micrófono del teclado para dictar.

### Hablarle como se habla en la calle

El asistente entiende cómo escribe la gente normalmente, con Claude y también sin conexión:

- Plata: "25 lucas", "30 barras", "20k", "veinte mil", "25.000".
- Abreviaturas y errores: "q", "pa", "porfa", "xq", "kiero", "amburguesa", "una limonda".
- Diminutivos y marcas: "gaseosita", "papitas", "una cocacola", "una pola".
- Antojos: "tengo sed", "algo pa picar", "me muero de hambre", "estoy pelado".
- Cuántos son: "somos 3 y tenemos 60 mil" reparte el presupuesto por persona.
- Cambios al plato: "sin cebolla", "no le eche tomate", "bien asada", "extra queso". Eso queda como nota del pedido para cocina.

## Flujo completo

1. **Dueño:** en la pantalla del mesero, botón **ADMIN PLATOS**. Pide la clave del dueño: la primera vez se crea ahí mismo y después siempre se pide, así los meseros no pueden cambiar platos ni precios. Tras 5 intentos fallidos hay que esperar 30 segundos. Dentro se agregan, modifican o eliminan platos, y cada plato debe tener categoría **Comida**, **Bebida** o **Acompañamiento**. Con **Cambiar mi clave** el dueño pone una nueva. Si un plato ya tiene pedidos guardados, al eliminarlo queda como agotado para no dañar el historial.
2. **Cliente:** abre el menú web desde el sticker (`/?mesa=2`), arma su pedido, puede preguntarle al asistente y toca **Enviar solicitud al mesero**. El pedido queda en estado `Solicitado` y el cliente ve en su celular cuando lo aprueban, se prepara y está listo.
3. **Mesero:** la solicitud aparece en el recuadro **Solicitudes del cliente**. Con **Aprobar y enviar a cocina** pasa a `Pendiente`; con **Rechazar** se borra.
4. **Cocina:** a los 3 segundos aparece la tarjeta, se lee en voz alta y el chef la avanza a Preparando y Listo.

## Beeper digital

Cuando el cliente envía su pedido, el celular se convierte en un beeper como los de los restaurantes: muestra el número del pedido y los pasos (Enviado, Aprobado, En cocina, ¡Listo!). La persona se puede alejar de la mesa.

- **Activar avisos en este celular:** pide permiso de notificaciones. Desde ahí, cuando el chef marca el pedido como **Listo**, le llega un mensaje al celular que vibra aunque tenga la pantalla bloqueada o el navegador cerrado. También avisa cuando el mesero lo aprueba, cuando entra a preparación y si lo rechaza.
- **Con la página abierta:** el celular vibra y pita cada pocos segundos hasta que el cliente toca **Ya lo recogí**.
- **Mantener pantalla encendida:** evita que el celular se bloquee mientras espera.
- **Probar vibración:** para que el cliente revise que su celular sí vibra.

Detalles a tener en cuenta:

- Las notificaciones solo funcionan con la dirección `https://` del túnel. En `http://localhost` funcionan desde el mismo computador, pero no desde un celular.
- En iPhone (iOS 16.4 o superior) primero hay que abrir el menú en Safari, tocar **Compartir → Agregar a inicio** y abrir el menú desde ese ícono; así sí llegan las notificaciones. El iPhone no deja que una página web vibre, así que ahí el aviso es la notificación con su sonido.
- Las claves de las notificaciones se generan solas la primera vez y se guardan en `%LOCALAPPDATA%\SistemaKDS`. No hay que configurar nada.

## Link para los celulares (sin abrir el cmd)

El programa abre el túnel solo al arrancar y lo cierra al salir. La barra de abajo de la ventana principal muestra **LINK PARA LOS NFC**; con un clic se copia. Hay tres formas, de la más fácil a la más profesional:

### Opción 1: link permanente gratis con ngrok (recomendada)

El link no cambia nunca, así que los stickers se graban una sola vez.

1. Crear una cuenta gratis en [ngrok.com](https://ngrok.com).
2. En el panel de ngrok, en **Domains**, copiar el dominio gratis que te asignan (algo como `nombre-raro.ngrok-free.dev`).
3. En el cmd, una sola vez:

```cmd
winget install ngrok.ngrok
ngrok config add-authtoken TU_TOKEN
setx KDS_NGROK_DOMINIO "nombre-raro.ngrok-free.dev"
```

El token sale en el panel de ngrok, en **Your Authtoken**. Después de `setx` hay que cerrar y abrir Visual Studio. Desde ahí, cada vez que se abre el programa la barra dice `LINK PARA LOS NFC: https://nombre-raro.ngrok-free.dev/?mesa=1 · FIJO`.

En la cuenta gratis, la primera vez que un celular abre el link ngrok muestra una página de aviso con el botón **Visit Site**; se toca una vez y ya entra al menú.

### Opción 2: dominio propio con Cloudflare

Si compran un dominio (por ejemplo `mirestaurante.com`, desde unos 10 dólares al año), Cloudflare da un link permanente sin página de aviso que queda instalado como servicio de Windows y arranca solo con el computador:

1. Pasar el dominio a Cloudflare y entrar a **Zero Trust → Networks → Tunnels → Create a tunnel**.
2. En **Public hostname** poner `menu.mirestaurante.com`, servicio `HTTP`, URL `localhost:8080`, y en **Additional settings → HTTP Settings** poner `localhost:8080` en *HTTP Host Header*.
3. Cloudflare muestra un comando `cloudflared service install ...`; se pega en un cmd abierto como administrador.
4. Para que el programa muestre ese link: `setx KDS_LINK_PUBLICO "https://menu.mirestaurante.com"`.

### Opción 3: link temporal automático

Si no se configura nada pero `cloudflared` está instalado (`winget install --id Cloudflare.cloudflared`), el programa abre un túnel rápido solo. La barra muestra el link en naranja con el aviso *CAMBIA CADA VEZ QUE ABRES EL PROGRAMA*, así que con esta opción hay que volver a grabar los stickers cada vez.

## Stickers NFC

En la app NFC Tools: **Escribir → Añadir registro → URL/URI** y se pega la dirección del túnel con la mesa al final:

- Mesa 1: `https://tu-enlace.trycloudflare.com/?mesa=1`
- Mesa 2: `https://tu-enlace.trycloudflare.com/?mesa=2`

Como respaldo conviene imprimir un QR con la misma dirección por si algún celular no tiene NFC.
