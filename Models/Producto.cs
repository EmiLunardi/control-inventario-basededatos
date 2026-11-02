namespace ControlInventario.Models
{
    public class Producto
    {
        public int Id { get; set; }
        public string CodigoSku { get; set; } = string.Empty;
        public int CategoriaId { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;
        public int? ProveedorId { get; set; }
        public string ProveedorNombre { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal PrecioCosto { get; set; }
        public decimal Precio { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public int StockOptimo { get; set; }
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Evalúa internamente en el sistema el estado del stock aplicando la regla del 10%:
        /// - Crítico: Cuando el StockActual es menor o igual al StockMinimo (o al 10% del StockOptimo).
        /// - Advertencia: Cuando está a un 10% o menos de quedarse sin el stock mínimo/óptimo (StockMinimo * 1.10).
        /// - Óptimo: Cuando supera ese margen del 10%.
        /// </summary>
        public string ObtenerEstadoSemaforo()
        {
            double limiteCritico = StockOptimo > 0 ? StockOptimo * 0.10 : StockMinimo;
            double limiteAdvertencia = StockMinimo * 1.10;

            if (StockActual <= limiteCritico || StockActual <= StockMinimo)
            {
                return "Crítico";
            }
            if (StockActual <= limiteAdvertencia)
            {
                return "Advertencia";
            }
            return "Óptimo";
        }

        public string ObtenerClaseBadge()
        {
            return ObtenerEstadoSemaforo() switch
            {
                "Crítico" => "bg-danger",
                "Advertencia" => "bg-warning text-dark",
                _ => "bg-success"
            };
        }

        public double ObtenerPorcentajeSobreMinimo()
        {
            if (StockMinimo <= 0) return 100.0;
            return Math.Round(((double)(StockActual - StockMinimo) / StockMinimo) * 100.0, 1);
        }
    }
}
