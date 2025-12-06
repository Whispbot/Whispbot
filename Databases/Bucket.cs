using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Databases
{
    public static class Bucket
    {
        public static readonly string? endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT");
        public static readonly string? bucket = Environment.GetEnvironmentVariable("S3_BUCKET");
        public static readonly string? region = Environment.GetEnvironmentVariable("S3_REGION");
        private static readonly string? accessKeyId = Environment.GetEnvironmentVariable("S3_ACCESS_KEY_ID");
        private static readonly string? secretAccessKey = Environment.GetEnvironmentVariable("S3_SECRET_ACCESS_KEY");

        public static readonly string cdnUrl = "https://cdn.whisp.bot";

        private static readonly BasicAWSCredentials _credentials = new(accessKeyId, secretAccessKey);
        private static readonly AmazonS3Config _s3Config = new()
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            UseHttp = false
        };

        public static readonly AmazonS3Client client = new(_credentials, _s3Config);

        public static string GetPublicUrl(string key)
        {
            return $"{cdnUrl}/{key}";
        }
        
        public static Task<string> GetPresignedUrl(string key, double? expiresInSeconds = null)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = key,
                Expires = expiresInSeconds is not null ? DateTime.UtcNow.AddSeconds((double)expiresInSeconds) : null
            };
            return client.GetPreSignedURLAsync(request);
        }

        public static Task<string> PutPresignedUrl(string key, string contentType, double expiresInSeconds = 300)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds),
                ContentType = contentType
            };
            return client.GetPreSignedURLAsync(request);
        }

        public static Task DeleteObject(string key)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = bucket,
                Key = key
            };
            return client.DeleteObjectAsync(request);
        }
    }
}
