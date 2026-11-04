namespace CloudHubs.Application.Modules.MeroSpace.DTOs;

public sealed record MeroSpaceCommentReplyDto(
    Guid Id,
    string AuthorName,
    string AuthorEmail,
    string Content,
    DateTime CreatedAt);

public sealed record MeroSpaceCommentDto(
    Guid Id,
    string FilePath,
    int LineNumber,
    string? SelectedText,
    string Content,
    string AuthorName,
    string AuthorEmail,
    DateTime CreatedAt,
    bool IsResolved,
    IReadOnlyList<MeroSpaceCommentReplyDto> Replies);

public sealed record CreateMeroSpaceCommentRequest(
    string FilePath,
    int LineNumber,
    string? SelectedText,
    string Content);

public sealed record ReplyMeroSpaceCommentRequest(
    string Content);

