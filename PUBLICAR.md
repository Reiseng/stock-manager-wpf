# Cómo publicar una versión nueva

El programa se instala con `StockManager-win-Setup.exe` y, cada vez que se abre,
revisa en GitHub Releases si hay una versión más nueva. Si la hay, muestra un aviso:

- **Actualizar ahora**: descarga, instala y vuelve a abrir el programa.
- **Al cerrar el programa**: descarga en segundo plano y la instala cuando se cierra.
- **Más tarde**: no hace nada; vuelve a preguntar la próxima vez que se abra.

Sin internet no pasa nada: el programa funciona igual y no muestra ningún aviso.

## Publicar (automático, recomendado)

1. Subí el número de versión. Debe ser **mayor** que la anterior (1.0.0 → 1.1.0).
2. Opcional: escribí en `notas-de-version.md` qué cambió. Se muestra en el aviso.
3. Hacé commit y push, y después creá el tag:

   ```
   git tag v1.1.0
   git push origin v1.1.0
   ```

4. GitHub Actions (pestaña **Actions** del repo) arma el instalador y crea el Release.
   Tarda unos minutos.

## Publicar desde tu PC

```
.\build\publicar.ps1 -Version 1.1.0          # solo arma el instalador en .\Releases
.\build\publicar.ps1 -Version 1.1.0 -Subir   # además lo publica (necesita GITHUB_TOKEN)
```

## Primera instalación en una PC

Descargá `StockManager-win-Setup.exe` del último Release y ejecutalo. No pide permisos
de administrador. Como el instalador no está firmado, la primera vez Windows puede mostrar
"Windows protegió su PC": tocá **Más información → Ejecutar de todas formas**.

## Dónde quedan los datos

En `%LocalAppData%\StockManagerData` (base de datos, copias de seguridad y configuración).
Esa carpeta **no** se borra al actualizar ni al desinstalar.

## Cambios en la base de datos

Si una versión nueva necesita cambiar la base, agregá una migración al final de la lista
en `Services/DataBaseService.cs`. Se aplica sola al abrir la versión nueva, con una copia
de seguridad previa. Nunca modifiques una migración que ya se haya publicado.
