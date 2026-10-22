using System.Text;
using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Infrastructure.Services;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-184: bình luận/trả lời/@mention, tệp đính kèm, người theo dõi.</summary>
public class IssueCollaborationServiceTests
{
    private readonly InMemoryIssueRepository _issues = new();
    private readonly InMemoryCollaborationRepository _collaboration = new();
    private readonly InMemoryAttachmentStorage _storage = new();
    private readonly IssueCollaborationService _service;

    public IssueCollaborationServiceTests()
    {
        _service = new IssueCollaborationService(_issues, _collaboration, new SharedUserLookupService(), new SharedPermissionEvaluator(),
            _storage, new AttachmentOptions { MaxFileSizeBytes = 1024 }, new CreateCommentDtoValidator(), new UpdateCommentDtoValidator(),
            new FixedTimeProvider(new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc)));
    }

    private Task<Result<CommentDto>> Comment(string content, Guid? parent = null, params Guid[] mentions) =>
        _service.AddCommentAsync(SharedTestData.StoryIssueId,
            new CreateCommentDto { Content = content, ParentCommentId = parent, MentionedUserIds = [.. mentions] },
            SharedTestData.DeveloperUserId);

    [Fact]
    public async Task CommentKeepsOnlyMemberMentionsLogsActivityAndWatchesAuthor()
    {
        var result = await Comment("  @Owner xem giúp  ", null, SharedTestData.OwnerUserId, SharedTestData.OutsiderUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal("@Owner xem giúp", result.Value.Content);
        Assert.Equal("Demo Developer", result.Value.AuthorName);
        Assert.Equal([SharedTestData.OwnerUserId], result.Value.MentionedUserIds);
        Assert.Equal("CommentAdded", Assert.Single(_issues.Activities).Action);
        Assert.Contains(_collaboration.Watchers, w => w.UserId == SharedTestData.DeveloperUserId);
        Assert.Equal(1, _collaboration.SaveCount);
    }

    [Fact]
    public async Task RepliesAttachToRootCommentOnly()
    {
        var root = (await Comment("Gốc")).Value;
        var reply = await _service.ReplyAsync(root.Id, new CreateCommentDto { Content = "Trả lời" }, SharedTestData.OwnerUserId);
        var nested = await _service.ReplyAsync(reply.Value.Id, new CreateCommentDto { Content = "Trả lời lồng" }, SharedTestData.OwnerUserId);

        Assert.Equal(root.Id, reply.Value.ParentCommentId);
        Assert.Equal(root.Id, nested.Value.ParentCommentId);
    }

    [Fact]
    public async Task DeletedCommentWithRepliesStaysAsPlaceholder()
    {
        var root = (await Comment("Sẽ bị xóa")).Value;
        await _service.ReplyAsync(root.Id, new CreateCommentDto { Content = "Còn lại" }, SharedTestData.OwnerUserId);
        var lonely = (await Comment("Không có trả lời")).Value;

        Assert.True((await _service.DeleteCommentAsync(root.Id, SharedTestData.DeveloperUserId, canModerate: false)).IsSuccess);
        Assert.True((await _service.DeleteCommentAsync(lonely.Id, SharedTestData.OwnerUserId, canModerate: true)).IsSuccess);

        var comments = (await _service.GetCommentsAsync(SharedTestData.StoryIssueId)).Value;
        Assert.Equal(2, comments.Count);
        var placeholder = comments.Single(c => c.Id == root.Id);
        Assert.True(placeholder.IsDeleted);
        Assert.Equal(string.Empty, placeholder.Content);
    }

    [Fact]
    public async Task OnlyAuthorEditsAndModeratorsDeleteOthers()
    {
        var comment = (await Comment("Bản đầu")).Value;

        Assert.Equal(ErrorType.Forbidden, (await _service.UpdateCommentAsync(comment.Id,
            new UpdateCommentDto { Content = "Sửa trộm" }, SharedTestData.OwnerUserId)).Error.Type);
        Assert.Equal(ErrorType.Forbidden, (await _service.DeleteCommentAsync(comment.Id, SharedTestData.OwnerUserId, canModerate: false)).Error.Type);

        var edited = await _service.UpdateCommentAsync(comment.Id, new UpdateCommentDto { Content = "Bản sửa" }, SharedTestData.DeveloperUserId);
        Assert.True(edited.Value.IsEdited);
        Assert.Equal("Bản sửa", edited.Value.Content);
    }

    [Fact]
    public async Task CommentValidationAndParentChecks()
    {
        Assert.Equal("COMMENT_CONTENT_REQUIRED", (await Comment("   ")).Error.Code);
        Assert.Equal("COMMENT_PARENT_INVALID", (await Comment("x", Guid.NewGuid())).Error.Code);

        var otherIssueComment = await _service.AddCommentAsync(SharedTestData.BugIssueId,
            new CreateCommentDto { Content = "Ở Issue khác" }, SharedTestData.OwnerUserId);
        Assert.Equal("COMMENT_PARENT_INVALID", (await Comment("x", otherIssueComment.Value.Id)).Error.Code);
        Assert.Equal("ISSUE_NOT_FOUND", (await _service.AddCommentAsync(Guid.NewGuid(),
            new CreateCommentDto { Content = "x" }, SharedTestData.OwnerUserId)).Error.Code);
    }

    [Fact]
    public async Task UploadStoresUnderGeneratedNameWithSafeDisplayNameAndCanBeDownloaded()
    {
        var bytes = Encoding.UTF8.GetBytes("nội dung");
        var result = await _service.UploadAttachmentAsync(SharedTestData.StoryIssueId, @"..\..\secret\Bao cao.PDF",
            bytes.Length, new MemoryStream(bytes), SharedTestData.DeveloperUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Bao cao.PDF", result.Value.FileName);
        Assert.Equal("application/pdf", result.Value.ContentType);
        var stored = Assert.Single(_storage.Files).Key;
        Assert.Equal(result.Value.Id.ToString("N") + ".pdf", stored);

        var opened = await _service.OpenAttachmentAsync(result.Value.Id);
        using var reader = new StreamReader(opened.Value.Content);
        Assert.Equal("nội dung", await reader.ReadToEndAsync());

        Assert.True((await _service.DeleteAttachmentAsync(result.Value.Id, SharedTestData.OwnerUserId)).IsSuccess);
        Assert.Empty(_storage.Files);
        Assert.Equal(["AttachmentAdded", "AttachmentRemoved"], _issues.Activities.Select(a => a.Action));
    }

    [Theory]
    [InlineData("virus.exe", 10)]
    [InlineData("big.pdf", 2048)]
    [InlineData("empty.pdf", 0)]
    public async Task UploadRejectsDisallowedTypeOrSize(string fileName, long length)
    {
        var result = await _service.UploadAttachmentAsync(SharedTestData.StoryIssueId, fileName, length, new MemoryStream(new byte[length]), SharedTestData.OwnerUserId);
        Assert.Equal("ATTACHMENT_FILE_INVALID", result.Error.Code);
        Assert.Empty(_storage.Files);
        Assert.Empty(_collaboration.Attachments);
    }

    [Fact]
    public async Task WatchAndUnwatchAreIdempotent()
    {
        Assert.Equal(1, (await _service.WatchAsync(SharedTestData.StoryIssueId, SharedTestData.ViewerUserId)).Value.WatcherCount);
        Assert.Equal(1, (await _service.WatchAsync(SharedTestData.StoryIssueId, SharedTestData.ViewerUserId)).Value.WatcherCount);
        Assert.Equal("Demo Viewer", Assert.Single((await _service.GetWatchersAsync(SharedTestData.StoryIssueId)).Value).DisplayName);

        var off = await _service.UnwatchAsync(SharedTestData.StoryIssueId, SharedTestData.ViewerUserId);
        Assert.False(off.Value.IsWatching);
        Assert.Equal(0, (await _service.UnwatchAsync(SharedTestData.StoryIssueId, SharedTestData.ViewerUserId)).Value.WatcherCount);
    }

    [Fact]
    public async Task LocalStorageRefusesToEscapeRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "pm-attach-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalAttachmentStorage(root);
            await storage.SaveAsync(@"..\..\evil.txt", new MemoryStream([1, 2, 3]));
            Assert.True(File.Exists(Path.Combine(root, "evil.txt")));
            Assert.False(File.Exists(Path.Combine(root, "..", "evil.txt")));
            using (var opened = storage.OpenRead("evil.txt"))
            {
                Assert.NotNull(opened);
            }

            storage.Delete("evil.txt");
            Assert.Null(storage.OpenRead("evil.txt"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
