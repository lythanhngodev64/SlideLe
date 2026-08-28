using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SlideLe;

/// <summary>
/// Reads public PowerPoint files from a GitHub folder URL and streams individual downloads.
/// </summary>
internal sealed class GitHubFolderClient
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<IReadOnlyList<PresentationFile>> GetPptxFilesAsync(
        string sourceUrl,
        CancellationToken cancellationToken) =>
        GetPptxFilesAsync(
            sourceUrl,
            includeSubdirectories: true,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Gets PowerPoint files in the requested folder, optionally including its descendants.
    /// </summary>
    public async Task<IReadOnlyList<PresentationFile>> GetPptxFilesAsync(
        string sourceUrl,
        bool includeSubdirectories,
        CancellationToken cancellationToken)
    {
        GitHubFolderAddress address = GitHubFolderAddress.Parse(sourceUrl);
        GitHubCommitSnapshot snapshot = await GetCommitSnapshotAsync(address, cancellationToken);
        string targetTreeSha = await GetFolderTreeShaAsync(
            address,
            snapshot.RootTreeSha,
            cancellationToken);
        IReadOnlyList<GitHubTreeItem> items = await GetTreeItemsAsync(
            address,
            targetTreeSha,
            includeSubdirectories,
            cancellationToken);

        return items
            .Where(item =>
                string.Equals(item.Type, "blob", StringComparison.Ordinal) &&
                !string.Equals(item.Mode, "120000", StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(item.Path) &&
                item.Path.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase))
            .Select(item => CreatePresentationFile(address, item.Path!, item.Size, snapshot.CommitSha))
            .OrderBy(file => file.RelativePath, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private async Task<string> GetFolderTreeShaAsync(
        GitHubFolderAddress address,
        string rootTreeSha,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(address.FolderPath))
        {
            return rootTreeSha;
        }

        string currentTreeSha = rootTreeSha;
        foreach (string folderName in address.FolderPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            IReadOnlyList<GitHubTreeItem> items = await GetTreeItemsAsync(
                address,
                currentTreeSha,
                includeSubdirectories: false,
                cancellationToken);
            GitHubTreeItem? folder = items.FirstOrDefault(item =>
                string.Equals(item.Type, "tree", StringComparison.Ordinal) &&
                string.Equals(item.Path, folderName, StringComparison.Ordinal));

            if (folder?.Sha is not { Length: > 0 } folderTreeSha)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy thư mục '{address.FolderPath}' trong nhánh '{address.Reference}'.");
            }

            currentTreeSha = folderTreeSha;
        }

        return currentTreeSha;
    }

    private async Task<IReadOnlyList<GitHubTreeItem>> GetTreeItemsAsync(
        GitHubFolderAddress address,
        string treeSha,
        bool includeSubdirectories,
        CancellationToken cancellationToken)
    {
        Uri requestUri = CreateTreeApiUri(address, treeSha, includeSubdirectories);

        using HttpResponseMessage response = await HttpClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        EnsureSuccess(response);

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        GitHubTreeResponse treeResponse = await JsonSerializer.DeserializeAsync<GitHubTreeResponse>(
                responseStream,
                JsonOptions,
                cancellationToken)
            ?? throw new InvalidOperationException("GitHub trả về dữ liệu danh sách không hợp lệ.");

        if (treeResponse.Truncated)
        {
            throw new InvalidOperationException(
                "Thư mục có quá nhiều tệp, GitHub không trả đủ danh sách để quét an toàn.");
        }

        return treeResponse.Tree
            ?? throw new InvalidOperationException("GitHub không trả về cây thư mục.");
    }

    public async Task DownloadAsync(
        PresentationFile file,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Chưa chọn vị trí lưu tệp.", nameof(destinationPath));
        }

        using HttpResponseMessage response = await HttpClient.GetAsync(
            file.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        EnsureSuccess(response);

        string fullDestinationPath = Path.GetFullPath(destinationPath);
        string destinationDirectory = Path.GetDirectoryName(fullDestinationPath)
            ?? throw new InvalidOperationException("Không xác định được thư mục lưu tệp.");
        Directory.CreateDirectory(destinationDirectory);

        string temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.partial");

        try
        {
            await using Stream sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            long copiedSize;
            await using (var destinationStream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await sourceStream.CopyToAsync(destinationStream, 81920, cancellationToken);
                await destinationStream.FlushAsync(cancellationToken);
                copiedSize = destinationStream.Length;
            }

            if (file.SizeInBytes is long expectedSize && copiedSize != expectedSize)
            {
                throw new IOException(
                    $"Tệp tải về không đầy đủ (nhận {copiedSize:N0} byte, cần {expectedSize:N0} byte).");
            }

            File.Move(temporaryPath, fullDestinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SlideLe", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private async Task<GitHubCommitSnapshot> GetCommitSnapshotAsync(
        GitHubFolderAddress address,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await HttpClient.GetAsync(
            CreateCommitApiUri(address),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        EnsureSuccess(response);

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        GitHubCommitResponse commitResponse = await JsonSerializer.DeserializeAsync<GitHubCommitResponse>(
                responseStream,
                JsonOptions,
                cancellationToken)
            ?? throw new InvalidOperationException("GitHub không trả về thông tin phiên bản của nhánh.");

        string commitSha = !string.IsNullOrWhiteSpace(commitResponse.Sha)
            ? commitResponse.Sha
            : throw new InvalidOperationException("GitHub không trả về mã phiên bản của nhánh.");
        string rootTreeSha = commitResponse.Commit?.Tree?.Sha is { Length: > 0 } treeSha
            ? treeSha
            : throw new InvalidOperationException("GitHub không trả về cây thư mục của nhánh.");

        return new GitHubCommitSnapshot(commitSha, rootTreeSha);
    }

    private static Uri CreateCommitApiUri(GitHubFolderAddress address)
    {
        string owner = Uri.EscapeDataString(address.Owner);
        string repository = Uri.EscapeDataString(address.Repository);
        string reference = Uri.EscapeDataString(address.Reference);
        return new Uri($"https://api.github.com/repos/{owner}/{repository}/commits/{reference}");
    }

    private static Uri CreateTreeApiUri(
        GitHubFolderAddress address,
        string treeSha,
        bool includeSubdirectories)
    {
        string owner = Uri.EscapeDataString(address.Owner);
        string repository = Uri.EscapeDataString(address.Repository);
        string reference = Uri.EscapeDataString(treeSha);
        string recursiveQuery = includeSubdirectories ? "?recursive=1" : string.Empty;
        return new Uri($"https://api.github.com/repos/{owner}/{repository}/git/trees/{reference}{recursiveQuery}");
    }

    private static PresentationFile CreatePresentationFile(
        GitHubFolderAddress address,
        string relativePath,
        long? sizeInBytes,
        string commitSha)
    {
        string repositoryPath = string.IsNullOrEmpty(address.FolderPath)
            ? relativePath
            : $"{address.FolderPath}/{relativePath}";
        int lastSeparatorIndex = relativePath.LastIndexOf('/');
        string name = lastSeparatorIndex >= 0
            ? relativePath[(lastSeparatorIndex + 1)..]
            : relativePath;

        string escapedPath = string.Join(
            "/",
            repositoryPath.Split('/').Select(Uri.EscapeDataString));
        string downloadUrl = $"https://raw.githubusercontent.com/{Uri.EscapeDataString(address.Owner)}/" +
                             $"{Uri.EscapeDataString(address.Repository)}/{Uri.EscapeDataString(commitSha)}/" +
                             escapedPath;

        return new PresentationFile(name, relativePath, sizeInBytes, downloadUrl);
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                "Không tìm thấy kho mã nguồn hoặc nhánh GitHub. Hãy kiểm tra lại URL " +
                "(tên nhánh có dấu '/' cần mã hóa thành '%2F').");
        }

        bool primaryRateLimitReached = response.Headers.TryGetValues(
            "X-RateLimit-Remaining",
            out IEnumerable<string>? remaining) && remaining.FirstOrDefault() == "0";
        bool retryAfterProvided = response.Headers.RetryAfter is not null;

        if (response.StatusCode == HttpStatusCode.TooManyRequests ||
            (response.StatusCode == HttpStatusCode.Forbidden &&
             (primaryRateLimitReached || retryAfterProvided)))
        {
            throw new InvalidOperationException(CreateRateLimitMessage(response));
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                "GitHub từ chối truy cập. Hãy dùng URL của thư mục công khai hoặc thử lại sau.");
        }

        throw new HttpRequestException(
            $"GitHub trả về lỗi {(int)response.StatusCode} ({response.ReasonPhrase}).");
    }

    private static string CreateRateLimitMessage(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan retryAfter)
        {
            return $"GitHub đang tạm giới hạn yêu cầu. Vui lòng thử lại sau khoảng {Math.Ceiling(retryAfter.TotalMinutes):N0} phút.";
        }

        if (response.Headers.TryGetValues("X-RateLimit-Reset", out IEnumerable<string>? resetValues) &&
            long.TryParse(resetValues.FirstOrDefault(), out long resetUnixSeconds))
        {
            DateTimeOffset resetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnixSeconds).ToLocalTime();
            return $"GitHub đã tạm giới hạn yêu cầu. Vui lòng thử lại sau {resetTime:HH:mm}.";
        }

        return "GitHub đã tạm giới hạn yêu cầu. Vui lòng thử lại sau ít phút.";
    }

    private sealed record GitHubCommitSnapshot(string CommitSha, string RootTreeSha);

    private sealed class GitHubCommitResponse
    {
        public string? Sha { get; init; }

        public GitHubCommitDetails? Commit { get; init; }
    }

    private sealed class GitHubCommitDetails
    {
        public GitHubTreeReference? Tree { get; init; }
    }

    private sealed class GitHubTreeReference
    {
        public string? Sha { get; init; }
    }

    private sealed class GitHubTreeResponse
    {
        public bool Truncated { get; init; }

        public IReadOnlyList<GitHubTreeItem>? Tree { get; init; }
    }

    private sealed class GitHubTreeItem
    {
        public string? Mode { get; init; }

        public string? Path { get; init; }

        public string? Sha { get; init; }

        public long? Size { get; init; }

        public string? Type { get; init; }
    }
}

internal sealed record GitHubFolderAddress(
    string Owner,
    string Repository,
    string Reference,
    string FolderPath)
{
    public static GitHubFolderAddress Parse(string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl) ||
            !Uri.TryCreate(sourceUrl.Trim(), UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Host, "www.github.com", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "URL phải là liên kết HTTPS đến thư mục GitHub, ví dụ: https://github.com/chutai/kho/tree/main/Slide");
        }

        string[] segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        if (segments.Length < 4 ||
            !string.Equals(segments[2], "tree", StringComparison.OrdinalIgnoreCase) ||
            segments.Take(4).Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "URL phải có dạng https://github.com/chutai/kho/tree/nhanh/thu-muc.");
        }

        string folderPath = string.Join('/', segments.Skip(4));
        if (folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Đường dẫn thư mục GitHub không hợp lệ.");
        }

        return new GitHubFolderAddress(
            segments[0],
            segments[1],
            segments[3],
            folderPath.Trim('/'));
    }
}
