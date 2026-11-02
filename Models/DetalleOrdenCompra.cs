namespace ControlInventario.Models
{
    public class DetalleOrdenCompra
    {
        public int Id { get; set; }
        public int OrdenCompraId { get; set; }
        public int ProductoId { get; set; }
        public int CantidadSolicitada { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => CantidadSolicitada * PrecioUnitario;
    }
}
