const VIBRACION_LISTO = [600, 200, 600, 200, 600, 200, 1200];
const VIBRACION_SUAVE = [200, 100, 200];

self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", evento => evento.waitUntil(self.clients.claim()));

self.addEventListener("push", evento => {
  let aviso = { titulo: "Menú KDS", cuerpo: "Hay novedades con tu pedido.", tipo: "info", pedido: 0, url: "/" };
  try { aviso = { ...aviso, ...evento.data.json() }; } catch (e) {}

  const listo = aviso.tipo === "listo";

  evento.waitUntil((async () => {
    const ventanas = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
    ventanas.forEach(v => v.postMessage({ tipo: "estado", aviso }));

    await self.registration.showNotification(aviso.titulo, {
      body: aviso.cuerpo,
      icon: "/icono.svg",
      badge: "/icono.svg",
      tag: `pedido-${aviso.pedido}`,
      renotify: true,
      requireInteraction: listo,
      vibrate: listo ? VIBRACION_LISTO : VIBRACION_SUAVE,
      data: { url: aviso.url, pedido: aviso.pedido }
    });
  })());
});

self.addEventListener("notificationclick", evento => {
  evento.notification.close();
  const destino = (evento.notification.data && evento.notification.data.url) || "/";

  evento.waitUntil((async () => {
    const ventanas = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
    for (const v of ventanas) {
      if ("focus" in v) {
        v.postMessage({ tipo: "abrir-beeper" });
        return v.focus();
      }
    }
    return self.clients.openWindow(destino);
  })());
});
