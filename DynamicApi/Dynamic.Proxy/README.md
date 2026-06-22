# Dynamic.Proxy

Reverse proxy YARP para publicar `appdynamic.es` desde un unico frontal:

- `/Api` y `/Api/*` se envian a la API local `http://127.0.0.1:16000`, reescribiendo el prefijo publico a `/api`.
- `/Api/negocios-media/*` se envia a `/negocios-media/*` en la API para servir media publica del backend sin abrir otro prefijo en el dominio.
- `/negocios-media/*` se envia directamente a la API para servir imagenes publicas de negocios.
- El resto de rutas se envian a `https://cdn.appdynamic.es`.

## Despliegue recomendado

1. Publica la API y el proxy como servicios separados:

   ```powershell
   dotnet publish DynamicApi/DynamicApi/DynamicApi.csproj -c Release -o artifacts/publish/api
   dotnet publish DynamicApi/Dynamic.Proxy/Dynamic.Proxy.csproj -c Release -o artifacts/publish/proxy
   ```

2. En el VPS, ejecuta la API con `ASPNETCORE_ENVIRONMENT=Production`; escuchara solo en `127.0.0.1:16000`.

3. Ejecuta `Dynamic.Proxy` con `ASPNETCORE_ENVIRONMENT=Production`; escuchara en `127.0.0.1:15000`.

4. En Vesta/Nginx/Apache, configura `appdynamic.es` para terminar TLS y reenviar a `http://127.0.0.1:15000`.

5. Mantén cerrado el puerto `16000` en el firewall. La API debe ser accesible solo desde el propio VPS.

## Seguridad incluida

- Host allow-list para `appdynamic.es` y `www.appdynamic.es`.
- Kestrel sin cabecera `Server`.
- Limite de cuerpo de peticion de 10 MB y timeout corto de cabeceras.
- Rate limit de API: 120 peticiones por minuto.
- Bloqueo de metodos `TRACE` y `CONNECT`.
- Eliminacion de cabeceras `Forwarded` entrantes antes de reenviar al backend.
- Cabeceras defensivas: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` y HSTS cuando la peticion llega como HTTPS.
