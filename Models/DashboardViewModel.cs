namespace ControlInventario.Models
{
    public class CategoriaResumenViewModel
    {
        public int CategoriaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int TotalProductos { get; set; }
        public int StockTotalUnidades { get; set; }
        public int StockMinimoTotal { get; set; }
        public decimal ValorizadoCosto { get; set; }
        public int CantidadOptimo { get; set; }
        public int CantidadAdvertencia { get; set; }
        public int CantidadCritico { get; set; }

        /// <summary>
        /// Determina el estado semafórico consolidado de la categoría:
        /// - Crítico si posee al menos un producto en estado Crítico.
        /// - Advertencia si no posee críticos pero tiene al menos un producto en Advertencia.
        /// - Óptimo si todos sus productos se encuentran en nivel Óptimo.
        /// </summary>
        public string EstadoSemaforoCategoria
        {
            get
            {
                if (CantidadCritico > 0) return "Crítico";
                if (CantidadAdvertencia > 0) return "Advertencia";
                return "Óptimo";
            }
        }

        public string ClaseBadgeCategoria => EstadoSemaforoCategoria switch
        {
            "Crítico" => "bg-danger",
            "Advertencia" => "bg-warning text-dark",
            _ => "bg-success"
        };
    }

    public class DashboardViewModel
    {
        public List<Producto> Productos { get; set; } = new();
        public List<CategoriaResumenViewModel> CategoriasResumen { get; set; } = new();
        public List<StockSucursal> StockPorSucursal { get; set; } = new();
        public List<MovimientoStock> Movimientos { get; set; } = new();
        public List<Sucursal> Sucursales { get; set; } = new();
        public List<Proveedor> Proveedores { get; set; } = new();
        public List<OrdenCompra> OrdenesCompra { get; set; } = new();

        // Indicadores KPI Globales calculados dinámicamente
        public int TotalOptimo => Productos.Count(p => p.ObtenerEstadoSemaforo() == "Óptimo");
        public int TotalAdvertencia => Productos.Count(p => p.ObtenerEstadoSemaforo() == "Advertencia");
        public int TotalCritico => Productos.Count(p => p.ObtenerEstadoSemaforo() == "Crítico");
        public int TotalMovimientos => Movimientos.Count;
        public int TotalUnidadesStock => Productos.Sum(p => p.StockActual);
        public decimal ValorTotalInventario => Productos.Sum(p => p.StockActual * p.PrecioCosto);
    }
}
