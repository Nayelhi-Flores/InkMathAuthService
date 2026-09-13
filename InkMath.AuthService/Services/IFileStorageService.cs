using System.Text.RegularExpressions;

namespace InkMath.AuthService.Services
{
    public interface IFileStorageService
    {
        Task<(bool EsValido, string Mensaje, string RutaGuardada)> GuardarArchivoSeguroAsync(
            IFormFile archivo,
            string carpetaDestino,
            string[] extensionesPermitidas,
            string[] mimeTypesPermitidos,
            long tamanoMaximoBytes = 10_485_760);

        (bool EsValido, string Mensaje, string UrlProcesada) ValidarUrlVideoSegura(string urlInput);
        (bool EsValido, string Mensaje) ValidarUrlEnlaceSegura(string urlInput);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;

        // Whitelist de dominios permitidos para Enlaces Generales Educativos
        private readonly HashSet<string> _dominiosPermitidosEnlaces = new(StringComparer.OrdinalIgnoreCase)
        {
            "wikipedia.org",
            "es.wikipedia.org",
            "khanacademy.org",
            "es.khanacademy.org",
            "geogebra.org",
            "drive.google.com",
            "docs.google.com",
            "mineduc.gob.gt"
        };

        // Whitelist de dominios permitidos para Videos
        private readonly HashSet<string> _dominiosPermitidosVideos = new(StringComparer.OrdinalIgnoreCase)
        {
            "youtube.com",
            "www.youtube.com",
            "youtu.be"
        };

        public FileStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<(bool EsValido, string Mensaje, string RutaGuardada)> GuardarArchivoSeguroAsync(
            IFormFile archivo,
            string carpetaDestino,
            string[] extensionesPermitidas,
            string[] mimeTypesPermitidos,
            long tamanoMaximoBytes = 10_485_760)
        {
            if (archivo == null || archivo.Length == 0)
                return (false, "El archivo está vacío o no fue proporcionado.", string.Empty);

            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

            // 1. Validar tamaño máximo
            if (archivo.Length > tamanoMaximoBytes)
                return (false, $"El archivo excede el tamaño máximo permitido ({tamanoMaximoBytes / 1024 / 1024} MB).", string.Empty);

            // 2. Validar extensión y MIME type
            if (!extensionesPermitidas.Contains(extension) || !mimeTypesPermitidos.Contains(archivo.ContentType.ToLower()))
                return (false, "Formato o tipo de archivo no permitido.", string.Empty);

            // 3. Validar firma binaria (Magic Numbers)
            if (!ValidarMagicNumbers(archivo))
                return (false, "El contenido del archivo no coincide con su extensión (Firma binaria inválida).", string.Empty);

            // 4. Guardar en disco
            var uploadsFolder = Path.Combine(_environment.ContentRootPath, carpetaDestino);
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            return (true, "Archivo guardado exitosamente.", fileName);
        }

        public (bool EsValido, string Mensaje, string UrlProcesada) ValidarUrlVideoSegura(string urlInput)
        {
            if (string.IsNullOrWhiteSpace(urlInput))
                return (false, "La URL del video no puede estar vacía.", string.Empty);

            if (!Uri.TryCreate(urlInput.Trim(), UriKind.Absolute, out Uri? uriResult))
                return (false, "La URL proporcionada no tiene un formato válido.", string.Empty);

            // Forzar únicamente HTTPS
            if (uriResult.Scheme != Uri.UriSchemeHttps)
                return (false, "Por seguridad, solo se permiten URLs seguras con protocolo HTTPS.", string.Empty);

            string host = uriResult.Host.ToLower();

            // Validar Whitelist de Host
            if (!_dominiosPermitidosVideos.Contains(host))
                return (false, $"El dominio '{host}' no está dentro de la lista de proveedores de video autorizados (YouTube).", string.Empty);

            // Validación específica para YouTube
            if (host.Contains("youtube.com") || host.Contains("youtu.be"))
            {
                if (!Regex.IsMatch(uriResult.ToString(), @"^(https?://)?(www\.)?(youtube\.com/(watch\?v=|embed/)|youtu\.be/)[a-zA-Z0-9_-]{11}"))
                {
                    return (false, "La URL no corresponde a un video válido de YouTube.", string.Empty);
                }
            }

            return (true, "URL de video válida y segura.", uriResult.ToString());
        }

        public (bool EsValido, string Mensaje) ValidarUrlEnlaceSegura(string urlInput)
        {
            if (string.IsNullOrWhiteSpace(urlInput))
                return (false, "La URL del enlace no puede estar vacía.");

            if (!Uri.TryCreate(urlInput.Trim(), UriKind.Absolute, out Uri? uriResult))
                return (false, "La URL proporcionada no tiene un formato válido.");

            // Forzar únicamente HTTPS
            if (uriResult.Scheme != Uri.UriSchemeHttps)
                return (false, "Por seguridad, solo se permiten URLs seguras con protocolo HTTPS.");

            string host = uriResult.Host.ToLower();

            // Validar si el host termina o coincide con la lista permitida
            bool esDominioPermitido = _dominiosPermitidosEnlaces.Any(d => host == d || host.EndsWith("." + d));

            if (!esDominioPermitido)
                return (false, $"El dominio '{host}' no pertenece a la lista de sitios web educativos autorizados.");

            return (true, "URL autorizada exitosamente.");
        }

        private bool ValidarMagicNumbers(IFormFile archivo)
        {
            using var stream = archivo.OpenReadStream();
            using var reader = new BinaryReader(stream);
            var headerBytes = reader.ReadBytes(4);
            stream.Position = 0;

            if (headerBytes.Length < 4) return false;

            // Firmas para PDF y JPEG/JPG
            bool esPdf = headerBytes[0] == 0x25 && headerBytes[1] == 0x50 && headerBytes[2] == 0x44 && headerBytes[3] == 0x46;
            bool esJpeg = headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF;

            return esPdf || esJpeg;
        }
    }
}
