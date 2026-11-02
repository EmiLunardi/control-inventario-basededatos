namespace ControlInventario.Models
{
    public class MovimientoStock
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public string CodigoSku { get; set; } = string.Empty;
        public string CategoriaNombre { get; set; } = string.Empty;
        public int SucursalId { get; set; }
        public string SucursalNombre { get; set; } = string.Empty;
        public int? UsuarioId { get; set; }
        public string UsuarioNombre { get; set; } = "admin";
        public string TipoMovimiento { get; set; } = string.Empty; // 'ENTRADA', 'SALIDA', 'AJUSTE'
        public int Cantidad { get; set; }
        public string? Motivo { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
    }
}
