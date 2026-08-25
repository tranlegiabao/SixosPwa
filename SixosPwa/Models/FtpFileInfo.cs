#nullable disable
namespace SixosPwa.Models
{
    public class FtpFileInfo
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public long Size { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool IsDirectory { get; set; }
        public string FileType { get; set; }
    }

    public class ListImagesRequest
    {
        public string DirectoryPath { get; set; } = "";
    }
}