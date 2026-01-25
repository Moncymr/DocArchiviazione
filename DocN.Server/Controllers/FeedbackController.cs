using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocN.Data;
using DocN.Data.Models;

namespace DocN.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(ApplicationDbContext context, ILogger<FeedbackController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Submit user feedback on a RAG response
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitFeedback([FromBody] SubmitFeedbackRequest request)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            var feedback = new ResponseFeedback
            {
                UserId = request.UserId,
                Query = request.Query,
                Response = request.Response,
                ConfidenceScore = request.ConfidenceScore,
                IsHelpful = request.IsHelpful,
                Comment = request.Comment,
                SourceDocumentIds = request.SourceDocumentIds,
                SourceChunkIds = request.SourceChunkIds,
                TenantId = user.TenantId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ResponseFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Feedback submitted for query: {Query} by user: {UserId}", 
                request.Query, request.UserId);

            return Ok(new { id = feedback.Id, message = "Feedback submitted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting feedback");
            return StatusCode(500, "An error occurred while submitting feedback");
        }
    }

    /// <summary>
    /// Get feedback statistics
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Policy = "AdminUsers")]
    public async Task<IActionResult> GetFeedbackStats()
    {
        try
        {
            var totalFeedback = await _context.ResponseFeedbacks.CountAsync();
            var helpfulCount = await _context.ResponseFeedbacks.CountAsync(f => f.IsHelpful);
            var notHelpfulCount = await _context.ResponseFeedbacks.CountAsync(f => !f.IsHelpful);
            var averageConfidence = await _context.ResponseFeedbacks.AverageAsync(f => f.ConfidenceScore);
            
            var lowConfidenceFeedback = await _context.ResponseFeedbacks
                .Where(f => f.ConfidenceScore < 50)
                .CountAsync();

            return Ok(new
            {
                totalFeedback,
                helpfulCount,
                notHelpfulCount,
                helpfulPercentage = totalFeedback > 0 ? (helpfulCount * 100.0 / totalFeedback) : 0,
                averageConfidence,
                lowConfidenceFeedback
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting feedback stats");
            return StatusCode(500, "An error occurred while getting feedback stats");
        }
    }

    public class SubmitFeedbackRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public bool IsHelpful { get; set; }
        public string? Comment { get; set; }
        public string? SourceDocumentIds { get; set; }
        public string? SourceChunkIds { get; set; }
    }
}
