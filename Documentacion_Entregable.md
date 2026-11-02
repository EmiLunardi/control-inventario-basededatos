# Documentación Técnica: Sistema de Control de Inventario y Stock

## 1. Descripción General del Proyecto
El proyecto consiste en una aplicación web integral diseñada para llevar el control del inventario, abastecimiento y stock por sucursal de una distribuidora. El sistema proporciona un **Tablero de Control (Dashboard)** con semaforización y navegación multinivel (*Drill-Down / Drill-Up*), acompañado de **tres prototipos operativos adicionales** para la gestión del catálogo de productos, el registro auditado de movimientos de stock y la administración de proveedores y órdenes de compra.

Está construido utilizando el patrón arquitectónico **MVC (Modelo-Vista-Controlador)** sobre la plataforma **ASP.NET Core (C#)** y base de datos **PostgreSQL** mediante **Dapper**.

---

## 2. Prototipos de Interfaz de Usuario (UI) Incluidos

El sistema cuenta con la pantalla de inicio de sesión y **4 prototipos funcionales** accesibles desde la barra de navegación superior:

1. **Prototipo 1 — Tablero de Control Principal (`Views/Home/Index.cshtml`)**:
   * **Indicadores Funcionando (KPIs en tiempo real):** Calcula dinámicamente la cantidad y el porcentaje de artículos en **Stock Óptimo**, **Stock en Advertencia** y **Stock Crítico**, junto con el **Total de Movimientos Auditados**, **Unidades Totales** y **Valorizado de Inventario ($)**. Todos los indicadores se recalculan al filtrar por sucursal o al navegar entre los niveles.
   * **Semaforización basada en la Regla del 10%** funcionando en todos los niveles (Global, por Categoría, por Producto y por Sucursal):
     * **Óptimo (Verde):** El `StockActual` supera en más de un 10% el umbral de cada producto (`StockActual > StockMinimo * 1.10`).
     * **Advertencia (Amarillo):** El `StockActual` se encuentra a un **10% o menos** de caer en el quiebre de stock (`StockMinimo < StockActual <= StockMinimo * 1.10`).
     * **Crítico (Rojo):** El `StockActual` está en o por debajo del `StockMinimo` (o $\le 10\%$ del `StockOptimo`).
   * **Visualizaciones Interactivas Funcionando (Chart.js):**
     * *Gráfico de Dona:* Distribución porcentual de la semaforización del nivel actual (permite hacer clic en un sector para hacer *Drill-Down* por estado semafórico).
     * *Gráfico de Barras Dinámico:* En Nivel 1 compara `Stock Disponible vs. Umbral Mínimo` por Categoría; en Nivel 2 compara `Stock Actual vs. Stock Mínimo` de cada Producto de la categoría seleccionada; en Nivel 3 compara `Existencia Física vs. Punto de Reorden` por Sucursal.
   * **Navegación Multinivel con Drill-Down y Drill-Up en 3 niveles** (con barra de *Breadcrumbs* y botones de subida y bajada en tablas, KPIs y gráficos):
     * *Nivel 1 (Global / Categorías)*: Resumen de categorías con desglose de semáforo (`🟢 / 🟡 / 🔴`), unidades, valorizado y estado consolidado de cada categoría.
     * *Nivel 2 (Productos de la Categoría o Estado seleccionado)*: Listado filtrado de productos con cálculo de margen porcentual sobre el mínimo y estado semafórico individual.
     * *Nivel 3 (Trazabilidad del Producto)*: Ficha técnica del artículo, existencias y semáforo desglosados por Sucursal (`StockSucursales`) e historial real de movimientos (`MovimientosStock`) de ese producto.
2. **Prototipo 2 — Gestión de Catálogo de Productos (`Views/Home/Productos.cshtml`)**:
   * Pantalla funcional de administración maestra de artículos (ABM), con búsqueda interactiva en tiempo real por código SKU o nombre, filtrado por Categoría, Proveedor y Estado de Semáforo, alta de nuevos productos, ajuste de stock/umbrales y baja de artículos con impacto directo en el Tablero de Control.
3. **Prototipo 3 — Registro y Auditoría de Movimientos de Stock (`Views/Home/Movimientos.cshtml`)**:
   * Pantalla operativa funcional que permite asentar nuevas operaciones (`ENTRADA`, `SALIDA`, `AJUSTE`) especificando sucursal, producto, cantidad y comprobante/motivo; actualiza automáticamente las existencias por sucursal (`StockSucursales`), el stock consolidado del producto y el semáforo del Tablero.
4. **Prototipo 4 — Gestión de Proveedores y Órdenes de Compra (`Views/Home/ProveedoresOrdenes.cshtml`)**:
   * Pantalla de abastecimiento funcional para dar de alta proveedores homologados (`Proveedores`), emitir órdenes de reposición (`OrdenesCompra`) y recibir mercadería con un clic (incrementando automáticamente el stock en la sucursal destino y registrando el movimiento de entrada).

---

## 3. Base de Datos y Modelo Canónico (12 Entidades)

La persistencia de datos se realiza en **PostgreSQL** (esquema completo en `database_schema.sql`) y cada tabla tiene su correspondiente entidad en C# dentro de la carpeta `Models/`:

1. **`Roles`** (`Models/Rol.cs`): Perfiles de usuario del sistema.
2. **`Permisos`** (`Models/Permiso.cs`): Permisos granulares sobre los módulos.
3. **`RolesPermisos`** (`Models/RolPermiso.cs`): Asociación N:M entre roles y permisos.
4. **`Sucursales`** (`Models/Sucursal.cs`): Depósitos o puntos físicos de distribución.
5. **`Usuarios`** (`Models/Usuario.cs`): Cuentas de acceso con contraseña encriptada (`PasswordHash` en SHA-256) y asociación a rol y sucursal.
6. **`Categorias`** (`Models/Categoria.cs`): Familias de clasificación de artículos.
7. **`Proveedores`** (`Models/Proveedor.cs`): Empresas abastecedoras (CUIT, razón social, contacto).
8. **`Productos`** (`Models/Producto.cs`): Catálogo maestro con SKU, costo, precio de venta, proveedor habitual y stock mínimo.
9. **`StockSucursales`** (`Models/StockSucursal.cs`): Existencia actual y punto de reorden de cada producto en cada sucursal.
10. **`MovimientosStock`** (`Models/MovimientoStock.cs`): Historial transaccional de entradas, salidas y ajustes auditado por usuario.
11. **`OrdenesCompra`** (`Models/OrdenCompra.cs`): Cabecera de pedidos de reposición a proveedores por sucursal destino.
12. **`DetallesOrdenCompra`** (`Models/DetalleOrdenCompra.cs`): Renglones de productos, cantidades solicitadas y costos unitarios de cada orden de compra.

---

## 4. Módulo de Autenticación y Encriptación de Contraseñas (SHA-256)

Las contraseñas **nunca se almacenan ni comparan en texto plano**:

* **Clase utilitaria (`Models/PasswordHelper.cs`)**: Utiliza `System.Security.Cryptography.SHA256` nativo de .NET para transformar cualquier contraseña ingresada en un hash hexadecimal irreversible de 64 caracteres (`EncriptarPassword`).
* **Validación en el Login (`Controllers/AccountController.cs`)**:
  1. Cuando el usuario envía su usuario y contraseña desde `LoginViewModel`, el controlador calcula primero el hash SHA-256:
     ```csharp
     string passwordHash = PasswordHelper.EncriptarPassword(model.Password);
     ```
  2. Luego consulta en PostgreSQL vía Dapper comparando únicamente el hash:
     ```sql
     SELECT Id, NombreUsuario, PasswordHash, Rol
     FROM Usuarios
     WHERE NombreUsuario = @Username AND PasswordHash = @PasswordHash
     ```
  3. Para el usuario de prueba `admin`, la contraseña `admin123` se almacena en la base de datos como el hash SHA-256:
     `240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9`.
  4. Al coincidir, se emite la cookie encriptada de sesión (`CookieAuthenticationDefaults.AuthenticationScheme`) con los *Claims* de nombre y rol del usuario.
