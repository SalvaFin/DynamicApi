# Dynamic systemd services

Servicios para ejecutar la API y el reverse proxy YARP en el VPS.

Rutas esperadas:

- API: `/home/dynamic/DynamicApi`
- Proxy: `/opt/appdynamic/proxy`

Usuario esperado:

- API: `dynamic`
- Proxy: `appdynamic`

## Primera instalacion en el VPS

```bash
sudo mkdir -p /home/dynamic/DynamicApi /opt/appdynamic/proxy
sudo chown -R dynamic:dynamic /home/dynamic/DynamicApi
sudo useradd --system --home /opt/appdynamic --shell /usr/sbin/nologin appdynamic
sudo chown -R appdynamic:appdynamic /opt/appdynamic
```

Sube los binarios publicados:

```bash
sudo rsync -av --delete ./publish-api/ /home/dynamic/DynamicApi/
sudo rsync -av --delete ./publish-proxy/ /opt/appdynamic/proxy/
sudo chown -R dynamic:dynamic /home/dynamic/DynamicApi
sudo mkdir -p /home/dynamic/DynamicApi/uploads
sudo chown -R dynamic:dynamic /home/dynamic/DynamicApi/uploads
sudo chmod 755 /home/dynamic/DynamicApi /home/dynamic/DynamicApi/uploads
sudo chown -R appdynamic:appdynamic /opt/appdynamic
sudo chmod +x /home/dynamic/DynamicApi/DynamicApi
sudo chmod +x /opt/appdynamic/proxy/Dynamic.Proxy
```

Instala los servicios:

```bash
sudo cp dynamic-api.service /etc/systemd/system/dynamic-api.service
sudo cp dynamic-proxy.service /etc/systemd/system/dynamic-proxy.service
sudo systemctl daemon-reload
sudo systemctl enable dynamic-api dynamic-proxy
sudo systemctl start dynamic-api dynamic-proxy
```

Comprobacion:

```bash
sudo systemctl status dynamic-api --no-pager
sudo systemctl status dynamic-proxy --no-pager
curl -I http://127.0.0.1:16000/api/users/auth/me
curl -I -H "Host: appdynamic.es" http://127.0.0.1:15000/
```

Logs:

```bash
sudo journalctl -u dynamic-api -f
sudo journalctl -u dynamic-proxy -f
```

## Actualizar binarios

```bash
sudo systemctl stop dynamic-api dynamic-proxy
sudo rsync -av --delete ./publish-api/ /home/dynamic/DynamicApi/
sudo rsync -av --delete ./publish-proxy/ /opt/appdynamic/proxy/
sudo chown -R dynamic:dynamic /home/dynamic/DynamicApi
sudo mkdir -p /home/dynamic/DynamicApi/uploads
sudo chown -R dynamic:dynamic /home/dynamic/DynamicApi/uploads
sudo chmod 755 /home/dynamic/DynamicApi /home/dynamic/DynamicApi/uploads
sudo chown -R appdynamic:appdynamic /opt/appdynamic
sudo chmod +x /home/dynamic/DynamicApi/DynamicApi
sudo chmod +x /opt/appdynamic/proxy/Dynamic.Proxy
sudo systemctl start dynamic-api dynamic-proxy
```
