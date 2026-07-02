using Microsoft.Extensions.Options;
using Npgsql.BackendMessages;
using VanitasStudios_WebApp.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace VanitasStudios_WebApp.Service
{
    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IOptions<CloudinarySettings> config)
        {
            var account = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
            );
            _cloudinary = new Cloudinary(account);
        }

        /// <summary>
        /// Carica un contenuto multimediale (Immagine o Video) su Cloudinary.
        /// </summary>
        public async Task<string?> UploadMediaAsync(IFormFile file, string folderPath, string resourceType = "image")
        {
            if (file == null || file.Length == 0)
                return null;

            using var stream = file.OpenReadStream();
            var fileDescription = new FileDescription(file.FileName, stream);
            var publicId = Path.GetFileNameWithoutExtension(file.FileName) + "_" + Guid.NewGuid().ToString()[..8];

            RawUploadResult uploadResult;

            // Dividiamo la logica in base al tipo: l'SDK gradisce parametri dedicati
            if (resourceType.ToLower() == "video")
            {
                var videoParams = new VideoUploadParams()
                {
                    File = fileDescription,
                    Folder = folderPath,
                    PublicId = publicId
                };
                uploadResult = await _cloudinary.UploadAsync(videoParams);
            }
            else
            {
                var imageParams = new ImageUploadParams()
                {
                    File = fileDescription,
                    Folder = folderPath,
                    PublicId = publicId
                };
                uploadResult = await _cloudinary.UploadAsync(imageParams);
            }

            if (uploadResult.Error != null)
            {
                Console.WriteLine($"[Cloudinary Error]: {uploadResult.Error.Message}");
                return null;
            }

            return uploadResult.SecureUrl.ToString();
        }
    }
}
