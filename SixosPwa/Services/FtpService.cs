#nullable disable
using SixosPwa.Models;
using Microsoft.Extensions.Options;
using System.Net;

namespace SixosPwa.Services
{
    public interface IFtpService
    {
        Task<string> UploadFileAsync(IFormFile file, string remoteDirectory = "");
        Task<string> UploadBytesAsync(byte[] bytes, string fileName, string remoteDirectory = "");
        Task<bool> DeleteFileAsync(string remoteFilePath);
        Task<bool> TestConnectionAsync();
        Task<Stream> DownloadAsync(string remoteFilePath);
        Task<List<FtpFileInfo>> ListFilesInDirectoryAsync(string remoteDirectory);
        Task<bool> MoveFileAsync(string sourceFilePath, string destinationFilePath);
        Task<bool> CopyFileAsync(string sourceFilePath, string destinationFilePath);
        Task<bool> DeleteFileByUrlAsync(string fileUrl);
    }
    public class FtpService : IFtpService
    {
        private readonly FtpSettings _ftpSettings;
        private readonly ILogger<FtpService> _logger;

        public FtpService(IOptions<FtpSettings> ftpSettings, ILogger<FtpService> logger)
        {
            _ftpSettings = ftpSettings.Value;
            _logger = logger;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string remoteDirectory = "")
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty");

            string originalFileName = Path.GetFileName(file.FileName);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
            string fileExt = Path.GetExtension(originalFileName);

            remoteDirectory = remoteDirectory.TrimStart('/');
            string remoteFilePath = string.IsNullOrEmpty(remoteDirectory)
                ? originalFileName
                : $"{remoteDirectory}/{originalFileName}";

            int counter = 1;
            while (await FileExistsAsync("/" + remoteFilePath))
            {
                string newFileName = $"{fileNameWithoutExt}({counter}){fileExt}";
                remoteFilePath = string.IsNullOrEmpty(remoteDirectory)
                    ? newFileName
                    : $"{remoteDirectory}/{newFileName}";
                counter++;
            }

            string ftpUrl = $"ftp://{_ftpSettings.FtpHost}/{remoteFilePath}";

            try
            {
                if (!string.IsNullOrEmpty(remoteDirectory))
                {
                    await CreateDirectoryIfNotExistsAsync(remoteDirectory);
                }

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                request.UseBinary = true;
                request.UsePassive = true;
                request.KeepAlive = false;

                using (var fileStream = file.OpenReadStream())
                using (var ftpStream = await request.GetRequestStreamAsync())
                {
                    await fileStream.CopyToAsync(ftpStream);
                }

                return remoteFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError($"FTP Upload Error: {ex.Message}");
                throw;
            }
        }

        public async Task<string> UploadBytesAsync(byte[] bytes, string fileName, string remoteDirectory = "")
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Bytes array is empty");

            string originalFileName = Path.GetFileName(fileName);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
            string fileExt = Path.GetExtension(originalFileName);

            remoteDirectory = remoteDirectory.TrimStart('/');
            string remoteFilePath = string.IsNullOrEmpty(remoteDirectory)
                ? originalFileName
                : $"{remoteDirectory}/{originalFileName}";

            int counter = 1;
            while (await FileExistsAsync("/" + remoteFilePath))
            {
                string newFileName = $"{fileNameWithoutExt}({counter}){fileExt}";
                remoteFilePath = string.IsNullOrEmpty(remoteDirectory)
                    ? newFileName
                    : $"{remoteDirectory}/{newFileName}";
                counter++;
            }

            string ftpUrl = $"ftp://{_ftpSettings.FtpHost}/{remoteFilePath}";

            try
            {
                if (!string.IsNullOrEmpty(remoteDirectory))
                {
                    await CreateDirectoryIfNotExistsAsync(remoteDirectory);
                }

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                request.UseBinary = true;
                request.UsePassive = true;
                request.KeepAlive = false;

                using (var memoryStream = new MemoryStream(bytes))
                using (var ftpStream = await request.GetRequestStreamAsync())
                {
                    await memoryStream.CopyToAsync(ftpStream);
                }

                return remoteFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError($"FTP UploadBytes Error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string remoteFilePath)
        {
            var ftpUrl = $"ftp://{_ftpSettings.FtpHost}/{remoteFilePath}";

            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.DeleteFile;
                request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                _logger.LogInformation($"FTP URL: ftp://{_ftpSettings.FtpHost}/{remoteFilePath}");

                using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    //_logger.LogInformation($"Delete Complete, status: {response.StatusDescription}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"FTP Delete Error: {ex.Message}");
                return false;
            }
        }

        private async Task CreateDirectoryIfNotExistsAsync(string remoteDirectory)
        {
            var directories = remoteDirectory.Split('/');
            var currentPath = "";

            foreach (var dir in directories)
            {
                if (string.IsNullOrEmpty(dir)) continue;

                currentPath += $"/{dir}";
                var ftpUrl = $"ftp://{_ftpSettings.FtpHost}{currentPath}";

                try
                {
                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                    request.Method = WebRequestMethods.Ftp.MakeDirectory;
                    request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                    request.UsePassive = true;

                    using (var response = (FtpWebResponse)await request.GetResponseAsync())
                    {
                        _logger.LogInformation($"Created directory: {currentPath}");
                    }
                }
                catch (WebException ex) when (ex.Response is FtpWebResponse response)
                {
                    if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                    {
                        _logger.LogInformation($"Directory already exists: {currentPath}");
                    }
                    else
                    {
                        _logger.LogWarning($"Unexpected error creating directory {currentPath}: {response.StatusDescription}");
                    }
                }
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var ftpUrl = $"ftp://{_ftpSettings.FtpHost}/";
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                request.UsePassive = true;

                using (var response = (FtpWebResponse)await request.GetResponseAsync())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var result = await reader.ReadToEndAsync();
                    //_logger.LogInformation($"FTP Connection successful. Root directory content:\n{result}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"FTP Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task<Stream> DownloadAsync(string remoteFilePath)
        {
            if (string.IsNullOrWhiteSpace(remoteFilePath))
                throw new ArgumentException("Invalid file path");

            if (!remoteFilePath.StartsWith("/"))
                remoteFilePath = "/" + remoteFilePath;

            var ftpUrl = $"ftp://{_ftpSettings.FtpHost}{remoteFilePath}";

            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                request.UseBinary = true;
                request.UsePassive = true;
                request.KeepAlive = false;

                using (var response = (FtpWebResponse)await request.GetResponseAsync())
                using (var responseStream = response.GetResponseStream())
                {
                    var memoryStream = new MemoryStream();
                    await responseStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    _logger.LogInformation($"Download Complete, status: {response.StatusDescription}");
                    return memoryStream;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"FTP Download Error: {ex.Message}");
                throw;
            }
        }

        public async Task<List<FtpFileInfo>> ListFilesInDirectoryAsync(string remoteDirectory)
        {
            var fileList = new List<FtpFileInfo>();

            remoteDirectory = remoteDirectory?.TrimStart('/') ?? "";
            var ftpUrl = string.IsNullOrEmpty(remoteDirectory)
                ? $"ftp://{_ftpSettings.FtpHost}/"
                : $"ftp://{_ftpSettings.FtpHost}/{remoteDirectory}";

            try
            {
                if (!await DirectoryExistsAsync(remoteDirectory))
                {
                    _logger.LogWarning($"Directory does not exist: {ftpUrl}");
                    return fileList;
                }

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                request.UsePassive = true;
                request.KeepAlive = false;

                using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var fileInfo = ParseFtpListLine(line, remoteDirectory);
                        if (fileInfo != null)
                        {
                            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".jfif" };
                            var extension = Path.GetExtension(fileInfo.FileName).ToLowerInvariant();

                            if (!fileInfo.IsDirectory && imageExtensions.Contains(extension))
                            {
                                fileList.Add(fileInfo);
                            }
                        }
                    }
                }

                _logger.LogInformation($"Found {fileList.Count} image(s) in directory");
                return fileList;
            }
            catch (WebException ex) when (ex.Response is FtpWebResponse ftpResponse)
            {
                if (ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    _logger.LogWarning($"Directory not found or empty: {ftpUrl}");
                    return fileList;
                }

                _logger.LogError($"FTP Error ({ftpResponse.StatusCode}): {ftpResponse.StatusDescription}");
                return fileList;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error listing directory: {ex.Message}");
                return fileList;
            }
        }

        private FtpFileInfo ParseFtpListLine(string line, string currentDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line) || line.Contains(" .") || line.Contains(" .."))
                    return null;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 9)
                {
                    return ParseWindowsFormat(line, currentDirectory);
                }

                var permissions = parts[0];
                var isDirectory = permissions.StartsWith("d");

                var dateTimeParts = 0;
                for (int i = 5; i < parts.Length - 1; i++)
                {
                    if (DateTime.TryParse($"{parts[i]} {parts[i + 1]} {parts[i + 2]}", out _))
                    {
                        dateTimeParts = 3;
                        break;
                    }
                    if (DateTime.TryParse($"{parts[i]} {parts[i + 1]}", out _))
                    {
                        dateTimeParts = 2;
                        break;
                    }
                }

                var fileNameStartIndex = 5 + dateTimeParts;
                if (fileNameStartIndex >= parts.Length) return null;

                var fileName = string.Join(" ", parts.Skip(fileNameStartIndex));

                if (string.IsNullOrEmpty(fileName) || fileName == "." || fileName == "..")
                    return null;

                var fileInfo = new FtpFileInfo
                {
                    FileName = fileName,
                    FullPath = string.IsNullOrEmpty(currentDirectory) ? fileName : $"{currentDirectory}/{fileName}",
                    IsDirectory = isDirectory,
                    Size = isDirectory ? 0 : (long.TryParse(parts[4], out var size) ? size : 0),
                    FileType = isDirectory ? "directory" : Path.GetExtension(fileName).ToLowerInvariant().TrimStart('.')
                };

                if (DateTime.TryParse($"{parts[5]} {parts[6]} {parts[7]}", out var modDate))
                {
                    fileInfo.ModifiedDate = modDate;
                }

                return fileInfo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to parse FTP line: {line}. Error: {ex.Message}");
                return null;
            }
        }

        private FtpFileInfo ParseWindowsFormat(string line, string currentDirectory)
        {
            try
            {
                // Windows format: 12-12-2023 12:00PM <DIR> FolderName
                // Windows format: 12-12-2023 12:00PM 12345 fileName.jpg
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 4) return null;

                var isDirectory = line.Contains("<DIR>");
                var size = 0L;
                var fileNameStartIndex = 3;

                if (!isDirectory)
                {
                    if (long.TryParse(parts[2], out var fileSize))
                    {
                        size = fileSize;
                    }
                    fileNameStartIndex = 3;
                }
                else
                {
                    fileNameStartIndex = 3; // Bỏ qua <DIR>
                }

                var fileName = string.Join(" ", parts.Skip(fileNameStartIndex));

                if (string.IsNullOrEmpty(fileName) || fileName == "." || fileName == "..")
                    return null;

                DateTime? modDate = null;
                if (DateTime.TryParse($"{parts[0]} {parts[1]}", out var parsedDate))
                {
                    modDate = parsedDate;
                }

                return new FtpFileInfo
                {
                    FileName = fileName,
                    FullPath = string.IsNullOrEmpty(currentDirectory) ? fileName : $"{currentDirectory}/{fileName}",
                    IsDirectory = isDirectory,
                    Size = size,
                    ModifiedDate = modDate,
                    FileType = isDirectory ? "directory" : Path.GetExtension(fileName).ToLowerInvariant().TrimStart('.')
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> MoveFileAsync(string sourceFilePath, string destinationFilePath)
        {
            try
            {
                if (!sourceFilePath.StartsWith("/"))
                    sourceFilePath = "/" + sourceFilePath;
                if (!destinationFilePath.StartsWith("/"))
                    destinationFilePath = "/" + destinationFilePath;

                var sourceFtpUrl = $"ftp://{_ftpSettings.FtpHost}{sourceFilePath}";
                var destFtpUrl = $"ftp://{_ftpSettings.FtpHost}{destinationFilePath}";

                _logger.LogInformation($"Attempting to move file: {sourceFtpUrl} -> {destFtpUrl}");

                if (!await FileExistsAsync(sourceFilePath))
                {
                    _logger.LogError($"Source file does not exist: {sourceFilePath}");
                    return false;
                }

                var destDirectory = Path.GetDirectoryName(destinationFilePath)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(destDirectory))
                {
                    await CreateDirectoryIfNotExistsAsync(destDirectory.TrimStart('/'));
                }

                if (!await DirectoryExistsAsync(destDirectory.TrimStart('/')))
                {
                    _logger.LogError($"Destination directory does not exist: {destDirectory}");
                    return false;
                }

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(sourceFtpUrl);
                request.Method = WebRequestMethods.Ftp.Rename;
                request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                request.RenameTo = destinationFilePath.TrimStart('/');
                request.UsePassive = true;
                request.KeepAlive = false;

                using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    _logger.LogInformation($"Move Complete: {sourceFilePath} -> {destinationFilePath}, Status: {response.StatusDescription}");
                    return true;
                }
            }
            catch (WebException ex) when (ex.Response is FtpWebResponse ftpResponse)
            {
                _logger.LogError($"FTP Move Error ({ftpResponse.StatusCode}): {ftpResponse.StatusDescription}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"FTP Move Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CopyFileAsync(string sourceFilePath, string destinationFilePath)
        {
            try
            {
                if (!sourceFilePath.StartsWith("/"))
                    sourceFilePath = "/" + sourceFilePath;
                if (!destinationFilePath.StartsWith("/"))
                    destinationFilePath = "/" + destinationFilePath;

                using (var sourceStream = await DownloadAsync(sourceFilePath))
                {
                    var destDirectory = Path.GetDirectoryName(destinationFilePath)?.Replace("\\", "/");
                    if (!string.IsNullOrEmpty(destDirectory))
                    {
                        await CreateDirectoryIfNotExistsAsync(destDirectory.TrimStart('/'));
                    }

                    var destFtpUrl = $"ftp://{_ftpSettings.FtpHost}{destinationFilePath}";

                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(destFtpUrl);
                    request.Method = WebRequestMethods.Ftp.UploadFile;
                    request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                    request.UseBinary = true;
                    request.UsePassive = true;
                    request.KeepAlive = false;

                    using (var ftpStream = await request.GetRequestStreamAsync())
                    {
                        await sourceStream.CopyToAsync(ftpStream);
                    }

                    _logger.LogInformation($"File copied: {sourceFilePath} -> {destinationFilePath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"FTP Copy Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteFileByUrlAsync(string fileUrl)
        {
            try
            {
                var uri = new Uri(fileUrl);
                var ftpPath = uri.AbsolutePath;

                return await DeleteFileAsync(ftpPath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Delete file by URL error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> FileExistsAsync(string filePath)
        {
            try
            {
                var ftpUrl = $"ftp://{_ftpSettings.FtpHost}{filePath}";
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.GetFileSize;
                request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                request.UsePassive = true;

                using (var response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    return true;
                }
            }
            catch (WebException ex) when (ex.Response is FtpWebResponse ftpResponse)
            {
                if (ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                    return false;
                throw;
            }
        }

        private async Task<bool> DirectoryExistsAsync(string directoryPath)
        {
            try
            {
                var ftpUrl = $"ftp://{_ftpSettings.FtpHost}/{directoryPath}";
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(_ftpSettings.FtpUsername, _ftpSettings.FtpPassword);
                request.UsePassive = true;

                using (var response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    return true;
                }
            }
            catch (WebException ex) when (ex.Response is FtpWebResponse ftpResponse)
            {
                if (ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                    return false;
                throw;
            }
        }
    }
}
