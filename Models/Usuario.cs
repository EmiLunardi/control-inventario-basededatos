namespace ControlInventario.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public int? SucursalId { get; set; }
        public int? RolId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string Rol { get; set; } = "Usuario";
        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}
