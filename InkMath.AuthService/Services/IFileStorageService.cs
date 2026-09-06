namespace InkMath.AuthService.Services
{
    public interface IFileStorageService
    {
        Task<(bool EsValido, string Mensaje, string RutaGuardada)> GuardarArchivoSeguroAsync(
            IFormFile archivo,
            string carpetaDestino,
            string[] extensionesPermitidas,
            string[] mimeTypesPermitidos);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;

        public FileStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<(bool EsValido, string Mensaje, string RutaGuardada)> GuardarArchivoSeguroAsync(
            IFormFile archivo,
            string carpetaDestino,
            string[] extensionesPermitidas,
            string[] mimeTypesPermitidos)
        {
            if (archivo == null || archivo.Length == 0)
                return (false, "El archivo está vacío o no fue proporcionado.", string.Empty);

            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

            // 1. Validar extensión y MIME type
            if (!extensionesPermitidas.Contains(extension) || !mimeTypesPermitidos.Contains(archivo.ContentType.ToLower()))
                return (false, "Formato o tipo de archivo no permitido.", string.Empty);

            // 2. Validar firma binaria (Magic Numbers)
            if (!ValidarMagicNumbers(archivo))
                return (false, "El contenido del archivo no coincide con su extensión (Firma binaria inválida).", string.Empty);

            // 3. Guardar en disco
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
