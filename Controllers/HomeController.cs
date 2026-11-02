using ControlInventario.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Diagnostics;

namespace ControlInventario.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _connectionString;

        // Estado persistente en memoria sincronizado con PostgreSQL durante la ejecución de la aplicación
        // para reflejar en tiempo real altas, bajas, movimientos de stock, recepción de órdenes y semaforización.
        private static readonly object _lock = new();
        private static bool _inicializado = false;

        private static List<Categoria> _categorias = new();
        private static List<Sucursal> _sucursales = new();
        private static List<Proveedor> _proveedores = new();
        private static List<Producto> _productos = new();
        private static List<StockSucursal> _stockSucursales = new();
        private static List<MovimientoStock> _movimientos = new();
        private static List<OrdenCompra> _ordenesCompra = new();

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            AsegurarDatosInicializados(_connectionString);
        }

        private static void AsegurarDatosInicializados(string connectionString)
        {
            lock (_lock)
            {
                if (_inicializado) return;

                _categorias = new List<Categoria>
                {
                    new Categoria { Id = 1, Nombre = "Bebidas", Descripcion = "Bebidas sin alcohol, aguas, gaseosas y cervezas" },
                    new Categoria { Id = 2, Nombre = "Almacén", Descripcion = "Productos secos, harinas, arroz, pastas y aceites" },
                    new Categoria { Id = 3, Nombre = "Lácteos", Descripcion = "Leches fluidas, quesos, yogures y mantecas refrigeradas" },
                    new Categoria { Id = 4, Nombre = "Limpieza", Descripcion = "Desinfectantes, detergentes, lavandinas e higiene" }
                };

                _sucursales = new List<Sucursal>
                {
                    new Sucursal { Id = 1, Nombre = "Central", Ubicacion = "Av. Pellegrini 1850, Rosario", Telefono = "0341-482-1000" },
                    new Sucursal { Id = 2, Nombre = "Sucursal Norte", Ubicacion = "Av. Alberdi 950, Rosario", Telefono = "0341-439-2200" },
                    new Sucursal { Id = 3, Nombre = "Sucursal Sur", Ubicacion = "Av. San Martín 3420, Rosario", Telefono = "0341-461-3500" }
                };

                _proveedores = new List<Proveedor>
                {
                    new Proveedor { Id = 1, RazonSocial = "Distribuidora Rosario Bebidas S.A.", Cuit = "30-54892104-9", Telefono = "0341-456-8890", Email = "ventas@rosariobebidas.com.ar", Direccion = "Av. Circunvalación y Av. Uriburu, Rosario" },
                    new Proveedor { Id = 2, RazonSocial = "Molinos del Litoral S.R.L.", Cuit = "30-61029384-2", Telefono = "0341-492-1102", Email = "pedidos@molinosdellitoral.com.ar", Direccion = "Parque Industrial Alvear, Ruta 21 Km 7, Santa Fe" },
                    new Proveedor { Id = 3, RazonSocial = "Lácteos Santa Fe S.A.", Cuit = "30-50129381-7", Telefono = "0341-431-5228", Email = "mayoristas@lacteossantafe.com.ar", Direccion = "Av. Ovidio Lagos 5200, Rosario" },
                    new Proveedor { Id = 4, RazonSocial = "Química San Lorenzo S.A.", Cuit = "30-70918273-4", Telefono = "0341-495-9012", Email = "contacto@quimicasanlorenzo.com.ar", Direccion = "Parque Industrial Pérez, Ruta 33 Km 788, Santa Fe" }
                };

                _productos = new List<Producto>
                {
                    // Bebidas (1 Óptimo, 1 Advertencia, 1 Crítico)
                    new Producto { Id = 1, CodigoSku = "SKU-BEB-001", Nombre = "Agua Mineral 1.5L", CategoriaId = 1, CategoriaNombre = "Bebidas", ProveedorId = 1, ProveedorNombre = "Distribuidora Rosario Bebidas S.A.", PrecioCosto = 520m, Precio = 800m, StockActual = 150, StockMinimo = 50, StockOptimo = 200 },
                    new Producto { Id = 2, CodigoSku = "SKU-BEB-002", Nombre = "Cerveza Lager 1L", CategoriaId = 1, CategoriaNombre = "Bebidas", ProveedorId = 1, ProveedorNombre = "Distribuidora Rosario Bebidas S.A.", PrecioCosto = 1650m, Precio = 2500m, StockActual = 105, StockMinimo = 100, StockOptimo = 250 }, // Advertencia (+5% sobre mínimo)
                    new Producto { Id = 3, CodigoSku = "SKU-BEB-003", Nombre = "Gaseosa Cola 2.25L", CategoriaId = 1, CategoriaNombre = "Bebidas", ProveedorId = 1, ProveedorNombre = "Distribuidora Rosario Bebidas S.A.", PrecioCosto = 1400m, Precio = 2150m, StockActual = 35, StockMinimo = 60, StockOptimo = 180 }, // Crítico

                    // Almacén (2 Óptimos, 1 Advertencia)
                    new Producto { Id = 4, CodigoSku = "SKU-ALM-001", Nombre = "Arroz Blanco 1kg", CategoriaId = 2, CategoriaNombre = "Almacén", ProveedorId = 2, ProveedorNombre = "Molinos del Litoral S.R.L.", PrecioCosto = 780m, Precio = 1200m, StockActual = 200, StockMinimo = 30, StockOptimo = 220 },
                    new Producto { Id = 5, CodigoSku = "SKU-ALM-002", Nombre = "Fideos Espagueti 500g", CategoriaId = 2, CategoriaNombre = "Almacén", ProveedorId = 2, ProveedorNombre = "Molinos del Litoral S.R.L.", PrecioCosto = 580m, Precio = 900m, StockActual = 43, StockMinimo = 40, StockOptimo = 120 }, // Advertencia (+7.5% sobre mínimo)
                    new Producto { Id = 6, CodigoSku = "SKU-ALM-003", Nombre = "Aceite de Girasol 1.5L", CategoriaId = 2, CategoriaNombre = "Almacén", ProveedorId = 2, ProveedorNombre = "Molinos del Litoral S.R.L.", PrecioCosto = 1850m, Precio = 2700m, StockActual = 110, StockMinimo = 45, StockOptimo = 150 },

                    // Lácteos (1 Crítico, 1 Advertencia, 1 Óptimo)
                    new Producto { Id = 7, CodigoSku = "SKU-LAC-001", Nombre = "Leche Entera 1L", CategoriaId = 3, CategoriaNombre = "Lácteos", ProveedorId = 3, ProveedorNombre = "Lácteos Santa Fe S.A.", PrecioCosto = 720m, Precio = 1100m, StockActual = 15, StockMinimo = 50, StockOptimo = 180 }, // Crítico
                    new Producto { Id = 8, CodigoSku = "SKU-LAC-002", Nombre = "Queso Cremoso Horma 4kg", CategoriaId = 3, CategoriaNombre = "Lácteos", ProveedorId = 3, ProveedorNombre = "Lácteos Santa Fe S.A.", PrecioCosto = 14200m, Precio = 21000m, StockActual = 22, StockMinimo = 20, StockOptimo = 60 }, // Advertencia (+10% exacto)
                    new Producto { Id = 9, CodigoSku = "SKU-LAC-003", Nombre = "Manteca Primera Calidad 200g", CategoriaId = 3, CategoriaNombre = "Lácteos", ProveedorId = 3, ProveedorNombre = "Lácteos Santa Fe S.A.", PrecioCosto = 1350m, Precio = 2100m, StockActual = 85, StockMinimo = 30, StockOptimo = 100 },

                    // Limpieza (2 Óptimos, 1 Crítico)
                    new Producto { Id = 10, CodigoSku = "SKU-LIM-001", Nombre = "Lavandina Concentrada 1L", CategoriaId = 4, CategoriaNombre = "Limpieza", ProveedorId = 4, ProveedorNombre = "Química San Lorenzo S.A.", PrecioCosto = 490m, Precio = 850m, StockActual = 120, StockMinimo = 20, StockOptimo = 150 },
                    new Producto { Id = 11, CodigoSku = "SKU-LIM-002", Nombre = "Detergente Vajilla 750ml", CategoriaId = 4, CategoriaNombre = "Limpieza", ProveedorId = 4, ProveedorNombre = "Química San Lorenzo S.A.", PrecioCosto = 820m, Precio = 1350m, StockActual = 95, StockMinimo = 35, StockOptimo = 120 },
                    new Producto { Id = 12, CodigoSku = "SKU-LIM-003", Nombre = "Desinfectante Pisos 5L", CategoriaId = 4, CategoriaNombre = "Limpieza", ProveedorId = 4, ProveedorNombre = "Química San Lorenzo S.A.", PrecioCosto = 2400m, Precio = 3900m, StockActual = 18, StockMinimo = 25, StockOptimo = 80 } // Crítico
                };

                // Desglose por sucursal coherente con el StockActual de cada producto
                _stockSucursales = new List<StockSucursal>
                {
                    // Prod 1: Agua Mineral (150 total = 80 + 45 + 25)
                    new StockSucursal { Id = 1, ProductoId = 1, CodigoSku = "SKU-BEB-001", ProductoNombre = "Agua Mineral 1.5L", CategoriaNombre = "Bebidas", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 80, PuntoReorden = 20 },
                    new StockSucursal { Id = 2, ProductoId = 1, CodigoSku = "SKU-BEB-001", ProductoNombre = "Agua Mineral 1.5L", CategoriaNombre = "Bebidas", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 45, PuntoReorden = 15 },
                    new StockSucursal { Id = 3, ProductoId = 1, CodigoSku = "SKU-BEB-001", ProductoNombre = "Agua Mineral 1.5L", CategoriaNombre = "Bebidas", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 25, PuntoReorden = 15 },

                    // Prod 2: Cerveza Lager (105 total = 50 + 33 + 22)
                    new StockSucursal { Id = 4, ProductoId = 2, CodigoSku = "SKU-BEB-002", ProductoNombre = "Cerveza Lager 1L", CategoriaNombre = "Bebidas", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 50, PuntoReorden = 40 },
                    new StockSucursal { Id = 5, ProductoId = 2, CodigoSku = "SKU-BEB-002", ProductoNombre = "Cerveza Lager 1L", CategoriaNombre = "Bebidas", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 33, PuntoReorden = 30 },
                    new StockSucursal { Id = 6, ProductoId = 2, CodigoSku = "SKU-BEB-002", ProductoNombre = "Cerveza Lager 1L", CategoriaNombre = "Bebidas", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 22, PuntoReorden = 30 },

                    // Prod 3: Gaseosa Cola (35 total = 15 + 12 + 8)
                    new StockSucursal { Id = 7, ProductoId = 3, CodigoSku = "SKU-BEB-003", ProductoNombre = "Gaseosa Cola 2.25L", CategoriaNombre = "Bebidas", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 15, PuntoReorden = 25 },
                    new StockSucursal { Id = 8, ProductoId = 3, CodigoSku = "SKU-BEB-003", ProductoNombre = "Gaseosa Cola 2.25L", CategoriaNombre = "Bebidas", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 12, PuntoReorden = 20 },
                    new StockSucursal { Id = 9, ProductoId = 3, CodigoSku = "SKU-BEB-003", ProductoNombre = "Gaseosa Cola 2.25L", CategoriaNombre = "Bebidas", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 8, PuntoReorden = 15 },

                    // Prod 4: Arroz Blanco (200 total = 110 + 50 + 40)
                    new StockSucursal { Id = 10, ProductoId = 4, CodigoSku = "SKU-ALM-001", ProductoNombre = "Arroz Blanco 1kg", CategoriaNombre = "Almacén", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 110, PuntoReorden = 15 },
                    new StockSucursal { Id = 11, ProductoId = 4, CodigoSku = "SKU-ALM-001", ProductoNombre = "Arroz Blanco 1kg", CategoriaNombre = "Almacén", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 50, PuntoReorden = 10 },
                    new StockSucursal { Id = 12, ProductoId = 4, CodigoSku = "SKU-ALM-001", ProductoNombre = "Arroz Blanco 1kg", CategoriaNombre = "Almacén", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 40, PuntoReorden = 5 },

                    // Prod 5: Fideos Espagueti (43 total = 20 + 11 + 12)
                    new StockSucursal { Id = 13, ProductoId = 5, CodigoSku = "SKU-ALM-002", ProductoNombre = "Fideos Espagueti 500g", CategoriaNombre = "Almacén", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 20, PuntoReorden = 18 },
                    new StockSucursal { Id = 14, ProductoId = 5, CodigoSku = "SKU-ALM-002", ProductoNombre = "Fideos Espagueti 500g", CategoriaNombre = "Almacén", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 11, PuntoReorden = 11 },
                    new StockSucursal { Id = 15, ProductoId = 5, CodigoSku = "SKU-ALM-002", ProductoNombre = "Fideos Espagueti 500g", CategoriaNombre = "Almacén", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 12, PuntoReorden = 11 },

                    // Prod 6: Aceite de Girasol (110 total = 60 + 30 + 20)
                    new StockSucursal { Id = 16, ProductoId = 6, CodigoSku = "SKU-ALM-003", ProductoNombre = "Aceite de Girasol 1.5L", CategoriaNombre = "Almacén", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 60, PuntoReorden = 20 },
                    new StockSucursal { Id = 17, ProductoId = 6, CodigoSku = "SKU-ALM-003", ProductoNombre = "Aceite de Girasol 1.5L", CategoriaNombre = "Almacén", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 30, PuntoReorden = 15 },
                    new StockSucursal { Id = 18, ProductoId = 6, CodigoSku = "SKU-ALM-003", ProductoNombre = "Aceite de Girasol 1.5L", CategoriaNombre = "Almacén", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 20, PuntoReorden = 10 },

                    // Prod 7: Leche Entera (15 total = 8 + 4 + 3)
                    new StockSucursal { Id = 19, ProductoId = 7, CodigoSku = "SKU-LAC-001", ProductoNombre = "Leche Entera 1L", CategoriaNombre = "Lácteos", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 8, PuntoReorden = 25 },
                    new StockSucursal { Id = 20, ProductoId = 7, CodigoSku = "SKU-LAC-001", ProductoNombre = "Leche Entera 1L", CategoriaNombre = "Lácteos", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 4, PuntoReorden = 15 },
                    new StockSucursal { Id = 21, ProductoId = 7, CodigoSku = "SKU-LAC-001", ProductoNombre = "Leche Entera 1L", CategoriaNombre = "Lácteos", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 3, PuntoReorden = 10 },

                    // Prod 8: Queso Cremoso (22 total = 11 + 6 + 5)
                    new StockSucursal { Id = 22, ProductoId = 8, CodigoSku = "SKU-LAC-002", ProductoNombre = "Queso Cremoso Horma 4kg", CategoriaNombre = "Lácteos", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 11, PuntoReorden = 10 },
                    new StockSucursal { Id = 23, ProductoId = 8, CodigoSku = "SKU-LAC-002", ProductoNombre = "Queso Cremoso Horma 4kg", CategoriaNombre = "Lácteos", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 6, PuntoReorden = 5 },
                    new StockSucursal { Id = 24, ProductoId = 8, CodigoSku = "SKU-LAC-002", ProductoNombre = "Queso Cremoso Horma 4kg", CategoriaNombre = "Lácteos", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 5, PuntoReorden = 5 },

                    // Prod 9: Manteca 200g (85 total = 45 + 25 + 15)
                    new StockSucursal { Id = 25, ProductoId = 9, CodigoSku = "SKU-LAC-003", ProductoNombre = "Manteca Primera Calidad 200g", CategoriaNombre = "Lácteos", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 45, PuntoReorden = 15 },
                    new StockSucursal { Id = 26, ProductoId = 9, CodigoSku = "SKU-LAC-003", ProductoNombre = "Manteca Primera Calidad 200g", CategoriaNombre = "Lácteos", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 25, PuntoReorden = 10 },
                    new StockSucursal { Id = 27, ProductoId = 9, CodigoSku = "SKU-LAC-003", ProductoNombre = "Manteca Primera Calidad 200g", CategoriaNombre = "Lácteos", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 15, PuntoReorden = 5 },

                    // Prod 10: Lavandina 1L (120 total = 65 + 35 + 20)
                    new StockSucursal { Id = 28, ProductoId = 10, CodigoSku = "SKU-LIM-001", ProductoNombre = "Lavandina Concentrada 1L", CategoriaNombre = "Limpieza", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 65, PuntoReorden = 10 },
                    new StockSucursal { Id = 29, ProductoId = 10, CodigoSku = "SKU-LIM-001", ProductoNombre = "Lavandina Concentrada 1L", CategoriaNombre = "Limpieza", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 35, PuntoReorden = 5 },
                    new StockSucursal { Id = 30, ProductoId = 10, CodigoSku = "SKU-LIM-001", ProductoNombre = "Lavandina Concentrada 1L", CategoriaNombre = "Limpieza", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 20, PuntoReorden = 5 },

                    // Prod 11: Detergente 750ml (95 total = 50 + 25 + 20)
                    new StockSucursal { Id = 31, ProductoId = 11, CodigoSku = "SKU-LIM-002", ProductoNombre = "Detergente Vajilla 750ml", CategoriaNombre = "Limpieza", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 50, PuntoReorden = 15 },
                    new StockSucursal { Id = 32, ProductoId = 11, CodigoSku = "SKU-LIM-002", ProductoNombre = "Detergente Vajilla 750ml", CategoriaNombre = "Limpieza", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 25, PuntoReorden = 10 },
                    new StockSucursal { Id = 33, ProductoId = 11, CodigoSku = "SKU-LIM-002", ProductoNombre = "Detergente Vajilla 750ml", CategoriaNombre = "Limpieza", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 20, PuntoReorden = 10 },

                    // Prod 12: Desinfectante Pisos 5L (18 total = 10 + 5 + 3)
                    new StockSucursal { Id = 34, ProductoId = 12, CodigoSku = "SKU-LIM-003", ProductoNombre = "Desinfectante Pisos 5L", CategoriaNombre = "Limpieza", SucursalId = 1, SucursalNombre = "Central", CantidadActual = 10, PuntoReorden = 12 },
                    new StockSucursal { Id = 35, ProductoId = 12, CodigoSku = "SKU-LIM-003", ProductoNombre = "Desinfectante Pisos 5L", CategoriaNombre = "Limpieza", SucursalId = 2, SucursalNombre = "Sucursal Norte", CantidadActual = 5, PuntoReorden = 8 },
                    new StockSucursal { Id = 36, ProductoId = 12, CodigoSku = "SKU-LIM-003", ProductoNombre = "Desinfectante Pisos 5L", CategoriaNombre = "Limpieza", SucursalId = 3, SucursalNombre = "Sucursal Sur", CantidadActual = 3, PuntoReorden = 5 }
                };

                _movimientos = new List<MovimientoStock>
                {
                    new MovimientoStock { Id = 1, ProductoId = 1, CodigoSku = "SKU-BEB-001", ProductoNombre = "Agua Mineral 1.5L", CategoriaNombre = "Bebidas", SucursalId = 1, SucursalNombre = "Central", UsuarioNombre = "admin", TipoMovimiento = "ENTRADA", Cantidad = 120, Motivo = "Recepción OC-2026-001", Fecha = DateTime.Now.AddDays(-8) },
                    new MovimientoStock { Id = 2, ProductoId = 1, CodigoSku = "SKU-BEB-001", ProductoNombre = "Agua Mineral 1.5L", CategoriaNombre = "Bebidas", SucursalId = 2, SucursalNombre = "Sucursal Norte", UsuarioNombre = "gerente_log", TipoMovimiento = "SALIDA", Cantidad = 25, Motivo = "Despacho Pedido Mayorista #1042", Fecha = DateTime.Now.AddDays(-3) },
                    new MovimientoStock { Id = 3, ProductoId = 2, CodigoSku = "SKU-BEB-002", ProductoNombre = "Cerveza Lager 1L", CategoriaNombre = "Bebidas", SucursalId = 1, SucursalNombre = "Central", UsuarioNombre = "admin", TipoMovimiento = "ENTRADA", Cantidad = 90, Motivo = "Abastecimiento mensual cervecería", Fecha = DateTime.Now.AddDays(-7) },
                    new MovimientoStock { Id = 4, ProductoId = 2, CodigoSku = "SKU-BEB-002", ProductoNombre = "Cerveza Lager 1L", CategoriaNombre = "Bebidas", SucursalId = 3, SucursalNombre = "Sucursal Sur", UsuarioNombre = "operador_sur", TipoMovimiento = "SALIDA", Cantidad = 35, Motivo = "Despacho bares zona sur #4412", Fecha = DateTime.Now.AddDays(-2) },
                    new MovimientoStock { Id = 5, ProductoId = 3, CodigoSku = "SKU-BEB-003", ProductoNombre = "Gaseosa Cola 2.25L", CategoriaNombre = "Bebidas", SucursalId = 1, SucursalNombre = "Central", UsuarioNombre = "operador_central", TipoMovimiento = "SALIDA", Cantidad = 45, Motivo = "Venta mayorista supermercados", Fecha = DateTime.Now.AddDays(-1) },
                    new MovimientoStock { Id = 6, ProductoId = 4, CodigoSku = "SKU-ALM-001", ProductoNombre = "Arroz Blanco 1kg", CategoriaNombre = "Almacén", SucursalId = 1, SucursalNombre = "Central", UsuarioNombre = "admin", TipoMovimiento = "ENTRADA", Cantidad = 200, Motivo = "Recepción OC-2026-002", Fecha = DateTime.Now.AddDays(-10) },
                    new MovimientoStock { Id = 7, ProductoId = 5, CodigoSku = "SKU-ALM-002", ProductoNombre = "Fideos Espagueti 500g", CategoriaNombre = "Almacén", SucursalId = 2, SucursalNombre = "Sucursal Norte", UsuarioNombre = "gerente_log", TipoMovimiento = "SALIDA", Cantidad = 30, Motivo = "Remito entrega #0004-00018290", Fecha = DateTime.Now.AddDays(-4) },
                    new MovimientoStock { Id = 8, ProductoId = 6, CodigoSku = "SKU-ALM-003", ProductoNombre = "Aceite de Girasol 1.5L", CategoriaNombre = "Almacén", SucursalId = 1, SucursalNombre = "Central", UsuarioNombre = "admin", TipoMovimiento = "ENTRADA", Cantidad = 80, Motivo = "Ingreso lote aceitera", Fecha = DateTime.Now.AddDays(-6) },
                    new MovimientoStock { Id = 9, ProductoId = 7, CodigoSku = "SKU-LAC-001", ProductoNombre = "Leche Entera 1L", CategoriaNombre = "Lácteos", SucursalId = 1, SucursalNombre = "Central", UsuarioNombre = "operador_central", TipoMovimiento = "SALIDA", Cantidad = 50, Motivo = "Distribución diaria comercios", Fecha = DateTime.Now.AddDays(-2) },
                    new MovimientoStock { Id = 10, ProductoId = 7, CodigoSku = "SKU-LAC-001", ProductoNombre = "Leche Entera 1L", CategoriaNombre = "Lácteos", SucursalId = 2, SucursalNombre = "Sucursal Norte", UsuarioNombre = "gerente_log", TipoMovimiento = "AJUSTE", Cantidad = -5, Motivo = "Merma por envase dañado en transporte", Fecha = DateTime.Now.AddDays(-1) },
                    new MovimientoStock { Id = 11, ProductoId = 8, CodigoSku = "SKU-LAC-002", ProductoNombre = "Queso Cremoso Horma 4kg", CategoriaNombre = "Lácteos", SucursalId = 3, SucursalNombre = "Sucursal Sur", UsuarioNombre = "operador_sur", TipoMovimiento = "SALIDA", Cantidad = 12, Motivo = "Despacho fiambrerías zona sur", Fecha = DateTime.Now.AddDays(-3) },
                    new MovimientoStock { Id = 12, ProductoId = 9, CodigoSku = "SKU-LAC-003", ProductoNombre = "Manteca Primera Calidad 200g", CategoriaNombre = "Lácteos", SucursalId = 1, SucursalNombre = "Central", UsuarioNombre = "admin", TipoMovimiento = "ENTRADA", Cantidad = 60, Motivo = "Ingreso cámara frigorífica Central", Fecha = DateTime.Now.AddDays(-5) },
                    new MovimientoStock { Id = 13, ProductoId = 10, CodigoSku = "SKU-LIM-001", ProductoNombre = "Lavandina Concentrada 1L", CategoriaNombre = "Limpieza", SucursalId = 1, SucursalNombre = "Central", UsuarioNombre = "admin", TipoMovimiento = "ENTRADA", Cantidad = 100, Motivo = "Compra mayorista Química San Lorenzo", Fecha = DateTime.Now.AddDays(-9) },
                    new MovimientoStock { Id = 14, ProductoId = 11, CodigoSku = "SKU-LIM-002", ProductoNombre = "Detergente Vajilla 750ml", CategoriaNombre = "Limpieza", SucursalId = 2, SucursalNombre = "Sucursal Norte", UsuarioNombre = "gerente_log", TipoMovimiento = "SALIDA", Cantidad = 15, Motivo = "Despacho clientes gastronómicos", Fecha = DateTime.Now.AddDays(-2) },
                    new MovimientoStock { Id = 15, ProductoId = 12, CodigoSku = "SKU-LIM-003", ProductoNombre = "Desinfectante Pisos 5L", CategoriaNombre = "Limpieza", SucursalId = 3, SucursalNombre = "Sucursal Sur", UsuarioNombre = "operador_sur", TipoMovimiento = "SALIDA", Cantidad = 22, Motivo = "Entrega consorcios y clínicas", Fecha = DateTime.Now.AddHours(-12) }
                };

                _ordenesCompra = new List<OrdenCompra>
                {
                    new OrdenCompra { Id = 1, NumeroOrden = "OC-2026-001", ProveedorId = 1, ProveedorNombre = "Distribuidora Rosario Bebidas S.A.", SucursalDestinoId = 1, SucursalDestinoNombre = "Central", ProductoId = 1, ProductoNombre = "Agua Mineral 1.5L", CantidadSolicitada = 100, PrecioUnitario = 520m, Total = 52000m, FechaEmision = DateTime.Now.AddDays(-10), FechaRecepcion = DateTime.Now.AddDays(-8), Estado = "RECIBIDA", Observaciones = "Abastecimiento mensual bebidas" },
                    new OrdenCompra { Id = 2, NumeroOrden = "OC-2026-002", ProveedorId = 2, ProveedorNombre = "Molinos del Litoral S.R.L.", SucursalDestinoId = 1, SucursalDestinoNombre = "Central", ProductoId = 4, ProductoNombre = "Arroz Blanco 1kg", CantidadSolicitada = 200, PrecioUnitario = 780m, Total = 156000m, FechaEmision = DateTime.Now.AddDays(-12), FechaRecepcion = DateTime.Now.AddDays(-10), Estado = "RECIBIDA", Observaciones = "Lote secos temporada alta" },
                    new OrdenCompra { Id = 3, NumeroOrden = "OC-2026-003", ProveedorId = 1, ProveedorNombre = "Distribuidora Rosario Bebidas S.A.", SucursalDestinoId = 3, SucursalDestinoNombre = "Sucursal Sur", ProductoId = 3, ProductoNombre = "Gaseosa Cola 2.25L", CantidadSolicitada = 100, PrecioUnitario = 1400m, Total = 140000m, FechaEmision = DateTime.Now.AddDays(-2), Estado = "PENDIENTE", Observaciones = "Reposición urgente por stock crítico" },
                    new OrdenCompra { Id = 4, NumeroOrden = "OC-2026-004", ProveedorId = 3, ProveedorNombre = "Lácteos Santa Fe S.A.", SucursalDestinoId = 1, SucursalDestinoNombre = "Central", ProductoId = 7, ProductoNombre = "Leche Entera 1L", CantidadSolicitada = 80, PrecioUnitario = 720m, Total = 57600m, FechaEmision = DateTime.Now.AddDays(-1), Estado = "PENDIENTE", Observaciones = "Reposición crítica lácteos Central" },
                    new OrdenCompra { Id = 5, NumeroOrden = "OC-2026-005", ProveedorId = 4, ProveedorNombre = "Química San Lorenzo S.A.", SucursalDestinoId = 1, SucursalDestinoNombre = "Central", ProductoId = 12, ProductoNombre = "Desinfectante Pisos 5L", CantidadSolicitada = 50, PrecioUnitario = 2400m, Total = 120000m, FechaEmision = DateTime.Now.AddHours(-6), Estado = "PENDIENTE", Observaciones = "Refuerzo stock crítico limpieza" }
                };

                _inicializado = true;
                SincronizarEsquemaInicialEnBaseDeDatos(connectionString);
            }
        }

        private static void SincronizarEsquemaInicialEnBaseDeDatos(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return;
            try
            {
                using var conn = new NpgsqlConnection(connectionString);
                conn.Open();

                conn.Execute(@"
                    CREATE TABLE IF NOT EXISTS Roles (
                        Id SERIAL PRIMARY KEY,
                        Nombre VARCHAR(50) UNIQUE NOT NULL,
                        Descripcion TEXT
                    );
                    CREATE TABLE IF NOT EXISTS Permisos (
                        Id SERIAL PRIMARY KEY,
                        Nombre VARCHAR(80) UNIQUE NOT NULL,
                        Descripcion TEXT
                    );
                    CREATE TABLE IF NOT EXISTS RolesPermisos (
                        RolId INTEGER NOT NULL REFERENCES Roles(Id) ON DELETE CASCADE,
                        PermisoId INTEGER NOT NULL REFERENCES Permisos(Id) ON DELETE CASCADE,
                        PRIMARY KEY (RolId, PermisoId)
                    );
                    CREATE TABLE IF NOT EXISTS Sucursales (
                        Id SERIAL PRIMARY KEY,
                        Nombre VARCHAR(100) NOT NULL,
                        Ubicacion VARCHAR(200) NOT NULL,
                        Telefono VARCHAR(50),
                        Activa BOOLEAN NOT NULL DEFAULT TRUE
                    );
                    ALTER TABLE Sucursales ADD COLUMN IF NOT EXISTS Ubicacion VARCHAR(200) DEFAULT '';
                    ALTER TABLE Sucursales ADD COLUMN IF NOT EXISTS Telefono VARCHAR(50);
                    ALTER TABLE Sucursales ADD COLUMN IF NOT EXISTS Activa BOOLEAN NOT NULL DEFAULT TRUE;

                    CREATE TABLE IF NOT EXISTS Usuarios (
                        Id SERIAL PRIMARY KEY,
                        SucursalId INTEGER REFERENCES Sucursales(Id),
                        RolId INTEGER REFERENCES Roles(Id),
                        NombreUsuario VARCHAR(50) UNIQUE NOT NULL,
                        Email VARCHAR(120),
                        PasswordHash VARCHAR(64) NOT NULL,
                        Rol VARCHAR(30) NOT NULL DEFAULT 'Administrador',
                        Activo BOOLEAN NOT NULL DEFAULT TRUE,
                        FechaCreacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                    ALTER TABLE Usuarios ADD COLUMN IF NOT EXISTS SucursalId INTEGER;
                    ALTER TABLE Usuarios ADD COLUMN IF NOT EXISTS RolId INTEGER;
                    ALTER TABLE Usuarios ADD COLUMN IF NOT EXISTS Email VARCHAR(120);
                    ALTER TABLE Usuarios ADD COLUMN IF NOT EXISTS Rol VARCHAR(30) NOT NULL DEFAULT 'Administrador';
                    ALTER TABLE Usuarios ADD COLUMN IF NOT EXISTS Activo BOOLEAN NOT NULL DEFAULT TRUE;
                    ALTER TABLE Usuarios ADD COLUMN IF NOT EXISTS FechaCreacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP;

                    CREATE TABLE IF NOT EXISTS Categorias (
                        Id SERIAL PRIMARY KEY,
                        Nombre VARCHAR(100) NOT NULL,
                        Descripcion TEXT,
                        Activa BOOLEAN NOT NULL DEFAULT TRUE
                    );
                    ALTER TABLE Categorias ADD COLUMN IF NOT EXISTS Descripcion TEXT;
                    ALTER TABLE Categorias ADD COLUMN IF NOT EXISTS Activa BOOLEAN NOT NULL DEFAULT TRUE;

                    CREATE TABLE IF NOT EXISTS Proveedores (
                        Id SERIAL PRIMARY KEY,
                        RazonSocial VARCHAR(150) NOT NULL,
                        Cuit VARCHAR(20) UNIQUE NOT NULL,
                        Telefono VARCHAR(50),
                        Email VARCHAR(120),
                        Direccion VARCHAR(200),
                        Activo BOOLEAN NOT NULL DEFAULT TRUE
                    );

                    CREATE TABLE IF NOT EXISTS Productos (
                        Id SERIAL PRIMARY KEY,
                        CodigoSku VARCHAR(30),
                        CategoriaId INTEGER REFERENCES Categorias(Id),
                        ProveedorId INTEGER REFERENCES Proveedores(Id),
                        Nombre VARCHAR(150) NOT NULL,
                        Descripcion TEXT,
                        PrecioCosto DECIMAL(10, 2) NOT NULL DEFAULT 0.00,
                        Precio DECIMAL(10, 2) NOT NULL,
                        StockActual INTEGER NOT NULL DEFAULT 0,
                        StockMinimo INTEGER NOT NULL DEFAULT 10,
                        Activo BOOLEAN NOT NULL DEFAULT TRUE
                    );
                    ALTER TABLE Productos ADD COLUMN IF NOT EXISTS CodigoSku VARCHAR(30);
                    ALTER TABLE Productos ADD COLUMN IF NOT EXISTS CategoriaId INTEGER;
                    ALTER TABLE Productos ADD COLUMN IF NOT EXISTS ProveedorId INTEGER;
                    ALTER TABLE Productos ADD COLUMN IF NOT EXISTS Descripcion TEXT;
                    ALTER TABLE Productos ADD COLUMN IF NOT EXISTS PrecioCosto DECIMAL(10, 2) NOT NULL DEFAULT 0.00;
                    ALTER TABLE Productos ADD COLUMN IF NOT EXISTS Precio DECIMAL(10, 2) NOT NULL DEFAULT 0.00;
                    ALTER TABLE Productos ADD COLUMN IF NOT EXISTS StockActual INTEGER NOT NULL DEFAULT 0;
                    ALTER TABLE Productos ADD COLUMN IF NOT EXISTS StockMinimo INTEGER NOT NULL DEFAULT 10;
                    ALTER TABLE Productos ADD COLUMN IF NOT EXISTS Activo BOOLEAN NOT NULL DEFAULT TRUE;
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_productos_codigosku ON Productos (CodigoSku);

                    CREATE TABLE IF NOT EXISTS StockSucursales (
                        Id SERIAL PRIMARY KEY,
                        ProductoId INTEGER NOT NULL,
                        SucursalId INTEGER NOT NULL,
                        CantidadActual INTEGER NOT NULL DEFAULT 0,
                        PuntoReorden INTEGER NOT NULL DEFAULT 10,
                        UltimaActualizacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_stocksucursales_prod_suc ON StockSucursales (ProductoId, SucursalId);

                    CREATE TABLE IF NOT EXISTS MovimientosStock (
                        Id SERIAL PRIMARY KEY,
                        ProductoId INTEGER NOT NULL,
                        SucursalId INTEGER,
                        UsuarioId INTEGER,
                        TipoMovimiento VARCHAR(20) NOT NULL,
                        Cantidad INTEGER NOT NULL,
                        Motivo VARCHAR(250),
                        Fecha TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                    ALTER TABLE MovimientosStock ADD COLUMN IF NOT EXISTS SucursalId INTEGER;
                    ALTER TABLE MovimientosStock ADD COLUMN IF NOT EXISTS UsuarioId INTEGER;
                    ALTER TABLE MovimientosStock ADD COLUMN IF NOT EXISTS Motivo VARCHAR(250);
                    ALTER TABLE MovimientosStock ADD COLUMN IF NOT EXISTS Fecha TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP;

                    CREATE TABLE IF NOT EXISTS OrdenesCompra (
                        Id SERIAL PRIMARY KEY,
                        NumeroOrden VARCHAR(30) UNIQUE NOT NULL,
                        ProveedorId INTEGER NOT NULL,
                        SucursalDestinoId INTEGER NOT NULL,
                        UsuarioId INTEGER,
                        FechaEmision TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FechaRecepcion TIMESTAMP,
                        Estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
                        Total DECIMAL(12, 2) NOT NULL DEFAULT 0.00,
                        Observaciones TEXT
                    );

                    CREATE TABLE IF NOT EXISTS DetallesOrdenCompra (
                        Id SERIAL PRIMARY KEY,
                        OrdenCompraId INTEGER NOT NULL,
                        ProductoId INTEGER NOT NULL,
                        CantidadSolicitada INTEGER NOT NULL,
                        PrecioUnitario DECIMAL(10, 2) NOT NULL
                    );
                ");

                // Roles y Permisos iniciales
                conn.Execute(@"
                    INSERT INTO Roles (Nombre, Descripcion) VALUES
                    ('Administrador', 'Acceso total al sistema y configuración'),
                    ('Gerente Logística', 'Supervisión de stock, proveedores y órdenes de compra'),
                    ('Operador Depósito', 'Registro de entradas y salidas de mercadería por sucursal')
                    ON CONFLICT (Nombre) DO NOTHING;

                    INSERT INTO Permisos (Nombre, Descripcion) VALUES
                    ('VER_DASHBOARD', 'Visualizar tablero de control y semaforización'),
                    ('GESTIONAR_PRODUCTOS', 'Alta, baja y modificación de catálogo de productos'),
                    ('REGISTRAR_MOVIMIENTOS', 'Asentar entradas, salidas y ajustes de stock'),
                    ('EMITIR_ORDENES_COMPRA', 'Gestionar proveedores y emitir órdenes de reposición')
                    ON CONFLICT (Nombre) DO NOTHING;
                ");

                foreach (var cat in _categorias)
                {
                    int updated = conn.Execute("UPDATE Categorias SET Nombre = @Nombre, Descripcion = @Descripcion WHERE Id = @Id;", cat);
                    if (updated == 0)
                    {
                        conn.Execute("INSERT INTO Categorias (Id, Nombre, Descripcion, Activa) VALUES (@Id, @Nombre, @Descripcion, TRUE);", cat);
                    }
                }

                foreach (var suc in _sucursales)
                {
                    int updated = conn.Execute("UPDATE Sucursales SET Nombre = @Nombre, Ubicacion = @Ubicacion, Telefono = @Telefono WHERE Id = @Id;", suc);
                    if (updated == 0)
                    {
                        conn.Execute("INSERT INTO Sucursales (Id, Nombre, Ubicacion, Telefono, Activa) VALUES (@Id, @Nombre, @Ubicacion, @Telefono, TRUE);", suc);
                    }
                }

                foreach (var prov in _proveedores)
                {
                    int updated = conn.Execute("UPDATE Proveedores SET RazonSocial = @RazonSocial, Cuit = @Cuit, Telefono = @Telefono, Email = @Email, Direccion = @Direccion WHERE Id = @Id OR Cuit = @Cuit;", prov);
                    if (updated == 0)
                    {
                        conn.Execute("INSERT INTO Proveedores (Id, RazonSocial, Cuit, Telefono, Email, Direccion, Activo) VALUES (@Id, @RazonSocial, @Cuit, @Telefono, @Email, @Direccion, TRUE);", prov);
                    }
                }

                foreach (var prod in _productos)
                {
                    int updated = conn.Execute(@"
                        UPDATE Productos
                        SET CodigoSku = @CodigoSku, CategoriaId = @CategoriaId, ProveedorId = @ProveedorId,
                            Nombre = @Nombre, PrecioCosto = @PrecioCosto, Precio = @Precio,
                            StockActual = @StockActual, StockMinimo = @StockMinimo
                        WHERE Id = @Id OR Nombre = @Nombre;", prod);
                    if (updated == 0)
                    {
                        conn.Execute(@"
                            INSERT INTO Productos (Id, CodigoSku, CategoriaId, ProveedorId, Nombre, PrecioCosto, Precio, StockActual, StockMinimo, Activo)
                            VALUES (@Id, @CodigoSku, @CategoriaId, @ProveedorId, @Nombre, @PrecioCosto, @Precio, @StockActual, @StockMinimo, TRUE);", prod);
                    }
                }

                int countStockSuc = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM StockSucursales;");
                if (countStockSuc == 0)
                {
                    foreach (var ss in _stockSucursales)
                    {
                        conn.Execute(@"
                            INSERT INTO StockSucursales (ProductoId, SucursalId, CantidadActual, PuntoReorden, UltimaActualizacion)
                            VALUES (@ProductoId, @SucursalId, @CantidadActual, @PuntoReorden, CURRENT_TIMESTAMP);", ss);
                    }
                }

                int countOrdenes = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM OrdenesCompra;");
                if (countOrdenes == 0)
                {
                    foreach (var oc in _ordenesCompra)
                    {
                        int ocId = conn.ExecuteScalar<int>(@"
                            INSERT INTO OrdenesCompra (NumeroOrden, ProveedorId, SucursalDestinoId, Estado, Total, Observaciones, FechaEmision)
                            VALUES (@NumeroOrden, @ProveedorId, @SucursalDestinoId, @Estado, @Total, @Observaciones, @FechaEmision)
                            RETURNING Id;", oc);
                        conn.Execute(@"
                            INSERT INTO DetallesOrdenCompra (OrdenCompraId, ProductoId, CantidadSolicitada, PrecioUnitario)
                            VALUES (@OrdenCompraId, @ProductoId, @CantidadSolicitada, @PrecioUnitario);",
                            new { OrdenCompraId = ocId, oc.ProductoId, oc.CantidadSolicitada, oc.PrecioUnitario });
                    }
                }

                // Alinear secuencias de PostgreSQL
                conn.Execute(@"
                    SELECT setval(pg_get_serial_sequence('categorias', 'id'), COALESCE((SELECT MAX(Id) FROM Categorias), 1));
                    SELECT setval(pg_get_serial_sequence('sucursales', 'id'), COALESCE((SELECT MAX(Id) FROM Sucursales), 1));
                    SELECT setval(pg_get_serial_sequence('proveedores', 'id'), COALESCE((SELECT MAX(Id) FROM Proveedores), 1));
                    SELECT setval(pg_get_serial_sequence('productos', 'id'), COALESCE((SELECT MAX(Id) FROM Productos), 1));
                ");
            }
            catch
            {
                // Si el motor local de base de datos no está disponible, se continúa operando en memoria.
            }
        }

        private DashboardViewModel ConstruirViewModel()
        {
            lock (_lock)
            {
                var resumenCategorias = _categorias.Select(cat =>
                {
                    var prodsCat = _productos.Where(p => p.CategoriaId == cat.Id || p.CategoriaNombre == cat.Nombre).ToList();
                    return new CategoriaResumenViewModel
                    {
                        CategoriaId = cat.Id,
                        Nombre = cat.Nombre,
                        Descripcion = cat.Descripcion ?? string.Empty,
                        TotalProductos = prodsCat.Count,
                        StockTotalUnidades = prodsCat.Sum(p => p.StockActual),
                        StockMinimoTotal = prodsCat.Sum(p => p.StockMinimo),
                        ValorizadoCosto = prodsCat.Sum(p => p.StockActual * p.PrecioCosto),
                        CantidadOptimo = prodsCat.Count(p => p.ObtenerEstadoSemaforo() == "Óptimo"),
                        CantidadAdvertencia = prodsCat.Count(p => p.ObtenerEstadoSemaforo() == "Advertencia"),
                        CantidadCritico = prodsCat.Count(p => p.ObtenerEstadoSemaforo() == "Crítico")
                    };
                }).ToList();

                return new DashboardViewModel
                {
                    Productos = _productos.OrderBy(p => p.CategoriaId).ThenBy(p => p.CodigoSku).ToList(),
                    CategoriasResumen = resumenCategorias,
                    StockPorSucursal = _stockSucursales.OrderBy(s => s.ProductoId).ThenBy(s => s.SucursalId).ToList(),
                    Movimientos = _movimientos.OrderByDescending(m => m.Fecha).ToList(),
                    Sucursales = _sucursales.ToList(),
                    Proveedores = _proveedores.ToList(),
                    OrdenesCompra = _ordenesCompra.OrderByDescending(o => o.FechaEmision).ToList()
                };
            }
        }

        public IActionResult Index()
        {
            var vm = ConstruirViewModel();
            return View(vm);
        }

        public IActionResult Productos()
        {
            var vm = ConstruirViewModel();
            return View(vm);
        }

        [HttpPost]
        public IActionResult CrearProducto(string codigoSku, string nombre, int categoriaId, int proveedorId, decimal precioCosto, decimal precio, int stockInicial, int stockMinimo)
        {
            lock (_lock)
            {
                var cat = _categorias.FirstOrDefault(c => c.Id == categoriaId) ?? _categorias.First();
                var prov = _proveedores.FirstOrDefault(p => p.Id == proveedorId) ?? _proveedores.First();
                int nuevoId = _productos.Any() ? _productos.Max(p => p.Id) + 1 : 1;

                var nuevoProd = new Producto
                {
                    Id = nuevoId,
                    CodigoSku = string.IsNullOrWhiteSpace(codigoSku) ? $"SKU-GEN-{nuevoId:D3}" : codigoSku.Trim().ToUpper(),
                    Nombre = nombre.Trim(),
                    CategoriaId = cat.Id,
                    CategoriaNombre = cat.Nombre,
                    ProveedorId = prov.Id,
                    ProveedorNombre = prov.RazonSocial,
                    PrecioCosto = precioCosto,
                    Precio = precio,
                    StockActual = stockInicial,
                    StockMinimo = stockMinimo,
                    StockOptimo = Math.Max(stockMinimo * 3, stockInicial)
                };

                _productos.Add(nuevoProd);

                // Distribuir el stock inicial en Casa Central y crear registros en las demás sucursales
                int idStock = _stockSucursales.Any() ? _stockSucursales.Max(s => s.Id) + 1 : 1;
                _stockSucursales.Add(new StockSucursal
                {
                    Id = idStock++,
                    ProductoId = nuevoProd.Id,
                    CodigoSku = nuevoProd.CodigoSku,
                    ProductoNombre = nuevoProd.Nombre,
                    CategoriaNombre = nuevoProd.CategoriaNombre,
                    SucursalId = 1,
                    SucursalNombre = "Central",
                    CantidadActual = stockInicial,
                    PuntoReorden = Math.Max(5, stockMinimo / 2)
                });

                if (stockInicial > 0)
                {
                    _movimientos.Add(new MovimientoStock
                    {
                        Id = _movimientos.Any() ? _movimientos.Max(m => m.Id) + 1 : 1,
                        ProductoId = nuevoProd.Id,
                        CodigoSku = nuevoProd.CodigoSku,
                        ProductoNombre = nuevoProd.Nombre,
                        CategoriaNombre = nuevoProd.CategoriaNombre,
                        SucursalId = 1,
                        SucursalNombre = "Central",
                        UsuarioNombre = User.Identity?.Name ?? "admin",
                        TipoMovimiento = "ENTRADA",
                        Cantidad = stockInicial,
                        Motivo = "Alta inicial de producto en catálogo",
                        Fecha = DateTime.Now
                    });
                }

                try
                {
                    using var conn = new NpgsqlConnection(_connectionString);
                    int dbProdId = conn.ExecuteScalar<int>(@"
                        INSERT INTO Productos (CodigoSku, CategoriaId, ProveedorId, Nombre, PrecioCosto, Precio, StockActual, StockMinimo, Activo)
                        VALUES (@CodigoSku, @CategoriaId, @ProveedorId, @Nombre, @PrecioCosto, @Precio, @StockActual, @StockMinimo, TRUE)
                        RETURNING Id;", nuevoProd);

                    conn.Execute(@"
                        INSERT INTO StockSucursales (ProductoId, SucursalId, CantidadActual, PuntoReorden, UltimaActualizacion)
                        VALUES (@ProductoId, 1, @CantidadActual, @PuntoReorden, CURRENT_TIMESTAMP);",
                        new { ProductoId = dbProdId, CantidadActual = stockInicial, PuntoReorden = Math.Max(5, stockMinimo / 2) });

                    if (stockInicial > 0)
                    {
                        conn.Execute(@"
                            INSERT INTO MovimientosStock (ProductoId, SucursalId, TipoMovimiento, Cantidad, Motivo, Fecha)
                            VALUES (@ProductoId, 1, 'ENTRADA', @Cantidad, 'Alta inicial de producto en catálogo', CURRENT_TIMESTAMP);",
                            new { ProductoId = dbProdId, Cantidad = stockInicial });
                    }
                }
                catch { }

                TempData["Mensaje"] = $"Producto '{nuevoProd.Nombre}' ({nuevoProd.CodigoSku}) registrado exitosamente con estado '{nuevoProd.ObtenerEstadoSemaforo()}'.";
            }

            return RedirectToAction(nameof(Productos));
        }

        [HttpPost]
        public IActionResult ActualizarStockProducto(int id, int stockActual, int stockMinimo, decimal precio)
        {
            lock (_lock)
            {
                var prod = _productos.FirstOrDefault(p => p.Id == id);
                if (prod != null)
                {
                    int diferencia = stockActual - prod.StockActual;
                    prod.StockActual = Math.Max(0, stockActual);
                    prod.StockMinimo = Math.Max(1, stockMinimo);
                    if (precio > 0) prod.Precio = precio;

                    var stockCentral = _stockSucursales.FirstOrDefault(s => s.ProductoId == id && s.SucursalId == 1);
                    if (stockCentral != null)
                    {
                        stockCentral.CantidadActual = Math.Max(0, stockCentral.CantidadActual + diferencia);
                    }

                    if (diferencia != 0)
                    {
                        _movimientos.Add(new MovimientoStock
                        {
                            Id = _movimientos.Any() ? _movimientos.Max(m => m.Id) + 1 : 1,
                            ProductoId = prod.Id,
                            CodigoSku = prod.CodigoSku,
                            ProductoNombre = prod.Nombre,
                            CategoriaNombre = prod.CategoriaNombre,
                            SucursalId = 1,
                            SucursalNombre = "Central",
                            UsuarioNombre = User.Identity?.Name ?? "admin",
                            TipoMovimiento = "AJUSTE",
                            Cantidad = diferencia,
                            Motivo = "Actualización de parámetros desde Catálogo",
                            Fecha = DateTime.Now
                        });
                    }

                    try
                    {
                        using var conn = new NpgsqlConnection(_connectionString);
                        conn.Execute(@"
                            UPDATE Productos
                            SET StockActual = @StockActual, StockMinimo = @StockMinimo, Precio = @Precio
                            WHERE CodigoSku = @CodigoSku OR Id = @Id;", prod);

                        if (diferencia != 0)
                        {
                            int? dbProdId = conn.ExecuteScalar<int?>("SELECT Id FROM Productos WHERE CodigoSku = @CodigoSku OR Id = @Id LIMIT 1;", prod);
                            if (dbProdId.HasValue)
                            {
                                conn.Execute(@"
                                    INSERT INTO MovimientosStock (ProductoId, SucursalId, TipoMovimiento, Cantidad, Motivo, Fecha)
                                    VALUES (@ProductoId, 1, 'AJUSTE', @Cantidad, 'Actualización de parámetros desde Catálogo', CURRENT_TIMESTAMP);",
                                    new { ProductoId = dbProdId.Value, Cantidad = diferencia });
                            }
                        }
                    }
                    catch { }

                    TempData["Mensaje"] = $"Producto '{prod.Nombre}' actualizado. Estado actual del semáforo: {prod.ObtenerEstadoSemaforo()}.";
                }
            }
            return RedirectToAction(nameof(Productos));
        }

        [HttpPost]
        public IActionResult EliminarProducto(int id)
        {
            lock (_lock)
            {
                var prod = _productos.FirstOrDefault(p => p.Id == id);
                if (prod != null)
                {
                    _productos.Remove(prod);
                    _stockSucursales.RemoveAll(s => s.ProductoId == id);

                    try
                    {
                        using var conn = new NpgsqlConnection(_connectionString);
                        conn.Execute("DELETE FROM Productos WHERE CodigoSku = @CodigoSku OR Id = @Id;", prod);
                    }
                    catch { }

                    TempData["Mensaje"] = $"Producto '{prod.Nombre}' dado de baja correctamente.";
                }
            }
            return RedirectToAction(nameof(Productos));
        }

        public IActionResult Movimientos()
        {
            var vm = ConstruirViewModel();
            return View(vm);
        }

        [HttpPost]
        public IActionResult RegistrarMovimiento(string tipoMovimiento, int sucursalId, int productoId, int cantidad, string motivo)
        {
            lock (_lock)
            {
                var prod = _productos.FirstOrDefault(p => p.Id == productoId);
                var suc = _sucursales.FirstOrDefault(s => s.Id == sucursalId);

                if (prod != null && suc != null && cantidad > 0)
                {
                    int delta = tipoMovimiento == "SALIDA" ? -Math.Abs(cantidad) :
                                tipoMovimiento == "ENTRADA" ? Math.Abs(cantidad) : cantidad;

                    var regSucursal = _stockSucursales.FirstOrDefault(s => s.ProductoId == productoId && s.SucursalId == sucursalId);
                    if (regSucursal == null)
                    {
                        regSucursal = new StockSucursal
                        {
                            Id = _stockSucursales.Any() ? _stockSucursales.Max(s => s.Id) + 1 : 1,
                            ProductoId = prod.Id,
                            CodigoSku = prod.CodigoSku,
                            ProductoNombre = prod.Nombre,
                            CategoriaNombre = prod.CategoriaNombre,
                            SucursalId = suc.Id,
                            SucursalNombre = suc.Nombre,
                            CantidadActual = 0,
                            PuntoReorden = Math.Max(5, prod.StockMinimo / 3)
                        };
                        _stockSucursales.Add(regSucursal);
                    }

                    regSucursal.CantidadActual = Math.Max(0, regSucursal.CantidadActual + delta);
                    regSucursal.UltimaActualizacion = DateTime.Now;

                    // Recalcular StockActual consolidado del producto sumando todas las sucursales
                    prod.StockActual = _stockSucursales.Where(s => s.ProductoId == prod.Id).Sum(s => s.CantidadActual);

                    string motivoFinal = string.IsNullOrWhiteSpace(motivo) ? "Operación registrada en depósito" : motivo.Trim();
                    int cantFinal = tipoMovimiento == "SALIDA" ? -Math.Abs(cantidad) : Math.Abs(cantidad);

                    _movimientos.Add(new MovimientoStock
                    {
                        Id = _movimientos.Any() ? _movimientos.Max(m => m.Id) + 1 : 1,
                        ProductoId = prod.Id,
                        CodigoSku = prod.CodigoSku,
                        ProductoNombre = prod.Nombre,
                        CategoriaNombre = prod.CategoriaNombre,
                        SucursalId = suc.Id,
                        SucursalNombre = suc.Nombre,
                        UsuarioNombre = User.Identity?.Name ?? "admin",
                        TipoMovimiento = tipoMovimiento,
                        Cantidad = cantFinal,
                        Motivo = motivoFinal,
                        Fecha = DateTime.Now
                    });

                    try
                    {
                        using var conn = new NpgsqlConnection(_connectionString);
                        int? dbProdId = conn.ExecuteScalar<int?>("SELECT Id FROM Productos WHERE CodigoSku = @CodigoSku OR Id = @Id LIMIT 1;", prod);
                        if (dbProdId.HasValue)
                        {
                            conn.Execute(@"
                                UPDATE Productos SET StockActual = @StockActual WHERE Id = @Id;
                                UPDATE StockSucursales SET CantidadActual = @CantidadActual, UltimaActualizacion = CURRENT_TIMESTAMP WHERE ProductoId = @ProductoId AND SucursalId = @SucursalId;
                                INSERT INTO MovimientosStock (ProductoId, SucursalId, TipoMovimiento, Cantidad, Motivo, Fecha)
                                VALUES (@ProductoId, @SucursalId, @TipoMovimiento, @Cantidad, @Motivo, CURRENT_TIMESTAMP);",
                                new
                                {
                                    Id = dbProdId.Value,
                                    StockActual = prod.StockActual,
                                    ProductoId = dbProdId.Value,
                                    SucursalId = suc.Id,
                                    CantidadActual = regSucursal.CantidadActual,
                                    TipoMovimiento = tipoMovimiento,
                                    Cantidad = cantFinal,
                                    Motivo = motivoFinal
                                });
                        }
                    }
                    catch { }

                    TempData["Mensaje"] = $"Movimiento ({tipoMovimiento}) de {cantidad} u. sobre '{prod.Nombre}' en {suc.Nombre} asentado. Nuevo stock consolidado: {prod.StockActual} u. ({prod.ObtenerEstadoSemaforo()}).";
                }
            }

            return RedirectToAction(nameof(Movimientos));
        }

        public IActionResult ProveedoresOrdenes()
        {
            var vm = ConstruirViewModel();
            return View(vm);
        }

        [HttpPost]
        public IActionResult CrearProveedor(string razonSocial, string cuit, string telefono, string email, string direccion)
        {
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(razonSocial))
                {
                    int nuevoId = _proveedores.Any() ? _proveedores.Max(p => p.Id) + 1 : 1;
                    var nuevoProv = new Proveedor
                    {
                        Id = nuevoId,
                        RazonSocial = razonSocial.Trim(),
                        Cuit = string.IsNullOrWhiteSpace(cuit) ? $"30-{10000000 + nuevoId}-9" : cuit.Trim(),
                        Telefono = telefono ?? "-",
                        Email = email ?? "-",
                        Direccion = direccion ?? "Zona Industrial",
                        Activo = true
                    };
                    _proveedores.Add(nuevoProv);

                    try
                    {
                        using var conn = new NpgsqlConnection(_connectionString);
                        conn.Execute(@"
                            INSERT INTO Proveedores (RazonSocial, Cuit, Telefono, Email, Direccion, Activo)
                            VALUES (@RazonSocial, @Cuit, @Telefono, @Email, @Direccion, TRUE)
                            ON CONFLICT (Cuit) DO UPDATE
                            SET RazonSocial = EXCLUDED.RazonSocial, Telefono = EXCLUDED.Telefono,
                                Email = EXCLUDED.Email, Direccion = EXCLUDED.Direccion;", nuevoProv);
                    }
                    catch { }

                    TempData["Mensaje"] = $"Proveedor '{razonSocial}' homologado correctamente.";
                }
            }
            return RedirectToAction(nameof(ProveedoresOrdenes));
        }

        [HttpPost]
        public IActionResult EmitirOrden(int proveedorId, int sucursalDestinoId, int productoId, int cantidad, decimal costoUnitario, string observaciones)
        {
            lock (_lock)
            {
                var prov = _proveedores.FirstOrDefault(p => p.Id == proveedorId) ?? _proveedores.First();
                var suc = _sucursales.FirstOrDefault(s => s.Id == sucursalDestinoId) ?? _sucursales.First();
                var prod = _productos.FirstOrDefault(p => p.Id == productoId) ?? _productos.First();

                int nuevoId = _ordenesCompra.Any() ? _ordenesCompra.Max(o => o.Id) + 1 : 1;
                string numOrden = $"OC-2026-{nuevoId:D3}";
                decimal costo = costoUnitario > 0 ? costoUnitario : prod.PrecioCosto;

                var nuevaOrden = new OrdenCompra
                {
                    Id = nuevoId,
                    NumeroOrden = numOrden,
                    ProveedorId = prov.Id,
                    ProveedorNombre = prov.RazonSocial,
                    SucursalDestinoId = suc.Id,
                    SucursalDestinoNombre = suc.Nombre,
                    ProductoId = prod.Id,
                    ProductoNombre = prod.Nombre,
                    CantidadSolicitada = Math.Max(1, cantidad),
                    PrecioUnitario = costo,
                    Total = Math.Max(1, cantidad) * costo,
                    FechaEmision = DateTime.Now,
                    Estado = "PENDIENTE",
                    Observaciones = string.IsNullOrWhiteSpace(observaciones) ? "Reposición programada" : observaciones.Trim()
                };
                _ordenesCompra.Add(nuevaOrden);

                try
                {
                    using var conn = new NpgsqlConnection(_connectionString);
                    int dbOrdenId = conn.ExecuteScalar<int>(@"
                        INSERT INTO OrdenesCompra (NumeroOrden, ProveedorId, SucursalDestinoId, Estado, Total, Observaciones, FechaEmision)
                        VALUES (@NumeroOrden, @ProveedorId, @SucursalDestinoId, 'PENDIENTE', @Total, @Observaciones, CURRENT_TIMESTAMP)
                        ON CONFLICT (NumeroOrden) DO UPDATE SET Total = EXCLUDED.Total
                        RETURNING Id;", nuevaOrden);

                    int? dbProdId = conn.ExecuteScalar<int?>("SELECT Id FROM Productos WHERE CodigoSku = @CodigoSku OR Id = @Id LIMIT 1;", prod);
                    if (dbProdId.HasValue)
                    {
                        conn.Execute(@"
                            INSERT INTO DetallesOrdenCompra (OrdenCompraId, ProductoId, CantidadSolicitada, PrecioUnitario)
                            VALUES (@OrdenCompraId, @ProductoId, @CantidadSolicitada, @PrecioUnitario);",
                            new { OrdenCompraId = dbOrdenId, ProductoId = dbProdId.Value, nuevaOrden.CantidadSolicitada, nuevaOrden.PrecioUnitario });
                    }
                }
                catch { }

                TempData["Mensaje"] = $"Orden de Compra {numOrden} emitida por {cantidad} u. de '{prod.Nombre}' hacia {suc.Nombre}.";
            }
            return RedirectToAction(nameof(ProveedoresOrdenes));
        }

        [HttpPost]
        public IActionResult RecibirOrden(int id)
        {
            lock (_lock)
            {
                var orden = _ordenesCompra.FirstOrDefault(o => o.Id == id);
                if (orden != null && orden.Estado == "PENDIENTE")
                {
                    orden.Estado = "RECIBIDA";
                    orden.FechaRecepcion = DateTime.Now;

                    var prod = _productos.FirstOrDefault(p => p.Id == orden.ProductoId);
                    var suc = _sucursales.FirstOrDefault(s => s.Id == orden.SucursalDestinoId);

                    if (prod != null && suc != null)
                    {
                        var regSucursal = _stockSucursales.FirstOrDefault(s => s.ProductoId == prod.Id && s.SucursalId == suc.Id);
                        if (regSucursal == null)
                        {
                            regSucursal = new StockSucursal
                            {
                                Id = _stockSucursales.Any() ? _stockSucursales.Max(s => s.Id) + 1 : 1,
                                ProductoId = prod.Id,
                                CodigoSku = prod.CodigoSku,
                                ProductoNombre = prod.Nombre,
                                CategoriaNombre = prod.CategoriaNombre,
                                SucursalId = suc.Id,
                                SucursalNombre = suc.Nombre,
                                CantidadActual = 0,
                                PuntoReorden = Math.Max(5, prod.StockMinimo / 3)
                            };
                            _stockSucursales.Add(regSucursal);
                        }

                        regSucursal.CantidadActual += orden.CantidadSolicitada;
                        regSucursal.UltimaActualizacion = DateTime.Now;
                        prod.StockActual = _stockSucursales.Where(s => s.ProductoId == prod.Id).Sum(s => s.CantidadActual);

                        _movimientos.Add(new MovimientoStock
                        {
                            Id = _movimientos.Any() ? _movimientos.Max(m => m.Id) + 1 : 1,
                            ProductoId = prod.Id,
                            CodigoSku = prod.CodigoSku,
                            ProductoNombre = prod.Nombre,
                            CategoriaNombre = prod.CategoriaNombre,
                            SucursalId = suc.Id,
                            SucursalNombre = suc.Nombre,
                            UsuarioNombre = User.Identity?.Name ?? "admin",
                            TipoMovimiento = "ENTRADA",
                            Cantidad = orden.CantidadSolicitada,
                            Motivo = $"Recepción de Orden de Compra {orden.NumeroOrden}",
                            Fecha = DateTime.Now
                        });

                        try
                        {
                            using var conn = new NpgsqlConnection(_connectionString);
                            conn.Execute(@"
                                UPDATE OrdenesCompra
                                SET Estado = 'RECIBIDA', FechaRecepcion = CURRENT_TIMESTAMP
                                WHERE NumeroOrden = @NumeroOrden;", orden);

                            int? dbProdId = conn.ExecuteScalar<int?>("SELECT Id FROM Productos WHERE CodigoSku = @CodigoSku OR Id = @Id LIMIT 1;", prod);
                            if (dbProdId.HasValue)
                            {
                                conn.Execute(@"
                                    UPDATE Productos SET StockActual = @StockActual WHERE Id = @Id;
                                    INSERT INTO StockSucursales (ProductoId, SucursalId, CantidadActual, PuntoReorden, UltimaActualizacion)
                                    VALUES (@ProductoId, @SucursalId, @CantidadActual, @PuntoReorden, CURRENT_TIMESTAMP)
                                    ON CONFLICT (ProductoId, SucursalId) DO UPDATE
                                    SET CantidadActual = EXCLUDED.CantidadActual, UltimaActualizacion = CURRENT_TIMESTAMP;
                                    INSERT INTO MovimientosStock (ProductoId, SucursalId, TipoMovimiento, Cantidad, Motivo, Fecha)
                                    VALUES (@ProductoId, @SucursalId, 'ENTRADA', @Cantidad, @Motivo, CURRENT_TIMESTAMP);",
                                    new
                                    {
                                        Id = dbProdId.Value,
                                        StockActual = prod.StockActual,
                                        ProductoId = dbProdId.Value,
                                        SucursalId = suc.Id,
                                        CantidadActual = regSucursal.CantidadActual,
                                        PuntoReorden = regSucursal.PuntoReorden,
                                        Cantidad = orden.CantidadSolicitada,
                                        Motivo = $"Recepción de Orden de Compra {orden.NumeroOrden}"
                                    });
                            }
                        }
                        catch { }

                        TempData["Mensaje"] = $"Mercadería de {orden.NumeroOrden} recibida: +{orden.CantidadSolicitada} u. de '{prod.Nombre}' ingresadas en {suc.Nombre}. Estado actual del producto: {prod.ObtenerEstadoSemaforo()}.";
                    }
                }
            }
            return RedirectToAction(nameof(ProveedoresOrdenes));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
