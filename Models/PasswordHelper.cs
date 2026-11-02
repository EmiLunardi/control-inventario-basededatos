using System.Security.Cryptography;
using System.Text;

namespace ControlInventario.Models
{
    public static class PasswordHelper
    {
        /// <summary>
        /// Encripta (hashea) una contraseña en texto plano utilizando el algoritmo criptográfico SHA-256.
        /// Devuelve una cadena hexadecimal de 64 caracteres en minúsculas.
        /// </summary>
        public static string EncriptarPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
