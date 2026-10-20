using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text;
using Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Api.Services;

public enum FileUploadKind
{
    ListingImage,
    Avatar,
    ChatAttachment
}

public sealed record ValidatedFileUpload(string Extension, string ContentType, MessageType? MessageType = null);

/// <summary>
/// Centralized limits and content-based validation for every user-uploaded file.
/// File.ContentType and the original file name are treated as untrusted metadata.
/// </summary>
public sealed class FileUploadSecurityService
{
    public const int MaxListingImagesPerRequest = 8;
    public const long MaxListingImageSizeBytes = 8 * 1024 * 1024;
    public const long MaxAvatarSizeBytes = 5 * 1024 * 1024;
    public const long MaxChatAttachmentSizeBytes = 10 * 1024 * 1024;
    public const long MaxListingRequestBytes = 32 * 1024 * 1024;
    public const long MaxAvatarRequestBytes = 6 * 1024 * 1024;
    public const long MaxChatRequestBytes = 11 * 1024 * 1024;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Gif87Signature = Encoding.ASCII.GetBytes("GIF87a");
    private static readonly byte[] Gif89Signature = Encoding.ASCII.GetBytes("GIF89a");
    private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");
    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    public async Task<IReadOnlyList<ValidatedFileUpload>> ValidateBatchAsync(
        IReadOnlyCollection<IFormFile> files,
        FileUploadKind kind,
        CancellationToken cancellationToken = default)
    {
        var limits = GetLimits(kind);
        if (files.Count == 0)
        {
            throw new ValidationException("At least one file is required.");
        }

        if (files.Count > limits.MaxFiles)
        {
            throw new ValidationException($"You can upload up to {limits.MaxFiles} files at once.");
        }

        long totalBytes = 0;
        var result = new List<ValidatedFileUpload>(files.Count);
        foreach (var file in files)
        {
            totalBytes = checked(totalBytes + file.Length);
            if (totalBytes > limits.MaxTotalBytes)
            {
                throw new ValidationException("The total upload size is too large.");
            }

            result.Add(await ValidateAsync(file, kind, cancellationToken));
        }

        return result;
    }

    public async Task<ValidatedFileUpload> ValidateAsync(
        IFormFile file,
        FileUploadKind kind,
        CancellationToken cancellationToken = default)
    {
        var limits = GetLimits(kind);
        if (file.Length <= 0)
        {
            throw new ValidationException("The uploaded file is empty.");
        }

        if (file.Length > limits.MaxFileBytes)
        {
            throw new ValidationException("The uploaded file is too large.");
        }

        var fileName = file.FileName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.IndexOfAny(['/', '\\', '\0']) >= 0)
        {
            throw new ValidationException("The uploaded file name is invalid.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!limits.Types.TryGetValue(extension, out var type))
        {
            throw new ValidationException("This file type is not supported.");
        }

        var declaredContentType = file.ContentType?
            .Split(';', 2)[0]
            .Trim()
            .ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(declaredContentType) &&
            !type.AllowedContentTypes.Contains(declaredContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("The file content type does not match its extension.");
        }

        await EnsureContentMatchesAsync(file, extension, type.ContentType, cancellationToken);
        return new ValidatedFileUpload(extension, type.ContentType, type.MessageType);
    }

    public static string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    private static async Task EnsureContentMatchesAsync(
        IFormFile file,
        string extension,
        string expectedContentType,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[16];
        var headerLength = await ReadAtMostAsync(stream, header, cancellationToken);

        var isValid = extension switch
        {
            ".jpg" or ".jpeg" => StartsWith(header, headerLength, JpegSignature),
            ".png" => StartsWith(header, headerLength, PngSignature),
            ".webp" => headerLength >= 12 &&
                       StartsWith(header, headerLength, Encoding.ASCII.GetBytes("RIFF")) &&
                       header[8] == (byte)'W' && header[9] == (byte)'E' &&
                       header[10] == (byte)'B' && header[11] == (byte)'P',
            ".gif" => StartsWith(header, headerLength, Gif87Signature) ||
                      StartsWith(header, headerLength, Gif89Signature),
            ".pdf" => StartsWith(header, headerLength, PdfSignature),
            ".doc" => StartsWith(header, headerLength, OleSignature),
            ".docx" => await IsDocxArchiveAsync(file, cancellationToken),
            ".zip" => StartsWith(header, headerLength, ZipSignature) ||
                      (headerLength >= 4 && header[0] == 0x50 && header[1] == 0x4B &&
                       (header[2] == 0x05 || header[2] == 0x07) && header[3] == 0x06),
            ".txt" => await IsUtf8TextAsync(file, cancellationToken),
            _ => false
        };

        if (!isValid)
        {
            throw new ValidationException($"The file is not a valid {expectedContentType} file.");
        }
    }

    private static async Task<bool> IsDocxArchiveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = file.OpenReadStream();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            return archive.GetEntry("[Content_Types].xml") is not null &&
                   archive.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static async Task<bool> IsUtf8TextAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);

        try
        {
            var buffer = new char[4096];
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                if (buffer.AsSpan(0, read).Contains('\0'))
                {
                    return false;
                }
            }

            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static async Task<int> ReadAtMostAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var current = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (current == 0)
            {
                break;
            }

            read += current;
        }

        return read;
    }

    private static bool StartsWith(byte[] value, int valueLength, byte[] prefix)
    {
        if (valueLength < prefix.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix.Length; index++)
        {
            if (value[index] != prefix[index])
            {
                return false;
            }
        }

        return true;
    }

    private static UploadLimits GetLimits(FileUploadKind kind)
    {
        return kind switch
        {
            FileUploadKind.ListingImage => new UploadLimits(
                MaxFiles: MaxListingImagesPerRequest,
                MaxFileBytes: MaxListingImageSizeBytes,
                MaxTotalBytes: MaxListingRequestBytes,
                Types: new Dictionary<string, FileType>(StringComparer.OrdinalIgnoreCase)
                {
                    [".jpg"] = new("image/jpeg", ["image/jpeg", "image/pjpeg"]),
                    [".jpeg"] = new("image/jpeg", ["image/jpeg", "image/pjpeg"]),
                    [".png"] = new("image/png", ["image/png"]),
                    [".webp"] = new("image/webp", ["image/webp"])
                }),
            FileUploadKind.Avatar => new UploadLimits(
                MaxFiles: 1,
                MaxFileBytes: MaxAvatarSizeBytes,
                MaxTotalBytes: MaxAvatarRequestBytes,
                Types: new Dictionary<string, FileType>(StringComparer.OrdinalIgnoreCase)
                {
                    [".jpg"] = new("image/jpeg", ["image/jpeg", "image/pjpeg"]),
                    [".jpeg"] = new("image/jpeg", ["image/jpeg", "image/pjpeg"]),
                    [".png"] = new("image/png", ["image/png"]),
                    [".webp"] = new("image/webp", ["image/webp"])
                }),
            FileUploadKind.ChatAttachment => new UploadLimits(
                MaxFiles: 1,
                MaxFileBytes: MaxChatAttachmentSizeBytes,
                MaxTotalBytes: MaxChatRequestBytes,
                Types: new Dictionary<string, FileType>(StringComparer.OrdinalIgnoreCase)
                {
                    [".jpg"] = new("image/jpeg", ["image/jpeg", "image/pjpeg"], MessageType.Image),
                    [".jpeg"] = new("image/jpeg", ["image/jpeg", "image/pjpeg"], MessageType.Image),
                    [".png"] = new("image/png", ["image/png"], MessageType.Image),
                    [".webp"] = new("image/webp", ["image/webp"], MessageType.Image),
                    [".gif"] = new("image/gif", ["image/gif"], MessageType.Image),
                    [".pdf"] = new("application/pdf", ["application/pdf"], MessageType.File),
                    [".doc"] = new("application/msword", ["application/msword"], MessageType.File),
                    [".docx"] = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"], MessageType.File),
                    [".txt"] = new("text/plain", ["text/plain"], MessageType.File),
                    [".zip"] = new("application/zip", ["application/zip", "application/x-zip-compressed"], MessageType.File)
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private sealed record UploadLimits(
        int MaxFiles,
        long MaxFileBytes,
        long MaxTotalBytes,
        IReadOnlyDictionary<string, FileType> Types);

    private sealed record FileType(
        string ContentType,
        IReadOnlyCollection<string> AllowedContentTypes,
        MessageType? MessageType = null);
}
