using LinksApi.Models;
using System.Collections.Concurrent;

namespace LinksApi.Services;

public class ValidationJobManager
{
    private static readonly ConcurrentDictionary<string, ValidationJob> _jobs = new();

    public ValidationJob CreateJob(int totalLinks, int totalBatches)
    {
        var job = new ValidationJob
        {
            JobId = Guid.NewGuid().ToString(),
            Status = "queued",
            StartedAt = DateTime.UtcNow,
            TotalLinks = totalLinks,
            TotalBatches = totalBatches
        };

        _jobs.TryAdd(job.JobId, job);
        return job;
    }

    public ValidationJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public void UpdateJobProgress(string jobId, int processedLinks, int validLinks, int brokenLinks, int currentBatch)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.ProcessedLinks = processedLinks;
            job.ValidLinks = validLinks;
            job.BrokenLinks = brokenLinks;
            job.CurrentBatch = currentBatch;
        }
    }

    public void UpdateJobStatus(string jobId, string status, string? errorMessage = null)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = status;
            if (status == "completed" || status == "failed")
            {
                job.CompletedAt = DateTime.UtcNow;
            }
            if (errorMessage != null)
            {
                job.ErrorMessage = errorMessage;
            }
        }
    }

    public List<ValidationJob> GetAllJobs()
    {
        return _jobs.Values.OrderByDescending(j => j.StartedAt).ToList();
    }
}
