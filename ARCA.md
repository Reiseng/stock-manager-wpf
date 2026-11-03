# Facturación electrónica con ARCA: guía paso a paso

Stock Manager pide el **CAE** a ARCA por *web service* (WSFEv1) solo para las ventas que marcás con
**"Facturar en ARCA"** (tecla F9 en la caja). El resto queda como comprobante interno.

Para conectarse hace falta un **certificado digital** del CUIT del negocio. Hay dos entornos, cada uno
con su propio certificado:

| Entorno | Para qué | Dónde se saca el certificado |
|---|---|---|
| **Homologación (prueba)** | Probar sin emitir facturas reales | WSASS - Autogestión Certificados Homologación |
| **Producción** | Facturas reales | Administración de Certificados Digitales |

Empezá siempre por homologación. Los nombres de los menús de ARCA pueden cambiar un poco con el tiempo.

---

## Antes de empezar

- **Clave fiscal nivel 3** del titular del CUIT del negocio.
- En Stock Manager, **Configuración > Datos del negocio**: el **CUIT** y la **condición frente al IVA**
  (Responsable Inscripto o Monotributista) tienen que estar bien cargados.

### ¿Ya lo tienen?

Entrá a ARCA con la clave fiscal y fijate en la lista de servicios:
- Si aparece **"Administración de Certificados Digitales"** y adentro hay algún alias, ya existe un certificado
  de producción. Igual conviene generar uno nuevo para este programa: el que existe tiene su clave privada
  en otra PC o en otro sistema.
- En **"Administración de puntos de venta y domicilios"** se ven los puntos de venta y con qué sistema
  emiten. Para este programa hace falta uno de **web services** (ver el paso 3 de producción).

---

## Homologación (prueba)

1. **Generar el pedido en el programa.** En Configuración > Facturación electrónica: activá la facturación,
   elegí **Prueba (homologación)**, poné un nombre (alias), por ejemplo `stockmanager`, y tocá
   **"1. Generar pedido (.csr)"**. Guardá el archivo.
2. **Habilitar WSASS en ARCA.** Entrá con clave fiscal, andá a **Administrador de Relaciones de Clave Fiscal >
   Adherir servicio > ARCA > Servicios interactivos > "WSASS - Autogestión Certificados Homologación"**.
   Puede tardar unos minutos en aparecer.
3. **Crear el certificado.** Entrá a **WSASS > Nuevo certificado**: poné el mismo alias y pegá el contenido
   del archivo `.csr` (abrilo con el Bloc de notas y copiá todo, incluidas las líneas `BEGIN` y `END`).
   ARCA muestra el certificado: copialo y guardalo como `stockmanager.crt`.
4. **Autorizar el servicio.** En WSASS, **Crear autorización a servicio**: elegí el alias, el servicio
   **wsfe (Facturación Electrónica)** y la CUIT del negocio.
5. **Importar en el programa:** **"2. Importar certificado"** y elegí el `.crt`.
6. **Probar conexión.** Tiene que decir *"Todo en orden (homologación)"*. En homologación el punto de venta
   puede ser cualquier número, por ejemplo `1`.
7. Hacé una venta con **"Facturar en ARCA"** tildado. El PDF va a decir *"HOMOLOGACIÓN: SIN VALIDEZ FISCAL"*.

## Producción (facturas reales)

1. **Generar un pedido nuevo en el programa**, ahora con **Producción** elegido. Es otro certificado, distinto
   del de prueba.
2. **Habilitar el servicio de certificados:** Administrador de Relaciones de Clave Fiscal > Adherir servicio >
   **"Administración de Certificados Digitales"**.
3. **Crear el punto de venta para web services:** **"Administración de puntos de venta y domicilios"** >
   Agregar. Elegí el sistema de **web services**. Suele llamarse *"RECE para aplicativo y web services"*
   para Responsables Inscriptos y *"Factura Electrónica - Monotributo - Web Services"* para monotributistas.
   Un punto de venta de "Comprobantes en línea" **no sirve** para el programa. Anotá el número.
4. **Obtener el certificado:** en **Administración de Certificados Digitales > Agregar alias**: alias + subí el
   `.csr`. Después descargá el certificado (`.crt`).
5. **Autorizar el servicio:** **Administrador de Relaciones de Clave Fiscal > Nueva relación > Buscar >
   ARCA > WebServices > "Facturación Electrónica"**. Como *representante* elegí el **computador fiscal** con el
   alias del paso 4 y confirmá.
6. **En el programa:** importá el `.crt`, poné el **número de punto de venta** del paso 3 y tocá
   **Probar conexión**. Tiene que decir *"Todo en orden (producción)"*.

El certificado dura unos dos años. Cuando vence, Configuración lo indica y la caja no deja facturar en ARCA hasta cargar uno nuevo (pasos 1, 4 y 6).

---

## Cómo funciona en el día a día

- **Caja:** "Facturar en ARCA" (F9) viene destildado. Se puede cambiar en Configuración. El programa elige
  solo la letra: **A** si el cliente es Responsable Inscripto o Monotributista con CUIT, **B** para el resto
  y **C** si el negocio es monotributista.
- **Sin internet o con ARCA caído:** la venta se guarda igual como *"Pendiente de CAE"* y se reintenta sola
  al abrir el programa y después de cada venta.
- **Rechazos:** quedan en el Historial de ventas con el motivo. Corregí el dato (por ejemplo, el documento
  o la condición frente al IVA del cliente) y usá **"Facturar en ARCA"** desde el historial.
- **Facturar después:** una venta interna se puede facturar desde el historial. Se pide el CAE con la fecha
  de ese día.
- **Notas de crédito y débito:** solo van a ARCA si la factura original tiene CAE, y salen con la misma letra.
- **Consumidor final desde $10.000.000:** hay que identificar al cliente con DNI o CUIL (RG 5824/2026).
- **Varias cajas:** cada PC necesita su propio punto de venta y su propio certificado.

> Qué operaciones corresponde facturar y cuándo lo define el contador del negocio. El programa solo
> permite elegirlo venta por venta.
