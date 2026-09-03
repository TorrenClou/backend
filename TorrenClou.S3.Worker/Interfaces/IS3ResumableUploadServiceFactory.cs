using Amazon.S3;
using TorrenClou.Core.Interfaces;

namespace TorrenClou.S3.Worker.Interfaces
{
    /// <summary>
    /// Factory for creating IS3ResumableUploadService instances with user-specific S3 clients
    /// </summary>
    public interface IS3ResumableUploadServiceFactory
    {
        /// <summary>
        /// Creates a new S3ResumableUploadService instance with the provided S3 client
        /// </summary>
        /// <param name="s3Client">Configured AmazonS3 client with user credentials</param>
        IS3ResumableUploadService Create(IAmazonS3 s3Client);
    }
}
