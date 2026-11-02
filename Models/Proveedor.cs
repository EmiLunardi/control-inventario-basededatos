namespace ControlInventario.Models
{
    public class Proveedor
    {
        public int Id { get; set; }
        public string RazonSocial { get; set; } = string.Empty;
        public string Cuit { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Direccion { get; set; }
        public bool Activo { get; set; } = true;
    }
}
